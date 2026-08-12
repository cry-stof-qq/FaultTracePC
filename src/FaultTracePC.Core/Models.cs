namespace FaultTracePC.Core;

// ---------------------------------------------------------------------------
// Modèles de données : instantané système
// ---------------------------------------------------------------------------

public sealed class OsInfo
{
    public string Caption { get; set; } = "";
    public string Version { get; set; } = "";
    public string BuildNumber { get; set; } = "";
    public string DisplayVersion { get; set; } = "";   // ex: 24H2 (registre)
    public string Architecture { get; set; } = "";
    public DateTime? InstallDate { get; set; }
    public DateTime? LastBootUpTime { get; set; }
    public TimeSpan? Uptime => LastBootUpTime is null ? null : DateTime.Now - LastBootUpTime;
    public ulong TotalVisibleMemoryKB { get; set; }
    public ulong FreePhysicalMemoryKB { get; set; }
    /// <summary>Limite de mémoire virtuelle (RAM + fichier d'échange), Ko.</summary>
    public ulong TotalVirtualMemoryKB { get; set; }
    public ulong FreeVirtualMemoryKB { get; set; }
    public string PageFileInfo { get; set; } = "";
}

/// <summary>Instantané d'un processus en cours (au moment du scan).</summary>
public sealed class ProcessInfo
{
    public int Pid { get; set; }
    public string Name { get; set; } = "";
    /// <summary>Mémoire privée engagée (la vraie « consommation » du processus).</summary>
    public long PrivateBytes { get; set; }
    public long WorkingSetBytes { get; set; }
    /// <summary>% CPU mesuré sur ~1 s (normalisé sur tous les cœurs).</summary>
    public double CpuPercent { get; set; }
    /// <summary>Débit disque lecture+écriture mesuré sur ~1 s (octets/s).</summary>
    public double IoBytesPerSec { get; set; }
}

public sealed class BiosInfo
{
    public string Manufacturer { get; set; } = "";
    public string Version { get; set; } = "";
    public DateTime? ReleaseDate { get; set; }
    public string BaseboardManufacturer { get; set; } = "";
    public string BaseboardProduct { get; set; } = "";
    public string SystemManufacturer { get; set; } = "";
    public string SystemModel { get; set; } = "";
}

public sealed class CpuInfo
{
    public string Name { get; set; } = "";
    public uint Cores { get; set; }
    public uint LogicalProcessors { get; set; }
    public uint MaxClockSpeedMHz { get; set; }
    public string Socket { get; set; } = "";
}

public sealed class RamModule
{
    public string BankLabel { get; set; } = "";
    public string DeviceLocator { get; set; } = "";
    public ulong CapacityBytes { get; set; }
    public uint SpeedMTs { get; set; }
    public uint ConfiguredSpeedMTs { get; set; }
    public string Manufacturer { get; set; } = "";
    public string PartNumber { get; set; } = "";
}

public sealed class GpuInfo
{
    public string Name { get; set; } = "";
    public string DriverVersion { get; set; } = "";
    public DateTime? DriverDate { get; set; }
    public string VideoProcessor { get; set; } = "";
    public string Status { get; set; } = "";
}

public sealed class DiskInfo
{
    public string Model { get; set; } = "";
    public ulong SizeBytes { get; set; }
    public string InterfaceType { get; set; } = "";
    public string MediaType { get; set; } = "";       // SSD / HDD / inconnu
    public string WmiStatus { get; set; } = "";       // Win32_DiskDrive.Status ("OK"…)
    public string HealthStatus { get; set; } = "";    // MSFT_PhysicalDisk ("Healthy"…)
    public int? TemperatureC { get; set; }            // MSFT_StorageReliabilityCounter, si dispo
    public int? WearPercent { get; set; }             // usure SSD, si dispo
    public ulong? PowerOnHours { get; set; }
    public ulong? ReadErrorsTotal { get; set; }
}

public sealed class VolumeInfo
{
    public string Letter { get; set; } = "";
    public string Label { get; set; } = "";
    public string FileSystem { get; set; } = "";
    public ulong SizeBytes { get; set; }
    public ulong FreeBytes { get; set; }
    public double PercentFree => SizeBytes == 0 ? 0 : Math.Round(FreeBytes * 100.0 / SizeBytes, 1);
}

