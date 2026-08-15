using System.Management;
using Microsoft.Win32;

namespace FaultTracePC.Core.Collectors;

/// <summary>
/// Collecte l'instantané matériel/logiciel via WMI.
/// Chaque section est isolée : une erreur sur un composant n'empêche pas le reste.
/// </summary>
public sealed class SystemInfoCollector
{
    private readonly List<string> _errors;

    public SystemInfoCollector(List<string> errors) => _errors = errors;

    public SystemSnapshot Collect(bool includeDrivers)
    {
        var s = new SystemSnapshot { MachineName = Environment.MachineName };

        Safe("OS", () => CollectOs(s.Os));
        Safe("BIOS/carte mère", () => CollectBios(s.Bios));
        Safe("CPU", () => CollectCpu(s.Cpu));
        Safe("RAM", () => CollectRam(s.RamModules));
        Safe("GPU", () => CollectGpu(s.Gpus));
        Safe("Disques", () => CollectDisks(s.Disks));
        Safe("Volumes", () => CollectVolumes(s.Volumes));
        Safe("SMART", () => new SmartCollector(_errors).Enrich(s.Disks));
        Safe("Batterie", () => s.Batteries.AddRange(new BatteryCollector(_errors).Collect()));
        Safe("Logiciels installés", () => s.InstalledApps.AddRange(InstalledSoftwareCollector.Collect(_errors)));
        if (includeDrivers)
            Safe("Pilotes", () => s.Drivers.AddRange(DriverCollector.Collect()));

        return s;
    }

    private void Safe(string what, Action action)
    {
        try { action(); }
        catch (Exception ex) { _errors.Add($"Collecte {what} : {ex.Message}"); }
    }

    private static IEnumerable<ManagementObject> Query(string wql, string? scope = null)
    {
        using var searcher = scope is null
            ? new ManagementObjectSearcher(wql)
            : new ManagementObjectSearcher(scope, wql);
        foreach (ManagementObject mo in searcher.Get())
            yield return mo;
    }

    private static string S(ManagementObject mo, string prop) =>
        mo.Properties.Cast<PropertyData>().Any(p => p.Name == prop) ? mo[prop]?.ToString()?.Trim() ?? "" : "";

    private static T? V<T>(ManagementObject mo, string prop) where T : struct
    {
        try { var v = mo[prop]; return v is null ? null : (T)Convert.ChangeType(v, typeof(T)); }
        catch { return null; }
    }

    private static DateTime? WmiDate(string dmtf)
    {
        if (string.IsNullOrWhiteSpace(dmtf)) return null;
        try { return ManagementDateTimeConverter.ToDateTime(dmtf); } catch { return null; }
    }

    private void CollectOs(OsInfo os)
    {
        foreach (var mo in Query("SELECT * FROM Win32_OperatingSystem"))
        {
            os.Caption = S(mo, "Caption");
            os.Version = S(mo, "Version");
            os.BuildNumber = S(mo, "BuildNumber");
            os.Architecture = S(mo, "OSArchitecture");
            os.InstallDate = WmiDate(S(mo, "InstallDate"));
            os.LastBootUpTime = WmiDate(S(mo, "LastBootUpTime"));
            os.TotalVisibleMemoryKB = V<ulong>(mo, "TotalVisibleMemorySize") ?? 0;
            os.FreePhysicalMemoryKB = V<ulong>(mo, "FreePhysicalMemory") ?? 0;
            os.TotalVirtualMemoryKB = V<ulong>(mo, "TotalVirtualMemorySize") ?? 0;
            os.FreeVirtualMemoryKB = V<ulong>(mo, "FreeVirtualMemory") ?? 0;
        }

        // DisplayVersion (23H2, 24H2…) n'est pas dans WMI : registre.
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            os.DisplayVersion = key?.GetValue("DisplayVersion")?.ToString() ?? "";
        }
        catch { /* non bloquant */ }

