using System.Text.Json.Serialization;

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
    /// <summary>Numéro physique (Win32_DiskDrive.Index = MSFT_PhysicalDisk.DeviceId).</summary>
    public int? Index { get; set; }
    public ulong SizeBytes { get; set; }
    public string InterfaceType { get; set; } = "";
    public string MediaType { get; set; } = "";       // SSD / HDD / inconnu
    public string WmiStatus { get; set; } = "";       // Win32_DiskDrive.Status ("OK"…)
    /// <summary>
    /// État déclaré par le disque lui-même (MSFT_PhysicalDisk.HealthStatus).
    /// Énumération et non texte : ce qui sert à DÉCIDER ne doit pas être le mot
    /// affiché à l'écran. Voir DiskHealth.cs.
    /// </summary>
    public DiskHealth Health { get; set; } = DiskHealth.NotReported;
    public int? TemperatureC { get; set; }            // MSFT_StorageReliabilityCounter, si dispo
    public int? WearPercent { get; set; }             // usure SSD, si dispo
    public ulong? PowerOnHours { get; set; }
    public ulong? ReadErrorsTotal { get; set; }

    /// <summary>
    /// Lettres des volumes portés par ce disque physique (« C: », « D: »…).
    ///
    /// C'est la seule désignation qu'un utilisateur non technicien reconnaît :
    /// « Disque 0 » ne lui dit rien, « C: » lui dit tout. Vide quand le disque ne
    /// porte aucun volume monté — un disque neuf, non partitionné, ou branché en
    /// lecture par un technicien.
    /// </summary>
    public List<string> Letters { get; set; } = new();

    /// <summary>Attributs SMART détaillés (ceux qui prédisent réellement une panne).</summary>
    public SmartInfo? Smart { get; set; }
}

/// <summary>
/// Les indicateurs SMART qui comptent vraiment. Le reste des dizaines d'attributs
/// n'a aucune valeur prédictive démontrée : on ne retient que ceux dont la hausse
/// annonce une défaillance.
/// </summary>
public sealed class SmartInfo
{
    /// <summary>true si le disque lui-même annonce une défaillance imminente (SMART status).</summary>
    public bool? PredictedFailure { get; set; }
    /// <summary>Attribut 5 : secteurs devenus défectueux et remplacés par des secteurs de réserve.</summary>
    public ulong? ReallocatedSectors { get; set; }
    /// <summary>Attribut 197 : secteurs instables en attente de réallocation — le signal le plus précoce.</summary>
    public ulong? PendingSectors { get; set; }
    /// <summary>Attribut 198 : secteurs illisibles définitivement.</summary>
    public ulong? UncorrectableSectors { get; set; }
    /// <summary>Attribut 199 : erreurs de transmission — signale presque toujours un CÂBLE défectueux.</summary>
    public ulong? UdmaCrcErrors { get; set; }
    /// <summary>Attribut 187 : erreurs non corrigeables signalées à l'hôte.</summary>
    public ulong? ReportedUncorrectable { get; set; }
    public ulong? PowerOnHours { get; set; }
    public ulong? PowerCycles { get; set; }
    public int? TemperatureC { get; set; }
    /// <summary>Pourcentage de durée de vie restante d'un SSD (100 = neuf).</summary>
    public int? SsdLifeLeftPercent { get; set; }
    /// <summary>Origine des données : "SMART (SATA)", "Compteurs Windows (NVMe)"…</summary>
    public string Source { get; set; } = "";

    // --- Spécifique NVMe (journal de santé lu directement auprès du disque) ---

    /// <summary>Réserve de blocs de remplacement restante, en % (NVMe).</summary>
    public int? AvailableSparePercent { get; set; }

    /// <summary>Seuil sous lequel le fabricant déclare le disque en fin de vie (NVMe).</summary>
    public int? AvailableSpareThresholdPercent { get; set; }

    /// <summary>Drapeaux d'alerte levés par le contrôleur NVMe (0 = aucun).</summary>
    public byte? CriticalWarning { get; set; }

    /// <summary>Arrêts brutaux comptés par le disque (NVMe).</summary>
    public ulong? UnsafeShutdowns { get; set; }

