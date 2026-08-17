using System.Diagnostics;
using System.IO; // retiré des usings implicites par le SDK WPF (conflit Path) — requis pour File.Exists
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FaultTracePC.Core;
using FaultTracePC.Core.Report;

namespace FaultTracePC.App;

public partial class MainWindow : Window
{
    private string? _lastReportPath;
    private string? _lastRepairScriptPath;
    private bool _scanning;
    private System.Windows.Forms.NotifyIcon? _tray;
    private bool _forceClose;

    public MainWindow()
    {
        InitializeComponent();
        RefreshLanguageButton();
        Loaded += (_, _) => RefreshMonitorButton();
        // Version réellement embarquée dans l'assembly : le pied de page ne peut
        // pas mentir sur ce qui tourne.
        Loaded += (_, _) =>
        {
            TxtFoot.Text = Lang.T($"v{UpdateChecker.CurrentVersion} — diagnostic et réparation", $"v{UpdateChecker.CurrentVersion} — diagnosis and repair");
            // Vérification au démarrage : uniquement si l'utilisateur l'a demandée.
            _startupPrefLoaded = false;
            ChkUpdateStartup.IsChecked = UpdateChecker.CheckAtStartup;
            _startupPrefLoaded = true;
            if (UpdateChecker.CheckAtStartup) _ = CheckUpdateAsync(silent: true);
        };
        // Le service peut être démarré/arrêté hors de l'application : on garde
        // l'indicateur à jour sans que l'utilisateur ait à rouvrir la fenêtre.
        Activated += (_, _) => RefreshMonitorButton();
        var stateTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
        stateTimer.Tick += (_, _) => RefreshMonitorButton();
        stateTimer.Start();

        // Surveillance des alertes préventives émises par le service : notification
        // dès qu'un signe avant-coureur apparaît, sans attendre le prochain scan.
        _alertWatchStart = DateTime.Now;
        var alertTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        alertTimer.Tick += (_, _) => CheckNewAlerts();
        alertTimer.Start();
    }

    private DateTime _alertWatchStart;

    /// <summary>Affiche une bulle de notification pour chaque nouvelle alerte préventive.</summary>
    private void CheckNewAlerts()
    {
        try
        {
            var fresh = FaultTracePC.Core.Collectors.AlertLogReader.ReadSince(_alertWatchStart);
            if (fresh.Count == 0) return;
            _alertWatchStart = fresh[^1].Time;

            foreach (var a in fresh)
            {
                var prefix = a.Level == "crit" ? "⛔ " : "⚠ ";
                TxtStatus.Text = prefix + a.Title + " — " + a.Recommendation;

                EnsureTrayIcon();
                _tray?.ShowBalloonTip(10000, Lang.T("FaultTracePC — alerte préventive", "FaultTracePC — preventive alert"),
                    a.Title + "\n" + a.Recommendation,
                    a.Level == "crit"
                        ? System.Windows.Forms.ToolTipIcon.Error
                        : System.Windows.Forms.ToolTipIcon.Warning);
            }
        }
        catch { /* notification best effort */ }
    }

    /// <summary>
    /// À la fermeture : si la surveillance tourne, expliquer qu'elle CONTINUE en service,
    /// et proposer de réduire l'application à côté de l'horloge plutôt que de la fermer.
    /// </summary>
    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        // _forceClose : fermeture demandée depuis le menu de l'icône — ne pas re-demander.
        if (!_forceClose && MonitorServiceManager.GetState() == MonitorState.Running)
        {
            var choice = MessageBox.Show(this,
                Lang.T("La surveillance temps réel tourne en SERVICE Windows : elle continuera même si tu fermes ", "Real-time monitoring runs as a Windows SERVICE: it will keep going even if you close ") +
                Lang.T("FaultTracePC, et redémarrera automatiquement avec le PC.\n\n", "FaultTracePC, and will restart automatically with the PC.\n\n") +
                Lang.T("Oui : réduire FaultTracePC à côté de l'horloge (zone de notification).\n", "Yes: minimise FaultTracePC next to the clock (notification area).\n") +
                Lang.T("Non : fermer FaultTracePC — la surveillance continue en arrière-plan.\n", "No: close FaultTracePC — monitoring carries on in the background.\n") +
                Lang.T("Annuler : rester ouvert.\n\n", "Cancel: stay open.\n\n") +
                Lang.T("(Pour arrêter aussi la surveillance : bouton 📡, ou clic droit sur l'icône de notification.)", "(To stop monitoring as well: the 📡 button, or right-click the notification icon.)"),
                "FaultTracePC", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

            if (choice == MessageBoxResult.Cancel) { e.Cancel = true; return; }
            if (choice == MessageBoxResult.Yes)
            {
                e.Cancel = true;
                MinimizeToTray();
                return;
            }
        }
        if (_tray is not null)
        {
            _tray.ContextMenuStrip?.Dispose();
            _tray.Dispose();
            _tray = null;
        }
        base.OnClosing(e);
    }

