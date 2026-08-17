using System.Management;

namespace FaultTracePC.Core.Collectors;

/// <summary>
/// Lit l'historique du Moniteur de fiabilité (Win32_ReliabilityRecords).
/// Nécessite que la tâche RAC soit active (c'est le cas par défaut) ;
/// en cas d'indisponibilité, la collecte est simplement vide.
/// </summary>
public sealed class ReliabilityCollector
{
    private readonly List<string> _errors;

    public ReliabilityCollector(List<string> errors) => _errors = errors;

    public List<ReliabilityRecord> Collect(int days)
    {
        var list = new List<ReliabilityRecord>();
        var cutoff = DateTime.Now.AddDays(-days);
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT TimeGenerated, SourceName, EventIdentifier, ProductName, Message FROM Win32_ReliabilityRecords");
            foreach (ManagementObject mo in searcher.Get())
            {
                DateTime? time = null;
                try { time = ManagementDateTimeConverter.ToDateTime(mo["TimeGenerated"]?.ToString() ?? ""); }
                catch { }
                if (time is null || time < cutoff) continue;

                list.Add(new ReliabilityRecord
                {
                    TimeLocal = time.Value,
                    SourceName = mo["SourceName"]?.ToString() ?? "",
                    EventId = Convert.ToInt32(mo["EventIdentifier"] ?? 0),
                    ProductName = mo["ProductName"]?.ToString() ?? "",
                    Message = Truncate(mo["Message"]?.ToString() ?? "", 400),
                });
            }
        }
        catch (Exception ex)
        {
            _errors.Add(Lang.T($"Moniteur de fiabilité : {ex.Message} (données RAC peut-être indisponibles)", $"Reliability Monitor: {ex.Message} (RAC data may be unavailable)"));
        }
        return list.OrderByDescending(r => r.TimeLocal).ToList();
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max] + "…";
}