    /// <summary>La réserve de blocs est-elle passée sous le seuil du fabricant ?</summary>
    public bool SpareExhausted =>
        AvailableSparePercent is { } s && AvailableSpareThresholdPercent is { } t && t > 0 && s < t;

    /// <summary>Total des secteurs défectueux (réalloués + en attente + illisibles).</summary>
    public ulong BadSectors =>
        (ReallocatedSectors ?? 0) + (PendingSectors ?? 0) + (UncorrectableSectors ?? 0);

    /// <summary>
    /// Vrai si au moins UN compteur a réellement été lu. Sans ce garde-fou, un objet
    /// vide s'affichait sous forme d'une ligne de tirets — ce qui faisait passer
    /// « je n'ai rien mesuré » pour « tout va bien ». Un outil de diagnostic ne doit
    /// jamais présenter une absence de mesure comme un résultat.
    /// </summary>
    public bool HasData =>
        PredictedFailure is not null || ReallocatedSectors is not null || PendingSectors is not null ||
        UncorrectableSectors is not null || UdmaCrcErrors is not null || ReportedUncorrectable is not null ||
        PowerOnHours is not null || PowerCycles is not null || TemperatureC is not null ||
        SsdLifeLeftPercent is not null || AvailableSparePercent is not null ||
        CriticalWarning is not null || UnsafeShutdowns is not null;
}

/// <summary>État de la batterie d'un portable.</summary>
public sealed class BatteryInfo
{
    public string Name { get; set; } = "";
    public string Chemistry { get; set; } = "";
    /// <summary>Capacité prévue par le constructeur (mWh).</summary>
    public uint? DesignedCapacity { get; set; }
    /// <summary>Capacité réellement atteinte à pleine charge aujourd'hui (mWh).</summary>
    public uint? FullChargedCapacity { get; set; }
    public uint? CycleCount { get; set; }
    /// <summary>Charge actuelle en %.</summary>
    public ushort? ChargeRemainingPercent { get; set; }
    public string Status { get; set; } = "";

    /// <summary>Usure en % : 0 = comme neuve, 100 = ne tient plus rien.</summary>
    public int? WearPercent =>
        DesignedCapacity is > 0 && FullChargedCapacity is not null
            ? Math.Clamp((int)Math.Round(100.0 * (1.0 - (double)FullChargedCapacity.Value / DesignedCapacity.Value)), 0, 100)
            : null;

    /// <summary>Santé restante en % (100 - usure).</summary>
    public int? HealthPercent => WearPercent is { } w ? 100 - w : null;
}

/// <summary>Un logiciel installé (registre de désinstallation).</summary>
public sealed class InstalledApp
{
    public string Name { get; set; } = "";
    public string Version { get; set; } = "";
    public string Publisher { get; set; } = "";
    public DateTime? InstallDate { get; set; }
    public string InstallLocation { get; set; } = "";
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

/// <summary>
/// POINT 53. Ce que le rapport doit savoir dire du réseau. Dans un parc, « plus de
/// réseau » est l'une des pannes les plus fréquentes, et c'était le seul domaine sur
/// lequel le logiciel n'avait rien à dire.
/// </summary>
public sealed class NetworkInfo
{
    public List<NetworkAdapterInfo> Adapters { get; set; } = new();

    /// <summary>
    /// Nombre de réseaux Wi-Fi enregistrés. <b>-1 signifie « pas pu regarder »</b>, ce
    /// qui n'est pas la même chose que zéro : l'un empêche de conclure, l'autre EST la
    /// conclusion. Les confondre ferait annoncer une panne sur une machine saine.
    /// </summary>
    public int WifiProfileCount { get; set; } = -1;
    public string WifiProfileNote { get; set; } = "";

    public List<ServiceStateInfo> Services { get; set; } = new();
    public bool PartOfDomain { get; set; }
    public string Domain { get; set; } = "";

