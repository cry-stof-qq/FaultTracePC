using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace FaultTracePC.App;

public enum MonitorState { NotInstalled, Stopped, Running, ExeNotFound }

/// <summary>
/// Gestion du service de surveillance (FaultTracePCMonitor) : installation,
/// démarrage, arrêt, désinstallation via sc.exe — l'application tournant déjà
/// en administrateur, aucune élévation supplémentaire n'est nécessaire.
/// </summary>
public static class MonitorServiceManager
{
    public const string ServiceName = "FaultTracePCMonitor";

    public static MonitorState GetState()
    {
        // L'existence du service se lit de façon fiable (et sans dépendre de la langue)
        // dans le registre ; son état via sc query (le mot-clé RUNNING n'est pas localisé).
        using var key = Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Services\{ServiceName}");
        if (key is null) return MonitorState.NotInstalled;

        var (_, output) = RunSc($"query {ServiceName}");
        return output.Contains("RUNNING", StringComparison.OrdinalIgnoreCase)
            ? MonitorState.Running
            : MonitorState.Stopped;
    }

    /// <summary>Cherche FaultTracePC.Monitor.exe (dossier de l'app, puis sortie de build du projet frère).</summary>
    public static string? FindMonitorExe()
    {
        var appDir = AppContext.BaseDirectory;
        var candidates = new List<string>
        {
            Path.Combine(appDir, "FaultTracePC.Monitor.exe"),
        };
        // En développement (dotnet build) : remonter vers le projet frère.
        try
        {
            var dir = new DirectoryInfo(appDir);
            for (int i = 0; i < 6 && dir is not null; i++, dir = dir.Parent)
            {
                var probe = Path.Combine(dir.FullName, "FaultTracePC.Monitor", "bin");
                if (Directory.Exists(probe))
                {
                    candidates.AddRange(Directory.EnumerateFiles(probe, "FaultTracePC.Monitor.exe", SearchOption.AllDirectories));
                    break;
                }
            }
        }
        catch { }
        return candidates.FirstOrDefault(File.Exists);
    }

    public static (bool Ok, string Message) InstallAndStart()
    {
        var exe = FindMonitorExe();
        if (exe is null)
            return (false, "FaultTracePC.Monitor.exe introuvable. Compile d'abord la solution complète (dotnet build), ou publie les deux projets dans le même dossier.");

        var (code, output) = RunSc($"create {ServiceName} binPath= \"{exe}\" start= auto DisplayName= \"FaultTracePC — Surveillance temps réel\"");
        if (code != 0 && !output.Contains("1073")) // 1073 = existe déjà
            return (false, $"Échec de l'installation du service : {output.Trim()}");

        RunSc($"description {ServiceName} \"Boîte noire FaultTracePC : journal continu (températures, mémoire, événements) pour retrouver les secondes précédant un crash.\"");
        RunSc($"failure {ServiceName} reset= 86400 actions= restart/60000/restart/60000/restart/60000");

        var (startCode, startOut) = RunSc($"start {ServiceName}");
        return startCode == 0 || startOut.Contains("1056") // 1056 = déjà démarré
            ? (true, "Surveillance temps réel installée et démarrée. Le journal s'écrit dans C:\\ProgramData\\FaultTracePC\\Flight.")
            : (false, $"Service installé mais démarrage en échec : {startOut.Trim()}");
    }

    public static (bool Ok, string Message) StopAndUninstall()
    {
        RunSc($"stop {ServiceName}");
        // Petite attente pour laisser le service écrire son marqueur d'arrêt propre.
        Thread.Sleep(1500);
        var (code, output) = RunSc($"delete {ServiceName}");
        return code == 0
            ? (true, "Surveillance arrêtée et désinstallée. Le journal existant est conservé pour les analyses.")
            : (false, $"Échec de la désinstallation : {output.Trim()}");
    }

    public static (bool Ok, string Message) Start()
    {
        var (code, output) = RunSc($"start {ServiceName}");
        return code == 0 ? (true, "Surveillance démarrée.") : (false, output.Trim());
    }

    private static (int ExitCode, string Output) RunSc(string args)
    {
        try
        {
            var psi = new ProcessStartInfo("sc.exe", args)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var p = Process.Start(psi)!;
            var output = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
            p.WaitForExit(15000);
            return (p.ExitCode, output);
        }
        catch (Exception ex)
        {
            return (-1, ex.Message);
        }
    }
}
