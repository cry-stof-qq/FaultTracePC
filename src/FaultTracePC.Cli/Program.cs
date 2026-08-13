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
    private static int Main(string[] args)
    {
        // Sans cela, les accents sortent en charabia dans une console cmd.exe héritée.
        try { Console.OutputEncoding = Encoding.UTF8; } catch { }

        var options = CliOptions.Parse(args);
        if (options.ShowHelp)
        {
            PrintHelp();
            return 0;
        }
        if (options.Error is not null)
        {
            Console.Error.WriteLine("ERREUR : " + options.Error);
            Console.Error.WriteLine("Aide : FaultTracePC.Cli.exe --help");
            return 3;
        }

        try
        {
            if (!options.Quiet)
                Console.WriteLine($"FaultTracePC — analyse de {Environment.MachineName} sur {options.Days} jours…");

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
                if (!options.Quiet) Console.WriteLine($"Résumé JSON : {jsonPath}");
            }

            if (!options.Quiet)
            {
                Console.WriteLine();
                Console.WriteLine("VERDICT : " + report.Verdict);
                Console.WriteLine($"  {critical} conclusion(s) critique(s), {warnings} avertissement(s), " +
                                  $"{report.Bsods.Count} écran(s) bleu(s), {report.Flight.Alerts.Count} alerte(s) préventive(s).");
                foreach (var f in report.Findings.Where(f => f.Severity != Severity.Info).Take(10))
                    Console.WriteLine($"  - [{(f.Severity == Severity.Critical ? "CRITIQUE" : "ATTENTION")}] {f.Title}");
                Console.WriteLine($"Rapport : {htmlPath}");
                if (report.CollectorErrors.Count > 0)
                    Console.WriteLine($"  ({report.CollectorErrors.Count} source(s) non lisible(s) — voir la section Limitations du rapport)");
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

            return critical > 0 ? 2 : warnings > 0 ? 1 : 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("ERREUR : " + ex.Message);
            Console.Error.WriteLine("Vérifie que la commande est lancée en administrateur et que le dossier de sortie est accessible.");
            return 3;
        }
    }

    private static string Sanitize(string name) =>
        string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));

    private static void PrintHelp()
    {
        Console.WriteLine("""
            FaultTracePC — diagnostic de pannes Windows en ligne de commande

            UTILISATION
              FaultTracePC.Cli.exe [options]

            OPTIONS
              --days, -d <n>     Période d'analyse en jours (défaut : 30, max 90)
              --output, -o <dir> Dossier de dépôt du rapport (local ou UNC \\serveur\partage)
              --json             Écrit aussi un résumé JSON à côté du rapport HTML
              --no-deep          Désactive l'analyse symbolique des dumps (WinDbg/CDB)
              --no-drivers       Désactive l'inventaire des pilotes (plus rapide)
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
                        o.Error = "valeur invalide ou manquante pour --days (entier entre 1 et 90)"; break;
                    case "--output" or "-o" when i + 1 < args.Length:
                        o.OutputDir = args[++i]; break;
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