    /// <summary>Carte sans fil RÉELLE : les adaptateurs virtuels ne comptent pas.</summary>
    public bool HasWireless => Adapters.Any(a => a.IsWireless && a.IsPhysical);
}

public sealed class NetworkAdapterInfo
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    /// <summary>Wi-Fi, Ethernet, ou autre.</summary>
    public string Kind { get; set; } = "";
    public bool IsWireless { get; set; }
    /// <summary>État opérationnel rendu par Windows (Up, Down, NotPresent…).</summary>
    public string Status { get; set; } = "";
    /// <summary>Constructeur seul : une adresse MAC complète identifie une machine.</summary>
    public string MacMasked { get; set; } = "";
    /// <summary>
    /// Carte matérielle réelle ? Les cartes virtuelles — VPN, VirtualBox, Wi-Fi Direct —
    /// se déclarent comme les autres. En compter une comme « carte Wi-Fi présente »
    /// ferait conclure à une panne de Wi-Fi sur un poste fixe qui n'en a jamais eu.
    /// </summary>
    public bool IsPhysical { get; set; } = true;
    public string DriverVersion { get; set; } = "";
    public DateTime? DriverDate { get; set; }
    public bool HasIpV4 { get; set; }
}

public sealed class ServiceStateInfo
{
    public string Name { get; set; } = "";
    public string DisplayName { get; set; } = "";
    /// <summary>Valeur BRUTE de WMI (Running, Stopped…) : c'est elle qui sert à décider.</summary>
    public string State { get; set; } = "";
    /// <summary>Valeur BRUTE de WMI (Auto, Manual, Disabled…).</summary>
    public string StartMode { get; set; } = "";

    /// <summary>
    /// Ces deux libellés sont ce qu'on AFFICHE. Le rapport français montrait
    /// « Running · Auto », c'est-à-dire du texte anglais de WMI laissé tel quel.
    /// La valeur brute reste seule à servir aux comparaisons : traduire ce qui décide
    /// est le meilleur moyen de casser une règle le jour où la langue change.
    /// </summary>
    public string StateLabel => State.ToLowerInvariant() switch
    {
        "running" => Lang.T("en cours", "running"),
        "stopped" => Lang.T("arrêté", "stopped"),
        "paused" => Lang.T("suspendu", "paused"),
        "start pending" => Lang.T("démarrage en cours", "starting"),
        "stop pending" => Lang.T("arrêt en cours", "stopping"),
        _ => State,
    };

    public string StartModeLabel => StartMode.ToLowerInvariant() switch
    {
        "auto" => Lang.T("automatique", "automatic"),
        "manual" => Lang.T("à la demande", "on demand"),
        "disabled" => Lang.T("désactivé", "disabled"),
        "boot" or "system" => Lang.T("au démarrage de Windows", "at Windows start-up"),
        _ => StartMode,
    };

    /// <summary>
    /// Un service en démarrage « Manual » est conçu pour rester arrêté tant que rien
    /// ne le réclame : sous Windows moderne, la plupart sont même à déclenchement
    /// automatique. L'arrêt n'y est PAS une panne.
    ///
    /// Constaté le 18/09/2026 sur deux machines en parfait état de marche : NlaSvc,
    /// arrêté et en démarrage « Manual » sur les deux, était signalé comme un défaut.
    /// Un faux positif sur chaque poste du parc décrédibilise tout le reste du rapport.
    /// </summary>
    public bool DoitTourner => StartMode.Equals("Auto", StringComparison.OrdinalIgnoreCase)
                            || StartMode.Equals("Automatic", StringComparison.OrdinalIgnoreCase);
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
    /// <summary>Batteries (vide sur un poste fixe).</summary>
    public List<BatteryInfo> Batteries { get; set; } = new();
    /// <summary>Logiciels installés — sert à vérifier si un logiciel fautif est encore présent.</summary>
    public List<InstalledApp> InstalledApps { get; set; } = new();
    /// <summary>État du réseau : cartes, profils Wi-Fi, services, domaine.</summary>
    public NetworkInfo Network { get; set; } = new();

    /// <summary>
    /// Ce que Windows garde en mémoire des supports déjà montés sur ce poste.
    /// Sert à mettre un nom sur un disque qui n'est plus là au moment de l'analyse.
    /// </summary>
    public StorageHistoryInfo StorageHistory { get; set; } = new();

    public string MachineName { get; set; } = "";
}

