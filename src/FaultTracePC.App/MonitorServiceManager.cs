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

    /// <summary>Dossier de déploiement stable du service — JAMAIS le dossier de build,
    /// sinon le service en cours d'exécution verrouille les DLL et bloque toute recompilation.</summary>
    private static string DeployDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                     "FaultTracePC", "Service");

    public static (bool Ok, string Message) InstallAndStart()
    {
        var sourceExe = FindMonitorExe();
        if (sourceExe is null)
            return (false, "FaultTracePC.Monitor.exe introuvable. Compile d'abord la solution complète (dotnet build), ou publie les deux projets dans le même dossier.");

        bool alreadyInstalled = GetState() != MonitorState.NotInstalled;

        // Mise à jour : arrêter l'instance en cours pour libérer les fichiers déployés.
        if (GetState() == MonitorState.Running)
        {
            RunSc($"stop {ServiceName}");
            Thread.Sleep(2000);
        }

        // Copie du build vers le dossier stable (réessai une fois si un fichier est encore tenu).
        try { CopyDirectory(Path.GetDirectoryName(sourceExe)!, DeployDir); }
        catch (IOException)
        {
            Thread.Sleep(2000);
            try { CopyDirectory(Path.GetDirectoryName(sourceExe)!, DeployDir); }
            catch (Exception ex) { return (false, $"Impossible de déployer le service vers {DeployDir} : {ex.Message}"); }
        }
        catch (Exception ex) { return (false, $"Impossible de déployer le service vers {DeployDir} : {ex.Message}"); }

        var deployedExe = Path.Combine(DeployDir, "FaultTracePC.Monitor.exe");

        if (!alreadyInstalled)
        {
            var (code, output) = RunSc($"create {ServiceName} binPath= \"{deployedExe}\" start= auto DisplayName= \"FaultTracePC — Surveillance temps réel\"");
            if (code != 0 && !output.Contains("1073")) // 1073 = existe déjà
                return (false, $"Échec de l'installation du service : {output.Trim()}");
            RunSc($"description {ServiceName} \"Boîte noire FaultTracePC : journal continu (températures, mémoire, événements) pour retrouver les secondes précédant un crash.\"");
            RunSc($"failure {ServiceName} reset= 86400 actions= restart/60000/restart/60000/restart/60000");
        }
        else
        {
            // Service existant (peut-être enregistré sur un ancien chemin) : on repointe vers le déploiement stable.
            RunSc($"config {ServiceName} binPath= \"{deployedExe}\" start= auto");
        }

        var (startCode, startOut) = RunSc($"start {ServiceName}");
        return startCode == 0 || startOut.Contains("1056") // 1056 = déjà démarré
            ? (true, $"Surveillance {(alreadyInstalled ? "mise à jour" : "installée")} et démarrée depuis {DeployDir}. Journal : C:\\ProgramData\\FaultTracePC\\Flight.")
            : (false, $"Service installé mais démarrage en échec : {startOut.Trim()}");
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(source))
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), overwrite: true);
        foreach (var dir in Directory.EnumerateDirectories(source))
            CopyDirectory(dir, Path.Combine(destination, Path.GetFileName(dir)));
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

    /// <summary>Arrête le service SANS le désinstaller (il redémarrera au prochain boot).</summary>
    public static (bool Ok, string Message) StopOnly()
    {
        var (code, output) = RunSc($"stop {ServiceName}");
        return code == 0
            ? (true, "Surveillance arrêtée. Le service reste installé et redémarrera au prochain démarrage du PC — pour l'arrêter définitivement : bouton 📡 → désinstaller.")
            : (false, output.Trim());
    }

    /// <summary>Redémarre le service (nécessaire après un changement de configuration réseau).</summary>
    public static void Restart()
    {
        RunSc($"stop {ServiceName}");
        Thread.Sleep(2000);
        RunSc($"start {ServiceName}");
    }

    // ------------------------------------------------------------------
    // Pare-feu : règle entrante limitée aux plages privées (défense en profondeur,
    // en plus du double contrôle IP+token effectué par le service lui-même).
    // ------------------------------------------------------------------

    private const string FirewallRuleName = "FaultTracePC Telemetry";

    public static void EnsureFirewallRule(int port)
    {
        RunNetsh($"advfirewall firewall delete rule name=\"{FirewallRuleName}\"");
        RunNetsh($"advfirewall firewall add rule name=\"{FirewallRuleName}\" dir=in action=allow protocol=TCP localport={port} " +
                 "remoteip=127.0.0.1,10.0.0.0/8,172.16.0.0/12,192.168.0.0/16");
    }

    public static void RemoveFirewallRule() =>
        RunNetsh($"advfirewall firewall delete rule name=\"{FirewallRuleName}\"");

    private static void RunNetsh(string args)
    {
        try
        {
            var psi = new ProcessStartInfo("netsh.exe", args)
            { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            using var p = Process.Start(psi)!;
            p.StandardOutput.ReadToEnd();
            p.WaitForExit(10000);
        }
        catch { /* best effort — le service refuse de toute façon les IP non privées */ }
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
