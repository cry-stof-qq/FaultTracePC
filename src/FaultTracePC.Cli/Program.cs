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

        // Configuration du mode parc : n'analyse rien non plus, et se déploie par
        // GPO au même titre que le réglage de langue.
        if (GenerateMasterSecret(args) is { } codeSecret) return codeSecret;
        if (ConfigureRemote(args) is { } codeParc) return codeParc;

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

    /// <summary>
    /// « --generate-master-secret » : produit un secret maître de parc et s'arrête.
    ///
    /// Il se range dans un gestionnaire de mots de passe. C'est le SEUL secret à
    /// conserver : tous les jetons des postes s'en déduisent, et une console
    /// reconstruite depuis zéro retrouve l'accès au parc entier avec lui seul.
    /// </summary>
    private static int? GenerateMasterSecret(string[] args)
    {
        if (!args.Any(a => a.Equals("--generate-master-secret", StringComparison.OrdinalIgnoreCase)))
            return null;

        Console.WriteLine(RemoteConfig.GenerateMasterSecret());
        Console.Error.WriteLine(Lang.T(
            "Conserve ce secret dans un gestionnaire de mots de passe. Il ne peut pas être retrouvé, et le perdre oblige à reconfigurer tous les postes.",
            "Keep this secret in a password manager. It cannot be recovered, and losing it means reconfiguring every machine."));
        return 0;
    }

    /// <summary>
    /// « --configure-remote --master-secret &lt;secret|-&gt; [--port n] » : prépare un
    /// poste pour le mode parc, sans interface, donc déployable par GPO.
    ///
    /// LE POSTE NE CONNAÎT JAMAIS LE SECRET MAÎTRE. Il reçoit sa valeur le temps
    /// d'une commande, en déduit SON jeton, et n'écrit que celui-là. Un secret
    /// laissé sur chaque poste offrirait le parc entier à qui ouvre un seul poste.
    ///
    /// Le secret peut être lu sur l'ENTRÉE STANDARD (valeur « - ») : passé en
    /// argument, il est visible dans la liste des processus le temps de
    /// l'exécution, ce qui est acceptable pour une commande manuelle et
    /// déconseillé dans un script partagé.
    /// </summary>
    private static int? ConfigureRemote(string[] args)
    {
        if (!args.Any(a => a.Equals("--configure-remote", StringComparison.OrdinalIgnoreCase)))
            return null;

        var secret = Argument(args, "--master-secret");
        if (secret == "-") secret = Console.In.ReadLine();

        if (string.IsNullOrWhiteSpace(secret))
        {
            Console.Error.WriteLine(Lang.T("ERREUR : --configure-remote attend --master-secret <valeur> ou --master-secret - pour le lire sur l'entrée standard.",
                                           "ERROR: --configure-remote expects --master-secret <value>, or --master-secret - to read it from standard input."));
            return 3;
        }

        var port = int.TryParse(Argument(args, "--port"), out var p) ? p : 58620;
        if (port is < 1024 or > 65535)
        {
            Console.Error.WriteLine(Lang.T("ERREUR : --port attend un nombre entre 1024 et 65535.",
                                           "ERROR: --port expects a number between 1024 and 65535."));
            return 3;
        }

        string jeton;
        try
        {
            jeton = RemoteConfig.DeriveToken(secret, Environment.MachineName);
        }
        catch (ArgumentException)
        {
            Console.Error.WriteLine(Lang.T($"ERREUR : secret maître trop faible — {RemoteConfig.MasterSecretMinLength} caractères au minimum. Utilise --generate-master-secret.",
                                           $"ERROR: master secret too weak — {RemoteConfig.MasterSecretMinLength} characters minimum. Use --generate-master-secret."));
            return 3;
        }

        try
        {
            new RemoteConfig { Mode = "Client", Port = port, Token = jeton }.Save();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(Lang.T($"ERREUR : écriture impossible dans {RemoteConfig.ConfigPath} — {ex.Message}",
                                           $"ERROR: cannot write to {RemoteConfig.ConfigPath} — {ex.Message}"));
            return 3;
        }

        // Relecture plutôt que confiance : Save() peut réussir sur un disque plein
        // sans que le contenu soit celui qu'on croit.
        var relu = RemoteConfig.Load();
        if (relu.Mode != "Client" || relu.Token != jeton || relu.Port != port)
        {
            Console.Error.WriteLine(Lang.T($"ERREUR : le fichier {RemoteConfig.ConfigPath} ne contient pas ce qui vient d'être écrit.",
                                           $"ERROR: the file {RemoteConfig.ConfigPath} does not contain what was just written."));
            return 3;
        }

        // Ni le secret ni le jeton ne sont affichés : le jeton se recalcule côté
        // console, et l'écrire ici le déposerait dans les journaux de déploiement.
        Console.WriteLine(Lang.T($"Mode parc activé sur {Environment.MachineName}, port {port}.",
                                 $"Fleet mode enabled on {Environment.MachineName}, port {port}."));
        Console.WriteLine(Lang.T("La console retrouvera ce poste avec le secret maître et son nom de machine — rien à recopier.",
                                 "The console will find this machine again from the master secret and its machine name — nothing to copy."));
        return 0;
    }

    /// <summary>Valeur suivant une option, ou null si l'option est absente ou terminale.</summary>
    private static string? Argument(string[] args, string option)
    {
        var i = Array.FindIndex(args, a => a.Equals(option, StringComparison.OrdinalIgnoreCase));
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
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

              --generate-master-secret
                                 Produit un secret maître de parc, puis quitte. À ranger
                                 dans un gestionnaire de mots de passe : c'est le seul
                                 secret à conserver, tous les jetons s'en déduisent.
              --configure-remote --master-secret <valeur|->  [--port <n>]
                                 Prépare ce poste pour le mode parc, puis quitte. Le jeton
                                 est DÉDUIT du secret et du nom de machine : rien à
                                 recopier vers la console. Le secret n'est pas conservé
                                 sur le poste. « - » le lit sur l'entrée standard, ce qui
                                 évite de l'exposer dans la liste des processus.
                                 Port par défaut : 58620.
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

              --generate-master-secret
                                 Produces a fleet master secret, then exits. Keep it in a
                                 password manager: it is the only secret to preserve, every
                                 machine token is derived from it.
              --configure-remote --master-secret <value|->  [--port <n>]
                                 Prepares this machine for fleet mode, then exits. The token
                                 is DERIVED from the secret and the machine name: nothing to
                                 copy over to the console. The secret is not kept on the
                                 machine. "-" reads it from standard input, which avoids
                                 exposing it in the process list. Default port: 58620.
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

    /// <summary>
    /// Interne, et non privée : le projet de tests exerce cette analyse
    /// d'arguments. Une ligne de commande mal comprise se déploie par GPO sur
    /// tout un parc — c'est le dernier endroit où l'on peut se permettre de
    /// deviner.
    /// </summary>
    internal sealed class CliOptions
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
                        o.Error ??= Lang.T("valeur invalide ou manquante pour --days (entier entre 1 et 90)", "invalid or missing value for --days (integer between 1 and 90)"); break;
                    case "--output" or "-o" when i + 1 < args.Length:
                        o.OutputDir = args[++i]; break;
                    case "--output" or "-o":
                        o.Error ??= Lang.T("valeur manquante pour --output (un dossier)", "missing value for --output (a folder)"); break;
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

                    // DÉFAUT CONSTATÉ LE 29/08/2026 : sans ce cas, une option
                    // inconnue était ignorée EN SILENCE et le programme lançait
                    // une analyse complète de trente jours — en rendant 0, donc
                    // en annonçant un succès. Une faute de frappe dans un script
                    // GPO (« --configure-remot ») aurait analysé tout un parc au
                    // lieu de le configurer, sans que rien ne le signale.
                    default:
                        o.Error ??= Lang.T($"option inconnue : {args[i]}", $"unknown option: {args[i]}");
                        break;
                }
            }
            return o;
        }
    }
}