/// <summary>
/// Trace laissée dans la base de registre par les supports montés par le passé.
///
/// POURQUOI CETTE SOURCE EXISTE
/// Le journal Windows n'écrit qu'un numéro de disque — « \Device\Harddisk1 » — et ce
/// numéro est réattribué à chaque branchement. Sur un support débranché depuis, le
/// rapport ne pouvait donc rien dire de plus que « un disque qui portait le numéro 1 ».
/// La base de registre, elle, retient quelle lettre a été montée par quel matériel :
/// c'est la seule façon de proposer un nom.
///
/// C'EST UNE PISTE, PAS UNE IDENTIFICATION. Rien dans le registre ne relie une lettre
/// au numéro de disque qu'elle portait ce jour-là. Le rapport doit le dire.
/// </summary>
/// <summary>
/// D'où vient la connaissance d'un poste. Un même poste peut venir de plusieurs
/// sources : les indicateurs se cumulent au lieu de se remplacer.
///
/// POSTES.CSV N'EST PAS UNE SOURCE. Il n'apporte qu'une adresse MAC à un poste
/// déjà listé — voir <see cref="ParkInventory"/> pour le pourquoi.
/// </summary>
[Flags]
public enum SourcesDuPoste
{
    Aucune = 0,
    /// <summary>Compte d'ordinateur trouvé dans l'Active Directory.</summary>
    ActiveDirectory = 1,
    /// <summary>Déjà suivi par la console de parc (parc.json).</summary>
    Console = 2,
}

/// <summary>
/// Un poste tel que les sources le décrivent, avant toute action. Aucun champ ici
/// n'est le résultat d'une interrogation du poste lui-même : cette liste se
/// construit sans qu'aucune machine ne soit contactée.
/// </summary>
public sealed class PosteDuParc
{
    /// <summary>
    /// Nom Windows court, en majuscules. C'est la CLÉ : le jeton du mode parc s'en
    /// déduit, le déploiement le nomme, et les trois sources s'y reconnaissent.
    /// </summary>
    public string Name { get; set; } = "";

    public SourcesDuPoste Sources { get; set; }

    /// <summary>Adresse à employer pour joindre le poste : nom DNS ou adresse IP.</summary>
    public string Host { get; set; } = "";

    public int Port { get; set; } = ParkInventory.PortParDefaut;

    /// <summary>
    /// Adresse MAC connue, pour le réveil réseau. Vient de postes.csv et peut donc
    /// être périmée — le script interroge le DHCP en premier quand il en a un.
    /// Vide signifie « pas connue », jamais « pas de carte réseau ».
    /// </summary>
    public string Mac { get; set; } = "";

    /// <summary>Unité d'organisation d'où le compte a été lu, en clair.</summary>
    public string OrganizationalUnit { get; set; } = "";

    /// <summary>Dernière ouverture de session connue de l'annuaire, si elle l'est.</summary>
    public DateTime? LastLogon { get; set; }

    /// <summary>Compte d'ordinateur désactivé dans l'annuaire.</summary>
    public bool Disabled { get; set; }

    public bool MacConnue => Mac.Length > 0;

    /// <summary>Les sources, en toutes lettres — « annuaire », « console », « annuaire + console ».</summary>
    public string SourcesLabel
    {
        get
        {
            var noms = new List<string>();
            if (Sources.HasFlag(SourcesDuPoste.ActiveDirectory)) noms.Add(Lang.T("annuaire", "directory"));
            if (Sources.HasFlag(SourcesDuPoste.Console)) noms.Add(Lang.T("console", "console"));
            return noms.Count == 0 ? Lang.T("inconnue", "unknown") : string.Join(" + ", noms);
        }
    }
}

public sealed class StorageHistoryInfo
{
    /// <summary>
    /// <b>false signifie « pas pu regarder »</b> (accès refusé, clé absente), ce qui
    /// n'est pas la même chose qu'une liste vide : l'un empêche de conclure, l'autre
    /// EST une conclusion. La lecture de SYSTEM\MountedDevices demande des droits
    /// d'administrateur.
    /// </summary>
    public bool Readable { get; set; }
    public string Note { get; set; } = "";

    public List<RememberedVolume> Volumes { get; set; } = new();

