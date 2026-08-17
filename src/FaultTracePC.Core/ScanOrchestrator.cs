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

        Step(progress, Lang.T("Collecte des informations système (WMI)…", "Collecting system information (WMI)…"), 5);
        report.System = new SystemInfoCollector(errors).Collect(options.IncludeDrivers);
        ct.ThrowIfCancellationRequested();

        Step(progress, Lang.T("Relevé des processus en cours (RAM, CPU, disque)…", "Reading the running processes (RAM, CPU, disk)…"), 25);
        report.Processes = new ProcessCollector(errors).Collect();
        ct.ThrowIfCancellationRequested();

        Step(progress, Lang.T("Lecture du journal d'événements Windows…", "Reading the Windows event log…"), 40);
        report.Events = new EventLogCollector(errors).Collect(options.Days);
        ct.ThrowIfCancellationRequested();

        Step(progress, Lang.T("Lecture du Moniteur de fiabilité…", "Reading the Reliability Monitor…"), 60);
        report.ReliabilityRecords = new ReliabilityCollector(errors).Collect(options.Days);
        ct.ThrowIfCancellationRequested();

        Step(progress, Lang.T("Analyse des fichiers dump (Minidump, MEMORY.DMP)…", "Analysing the dump files (Minidump, MEMORY.DMP)…"), 70);
        report.Dumps = new DumpCollector(errors).Collect();
        ct.ThrowIfCancellationRequested();

        bool hasKernelDumps = report.Dumps.Any(d => d.Kind is DumpKind.KernelMinidump or DumpKind.FullMemoryDump);
        if (options.DeepDumpAnalysis && report.Dumps.Count > 0)
        {
            Step(progress, Lang.T("Analyse profonde des dumps (WinDbg/CDB, symboles Microsoft)…", "Deep analysis of the dumps (WinDbg/CDB, Microsoft symbols)…"), 78);
            new CdbAnalyzer(errors).AnalyzeAll(report.Dumps, options.MaxDeepDumps, ct);
        }
        else if (!options.DeepDumpAnalysis && hasKernelDumps)
        {
            errors.Add(Lang.T(
                "Analyse profonde des dumps DÉSACTIVÉE (case décochée) : le pilote fautif des BSOD "
                + "ne sera pas identifié. Recocher « Analyse profonde (WinDbg) » pour un diagnostic complet.",
                "Deep dump analysis DISABLED (box unticked): the driver behind the BSODs will not be "
                + "identified. Tick “Deep analysis (WinDbg)” again for a complete diagnosis."));
        }
        ct.ThrowIfCancellationRequested();

        Step(progress, Lang.T("Lecture de la boîte noire (surveillance temps réel)…", "Reading the flight recorder (real-time monitoring)…"), 86);
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
            report.Flight.Alerts = AlertLogReader.Read(options.Days, errors);
        }
        catch (Exception ex) { errors.Add(Lang.T($"Boîte noire : {ex.Message}", $"Flight recorder: {ex.Message}")); }

        Step(progress, Lang.T("Corrélation et diagnostic…", "Cross-referencing and diagnosis…"), 90);
        new RulesEngine().Analyze(report);

        Step(progress, Lang.T("Comparaison avec le scan précédent…", "Comparing with the previous scan…"), 96);
        try
        {
            report.Comparison = Report.ScanHistory.CompareWithPrevious(report, errors);
            Report.ScanHistory.Save(report, errors);
        }
        catch (Exception ex) { errors.Add(Lang.T($"Historique des scans : {ex.Message}", $"Scan history: {ex.Message}")); }

        Step(progress, Lang.T("Terminé.", "Done."), 100);
        return report;
    }

    private static void Step(IProgress<ScanProgress>? p, string label, int pct) =>
        p?.Report(new ScanProgress(label, pct));
}