        try
        {
            var parts = new List<string>();
            foreach (var mo in Query("SELECT * FROM Win32_PageFileUsage"))
                parts.Add($"{S(mo, "Name")} ({V<uint>(mo, "AllocatedBaseSize")} Mo alloués, pic {V<uint>(mo, "PeakUsage")} Mo)");
            os.PageFileInfo = parts.Count > 0 ? string.Join(" ; ", parts) : "géré automatiquement / aucun";
        }
        catch { /* non bloquant */ }
    }

    private void CollectBios(BiosInfo b)
    {
        foreach (var mo in Query("SELECT * FROM Win32_BIOS"))
        {
            b.Manufacturer = S(mo, "Manufacturer");
            b.Version = S(mo, "SMBIOSBIOSVersion");
            b.ReleaseDate = WmiDate(S(mo, "ReleaseDate"));
        }
        foreach (var mo in Query("SELECT * FROM Win32_BaseBoard"))
        {
            b.BaseboardManufacturer = S(mo, "Manufacturer");
            b.BaseboardProduct = S(mo, "Product");
        }
        foreach (var mo in Query("SELECT * FROM Win32_ComputerSystem"))
        {
            b.SystemManufacturer = S(mo, "Manufacturer");
            b.SystemModel = S(mo, "Model");
        }
    }

    private void CollectCpu(CpuInfo c)
    {
        foreach (var mo in Query("SELECT * FROM Win32_Processor"))
        {
            c.Name = S(mo, "Name");
            c.Cores = V<uint>(mo, "NumberOfCores") ?? 0;
            c.LogicalProcessors = V<uint>(mo, "NumberOfLogicalProcessors") ?? 0;
            c.MaxClockSpeedMHz = V<uint>(mo, "MaxClockSpeed") ?? 0;
            c.Socket = S(mo, "SocketDesignation");
        }
    }

    private void CollectRam(List<RamModule> list)
    {
        foreach (var mo in Query("SELECT * FROM Win32_PhysicalMemory"))
        {
            list.Add(new RamModule
            {
                BankLabel = S(mo, "BankLabel"),
                DeviceLocator = S(mo, "DeviceLocator"),
                CapacityBytes = V<ulong>(mo, "Capacity") ?? 0,
                SpeedMTs = V<uint>(mo, "Speed") ?? 0,
                ConfiguredSpeedMTs = V<uint>(mo, "ConfiguredClockSpeed") ?? 0,
                Manufacturer = S(mo, "Manufacturer"),
                PartNumber = S(mo, "PartNumber"),
            });
        }
    }

    private void CollectGpu(List<GpuInfo> list)
    {
        foreach (var mo in Query("SELECT * FROM Win32_VideoController"))
        {
            list.Add(new GpuInfo
            {
                Name = S(mo, "Name"),
                DriverVersion = S(mo, "DriverVersion"),
                DriverDate = WmiDate(S(mo, "DriverDate")),
                VideoProcessor = S(mo, "VideoProcessor"),
                Status = S(mo, "Status"),
            });
        }
    }

    private void CollectDisks(List<DiskInfo> list)
    {
        foreach (var mo in Query("SELECT * FROM Win32_DiskDrive"))
        {
            list.Add(new DiskInfo
            {
                Model = S(mo, "Model"),
                SizeBytes = V<ulong>(mo, "Size") ?? 0,
                InterfaceType = S(mo, "InterfaceType"),
                WmiStatus = S(mo, "Status"),
                // Numéro physique du disque : c'est la MÊME numérotation que
                // MSFT_PhysicalDisk.DeviceId, donc la seule association fiable.
                Index = V<int>(mo, "Index"),
            });
        }

        // Santé + type de média via l'espace de noms Storage (Windows 8+).
        try
        {
            foreach (var mo in Query("SELECT * FROM MSFT_PhysicalDisk", @"root\Microsoft\Windows\Storage"))
            {
                var model = S(mo, "Model");
                if (string.IsNullOrEmpty(model)) model = S(mo, "FriendlyName");

                // Association par numéro physique d'abord : MSFT_PhysicalDisk.DeviceId
                // et Win32_DiskDrive.Index désignent le même disque. Le rapprochement
                // par modèle ne sert que de repli — il est ambigu dès que deux disques
                // identiques sont montés sur la même machine.
                DiskInfo? match = null;
                if (int.TryParse(S(mo, "DeviceId"), out var devId))
                    match = list.FirstOrDefault(d => d.Index == devId);
                if (match is null && model.Length > 0)
                    match = list.FirstOrDefault(d =>
                        d.Model.Length > 0 &&
                        (d.Model.Contains(model, StringComparison.OrdinalIgnoreCase) ||
                         model.Contains(d.Model, StringComparison.OrdinalIgnoreCase)));
                if (match is null) continue;

                var health = V<ushort>(mo, "HealthStatus");
                match.HealthStatus = health switch
                {
                    0 => "Sain",
                    1 => "Avertissement",
                    2 => "Défaillant",
                    _ => "Inconnu"
                };
                var media = V<ushort>(mo, "MediaType");
                match.MediaType = media switch { 3 => "HDD", 4 => "SSD", 5 => "SCM", _ => match.MediaType };

                // Compteurs de fiabilité (température, usure, heures, erreurs).
                //
                // ATTENTION : MSFT_StorageReliabilityCounter n'est PAS énumérable.
                // Le fournisseur de stockage ne matérialise l'instance qu'à travers
                // l'association depuis le disque — c'est exactement pour cette raison
                // que Get-StorageReliabilityCounter exige un disque en entrée et n'a
                // pas de forme sans paramètre. Un « SELECT * FROM
                // MSFT_StorageReliabilityCounter » renvoie zéro instance, sans erreur :
                // les valeurs disparaissaient donc en silence (corrigé en 1.1).
                foreach (var rc in RelatedReliabilityCounters(mo))
                {
                    // Ces propriétés sont déclarées UInt16/UInt32/UInt64 selon les
                    // versions de Windows : on convertit largement plutôt que de
                    // parier sur un type exact.
                    var temp = V<int>(rc, "Temperature");
                    if (temp is > 0 and < 120) match.TemperatureC = temp;
                    var wear = V<int>(rc, "Wear");
                    if (wear is >= 0 and <= 100) match.WearPercent = wear;
                    match.PowerOnHours ??= V<ulong>(rc, "PowerOnHours");
                    match.ReadErrorsTotal ??= V<ulong>(rc, "ReadErrorsTotal");
                    break; // une seule instance de compteur par disque
                }
            }
        }
        catch { /* non bloquant */ }

        AttachDriveLetters(list);
    }

    /// <summary>
    /// Rattache à chaque disque physique les lettres de ses volumes.
    ///
    /// POURQUOI : « \Device\Harddisk0 » et « Disque 0 » ne parlent qu'aux
    /// techniciens. « C: » parle à tout le monde, et c'est ce qu'affiche
    /// l'Explorateur. Sans ce rattachement, le rapport peut nommer un disque
    /// fautif sans que son lecteur puisse le reconnaître.
    ///
    /// COMMENT : la chaîne d'associations WMI documentée, en deux sauts —
    /// Win32_DiskDrive → Win32_DiskDriveToDiskPartition → Win32_DiskPartition
    /// → Win32_LogicalDiskToPartition → Win32_LogicalDisk. Il n'existe pas de
    /// lien direct : une partition peut ne porter aucune lettre (réservée au
    /// système, partition de récupération), et un disque peut n'en porter aucune.
    ///
    /// Entièrement non bloquant : un échec laisse simplement la liste vide, et le
    /// rapport se rabat sur le numéro de disque.
    /// </summary>
    private void AttachDriveLetters(List<DiskInfo> list)
    {
        foreach (var disk in list)
        {
            if (disk.Index is not { } index) continue;
            try
            {
                var lettres = new List<string>();
                var diskPath = $"Win32_DiskDrive.DeviceID='\\\\\\\\.\\\\PHYSICALDRIVE{index}'";

                foreach (var part in Query(
                    $"ASSOCIATORS OF {{{diskPath}}} WHERE AssocClass=Win32_DiskDriveToDiskPartition"))
                {
                    var partId = S(part, "DeviceID");
                    if (partId.Length == 0) continue;

                    foreach (var vol in Query(
                        $"ASSOCIATORS OF {{Win32_DiskPartition.DeviceID='{partId}'}} WHERE AssocClass=Win32_LogicalDiskToPartition"))
                    {
                        var lettre = S(vol, "DeviceID");   // « C: »
                        if (lettre.Length > 0 && !lettres.Contains(lettre, StringComparer.OrdinalIgnoreCase))
                            lettres.Add(lettre);
                    }
                }

                lettres.Sort(StringComparer.OrdinalIgnoreCase);
                disk.Letters = lettres;
            }
            catch
            {
                // Un disque dont les lettres restent introuvables reste un disque
                // parfaitement diagnosticable : on n'échoue pas pour si peu.
            }
        }
    }

    /// <summary>
    /// Compteurs de fiabilité associés à un disque physique.
    ///
    /// On passe par une requête ASSOCIATORS plutôt que par une énumération directe :
    /// le fournisseur de stockage ne matérialise l'instance qu'au travers de
    /// l'association depuis le disque. Deux façons d'obtenir le chemin de l'objet,
    /// la seconde reconstruisant le chemin à la main si la propriété système
    /// __RELPATH n'est pas accessible.
    /// </summary>
    private static IEnumerable<ManagementObject> RelatedReliabilityCounters(ManagementObject disk)
    {
        const string ns = @"root\Microsoft\Windows\Storage";
        const string result = " WHERE ResultClass = MSFT_StorageReliabilityCounter";

        string? relPath = null;
        try { relPath = disk["__RELPATH"]?.ToString(); } catch { /* propriété système indisponible */ }

        if (string.IsNullOrWhiteSpace(relPath))
        {
            var objectId = S(disk, "ObjectId");
            if (objectId.Length == 0) yield break;
            // Échappement WMI : antislash puis guillemet, dans cet ordre.
            var escaped = objectId.Replace("\\", "\\\\").Replace("\"", "\\\"");
            relPath = $"MSFT_PhysicalDisk.ObjectId=\"{escaped}\"";
        }

        // Query() peut lever si le chemin est refusé : on isole l'échec ici pour ne
        // pas interrompre la collecte des autres disques.
        List<ManagementObject> found;
        try { found = Query("ASSOCIATORS OF {" + relPath + "}" + result, ns).ToList(); }
        catch { yield break; }

        foreach (var mo in found) yield return mo;
    }

    private void CollectVolumes(List<VolumeInfo> list)
    {
        foreach (var mo in Query("SELECT * FROM Win32_LogicalDisk WHERE DriveType=3"))
        {
            list.Add(new VolumeInfo
            {
                Letter = S(mo, "DeviceID"),
                Label = S(mo, "VolumeName"),
                FileSystem = S(mo, "FileSystem"),
                SizeBytes = V<ulong>(mo, "Size") ?? 0,
                FreeBytes = V<ulong>(mo, "FreeSpace") ?? 0,
            });
        }
    }
}
