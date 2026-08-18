using System.Text;
using System.Text.Json;
using FaultTracePC.Core;
using FaultTracePC.Core.Report;

namespace FaultTracePC.Cli;

/// <summary>
/// FaultTracePC en ligne de commande : diagnostic silencieux, rapport déposé où
/// on veut (dossier local ou partage réseau UNC), code de sortie exploitable par
/// un script — pensé pour un déploiement de parc (GPO, tâche planifiée, SCCM…).
///
/// Codes de sortie :
///   0 = aucun problème significatif
///   1 = avertissements uniquement
///   2 = au moins une conclusion critique
///   3 = erreur d'exécution (droits, chemin inaccessible…)
/// </summary>
internal static class Program
{
    /// <summary>
    /// Dernier filet. Le bloc protégé de <see cref="Run"/> ne couvre que l'analyse
    /// elle-même : une exception levée avant lui — résolution de la langue, lecture
    /// des arguments, réglage machine — refermait la console sans un mot, ce qui est
    /// précisément le défaut signalé par un utilisateur en août 2026.
    /// </summary>
    private static int Main(string[] args)
    {
        try { return Run(args); }
        catch (Exception ex)
        {
            var chemin = ErrorLog.Write("cli", ex);
            try
            {
                // Sur la sortie d'erreur, y compris sous --quiet : un plantage n'est
                // pas une information qu'on a le droit de taire à un script.
                Console.Error.WriteLine(Lang.T("ERREUR : ", "ERROR: ") + ex.Message);
                if (chemin is not null)
                    Console.Error.WriteLine(Lang.T($"Détail technique : {chemin}", $"Technical detail: {chemin}"));
            }
            catch { /* plus de console : le journal reste */ }
            return 3;
        }
    }

