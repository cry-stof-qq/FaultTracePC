using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

using FaultTracePC.Core;

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

    /// <summary>
    /// true si le service a été posé par le paquet MSI (binaire sous Program Files) :
    /// dans ce cas l'application ne redéploie rien, elle se contente de le piloter —
    /// c'est l'installeur qui gère les mises à jour.
    /// </summary>
    public static bool IsManagedByInstaller()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Services\{ServiceName}");
            var imagePath = key?.GetValue("ImagePath")?.ToString() ?? "";
            return imagePath.Contains(@"\Program Files", StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    public static (bool Ok, string Message) InstallAndStart()
    {
        // Service installé par le MSI : on ne touche pas à ses fichiers, mais on le
        // REDÉMARRE réellement — cette méthode est aussi appelée après un changement
        // de configuration réseau, que le service ne relit qu'au démarrage.
        if (IsManagedByInstaller())
        {
            if (GetState() == MonitorState.Running)
            {
                RunSc($"stop {ServiceName}");
                Thread.Sleep(2000);
            }
            var (c, o) = RunSc($"start {ServiceName}");
            return c == 0 || o.Contains("1056")
                ? (true, Lang.T("Surveillance (re)démarrée. Service installé par le paquet MSI : ses mises à jour passent par l'installeur.",
                                "Monitoring (re)started. This service was installed by the MSI package: its updates go through the installer."))
                : (false, Lang.T($"Service MSI présent mais démarrage en échec : {o.Trim()}",
                                 $"MSI service present but failed to start: {o.Trim()}"));
        }

        var sourceExe = FindMonitorExe();
        if (sourceExe is null)
            return (false, Lang.T("FaultTracePC.Monitor.exe introuvable. Compile d'abord la solution complète (dotnet build), ou publie les deux projets dans le même dossier.",
                                  "FaultTracePC.Monitor.exe not found. Build the whole solution first (dotnet build), or publish both projects into the same folder."));

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
            catch (Exception ex) { return (false, Lang.T($"Impossible de déployer le service vers {DeployDir} : {ex.Message}", $"Cannot deploy the service to {DeployDir}: {ex.Message}")); }
        }
        catch (Exception ex) { return (false, Lang.T($"Impossible de déployer le service vers {DeployDir} : {ex.Message}", $"Cannot deploy the service to {DeployDir}: {ex.Message}")); }

        var deployedExe = Path.Combine(DeployDir, "FaultTracePC.Monitor.exe");

        if (!alreadyInstalled)
        {
            // Le nom affiché et la description sont posés dans le registre Windows au moment
            // de l'installation : ils gardent la langue du poste qui a installé le service.
            var displayName = Lang.T("FaultTracePC — Surveillance temps réel", "FaultTracePC — Real-time monitoring");
            var (code, output) = RunSc($"create {ServiceName} binPath= \"{deployedExe}\" start= auto DisplayName= \"{displayName}\"");
            if (code != 0 && !output.Contains("1073")) // 1073 = existe déjà
                return (false, Lang.T($"Échec de l'installation du service : {output.Trim()}", $"Service installation failed: {output.Trim()}"));
            var description = Lang.T(
                "Boîte noire FaultTracePC : journal continu (températures, mémoire, événements) pour retrouver les secondes précédant un crash.",
                "FaultTracePC black box: continuous log (temperatures, memory, events) to recover the seconds before a crash.");
            RunSc($"description {ServiceName} \"{description}\"");
            RunSc($"failure {ServiceName} reset= 86400 actions= restart/60000/restart/60000/restart/60000");
        }
        else
        {
            // Service existant (peut-être enregistré sur un ancien chemin) : on repointe vers le déploiement stable.
            RunSc($"config {ServiceName} binPath= \"{deployedExe}\" start= auto");
        }

        var (startCode, startOut) = RunSc($"start {ServiceName}");
        return startCode == 0 || startOut.Contains("1056") // 1056 = déjà démarré
            ? (true, Lang.T($"Surveillance {(alreadyInstalled ? "mise à jour" : "installée")} et démarrée depuis {DeployDir}. Journal : C:\\ProgramData\\FaultTracePC\\Flight.",
                            $"Monitoring {(alreadyInstalled ? "updated" : "installed")} and started from {DeployDir}. Log: C:\\ProgramData\\FaultTracePC\\Flight."))
            : (false, Lang.T($"Service installé mais démarrage en échec : {startOut.Trim()}", $"Service installed but failed to start: {startOut.Trim()}"));
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
        if (IsManagedByInstaller())
        {
            var (c, o) = RunSc($"stop {ServiceName}");
            return c == 0 || o.Contains("1062")
                ? (true, Lang.T("Surveillance arrêtée. Ce service a été installé par le paquet MSI : pour le retirer complètement, désinstalle FaultTracePC depuis « Applications installées ».",
                                "Monitoring stopped. This service was installed by the MSI package: to remove it completely, uninstall FaultTracePC from “Installed apps”."))
                : (false, o.Trim());
        }

        RunSc($"stop {ServiceName}");
        // Petite attente pour laisser le service écrire son marqueur d'arrêt propre.
        Thread.Sleep(1500);
        var (code, output) = RunSc($"delete {ServiceName}");
        return code == 0
            ? (true, Lang.T("Surveillance arrêtée et désinstallée. Le journal existant est conservé pour les analyses.",
                            "Monitoring stopped and uninstalled. The existing log is kept for analysis."))
            : (false, Lang.T($"Échec de la désinstallation : {output.Trim()}", $"Uninstall failed: {output.Trim()}"));
    }

    public static (bool Ok, string Message) Start()
    {
        var (code, output) = RunSc($"start {ServiceName}");
        return code == 0 ? (true, Lang.T("Surveillance démarrée.", "Monitoring started.")) : (false, output.Trim());
    }

    /// <summary>Arrête le service SANS le désinstaller (il redémarrera au prochain boot).</summary>
    public static (bool Ok, string Message) StopOnly()
    {
        var (code, output) = RunSc($"stop {ServiceName}");
        return code == 0
            ? (true, Lang.T("Surveillance arrêtée. Le service reste installé et redémarrera au prochain démarrage du PC — pour l'arrêter définitivement : bouton 📡 → désinstaller.",
                            "Monitoring stopped. The service stays installed and will restart at the next boot — to stop it for good: 📡 button → uninstall."))
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

    // La règle elle-même vit dans Core (FirewallRule) : la ligne de commande en a
    // besoin pour les déploiements sans interface, et deux copies du même netsh
    // auraient fini par diverger.
    public static void EnsureFirewallRule(int port) => FirewallRule.Poser(port, out _);

    public static void RemoveFirewallRule() => FirewallRule.Retirer(out _);

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
