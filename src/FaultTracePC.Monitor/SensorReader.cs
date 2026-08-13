using LibreHardwareMonitor.Hardware;

namespace FaultTracePC.Monitor;

/// <summary>
/// Lecture des capteurs matériels via LibreHardwareMonitor : charge CPU,
/// température CPU (max des sondes), température et charge GPU.
/// Toutes les lectures sont best-effort : un capteur absent renvoie null.
/// </summary>
public sealed class SensorReader : IDisposable
{
    private readonly Computer? _computer;

    public SensorReader()
    {
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
        catch
        {
            _computer = null; // capteurs indisponibles : le service continue sans températures
        }
    }

    public (double? CpuLoad, double? CpuTemp, double? GpuTemp, double? GpuLoad) Read()
    {
        if (_computer is null) return (null, null, null, null);

        double? cpuLoad = null, cpuTemp = null, gpuTemp = null, gpuLoad = null;
        try
        {
            foreach (var hw in _computer.Hardware)
            {
                hw.Update();
                switch (hw.HardwareType)
                {
                    case HardwareType.Cpu:
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
        return (cpuLoad, cpuTemp, gpuTemp, gpuLoad);
    }

    public void Dispose()
    {
        try { _computer?.Close(); } catch { }
    }
}
