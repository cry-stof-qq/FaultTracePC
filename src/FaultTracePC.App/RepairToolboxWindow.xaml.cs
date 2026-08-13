using System.Diagnostics;
using System.Management;
using System.Text.RegularExpressions;
using System.Windows;

namespace FaultTracePC.App;

/// <summary>
/// Boîte à outils : réparations à un clic, exécutées dans une fenêtre PowerShell
/// VISIBLE (l'utilisateur voit ce qui se passe — pédagogie et confiance).
/// Traitement dédié du cas « mise à jour Windows mal passée » : désinstallation
/// ciblée, réinitialisation des composants WU, réparation sur place.
/// </summary>
public partial class RepairToolboxWindow : Window
{
    public RepairToolboxWindow()
    {
        InitializeComponent();
        LoadUpdates();
    }

    // ------------------------------------------------------------------
    // Liste des mises à jour installées (Win32_QuickFixEngineering)
    // ------------------------------------------------------------------

    public sealed class UpdateRow
    {
        public string Kb { get; set; } = "";
        public string Description { get; set; } = "";
        public string InstalledOn { get; set; } = "";
    }

    private void BtnLoadUpdates_Click(object sender, RoutedEventArgs e) => LoadUpdates();

    private void LoadUpdates()
    {
        try
        {
            var rows = new List<UpdateRow>();
            using var searcher = new ManagementObjectSearcher(
                "SELECT HotFixID, Description, InstalledOn FROM Win32_QuickFixEngineering");
            foreach (ManagementObject mo in searcher.Get())
            {
                rows.Add(new UpdateRow
                {
                    Kb = mo["HotFixID"]?.ToString() ?? "",
                    Description = mo["Description"]?.ToString() ?? "",
                    InstalledOn = mo["InstalledOn"]?.ToString() ?? "",
                });
            }
            // Les plus récentes d'abord (la date QFE est au format américain ou vide).
            LvUpdates.ItemsSource = rows
                .OrderByDescending(r => DateTime.TryParse(r.InstalledOn, out var d) ? d : DateTime.MinValue)
                .ToList();
            TxtStatus.Text = $"{rows.Count} mise(s) à jour listée(s) (correctifs CBS — les mises à jour du Store et pilotes n'apparaissent pas ici).";
        }
        catch (Exception ex)
        {
            TxtStatus.Text = "Impossible de lister les mises à jour : " + ex.Message;
        }
    }

