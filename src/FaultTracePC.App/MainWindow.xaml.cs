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
        Loaded += (_, _) => RefreshMonitorButton();
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
                _tray?.ShowBalloonTip(10000, "FaultTracePC — alerte préventive",
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
                "La surveillance temps réel tourne en SERVICE Windows : elle continuera même si tu fermes " +
                "FaultTracePC, et redémarrera automatiquement avec le PC.\n\n" +
                "Oui : réduire FaultTracePC à côté de l'horloge (zone de notification).\n" +
                "Non : fermer FaultTracePC — la surveillance continue en arrière-plan.\n" +
                "Annuler : rester ouvert.\n\n" +
                "(Pour arrêter aussi la surveillance : bouton 📡, ou clic droit sur l'icône de notification.)",
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
            "Réduit à côté de l'horloge — la surveillance continue. Double-clic pour rouvrir.",
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
        menu.Items.Add("Quitter (la surveillance continue)", null, (_, _) => CloseFromTray(stopService: false));
        menu.Items.Add("Tout arrêter (surveillance comprise) et quitter", null, (_, _) => CloseFromTray(stopService: true));
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
                    BtnRealtime.Content = "📡  Surveillance : ACTIVE";
                    BtnRealtime.Background = new SolidColorBrush(Color.FromRgb(0x27, 0xAE, 0x60));
                    BtnRealtime.Foreground = System.Windows.Media.Brushes.White;
                    BtnRealtime.BorderThickness = new Thickness(0);
                    BtnRealtime.FontWeight = FontWeights.SemiBold;
                    break;

                case MonitorState.Stopped:
                    BtnRealtime.Content = "📡  Surveillance : ARRÊTÉE";
                    BtnRealtime.Background = new SolidColorBrush(Color.FromRgb(0xE6, 0x7E, 0x22));
                    BtnRealtime.Foreground = System.Windows.Media.Brushes.White;
                    BtnRealtime.BorderThickness = new Thickness(0);
                    BtnRealtime.FontWeight = FontWeights.SemiBold;
                    break;

                default: // non installée : apparence neutre d'origine
                    BtnRealtime.Content = "📡  Surveillance temps réel";
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
                            "Installer la surveillance temps réel ?\n\n" +
                            "Un service Windows léger (« FaultTracePC — Surveillance temps réel ») sera installé et démarré. " +
                            "Il enregistre en continu températures, mémoire et événements critiques dans C:\\ProgramData\\FaultTracePC\\Flight, " +
                            "pour retrouver les secondes précédant un crash. Consommation : < 1 % CPU.",
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
                        "Le service de surveillance est installé mais arrêté.\n\n" +
                        "Oui : le redémarrer.\nNon : le désinstaller.\nAnnuler : ne rien faire.",
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
                            "La surveillance est ACTIVE.\n\nVeux-tu l'arrêter et la désinstaller ? " +
                            "(le journal déjà enregistré est conservé pour les analyses)",
                            "FaultTracePC", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                        TxtStatus.Text = MonitorServiceManager.StopAndUninstall().Message;
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Opération impossible : " + ex.Message, "FaultTracePC",
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
            BtnRepair.IsEnabled = _lastRepairScriptPath is not null;
            ShowResults(report);
            OpenInBrowser(_lastReportPath);
        }
        catch (Exception ex)
        {
            TxtStatus.Text = "Erreur pendant l'analyse : " + ex.Message;
            MessageBox.Show(this,
                "L'analyse a échoué : " + ex.Message +
                "\n\nVérifie que l'application est bien lancée en administrateur.",
                "FaultTracePC", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            PbProgress.Visibility = Visibility.Collapsed;
            BtnScan.IsEnabled = true;
            _scanning = false;
        }
    }

    private void ShowResults(DiagnosticReport report)
    {
        TxtStatus.Text = $"Analyse terminée : {report.Bsods.Count} BSOD, {report.Events.Count} événements significatifs, " +
                         $"{report.Dumps.Count} dumps, {report.Findings.Count} conclusion(s). Rapport ouvert dans le navigateur." +
                         (report.RepairScriptPath is not null
                             ? $"\nScript de réparation généré : {report.RepairScriptPath}"
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

        TxtFoot.Text = $"Rapport : {_lastReportPath}";
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
                "Aucun journal de surveillance sur cette machine.\n\n" +
                "Installe d'abord la surveillance temps réel (bouton « 📡 »), attends quelques relevés (10 s chacun), puis rouvre ce visualiseur.",
                "FaultTracePC", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        new MonitorWindow { Owner = this }.Show();
    }

    private void BtnNetwork_Click(object sender, RoutedEventArgs e) =>
        new RemoteConfigWindow { Owner = this }.ShowDialog();

    private void BtnPark_Click(object sender, RoutedEventArgs e) =>
        new ParkWindow { Owner = this }.Show();

    private void BtnToolbox_Click(object sender, RoutedEventArgs e) =>
        new RepairToolboxWindow { Owner = this }.Show();

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
            MessageBox.Show(this, "Aucun script de réparation disponible — relance d'abord un scan.",
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
            MessageBox.Show(this, $"Impossible de lancer le script ({ex.Message}).\nChemin : {_lastRepairScriptPath}",
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
            MessageBox.Show(this, $"Impossible d'ouvrir le rapport automatiquement ({ex.Message}).\nChemin : {path}",
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
