using System.Diagnostics;
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

    public MainWindow()
    {
        InitializeComponent();
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
        var options = new ScanOptions { Days = days, IncludeDrivers = ChkDrivers.IsChecked == true };
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