    /// <summary>Réduit la fenêtre dans la zone de notification (l'app continue de tourner).</summary>
    private void MinimizeToTray()
    {
        EnsureTrayIcon();
        Hide();
        _tray!.ShowBalloonTip(3000, "FaultTracePC",
            Lang.T("Réduit à côté de l'horloge — la surveillance continue. Double-clic pour rouvrir.", "Minimised next to the clock — monitoring continues. Double-click to reopen."),
            System.Windows.Forms.ToolTipIcon.Info);
    }

    /// <summary>
    /// Crée l'icône de zone de notification si nécessaire — utilisée aussi bien pour la
    /// réduction que pour l'affichage des alertes préventives fenêtre ouverte.
    /// </summary>
    private void EnsureTrayIcon()
    {
        if (_tray is not null) return;

        System.Drawing.Icon icon;
        try { icon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!) ?? System.Drawing.SystemIcons.Application; }
        catch { icon = System.Drawing.SystemIcons.Application; }

        _tray = new System.Windows.Forms.NotifyIcon
        {
            Icon = icon,
            Text = "FaultTracePC",
            Visible = true,
        };
        _tray.DoubleClick += (_, _) => RestoreFromTray();
        _tray.BalloonTipClicked += (_, _) => RestoreFromTray();

        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("Ouvrir FaultTracePC", null, (_, _) => RestoreFromTray());
        menu.Items.Add(Lang.T("Quitter (la surveillance continue)", "Quit (monitoring carries on)"), null, (_, _) => CloseFromTray(stopService: false));
        menu.Items.Add(Lang.T("Tout arrêter (surveillance comprise) et quitter", "Stop everything (monitoring included) and quit"), null, (_, _) => CloseFromTray(stopService: true));
        _tray.ContextMenuStrip = menu;
    }

    private void CloseFromTray(bool stopService)
    {
        if (stopService)
        {
            var (_, msg) = MonitorServiceManager.StopOnly();
            System.Windows.Forms.MessageBox.Show(msg, "FaultTracePC");
        }
        _forceClose = true;
        Close();
    }

    /// <summary>Ramène la fenêtre au premier plan (l'icône reste pour les alertes).</summary>
    private void RestoreFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    /// <summary>
    /// Le bouton surveillance reflète l'état du service par sa couleur :
    /// vert = surveillance active, orange = installée mais arrêtée, gris = non installée.
    /// </summary>
    private void RefreshMonitorButton()
    {
        try
        {
            switch (MonitorServiceManager.GetState())
            {
                case MonitorState.Running:
                    BtnRealtime.Content = Lang.T("📡  Surveillance : ACTIVE", "📡  Monitoring: ACTIVE");
                    BtnRealtime.Background = new SolidColorBrush(Color.FromRgb(0x27, 0xAE, 0x60));
                    BtnRealtime.Foreground = System.Windows.Media.Brushes.White;
                    BtnRealtime.BorderThickness = new Thickness(0);
                    BtnRealtime.FontWeight = FontWeights.SemiBold;
                    break;

                case MonitorState.Stopped:
                    BtnRealtime.Content = Lang.T("📡  Surveillance : ARRÊTÉE", "📡  Monitoring: STOPPED");
                    BtnRealtime.Background = new SolidColorBrush(Color.FromRgb(0xE6, 0x7E, 0x22));
                    BtnRealtime.Foreground = System.Windows.Media.Brushes.White;
                    BtnRealtime.BorderThickness = new Thickness(0);
                    BtnRealtime.FontWeight = FontWeights.SemiBold;
                    break;

                default: // non installée : apparence neutre d'origine
                    BtnRealtime.Content = Lang.T("📡  Surveillance temps réel", "📡  Real-time monitoring");
                    BtnRealtime.ClearValue(BackgroundProperty);
                    BtnRealtime.ClearValue(ForegroundProperty);
                    BtnRealtime.ClearValue(BorderThicknessProperty);
                    BtnRealtime.FontWeight = FontWeights.Normal;
                    break;
            }
        }
        catch { /* affichage seulement */ }
    }

    /// <summary>
    /// Gère le cycle de vie du service boîte noire : installer+démarrer,
    /// redémarrer s'il est arrêté, ou (sur confirmation) arrêter+désinstaller.
    /// </summary>
    private void BtnRealtime_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            switch (MonitorServiceManager.GetState())
            {
                case MonitorState.NotInstalled:
                {
                    if (MessageBox.Show(this,
                            Lang.T("Installer la surveillance temps réel ?\n\n", "Install real-time monitoring?\n\n") +
                            Lang.T("Un service Windows léger (« FaultTracePC — Surveillance temps réel ») sera installé et démarré. ", "A lightweight Windows service (“FaultTracePC — Real-time monitoring”) will be installed and started. ") +
                            Lang.T("Il enregistre en continu températures, mémoire et événements critiques dans C:\\ProgramData\\FaultTracePC\\Flight, ", "It continuously records temperatures, memory and critical events in C:\\ProgramData\\FaultTracePC\\Flight, ") +
                            Lang.T("pour retrouver les secondes précédant un crash. Consommation : < 1 % CPU.", "so the seconds before a crash can be recovered. Usage: < 1% CPU."),
                            "FaultTracePC", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                        return;
                    var (ok, msg) = MonitorServiceManager.InstallAndStart();
                    TxtStatus.Text = msg;
                    if (!ok) MessageBox.Show(this, msg, "FaultTracePC", MessageBoxButton.OK, MessageBoxImage.Warning);
                    break;
                }
                case MonitorState.Stopped:
                {
                    var choice = MessageBox.Show(this,
                        Lang.T("Le service de surveillance est installé mais arrêté.\n\n", "The monitoring service is installed but stopped.\n\n") +
                        Lang.T("Oui : le redémarrer.\nNon : le désinstaller.\nAnnuler : ne rien faire.", "Yes: start it again.\nNo: uninstall it.\nCancel: do nothing."),
                        "FaultTracePC", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                    if (choice == MessageBoxResult.Yes)
                        TxtStatus.Text = MonitorServiceManager.Start().Message;
                    else if (choice == MessageBoxResult.No)
                        TxtStatus.Text = MonitorServiceManager.StopAndUninstall().Message;
                    break;
                }
                case MonitorState.Running:
                {
                    if (MessageBox.Show(this,
                            Lang.T("La surveillance est ACTIVE.\n\nVeux-tu l'arrêter et la désinstaller ? ", "Monitoring is ACTIVE.\n\nDo you want to stop and uninstall it? ") +
                            Lang.T("(le journal déjà enregistré est conservé pour les analyses)", "(the log already recorded is kept for analysis)"),
                            "FaultTracePC", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                        TxtStatus.Text = MonitorServiceManager.StopAndUninstall().Message;
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, Lang.T("Opération impossible : ", "Operation failed: ") + ex.Message, "FaultTracePC",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            RefreshMonitorButton();
        }
    }

    private async void BtnScan_Click(object sender, RoutedEventArgs e)
    {
        if (_scanning) return;
        _scanning = true;
        BtnScan.IsEnabled = false;
        LstFindings.Visibility = Visibility.Collapsed;
        VerdictBorder.Visibility = Visibility.Collapsed;
        PbProgress.Visibility = Visibility.Visible;
        PbProgress.Value = 0;

        var days = int.Parse((string)((ComboBoxItem)CmbDays.SelectedItem).Tag);
        var options = new ScanOptions
        {
            Days = days,
            IncludeDrivers = ChkDrivers.IsChecked == true,
            DeepDumpAnalysis = ChkDeep.IsChecked == true,
        };
        var progress = new Progress<ScanProgress>(p =>
        {
            TxtStatus.Text = p.Step;
            PbProgress.Value = p.Percent;
        });

        try
        {
            var report = await new ScanOrchestrator().RunAsync(options, progress);

            _lastReportPath = HtmlReportGenerator.WriteToDisk(report);
            _lastRepairScriptPath = report.RepairScriptPath;
            BtnOpenReport.IsEnabled = true;
            BtnPdf.IsEnabled = true;
            BtnRepair.IsEnabled = _lastRepairScriptPath is not null;
            ShowResults(report);
            OpenInBrowser(_lastReportPath);
            ProposerWinDbg(report, options);
        }
        catch (Exception ex)
        {
            TxtStatus.Text = Lang.T("Erreur pendant l'analyse : ", "Error during the analysis: ") + ex.Message;
            MessageBox.Show(this,
                Lang.T("L'analyse a échoué : ", "The analysis failed: ") + ex.Message +
                Lang.T("\n\nVérifie que l'application est bien lancée en administrateur.", "\n\nCheck that the application really is running as administrator."),
                "FaultTracePC", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            PbProgress.Visibility = Visibility.Collapsed;
            BtnScan.IsEnabled = true;
            _scanning = false;
        }
    }

    /// <summary>
    /// Après un scan qui a trouvé des dumps sans pouvoir les analyser, propose
    /// d'installer WinDbg.
    ///
    /// La question est posée ICI et pas ailleurs parce que c'est ici qu'elle a un
    /// sens : l'utilisateur vient de constater qu'aucun pilote n'est nommé. Un
    /// bouton dans un menu, il n'irait jamais le chercher.
    ///
    /// Et elle renvoie vers la boîte à outils plutôt que d'installer directement :
    /// c'est là que vivent les actions modifiant le système, avec leur garde-fou de
    /// concurrence et leur fenêtre visible. Un second chemin d'installation, posé à
    /// côté, contournerait tout ça.
    /// </summary>
    private void ProposerWinDbg(DiagnosticReport report, ScanOptions options)
    {
        if (!options.DeepDumpAnalysis) return;

        var dumpsNoyau = report.Dumps
            .Where(d => d.Kind is DumpKind.KernelMinidump or DumpKind.FullMemoryDump)
            .ToList();

        // Aucun dump : rien à analyser, donc rien à proposer.
        // Au moins un dump analysé en profondeur : l'outil est présent, rien à faire.
        if (dumpsNoyau.Count == 0 || dumpsNoyau.Any(d => d.DeepAnalyzed)) return;

        var choix = MessageBox.Show(this,
            Lang.T($"{dumpsNoyau.Count} fichier(s) d'incident ont été trouvés, mais le pilote fautif n'a pas pu être nommé : ", $"{dumpsNoyau.Count} crash file(s) were found, but the faulting driver could not be named: ")
            + Lang.T("les outils de débogage de Microsoft ne sont pas installés sur cette machine.\n\n", "the Microsoft debugging tools are not installed on this machine.\n\n")
            + Lang.T("Sans eux, le code d'arrêt est lu, mais le coupable reste souvent anonyme — c'est la différence ", "Without them the stop code is read, but the culprit often stays anonymous — that is the difference ")
            + Lang.T("entre « la machine a planté » et « c'est ce pilote-là ».\n\n", "between “the machine crashed” and “it is that driver”.\n\n")
            + Lang.T("Ouvrir la boîte à outils pour les installer ?", "Open the toolbox to install them?"),
            Lang.T("Analyse incomplète", "Incomplete analysis"), MessageBoxButton.YesNo, MessageBoxImage.Information);

        if (choix == MessageBoxResult.Yes) BtnToolbox_Click(this, new RoutedEventArgs());
    }

    private void ShowResults(DiagnosticReport report)
    {
        TxtStatus.Text = Lang.T($"Analyse terminée : {report.Bsods.Count} BSOD, {report.Events.Count} événements significatifs, ", $"Analysis complete: {report.Bsods.Count} BSOD, {report.Events.Count} significant events, ") +
                         Lang.T($"{report.Dumps.Count} dumps, {report.Findings.Count} conclusion(s). Rapport ouvert dans le navigateur.", $"{report.Dumps.Count} dumps, {report.Findings.Count} finding(s). Report opened in the browser.") +
                         (report.RepairScriptPath is not null
                             ? Lang.T($"\nScript de réparation généré : {report.RepairScriptPath}", $"\nRepair script generated: {report.RepairScriptPath}")
                             : "");

        TxtVerdict.Text = report.Verdict;
        var hasCritical = report.Findings.Any(f => f.Severity == Severity.Critical);
        var hasWarning = report.Findings.Any(f => f.Severity == Severity.Warning);
        VerdictBorder.Background = new SolidColorBrush(hasCritical
            ? Color.FromRgb(0xFD, 0xEC, 0xEA)
            : hasWarning ? Color.FromRgb(0xFE, 0xF5, 0xE7) : Color.FromRgb(0xEA, 0xFA, 0xF1));
        VerdictBorder.BorderBrush = new SolidColorBrush(hasCritical
            ? Color.FromRgb(0xE7, 0x4C, 0x3C)
            : hasWarning ? Color.FromRgb(0xE6, 0x7E, 0x22) : Color.FromRgb(0x27, 0xAE, 0x60));
        VerdictBorder.Visibility = Visibility.Visible;

        LstFindings.ItemsSource = report.Findings.Select(f => new FindingVm(f)).ToList();
        LstFindings.Visibility = Visibility.Visible;

        // Seulement le NOM du fichier : le chemin complet fait 60 caractères, pousse
        // tout le pied de fenêtre vers la droite et finissait par passer sous les
        // boutons. Le chemin entier reste accessible en infobulle, et le bouton
        // « Ouvrir le dernier rapport » évite d'avoir à le lire.
        TxtFoot.Text = _lastReportPath is null ? "" : Lang.T($"Rapport : {Path.GetFileName(_lastReportPath)}", $"Report: {Path.GetFileName(_lastReportPath)}");
        TxtFoot.ToolTip = _lastReportPath;
    }

    /// <summary>Ouvre le visualiseur du journal de la boîte noire.</summary>
    private void BtnViewer_Click(object sender, RoutedEventArgs e)
    {
        var flightDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "FaultTracePC", "Flight");
        if (!Directory.Exists(flightDir) || !Directory.EnumerateFiles(flightDir, "flight_*.jsonl").Any())
        {
            MessageBox.Show(this,
                Lang.T("Aucun journal de surveillance sur cette machine.\n\n", "No monitoring log on this machine.\n\n") +
                Lang.T("Installe d'abord la surveillance temps réel (bouton « 📡 »), attends quelques relevés (10 s chacun), puis rouvre ce visualiseur.", "Install real-time monitoring first (the “📡” button), wait for a few readings (10 s each), then reopen this viewer."),
                "FaultTracePC", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        new MonitorWindow { Owner = this }.Show();
    }

    private void BtnNetwork_Click(object sender, RoutedEventArgs e) =>
        new RemoteConfigWindow { Owner = this }.ShowDialog();

    private void BtnPark_Click(object sender, RoutedEventArgs e) =>
        new ParkWindow { Owner = this }.Show();

    /// <summary>Assistant guidé — pour qui ne sait pas ce qu'est un pilote.</summary>
    private void BtnGuided_Click(object sender, RoutedEventArgs e) =>
        new GuidedRepairWindow { Owner = this }.Show();

    private void BtnToolbox_Click(object sender, RoutedEventArgs e) =>
        new RepairToolboxWindow { Owner = this }.Show();

    // ==================================================================
    // Mise à jour du logiciel
    // ==================================================================

    private void BtnUpdate_Click(object sender, RoutedEventArgs e) => _ = CheckUpdateAsync(silent: false);

    /// <summary>
    /// « ? » — version installée et informations utiles au dépannage.
    ///
    /// La version n'était visible nulle part dans l'interface : impossible de
    /// répondre à « tu es en quelle version ? » sans passer par les propriétés du
    /// fichier. Elle est lue dans l'assembly, jamais codée en dur — un numéro écrit
    /// à la main finit toujours par mentir sur ce qui tourne réellement.
    /// </summary>
    // ==================================================================
    // Sélecteur de langue
    // ==================================================================

    /// <summary>
    /// Remet le bouton et les coches en accord avec la réalité : le libellé porte
    /// la langue ACTIVE, la coche la PRÉFÉRENCE enregistrée. Les deux se séparent
    /// dès qu'un changement attend un redémarrage.
    /// </summary>
    private void RefreshLanguageButton()
    {
        BtnLang.Content = Lang.IsFrench ? "FR" : "EN";
        var pref = Lang.Preference;
        MiLangFr.IsChecked = pref == AppLanguage.French;
        MiLangEn.IsChecked = pref == AppLanguage.English;
        MiLangAuto.IsChecked = pref is null;
    }

    private void BtnLang_Click(object sender, RoutedEventArgs e)
    {
        if (BtnLang.ContextMenu is not { } menu) return;
        RefreshLanguageButton();
        menu.PlacementTarget = BtnLang;
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    private void MiLang_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not string code) return;

        AppLanguage? choix = code switch
        {
            "fr" => AppLanguage.French,
            "en" => AppLanguage.English,
            _ => null,
        };

        Lang.Preference = choix;

        // Si la langue effective ne bouge pas — « automatique » choisi sur un
        // Windows déjà dans cette langue — il n'y a rien à redémarrer.
        var cible = Lang.Effective(choix);
        RefreshLanguageButton();

        if (cible == Lang.Current)
        {
            TxtStatus.Text = Lang.T("Langue enregistrée.", "Language saved.");
            return;
        }

        // Les libellés du XAML sont résolus à la CONSTRUCTION de la fenêtre :
        // seule une relance les reconstruit. Écrire « changement appliqué » sans
        // relancer laisserait une interface à moitié traduite.
        // La question se lit dans la langue CIBLE : c'est celle que l'utilisateur
        // vient de choisir. Lang.T répondrait encore dans l'ancienne.
        var titre = Lang.T(cible, "Langue", "Language");
        var question = Lang.T(cible,
            "Le changement de langue prend effet au redémarrage de FaultTracePC.\n\nRedémarrer maintenant ?",
            "The language change takes effect when FaultTracePC restarts.\n\nRestart now?");

        if (_scanning)
            question = Lang.T(cible,
                "Une analyse est en cours : redémarrer maintenant la perdrait.\n\n",
                "An analysis is running: restarting now would lose it.\n\n") + question;

        if (MessageBox.Show(this, question, titre,
                MessageBoxButton.YesNo, MessageBoxImage.Question,
                _scanning ? MessageBoxResult.No : MessageBoxResult.Yes) != MessageBoxResult.Yes)
        {
            TxtStatus.Text = Lang.T("Langue enregistrée : prise en compte au prochain démarrage.",
                                    "Language saved: it will apply at the next start.");
            return;
        }

        RestartApplication(cible);
    }

    /// <summary>
    /// Relance le processus. L'argument « --lang » est passé explicitement : la
    /// préférence vient d'être écrite, mais si le disque l'a refusée (profil en
    /// lecture seule) l'argument reste, lui, sans effet de bord.
    /// </summary>
    private void RestartApplication(AppLanguage cible)
    {
        var exe = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exe))
        {
            MessageBox.Show(this,
                Lang.T("Impossible de retrouver l'exécutable : ferme et rouvre FaultTracePC à la main.",
                       "Cannot locate the executable: close and reopen FaultTracePC manually."),
                "FaultTracePC", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(exe, "--lang " + Lang.Code(cible)) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this,
                Lang.T("Le redémarrage a échoué : ", "The restart failed: ") + ex.Message,
                "FaultTracePC", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _forceClose = true;
        Application.Current.Shutdown();
    }

    private void BtnAbout_Click(object sender, RoutedEventArgs e)
    {
        var exe = Environment.ProcessPath ?? Lang.T("(chemin inconnu)", "(unknown path)");

        string service;
        try
        {
            service = MonitorServiceManager.GetState() switch
            {
                MonitorState.Running => Lang.T("installé et en cours d'exécution", "installed and running"),
                MonitorState.Stopped => Lang.T("installé mais arrêté", "installed but stopped"),
                MonitorState.NotInstalled => Lang.T("non installé", "not installed"),
                _ => Lang.T("exécutable introuvable", "executable not found"),
            };
        }
        catch { service = Lang.T("état indéterminé", "state undetermined"); }

        bool admin;
        try
        {
            using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            admin = new System.Security.Principal.WindowsPrincipal(identity)
                .IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        }
        catch { admin = false; }

        var docs = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "FaultTracePC");

        var texte =
            $"FaultTracePC {UpdateChecker.CurrentVersion.ToString(3)}\n" +
            Lang.T("Licence MIT — sans aucune garantie.\n\n", "MIT licence — with no warranty whatsoever.\n\n") +
            Lang.T($"Droits administrateur : {(admin ? "oui" : "NON — les dumps et les journaux complets seront inaccessibles")}\n",
                   $"Administrator rights: {(admin ? "yes" : "NO — dumps and full logs will be out of reach")}\n") +
            Lang.T($"Surveillance temps réel : {service}\n", $"Real-time monitoring: {service}\n") +
            Lang.T($"Windows : {Environment.OSVersion.VersionString} ({(Environment.Is64BitOperatingSystem ? "64 bits" : "32 bits")})\n\n",
                   $"Windows: {Environment.OSVersion.VersionString} ({(Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit")})\n\n") +
            Lang.T($"Exécutable : {exe}\n", $"Executable: {exe}\n") +
            Lang.T($"Rapports : {docs}\n\n", $"Reports: {docs}\n\n") +
            Lang.T("Langue de l'interface et des rapports : français (option --lang fr|en|auto).\n\n",
                   "Interface and report language: English (option --lang fr|en|auto).\n\n") +
            Lang.T("Ouvrir la page des versions pour comparer avec la dernière publiée ?", "Open the releases page to compare with the latest published one?");

        var choix = MessageBox.Show(this, texte, Lang.T("À propos de FaultTracePC", "About FaultTracePC"),
            MessageBoxButton.YesNo, MessageBoxImage.Information);

        if (choix == MessageBoxResult.Yes)
        {
            try
            {
                Process.Start(new ProcessStartInfo(
                    "https://github.com/cry-stof-qq/FaultTracePC/releases/latest") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, Lang.T("Impossible d'ouvrir le navigateur : ", "Could not open the browser: ") + ex.Message,
                    "FaultTracePC", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }

    private bool _startupPrefLoaded;

    private void ChkUpdateStartup_Changed(object sender, RoutedEventArgs e)
    {
        // Ignoré tant que la case n'a pas été initialisée depuis la préférence
        // enregistrée, sinon le simple chargement de la fenêtre l'écraserait.
        if (!_startupPrefLoaded) return;
        UpdateChecker.CheckAtStartup = ChkUpdateStartup.IsChecked == true;
    }

    private bool _checkingUpdate;

    /// <summary>
    /// Interroge GitHub. En mode « silencieux » (démarrage), on ne dérange
    /// l'utilisateur que si une version plus récente existe réellement.
    /// Aucun téléchargement, aucune installation : FaultTracePC informe, l'humain décide.
    /// </summary>
    private async Task CheckUpdateAsync(bool silent)
    {
        if (_checkingUpdate) return;
        _checkingUpdate = true;
        if (!silent) { BtnUpdate.IsEnabled = false; BtnUpdate.Content = Lang.T("🔄 Vérification…", "🔄 Checking…"); }

        try
        {
            var info = await UpdateChecker.CheckAsync();

            if (info.UpdateAvailable)
            {
                TxtUpdate.Text = $"⬆ Version {info.Latest} disponible";
                TxtUpdate.Visibility = Visibility.Visible;

                var sb = new System.Text.StringBuilder();
                sb.AppendLine(Lang.T("Une nouvelle version de FaultTracePC est disponible.", "A new version of FaultTracePC is available."));
                sb.AppendLine();
                sb.AppendLine(Lang.T($"  Installée : {info.Current}", $"  Installed: {info.Current}"));
                sb.AppendLine(Lang.T($"  Publiée   : {info.Latest}", $"  Published: {info.Latest}") +
                    (info.PublishedAt is { } d ? Lang.T($" (le {d.LocalDateTime:dd/MM/yyyy})", $" ({d.LocalDateTime:yyyy-MM-dd})") : ""));
                if (info.Assets.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine(Lang.T("Fichiers publiés :", "Files published:"));
                    foreach (var (name, bytes) in info.Assets)
                        sb.AppendLine($"  • {name}" + (bytes > 0 ? $" ({bytes / 1024.0 / 1024:0.#} Mo)" : ""));
                }
                if (!string.IsNullOrWhiteSpace(info.ReleaseNotes))
                {
                    sb.AppendLine();
                    sb.AppendLine(Lang.T("Nouveautés :", "What's new:"));
                    var notes = info.ReleaseNotes.Length > 1200 ? info.ReleaseNotes[..1200] + "…" : info.ReleaseNotes;
                    sb.AppendLine(notes);
                }
                sb.AppendLine();
                sb.AppendLine(Lang.T("FaultTracePC ne télécharge et n'installe rien tout seul.", "FaultTracePC downloads and installs nothing by itself."));
                sb.AppendLine(Lang.T("Ouvrir la page de téléchargement dans le navigateur ?", "Open the download page in the browser?"));

                if (MessageBox.Show(this, sb.ToString(), Lang.T("FaultTracePC — mise à jour disponible", "FaultTracePC — update available"),
                        MessageBoxButton.YesNo, MessageBoxImage.Information, MessageBoxResult.Yes) == MessageBoxResult.Yes)
                    UpdateChecker.OpenDownloadPage(info.DownloadPage);
            }
            else
            {
                TxtUpdate.Visibility = Visibility.Collapsed;
                if (!silent)
                {
                    var extra = info.Succeeded
                        ? Lang.T("\n\nRien à faire.", "\n\nNothing to do.")
                        : Lang.T("\n\nCe n'est pas une anomalie sur un poste sans accès Internet : le mode parc de FaultTracePC fonctionne justement en réseau local fermé.", "\n\nThis is not a fault on a machine without Internet access: the fleet mode of FaultTracePC is designed to work on a closed network.");
                    MessageBox.Show(this, info.Summary + extra, Lang.T("FaultTracePC — mise à jour", "FaultTracePC — update"),
                        MessageBoxButton.OK,
                        info.Succeeded ? MessageBoxImage.Information : MessageBoxImage.Warning);
                }
            }
        }
        finally
        {
            _checkingUpdate = false;
            BtnUpdate.IsEnabled = true;
            BtnUpdate.Content = Lang.T("🔄 Vérifier les mises à jour", "🔄 Check for updates");
        }
    }

    /// <summary>
    /// Export PDF — uniquement quand l'utilisateur le demande. Aucun PDF n'est
    /// produit à l'issue d'une analyse : générer des fichiers que personne n'a
    /// réclamés encombre le dossier Documents et fait douter de ce que le
    /// logiciel fait d'autre sans le dire.
    /// </summary>
    private async void BtnPdf_Click(object sender, RoutedEventArgs e)
    {
        if (_lastReportPath is null || !File.Exists(_lastReportPath)) return;

        BtnPdf.IsEnabled = false;
        var previous = TxtStatus.Text;
        TxtStatus.Text = Lang.T("Création du PDF… (quelques secondes)", "Creating the PDF… (a few seconds)");

        var result = await Task.Run(() => PdfExporter.Export(_lastReportPath));

        BtnPdf.IsEnabled = true;
        if (result.Ok && result.PdfPath is not null)
        {
            TxtStatus.Text = Lang.T("PDF créé : ", "PDF created: ") + Path.GetFileName(result.PdfPath);
            if (MessageBox.Show(this,
                    Lang.T($"PDF créé :\n\n{result.PdfPath}\n\nL'ouvrir maintenant ?", $"PDF created:\n\n{result.PdfPath}\n\nOpen it now?"),
                    "FaultTracePC", MessageBoxButton.YesNo, MessageBoxImage.Information,
                    MessageBoxResult.Yes) == MessageBoxResult.Yes)
                OpenInBrowser(result.PdfPath);
        }
        else
        {
            TxtStatus.Text = previous;
            MessageBox.Show(this, Lang.T("Le PDF n'a pas pu être créé.\n\n", "The PDF could not be created.\n\n") + result.Error,
                "FaultTracePC", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void BtnOpenReport_Click(object sender, RoutedEventArgs e)
    {
        if (_lastReportPath is not null) OpenInBrowser(_lastReportPath);
    }

    /// <summary>
    /// Lance le script de réparation dans une fenêtre PowerShell.
    /// FaultTracePC tourne déjà en administrateur : la fenêtre héritera de l'élévation,
    /// et -ExecutionPolicy Bypass ne s'applique qu'à cette exécution (aucun réglage modifié).
    /// </summary>
    private void BtnRepair_Click(object sender, RoutedEventArgs e)
    {
        if (_lastRepairScriptPath is null || !File.Exists(_lastRepairScriptPath))
        {
            MessageBox.Show(this, Lang.T("Aucun script de réparation disponible — relance d'abord un scan.", "No repair script available — run a scan first."),
                "FaultTracePC", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{_lastRepairScriptPath}\"",
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, Lang.T($"Impossible de lancer le script ({ex.Message}).\nChemin : {_lastRepairScriptPath}", $"Could not start the script ({ex.Message}).\nPath: {_lastRepairScriptPath}"),
                "FaultTracePC", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OpenInBrowser(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, Lang.T($"Impossible d'ouvrir le rapport automatiquement ({ex.Message}).\nChemin : {path}", $"Could not open the report automatically ({ex.Message}).\nPath: {path}"),
                "FaultTracePC", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}

/// <summary>Modèle d'affichage d'une conclusion (public : requis pour le binding WPF).</summary>
public sealed class FindingVm
{
    public FindingVm(Finding f)
    {
        Title = f.Title;
        Details = f.Details;
        (BadgeText, BadgeBrush) = f.Severity switch
        {
            Severity.Critical => ("CRITIQUE", new SolidColorBrush(Color.FromRgb(0xE7, 0x4C, 0x3C))),
            Severity.Warning => ("AVERTISSEMENT", new SolidColorBrush(Color.FromRgb(0xE6, 0x7E, 0x22))),
            _ => ("INFO", new SolidColorBrush(Color.FromRgb(0x29, 0x80, 0xB9))),
        };
    }

    public string Title { get; }
    public string Details { get; }
    public string BadgeText { get; }
    public Brush BadgeBrush { get; }
}
