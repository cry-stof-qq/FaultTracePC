using System.Diagnostics.Eventing.Reader;
using System.Text.RegularExpressions;

namespace FaultTracePC.Core.Collectors;

/// <summary>
/// Interroge le journal d'événements Windows de façon ciblée (requêtes XPath par
/// fournisseur/ID) plutôt que de balayer tout le journal : rapide et léger.
/// </summary>
public sealed class EventLogCollector
{
    private readonly List<string> _errors;

    public EventLogCollector(List<string> errors) => _errors = errors;

    public List<WinEvent> Collect(int days)
    {
        var events = new List<WinEvent>();
        long ms = (long)TimeSpan.FromDays(days).TotalMilliseconds;

        // --- Journal Système -------------------------------------------------
        Collect(events, "System", $"*[System[Provider[@Name='Microsoft-Windows-WER-SystemErrorReporting'] and (EventID=1001) and TimeCreated[timediff(@SystemTime) <= {ms}]]]", EventCategory.Bsod);
        Collect(events, "System", $"*[System[Provider[@Name='Microsoft-Windows-Kernel-Power'] and (EventID=41) and TimeCreated[timediff(@SystemTime) <= {ms}]]]", EventCategory.PowerLoss);
        Collect(events, "System", $"*[System[Provider[@Name='EventLog'] and (EventID=6008) and TimeCreated[timediff(@SystemTime) <= {ms}]]]", EventCategory.UnexpectedShutdown);
        Collect(events, "System", $"*[System[Provider[@Name='Microsoft-Windows-WHEA-Logger'] and TimeCreated[timediff(@SystemTime) <= {ms}]]]", EventCategory.Whea);
        Collect(events, "System", $"*[System[Provider[@Name='Display'] and (EventID=4101) and TimeCreated[timediff(@SystemTime) <= {ms}]]]", EventCategory.Tdr);
        Collect(events, "System", $"*[System[(Provider[@Name='disk'] or Provider[@Name='Ntfs'] or Provider[@Name='volmgr'] or Provider[@Name='stornvme'] or Provider[@Name='storahci'] or Provider[@Name='iaStorA'] or Provider[@Name='iaStorAC']) and (Level=1 or Level=2 or Level=3) and TimeCreated[timediff(@SystemTime) <= {ms}]]]", EventCategory.DiskError);
        Collect(events, "System", $"*[System[Provider[@Name='Service Control Manager'] and (EventID=7000 or EventID=7001 or EventID=7031 or EventID=7034) and TimeCreated[timediff(@SystemTime) <= {ms}]]]", EventCategory.ServiceFailure);
        Collect(events, "System", $"*[System[Provider[@Name='Microsoft-Windows-MemoryDiagnostics-Results'] and TimeCreated[timediff(@SystemTime) <= {ms}]]]", EventCategory.MemoryDiag);
        Collect(events, "System", $"*[System[Provider[@Name='Microsoft-Windows-WindowsUpdateClient'] and (EventID=19 or EventID=20) and TimeCreated[timediff(@SystemTime) <= {ms}]]]", EventCategory.WindowsUpdate);
        // Saturation de la mémoire virtuelle : Windows nomme les processus les plus gourmands.
        Collect(events, "System", $"*[System[Provider[@Name='Microsoft-Windows-Resource-Exhaustion-Detector'] and (EventID=2004) and TimeCreated[timediff(@SystemTime) <= {ms}]]]", EventCategory.ResourceExhaustion);

        // --- Journal Application --------------------------------------------
        Collect(events, "Application", $"*[System[Provider[@Name='Application Error'] and (EventID=1000) and TimeCreated[timediff(@SystemTime) <= {ms}]]]", EventCategory.AppCrash);
        Collect(events, "Application", $"*[System[Provider[@Name='Application Hang'] and (EventID=1002) and TimeCreated[timediff(@SystemTime) <= {ms}]]]", EventCategory.AppHang);
        Collect(events, "Application", $"*[System[Provider[@Name='.NET Runtime'] and (EventID=1026) and TimeCreated[timediff(@SystemTime) <= {ms}]]]", EventCategory.AppCrash);

        return events.OrderByDescending(e => e.TimeLocal).ToList();
    }

    private void Collect(List<WinEvent> target, string logName, string xpath, EventCategory category)
    {
        try
        {
            var query = new EventLogQuery(logName, PathType.LogName, xpath) { ReverseDirection = true };
            using var reader = new EventLogReader(query);
            int count = 0;
            for (EventRecord? rec = reader.ReadEvent(); rec is not null; rec = reader.ReadEvent())
            {
                using (rec)
                {
                    if (++count > 500) break; // garde-fou : on ne charge jamais plus de 500 événements par requête
                    target.Add(ToWinEvent(rec, logName, category));
                }
            }
        }
        catch (EventLogNotFoundException) { /* journal absent : ignoré */ }
        catch (Exception ex)
        {
            _errors.Add(Lang.T($"Journal {logName} ({category}) : {ex.Message}", $"Log {logName} ({category}): {ex.Message}"));
        }
    }

