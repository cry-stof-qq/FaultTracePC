using System.Diagnostics;
using System.Runtime.InteropServices;

namespace FaultTracePC.Core.Collectors;

/// <summary>
/// Instantané des processus en cours : RAM privée, working set, % CPU et débit
/// disque mesurés sur une fenêtre d'environ une seconde (deux relevés).
/// Objectif : voir immédiatement qui consomme quoi au moment du scan — utile
/// pour les pannes de type « la mémoire a été épuisée par X ».
/// </summary>
public sealed class ProcessCollector
{
    private readonly List<string> _errors;

    public ProcessCollector(List<string> errors) => _errors = errors;

    public List<ProcessInfo> Collect()
    {
        var result = new List<ProcessInfo>();
        try
        {
            // Premier relevé
            var first = Sample();
            Thread.Sleep(1100);
            // Second relevé + calcul des deltas
            var elapsedSec = 1.1;
            int cores = Environment.ProcessorCount;

            foreach (var p in Process.GetProcesses())
            {
                try
                {
                    var info = new ProcessInfo
                    {
                        Pid = p.Id,
                        Name = p.ProcessName,
                        PrivateBytes = p.PrivateMemorySize64,
                        WorkingSetBytes = p.WorkingSet64,
                    };

                    if (first.TryGetValue(p.Id, out var f))
                    {
                        try
                        {
                            var cpuDelta = (p.TotalProcessorTime - f.Cpu).TotalSeconds;
                            info.CpuPercent = Math.Round(Math.Max(0, cpuDelta / elapsedSec / cores * 100), 1);
                        }
                        catch { /* accès refusé sur certains processus système */ }

                        if (f.IoBytes >= 0 && TryGetIoBytes(p, out var ioNow) && ioNow >= f.IoBytes)
                            info.IoBytesPerSec = Math.Round((ioNow - f.IoBytes) / elapsedSec);
                    }

                    result.Add(info);
                }
                catch { /* processus terminé entre-temps */ }
                finally { p.Dispose(); }
            }
        }
        catch (Exception ex)
        {
            _errors.Add(Lang.T($"Processus en cours : {ex.Message}", $"Running processes: {ex.Message}"));
        }

        return result.OrderByDescending(p => p.PrivateBytes).ToList();
    }

    private static Dictionary<int, (TimeSpan Cpu, long IoBytes)> Sample()
    {
        var dict = new Dictionary<int, (TimeSpan, long)>();
        foreach (var p in Process.GetProcesses())
        {
            try
            {
                TimeSpan cpu = TimeSpan.Zero;
                try { cpu = p.TotalProcessorTime; } catch { }
                long io = TryGetIoBytes(p, out var b) ? b : -1;
                dict[p.Id] = (cpu, io);
            }
            catch { }
            finally { p.Dispose(); }
        }
        return dict;
    }

    // ------------------------------------------------------------------
    // Compteurs d'E/S par processus (GetProcessIoCounters, kernel32)
    // ------------------------------------------------------------------

    [StructLayout(LayoutKind.Sequential)]
    private struct IO_COUNTERS
    {
        public ulong ReadOperationCount, WriteOperationCount, OtherOperationCount;
        public ulong ReadTransferCount, WriteTransferCount, OtherTransferCount;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetProcessIoCounters(IntPtr hProcess, out IO_COUNTERS counters);

    private static bool TryGetIoBytes(Process p, out long bytes)
    {
        bytes = 0;
        try
        {
            if (!GetProcessIoCounters(p.Handle, out var c)) return false;
            bytes = (long)(c.ReadTransferCount + c.WriteTransferCount);
            return true;
        }
        catch { return false; } // accès refusé (processus protégés)
    }
}
