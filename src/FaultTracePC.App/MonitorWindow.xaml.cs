using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using FaultTracePC.Core;

namespace FaultTracePC.App;

/// <summary>
/// Visualiseur du journal de la boîte noire, en quatre vues :
///  - « En direct » : les derniers relevés, rafraîchis toutes les 5 s (mode simple) ;
///  - « Courbes » : températures et mémoire tracées, avec repères d'incidents ;
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
        Loaded += (_, _) => DrawChart();
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
                Heure = Lang.ShortDateSecond(e.Time),
                Cpu = e.Kind == "s" ? Pct(e.CpuLoad) : "",
                TempCpu = e.Kind == "s" ? Temp(e.CpuTemp) : "",
                TempGpu = e.Kind == "s" ? Temp(e.GpuTemp) : "",
                Ram = e.Kind == "s" ? Pct(e.MemPct) : "",
                Info = e.Kind switch
                {
                    "e" => Lang.T($"⚠ ÉVÉNEMENT {e.EventCategory} — {e.EventMessage}", $"⚠ EVENT {e.EventCategory} — {e.EventMessage}"),
                    "b" => e.PreviousEndedAbruptly == true
                        ? Lang.T("▶ Démarrage de la surveillance — la session précédente s'était terminée BRUTALEMENT", "▶ Monitoring started — the previous session had ended ABRUPTLY")
                        : Lang.T("▶ Démarrage de la surveillance", "▶ Monitoring started"),
                    "x" => Lang.T("⏹ Arrêt propre de la surveillance", "⏹ Monitoring stopped cleanly"),
                    _ => e.TopProcesses is not null ? Lang.T($"Top : {e.TopProcesses}", $"Top: {e.TopProcesses}") : "",
                },
            }).ToList();

            var lastSample = recent.FirstOrDefault(e => e.Kind == "s");
            var active = lastSample is not null && DateTime.Now - lastSample.Time < TimeSpan.FromMinutes(2);
            TxtLiveStatus.Text = lastSample is null
                ? Lang.T("Aucun relevé trouvé — le service de surveillance est-il installé et démarré (bouton 📡) ?", "No reading found — is the monitoring service installed and started (📡 button)?")
                : Lang.T($"{(active ? "🟢 Service actif" : "🔴 Service arrêté ou en retard")} — dernier relevé : {lastSample.Time:HH:mm:ss} — rafraîchissement automatique toutes les 5 s",
                         $"{(active ? "🟢 Service running" : "🔴 Service stopped or lagging")} — last reading: {lastSample.Time:HH:mm:ss} — automatic refresh every 5 s");

            // Capteur CPU absent ? On l'explique au lieu de laisser des tirets mystérieux.
            var samples = recent.Where(e => e.Kind == "s").Take(20).ToList();
            TxtSensorNote.Visibility = samples.Count > 3 && samples.All(s => s.CpuTemp is null)
                ? Visibility.Visible : Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            TxtLiveStatus.Text = Lang.T("Erreur de lecture du journal : ", "Log read error: ") + ex.Message;
        }
    }

    private void Units_Changed(object sender, RoutedEventArgs e)
    {
        RefreshLive();
        if (LvHist.ItemsSource is not null) BtnLoadHistory_Click(sender, e);
        DrawChart();
    }

    // ------------------------------------------------------------------
    // Onglet « Courbes »
    //
    // Trois séries seulement (températures CPU/GPU et mémoire) : c'est le nombre
    // maximal qui reste distinguable pour un daltonien sur toutes les paires.
    // Couleurs issues d'une palette validée ; la légende nomme chaque série, donc
    // l'identité ne repose jamais sur la couleur seule.
    // ------------------------------------------------------------------

    private static readonly System.Windows.Media.Color SeriesCpu = System.Windows.Media.Color.FromRgb(0x2A, 0x78, 0xD6);
    private static readonly System.Windows.Media.Color SeriesGpu = System.Windows.Media.Color.FromRgb(0xEB, 0x68, 0x34);
    private static readonly System.Windows.Media.Color SeriesMem = System.Windows.Media.Color.FromRgb(0x1B, 0xAF, 0x7A);

    private List<FlightSample> _chartSamples = new();
    private List<FlightSample> _chartEvents = new();
    private double _chartMin, _chartMax;
    private DateTime _chartStart, _chartEnd;

    private void ChartRange_Changed(object sender, System.Windows.Controls.SelectionChangedEventArgs e) => DrawChart();

    private void ChartCanvas_SizeChanged(object sender, SizeChangedEventArgs e) => DrawChart();

    private int ChartHours =>
        CmbChartHours?.SelectedItem is System.Windows.Controls.ComboBoxItem it &&
        int.TryParse((string)it.Tag, out var h) ? h : 6;

    private void DrawChart()
    {
        if (ChartCanvas is null || ChartCanvas.ActualWidth < 50) return;
        ChartCanvas.Children.Clear();

        var hours = ChartHours;
        var since = DateTime.Now.AddHours(-hours);
        var all = ReadEntries(hours / 24 + 2);
        _chartSamples = all.Where(e => e.Kind == "s" && e.Time >= since).OrderBy(e => e.Time).ToList();
        _chartEvents = all.Where(e => e.Kind == "e" && e.Time >= since).ToList();

        if (_chartSamples.Count < 2)
        {
            AddText(Lang.T("Pas encore assez de relevés sur cette période — laisse la surveillance tourner quelques minutes.", "Not enough readings over this period yet — let the monitoring run for a few minutes."),
                12, 12, System.Windows.Media.Brushes.Gray);
            return;
        }

        double w = ChartCanvas.ActualWidth, h = ChartCanvas.ActualHeight;
        const double padL = 46, padR = 12, padT = 12, padB = 24;
        double plotW = Math.Max(10, w - padL - padR), plotH = Math.Max(10, h - padT - padB);

        _chartStart = _chartSamples[0].Time;
        _chartEnd = _chartSamples[^1].Time;
        var span = Math.Max(1, (_chartEnd - _chartStart).TotalSeconds);

        // Les valeurs affichées (converties si Fahrenheit) DOIVENT servir à calculer
        // l'échelle, sinon les courbes sortent du cadre en °F.
        double? CpuVal(FlightSample s) => UseFahrenheit && s.CpuTemp is { } c ? c * 9 / 5 + 32 : s.CpuTemp;
        double? GpuVal(FlightSample s) => UseFahrenheit && s.GpuTemp is { } g ? g * 9 / 5 + 32 : s.GpuTemp;

        // Échelle unique (un seul axe des ordonnées — jamais deux échelles).
        var values = _chartSamples.SelectMany(s => new[] { CpuVal(s), GpuVal(s), s.MemPct })
                                  .Where(v => v is not null).Select(v => v!.Value).ToList();
        _chartMin = 0;
        _chartMax = values.Count == 0 ? 100 : Math.Max(100, Math.Ceiling(values.Max() / 10) * 10);

        double X(DateTime t) => padL + (t - _chartStart).TotalSeconds / span * plotW;
        double Y(double v) => padT + plotH - (v - _chartMin) / (_chartMax - _chartMin) * plotH;

        // Grille discrète + graduations
        for (int i = 0; i <= 4; i++)
        {
            double v = _chartMin + (_chartMax - _chartMin) * i / 4.0;
            double y = Y(v);
            ChartCanvas.Children.Add(new System.Windows.Shapes.Line
            {
                X1 = padL, X2 = padL + plotW, Y1 = y, Y2 = y,
                Stroke = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xE8, 0xED, 0xF3)),
                StrokeThickness = 1,
            });
            AddText($"{v:0}", 6, y - 9, System.Windows.Media.Brushes.Gray, 11);
        }
        AddText(Lang.ShortDateMinute(_chartStart), padL, padT + plotH + 4, System.Windows.Media.Brushes.Gray, 11);
        var endLabel = Lang.ShortDateMinute(_chartEnd);
        AddText(endLabel, padL + plotW - endLabel.Length * 6.5, padT + plotH + 4, System.Windows.Media.Brushes.Gray, 11);

        // Repères verticaux des incidents (événements et alertes)
        foreach (var ev in _chartEvents)
        {
            bool isAlert = ev.EventCategory?.StartsWith("ALERTE#", StringComparison.Ordinal) == true;
            ChartCanvas.Children.Add(new System.Windows.Shapes.Line
            {
                X1 = X(ev.Time), X2 = X(ev.Time), Y1 = padT, Y2 = padT + plotH,
                Stroke = new SolidColorBrush(isAlert
                    ? System.Windows.Media.Color.FromRgb(0xE3, 0x49, 0x48)
                    : System.Windows.Media.Color.FromRgb(0xED, 0xA1, 0x00)),
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 3, 3 },
                Opacity = 0.8,
            });
        }

        DrawSeries(CpuVal, SeriesCpu, X, Y);
        DrawSeries(GpuVal, SeriesGpu, X, Y);
        DrawSeries(s => s.MemPct, SeriesMem, X, Y);

        TxtChartInfo.Text = Lang.T($"{_chartSamples.Count} relevés du {_chartStart:dd/MM HH:mm} au {_chartEnd:dd/MM HH:mm}", $"{_chartSamples.Count} readings from {_chartStart:MM-dd HH:mm} to {_chartEnd:MM-dd HH:mm}") +
                            (_chartEvents.Count > 0 ? Lang.T($" · {_chartEvents.Count} incident(s) en repères pointillés", $" · {_chartEvents.Count} incident(s) as dotted markers") : "") +
                            Lang.T(" · survole pour lire les valeurs.", " · hover to read the values.");
    }

    private void DrawSeries(Func<FlightSample, double?> selector,
                            System.Windows.Media.Color color,
                            Func<DateTime, double> x, Func<double, double> y)
    {
        var points = new PointCollection();
        foreach (var s in _chartSamples)
        {
            if (selector(s) is not { } v) continue;   // trou de mesure : on ne relie pas
            points.Add(new System.Windows.Point(x(s.Time), y(v)));
        }
        if (points.Count < 2) return;

        ChartCanvas.Children.Add(new System.Windows.Shapes.Polyline
        {
            Points = points,
            Stroke = new SolidColorBrush(color),
            StrokeThickness = 2,
            StrokeLineJoin = PenLineJoin.Round,
        });
    }

    private void AddText(string text, double left, double top,
                         System.Windows.Media.Brush brush, double size = 12)
    {
        var tb = new System.Windows.Controls.TextBlock { Text = text, Foreground = brush, FontSize = size };
        System.Windows.Controls.Canvas.SetLeft(tb, left);
        System.Windows.Controls.Canvas.SetTop(tb, top);
        ChartCanvas.Children.Add(tb);
    }

    // ---- Survol : valeurs sous le curseur ----

    private void ChartCanvas_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_chartSamples.Count < 2 || ChartCanvas.ActualWidth < 50) return;

        const double padL = 46, padR = 12;
        double plotW = Math.Max(10, ChartCanvas.ActualWidth - padL - padR);
        double ratio = Math.Clamp((e.GetPosition(ChartCanvas).X - padL) / plotW, 0, 1);
        var target = _chartStart.AddSeconds(ratio * (_chartEnd - _chartStart).TotalSeconds);

        var s = _chartSamples.OrderBy(x => Math.Abs((x.Time - target).TotalSeconds)).First();
        TxtChartInfo.Text = Lang.T($"{Lang.ShortDateSecond(s.Time)}  ·  CPU {Pct(s.CpuLoad)} % / {Temp(s.CpuTemp)}  ·  ", $"{Lang.ShortDateSecond(s.Time)}  ·  CPU {Pct(s.CpuLoad)}% / {Temp(s.CpuTemp)}  ·  ") +
                            Lang.T($"GPU {Temp(s.GpuTemp)}  ·  Mémoire {Pct(s.MemPct)} %  ·  Mém. virtuelle {Pct(s.CommitPct)} %", $"GPU {Temp(s.GpuTemp)}  ·  Memory {Pct(s.MemPct)}%  ·  Virtual mem. {Pct(s.CommitPct)}%") +
                            (s.TopProcesses is not null ? $"  ·  {s.TopProcesses}" : "");
    }

    private void ChartCanvas_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e) =>
        TxtChartInfo.Text = Lang.T($"{_chartSamples.Count} relevés · survole la courbe pour lire les valeurs.", $"{_chartSamples.Count} readings · hover the curve to read the values.");

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
                        Periode = Lang.ShortDateHour(g.Key) + " h",
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
            TxtHistStatus.Text = Lang.T(
                $"{entries.Count(x => x.Kind == "s")} relevés, {entries.Count(x => x.Kind == "e")} événement(s) sur {days} jour(s).",
                $"{entries.Count(x => x.Kind == "s")} readings, {entries.Count(x => x.Kind == "e")} event(s) over {days} day(s).");
        }
        catch (Exception ex)
        {
            TxtHistStatus.Text = Lang.T("Erreur : ", "Error: ") + ex.Message;
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
            TxtRawPath.Text = files.Count > 0 ? Path.GetDirectoryName(files[^1]) : Lang.T("Aucun fichier journal.", "No log file.");
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
            TxtRaw.Text = Lang.T("Erreur de lecture : ", "Read error: ") + ex.Message;
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