    private static int Run(string[] args)
    {
        // Sans cela, les accents sortent en charabia dans une console cmd.exe héritée.
        try { Console.OutputEncoding = Encoding.UTF8; } catch { }

        // Avant toute écriture : la langue conditionne jusqu'au message d'erreur
        // d'un argument invalide.
        Lang.Initialize(args);

        // Réglage machine : traité avant tout le reste, parce qu'il n'analyse
        // rien. C'est l'installeur qui l'appelle (action personnalisée MSI), et
        // un administrateur peut s'en servir à la main pour rattraper un poste.
        if (SetMachineLanguage(args) is { } codeSortie) return codeSortie;

        var options = CliOptions.Parse(args);
        if (options.ShowHelp)
        {
            PrintHelp();
            return 0;
        }
        if (options.Error is not null)
        {
            Console.Error.WriteLine(Lang.T("ERREUR : ", "ERROR: ") + options.Error);
            Console.Error.WriteLine(Lang.T("Aide : FaultTracePC.Cli.exe --help", "Help: FaultTracePC.Cli.exe --help"));
            return 3;
        }

        try
        {
            if (!options.Quiet)
                Console.WriteLine(Lang.T($"FaultTracePC — analyse de {Environment.MachineName} sur {options.Days} jours…", $"FaultTracePC — analysing {Environment.MachineName} over {options.Days} days…"));

            var progress = options.Quiet
                ? null
                : new Progress<ScanProgress>(p => Console.WriteLine($"  [{p.Percent,3}%] {p.Step}"));

            var report = new ScanOrchestrator()
                .RunAsync(new ScanOptions
                {
                    Days = options.Days,
                    IncludeDrivers = !options.NoDrivers,
                    DeepDumpAnalysis = !options.NoDeep,
                }, progress)
                .GetAwaiter().GetResult();

            // --- Écriture du rapport ------------------------------------
            var outputDir = options.OutputDir ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "FaultTracePC");
            Directory.CreateDirectory(outputDir);

            // Nom incluant la machine : indispensable quand tout un parc écrit
            // dans le même partage réseau.
            var baseName = $"Diagnostic_{Sanitize(Environment.MachineName)}_{report.GeneratedAt:yyyy-MM-dd_HHmm}";
            var htmlPath = Path.Combine(outputDir, baseName + ".html");
            // Génère d'abord le script de réparation : sans lui, le rapport n'aurait
            // pas sa section « Aide à la réparation ».
            try { RepairScriptGenerator.WriteToDisk(report); } catch { /* partage réseau en lecture seule, etc. */ }
            File.WriteAllText(htmlPath, HtmlReportGenerator.Generate(report), Encoding.UTF8);

            var critical = report.Findings.Count(f => f.Severity == Severity.Critical);
            var warnings = report.Findings.Count(f => f.Severity == Severity.Warning);

            if (options.Json)
            {
                var summary = new
                {
                    machine = Environment.MachineName,
                    generatedAt = report.GeneratedAt,
                    verdict = report.Verdict,
                    critical,
                    warnings,
                    bsodCount = report.Bsods.Count,
                    faultingDrivers = report.Bsods.Where(b => b.SuspectDriver is not null)
                                                  .Select(b => b.SuspectDriver).Distinct().ToArray(),
                    alerts = report.Flight.Alerts.Count,
                    report = htmlPath,
                    findings = report.Findings.Select(f => new { level = f.Severity.ToString(), f.Title, f.Recommendation }),
                };
                var jsonPath = Path.Combine(outputDir, baseName + ".json");
                File.WriteAllText(jsonPath,
                    JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true }), Encoding.UTF8);
                if (!options.Quiet) Console.WriteLine(Lang.T($"Résumé JSON : {jsonPath}", $"JSON summary: {jsonPath}"));
            }

            if (!options.Quiet)
            {
                Console.WriteLine();
                Console.WriteLine(Lang.T("VERDICT : ", "VERDICT: ") + report.Verdict);
                Console.WriteLine(Lang.T($"  {critical} conclusion(s) critique(s), {warnings} avertissement(s), ", $"  {critical} critical conclusion(s), {warnings} warning(s), ") +
                                  Lang.T($"{report.Bsods.Count} écran(s) bleu(s), {report.Flight.Alerts.Count} alerte(s) préventive(s).", $"{report.Bsods.Count} blue screen(s), {report.Flight.Alerts.Count} preventive alert(s)."));
                foreach (var f in report.Findings.Where(f => f.Severity != Severity.Info).Take(10))
                    Console.WriteLine($"  - [{(f.Severity == Severity.Critical ? Lang.T("CRITIQUE", "CRITICAL") : Lang.T("ATTENTION", "WARNING"))}] {f.Title}");
                Console.WriteLine(Lang.T($"Rapport : {htmlPath}", $"Report: {htmlPath}"));
                if (report.CollectorErrors.Count > 0)
                    Console.WriteLine(Lang.T($"  ({report.CollectorErrors.Count} source(s) non lisible(s) — voir la section Limitations du rapport)", $"  ({report.CollectorErrors.Count} source(s) unreadable — see the Limitations section of the report)"));
            }

            if (options.Open)
            {
                try
                {
                    System.Diagnostics.Process.Start(
                        new System.Diagnostics.ProcessStartInfo(htmlPath) { UseShellExecute = true });
                }
                catch { /* pas d'interface : sans importance */ }
            }

            // Même règle que le code transmis à la console de parc : les deux ne
            // peuvent plus diverger.
            return ScanLevelInfo.Of(report).ExitCode();
        }
        catch (Exception ex)
        {
            // Le message seul ne suffit pas à diagnostiquer : la pile part au journal.
            var chemin = ErrorLog.Write("cli scan", ex);
            Console.Error.WriteLine(Lang.T("ERREUR : ", "ERROR: ") + ex.Message);
            Console.Error.WriteLine(Lang.T("Vérifie que la commande est lancée en administrateur et que le dossier de sortie est accessible.", "Check that the command is run as administrator and that the output folder is reachable."));
            if (chemin is not null)
                Console.Error.WriteLine(Lang.T($"Détail technique : {chemin}", $"Technical detail: {chemin}"));
            return 3;
        }
    }

    /// <summary>
    /// Traite « --set-machine-lang &lt;fr|en|auto&gt; ». Renvoie null si l'argument
    /// est absent — le diagnostic suit alors son cours normalement.
    ///
    /// Écrire dans ProgramData exige les droits d'administrateur, que cet outil
    /// exige déjà. Le succès est vérifié par RELECTURE plutôt que déduit de
    /// l'absence d'exception : le setter avale les échecs d'écriture pour ne
    /// jamais casser un diagnostic, ce qui ferait mentir un simple « OK ».
    /// </summary>
    private static int? SetMachineLanguage(string[] args)
    {
        int i = Array.FindIndex(args, a =>
            a.Equals("--set-machine-lang", StringComparison.OrdinalIgnoreCase) ||
            a.Equals("--langue-machine", StringComparison.OrdinalIgnoreCase));
        if (i < 0) return null;

        var valeur = i + 1 < args.Length ? Lang.NormalizeCode(args[i + 1]) : null;
        if (valeur is null)
        {
            Console.Error.WriteLine(Lang.T("ERREUR : --set-machine-lang attend fr, en ou auto.",
                                           "ERROR: --set-machine-lang expects fr, en or auto."));
            return 3;
        }

        AppLanguage? choix = valeur switch
        {
            "fr" => AppLanguage.French,
            "en" => AppLanguage.English,
            _ => null,
        };

        Lang.MachinePreference = choix;

        if (Lang.MachinePreference != choix)
        {
            Console.Error.WriteLine(Lang.T($"ERREUR : écriture impossible dans {Lang.MachinePreferencePath} — droits administrateur requis.",
                                           $"ERROR: cannot write to {Lang.MachinePreferencePath} — administrator rights required."));
            return 3;
        }

        Console.WriteLine(Lang.T($"Langue par défaut du poste : {Lang.Code(choix)} ({Lang.MachinePreferencePath})",
                                 $"Machine default language: {Lang.Code(choix)} ({Lang.MachinePreferencePath})"));
        Console.WriteLine(Lang.T("Le choix d'un utilisateur dans l'application reste prioritaire sur ce réglage.",
                                 "A user's own choice in the application still takes precedence over this setting."));
        return 0;
    }

    private static string Sanitize(string name) =>
        string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));

    private static void PrintHelp()
    {
        // L'aide est écrite en page de codes OEM sur une console française :
        // les accents survivent parce que Main tente de forcer la sortie en UTF-8 (ligne 24).
        Console.WriteLine(Lang.IsFrench   // pas-de-traduction : la version anglaise est la branche « : » plus bas.
            ? """
            FaultTracePC — diagnostic de pannes Windows en ligne de commande

            UTILISATION
              FaultTracePC.Cli.exe [options]

            OPTIONS
              --days, -d <n>     Période d'analyse en jours (défaut : 30, max 90)
              --output, -o <dir> Dossier de dépôt du rapport (local ou UNC \\serveur\partage)
              --json             Écrit aussi un résumé JSON à côté du rapport HTML
              --no-deep          Désactive l'analyse symbolique des dumps (WinDbg/CDB)
              --no-drivers       Désactive l'inventaire des pilotes (plus rapide)
              --lang <fr|en|auto> Langue du rapport et des messages (défaut : celle de
                                 la session Windows). Le choix est retenu pour les fois
                                 suivantes ; « auto » revient au comportement automatique.
              --set-machine-lang <fr|en|auto>
                                 Écrit la langue par défaut DU POSTE (tous les comptes)
                                 puis quitte, sans rien analyser. Utilisé par l'installeur
                                 et pour un déploiement par GPO. Le choix propre à un
                                 utilisateur reste prioritaire.
              --quiet, -q        N'affiche rien (usage silencieux par GPO/tâche planifiée)
              --open             Ouvre le rapport à la fin (usage interactif)
              --help, -h, /?     Affiche cette aide

            REMARQUE
              L'outil exige les droits administrateur. En usage interactif, ouvre un
              terminal DÉJÀ élevé : sinon Windows relance l'outil dans une nouvelle
              fenêtre et le code de sortie n'est pas récupérable.

            CODES DE SORTIE
              0  aucun problème significatif
              1  avertissements uniquement
              2  au moins une conclusion critique
              3  erreur d'exécution

            EXEMPLES
              FaultTracePC.Cli.exe --days 90 --open
              FaultTracePC.Cli.exe --quiet --json --output \\srv-fichiers\Diagnostics$
            """
            : """
            FaultTracePC — Windows fault diagnosis from the command line

            USAGE
              FaultTracePC.Cli.exe [options]

            OPTIONS
              --days, -d <n>     Analysis period in days (default: 30, max 90)
              --output, -o <dir> Folder the report is written to (local or UNC \\server\share)
              --json             Also writes a JSON summary next to the HTML report
              --no-deep          Turns off symbolic dump analysis (WinDbg/CDB)
              --no-drivers       Turns off the driver inventory (faster)
              --lang <fr|en|auto> Language of the report and messages (default: the one
                                 of the Windows session). The choice is remembered for
                                 next time; "auto" returns to automatic behaviour.
              --set-machine-lang <fr|en|auto>
                                 Writes the default language OF THE MACHINE (all accounts)
                                 then exits, analysing nothing. Used by the installer and
                                 for GPO deployment. A user's own choice still wins.
              --quiet, -q        Prints nothing (silent use from GPO/scheduled task)
              --open             Opens the report at the end (interactive use)
              --help, -h, /?     Shows this help

            NOTE
              The tool requires administrator rights. For interactive use, open an
              ALREADY elevated terminal: otherwise Windows restarts the tool in a new
              window and the exit code cannot be retrieved.

            EXIT CODES
              0  no significant problem
              1  warnings only
              2  at least one critical conclusion
              3  runtime error

            EXAMPLES
              FaultTracePC.Cli.exe --days 90 --open
              FaultTracePC.Cli.exe --quiet --json --output \\srv-files\Diagnostics$
            """);
    }

    private sealed class CliOptions
    {
        public int Days { get; private set; } = 30;
        public string? OutputDir { get; private set; }
        public bool Json { get; private set; }
        public bool NoDeep { get; private set; }
        public bool NoDrivers { get; private set; }
        public bool Quiet { get; private set; }
        public bool Open { get; private set; }
        public bool ShowHelp { get; private set; }

        /// <summary>Renseigné si un argument est invalide (l'appelant sort en code 3).</summary>
        public string? Error { get; private set; }

        public static CliOptions Parse(string[] args)
        {
            var o = new CliOptions();
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i].ToLowerInvariant())
                {
                    case "--days" or "-d" when i + 1 < args.Length && int.TryParse(args[i + 1], out var d):
                        o.Days = Math.Clamp(d, 1, 90); i++; break;
                    case "--days" or "-d":
                        o.Error = Lang.T("valeur invalide ou manquante pour --days (entier entre 1 et 90)", "invalid or missing value for --days (integer between 1 and 90)"); break;
                    case "--output" or "-o" when i + 1 < args.Length:
                        o.OutputDir = args[++i]; break;
                    // La langue est résolue par Lang.Initialize avant même cette
                    // analyse ; ces deux cas existent uniquement pour que la VALEUR
                    // qui suit « --lang » ne soit pas prise pour un argument à part.
                    case "--lang" or "--langue" when i + 1 < args.Length && !args[i + 1].StartsWith('-'):
                        i++; break;
                    case "--lang" or "--langue": break;
                    case "--json": o.Json = true; break;
                    case "--no-deep": o.NoDeep = true; break;
                    case "--no-drivers": o.NoDrivers = true; break;
                    case "--quiet" or "-q": o.Quiet = true; break;
                    case "--open": o.Open = true; break;
                    case "--help" or "-h" or "/?": o.ShowHelp = true; break;
                }
            }
            return o;
        }
    }
}
