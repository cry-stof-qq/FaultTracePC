using LibreHardwareMonitor.Hardware;

namespace FaultTracePC.Monitor;

/// <summary>
/// Lecture des capteurs matériels via LibreHardwareMonitor : charge CPU,
/// température CPU (max des sondes), température et charge GPU, et depuis la
/// 1.11.0 (point 120) fréquence et consommation du processeur.
/// Toutes les lectures sont best-effort : un capteur absent renvoie null.
/// </summary>
public sealed class SensorReader : IDisposable
{
    private readonly Computer? _computer;

    /// <summary>
    /// Point 86 — ce que la bibliothèque de capteurs a vu à l'ouverture : écrit dans la
    /// ligne de début de session du journal, montré par le rapport quand la charge
    /// processeur manque.
    /// </summary>
    public string Diagnostic { get; }

    public SensorReader()
    {
        string? erreur = null;
        try
        {
            _computer = new Computer
            {
                IsCpuEnabled = true,
                IsGpuEnabled = true,
                IsMemoryEnabled = false,     // la mémoire est lue via l'API Windows (plus fiable)
                IsMotherboardEnabled = false, // évite le pilote SIO : on reste léger
                IsStorageEnabled = false,
            };
            _computer.Open();
        }
        catch (Exception ex)
        {
            _computer = null; // capteurs indisponibles : le service continue sans températures
            erreur = ex.Message;
        }
        Diagnostic = Decrire(erreur);
    }

    private string Decrire(string? erreur)
    {
        var processeurs = new List<(string, IReadOnlyList<string>, IReadOnlyList<string>, IReadOnlyList<string>)>();
        var autres = new List<string>();
        if (_computer is not null)
        {
            try
            {
                foreach (var hw in _computer.Hardware)
                {
                    try { hw.Update(); } catch { }
                    if (hw.HardwareType == HardwareType.Cpu)
                        processeurs.Add((hw.Name,
                            hw.Sensors.Where(s => s.SensorType == SensorType.Load).Select(s => s.Name).ToList(),
                            // Point 120 : les noms des sondes d'horloge et de puissance, pour
                            // savoir sur de vraies machines comment la bibliothèque les nomme.
                            hw.Sensors.Where(s => s.SensorType == SensorType.Clock).Select(s => s.Name).ToList(),
                            hw.Sensors.Where(s => s.SensorType == SensorType.Power).Select(s => s.Name).ToList()));
                    else
                        autres.Add($"{hw.HardwareType} ({hw.Name})");
                }
            }
            catch (Exception ex) { erreur ??= ex.Message; }
        }
        try { return FaultTracePC.Core.Analysis.DiagnosticCapteurs.EncoderComplet(erreur, processeurs, autres); }
        catch { return ""; }
    }

    public (double? CpuLoad, double? CpuTemp, double? GpuTemp, double? GpuLoad, double? CpuClockMHz, double? CpuPowerW) Read()
    {
        if (_computer is null) return (null, null, null, null, null, null);

        double? cpuLoad = null, cpuTemp = null, gpuTemp = null, gpuLoad = null, cpuClock = null, cpuPower = null;
        try
        {
            foreach (var hw in _computer.Hardware)
            {
                hw.Update();
                switch (hw.HardwareType)
                {
                    case HardwareType.Cpu:
                        // Point 120 : le choix des sondes est fait dans Core (testé).
                        cpuClock = FaultTracePC.Core.Analysis.CapteursProcesseur.Frequence(
                            hw.Sensors.Where(s => s.SensorType == SensorType.Clock).Select(s => (s.Name, (double?)s.Value)));
                        cpuPower = FaultTracePC.Core.Analysis.CapteursProcesseur.Puissance(
                            hw.Sensors.Where(s => s.SensorType == SensorType.Power).Select(s => (s.Name, (double?)s.Value)));
                        foreach (var s in hw.Sensors)
                        {
                            if (s.Value is not { } v || float.IsNaN((float)v)) continue;
                            if (s.SensorType == SensorType.Load && s.Name.Contains("Total", StringComparison.OrdinalIgnoreCase))
                                cpuLoad = Math.Round(v, 1);
                            else if (s.SensorType == SensorType.Temperature)
                                cpuTemp = Math.Max(cpuTemp ?? 0, Math.Round(v, 1));
                        }
                        break;

                    case HardwareType.GpuNvidia:
                    case HardwareType.GpuAmd:
                    case HardwareType.GpuIntel:
                        foreach (var s in hw.Sensors)
                        {
                            if (s.Value is not { } v || float.IsNaN((float)v)) continue;
                            if (s.SensorType == SensorType.Temperature)
                                gpuTemp = Math.Max(gpuTemp ?? 0, Math.Round(v, 1));
                            else if (s.SensorType == SensorType.Load && s.Name.Contains("Core", StringComparison.OrdinalIgnoreCase))
                                gpuLoad = Math.Round(v, 1);
                        }
                        break;
                }
            }
        }
        catch { /* une lecture ratée n'arrête pas la surveillance */ }

        // Filtre de vraisemblance : certaines sondes renvoient 0 ou 255 quand elles décrochent.
        if (cpuTemp is <= 1 or >= 120) cpuTemp = null;
        if (gpuTemp is <= 1 or >= 120) gpuTemp = null;
        return (cpuLoad, cpuTemp, gpuTemp, gpuLoad, cpuClock, cpuPower);
    }

    public void Dispose()
    {
        try { _computer?.Close(); } catch { }
    }
}
