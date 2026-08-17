using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace FaultTracePC.Core.Collectors;

/// <summary>
/// Lecture du journal de santé NVMe (SMART / Health Information Log, page 0x02)
/// directement auprès du disque, via <c>DeviceIoControl</c>.
///
/// Pourquoi ce détour : Windows n'expose PAS aux classes WMI les compteurs qui
/// disent réellement si un SSD NVMe s'abîme. Les classes MSStorageDriver_* sont
/// réservées à l'ATA/SATA, et MSFT_StorageReliabilityCounter ne donne au mieux
/// que température et usure. Le seul moyen d'obtenir l'équivalent des secteurs
/// défectueux sur un NVMe est d'interroger le contrôleur lui-même — c'est ce que
/// font CrystalDiskInfo et les outils des fabricants.
///
/// Ce que ça apporte, et qui n'existait nulle part ailleurs dans le rapport :
///  · Media and Data Integrity Errors — les erreurs d'intégrité irrécupérables,
///    l'analogue NVMe des secteurs illisibles ;
///  · Available Spare vs Available Spare Threshold — la réserve de blocs de
///    remplacement. Quand elle passe sous le seuil du fabricant, le disque est
///    en fin de vie : c'est LE signal d'alarme du NVMe ;
///  · Critical Warning — le drapeau que le contrôleur lève lui-même.
///
/// Aucune dépendance externe : uniquement des appels Win32.
/// </summary>
public static class NvmeSmartReader
{
    // ------------------------------------------------------------------
    // Interop Win32
    // ------------------------------------------------------------------

    private const uint GenericRead = 0x80000000;
    private const uint GenericWrite = 0x40000000;
    private const uint FileShareRead = 0x00000001;
    private const uint FileShareWrite = 0x00000002;
    private const uint OpenExisting = 3;

    /// <summary>CTL_CODE(IOCTL_STORAGE_BASE 0x2d, 0x0500, METHOD_BUFFERED, FILE_ANY_ACCESS).</summary>
    private const uint IoctlStorageQueryProperty = 0x002D1400;

    /// <summary>STORAGE_PROPERTY_ID.StorageDeviceProtocolSpecificProperty</summary>
    private const uint StorageDeviceProtocolSpecificProperty = 50;

    /// <summary>STORAGE_QUERY_TYPE.PropertyStandardQuery</summary>
    private const uint PropertyStandardQuery = 0;

    /// <summary>STORAGE_PROTOCOL_TYPE.ProtocolTypeNvme</summary>
    private const uint ProtocolTypeNvme = 3;

    /// <summary>STORAGE_PROTOCOL_NVME_DATA_TYPE.NVMeDataTypeLogPage</summary>
    private const uint NvmeDataTypeLogPage = 2;

    /// <summary>NVME_LOG_PAGE_HEALTH_INFO</summary>
    private const uint NvmeLogPageHealthInfo = 0x02;

