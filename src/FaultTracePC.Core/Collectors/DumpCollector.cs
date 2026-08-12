using System.Buffers.Binary;

namespace FaultTracePC.Core.Collectors;

/// <summary>
/// Énumère et analyse nativement les fichiers dump Windows, sans dépendance externe.
///
/// Les dumps noyau (Minidump, MEMORY.DMP, LiveKernelReports) commencent par un
/// en-tête DUMP_HEADER documenté publiquement (signature "PAGE" + "DU64" en 64 bits,
/// "PAGE" + "DUMP" en 32 bits). Le code STOP (BugCheckCode) et ses 4 paramètres y
/// sont stockés à offset fixe :
///   - 64 bits : BugCheckCode à 0x38 (uint32), paramètres à 0x40 (4 × uint64)
///   - 32 bits : BugCheckCode à 0x28 (uint32), paramètres à 0x2C (4 × uint32)
/// La date de crash de l'en-tête (FILETIME) est lue en best-effort avec contrôle de
/// vraisemblance ; sinon on retombe sur la date de modification du fichier.
///
/// Les dumps en mode utilisateur (WER) utilisent la signature "MDMP" : ils sont
/// listés mais le concept de BugCheck ne s'y applique pas.
/// </summary>
public sealed class DumpCollector
{
    private readonly List<string> _errors;

    public DumpCollector(List<string> errors) => _errors = errors;

    public List<DumpFileInfo> Collect()
    {
        var dumps = new List<DumpFileInfo>();
        var windir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

        // C:\Windows\Minidump\*.dmp
        SafeEnumerate(Path.Combine(windir, "Minidump"), "*.dmp", SearchOption.TopDirectoryOnly, dumps, DumpKind.KernelMinidump);

        // C:\Windows\MEMORY.DMP
        var memoryDmp = Path.Combine(windir, "MEMORY.DMP");
        if (File.Exists(memoryDmp))
            AddDump(dumps, memoryDmp, DumpKind.FullMemoryDump);

        // C:\Windows\LiveKernelReports\**\*.dmp (gels récupérés sans BSOD : watchdog GPU, USB…)
        SafeEnumerate(Path.Combine(windir, "LiveKernelReports"), "*.dmp", SearchOption.AllDirectories, dumps, DumpKind.LiveKernelReport);

        return dumps.OrderByDescending(d => d.CrashTimeFromHeader ?? d.LastWriteTime).ToList();
    }

    private void SafeEnumerate(string dir, string pattern, SearchOption option, List<DumpFileInfo> dumps, DumpKind kind)
    {
        try
        {
            if (!Directory.Exists(dir)) return;
            foreach (var f in Directory.EnumerateFiles(dir, pattern, option))
                AddDump(dumps, f, kind);
        }
        catch (Exception ex)
        {
            _errors.Add($"Dumps ({dir}) : {ex.Message}");
        }
    }

    private void AddDump(List<DumpFileInfo> dumps, string path, DumpKind kind)
    {
        var info = new DumpFileInfo { Path = path, Kind = kind };
        try
        {
            var fi = new FileInfo(path);
            info.SizeBytes = fi.Length;
            info.LastWriteTime = fi.LastWriteTime;
            ParseHeader(info);
        }
        catch (Exception ex)
        {
            info.ParseError = ex.Message;
        }
        dumps.Add(info);
    }

    private static void ParseHeader(DumpFileInfo info)
    {
        Span<byte> header = stackalloc byte[0x2000];
        int read;
        using (var fs = new FileStream(info.Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            read = fs.ReadAtLeast(header, header.Length, throwOnEndOfStream: false);
        if (read < 0x60) { info.ParseError = "Fichier trop court pour contenir un en-tête."; return; }

        uint sig = BinaryPrimitives.ReadUInt32LittleEndian(header);            // "PAGE" ou "MDMP"
        uint valid = BinaryPrimitives.ReadUInt32LittleEndian(header[4..]);     // "DU64" / "DUMP"

        const uint SIG_PAGE = 0x45474150; // "PAGE"
        const uint SIG_MDMP = 0x504D444D; // "MDMP"
        const uint VALID_DU64 = 0x34365544; // "DU64"
        const uint VALID_DUMP = 0x504D5544; // "DUMP"

        if (sig == SIG_MDMP)
        {
            info.Kind = DumpKind.UserModeMinidump;
            return;
        }
        if (sig != SIG_PAGE)
        {
            info.ParseError = "Signature inconnue (ni PAGE ni MDMP).";
            info.Kind = DumpKind.Unknown;
            return;
        }

        if (valid == VALID_DU64)
        {
            info.Is64Bit = true;
            info.BugCheckCode = BinaryPrimitives.ReadUInt32LittleEndian(header[0x38..]);
            var p = new ulong[4];
            for (int i = 0; i < 4; i++)
                p[i] = BinaryPrimitives.ReadUInt64LittleEndian(header[(0x40 + i * 8)..]);
            info.BugCheckParameters = p;
            if (read >= 0xFB0)
                info.CrashTimeFromHeader = TryReadFileTime(header, 0xFA8);
        }
        else if (valid == VALID_DUMP)
        {
            info.Is64Bit = false;
            info.BugCheckCode = BinaryPrimitives.ReadUInt32LittleEndian(header[0x28..]);
            var p = new ulong[4];
            for (int i = 0; i < 4; i++)
                p[i] = BinaryPrimitives.ReadUInt32LittleEndian(header[(0x2C + i * 4)..]);
            info.BugCheckParameters = p;
            if (read >= 0xFC8)
                info.CrashTimeFromHeader = TryReadFileTime(header, 0xFC0);
        }
        else
        {
            info.ParseError = "En-tête PAGE avec sous-signature inconnue.";
        }
    }

    /// <summary>
    /// Lit un FILETIME et ne le retient que s'il est vraisemblable (2005 → demain).
    /// Évite d'afficher une date fantaisiste si l'offset varie selon la version de Windows.
    /// </summary>
    private static DateTime? TryReadFileTime(ReadOnlySpan<byte> buf, int offset)
    {
        try
        {
            long ft = BinaryPrimitives.ReadInt64LittleEndian(buf[offset..]);
            if (ft <= 0) return null;
            var dt = DateTime.FromFileTime(ft);
            if (dt.Year < 2005 || dt > DateTime.Now.AddDays(1)) return null;
            return dt;
        }
        catch { return null; }
    }
}
