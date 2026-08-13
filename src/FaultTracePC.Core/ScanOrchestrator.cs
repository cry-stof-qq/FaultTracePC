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

        if (options.DeepDumpAnalysis && report.Dumps.Count > 0)
        {
            Step(progress, "Analyse profonde des dumps (WinDbg/CDB, symboles Microsoft)…", 78);
            new CdbAnalyzer(errors).AnalyzeAll(report.Dumps, options.MaxDeepDumps, ct);
        }
        ct.ThrowIfCancellationRequested();

        Step(progress, "Corrélation et diagnostic…", 92);
        new RulesEngine().Analyze(report);

        Step(progress, "Terminé.", 100);
        return report;
    }

    private static void Step(IProgress<ScanProgress>? p, string label, int pct) =>
        p?.Report(new ScanProgress(label, pct));
}