    private const int ProtocolSpecificDataSize = 40; // 10 champs DWORD
    private const int HeaderSize = 8;                // Version + Size (sortie) / PropertyId + QueryType (entrée)
    private const int LogPageSize = 512;
    private const int BufferSize = HeaderSize + ProtocolSpecificDataSize + 4096;

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "CreateFileW")]
    private static extern SafeFileHandle CreateFile(
        string lpFileName, uint dwDesiredAccess, uint dwShareMode, IntPtr lpSecurityAttributes,
        uint dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(
        SafeFileHandle hDevice, uint dwIoControlCode,
        byte[] lpInBuffer, int nInBufferSize,
        byte[] lpOutBuffer, int nOutBufferSize,
        out int lpBytesReturned, IntPtr lpOverlapped);

    // ------------------------------------------------------------------
    // Résultat
    // ------------------------------------------------------------------

    public sealed class NvmeHealth
    {
        /// <summary>Drapeaux levés par le contrôleur (0 = aucun problème signalé).</summary>
        public byte CriticalWarning { get; init; }
        public int? TemperatureC { get; init; }
        /// <summary>Réserve de blocs de remplacement encore disponible, en %.</summary>
        public int AvailableSparePercent { get; init; }
        /// <summary>Seuil sous lequel le fabricant considère le disque en fin de vie.</summary>
        public int AvailableSpareThresholdPercent { get; init; }
        /// <summary>Pourcentage d'endurance consommé (peut dépasser 100).</summary>
        public int PercentageUsed { get; init; }
        /// <summary>Erreurs d'intégrité irrécupérables — l'analogue des secteurs illisibles.</summary>
        public ulong MediaErrors { get; init; }
        public ulong ErrorLogEntries { get; init; }
        public ulong PowerCycles { get; init; }
        public ulong PowerOnHours { get; init; }
        public ulong UnsafeShutdowns { get; init; }
    }

    // ------------------------------------------------------------------

    /// <summary>
    /// Lit le journal de santé du disque physique <paramref name="physicalDriveIndex"/>
    /// (0 = \\.\PhysicalDrive0). Renvoie null si le disque n'est pas NVMe, si le
    /// pilote ne relaie pas la commande, ou si l'accès est refusé. Ne lève jamais.
    /// </summary>
    public static NvmeHealth? TryRead(int physicalDriveIndex, List<string>? errors = null)
    {
        if (!OperatingSystem.IsWindows()) return null;

        try
        {
            using var handle = CreateFile(
                $@"\\.\PhysicalDrive{physicalDriveIndex}",
                GenericRead | GenericWrite,
                FileShareRead | FileShareWrite,
                IntPtr.Zero, OpenExisting, 0, IntPtr.Zero);

            if (handle.IsInvalid)
            {
                // Sans élévation, l'ouverture du disque physique est refusée : c'est
                // attendu et non bloquant (le service et l'interface sont élevés).
                errors?.Add(Lang.T($"SMART NVMe (disque {physicalDriveIndex}) : accès refusé (erreur {Marshal.GetLastWin32Error()}).", $"SMART NVMe (disk {physicalDriveIndex}): access denied (error {Marshal.GetLastWin32Error()})"));
                return null;
            }

            var buffer = new byte[BufferSize];

            // --- STORAGE_PROPERTY_QUERY ---
            WriteU32(buffer, 0, StorageDeviceProtocolSpecificProperty);
            WriteU32(buffer, 4, PropertyStandardQuery);

            // --- STORAGE_PROTOCOL_SPECIFIC_DATA, placé dans AdditionalParameters ---
            // ProtocolDataOffset est relatif au DÉBUT de cette structure, d'où 40 et
            // non 48 : c'est l'erreur classique sur cet IOCTL.
            int p = HeaderSize;
            WriteU32(buffer, p + 0, ProtocolTypeNvme);
            WriteU32(buffer, p + 4, NvmeDataTypeLogPage);
            WriteU32(buffer, p + 8, NvmeLogPageHealthInfo);
            WriteU32(buffer, p + 12, 0);                          // sous-valeur
            WriteU32(buffer, p + 16, ProtocolSpecificDataSize);   // ProtocolDataOffset
            WriteU32(buffer, p + 20, LogPageSize);                // ProtocolDataLength

            if (!DeviceIoControl(handle, IoctlStorageQueryProperty,
                    buffer, buffer.Length, buffer, buffer.Length, out _, IntPtr.Zero))
            {
                // Cas normal sur un disque SATA, un contrôleur RAID, ou un pilote
                // qui ne relaie pas les commandes NVMe : on n'en fait pas une erreur.
                return null;
            }

            // La page de log commence après l'en-tête du descripteur + l'offset annoncé.
            int dataOffset = HeaderSize + (int)ReadU32(buffer, HeaderSize + 16);
            int dataLength = (int)ReadU32(buffer, HeaderSize + 20);
            if (dataLength < 192 || dataOffset < 0 || dataOffset + dataLength > buffer.Length) return null;

            return Parse(buffer, dataOffset);
        }
        catch (Exception ex)
        {
            errors?.Add(Lang.T($"SMART NVMe (disque {physicalDriveIndex}) : {ex.Message}", $"NVMe SMART (disk {physicalDriveIndex}): {ex.Message}"));
            return null;
        }
    }

    /// <summary>
    /// Décodage de la page 0x02 telle que définie par la norme NVMe.
    /// Les compteurs y sont sur 128 bits ; les 64 bits de poids faible suffisent
    /// très largement (2^64 heures de fonctionnement n'arriveront pas).
    /// </summary>
    internal static NvmeHealth? Parse(byte[] b, int o)
    {
        byte warning = b[o + 0];
        int kelvin = b[o + 1] | (b[o + 2] << 8);
        int spare = b[o + 3];
        int spareThreshold = b[o + 4];
        int used = b[o + 5];

        // Un log entièrement nul signifie que le pilote a répondu sans rien remplir.
        if (warning == 0 && kelvin == 0 && spare == 0 && spareThreshold == 0 && used == 0
            && ReadU64(b, o + 128) == 0 && ReadU64(b, o + 112) == 0)
            return null;

        return new NvmeHealth
        {
            CriticalWarning = warning,
            TemperatureC = kelvin > 0 ? kelvin - 273 : null,
            AvailableSparePercent = spare,
            AvailableSpareThresholdPercent = spareThreshold,
            PercentageUsed = used,
            PowerCycles = ReadU64(b, o + 112),
            PowerOnHours = ReadU64(b, o + 128),
            UnsafeShutdowns = ReadU64(b, o + 144),
            MediaErrors = ReadU64(b, o + 160),
            ErrorLogEntries = ReadU64(b, o + 176),
        };
    }

    /// <summary>Description lisible des drapeaux d'alerte du contrôleur.</summary>
    public static string DescribeWarning(byte flags)
    {
        if (flags == 0) return "";
        var parts = new List<string>();
        if ((flags & 0x01) != 0) parts.Add(Lang.T("réserve de blocs sous le seuil critique", "spare block reserve below the critical threshold"));
        if ((flags & 0x02) != 0) parts.Add(Lang.T("température hors plage de fonctionnement", "temperature outside the operating range"));
        if ((flags & 0x04) != 0) parts.Add(Lang.T("fiabilité dégradée par usure ou erreurs", "reliability degraded by wear or errors"));
        if ((flags & 0x08) != 0) parts.Add(Lang.T("disque passé en LECTURE SEULE", "drive switched to READ-ONLY"));
        if ((flags & 0x10) != 0) parts.Add(Lang.T("sauvegarde de la mémoire volatile défaillante", "volatile memory backup failing"));
        if ((flags & 0x20) != 0) parts.Add(Lang.T("mémoire persistante en lecture seule", "persistent memory in read-only mode"));
        return parts.Count > 0 ? string.Join(Lang.T(" ; ", "; "), parts) : Lang.T($"drapeau inconnu (0x{flags:X2})", $"unknown flag (0x{flags:X2})");
    }

    // ------------------------------------------------------------------

    private static void WriteU32(byte[] b, int o, uint v)
    {
        b[o] = (byte)v; b[o + 1] = (byte)(v >> 8); b[o + 2] = (byte)(v >> 16); b[o + 3] = (byte)(v >> 24);
    }

    private static uint ReadU32(byte[] b, int o) =>
        (uint)(b[o] | (b[o + 1] << 8) | (b[o + 2] << 16) | (b[o + 3] << 24));

    private static ulong ReadU64(byte[] b, int o)
    {
        ulong v = 0;
        for (int i = 7; i >= 0; i--) v = (v << 8) | b[o + i];
        return v;
    }
}
