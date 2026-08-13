using System.Management;

namespace FaultTracePC.Core.Collectors;

/// <summary>
/// Lecture des attributs SMART détaillés — ceux qui prédisent réellement une panne.
///
/// Deux chemins, car Windows n'expose pas la même chose selon le type de disque :
///  · SATA/ATA : les attributs bruts sont disponibles via WMI (espace de noms
///    root\wmi, classes MSStorageDriver_FailurePredictData/Status). C'est là qu'on
///    trouve les secteurs défectueux (5, 197, 198) et les erreurs de câble (199).
///  · NVMe : ces classes ne répondent pas. On se rabat sur les compteurs de
///    fiabilité de Windows (usure, température, heures, erreurs), moins détaillés
///    mais fiables — la source est indiquée dans le rapport, sans faire croire
///    qu'on a lu du SMART brut.
/// </summary>
public sealed class SmartCollector
{
    private readonly List<string> _errors;

    public SmartCollector(List<string> errors) => _errors = errors;

    public void Enrich(List<DiskInfo> disks)
    {
        if (disks.Count == 0) return;
        TryReadAtaSmart(disks);
        FillFromReliabilityCounters(disks);
    }

    // ------------------------------------------------------------------
    // SATA / ATA : attributs SMART bruts
    // ------------------------------------------------------------------

    private void TryReadAtaSmart(List<DiskInfo> disks)
    {
        // Correspondance InstanceName (root\wmi) → disque, via le PNPDeviceID.
        var byPnp = new Dictionary<string, DiskInfo>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Model, PNPDeviceID FROM Win32_DiskDrive");
            foreach (ManagementObject mo in searcher.Get())
            {
                var model = mo["Model"]?.ToString() ?? "";
                var pnp = mo["PNPDeviceID"]?.ToString() ?? "";
                var disk = disks.FirstOrDefault(d => d.Model.Equals(model, StringComparison.OrdinalIgnoreCase));
                if (disk is not null && pnp.Length > 0) byPnp[Normalize(pnp)] = disk;
            }
        }
        catch (Exception ex) { _errors.Add($"SMART (association des disques) : {ex.Message}"); }

        // 1) Le disque annonce-t-il lui-même une défaillance ?
        try
        {
            using var searcher = new ManagementObjectSearcher(@"root\wmi",
                "SELECT InstanceName, PredictFailure FROM MSStorageDriver_FailurePredictStatus");
            foreach (ManagementObject mo in searcher.Get())
            {
                var disk = Match(byPnp, mo["InstanceName"]?.ToString());
                if (disk is null) continue;
                (disk.Smart ??= new SmartInfo()).PredictedFailure = mo["PredictFailure"] as bool?;
                disk.Smart.Source = "SMART (SATA)";
            }
        }
        catch { /* classe absente (NVMe, contrôleur RAID…) : chemin normal */ }

        // 2) Attributs bruts
        try
        {
            using var searcher = new ManagementObjectSearcher(@"root\wmi",
                "SELECT InstanceName, VendorSpecific FROM MSStorageDriver_FailurePredictData");
            foreach (ManagementObject mo in searcher.Get())
            {
                var disk = Match(byPnp, mo["InstanceName"]?.ToString());
                if (disk is null || mo["VendorSpecific"] is not byte[] raw || raw.Length < 362) continue;
                ParseAttributes(raw, disk.Smart ??= new SmartInfo());
                disk.Smart.Source = "SMART (SATA)";
            }
        }
        catch { /* idem : pas de SMART ATA sur cette machine */ }
    }

    private static string Normalize(string s) => s.Replace("\\", "").Replace("_0", "").Trim().ToUpperInvariant();

    private static DiskInfo? Match(Dictionary<string, DiskInfo> byPnp, string? instanceName)
    {
        if (string.IsNullOrEmpty(instanceName)) return null;
        var key = Normalize(instanceName);
        if (byPnp.TryGetValue(key, out var exact)) return exact;
        // InstanceName porte souvent un suffixe (_0) : on compare par préfixe.
        foreach (var (k, v) in byPnp)
            if (key.StartsWith(k, StringComparison.OrdinalIgnoreCase) || k.StartsWith(key, StringComparison.OrdinalIgnoreCase))
                return v;
        return null;
    }

    /// <summary>
    /// La structure SMART fait 512 octets : 2 octets de version, puis 30 attributs
    /// de 12 octets. Chaque attribut : [0] identifiant, [1-2] indicateurs,
    /// [3] valeur normalisée, [4] pire valeur, [5-10] valeur brute (48 bits).
    /// </summary>
    private static void ParseAttributes(byte[] raw, SmartInfo smart)
    {
        for (int offset = 2; offset + 12 <= raw.Length && offset < 362; offset += 12)
        {
            byte id = raw[offset];
            if (id == 0) continue;

            byte normalized = raw[offset + 3];
            ulong value = 0;
            for (int i = 0; i < 6; i++) value |= (ulong)raw[offset + 5 + i] << (8 * i);

            switch (id)
            {
                case 5: smart.ReallocatedSectors = value; break;
                case 9: smart.PowerOnHours = value & 0xFFFFFFFF; break;
                case 12: smart.PowerCycles = value & 0xFFFFFFFF; break;
                case 187: smart.ReportedUncorrectable = value; break;
                case 194: // la température brute contient parfois min/max : on garde l'octet de poids faible
                    var t = (int)(value & 0xFF);
                    if (t is > 0 and < 120) smart.TemperatureC = t;
                    break;
                case 197: smart.PendingSectors = value; break;
                case 198: smart.UncorrectableSectors = value; break;
                case 199: smart.UdmaCrcErrors = value; break;
                // Durée de vie restante d'un SSD : l'identifiant varie selon le fabricant,
                // mais la valeur normalisée est un pourcentage dans tous les cas.
                case 231 or 177 or 233 or 202:
                    if (normalized is > 0 and <= 100) smart.SsdLifeLeftPercent = normalized;
                    break;
            }
        }
    }

    // ------------------------------------------------------------------
    // NVMe et compléments : compteurs de fiabilité de Windows
    // ------------------------------------------------------------------

    private void FillFromReliabilityCounters(List<DiskInfo> disks)
    {
        foreach (var d in disks)
        {
            var s = d.Smart;
            bool needsSource = s is null || string.IsNullOrEmpty(s.Source);
            s ??= d.Smart = new SmartInfo();

            // Les valeurs déjà collectées par SystemInfoCollector complètent le tableau.
            s.PowerOnHours ??= d.PowerOnHours;
            s.TemperatureC ??= d.TemperatureC;
            if (s.SsdLifeLeftPercent is null && d.WearPercent is { } wear && wear is >= 0 and <= 100)
                s.SsdLifeLeftPercent = 100 - wear;

            if (needsSource && (s.PowerOnHours is not null || s.TemperatureC is not null || s.SsdLifeLeftPercent is not null))
                s.Source = "Compteurs Windows (NVMe)";
        }
    }
}