    /// <summary>Supports amovibles connus de Windows mais non montés au moment de l'analyse.</summary>
    public List<RememberedVolume> AmoviblesAbsents =>
        Volumes.Where(v => v.IsRemovable && !v.CurrentlyMounted).ToList();
}

public sealed class RememberedVolume
{
    /// <summary>Lettre de lecteur, « D: ».</summary>
    public string Letter { get; set; } = "";

    /// <summary>Le dernier matériel à avoir porté cette lettre était un support USB de stockage.</summary>
    public bool IsRemovable { get; set; }

    /// <summary>« Disk&amp;Ven_SanDisk&amp;Prod_Cruzer_Blade&amp;Rev_1.00 », sans le numéro de série.</summary>
    public string DeviceId { get; set; } = "";

    /// <summary>Nom lisible déclaré par le matériel, quand Windows l'a retenu.</summary>
    public string FriendlyName { get; set; } = "";

    /// <summary>La lettre est-elle montée MAINTENANT ?</summary>
    public bool CurrentlyMounted { get; set; }

    /// <summary>Ce qu'on affiche : le nom lisible si Windows l'a, sinon l'identifiant matériel.</summary>
    public string Label => FriendlyName.Length > 0 ? FriendlyName : DeviceId;
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

    // --- Résultats de l'analyse profonde (CDB/WinDbg, Phase 2) ---
    /// <summary>true si !analyze -v a été exécuté sur ce dump.</summary>
    public bool DeepAnalyzed { get; set; }
    /// <summary>IMAGE_NAME : le module désigné fautif (ex: nvlddmkm.sys).</summary>
    public string? FaultingModule { get; set; }
    /// <summary>Ligne « Probably caused by » de CDB.</summary>
    public string? ProbablyCausedBy { get; set; }
    /// <summary>FAILURE_BUCKET_ID : signature de classement Microsoft du crash.</summary>
    public string? FailureBucket { get; set; }
    /// <summary>Processus actif au moment du crash (PROCESS_NAME).</summary>
    public string? CrashProcessName { get; set; }
    /// <summary>Premières lignes de la pile d'appels (STACK_TEXT).</summary>
    public string? StackExcerpt { get; set; }
    public string? DeepAnalysisError { get; set; }
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
public enum FaultCategory { Hardware, Memory, Storage, GpuDriver, Driver, Software, Power, WindowsUpdate, Network, None }

public sealed class Finding
{
    public Severity Severity { get; set; }
    public Confidence Confidence { get; set; }
    public FaultCategory Category { get; set; }

    /// <summary>
    /// Identifiant stable de la règle qui a produit cette conclusion, quand une
    /// AUTRE partie du code doit la reconnaître. Vide dans le cas général.
    ///
    /// POURQUOI CE CHAMP EXISTE
    /// Le verdict final cherchait la conclusion « pilote fautif identifié » en
    /// testant le DÉBUT DE SON TITRE. Le titre étant désormais traduit, ce test
    /// aurait été vrai en français et faux en anglais : sur un poste anglais, la
    /// preuve la plus forte du diagnostic — un pilote nommé par l'analyse
    /// symbolique — aurait été ignorée au profit d'une catégorie déduite des seuls
    /// codes STOP. Une conclusion qui sert à DÉCIDER ne se reconnaît pas à son
    /// texte affiché.
    /// </summary>
    public string Code { get; set; } = "";

    /// <summary>Objet nommé par la conclusion (fichier .sys, modèle de disque…), non traduit.</summary>
    public string Subject { get; set; } = "";

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

    /// <summary>
    /// Catégories d'événements pour lesquelles la collecte a buté sur son plafond
    /// (voir <see cref="Collectors.EventLogCollector.MaxEvenementsParRequete"/>).
    ///
    /// Pour ces catégories-là, compter les événements donne un PLANCHER et non un
    /// total : le rapport doit écrire « au moins N », jamais « N ».
    /// </summary>
    public HashSet<EventCategory> TruncatedEventCategories { get; set; } = new();

