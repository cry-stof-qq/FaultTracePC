using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace FaultTracePC.Core.Analysis;

/// <summary>
/// Analyse profonde des dumps noyau via CDB (le moteur en ligne de commande de
/// WinDbg) : exécute « !analyze -v » et en extrait le module fautif, la signature
/// de crash et la pile d'appels — exactement ce que fait un technicien à la main.
///
/// CDB est cherché aux emplacements connus (Debugging Tools du SDK Windows,
/// PATH, paquet WinDbg du Store). S'il est absent, l'analyse est simplement
/// sautée et le rapport l'indique avec la commande d'installation.
///
/// Symboles : un cache local est utilisé (%LOCALAPPDATA%\FaultTracePC\Symbols),
/// alimenté par le serveur public Microsoft si internet est disponible. Sans
/// internet, CDB identifie tout de même le module via la liste des modules du
/// dump — seule la pile détaillée perd en précision.
/// </summary>
public sealed class CdbAnalyzer
{
    private readonly List<string> _errors;
    private const int TimeoutMsFirst = 240_000; // 1er dump : téléchargement de symboles possible
    private const int TimeoutMsNext = 120_000;

    public CdbAnalyzer(List<string> errors) => _errors = errors;

    /// <summary>Chemin de cdb.exe, ou null si introuvable.</summary>
    public static string? LocateCdb()
    {
        var candidates = new List<string>();
        void AddKit(string root)
        {
            if (string.IsNullOrEmpty(root)) return;
            candidates.Add(Path.Combine(root, @"Windows Kits\10\Debuggers\x64\cdb.exe"));
            candidates.Add(Path.Combine(root, @"Windows Kits\11\Debuggers\x64\cdb.exe"));
        }
        AddKit(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86));
        AddKit(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles));

        foreach (var c in candidates.Where(File.Exists))
            return c;

        // PATH
        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var p = Path.Combine(dir.Trim(), "cdb.exe");
                if (File.Exists(p)) return p;
            }
            catch { /* entrée PATH invalide */ }
        }

        // Alias d'exécution du paquet WinDbg (winget install Microsoft.WinDbg) :
        // les versions récentes publient cdb.exe dans le dossier des alias utilisateur.
        try
        {
            var alias = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                @"Microsoft\WindowsApps\cdb.exe");
            if (File.Exists(alias)) return alias;
        }
        catch { }

        // Paquet WinDbg (winget install Microsoft.WinDbg) — l'accès à WindowsApps peut être refusé.
        try
        {
            var windowsApps = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "WindowsApps");
            if (Directory.Exists(windowsApps))
            {
                foreach (var pkg in Directory.EnumerateDirectories(windowsApps, "Microsoft.WinDbg*")
                                             .OrderByDescending(d => d))
                {
                    var p = Path.Combine(pkg, "amd64", "cdb.exe");
                    if (File.Exists(p)) return p;
                }
            }
        }
        catch { /* ACL WindowsApps : ignoré */ }

        return null;
    }

    /// <summary>Analyse les dumps noyau les plus récents (max <paramref name="maxDumps"/>).</summary>
    public void AnalyzeAll(List<DumpFileInfo> dumps, int maxDumps, CancellationToken ct = default)
    {
        var cdb = LocateCdb();
        if (cdb is null)
        {
            // Le message disait déjà quoi faire, mais laissait l'utilisateur recopier
            // une commande à la main. Il renvoie désormais vers le bouton qui l'exécute.
            _errors.Add(Lang.T(
                "Analyse profonde indisponible : CDB/WinDbg introuvable. Sans lui, le code STOP "
                + "est lu nativement mais le pilote exact n'est pas nommé. "
                + "Pour l'installer : bouton 🧰 Outils, puis « 🐞 Installer WinDbg (analyse des dumps) ». "
                + "En ligne de commande : winget install Microsoft.WinDbg, ou les « Debugging Tools for "
                + "Windows » du SDK pour une installation valable sur toute la machine.",
                "Deep analysis unavailable: CDB/WinDbg not found. Without it the STOP code "
                + "is read natively but the exact driver is not named. "
                + "To install it: button 🧰 Tools, then “🐞 Install WinDbg (dump analysis)”. "
                + "From the command line: winget install Microsoft.WinDbg, or the “Debugging Tools for "
                + "Windows” from the SDK for a machine-wide installation."));
            return;
        }

        var targets = dumps
            .Where(d => d.Kind is DumpKind.KernelMinidump or DumpKind.FullMemoryDump && d.ParseError is null)
            .OrderByDescending(d => d.CrashTimeFromHeader ?? d.LastWriteTime)
            .Take(maxDumps)
            .ToList();

        bool first = true;
        foreach (var d in targets)
        {
            ct.ThrowIfCancellationRequested();
            AnalyzeOne(cdb, d, first ? TimeoutMsFirst : TimeoutMsNext);
            first = false;
        }
    }

    private void AnalyzeOne(string cdb, DumpFileInfo dump, int timeoutMs)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = cdb,
                Arguments = $"-z \"{dump.Path}\" -c \"!analyze -v; q\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
            };

            // Cache de symboles local + serveur Microsoft (sans écraser une config existante).
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("_NT_SYMBOL_PATH")))
            {
                var cache = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "FaultTracePC", "Symbols");
                Directory.CreateDirectory(cache);
                psi.EnvironmentVariables["_NT_SYMBOL_PATH"] = $"srv*{cache}*https://msdl.microsoft.com/download/symbols";
            }

            using var p = Process.Start(psi);
            if (p is null) { dump.DeepAnalysisError = Lang.T("Impossible de démarrer CDB.", "Could not start CDB."); return; }

            var stdout = p.StandardOutput.ReadToEndAsync();
            _ = p.StandardError.ReadToEndAsync(); // drainé pour éviter tout blocage
            if (!p.WaitForExit(timeoutMs))
            {
                try { p.Kill(entireProcessTree: true); } catch { }
                dump.DeepAnalysisError = Lang.T($"Délai dépassé ({timeoutMs / 1000} s) — symboles trop longs à télécharger ?", $"Timed out ({timeoutMs / 1000} s) — symbols taking too long to download?");
                return;
            }

            Parse(dump, stdout.GetAwaiter().GetResult());
            dump.DeepAnalyzed = true;
        }
        catch (Exception ex)
        {
            dump.DeepAnalysisError = ex.Message;
            _errors.Add(Lang.T($"Analyse CDB de {Path.GetFileName(dump.Path)} : {ex.Message}", $"CDB analysis of {Path.GetFileName(dump.Path)}: {ex.Message}"));
        }
    }

    // ------------------------------------------------------------------
    // Parsing de la sortie de !analyze -v
    // ------------------------------------------------------------------

    private static readonly Regex ImageRx = new(@"^IMAGE_NAME:\s+(\S+)", RegexOptions.Multiline | RegexOptions.Compiled);
    private static readonly Regex ModuleRx = new(@"^MODULE_NAME:\s+(\S+)", RegexOptions.Multiline | RegexOptions.Compiled);
    private static readonly Regex CausedByRx = new(@"^Probably caused by\s*:\s*(.+)$", RegexOptions.Multiline | RegexOptions.Compiled);
    private static readonly Regex BucketRx = new(@"^FAILURE_BUCKET_ID:\s+(\S+)", RegexOptions.Multiline | RegexOptions.Compiled);
    private static readonly Regex ProcessRx = new(@"^PROCESS_NAME:\s+(\S+)", RegexOptions.Multiline | RegexOptions.Compiled);
    private static readonly Regex StackRx = new(@"STACK_TEXT:\s*\r?\n(.*?)(?:\r?\n\r?\n|\r?\nSTACK_COMMAND)", RegexOptions.Singleline | RegexOptions.Compiled);

    private static void Parse(DumpFileInfo dump, string output)
    {
        string? Get(Regex rx) { var m = rx.Match(output); return m.Success ? m.Groups[1].Value.Trim() : null; }

        dump.FaultingModule = Get(ImageRx);
        // memory_corruption / Unknown_Image ne sont pas de vrais fichiers : garder tel quel, les règles l'interprètent.
        if (string.IsNullOrEmpty(dump.FaultingModule))
            dump.FaultingModule = Get(ModuleRx);

        dump.ProbablyCausedBy = Get(CausedByRx);
        dump.FailureBucket = Get(BucketRx);
        dump.CrashProcessName = Get(ProcessRx);

        var stack = StackRx.Match(output);
        if (stack.Success)
        {
            // On garde les 12 premières lignes utiles, sans les adresses brutes interminables.
            var lines = stack.Groups[1].Value
                .Split('\n')
                .Select(l => l.TrimEnd())
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .Take(12)
                .Select(ShortenStackLine);
            dump.StackExcerpt = string.Join("\n", lines);
        }

        if (dump.FaultingModule is null && dump.ProbablyCausedBy is null)
            dump.DeepAnalysisError = "CDB n'a pas produit de verdict (sortie inattendue).";
    }

    /// <summary>Réduit une ligne de pile « addr : addr : module!symbole+off » à sa partie lisible.</summary>
    private static string ShortenStackLine(string line)
    {
        var parts = line.Split(':', StringSplitOptions.TrimEntries);
        var last = parts.Length > 0 ? parts[^1] : line;
        // La partie symbolique est après la dernière suite d'adresses hexadécimales.
        var m = Regex.Match(line, @"([A-Za-z_][\w.]*!\S+|[A-Za-z_][\w]*\+0x[0-9a-fA-F]+)\s*$");
        return m.Success ? m.Value : (last.Length > 90 ? last[..90] : last);
    }
}
