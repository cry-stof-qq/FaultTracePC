using System.Diagnostics;
using System.Management;
using System.Text.RegularExpressions;
using System.Windows;
using FaultTracePC.Core;

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
                .OrderByDescending(r => DateInstallation(r.InstalledOn))
                .ToList();
            TxtStatus.Text = Lang.T($"{rows.Count} mise(s) à jour listée(s) (correctifs CBS — les mises à jour du Store et pilotes n'apparaissent pas ici).", $"{rows.Count} update(s) listed (CBS hotfixes — Store updates and drivers do not appear here)");
        }
        catch (Exception ex)
        {
            TxtStatus.Text = Lang.T("Impossible de lister les mises à jour : ", "Could not list the updates: ") + ex.Message;
        }
    }


    /// <summary>
    /// <c>Win32_QuickFixEngineering.InstalledOn</c> est une chaîne dont le format
    /// varie d'une machine à l'autre — parfois « M/d/yyyy » quelle que soit la
    /// langue de Windows, parfois la culture du poste. On tente les deux plutôt
    /// que d'en imposer une : forcer la culture invariante casserait le tri là où
    /// il fonctionne aujourd'hui. Le résultat ne sert qu'à ordonner un affichage.
    /// </summary>
    private static DateTime DateInstallation(string brut) =>
        DateTime.TryParse(brut, System.Globalization.CultureInfo.CurrentCulture,
                          System.Globalization.DateTimeStyles.None, out var d)
        || DateTime.TryParse(brut, System.Globalization.CultureInfo.InvariantCulture,
                             System.Globalization.DateTimeStyles.None, out d)
            ? d : DateTime.MinValue;

    private void BtnUninstallUpdate_Click(object sender, RoutedEventArgs e)
    {
        if (LvUpdates.SelectedItem is not UpdateRow row)
        {
            MessageBox.Show(this, Lang.T("Sélectionne d'abord une mise à jour dans la liste.", "Select an update in the list first."), "FaultTracePC",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var m = Regex.Match(row.Kb, @"\d+");
        if (!m.Success)
        {
            MessageBox.Show(this, Lang.T($"Numéro KB illisible : {row.Kb}", $"Unreadable KB number: {row.Kb}"), "FaultTracePC",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (MessageBox.Show(this,
                Lang.T($"Désinstaller {row.Kb} ({row.Description}, installée le {row.InstalledOn}) ?\n\n", $"Uninstall {row.Kb} ({row.Description}, installed on {row.InstalledOn})?\n\n") +
                Lang.T("Windows demandera probablement un redémarrage. Attention : Windows Update réinstallera ", "Windows will probably ask for a restart. Careful: Windows Update will reinstall ") +
                Lang.T("cette mise à jour automatiquement sous quelques jours — si elle pose vraiment problème, ", "this update automatically within a few days — if it really is the problem, ") +
                Lang.T("suspends les mises à jour une semaine le temps qu'un correctif sorte.", "pause updates for a week until a fix is released."),
                "FaultTracePC", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        _currentTool = "uninstallkb";
        LaunchPs(Lang.T($"Write-Host 'Désinstallation de {row.Kb}…' -ForegroundColor Cyan; ", $"Write-Host 'Uninstalling {row.Kb}…' -ForegroundColor Cyan; ") +
                 $"wusa /uninstall /kb:{m.Value} /promptrestart");
        TxtStatus.Text = Lang.T($"Désinstallation de {row.Kb} lancée — suis la fenêtre PowerShell.", $"Uninstall of {row.Kb} started — follow the PowerShell window.");
    }

    /// <summary>
    /// Crée un point de restauration système. Windows limite la fréquence de
    /// création (un point par 24 h par défaut) : on lève cette limite le temps
    /// de l'opération, sinon l'appel échoue silencieusement.
    /// </summary>
    private void BtnRestorePoint_Click(object sender, RoutedEventArgs e)
    {
        _currentTool = "restorepoint";
        if (MessageBox.Show(this,
                Lang.T("Créer un point de restauration système maintenant ?\n\n", "Create a system restore point now?\n\n") +
                Lang.T("Cela permettra de revenir à l'état actuel si une réparation se passe mal. ", "It will make it possible to go back to the current state if a repair goes wrong. ") +
                Lang.T("Opération sans risque, qui prend quelques dizaines de secondes.", "A risk-free operation that takes a few tens of seconds."),
                "FaultTracePC", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        LaunchPs(
            Lang.T("Write-Host 'Création du point de restauration…' -ForegroundColor Cyan; ", "Write-Host 'Creating the restore point…' -ForegroundColor Cyan; ") +
            // La protection système doit être active sur C: pour que le point existe.
            "try { Enable-ComputerRestore -Drive 'C:\\' -ErrorAction SilentlyContinue } catch {}; " +
            "New-ItemProperty -Path 'HKLM:\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\SystemRestore' " +
            "-Name 'SystemRestorePointCreationFrequency' -Value 0 -PropertyType DWord -Force | Out-Null; " +
            Lang.T("Checkpoint-Computer -Description 'FaultTracePC - avant reparation' -RestorePointType 'MODIFY_SETTINGS'; ", "Checkpoint-Computer -Description 'FaultTracePC - before repair' -RestorePointType 'MODIFY_SETTINGS'; ") +
            Lang.T("if ($?) { Write-Host 'Point de restauration créé.' -ForegroundColor Green } ", "if ($?) { Write-Host 'Restore point created.' -ForegroundColor Green } ") +
            Lang.T("else { Write-Host 'Échec : la protection système est peut-être désactivée (Panneau de configuration > Système > Protection du système).' -ForegroundColor Yellow }; ", "else { Write-Host 'Failed: System Protection may be turned off (Control Panel > System > System Protection).' -ForegroundColor Yellow }") +
            "Get-ComputerRestorePoint | Select-Object -Last 5 SequenceNumber, Description, CreationTime | Format-Table -AutoSize");
    }

    private void BtnWindowsUpdate_Click(object sender, RoutedEventArgs e) =>
        new WindowsUpdateWindow { Owner = this }.Show();

    private void BtnInPlaceRepair_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(this,
            Lang.T("La réparation sur place réinstalle la MÊME version de Windows en conservant fichiers, ", "An in-place repair reinstalls the SAME version of Windows while keeping files, ") +
            Lang.T("applications et paramètres (~30-60 min, un redémarrage).\n\n", "applications and settings (~30-60 min, one restart).\n\n") +
            Lang.T("Dans la page qui va s'ouvrir : « Résoudre les problèmes à l'aide de Windows Update » → Réinstaller maintenant.", "On the page about to open: “Fix problems using Windows Update” → Reinstall now."),
            "FaultTracePC", MessageBoxButton.OK, MessageBoxImage.Information);
        try { Process.Start(new ProcessStartInfo("ms-settings:recovery") { UseShellExecute = true }); }
        catch (Exception ex) { TxtStatus.Text = Lang.T("Impossible d'ouvrir les Paramètres : ", "Could not open Settings: ") + ex.Message; }
    }

    // ------------------------------------------------------------------
    // Outils génériques (Tag → commande)
    // ------------------------------------------------------------------

    private void BtnTool_Click(object sender, RoutedEventArgs e)
    {
        var tag = (sender as FrameworkElement)?.Tag as string ?? "";
        // Mémorisé ici plutôt que passé à chaque appel : LaunchPs y lit l'action
        // en cours pour savoir si elle modifie le système et comment la nommer.
        _currentTool = tag;

        // Actions qui MODIFIENT le système : confirmation préalable.
        var confirmations = new Dictionary<string, string>
        {
            ["wureset"] = Lang.T("Réinitialiser les composants Windows Update ?\n\nLes services seront arrêtés, les caches de téléchargement purgés (SoftwareDistribution, catroot2) puis les services redémarrés. Sans danger pour tes fichiers — l'historique d'affichage des mises à jour sera vidé.", "Reset the Windows Update components?\n\nThe services will be stopped, the download caches purged, then the services restarted."),
            ["dismrestore"] = Lang.T("Lancer la réparation DISM /RestoreHealth ?\n\n~15 minutes, télécharge des fichiers sains depuis Windows Update.", "Run the DISM /RestoreHealth repair?\n\n~15 minutes, downloads healthy files from Windows Update."),
            ["chkdskfix"] = Lang.T("Planifier chkdsk C: /f ?\n\nLa vérification s'exécutera au prochain redémarrage du PC.", "Schedule chkdsk C: /f?\n\nThe check will run at the next restart of the PC."),
            ["mdsched"] = Lang.T("Lancer le diagnostic mémoire Windows ?\n\n⚠ Le PC REDÉMARRE immédiatement pour tester la RAM.", "Run the Windows Memory Diagnostic?\n\n⚠ The PC RESTARTS immediately to test the RAM."),
            ["componentcleanup"] = Lang.T("Purger les composants Windows obsolètes ?\n\n~10 à 20 minutes. Récupère souvent plusieurs gigaoctets.\n\n⚠ Après cette opération, les mises à jour déjà installées ne pourront plus être désinstallées — à éviter si tu suspectes justement une mise à jour récente.", "Purge the obsolete Windows components?\n\n~10 to 20 minutes. Often recovers several gigabytes.\n\n⚠ After this, the updates already installed can no longer be uninstalled."),
            ["temp"] = Lang.T("Vider les fichiers temporaires ?\n\nLes fichiers en cours d'utilisation seront ignorés. Sans risque.", "Empty the temporary files?\n\nFiles in use will be skipped. No risk."),
            ["networkreset"] = Lang.T("Réinitialiser la pile réseau ?\n\nWinsock, configuration IP et cache DNS seront remis à zéro.\n\n⚠ Un REDÉMARRAGE sera nécessaire, et les paramètres réseau manuels (IP fixe, proxy) devront être reconfigurés.", "Reset the network stack?\n\nWinsock, IP configuration and DNS cache will be reset.\n\n⚠ A RESTART is required."),
            ["defenderfull"] = Lang.T("Lancer une analyse COMPLÈTE de Microsoft Defender ?\n\nElle peut durer plus d'une heure et ralentir la machine pendant ce temps.", "Run a FULL Microsoft Defender scan?\n\nIt can take more than an hour and slow the machine down while it runs."),
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
            // Réglage d'alimentation des liens : cause la plus fréquemment
            // documentée des « réinitialisation au périphérique » (storahci 129).
            //
            // On AFFICHE, on ne modifie pas. Microsoft documente la syntaxe de
            // powercfg mais précise que les alias de sous-groupes varient selon les
            // systèmes : écrire un réglage à partir d'un alias non garanti ne
            // produirait aucune erreur, juste un réglage inchangé — et l'utilisateur
            // croirait avoir corrigé son problème. On lui montre l'état réel et on
            // lui ouvre le bon panneau ; c'est lui qui décide.
            // Installation de WinDbg. Sortie volontairement SANS ACCENTS : la console
            // n'est pas en UTF-8, l'ASCII s'affiche correctement sur toute page de codes.
            //
            // On installe le paquet winget, pas le SDK : le bouton sert une personne
            // devant sa machine. Sur un parc, WinDbg se déploie par GPO à côté de
            // FaultTracePC — c'est une decision de deploiement, pas un clic.
            case "windbg":
                LaunchPs(
                    "if (-not (Get-Command winget -ErrorAction SilentlyContinue)) { " +
                    Lang.T("  Write-Host 'winget est introuvable sur cette machine.' -ForegroundColor Red; ", "  Write-Host 'winget was not found on this machine.' -ForegroundColor Red; ") +
                    Lang.T("  Write-Host 'Installe App Installer depuis le Microsoft Store, ou les Debugging Tools for Windows via le SDK Windows.' -ForegroundColor Yellow ", "  Write-Host 'Install App Installer from the Microsoft Store, or the Debugging Tools for Windows from the Windows SDK.' -ForegroundColor Yellow; ") +
                    "} else { " +
                    Lang.T("  Write-Host 'Installation de WinDbg via winget...' -ForegroundColor Cyan; ", "  Write-Host 'Installing WinDbg through winget...' -ForegroundColor Cyan; ") +
                    "  winget install --id Microsoft.WinDbg --accept-package-agreements --accept-source-agreements; " +
                    "  if ($LASTEXITCODE -ne 0) { " +
                    "    Write-Host ''; " +
                    Lang.T("    Write-Host ('winget s''est termine avec le code ' + $LASTEXITCODE + '.') -ForegroundColor Yellow; ", "    Write-Host ('winget exited with code ' + $LASTEXITCODE + '.') -ForegroundColor Yellow; ") +
                    Lang.T("    Write-Host 'En etablissement, les sources winget sont frequemment bloquees par strategie.' -ForegroundColor Yellow; ", "    Write-Host 'In managed environments, winget sources are frequently blocked by policy.' -ForegroundColor Yellow; ") +
                    Lang.T("    Write-Host 'Repli : Debugging Tools for Windows via le SDK Windows, qui installe pour TOUTE la machine.' -ForegroundColor Yellow ", "    Write-Host 'Fallback: Debugging Tools for Windows from the Windows SDK, which installs for the WHOLE machine.' -ForegroundColor Yellow ") +
                    "  } else { " +
                    "    Write-Host ''; " +
                    Lang.T("    Write-Host 'Termine. Relance une analyse : le pilote fautif sera nomme si un dump est exploitable.' -ForegroundColor Green ", "    Write-Host 'Done. Run an analysis again: the faulting driver will be named if a dump can be read.' -ForegroundColor Green ") +
                    "  } " +
                    "}");
                break;
            case "linkpower":
                // Volontairement SANS ACCENTS : cette sortie s'affiche dans une
                // console dont la page de codes n'est pas UTF-8. L'ASCII s'affiche
                // correctement partout, y compris sur une session non francophone.
                LaunchPs(
                    Lang.T("Write-Host 'Reglages d''alimentation du schema actif' -ForegroundColor Cyan; ", "Write-Host 'Power settings of the active plan' -ForegroundColor Cyan; ") +
                    "Write-Host ''; " +
                    "$q = powercfg /query SCHEME_CURRENT 2>&1 | Out-String; " +
                    "$bloc = ($q -split '(?=Sous-groupe|Subgroup)') | Where-Object { $_ -match 'PCI|Disque dur|Hard disk' }; " +
                    Lang.T("if ($bloc) { $bloc | ForEach-Object { Write-Host $_ } } else { Write-Host 'Sous-groupes PCI Express / Disque introuvables dans la sortie de powercfg. Sortie complete ci-dessous :' -ForegroundColor Yellow; Write-Host $q }; ", "if ($bloc) { $bloc | ForEach-Object { Write-Host $_ } } else { Write-Host 'PCI Express / Disk subgroups not found in this power scheme.' -ForegroundColor Yellow }; ") +
                    "Write-Host ''; " +
                    Lang.T("Write-Host 'Une valeur d''index differente de 0 signifie que la gestion d''alimentation du lien est ACTIVE.' -ForegroundColor Yellow; ", "Write-Host 'An index value other than 0 means link power management is ACTIVE.' -ForegroundColor Yellow; ") +
                    Lang.T("Write-Host 'A modifier dans le panneau qui vient de s''ouvrir : PCI Express > Gestion de l''alimentation a l''etat de liaison > Desactive,' -ForegroundColor Yellow; ", "Write-Host 'Change it in the panel that just opened: PCI Express > Link State Power Management > Off,' -ForegroundColor Yellow; ") +
                    Lang.T("Write-Host 'et Disque dur > Arreter le disque dur apres > Jamais. Un redemarrage est necessaire.' -ForegroundColor Yellow; ", "Write-Host 'and Hard disk > Turn off hard disk after > Never. A restart is required.' -ForegroundColor Yellow") +
                    "Start-Process control.exe -ArgumentList 'powercfg.cpl,,3'");
                break;
            case "wureset":
                LaunchPs(
                    Lang.T("Write-Host 'Réinitialisation des composants Windows Update…' -ForegroundColor Cyan; ", "Write-Host 'Resetting the Windows Update components…' -ForegroundColor Cyan; ") +
                    "net stop wuauserv; net stop bits; net stop cryptsvc; " +
                    "if (Test-Path C:\\Windows\\SoftwareDistribution.old) { Remove-Item C:\\Windows\\SoftwareDistribution.old -Recurse -Force -ErrorAction SilentlyContinue }; " +
                    "if (Test-Path C:\\Windows\\System32\\catroot2.old) { Remove-Item C:\\Windows\\System32\\catroot2.old -Recurse -Force -ErrorAction SilentlyContinue }; " +
                    "Rename-Item C:\\Windows\\SoftwareDistribution SoftwareDistribution.old -Force -ErrorAction Continue; " +
                    "Rename-Item C:\\Windows\\System32\\catroot2 catroot2.old -Force -ErrorAction Continue; " +
                    "net start cryptsvc; net start bits; net start wuauserv; " +
                    Lang.T("Write-Host 'Terminé. Relance la recherche de mises à jour dans les Paramètres.' -ForegroundColor Green", "Write-Host 'Done. Search for updates again in Settings.' -ForegroundColor Green"));
                break;
            // ---- Espace disque ----
            case "diskusage":
                LaunchPs(
                    Lang.T("function Taille($p) { if (Test-Path $p) { try { '{0:N1} Go' -f ((Get-ChildItem $p -Recurse -Force -ErrorAction SilentlyContinue | Measure-Object Length -Sum).Sum / 1GB) } catch { 'inaccessible' } } else { 'absent' } }; ",
                           "function Taille($p) { if (Test-Path $p) { try { '{0:N1} GB' -f ((Get-ChildItem $p -Recurse -Force -ErrorAction SilentlyContinue | Measure-Object Length -Sum).Sum / 1GB) } catch { 'unreachable' } } else { 'absent' } }; ") +
                    Lang.T("Write-Host 'Espace occupé par les principaux postes :' -ForegroundColor Cyan; ", "Write-Host 'Space used by the main items:' -ForegroundColor Cyan; ") +
                    Lang.T("Write-Host ('  Composants Windows (WinSxS) : ' + (Taille 'C:\\Windows\\WinSxS')); ", "Write-Host ('  Windows components (WinSxS): ' + (Taille 'C:\\Windows\\WinSxS')); ") +
                    Lang.T("Write-Host ('  Windows.old (ancienne installation) : ' + (Taille 'C:\\Windows.old')); ", "Write-Host ('  Windows.old (previous installation): ' + (Taille 'C:\\Windows.old')); ") +
                    Lang.T("Write-Host ('  Temporaires utilisateur : ' + (Taille $env:TEMP)); ", "Write-Host ('  User temporary files: ' + (Taille $env:TEMP)); ") +
                    Lang.T("Write-Host ('  Temporaires Windows : ' + (Taille 'C:\\Windows\\Temp')); ", "Write-Host ('  Windows temporary files: ' + (Taille 'C:\\Windows\\Temp')); ") +
                    Lang.T("Write-Host ('  Cache Windows Update : ' + (Taille 'C:\\Windows\\SoftwareDistribution\\Download')); ", "Write-Host ('  Windows Update cache: ' + (Taille 'C:\\Windows\\SoftwareDistribution\\Download')); ") +
                    "Write-Host ''; Get-Volume | Where-Object DriveLetter | Select-Object DriveLetter, FileSystemLabel, " +
                    Lang.T("@{n='Libre (Go)';e={[math]::Round($_.SizeRemaining/1GB,1)}}, @{n='Total (Go)';e={[math]::Round($_.Size/1GB,1)}} | Format-Table -AutoSize", "@{n='Free (GB)';e={[math]::Round($_.SizeRemaining/1GB,1)}}, @{n='Total (GB)';e={[math]::Round($_.Size/1GB,1)}} | Format-Table -AutoSize"));
                break;

            case "componentcleanup":
                LaunchPs(Lang.T("Write-Host 'Analyse puis purge des composants obsolètes (patience)…' -ForegroundColor Cyan; ", "Write-Host 'Analysing then purging the obsolete components (be patient)…' -ForegroundColor Cyan; ") +
                         "DISM /Online /Cleanup-Image /AnalyzeComponentStore; " +
                         "DISM /Online /Cleanup-Image /StartComponentCleanup");
                break;

            case "temp":
                LaunchPs(
                    "$avant = (Get-PSDrive C).Free; " +
                    Lang.T("Write-Host 'Suppression des fichiers temporaires…' -ForegroundColor Cyan; ", "Write-Host 'Deleting the temporary files…' -ForegroundColor Cyan; ") +
                    "Remove-Item \"$env:TEMP\\*\" -Recurse -Force -ErrorAction SilentlyContinue; " +
                    "Remove-Item 'C:\\Windows\\Temp\\*' -Recurse -Force -ErrorAction SilentlyContinue; " +
                    "$apres = (Get-PSDrive C).Free; " +
                    Lang.T("Write-Host ('Espace libéré : {0:N2} Go' -f (($apres - $avant)/1GB)) -ForegroundColor Green", "Write-Host ('Space freed: {0:N2} GB' -f (($apres - $avant)/1GB)) -ForegroundColor Green"));
                break;

            case "cleanmgr": Open("cleanmgr.exe", "/d C:"); break;

            // ---- Démarrage / sécurité / réseau ----
            case "startup":
                LaunchPs(
                    Lang.T("Write-Host 'Programmes lancés au démarrage :' -ForegroundColor Cyan; ", "Write-Host 'Programs launched at startup:' -ForegroundColor Cyan; ") +
                    "Get-CimInstance Win32_StartupCommand | Select-Object Name, Command, Location, User | Format-Table -AutoSize -Wrap; " +
                    Lang.T("Write-Host 'Dossier Démarrage :' -ForegroundColor Cyan; ", "Write-Host 'Startup folder:' -ForegroundColor Cyan; ") +
                    "Get-ChildItem \"$env:APPDATA\\Microsoft\\Windows\\Start Menu\\Programs\\Startup\", " +
                    "'C:\\ProgramData\\Microsoft\\Windows\\Start Menu\\Programs\\Startup' -ErrorAction SilentlyContinue | Select-Object Name, LastWriteTime | Format-Table -AutoSize; " +
                    Lang.T("Write-Host 'Pour en désactiver : Gestionnaire des tâches > onglet Démarrage.' -ForegroundColor Yellow", "Write-Host 'To disable some: Task Manager > Startup tab.' -ForegroundColor Yellow"));
                break;

            case "defenderquick":
                LaunchPs(Lang.T("Write-Host 'Analyse rapide en cours…' -ForegroundColor Cyan; Start-MpScan -ScanType QuickScan; ", "Write-Host 'Quick scan running…' -ForegroundColor Cyan; Start-MpScan -ScanType QuickScan; ") +
                         Lang.T("Write-Host 'Terminé.' -ForegroundColor Green; ", "Write-Host 'Done.' -ForegroundColor Green; ") +
                         "Get-MpThreatDetection | Select-Object -Last 10 InitialDetectionTime, ThreatID, Resources | Format-Table -AutoSize");
                break;

            case "defenderfull":
                LaunchPs(Lang.T("Write-Host 'Analyse complète en cours (longue)…' -ForegroundColor Cyan; Start-MpScan -ScanType FullScan; ", "Write-Host 'Full scan running (long)…' -ForegroundColor Cyan; Start-MpScan -ScanType FullScan; ") +
                         Lang.T("Write-Host 'Terminé.' -ForegroundColor Green", "Write-Host 'Done.' -ForegroundColor Green"));
                break;

            case "defenderhistory":
                LaunchPs(
                    Lang.T("Write-Host 'État de la protection :' -ForegroundColor Cyan; ", "Write-Host 'Protection status:' -ForegroundColor Cyan; ") +
                    "Get-MpComputerStatus | Select-Object AntivirusEnabled, RealTimeProtectionEnabled, AntivirusSignatureLastUpdated, QuickScanAge, FullScanAge | Format-List; " +
                    Lang.T("Write-Host 'Menaces détectées :' -ForegroundColor Cyan; ", "Write-Host 'Threats detected:' -ForegroundColor Cyan; ") +
                    "$t = Get-MpThreatDetection -ErrorAction SilentlyContinue; " +
                    "if ($t) { $t | Sort-Object InitialDetectionTime -Descending | Select-Object -First 20 InitialDetectionTime, ThreatID, Resources | Format-Table -AutoSize -Wrap } " +
                    Lang.T("else { Write-Host '  Aucune menace détectée dans l''historique.' -ForegroundColor Green }", "else { Write-Host '  No threat found in the history.' -ForegroundColor Green }"));
                break;

            case "networkreset":
                LaunchPs("netsh winsock reset; netsh int ip reset; ipconfig /flushdns; ipconfig /registerdns; " +
                         Lang.T("Write-Host 'Réinitialisation terminée — REDÉMARRE la machine pour qu''elle prenne effet.' -ForegroundColor Yellow", "Write-Host 'Reset complete — RESTART the machine for it to take effect.' -ForegroundColor Yellow"));
                break;

            case "battery":
                LaunchPs("$out = Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'FaultTracePC\\rapport-batterie.html'; " +
                         "powercfg /batteryreport /output $out; " +
                         Lang.T("if (Test-Path $out) { Start-Process $out } else { Write-Host 'Aucune batterie détectée (poste fixe ?).' -ForegroundColor Yellow }", "if (Test-Path $out) { Start-Process $out } else { Write-Host 'No battery detected (desktop machine?).' -ForegroundColor Yellow }"));
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

    /// <summary>Action de la boîte à outils en cours de déclenchement.</summary>
    private string _currentTool = "";

    /// <summary>
    /// Lance une commande dans une fenêtre PowerShell VISIBLE qui reste ouverte.
    ///
    /// Deux réparations qui écrivent en même temps se gênent : sfc et DISM se
    /// disputent le magasin de composants, deux nettoyages se marchent dessus,
    /// une analyse antivirus complète ralentit tout le reste. Une seule action
    /// modifiante à la fois, donc — les consultations restent libres.
    /// </summary>
    private void LaunchPs(string command)
    {
        var tool = _currentTool;

        if (RunningTools.IsExclusive(tool) && RunningTools.BlockingLabel() is { } busy)
        {
            var r = MessageBox.Show(this,
                Lang.T($"Une action est déjà en cours :\n\n    {busy}\n\n", $"An action is already running:\n\n    {busy}\n\n") +
                Lang.T("Deux réparations lancées en même temps se gênent mutuellement et peuvent laisser ", "Two repairs started at the same time get in each other's way and can leave ") +
                Lang.T("le système dans un état incohérent — c'est particulièrement vrai pour sfc, DISM, ", "the system in an inconsistent state — this is especially true of sfc, DISM, ") +
                Lang.T("chkdsk et les analyses antivirus.\n\n", "chkdsk and antivirus scans.\n\n") +
                Lang.T("OUI — basculer vers la fenêtre en cours et attendre qu'elle finisse.\n", "YES — switch to the running window and wait for it to finish.\n") +
                Lang.T("NON — lancer quand même (déconseillé).\n", "NO — run it anyway (not advised).\n") +
                Lang.T("ANNULER — ne rien faire.", "CANCEL — do nothing."),
                Lang.T("FaultTracePC — une action est déjà en cours", "FaultTracePC — an action is already running"),
                MessageBoxButton.YesNoCancel, MessageBoxImage.Warning, MessageBoxResult.Yes);

            if (r == MessageBoxResult.Cancel) return;
            if (r == MessageBoxResult.Yes)
            {
                if (!RunningTools.FocusBlocking())
                    TxtStatus.Text = Lang.T("La fenêtre en cours n'a pas pu être ramenée au premier plan — cherche-la dans la barre des tâches.", "The running window could not be brought to the front — look for it in the taskbar.");
                return;
            }
        }

        try
        {
            var started = Process.Start(new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -NoExit -Command \"{command.Replace("\"", "\\\"")}\"",
                UseShellExecute = true,
            });

            if (started is not null && tool.Length > 0)
                RunningTools.Track(RunningTools.LabelOf(tool), started, RunningTools.IsExclusive(tool));

            TxtStatus.Text = RunningTools.IsExclusive(tool)
                ? Lang.T($"« {RunningTools.LabelOf(tool)} » en cours — les autres réparations attendront la fin de celle-ci.", $"“{RunningTools.LabelOf(tool)}” running — the other repairs will wait for it to finish.")
                : Lang.T("Commande lancée — suis son déroulement dans la fenêtre PowerShell.", "Command started — follow its progress in the PowerShell window.");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, Lang.T("Impossible de lancer la commande : ", "Could not start the command: ") + ex.Message, "FaultTracePC",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Open(string file, string args = "")
    {
        try { Process.Start(new ProcessStartInfo(file, args) { UseShellExecute = true }); }
        catch (Exception ex) { TxtStatus.Text = Lang.T($"Impossible d'ouvrir {file} : {ex.Message}", $"Cannot open {file}: {ex.Message}"); }
    }
}
