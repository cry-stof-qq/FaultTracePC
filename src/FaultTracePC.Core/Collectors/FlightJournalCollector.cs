using System.Text.Json;

namespace FaultTracePC.Core.Collectors;

/// <summary>
/// Lit le journal de la boîte noire (écrit par le service FaultTracePC.Monitor)
/// et extrait, pour chaque instant de crash candidat, les derniers échantillons
/// enregistrés juste avant — température, mémoire, processus. Lecture en flux
/// avec fenêtre glissante : léger même sur un journal de plusieurs jours.
/// </summary>
public sealed class FlightJournalCollector
{
    private static string FlightDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                     "FaultTracePC", "Flight");

    private const int WindowSize = 8;                 // échantillons conservés avant chaque crash
    private static readonly TimeSpan MaxGap = TimeSpan.FromMinutes(5); // pertinence du contexte

    private readonly List<string> _errors;

    public FlightJournalCollector(List<string> errors) => _errors = errors;

    /// <summary>Deux instants plus proches que cela désignent le même incident.</summary>
    private static readonly TimeSpan MemeIncident = TimeSpan.FromMinutes(3);

    /// <summary>
    /// Un seul plantage arrive ici plusieurs fois : l'en-tête du vidage, l'événement
    /// Kernel-Power et la date du fichier ne donnent pas la même seconde. Sans
    /// regroupement, le rapport affichait deux ou trois fois le MÊME tableau
    /// d'échantillons sous des titres à une minute d'écart — on croyait à des incidents
    /// distincts là où il n'y en avait qu'un.
    ///
    /// On garde l'instant le PLUS TARDIF de chaque groupe : c'est celui qui est le plus
    /// proche de l'arrêt réel, donc celui dont la fenêtre d'échantillons est la plus complète.
    /// </summary>
    internal static List<DateTime> Regrouper(IReadOnlyCollection<DateTime> instants)
    {
        var tries = instants.Distinct().OrderBy(t => t).ToList();
        var groupes = new List<DateTime>();
        foreach (var t in tries)
        {
            if (groupes.Count > 0 && t - groupes[^1] <= MemeIncident) groupes[^1] = t;
            else groupes.Add(t);
        }
        return groupes;
    }

    public FlightInfo Collect(IReadOnlyCollection<DateTime> crashTimes, int days)
    {
        var info = new FlightInfo();
        try
        {
            if (!Directory.Exists(FlightDir)) return info;
            var files = Directory.EnumerateFiles(FlightDir, "flight_*.jsonl")
                .Where(f => File.GetLastWriteTime(f) >= DateTime.Now.AddDays(-days))
                .OrderBy(f => f)
                .ToList();
            if (files.Count == 0) return info;

            info.JournalFound = true;
            info.DaysCovered = files.Count;

            // Bilan thermique : on cumule au fil de la lecture, sans second passage
            // sur des journaux qui peuvent peser plusieurs dizaines de mégaoctets.
            var thresholds = AlertSettings.Load();
            var cpuThermal = new Analysis.ThermalHistory(Analysis.ThermalHistory.CapteurCpu, thresholds.CpuTempWarn, thresholds.CpuTempCrit);
            var gpuThermal = new Analysis.ThermalHistory(Analysis.ThermalHistory.CapteurGpu, thresholds.GpuTempWarn, thresholds.GpuTempCrit);

            var targets = Regrouper(crashTimes);
            var window = new Queue<FlightSample>(WindowSize + 1);
            var contexts = new Dictionary<DateTime, FlightCrashContext>();

            foreach (var file in files)
            {
                using var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new StreamReader(fs);
                while (reader.ReadLine() is { } line)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    FlightSample? entry;
                    try { entry = JsonSerializer.Deserialize<FlightSample>(line); }
                    catch { continue; }
                    if (entry is null) continue;

                    switch (entry.Kind)
                    {
                        case "b" when entry.PreviousEndedAbruptly == true:
                            info.AbruptSessionEnds++;
                            break;
                        case "s":
                            info.LastSampleTime = entry.Time;
                            cpuThermal.Add(entry.Time, entry.CpuTemp);
                            gpuThermal.Add(entry.Time, entry.GpuTemp);
                            window.Enqueue(entry);
                            while (window.Count > WindowSize) window.Dequeue();
                            break;
                    }

                    // Un crash candidat vient-il d'être dépassé ? On fige la fenêtre.
                    while (targets.Count > 0 && entry.Time >= targets[0])
                    {
                        var t = targets[0];
                        targets.RemoveAt(0);
                        var samples = window.Where(s => t - s.Time <= MaxGap && s.Time <= t).ToList();
                        if (samples.Count > 0)
                            contexts[t] = new FlightCrashContext { CrashTime = t, Samples = samples };
                    }
                }
            }

            // Crashs postérieurs au dernier échantillon (fin de journal) : fenêtre restante.
            foreach (var t in targets)
            {
                var samples = window.Where(s => t - s.Time <= MaxGap && s.Time <= t).ToList();
                if (samples.Count > 0)
                    contexts[t] = new FlightCrashContext { CrashTime = t, Samples = samples };
            }

            info.Contexts = contexts.Values.OrderByDescending(c => c.CrashTime).ToList();
            info.Active = info.LastSampleTime is { } last && DateTime.Now - last < TimeSpan.FromMinutes(2);

            // Un capteur muet ne produit rien : on n'affiche pas une ligne vide qui
            // laisserait croire que la machine reste froide alors qu'on n'a rien mesuré.
            foreach (var stats in new[] { cpuThermal.Build(), gpuThermal.Build() })
                if (stats.HasData) info.Thermal.Add(stats);
        }
        catch (Exception ex)
        {
            _errors.Add(Lang.T($"Boîte noire (lecture du journal) : {ex.Message}", $"Flight recorder (reading the log): {ex.Message}"));
        }
        return info;
    }
}
