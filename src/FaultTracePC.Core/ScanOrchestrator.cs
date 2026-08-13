using FaultTracePC.Core.Analysis;
using FaultTracePC.Core.Collectors;

namespace FaultTracePC.Core;

/// <summary>
/// Orchestration du scan post-mortem (mode 1) : collecte → corrélation → rapport.
/// Chaque étape est isolée ; les erreurs partielles sont listées dans le rapport
/// plutôt que de faire échouer l'analyse.
/// </summary>
public sealed class ScanOrchestrator
{
    public async Task<DiagnosticReport> RunAsync(ScanOptions options, IProgress<ScanProgress>? progress = null, CancellationToken ct = default)
    {
        return await Task.Run(() => Run(options, progress, ct), ct);
    }

    private static DiagnosticReport Run(ScanOptions options, IProgress<ScanProgress>? progress, CancellationToken ct)
    {
        var report = new DiagnosticReport { ScanPeriodDays = options.Days };
        var errors = report.CollectorErrors;

        Step(progress, "Collecte des informations système (WMI)…", 5);
        report.System = new SystemInfoCollector(errors).Collect(options.IncludeDrivers);
        ct.ThrowIfCancellationRequested();

        Step(progress, "Relevé des processus en cours (RAM, CPU, disque)…", 25);
        report.Processes = new ProcessCollector(errors).Collect();
        ct.ThrowIfCancellationRequested();

        Step(progress, "Lecture du journal d'événements Windows…", 40);
        report.Events = new EventLogCollector(errors).Collect(options.Days);
        ct.ThrowIfCancellationRequested();

        Step(progress, "Lecture du Moniteur de fiabilité…", 60);
        report.ReliabilityRecords = new ReliabilityCollector(errors).Collect(options.Days);
        ct.ThrowIfCancellationRequested();

        Step(progress, "Analyse des fichiers dump (Minidump, MEMORY.DMP)…", 70);
        report.Dumps = new DumpCollector(errors).Collect();
        ct.ThrowIfCancellationRequested();

        bool hasKernelDumps = report.Dumps.Any(d => d.Kind is DumpKind.KernelMinidump or DumpKind.FullMemoryDump);
        if (options.DeepDumpAnalysis && report.Dumps.Count > 0)
        {
            Step(progress, "Analyse profonde des dumps (WinDbg/CDB, symboles Microsoft)…", 78);
            new CdbAnalyzer(errors).AnalyzeAll(report.Dumps, options.MaxDeepDumps, ct);
        }
        else if (!options.DeepDumpAnalysis && hasKernelDumps)
        {
            errors.Add("Analyse profonde des dumps DÉSACTIVÉE (case décochée) : le pilote fautif des BSOD "
                     + "ne sera pas identifié. Recocher « Analyse profonde (WinDbg) » pour un diagnostic complet.");
        }
        ct.ThrowIfCancellationRequested();

        Step(progress, "Lecture de la boîte noire (surveillance temps réel)…", 86);
        try
        {
            var crashTimes = new List<DateTime>();
            crashTimes.AddRange(report.Dumps
                .Where(d => d.Kind is DumpKind.KernelMinidump or DumpKind.FullMemoryDump)
                .Select(d => d.CrashTimeFromHeader ?? d.LastWriteTime));
            crashTimes.AddRange(report.Events
                .Where(e => e.Category is EventCategory.PowerLoss or EventCategory.UnexpectedShutdown)
                .Select(e => e.TimeLocal));
            report.Flight = new FlightJournalCollector(errors).Collect(crashTimes, options.Days);
        }
        catch (Exception ex) { errors.Add($"Boîte noire : {ex.Message}"); }

        Step(progress, "Corrélation et diagnostic…", 90);
        new RulesEngine().Analyze(report);

        Step(progress, "Comparaison avec le scan précédent…", 96);
        try
        {
            report.Comparison = Report.ScanHistory.CompareWithPrevious(report, errors);
            Report.ScanHistory.Save(report, errors);
        }
        catch (Exception ex) { errors.Add($"Historique des scans : {ex.Message}"); }

        Step(progress, "Terminé.", 100);
        return report;
    }

    private static void Step(IProgress<ScanProgress>? p, string label, int pct) =>
        p?.Report(new ScanProgress(label, pct));
}
