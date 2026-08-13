using System.Management;

namespace FaultTracePC.Core.Collectors;

/// <summary>
/// État de la batterie d'un portable. L'usure se calcule en comparant la capacité
/// prévue par le constructeur (BatteryStaticData.DesignedCapacity) à celle que la
/// batterie atteint réellement à pleine charge aujourd'hui (BatteryFullChargedCapacity).
/// C'est exactement ce que fait « powercfg /batteryreport », mais lisible en un chiffre.
/// Sur un poste fixe, la collecte renvoie simplement une liste vide.
/// </summary>
public sealed class BatteryCollector
{
    private readonly List<string> _errors;

    public BatteryCollector(List<string> errors) => _errors = errors;

    public List<BatteryInfo> Collect()
    {
        var result = new List<BatteryInfo>();
        try
        {
            // 1) Présence et état général (espace de noms standard)
            using (var searcher = new ManagementObjectSearcher(
                "SELECT Name, Chemistry, EstimatedChargeRemaining, BatteryStatus FROM Win32_Battery"))
            {
                foreach (ManagementObject mo in searcher.Get())
                {
                    result.Add(new BatteryInfo
                    {
                        Name = mo["Name"]?.ToString()?.Trim() ?? "Batterie",
                        Chemistry = ChemistryLabel(mo["Chemistry"]),
                        ChargeRemainingPercent = mo["EstimatedChargeRemaining"] as ushort?,
                        Status = StatusLabel(mo["BatteryStatus"]),
                    });
                }
            }
            if (result.Count == 0) return result;   // poste fixe : rien de plus à faire

            // 2) Capacités (espace de noms root\wmi) — c'est ce qui donne l'usure
            var designed = ReadCapacities(@"root\wmi",
                "SELECT InstanceName, DesignedCapacity FROM BatteryStaticData", "DesignedCapacity");
            var full = ReadCapacities(@"root\wmi",
                "SELECT InstanceName, FullChargedCapacity FROM BatteryFullChargedCapacity", "FullChargedCapacity");
            var cycles = ReadCapacities(@"root\wmi",
                "SELECT InstanceName, CycleCount FROM BatteryCycleCount", "CycleCount");

            // Une seule batterie dans l'immense majorité des cas : on associe par ordre.
            for (int i = 0; i < result.Count; i++)
            {
                result[i].DesignedCapacity = At(designed, i);
                result[i].FullChargedCapacity = At(full, i);
                var c = At(cycles, i);
                if (c is > 0) result[i].CycleCount = c;   // souvent non renseigné par le firmware
            }
        }
        catch (Exception ex)
        {
            _errors.Add($"Batterie : {ex.Message}");
        }
        return result;
    }

    private static uint? At(List<uint> values, int index) =>
        index < values.Count ? values[index] : null;

    private List<uint> ReadCapacities(string scope, string query, string property)
    {
        var values = new List<uint>();
        try
        {
            using var searcher = new ManagementObjectSearcher(scope, query);
            foreach (ManagementObject mo in searcher.Get())
            {
                try
                {
                    var v = Convert.ToUInt32(mo[property] ?? 0u);
                    values.Add(v);
                }
                catch { }
            }
        }
        catch { /* classe absente : batterie non conforme ou poste fixe */ }
        return values;
    }

    private static string ChemistryLabel(object? value) => Convert.ToInt32(value ?? 0) switch
    {
        3 => "Plomb", 4 => "Nickel-Cadmium", 5 => "Nickel-Hydrure métallique",
        6 => "Lithium-ion", 7 => "Zinc-air", 8 => "Lithium-polymère",
        _ => "",
    };

    private static string StatusLabel(object? value) => Convert.ToInt32(value ?? 0) switch
    {
        1 => "Sur batterie", 2 => "Sur secteur", 3 => "Pleine charge",
        4 => "Faible", 5 => "Critique", 6 => "En charge",
        7 => "En charge (élevée)", 8 => "En charge (faible)", 9 => "En charge (critique)",
        10 => "Indéterminé", 11 => "Partiellement chargée",
        _ => "",
    };
}