    private void BtnUninstallUpdate_Click(object sender, RoutedEventArgs e)
    {
        if (LvUpdates.SelectedItem is not UpdateRow row)
        {
            MessageBox.Show(this, "Sélectionne d'abord une mise à jour dans la liste.", "FaultTracePC",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var m = Regex.Match(row.Kb, @"\d+");
        if (!m.Success)
        {
            MessageBox.Show(this, $"Numéro KB illisible : {row.Kb}", "FaultTracePC",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (MessageBox.Show(this,
                $"Désinstaller {row.Kb} ({row.Description}, installée le {row.InstalledOn}) ?\n\n" +
                "Windows demandera probablement un redémarrage. Attention : Windows Update réinstallera " +
                "cette mise à jour automatiquement sous quelques jours — si elle pose vraiment problème, " +
                "suspends les mises à jour une semaine le temps qu'un correctif sorte.",
                "FaultTracePC", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        LaunchPs($"Write-Host 'Désinstallation de {row.Kb}…' -ForegroundColor Cyan; " +
                 $"wusa /uninstall /kb:{m.Value} /promptrestart");
        TxtStatus.Text = $"Désinstallation de {row.Kb} lancée — suis la fenêtre PowerShell.";
    }

    /// <summary>
    /// Crée un point de restauration système. Windows limite la fréquence de
    /// création (un point par 24 h par défaut) : on lève cette limite le temps
    /// de l'opération, sinon l'appel échoue silencieusement.
    /// </summary>
    private void BtnRestorePoint_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(this,
                "Créer un point de restauration système maintenant ?\n\n" +
                "Cela permettra de revenir à l'état actuel si une réparation se passe mal. " +
                "Opération sans risque, qui prend quelques dizaines de secondes.",
                "FaultTracePC", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        LaunchPs(
            "Write-Host 'Création du point de restauration…' -ForegroundColor Cyan; " +
            // La protection système doit être active sur C: pour que le point existe.
            "try { Enable-ComputerRestore -Drive 'C:\\' -ErrorAction SilentlyContinue } catch {}; " +
            "New-ItemProperty -Path 'HKLM:\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\SystemRestore' " +
            "-Name 'SystemRestorePointCreationFrequency' -Value 0 -PropertyType DWord -Force | Out-Null; " +
            "Checkpoint-Computer -Description 'FaultTracePC - avant reparation' -RestorePointType 'MODIFY_SETTINGS'; " +
            "if ($?) { Write-Host 'Point de restauration créé.' -ForegroundColor Green } " +
            "else { Write-Host 'Échec : la protection système est peut-être désactivée (Panneau de configuration > Système > Protection du système).' -ForegroundColor Yellow }; " +
            "Get-ComputerRestorePoint | Select-Object -Last 5 SequenceNumber, Description, CreationTime | Format-Table -AutoSize");
    }

    private void BtnWindowsUpdate_Click(object sender, RoutedEventArgs e) =>
        new WindowsUpdateWindow { Owner = this }.Show();

    private void BtnInPlaceRepair_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(this,
            "La réparation sur place réinstalle la MÊME version de Windows en conservant fichiers, " +
            "applications et paramètres (~30-60 min, un redémarrage).\n\n" +
            "Dans la page qui va s'ouvrir : « Résoudre les problèmes à l'aide de Windows Update » → Réinstaller maintenant.",
            "FaultTracePC", MessageBoxButton.OK, MessageBoxImage.Information);
        try { Process.Start(new ProcessStartInfo("ms-settings:recovery") { UseShellExecute = true }); }
        catch (Exception ex) { TxtStatus.Text = "Impossible d'ouvrir les Paramètres : " + ex.Message; }
    }

    // ------------------------------------------------------------------
    // Outils génériques (Tag → commande)
    // ------------------------------------------------------------------

    private void BtnTool_Click(object sender, RoutedEventArgs e)
    {
        var tag = (sender as FrameworkElement)?.Tag as string ?? "";

        // Actions qui MODIFIENT le système : confirmation préalable.
        var confirmations = new Dictionary<string, string>
        {
            ["wureset"] = "Réinitialiser les composants Windows Update ?\n\nLes services seront arrêtés, les caches de téléchargement purgés (SoftwareDistribution, catroot2) puis les services redémarrés. Sans danger pour tes fichiers — l'historique d'affichage des mises à jour sera vidé.",
            ["dismrestore"] = "Lancer la réparation DISM /RestoreHealth ?\n\n~15 minutes, télécharge des fichiers sains depuis Windows Update.",
            ["chkdskfix"] = "Planifier chkdsk C: /f ?\n\nLa vérification s'exécutera au prochain redémarrage du PC.",
            ["mdsched"] = "Lancer le diagnostic mémoire Windows ?\n\n⚠ Le PC REDÉMARRE immédiatement pour tester la RAM.",
            ["componentcleanup"] = "Purger les composants Windows obsolètes ?\n\n~10 à 20 minutes. Récupère souvent plusieurs gigaoctets.\n\n⚠ Après cette opération, les mises à jour déjà installées ne pourront plus être désinstallées — à éviter si tu suspectes justement une mise à jour récente.",
            ["temp"] = "Vider les fichiers temporaires ?\n\nLes fichiers en cours d'utilisation seront ignorés. Sans risque.",
            ["networkreset"] = "Réinitialiser la pile réseau ?\n\nWinsock, configuration IP et cache DNS seront remis à zéro.\n\n⚠ Un REDÉMARRAGE sera nécessaire, et les paramètres réseau manuels (IP fixe, proxy) devront être reconfigurés.",
            ["defenderfull"] = "Lancer une analyse COMPLÈTE de Microsoft Defender ?\n\nElle peut durer plus d'une heure et ralentir la machine pendant ce temps.",
        };
        if (confirmations.TryGetValue(tag, out var msg) &&
            MessageBox.Show(this, msg, "FaultTracePC", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        switch (tag)
        {
            case "sfc": LaunchPs("sfc /scannow"); break;
            case "dismscan": LaunchPs("DISM /Online /Cleanup-Image /ScanHealth"); break;
            case "dismrestore": LaunchPs("DISM /Online /Cleanup-Image /RestoreHealth"); break;
            case "chkdskscan": LaunchPs("Repair-Volume -DriveLetter C -Scan"); break;
            case "chkdskfix": LaunchPs("chkdsk C: /f"); break;
            case "mdsched": LaunchPs("mdsched.exe"); break;
            case "energy":
                LaunchPs("$out = Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'FaultTracePC\\rapport-energie.html'; " +
                         "powercfg /energy /output $out /duration 60; Start-Process $out");
                break;
            case "smart":
                LaunchPs("Get-PhysicalDisk | Select-Object FriendlyName, MediaType, HealthStatus, OperationalStatus | Format-Table -AutoSize; " +
                         "Get-PhysicalDisk | Get-StorageReliabilityCounter | Select-Object DeviceId, Temperature, Wear, ReadErrorsTotal, WriteErrorsTotal, PowerOnHours | Format-Table -AutoSize");
                break;
            case "wureset":
                LaunchPs(
                    "Write-Host 'Réinitialisation des composants Windows Update…' -ForegroundColor Cyan; " +
                    "net stop wuauserv; net stop bits; net stop cryptsvc; " +
                    "if (Test-Path C:\\Windows\\SoftwareDistribution.old) { Remove-Item C:\\Windows\\SoftwareDistribution.old -Recurse -Force -ErrorAction SilentlyContinue }; " +
                    "if (Test-Path C:\\Windows\\System32\\catroot2.old) { Remove-Item C:\\Windows\\System32\\catroot2.old -Recurse -Force -ErrorAction SilentlyContinue }; " +
                    "Rename-Item C:\\Windows\\SoftwareDistribution SoftwareDistribution.old -Force -ErrorAction Continue; " +
                    "Rename-Item C:\\Windows\\System32\\catroot2 catroot2.old -Force -ErrorAction Continue; " +
                    "net start cryptsvc; net start bits; net start wuauserv; " +
                    "Write-Host 'Terminé. Relance la recherche de mises à jour dans les Paramètres.' -ForegroundColor Green");
                break;
            // ---- Espace disque ----
            case "diskusage":
                LaunchPs(
                    "function Taille($p) { if (Test-Path $p) { try { '{0:N1} Go' -f ((Get-ChildItem $p -Recurse -Force -ErrorAction SilentlyContinue | Measure-Object Length -Sum).Sum / 1GB) } catch { 'inaccessible' } } else { 'absent' } }; " +
                    "Write-Host 'Espace occupé par les principaux postes :' -ForegroundColor Cyan; " +
                    "Write-Host ('  Composants Windows (WinSxS) : ' + (Taille 'C:\\Windows\\WinSxS')); " +
                    "Write-Host ('  Windows.old (ancienne installation) : ' + (Taille 'C:\\Windows.old')); " +
                    "Write-Host ('  Temporaires utilisateur : ' + (Taille $env:TEMP)); " +
                    "Write-Host ('  Temporaires Windows : ' + (Taille 'C:\\Windows\\Temp')); " +
                    "Write-Host ('  Cache Windows Update : ' + (Taille 'C:\\Windows\\SoftwareDistribution\\Download')); " +
                    "Write-Host ''; Get-Volume | Where-Object DriveLetter | Select-Object DriveLetter, FileSystemLabel, " +
                    "@{n='Libre (Go)';e={[math]::Round($_.SizeRemaining/1GB,1)}}, @{n='Total (Go)';e={[math]::Round($_.Size/1GB,1)}} | Format-Table -AutoSize");
                break;

            case "componentcleanup":
                LaunchPs("Write-Host 'Analyse puis purge des composants obsolètes (patience)…' -ForegroundColor Cyan; " +
                         "DISM /Online /Cleanup-Image /AnalyzeComponentStore; " +
                         "DISM /Online /Cleanup-Image /StartComponentCleanup");
                break;

            case "temp":
                LaunchPs(
                    "$avant = (Get-PSDrive C).Free; " +
                    "Write-Host 'Suppression des fichiers temporaires…' -ForegroundColor Cyan; " +
                    "Remove-Item \"$env:TEMP\\*\" -Recurse -Force -ErrorAction SilentlyContinue; " +
                    "Remove-Item 'C:\\Windows\\Temp\\*' -Recurse -Force -ErrorAction SilentlyContinue; " +
                    "$apres = (Get-PSDrive C).Free; " +
                    "Write-Host ('Espace libéré : {0:N2} Go' -f (($apres - $avant)/1GB)) -ForegroundColor Green");
                break;

            case "cleanmgr": Open("cleanmgr.exe", "/d C:"); break;

            // ---- Démarrage / sécurité / réseau ----
            case "startup":
                LaunchPs(
                    "Write-Host 'Programmes lancés au démarrage :' -ForegroundColor Cyan; " +
                    "Get-CimInstance Win32_StartupCommand | Select-Object Name, Command, Location, User | Format-Table -AutoSize -Wrap; " +
                    "Write-Host 'Dossier Démarrage :' -ForegroundColor Cyan; " +
                    "Get-ChildItem \"$env:APPDATA\\Microsoft\\Windows\\Start Menu\\Programs\\Startup\", " +
                    "'C:\\ProgramData\\Microsoft\\Windows\\Start Menu\\Programs\\Startup' -ErrorAction SilentlyContinue | Select-Object Name, LastWriteTime | Format-Table -AutoSize; " +
                    "Write-Host 'Pour en désactiver : Gestionnaire des tâches > onglet Démarrage.' -ForegroundColor Yellow");
                break;

            case "defenderquick":
                LaunchPs("Write-Host 'Analyse rapide en cours…' -ForegroundColor Cyan; Start-MpScan -ScanType QuickScan; " +
                         "Write-Host 'Terminé.' -ForegroundColor Green; Get-MpThreatDetection | Select-Object -Last 10 InitialDetectionTime, ThreatID, Resources | Format-Table -AutoSize");
                break;

            case "defenderfull":
                LaunchPs("Write-Host 'Analyse complète en cours (longue)…' -ForegroundColor Cyan; Start-MpScan -ScanType FullScan; " +
                         "Write-Host 'Terminé.' -ForegroundColor Green");
                break;

            case "defenderhistory":
                LaunchPs(
                    "Write-Host 'État de la protection :' -ForegroundColor Cyan; " +
                    "Get-MpComputerStatus | Select-Object AntivirusEnabled, RealTimeProtectionEnabled, AntivirusSignatureLastUpdated, QuickScanAge, FullScanAge | Format-List; " +
                    "Write-Host 'Menaces détectées :' -ForegroundColor Cyan; " +
                    "$t = Get-MpThreatDetection -ErrorAction SilentlyContinue; " +
                    "if ($t) { $t | Sort-Object InitialDetectionTime -Descending | Select-Object -First 20 InitialDetectionTime, ThreatID, Resources | Format-Table -AutoSize -Wrap } " +
                    "else { Write-Host '  Aucune menace détectée dans l''historique.' -ForegroundColor Green }");
                break;

            case "networkreset":
                LaunchPs("netsh winsock reset; netsh int ip reset; ipconfig /flushdns; ipconfig /registerdns; " +
                         "Write-Host 'Réinitialisation terminée — REDÉMARRE la machine pour qu''elle prenne effet.' -ForegroundColor Yellow");
                break;

            case "battery":
                LaunchPs("$out = Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'FaultTracePC\\rapport-batterie.html'; " +
                         "powercfg /batteryreport /output $out; " +
                         "if (Test-Path $out) { Start-Process $out } else { Write-Host 'Aucune batterie détectée (poste fixe ?).' -ForegroundColor Yellow }");
                break;

            case "resmon": Open("perfmon.exe", "/res"); break;
            case "rstrui": Open("rstrui.exe"); break;

            case "reliability": Open("perfmon.exe", "/rel"); break;
            case "eventvwr": Open("eventvwr.msc"); break;
            case "wusettings": Open("ms-settings:windowsupdate"); break;
            case "diskmgmt": Open("diskmgmt.msc"); break;
            case "msinfo": Open("msinfo32.exe"); break;
        }
    }

    /// <summary>Lance une commande dans une fenêtre PowerShell VISIBLE qui reste ouverte.</summary>
    private void LaunchPs(string command)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -NoExit -Command \"{command.Replace("\"", "\\\"")}\"",
                UseShellExecute = true,
            });
            TxtStatus.Text = "Commande lancée — suis son déroulement dans la fenêtre PowerShell.";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Impossible de lancer la commande : " + ex.Message, "FaultTracePC",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Open(string file, string args = "")
    {
        try { Process.Start(new ProcessStartInfo(file, args) { UseShellExecute = true }); }
        catch (Exception ex) { TxtStatus.Text = $"Impossible d'ouvrir {file} : {ex.Message}"; }
    }
}