    /// <summary>
    /// Actions d'entretien effectuées pendant l'analyse — purge de l'historique
    /// notamment.
    ///
    /// Distinct de <see cref="CollectorErrors"/> : ce ne sont pas des échecs, et les
    /// ranger parmi les « limitations de cette analyse » ferait passer un entretien
    /// normal pour un problème. Mais ce sont des modifications du disque de
    /// l'utilisateur, donc elles se disent.
    /// </summary>
    public List<string> Notes { get; set; } = new();
    /// <summary>Chemin du script PowerShell de réparation généré (si des problèmes ont été trouvés).</summary>
    public string? RepairScriptPath { get; set; }
    /// <summary>Lanceur .bat à double-clic (élévation UAC + ExecutionPolicy Bypass automatiques).</summary>
    public string? RepairLauncherPath { get; set; }
    /// <summary>Comparaison avec le scan précédent (null au premier scan).</summary>
    public ScanComparison? Comparison { get; set; }
    /// <summary>Données de la boîte noire (surveillance temps réel), si le journal existe.</summary>
    public FlightInfo Flight { get; set; } = new();
}

// ---------------------------------------------------------------------------
// Boîte noire (surveillance temps réel) — format partagé service/lecteur
// ---------------------------------------------------------------------------

/// <summary>
/// Une ligne du journal de vol (JSONL). Noms courts pour un journal compact.
/// Kind : "s"=échantillon, "e"=événement, "b"=début de session, "x"=arrêt propre.
/// </summary>
public sealed class FlightSample
{
    [JsonPropertyName("t")] public DateTime Time { get; set; }
    [JsonPropertyName("k")] public string Kind { get; set; } = "s";
    [JsonPropertyName("cpu")] public double? CpuLoad { get; set; }
    [JsonPropertyName("ct")] public double? CpuTemp { get; set; }
    [JsonPropertyName("gt")] public double? GpuTemp { get; set; }
    [JsonPropertyName("gl")] public double? GpuLoad { get; set; }
    [JsonPropertyName("m")] public double? MemPct { get; set; }
    [JsonPropertyName("c")] public double? CommitPct { get; set; }
    [JsonPropertyName("top")] public string? TopProcesses { get; set; }
    [JsonPropertyName("ec")] public string? EventCategory { get; set; }
    [JsonPropertyName("em")] public string? EventMessage { get; set; }
    [JsonPropertyName("ab")] public bool? PreviousEndedAbruptly { get; set; }
}

/// <summary>Les dernières secondes enregistrées avant un crash/arrêt brutal.</summary>
public sealed class FlightCrashContext
{
    public DateTime CrashTime { get; set; }
    public string Trigger { get; set; } = "";
    public List<FlightSample> Samples { get; set; } = new();

    /// <summary>
    /// Temps écoulé entre le dernier relevé et l'heure de l'incident. Un écart de
    /// plusieurs dizaines de secondes n'est pas un trou dans les données : c'est la
    /// machine qui a cessé de répondre avant de tomber, et c'est une mesure en soi.
    /// </summary>
    public TimeSpan? SilenceBefore =>
        Samples.Count == 0 ? null : CrashTime - Samples[^1].Time;
}

/// <summary>
/// Une alerte préventive émise par le service de surveillance : un signe avant-coureur
/// détecté AVANT la panne (surchauffe, disque qui se dégrade, mémoire qui sature…).
/// </summary>
public sealed class PreventiveAlert
{
    [JsonPropertyName("t")] public DateTime Time { get; set; }
    /// <summary>Identifiant stable de la règle (sert à l'anti-répétition) : cpu_temp, gpu_temp, commit, whea…</summary>
    [JsonPropertyName("id")] public string RuleId { get; set; } = "";
    /// <summary>"warn" (à surveiller) ou "crit" (agir maintenant).</summary>
    [JsonPropertyName("lv")] public string Level { get; set; } = "warn";
    [JsonPropertyName("ti")] public string Title { get; set; } = "";
    [JsonPropertyName("de")] public string Details { get; set; } = "";
    [JsonPropertyName("re")] public string Recommendation { get; set; } = "";
    /// <summary>Valeur mesurée ayant déclenché l'alerte (température, %, compteur…).</summary>
    [JsonPropertyName("va")] public double? Value { get; set; }