public sealed class DriverInfo
{
    public string Name { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Path { get; set; } = "";
    public string State { get; set; } = "";
    public string StartMode { get; set; } = "";
    public string FileVersion { get; set; } = "";
    public string CompanyName { get; set; } = "";
    public DateTime? FileDate { get; set; }
    public bool IsMicrosoft { get; set; }
}

public sealed class SystemSnapshot
{
    public OsInfo Os { get; set; } = new();
    public BiosInfo Bios { get; set; } = new();
    public CpuInfo Cpu { get; set; } = new();
    public List<RamModule> RamModules { get; set; } = new();
    public List<GpuInfo> Gpus { get; set; } = new();
    public List<DiskInfo> Disks { get; set; } = new();
    public List<VolumeInfo> Volumes { get; set; } = new();
    public List<DriverInfo> Drivers { get; set; } = new();
    public string MachineName { get; set; } = "";
}

// ---------------------------------------------------------------------------
// Modèles : événements, crashs, dumps
// ---------------------------------------------------------------------------

public enum EventCategory
{
    Bsod,             // BugCheck 1001
    PowerLoss,        // Kernel-Power 41
    UnexpectedShutdown, // EventLog 6008
    Whea,             // WHEA-Logger (erreurs matérielles)
    DiskError,        // disk / Ntfs / volmgr / stornvme / storahci
    Tdr,              // Display 4101 (réinitialisation pilote graphique)
    AppCrash,         // Application Error 1000
    AppHang,          // Application Hang 1002
    ServiceFailure,   // Service Control Manager
    MemoryDiag,       // Diagnostic mémoire Windows
    WindowsUpdate,    // Installations / échecs de mises à jour
    ResourceExhaustion, // Resource-Exhaustion-Detector 2004 : mémoire virtuelle saturée
    Other
}

public sealed class WinEvent
{
    public DateTime TimeLocal { get; set; }
    public string LogName { get; set; } = "";
    public string Provider { get; set; } = "";
    public int EventId { get; set; }
    public string Level { get; set; } = "";
    public EventCategory Category { get; set; } = EventCategory.Other;
    public string Message { get; set; } = "";
    /// <summary>Données extraites spécifiques (ex: nom d'application fautive, code bugcheck…)</summary>
    public Dictionary<string, string> Extracted { get; set; } = new();
}

public enum DumpKind { KernelMinidump, FullMemoryDump, LiveKernelReport, UserModeMinidump, Unknown }

public sealed class DumpFileInfo
{
    public string Path { get; set; } = "";
    public long SizeBytes { get; set; }
    public DateTime LastWriteTime { get; set; }
    public DumpKind Kind { get; set; } = DumpKind.Unknown;
    public uint? BugCheckCode { get; set; }
    public ulong[]? BugCheckParameters { get; set; }
    public DateTime? CrashTimeFromHeader { get; set; }
    public bool Is64Bit { get; set; }
    public string? ParseError { get; set; }
}

/// <summary>Un incident BSOD consolidé (fusion dump + événement 1001).</summary>
public sealed class BsodIncident
{
    public DateTime TimeLocal { get; set; }
    public uint? BugCheckCode { get; set; }
    public ulong[]? Parameters { get; set; }
    public string BugCheckName { get; set; } = "";
    public string? DumpPath { get; set; }
    public string? SuspectDriver { get; set; }
    public List<string> Sources { get; set; } = new(); // "Minidump", "Événement 1001"…
}

public sealed class ReliabilityRecord
{
    public DateTime TimeLocal { get; set; }
    public string SourceName { get; set; } = "";
    public int EventId { get; set; }
    public string ProductName { get; set; } = "";
    public string Message { get; set; } = "";
}

// ---------------------------------------------------------------------------
// Modèles : diagnostic
// ---------------------------------------------------------------------------

public enum Severity { Critical, Warning, Info }
public enum Confidence { High, Medium, Low }
public enum FaultCategory { Hardware, Memory, Storage, GpuDriver, Driver, Software, Power, WindowsUpdate, None }

public sealed class Finding
{
    public Severity Severity { get; set; }
    public Confidence Confidence { get; set; }
    public FaultCategory Category { get; set; }
    public string Title { get; set; } = "";
    public string Details { get; set; } = "";
    public string Recommendation { get; set; } = "";
}

public sealed class DiagnosticReport
{
    public DateTime GeneratedAt { get; set; } = DateTime.Now;
    public int ScanPeriodDays { get; set; }
    public SystemSnapshot System { get; set; } = new();
    public List<ProcessInfo> Processes { get; set; } = new();
    public List<BsodIncident> Bsods { get; set; } = new();
    public List<WinEvent> Events { get; set; } = new();
    public List<DumpFileInfo> Dumps { get; set; } = new();
    public List<ReliabilityRecord> ReliabilityRecords { get; set; } = new();
    public List<Finding> Findings { get; set; } = new();
    public string Verdict { get; set; } = "";
    public FaultCategory VerdictCategory { get; set; } = FaultCategory.None;
    /// <summary>Erreurs non bloquantes rencontrées pendant la collecte (transparence).</summary>
    public List<string> CollectorErrors { get; set; } = new();
    /// <summary>Chemin du script PowerShell de réparation généré (si des problèmes ont été trouvés).</summary>
    public string? RepairScriptPath { get; set; }
    /// <summary>Lanceur .bat à double-clic (élévation UAC + ExecutionPolicy Bypass automatiques).</summary>
    public string? RepairLauncherPath { get; set; }
}

public sealed class ScanOptions
{
    /// <summary>Période d'analyse de l'historique, en jours.</summary>
    public int Days { get; set; } = 30;
    /// <summary>Inclure l'inventaire des pilotes (peut prendre quelques secondes).</summary>
    public bool IncludeDrivers { get; set; } = true;
}

public sealed record ScanProgress(string Step, int Percent);