    private static WinEvent ToWinEvent(EventRecord rec, string logName, EventCategory category)
    {
        var e = new WinEvent
        {
            TimeLocal = rec.TimeCreated?.ToLocalTime() ?? DateTime.MinValue,
            LogName = logName,
            Provider = rec.ProviderName ?? "",
            EventId = rec.Id,
            Category = category,
        };

        try { e.Level = rec.LevelDisplayName ?? ""; } catch { e.Level = ""; }

        string message = "";
        try { message = rec.FormatDescription() ?? ""; } catch { /* description indisponible */ }
        e.Message = Truncate(message, 600);

        try { ExtractDetails(rec, e, message); }
        catch { /* l'extraction fine est best-effort */ }

        return e;
    }

    private static readonly Regex HexRx = new(@"0x[0-9a-fA-F]{8,16}", RegexOptions.Compiled);
    private static readonly Regex DumpPathRx = new(@"[A-Za-z]:\\[^\r\n""]+?\.(?:dmp|DMP)", RegexOptions.Compiled);
    private static readonly Regex BugcheckCodeXmlRx = new(@"BugcheckCode[""']>(\d+)<", RegexOptions.Compiled);

    private static void ExtractDetails(EventRecord rec, WinEvent e, string message)
    {
        switch (e.Category)
        {
            case EventCategory.Bsod:
            {
                // Événement 1001 : "La vérification d'erreur était : 0x00000133 (0x…, 0x…, 0x…, 0x…)"
                var hexes = HexRx.Matches(message).Select(m => m.Value).ToList();
                if (hexes.Count == 0 && rec.Properties.Count > 0)
                    hexes = HexRx.Matches(string.Join(" ", rec.Properties.Select(p => p.Value?.ToString() ?? "")))
                                 .Select(m => m.Value).ToList();
                if (hexes.Count >= 1) e.Extracted["BugCheckCode"] = hexes[0];
                if (hexes.Count >= 5) e.Extracted["Parameters"] = string.Join(", ", hexes.Skip(1).Take(4));

                var dump = DumpPathRx.Match(message);
                if (!dump.Success && rec.Properties.Count >= 2)
                    dump = DumpPathRx.Match(rec.Properties[1].Value?.ToString() ?? "");
                if (dump.Success) e.Extracted["DumpPath"] = dump.Value;
                break;
            }
            case EventCategory.PowerLoss:
            {
                // Kernel-Power 41 : BugcheckCode=0 → coupure brute (pas de BSOD enregistré).
                var xml = rec.ToXml();
                var m = BugcheckCodeXmlRx.Match(xml);
                if (m.Success) e.Extracted["BugcheckCode"] = m.Groups[1].Value;
                break;
            }
            case EventCategory.AppCrash when string.Equals(rec.ProviderName, "Application Error", StringComparison.OrdinalIgnoreCase):
            {
                // EventData 1000 : [0]=application, [3]=module fautif, [6]=code exception
                var p = rec.Properties;
                if (p.Count > 0) e.Extracted["App"] = p[0].Value?.ToString() ?? "";
                if (p.Count > 3) e.Extracted["Module"] = p[3].Value?.ToString() ?? "";
                if (p.Count > 6) e.Extracted["ExceptionCode"] = p[6].Value?.ToString() ?? "";
                break;
            }
            case EventCategory.AppHang:
            {
                var p = rec.Properties;
                if (p.Count > 0) e.Extracted["App"] = p[0].Value?.ToString() ?? "";
                break;
            }
            case EventCategory.MemoryDiag:
            {
                // 1201 = aucun problème ; 1202 = erreurs détectées.
                e.Extracted["HasErrors"] = (rec.Id == 1202).ToString();
                break;
            }
            case EventCategory.ResourceExhaustion:
            {
                // Le message 2004 liste les plus gros consommateurs, ex :
                // « … consommation de mémoire virtuelle : vmmem.exe (12345) a consommé 17179869184 octets… »
                var procs = System.Text.RegularExpressions.Regex
                    .Matches(message, @"([\w.\-]+\.exe)", System.Text.RegularExpressions.RegexOptions.IgnoreCase)
                    .Select(m => m.Groups[1].Value)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(5).ToList();
                if (procs.Count > 0) e.Extracted["Processus"] = string.Join(", ", procs);
                break;
            }
            case EventCategory.Tdr:
            {
                // Display 4101 : le nom du pilote réinitialisé est dans les données (ex: nvlddmkm)
                if (rec.Properties.Count > 0)
                    e.Extracted["Driver"] = rec.Properties[0].Value?.ToString() ?? "";
                break;
            }
        }
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max] + "…";
}
