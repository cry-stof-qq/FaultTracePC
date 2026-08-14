using System.Text.Json;
using System.Text.Json.Serialization;

namespace FaultTracePC.Core.Report;

/// <summary>
/// Historique des scans : chaque analyse est résumée en JSON dans
/// Documents\FaultTracePC\Historique, et le scan suivant se compare au précédent
/// pour répondre à LA question qui suit une réparation : « est-ce que c'est réglé ? ».
/// </summary>
public static class ScanHistory
{
    private static string HistoryDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "FaultTracePC", "Historique");

    // ------------------------------------------------------------------
    // Résumé persisté (volontairement compact : l'essentiel pour comparer)
    // ------------------------------------------------------------------

    public sealed class ScanSummary
    {
        public DateTime GeneratedAt { get; set; }
        public int ScanPeriodDays { get; set; }
        public List<BsodBrief> Bsods { get; set; } = new();
        public Dictionary<string, string> DriverVersions { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public List<DiskBrief> Disks { get; set; } = new();
        public int DiskErrorEvents { get; set; }
        public int WheaEvents { get; set; }
        public int AppCrashEvents { get; set; }
        public double MemUsedPct { get; set; }
        public long VirtualizationBytes { get; set; }
        public List<string> CriticalFindings { get; set; } = new();
    }

    public sealed class BsodBrief
    {
        public DateTime Time { get; set; }
        public uint? Code { get; set; }
        public string? Driver { get; set; }
    }

    public sealed class DiskBrief
    {
        public string Model { get; set; } = "";
        public string Health { get; set; } = "";
        public int? TemperatureC { get; set; }
        public int? WearPercent { get; set; }
        public ulong? ReadErrorsTotal { get; set; }
        /// <summary>Secteurs défectueux cumulés : leur ÉVOLUTION est le vrai signal d'alarme.</summary>
        public ulong? BadSectors { get; set; }
        public ulong? CrcErrors { get; set; }
    }

    // ------------------------------------------------------------------

    public static ScanSummary Summarize(DiagnosticReport r)
    {
        var os = r.System.Os;
        return new ScanSummary
        {
            GeneratedAt = r.GeneratedAt,
            ScanPeriodDays = r.ScanPeriodDays,
            Bsods = r.Bsods.Select(b => new BsodBrief { Time = b.TimeLocal, Code = b.BugCheckCode, Driver = b.SuspectDriver }).ToList(),
            DriverVersions = r.System.Drivers
                .Where(d => !string.IsNullOrEmpty(d.Path) && !string.IsNullOrEmpty(d.FileVersion))
                .GroupBy(d => Path.GetFileName(d.Path), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => $"{g.First().FileVersion}|{g.First().FileDate:yyyy-MM-dd}", StringComparer.OrdinalIgnoreCase),
            Disks = r.System.Disks.Select(d => new DiskBrief
            {
                Model = d.Model, Health = d.HealthStatus,
                TemperatureC = d.TemperatureC, WearPercent = d.WearPercent, ReadErrorsTotal = d.ReadErrorsTotal,
                BadSectors = d.Smart?.BadSectors, CrcErrors = d.Smart?.UdmaCrcErrors,
            }).ToList(),
            DiskErrorEvents = r.Events.Count(e => e.Category == EventCategory.DiskError),
            WheaEvents = r.Events.Count(e => e.Category == EventCategory.Whea),
            AppCrashEvents = r.Events.Count(e => e.Category == EventCategory.AppCrash),
            MemUsedPct = os.TotalVisibleMemoryKB == 0 ? 0
                : Math.Round(100.0 * (os.TotalVisibleMemoryKB - os.FreePhysicalMemoryKB) / os.TotalVisibleMemoryKB, 1),
            VirtualizationBytes = Analysis.RulesEngine.VirtualizationBytes(r),
            CriticalFindings = r.Findings.Where(f => f.Severity == Severity.Critical).Select(f => f.Title).ToList(),
        };
    }

    /// <summary>Sauvegarde le résumé du scan courant (après analyse).</summary>
    public static void Save(DiagnosticReport r, List<string> errors)
    {
        try
        {
            Directory.CreateDirectory(HistoryDir);
            var path = Path.Combine(HistoryDir, $"Scan_{r.GeneratedAt:yyyy-MM-dd_HHmmss}.json");
            var json = JsonSerializer.Serialize(Summarize(r), JsonOpts);
            File.WriteAllText(path, json);
        }
        catch (Exception ex) { errors.Add($"Historique des scans (écriture) : {ex.Message}"); }
    }

    /// <summary>
    /// Résumé du dernier scan de cette machine, ou null si elle n'a jamais été
    /// analysée. C'est ce que le mode parc rapatrie pour comparer les postes entre
    /// eux : il contient déjà versions de pilotes, crashs, disques et conclusions
    /// critiques, sans rien exposer de nominatif.
    /// </summary>
    public static ScanSummary? LoadLatest()
    {
        try
        {
            if (!Directory.Exists(HistoryDir)) return null;
            foreach (var file in Directory.EnumerateFiles(HistoryDir, "Scan_*.json").OrderByDescending(f => f))
            {
                try
                {
                    if (JsonSerializer.Deserialize<ScanSummary>(File.ReadAllText(file), JsonOpts) is { } s) return s;
                }
                catch { /* fichier corrompu : on essaie le précédent */ }
            }
        }
        catch { /* historique illisible */ }
        return null;
    }

    /// <summary>Charge le résumé du scan le plus récent AVANT le scan courant.</summary>
    public static ScanSummary? LoadPrevious(DateTime before, List<string> errors)
    {
        try
        {
            if (!Directory.Exists(HistoryDir)) return null;
            foreach (var file in Directory.EnumerateFiles(HistoryDir, "Scan_*.json").OrderByDescending(f => f))
            {
                try
                {
                    var s = JsonSerializer.Deserialize<ScanSummary>(File.ReadAllText(file), JsonOpts);
                    if (s is not null && s.GeneratedAt < before.AddMinutes(-1)) return s;
                }
                catch { /* fichier corrompu : on passe au précédent */ }
            }
        }
        catch (Exception ex) { errors.Add($"Historique des scans (lecture) : {ex.Message}"); }
        return null;
    }

    // ------------------------------------------------------------------
    // Comparaison
    // ------------------------------------------------------------------

    public static ScanComparison? CompareWithPrevious(DiagnosticReport r, List<string> errors)
    {
        var prev = LoadPrevious(r.GeneratedAt, errors);
        if (prev is null) return null;

        var c = new ScanComparison { PreviousScanAt = prev.GeneratedAt };

        // Nouveaux crashs = postérieurs au scan précédent.
        var newBsods = r.Bsods.Where(b => b.TimeLocal > prev.GeneratedAt).ToList();
        c.NewBsodCount = newBsods.Count;
        c.NewBsods = newBsods.Select(b =>
            $"{b.TimeLocal:dd/MM HH:mm} — {b.BugCheckName}{(b.SuspectDriver is not null ? $" ({b.SuspectDriver})" : "")}").ToList();

        // Même signature qu'avant ? (code ou pilote déjà vus)
        var prevCodes = prev.Bsods.Where(b => b.Code is not null).Select(b => b.Code!.Value).ToHashSet();
        var prevDrivers = prev.Bsods.Where(b => b.Driver is not null).Select(b => b.Driver!).ToHashSet(StringComparer.OrdinalIgnoreCase);
        c.SameSignatureRecurred = newBsods.Any(b =>
            (b.BugCheckCode is not null && prevCodes.Contains(b.BugCheckCode.Value)) ||
            (b.SuspectDriver is not null && prevDrivers.Contains(b.SuspectDriver)));

        // Pilotes mis à jour depuis le scan précédent.
        // (le dictionnaire relu du JSON perd son comparateur : on le reconstruit insensible à la casse)
        var prevDriverVersions = new Dictionary<string, string>(prev.DriverVersions, StringComparer.OrdinalIgnoreCase);
        foreach (var (sys, cur) in Summarize(r).DriverVersions)
        {
            if (prevDriverVersions.TryGetValue(sys, out var old) && old != cur)
                c.DriverUpdates.Add($"{sys} : {old.Split('|')[0]} → {cur.Split('|')[0]}");
        }
        if (c.DriverUpdates.Count > 12) c.DriverUpdates = c.DriverUpdates.Take(12).ToList();

        // Évolution des disques (santé, usure, température, erreurs de lecture).
        foreach (var d in r.System.Disks)
        {
            var old = prev.Disks.FirstOrDefault(x => x.Model.Equals(d.Model, StringComparison.OrdinalIgnoreCase));
            if (old is null) continue;
            if (!string.IsNullOrEmpty(old.Health) && !string.IsNullOrEmpty(d.HealthStatus) && old.Health != d.HealthStatus)
                c.DiskChanges.Add($"{d.Model} : santé {old.Health} → {d.HealthStatus}");
            if (old.WearPercent is { } ow && d.WearPercent is { } nw && nw > ow)
                c.DiskChanges.Add($"{d.Model} : usure {ow} % → {nw} %");
            if (old.ReadErrorsTotal is { } oe && d.ReadErrorsTotal is { } ne && ne > oe)
                c.DiskChanges.Add($"{d.Model} : erreurs de lecture {oe} → {ne}");
            // L'augmentation des secteurs défectueux est LE signal d'un disque qui meurt.
            if (old.BadSectors is { } ob && d.Smart?.BadSectors is { } nb && nb > ob)
                c.DiskChanges.Add($"⚠ {d.Model} : secteurs défectueux {ob} → {nb} — dégradation en cours, sauvegarder");
            if (old.CrcErrors is { } oc && d.Smart?.UdmaCrcErrors is { } nc && nc > oc)
                c.DiskChanges.Add($"{d.Model} : erreurs de câble (CRC) {oc} → {nc}");
            if (old.TemperatureC is { } ot && d.TemperatureC is { } nt && Math.Abs(nt - ot) >= 8)
                c.DiskChanges.Add($"{d.Model} : température {ot} °C → {nt} °C");
        }

        // Événements disque/WHEA apparus depuis le scan précédent.
        c.NewDiskErrorEvents = r.Events.Count(e => e.Category == EventCategory.DiskError && e.TimeLocal > prev.GeneratedAt);
        c.NewWheaEvents = r.Events.Count(e => e.Category == EventCategory.Whea && e.TimeLocal > prev.GeneratedAt);

        // Tendance mémoire (virtualisation).
        var curVm = Analysis.RulesEngine.VirtualizationBytes(r);
        if (prev.VirtualizationBytes > 0 || curVm > 0)
        {
            var deltaGb = (curVm - prev.VirtualizationBytes) / 1024.0 / 1024 / 1024;
            if (Math.Abs(deltaGb) >= 1)
                c.MemoryTrend = $"Virtualisation (vmmem) : {(deltaGb > 0 ? "+" : "")}{deltaGb:0.#} Go depuis le dernier scan.";
        }

        // Synthèse honnête.
        bool prevHadProblems = prev.Bsods.Count > 0 || prev.CriticalFindings.Count > 0;
        if (c.SameSignatureRecurred)
        {
            c.Tone = "crit";
            c.Assessment = $"Le problème PERSISTE : un nouveau crash avec la même signature qu'au scan du {prev.GeneratedAt:dd/MM/yyyy} s'est produit. La réparation n'a pas suffi.";
        }
        else if (c.NewBsodCount > 0)
        {
            c.Tone = "warn";
            c.Assessment = $"{c.NewBsodCount} nouveau(x) crash(s) depuis le scan du {prev.GeneratedAt:dd/MM/yyyy}, mais avec une signature DIFFÉRENTE : l'ancien problème semble réglé, un nouveau est apparu.";
        }
        else if (prevHadProblems)
        {
            var days = (r.GeneratedAt - prev.GeneratedAt).TotalDays;
            c.Tone = "ok";
            c.Assessment = $"Aucun nouveau crash depuis le scan du {prev.GeneratedAt:dd/MM/yyyy} ({days:0.#} jour(s)). "
                         + (days >= 7 ? "La réparation semble efficace." : "Bon signe — à confirmer sur la durée (recommandé : re-scanner après une semaine d'utilisation normale).");
        }
        else
        {
            c.Tone = "ok";
            c.Assessment = $"Machine stable depuis le scan du {prev.GeneratedAt:dd/MM/yyyy} : aucun crash avant comme après.";
        }

        return c;
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}
