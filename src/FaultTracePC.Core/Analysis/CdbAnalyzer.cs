using System.ComponentModel;
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
/// TROUVÉ NE VEUT PAS DIRE UTILISABLE (point 80, constaté le 24/09/2026). Le
/// WinDbg du Microsoft Store range son cdb.exe sous <c>Program Files\WindowsApps</c> :
/// le fichier se voit, mais Windows refuse de le lancer par son chemin — cinq fois
/// « Accès refusé » sur cinq dumps. Les « Debugging Tools for Windows » du Windows
/// SDK, eux, s'installent dans un dossier ordinaire (<c>Windows Kits\10\Debuggers</c>)
/// et sont cherchés en premier. Un refus de lancement est traité comme un état de
/// la machine, dit UNE fois, et non comme un échec répété sur chaque dump.
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

        // Alias d'exécution du paquet WinDbg, s'il existe. NON VÉRIFIÉ : ce commentaire
        // affirmait jusqu'au 09/10/2026 que les versions récentes publient cdb.exe à
        // cet endroit, sans source. Sur la machine du 24/09/2026 l'alias était absent
        // et c'est le chemin protégé ci-dessous qui a été retenu. On le garde parce
        // qu'il ne coûte rien et qu'un alias, s'il existe, se lance normalement.
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

    /// <summary>
    /// Analyse les dumps noyau les plus récents (max <paramref name="maxDumps"/>) et
    /// rend ce qui s'est passé — c'est ce que le rapport emploie pour dire POURQUOI
    /// un pilote n'est pas nommé.
    /// </summary>
    /// <param name="cheminCdb">
    /// Pour les tests uniquement : impose l'exécutable au lieu de le chercher. Un
    /// refus de lancement ne se provoque pas autrement sur un poste de développement.
    /// </param>
    public EtatAnalyseProfonde AnalyzeAll(List<DumpFileInfo> dumps, int maxDumps,
        CancellationToken ct = default, string? cheminCdb = null)
    {
        var targets = dumps
            .Where(d => d.Kind is DumpKind.KernelMinidump or DumpKind.FullMemoryDump && d.ParseError is null)
            .OrderByDescending(d => d.CrashTimeFromHeader ?? d.LastWriteTime)
            .Take(maxDumps)
            .ToList();

        // RIEN À ANALYSER, RIEN À RÉCLAMER. Jusqu'au 09/10/2026 le débogueur était
        // cherché avant de savoir s'il y avait un dump noyau, et une machine qui
        // n'avait que des dumps d'applications recevait « WinDbg introuvable » pour
        // une analyse qui n'aurait de toute façon rien eu à faire.
        if (targets.Count == 0) return EtatAnalyseProfonde.SansObjet;

        var cdb = cheminCdb ?? LocateCdb();
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
            return EtatAnalyseProfonde.Absente;
        }

        for (int i = 0; i < targets.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var refus = AnalyzeOne(cdb, targets[i], i == 0 ? TimeoutMsFirst : TimeoutMsNext);
            if (refus is not null)
            {
                SignalerLancementRefuse(cdb, refus, targets.Skip(i).ToList());
                return EtatAnalyseProfonde.Inaccessible;
            }
        }
        return EtatAnalyseProfonde.Faite;
    }

    /// <summary>
    /// Analyse un dump. Rend le motif du refus si le débogueur n'a PAS PU ÊTRE LANCÉ
    /// du tout, null dans tous les autres cas — y compris un échec propre à ce dump,
    /// qui reste inscrit sur le dump lui-même.
    ///
    /// LA DIFFÉRENCE COMPTE. Un délai dépassé ou une sortie illisible concernent ce
    /// dump-là ; le suivant peut réussir. Un lancement refusé concerne la machine :
    /// le suivant échouera de la même façon, et le répéter cinq fois dans les
    /// limitations noyait la seule information utile.
    /// </summary>
    private string? AnalyzeOne(string cdb, DumpFileInfo dump, int timeoutMs)
    {
        Process? p;
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

            p = Process.Start(psi);
        }
        catch (Win32Exception ex)
        {
            // Le message de .NET répète le chemin et le dossier de travail ; seul le
            // motif de Windows (« Accès refusé », « n'est pas une application Win32
            // valide »…) est utile, le chemin est donné une fois par l'appelant.
            // Certains motifs portent un « %1 » que Windows remplit d'habitude
            // lui-même : on y met le nom du fichier plutôt que de le laisser brut.
            return new Win32Exception(ex.NativeErrorCode).Message.Replace("%1", Path.GetFileName(cdb));
        }
        catch (Exception ex)
        {
            dump.DeepAnalysisError = ex.Message;
            _errors.Add(Lang.T($"Analyse CDB de {Path.GetFileName(dump.Path)} : {ex.Message}", $"CDB analysis of {Path.GetFileName(dump.Path)}: {ex.Message}"));
            return null;
        }

        if (p is null) { dump.DeepAnalysisError = Lang.T("Impossible de démarrer CDB.", "Could not start CDB."); return null; }

        try
        {
            using (p)
            {
                var stdout = p.StandardOutput.ReadToEndAsync();
                _ = p.StandardError.ReadToEndAsync(); // drainé pour éviter tout blocage
                if (!p.WaitForExit(timeoutMs))
                {
                    try { p.Kill(entireProcessTree: true); } catch { }
                    dump.DeepAnalysisError = Lang.T($"Délai dépassé ({timeoutMs / 1000} s) — symboles trop longs à télécharger ?", $"Timed out ({timeoutMs / 1000} s) — symbols taking too long to download?");
                    return null;
                }

                Parse(dump, stdout.GetAwaiter().GetResult());
                dump.DeepAnalyzed = true;
            }
        }
        catch (Exception ex)
        {
            dump.DeepAnalysisError = ex.Message;
            _errors.Add(Lang.T($"Analyse CDB de {Path.GetFileName(dump.Path)} : {ex.Message}", $"CDB analysis of {Path.GetFileName(dump.Path)}: {ex.Message}"));
        }
        return null;
    }

    /// <summary>
    /// Le débogueur existe mais ne se lance pas : UNE ligne dans les limitations, qui
    /// dit lequel, pourquoi, et ce qui fonctionne à la place — et sur chaque dump non
    /// analysé, un renvoi court vers cette ligne.
    ///
    /// SURTOUT PAS « installer WinDbg » : il est installé. C'était le défaut constaté
    /// le 24/09/2026, où le rapport demandait d'installer ce qui était déjà là.
    /// </summary>
    private void SignalerLancementRefuse(string cdb, string motif, List<DumpFileInfo> nonAnalyses)
    {
        foreach (var d in nonAnalyses)
            d.DeepAnalysisError = Lang.T(
                "WinDbg présent mais impossible à lancer — voir les limitations en fin de rapport.",
                "WinDbg present but cannot be started — see the limitations at the end of the report.");

        var message = Lang.T(
            $"Analyse profonde impossible : le débogueur a été trouvé ({cdb}) mais Windows a refusé de le lancer ({motif.TrimEnd('.', ' ')}). ",
            $"Deep analysis impossible: the debugger was found ({cdb}) but Windows refused to start it ({motif.TrimEnd('.', ' ')}). ");

        // Le paquet du Store n'est nommé que si le chemin le désigne ET que le lancement
        // a été refusé : les deux ont été constatés ensemble, et seulement ensemble.
        if (cdb.Contains(@"\WindowsApps\", StringComparison.OrdinalIgnoreCase))
            message += Lang.T(
                "C'est la version de WinDbg installée depuis le Microsoft Store : son exécutable est rangé dans un dossier protégé et ne se lance pas par son chemin. ",
                "This is the version of WinDbg installed from the Microsoft Store: its executable sits in a protected folder and cannot be started by its path. ");

        message += Lang.T(
            $"{nonAnalyses.Count} fichier(s) d'incident n'ont donc pas été analysés ; ce n'est pas un défaut de la machine. "
            + "Ce qui fonctionne : les « Debugging Tools for Windows », que l'on installe depuis le Windows SDK en ne cochant que ce composant.",
            $"{nonAnalyses.Count} crash file(s) were therefore not analysed; this is not a fault of the machine. "
            + "What works: the “Debugging Tools for Windows”, installed from the Windows SDK by ticking only that component.");

        _errors.Add(message);
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
            dump.DeepAnalysisError = Lang.T("CDB n'a pas produit de verdict (sortie inattendue).",
                                            "CDB produced no verdict (unexpected output).");
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
