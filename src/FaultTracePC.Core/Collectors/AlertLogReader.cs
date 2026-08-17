using System.Text.Json;

namespace FaultTracePC.Core.Collectors;

/// <summary>
/// Lecture du journal des alertes préventives (écrit par le service de surveillance).
/// Partagé par l'application (icône de notification, rapport) et l'API distante.
/// </summary>
public static class AlertLogReader
{
    /// <summary>Alertes des N derniers jours, les plus récentes d'abord.</summary>
    public static List<PreventiveAlert> Read(int days, List<string>? errors = null)
    {
        var result = new List<PreventiveAlert>();
        try
        {
            var path = AlertSettings.AlertsLogPath;
            if (!File.Exists(path)) return result;
            var cutoff = DateTime.Now.AddDays(-days);

            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(fs);
            while (reader.ReadLine() is { } line)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                try
                {
                    if (JsonSerializer.Deserialize<PreventiveAlert>(line) is { } a && a.Time >= cutoff)
                        result.Add(a);
                }
                catch { /* ligne corrompue : ignorée */ }
            }
        }
        catch (Exception ex)
        {
            errors?.Add(Lang.T($"Journal des alertes : {ex.Message}", $"Alerts log: {ex.Message}"));
        }
        // Le texte du fichier est celui de la langue en vigueur à l'écriture.
        // On le refabrique dans la langue en cours quand c'est possible.
        AlertCatalog.LocalizeAll(result);
        return result.OrderByDescending(a => a.Time).ToList();
    }

    /// <summary>
    /// Alertes postérieures à un instant donné (notification en direct, appelé toutes
    /// les 30 s). Ne lit que la FIN du fichier : coût constant même si le journal grossit.
    /// </summary>
    public static List<PreventiveAlert> ReadSince(DateTime since)
    {
        var result = new List<PreventiveAlert>();
        try
        {
            var path = AlertSettings.AlertsLogPath;
            if (!File.Exists(path)) return result;

            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            // On se place au plus 64 Ko avant la fin, puis on jette la ligne partielle.
            const int tail = 64 * 1024;
            bool partial = fs.Length > tail;
            if (partial) fs.Seek(-tail, SeekOrigin.End);

            using var reader = new StreamReader(fs);
            if (partial) reader.ReadLine();

            while (reader.ReadLine() is { } line)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                try
                {
                    if (JsonSerializer.Deserialize<PreventiveAlert>(line) is { } a && a.Time > since)
                        result.Add(a);
                }
                catch { }
            }
        }
        catch { }
        AlertCatalog.LocalizeAll(result);
        return result.OrderBy(a => a.Time).ToList();
    }
}
