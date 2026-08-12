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
            });
        }

        // Santé + type de média via l'espace de noms Storage (Windows 8+).
        try
        {
            foreach (var mo in Query("SELECT * FROM MSFT_PhysicalDisk", @"root\Microsoft\Windows\Storage"))
            {
                var model = S(mo, "Model");
                if (string.IsNullOrEmpty(model)) model = S(mo, "FriendlyName");
                var match = list.FirstOrDefault(d =>
                    d.Model.Contains(model, StringComparison.OrdinalIgnoreCase) ||
                    model.Contains(d.Model, StringComparison.OrdinalIgnoreCase));
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
            }
        }
        catch { /* espace de noms Storage indisponible : non bloquant */ }

        // Compteurs de fiabilité (température, usure) — pas toujours exposés selon le contrôleur.
        try
        {
            foreach (var mo in Query("SELECT * FROM MSFT_StorageReliabilityCounter", @"root\Microsoft\Windows\Storage"))
            {
                var deviceId = S(mo, "DeviceId");
                // Association best-effort par index : on rattache au disque de même position si possible.
                if (int.TryParse(deviceId, out var idx) && idx >= 0 && idx < list.Count)
                {
                    var d = list[idx];
                    var temp = V<byte>(mo, "Temperature");
                    if (temp is > 0 and < 120) d.TemperatureC = temp;
                    var wear = V<byte>(mo, "Wear");
                    if (wear is not null) d.WearPercent = wear;
                    d.PowerOnHours = V<uint>(mo, "PowerOnHours");
                    d.ReadErrorsTotal = V<ulong>(mo, "ReadErrorsTotal");
                }
            }
        }
        catch { /* non bloquant */ }
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