    /// <summary>
    /// Fragment NON déductible cité par le texte : extrait du message de Windows,
    /// liste des processus dominants. Conservé à part pour que la phrase puisse
    /// être refabriquée dans une autre langue — voir AlertCatalog. Absent des
    /// alertes écrites avant la 1.3.0 : leur texte d'origine est alors gardé.
    /// </summary>
    [JsonPropertyName("ex")] public string? Extract { get; set; }
}

/// <summary>Seuils de déclenchement des alertes préventives (ProgramData\FaultTracePC\alerts.json).</summary>
public sealed class AlertSettings
{
    public bool Enabled { get; set; } = true;
    public double CpuTempWarn { get; set; } = 85;
    public double CpuTempCrit { get; set; } = 95;
    public double GpuTempWarn { get; set; } = 85;
    public double GpuTempCrit { get; set; } = 95;
    /// <summary>Mémoire virtuelle engagée (%) — au-delà, gels et plantages guettent.</summary>
    public double CommitWarn { get; set; } = 90;
    public double CommitCrit { get; set; } = 97;
    /// <summary>Nombre d'échantillons consécutifs au-dessus du seuil avant d'alerter (anti-faux positif).</summary>
    public int ConsecutiveSamples { get; set; } = 3;
    /// <summary>Délai minimal avant de ré-alerter sur la même règle (minutes).</summary>
    public int RepeatMinutes { get; set; } = 60;
    /// <summary>Contrôle de la santé des disques (SMART) toutes les N minutes.</summary>
    public int DiskCheckMinutes { get; set; } = 60;

    public static string SettingsPath => Path.Combine(RemoteConfig.BaseDir, "alerts.json");
    public static string AlertsLogPath => Path.Combine(RemoteConfig.BaseDir, "Flight", "alerts.jsonl");

