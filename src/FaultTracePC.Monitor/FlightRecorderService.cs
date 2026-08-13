using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using FaultTracePC.Core;
using Microsoft.Extensions.Hosting;

namespace FaultTracePC.Monitor;

/// <summary>
/// L'enregistreur de vol : un échantillon toutes les 10 s (charge CPU, températures,
/// mémoire, top processus toutes les 30 s) + les événements critiques en direct,
/// écrits dans C:\ProgramData\FaultTracePC\Flight\flight_AAAAMMJJ.jsonl.
///
/// Chaque ligne est écrite en WriteThrough + Flush(true) : les données atteignent
/// physiquement le disque immédiatement, donc les dernières secondes avant un
/// crash/coupure sont toujours récupérables. Rotation : 14 jours conservés.
/// </summary>
public sealed class FlightRecorderService : BackgroundService
{
    public static string FlightDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                     "FaultTracePC", "Flight");

    private static readonly TimeSpan SampleInterval = TimeSpan.FromSeconds(10);
    private const int TopProcessEverySamples = 3;   // top processus toutes les 30 s
    private const int RetentionDays = 14;

    private readonly object _writeLock = new();
    private FileStream? _stream;
    private string _currentFile = "";
    private readonly List<EventLogWatcher> _watchers = new();
    private AlertEngine? _alerts;

    /// <summary>Trace l'alerte dans le journal de vol (pour la retrouver dans le contexte d'un crash).</summary>
    private void WriteAlertMarker(PreventiveAlert alert) =>
        WriteLine(new FlightSample
        {
            Time = alert.Time,
            Kind = "e",
            EventCategory = $"ALERTE#{alert.Level}",
            EventMessage = alert.Title,
        });

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Directory.CreateDirectory(FlightDir);
        CleanupOldFiles();

        bool abrupt = PreviousSessionEndedAbruptly();
        WriteLine(new FlightSample { Time = DateTime.Now, Kind = "b", PreviousEndedAbruptly = abrupt });

        using var sensors = new SensorReader();
        var alertSettings = AlertSettings.Load();
        // Écrit alerts.json au premier démarrage : les seuils deviennent visibles et modifiables.
        try { if (!File.Exists(AlertSettings.SettingsPath)) alertSettings.Save(); } catch { }
        _alerts = new AlertEngine(alertSettings);
        StartEventWatchers();

        int counter = 0;
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var (cpuLoad, cpuTemp, gpuTemp, gpuLoad) = sensors.Read();
                    var (memPct, commitPct) = ReadMemory();

                    var sample = new FlightSample
                    {
                        Time = DateTime.Now,
                        Kind = "s",
                        CpuLoad = cpuLoad,
                        CpuTemp = cpuTemp,
                        GpuTemp = gpuTemp,
                        GpuLoad = gpuLoad,
                        MemPct = memPct,
                        CommitPct = commitPct,
                        TopProcesses = (++counter % TopProcessEverySamples == 0) ? TopProcesses() : null,
                    };
                    WriteLine(sample);

                    // Alertes préventives : seuils sur l'échantillon + contrôle périodique des disques.
                    foreach (var alert in _alerts!.Evaluate(sample))
                        WriteAlertMarker(alert);
                    foreach (var alert in _alerts.CheckDisksIfDue())
                        WriteAlertMarker(alert);
                }
                catch { /* un échantillon raté ne doit jamais arrêter la boîte noire */ }

                await Task.Delay(SampleInterval, stoppingToken);
            }
        }
        catch (OperationCanceledException) { /* arrêt demandé */ }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        // D'abord arrêter la boucle d'échantillonnage (base.StopAsync annule le token
        // et attend ExecuteAsync), PUIS écrire le marqueur d'arrêt propre — sinon un
        // échantillon en vol pourrait s'écrire après le « x » et fausser la détection
        // d'arrêt brutal à la session suivante.
        foreach (var w in _watchers)
        {
            try { w.Enabled = false; w.Dispose(); } catch { }
        }
        await base.StopAsync(cancellationToken);
        WriteLine(new FlightSample { Time = DateTime.Now, Kind = "x" });
        lock (_writeLock) { _stream?.Dispose(); _stream = null; }
    }

    // ------------------------------------------------------------------
    // Écriture du journal (une ligne = une entrée, flush physique immédiat)
    // ------------------------------------------------------------------

    private void WriteLine(FlightSample entry)
    {
        try
        {
            lock (_writeLock)
            {
                var file = Path.Combine(FlightDir, $"flight_{DateTime.Now:yyyyMMdd}.jsonl");
                if (_stream is null || file != _currentFile)
                {
                    _stream?.Dispose();
                    _stream = new FileStream(file, FileMode.Append, FileAccess.Write, FileShare.Read,
                                             bufferSize: 4096, FileOptions.WriteThrough);
                    _currentFile = file;
                    CleanupOldFiles();
                }
                var json = JsonSerializer.Serialize(entry, JsonOpts);
                var bytes = Encoding.UTF8.GetBytes(json + "\n");
                _stream.Write(bytes, 0, bytes.Length);
                _stream.Flush(flushToDisk: true);
            }
        }
        catch { /* disque plein / verrouillé : on réessaiera à la prochaine ligne */ }
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private void CleanupOldFiles()
    {
        try
        {
            foreach (var f in Directory.EnumerateFiles(FlightDir, "flight_*.jsonl"))
                if (File.GetLastWriteTime(f) < DateTime.Now.AddDays(-RetentionDays))
                    File.Delete(f);
        }
        catch { }
    }

    private static bool PreviousSessionEndedAbruptly()
    {
        try
        {
            var latest = Directory.EnumerateFiles(FlightDir, "flight_*.jsonl")
                                  .OrderByDescending(f => f).FirstOrDefault();
            if (latest is null) return false;
            // Dernière ligne non vide du dernier fichier : un arrêt propre se termine par "x".
            string? lastLine = null;
            using var fs = new FileStream(latest, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(fs);
            while (reader.ReadLine() is { } line)
                if (!string.IsNullOrWhiteSpace(line)) lastLine = line;
            if (lastLine is null) return false;
            var entry = JsonSerializer.Deserialize<FlightSample>(lastLine);
            return entry?.Kind != "x";
        }
        catch { return false; }
    }

    // ------------------------------------------------------------------
    // Mémoire (API Windows, léger et fiable)
    // ------------------------------------------------------------------

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys, ullAvailPhys, ullTotalPageFile, ullAvailPageFile;
        public ulong ullTotalVirtual, ullAvailVirtual, ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX buffer);

    private static (double? MemPct, double? CommitPct) ReadMemory()
    {
        var ms = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
        if (!GlobalMemoryStatusEx(ref ms)) return (null, null);
        double? commit = ms.ullTotalPageFile == 0 ? null
            : Math.Round(100.0 * (ms.ullTotalPageFile - ms.ullAvailPageFile) / ms.ullTotalPageFile, 1);
        return (ms.dwMemoryLoad, commit);
    }

    private static string? TopProcesses()
    {
        try
        {
            var top = Process.GetProcesses()
                .Select(p =>
                {
                    try { return (p.ProcessName, Bytes: p.PrivateMemorySize64); }
                    catch { return (p.ProcessName, Bytes: 0L); }
                    finally { p.Dispose(); }
                })
                .OrderByDescending(x => x.Bytes)
                .Take(3)
                .Select(x => $"{x.ProcessName} {x.Bytes / 1024 / 1024} Mo");
            return string.Join(", ", top);
        }
        catch { return null; }
    }

    // ------------------------------------------------------------------
    // Événements critiques en direct
    // ------------------------------------------------------------------

    private void StartEventWatchers()
    {
        Subscribe("System",
            "*[System[Provider[@Name='Microsoft-Windows-WHEA-Logger'] or " +
            "(Provider[@Name='Microsoft-Windows-Kernel-Power'] and EventID=41) or " +
            "(Provider[@Name='Microsoft-Windows-Resource-Exhaustion-Detector'] and EventID=2004) or " +
            "((Provider[@Name='disk'] or Provider[@Name='Ntfs'] or Provider[@Name='stornvme'] or Provider[@Name='storahci']) and (Level=1 or Level=2))]]",
            "System");
        Subscribe("Application",
            "*[System[(Provider[@Name='Application Error'] and EventID=1000) or (Provider[@Name='Application Hang'] and EventID=1002)]]",
            "Application");
    }

    private void Subscribe(string log, string xpath, string label)
    {
        try
        {
            var watcher = new EventLogWatcher(new EventLogQuery(log, PathType.LogName, xpath));
            watcher.EventRecordWritten += (_, e) =>
            {
                if (e.EventRecord is not { } rec) return;
                try
                {
                    string msg;
                    try { msg = rec.FormatDescription() ?? ""; } catch { msg = ""; }
                    var category = $"{rec.ProviderName}#{rec.Id}";
                    WriteLine(new FlightSample
                    {
                        Time = rec.TimeCreated?.ToLocalTime() ?? DateTime.Now,
                        Kind = "e",
                        EventCategory = category,
                        EventMessage = msg.Length > 220 ? msg[..220] : msg,
                    });

                    // Certains événements méritent une alerte préventive immédiate.
                    if (_alerts?.EvaluateEvent(category, msg) is { } alert)
                        WriteAlertMarker(alert);
                }
                catch { }
                finally { rec.Dispose(); }
            };
            watcher.Enabled = true;
            _watchers.Add(watcher);
        }
        catch { /* journal indisponible : on continue sans */ }
    }
}
