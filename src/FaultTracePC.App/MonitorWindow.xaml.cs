using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using FaultTracePC.Core;

namespace FaultTracePC.App;

/// <summary>
/// Visualiseur du journal de la boîte noire, en trois vues :
///  - « En direct » : les derniers relevés, rafraîchis toutes les 5 s (mode simple) ;
///  - « Historique » : agrégation par heure sur 1 à 14 jours ;
///  - « Données brutes » : les lignes JSONL telles qu'écrites par le service (mode avancé).
/// Bascule °C/°F pour toutes les vues.
/// </summary>
public partial class MonitorWindow : Window
{
    private static string FlightDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                     "FaultTracePC", "Flight");

    private readonly DispatcherTimer _timer;
    private bool UseFahrenheit => ChkFahrenheit.IsChecked == true;

    public MonitorWindow()
    {
        InitializeComponent();
        RefreshLive();
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _timer.Tick += (_, _) => RefreshLive();
        _timer.Start();
        Closed += (_, _) => _timer.Stop();
    }

    // ------------------------------------------------------------------
    // Lecture du journal
    // ------------------------------------------------------------------

    private static IEnumerable<string> JournalFiles(int days) =>
        !Directory.Exists(FlightDir)
            ? Enumerable.Empty<string>()
            : Directory.EnumerateFiles(FlightDir, "flight_*.jsonl")
                .Where(f => File.GetLastWriteTime(f) >= DateTime.Now.AddDays(-days))
                .OrderBy(f => f);

    private static List<FlightSample> ReadEntries(int days)
    {
        var list = new List<FlightSample>();
        foreach (var file in JournalFiles(days))
        {
            try
            {
                using var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new StreamReader(fs);
                while (reader.ReadLine() is { } line)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    try
                    {
                        if (JsonSerializer.Deserialize<FlightSample>(line) is { } e)
                            list.Add(e);
                    }
                    catch { /* ligne corrompue (crash en pleine écriture) : ignorée */ }
                }
            }
            catch { /* fichier verrouillé : ignoré */ }
        }
        return list;
    }

    private string Temp(double? celsius) =>
        celsius is null ? "—"
        : UseFahrenheit ? $"{celsius * 9 / 5 + 32:0.#} °F"
        : $"{celsius:0.#} °C";

    private static string Pct(double? v) => v is null ? "—" : $"{v:0.#}";

    // ------------------------------------------------------------------
    // Onglet « En direct »
    // ------------------------------------------------------------------

    private void RefreshLive()
    {
        try
        {
            var entries = ReadEntries(2);
            var recent = entries.OrderByDescending(e => e.Time).Take(90).ToList();

            LvLive.ItemsSource = recent.Select(e => new LiveRow
            {
                Heure = e.Time.ToString("dd/MM HH:mm:ss"),
                Cpu = e.Kind == "s" ? Pct(e.CpuLoad) : "",
                TempCpu = e.Kind == "s" ? Temp(e.CpuTemp) : "",
                TempGpu = e.Kind == "s" ? Temp(e.GpuTemp) : "",
                Ram = e.Kind == "s" ? Pct(e.MemPct) : "",
                Info = e.Kind switch
                {
                    "e" => $"⚠ ÉVÉNEMENT {e.EventCategory} — {e.EventMessage}",
                    "b" => e.PreviousEndedAbruptly == true
                        ? "▶ Démarrage de la surveillance — la session précédente s'était terminée BRUTALEMENT"
                        : "▶ Démarrage de la surveillance",
                    "x" => "⏹ Arrêt propre de la surveillance",
                    _ => e.TopProcesses is not null ? $"Top : {e.TopProcesses}" : "",
                },
            }).ToList();

            var lastSample = recent.FirstOrDefault(e => e.Kind == "s");
            var active = lastSample is not null && DateTime.Now - lastSample.Time < TimeSpan.FromMinutes(2);
            TxtLiveStatus.Text = lastSample is null
                ? "Aucun relevé trouvé — le service de surveillance est-il installé et démarré (bouton 📡) ?"
                : $"{(active ? "🟢 Service actif" : "🔴 Service arrêté ou en retard")} — dernier relevé : {lastSample.Time:HH:mm:ss} — rafraîchissement automatique toutes les 5 s";

            // Capteur CPU absent ? On l'explique au lieu de laisser des tirets mystérieux.
            var samples = recent.Where(e => e.Kind == "s").Take(20).ToList();
            TxtSensorNote.Visibility = samples.Count > 3 && samples.All(s => s.CpuTemp is null)
                ? Visibility.Visible : Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            TxtLiveStatus.Text = "Erreur de lecture du journal : " + ex.Message;
        }
    }

    private void Units_Changed(object sender, RoutedEventArgs e)
    {
        RefreshLive();
        if (LvHist.ItemsSource is not null) BtnLoadHistory_Click(sender, e);
    }

    // ------------------------------------------------------------------
    // Onglet « Historique » (agrégation par heure)
    // ------------------------------------------------------------------

    private void BtnLoadHistory_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            int days = int.Parse((string)((System.Windows.Controls.ComboBoxItem)CmbHistDays.SelectedItem).Tag);
            var cutoff = DateTime.Now.AddDays(-days);
            var entries = ReadEntries(days + 1).Where(x => x.Time >= cutoff).ToList();

            var rows = entries
                .GroupBy(x => new DateTime(x.Time.Year, x.Time.Month, x.Time.Day, x.Time.Hour, 0, 0))
                .OrderByDescending(g => g.Key)
                .Select(g =>
                {
                    var s = g.Where(x => x.Kind == "s").ToList();
                    return new HistRow
                    {
                        Periode = g.Key.ToString("dd/MM HH") + " h",
                        CpuMoy = s.Count == 0 ? "—" : Pct(s.Average(x => x.CpuLoad ?? 0)),
                        CpuMax = s.Count == 0 ? "—" : Pct(s.Max(x => x.CpuLoad ?? 0)),
                        TempCpuMax = s.Count == 0 ? "—" : Temp(MaxOrNull(s, x => x.CpuTemp)),
                        TempGpuMax = s.Count == 0 ? "—" : Temp(MaxOrNull(s, x => x.GpuTemp)),
                        RamMax = s.Count == 0 ? "—" : Pct(s.Max(x => x.MemPct ?? 0)),
                        CommitMax = s.Count == 0 ? "—" : Pct(s.Max(x => x.CommitPct ?? 0)),
                        Evenements = g.Count(x => x.Kind == "e") is var n and > 0 ? $"⚠ {n}" : "0",
                    };
                })
                .ToList();

            LvHist.ItemsSource = rows;
            TxtHistStatus.Text = $"{entries.Count(x => x.Kind == "s")} relevés, {entries.Count(x => x.Kind == "e")} événement(s) sur {days} jour(s).";
        }
        catch (Exception ex)
        {
            TxtHistStatus.Text = "Erreur : " + ex.Message;
        }
    }

    private static double? MaxOrNull(IEnumerable<FlightSample> samples, Func<FlightSample, double?> selector)
    {
        var values = samples.Select(selector).Where(v => v is not null).Select(v => v!.Value).ToList();
        return values.Count == 0 ? null : values.Max();
    }

    // ------------------------------------------------------------------
    // Onglet « Données brutes »
    // ------------------------------------------------------------------

    private void BtnRawRefresh_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var files = JournalFiles(2).ToList();
            TxtRawPath.Text = files.Count > 0 ? Path.GetDirectoryName(files[^1]) : "Aucun fichier journal.";
            var lines = new List<string>();
            foreach (var f in files)
            {
                using var fs = new FileStream(f, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new StreamReader(fs);
                while (reader.ReadLine() is { } line)
                    if (!string.IsNullOrWhiteSpace(line)) lines.Add(line);
            }
            TxtRaw.Text = string.Join(Environment.NewLine, lines.TakeLast(300));
            TxtRaw.ScrollToEnd();
        }
        catch (Exception ex)
        {
            TxtRaw.Text = "Erreur de lecture : " + ex.Message;
        }
    }

    public sealed class LiveRow
    {
        public string Heure { get; set; } = "";
        public string Cpu { get; set; } = "";
        public string TempCpu { get; set; } = "";
        public string TempGpu { get; set; } = "";
        public string Ram { get; set; } = "";
        public string Info { get; set; } = "";
    }

    public sealed class HistRow
    {
        public string Periode { get; set; } = "";
        public string CpuMoy { get; set; } = "";
        public string CpuMax { get; set; } = "";
        public string TempCpuMax { get; set; } = "";
        public string TempGpuMax { get; set; } = "";
        public string RamMax { get; set; } = "";
        public string CommitMax { get; set; } = "";
        public string Evenements { get; set; } = "";
    }
}