    public static AlertSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath) &&
                System.Text.Json.JsonSerializer.Deserialize<AlertSettings>(File.ReadAllText(SettingsPath)) is { } s)
                return s;
        }
        catch { }
        return new AlertSettings();
    }

    public void Save()
    {
        Directory.CreateDirectory(RemoteConfig.BaseDir);
        File.WriteAllText(SettingsPath,
            System.Text.Json.JsonSerializer.Serialize(this, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
    }
}

/// <summary>État de la boîte noire au moment du scan.</summary>
public sealed class FlightInfo
{
    public bool JournalFound { get; set; }
    public DateTime? LastSampleTime { get; set; }
    /// <summary>true si un échantillon date de moins de 2 minutes (service actif).</summary>
    public bool Active { get; set; }
    public int AbruptSessionEnds { get; set; }
    public int DaysCovered { get; set; }
    public List<FlightCrashContext> Contexts { get; set; } = new();
    /// <summary>Alertes préventives émises par le service sur la période analysée.</summary>
    public List<PreventiveAlert> Alerts { get; set; } = new();

    /// <summary>Temps cumulé passé au-dessus des seuils, par capteur.</summary>
    public List<ThermalStats> Thermal { get; set; } = new();
}

/// <summary>
/// Bilan thermique d'un capteur sur la période : ce n'est pas la température
/// instantanée qui annonce un plantage, mais le temps passé trop haut.
/// </summary>
public sealed class ThermalStats
{
    public string Sensor { get; set; } = "";
    public double WarnThreshold { get; set; }
    public double CritThreshold { get; set; }
    public double? MaxC { get; set; }
    public DateTime? MaxAt { get; set; }
    public double? AverageC { get; set; }
    public int SampleCount { get; set; }
    /// <summary>Durée réellement couverte par les relevés (hors coupures de mesure).</summary>
    public TimeSpan Observed { get; set; }
    public TimeSpan AboveWarn { get; set; }
    public TimeSpan AboveCrit { get; set; }
    public List<ThermalEpisode> LongestEpisodes { get; set; } = new();

    /// <summary>Part du temps observé passée au-dessus du seuil d'alerte.</summary>
    public double WarnPercent =>
        Observed.TotalSeconds <= 0 ? 0 : Math.Round(100 * AboveWarn.TotalSeconds / Observed.TotalSeconds, 1);

    public bool HasData => SampleCount > 0;
}

/// <summary>Un épisode continu au-dessus du seuil d'alerte.</summary>
public sealed class ThermalEpisode
{
    public DateTime Start { get; set; }
    public double Minutes { get; set; }
    public double PeakC { get; set; }
}

/// <summary>Résultat de la comparaison avec le scan précédent — la boucle « est-ce réparé ? ».</summary>
public sealed class ScanComparison
{
    public DateTime PreviousScanAt { get; set; }
    /// <summary>Phrase de synthèse : réparation efficace / problème persistant / stable…</summary>
    public string Assessment { get; set; } = "";
    /// <summary>Tonalité pour l'affichage : ok / warn / crit.</summary>
    public string Tone { get; set; } = "ok";
    public int NewBsodCount { get; set; }
    public List<string> NewBsods { get; set; } = new();
    /// <summary>true si un nouveau crash a la même signature (code/pilote) qu'avant.</summary>
    public bool SameSignatureRecurred { get; set; }
    public List<string> DriverUpdates { get; set; } = new();

    /// <summary>
    /// POINT 55. Nom du pilote accusé par la conclusion qui a été REMPLACÉ entre les
    /// deux analyses, alors que des plantages de même signature sont survenus après.
    ///
    /// C'est la phrase qui clôt un dossier : le pilote a été changé, les plantages ont
    /// continué, donc ce n'est pas lui. Sans ce rapprochement, la recommandation reste
    /// « réinstaller proprement le pilote » — c'est-à-dire refaire ce qui vient
    /// d'échouer. Constaté le 14/09/2026 sur POSTE-TEMOIN : nvlddmkm.sys est passé de
    /// 32.0.15.8216 à 32.0.15.8278 entre deux analyses, et quatre écrans bleus portant
    /// la même signature ont suivi.
    /// </summary>
    public string? SuspectDriverReplacedInVain { get; set; }
    public List<string> DiskChanges { get; set; } = new();
    public int NewDiskErrorEvents { get; set; }
    public int NewWheaEvents { get; set; }
    public string MemoryTrend { get; set; } = "";

    /// <summary>
    /// Dégradations matérielles constatées DEPUIS le scan précédent.
    ///
    /// Distinct de <see cref="DiskChanges"/>, qui liste toutes les variations, y
    /// compris anodines (température, retour à un meilleur état). Ici ne figurent
    /// que les évolutions défavorables — celles qui doivent peser sur le verdict.
    /// </summary>
    public List<HardwareConcern> HardwareConcerns { get; set; } = new();

    /// <summary>
    /// Sévérité la plus élevée parmi <see cref="HardwareConcerns"/> :
    /// "" (aucune), "warn" ou "crit".
    /// </summary>
    public string HardwareSeverity { get; set; } = "";
}

/// <summary>
/// Une dégradation matérielle constatée entre deux scans.
///
/// POURQUOI CE TYPE EXISTE : jusqu'à la 1.2.0, la conclusion du rapport ne se
/// calculait qu'à partir des plantages. Une machine qui n'avait jamais planté
/// mais dont le disque perdait des secteurs s'entendait dire « Machine stable »,
/// l'alerte étant reléguée en petit sous le titre. C'est précisément le cas où
/// l'utilisateur n'a AUCUN autre signal pour se méfier — et donc le pire endroit
/// possible pour le rassurer.
/// </summary>
public sealed class HardwareConcern
{
    /// <summary>"warn" ou "crit".</summary>
    public string Severity { get; set; } = "warn";

    /// <summary>Phrase explicative, destinée à être lue telle quelle par l'utilisateur.</summary>
    public string Message { get; set; } = "";
}

public sealed class ScanOptions
{
    /// <summary>Période d'analyse de l'historique, en jours.</summary>
    public int Days { get; set; } = 30;
    /// <summary>Inclure l'inventaire des pilotes (peut prendre quelques secondes).</summary>
    public bool IncludeDrivers { get; set; } = true;
    /// <summary>Analyse profonde des dumps via CDB/WinDbg si présent (identifie le pilote exact).</summary>
    public bool DeepDumpAnalysis { get; set; } = true;
    /// <summary>Nombre maximum de dumps analysés en profondeur (les plus récents d'abord).</summary>
    public int MaxDeepDumps { get; set; } = 5;
}

public sealed record ScanProgress(string Step, int Percent);
