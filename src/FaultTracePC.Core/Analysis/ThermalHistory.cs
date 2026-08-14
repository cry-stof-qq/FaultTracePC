namespace FaultTracePC.Core.Analysis;

/// <summary>
/// Cumul du temps passé au-dessus d'un seuil de température, à partir du journal
/// de la boîte noire.
///
/// L'indicateur utile n'est pas « il fait 82 °C en ce moment » : une pointe brève
/// pendant une compilation ou un jeu est normale et sans conséquence. Ce qui
/// annonce les plantages thermiques, c'est la DURÉE cumulée passée trop haut —
/// « ce PC a passé 40 minutes au-dessus de 90 °C cette semaine ». Un
/// thermomètre instantané ne le dira jamais.
///
/// Deux choix de méthode, tous deux volontairement prudents :
///
///  · un intervalle entre deux relevés n'est compté au-dessus du seuil que si
///    LES DEUX extrémités le dépassent. On sous-estime donc légèrement plutôt
///    que de gonfler un chiffre qui sert à alarmer quelqu'un ;
///  · au-delà d'un écart de deux minutes entre deux relevés, l'intervalle est
///    ignoré : le service était arrêté, ou la machine éteinte. Sans cette
///    précaution, une nuit d'extinction entre deux relevés chauds compterait
///    comme huit heures de surchauffe.
/// </summary>
public sealed class ThermalHistory
{
    /// <summary>Au-delà de cet écart, on considère que la mesure a été interrompue.</summary>
    public static readonly TimeSpan MaxGap = TimeSpan.FromMinutes(2);

    private readonly string _sensor;
    private readonly double _warn;
    private readonly double _crit;

    private DateTime? _prevTime;
    private double? _prevTemp;

    private double _sum;
    private int _count;

    private DateTime? _episodeStart;
    private double _episodePeak;
    private readonly List<Episode> _episodes = new();

    public ThermalHistory(string sensor, double warnThreshold, double critThreshold)
    {
        _sensor = sensor;
        _warn = warnThreshold;
        _crit = critThreshold;
    }

    public readonly record struct Episode(DateTime Start, TimeSpan Duration, double Peak);

    public double? MaxC { get; private set; }
    public DateTime? MaxAt { get; private set; }
    public TimeSpan Observed { get; private set; }
    public TimeSpan AboveWarn { get; private set; }
    public TimeSpan AboveCrit { get; private set; }

    /// <summary>Ajoute un relevé. Les relevés doivent arriver dans l'ordre chronologique.</summary>
    public void Add(DateTime time, double? temp)
    {
        if (temp is not { } t || t <= 0 || t > 150) return; // capteur muet ou aberrant

        _sum += t;
        _count++;
        if (MaxC is null || t > MaxC) { MaxC = t; MaxAt = time; }

        if (_prevTime is { } pt && _prevTemp is { } ptemp)
        {
            var gap = time - pt;
            if (gap > TimeSpan.Zero && gap <= MaxGap)
            {
                Observed += gap;

                bool bothAboveWarn = ptemp >= _warn && t >= _warn;
                bool bothAboveCrit = ptemp >= _crit && t >= _crit;
                if (bothAboveWarn) AboveWarn += gap;
                if (bothAboveCrit) AboveCrit += gap;

                if (bothAboveWarn)
                {
                    _episodeStart ??= pt;
                    _episodePeak = Math.Max(_episodePeak, Math.Max(ptemp, t));
                }
                else CloseEpisode(pt);
            }
            else CloseEpisode(pt); // coupure de mesure : l'épisode en cours s'arrête là
        }

        _prevTime = time;
        _prevTemp = t;
    }

    private void CloseEpisode(DateTime end)
    {
        if (_episodeStart is not { } start) return;
        var d = end - start;
        if (d >= TimeSpan.FromSeconds(30)) _episodes.Add(new Episode(start, d, _episodePeak));
        _episodeStart = null;
        _episodePeak = 0;
    }

    /// <summary>À appeler après le dernier relevé, pour clore un épisode encore ouvert.</summary>
    public ThermalStats Build()
    {
        if (_prevTime is { } last) CloseEpisode(last);

        return new ThermalStats
        {
            Sensor = _sensor,
            WarnThreshold = _warn,
            CritThreshold = _crit,
            MaxC = MaxC,
            MaxAt = MaxAt,
            AverageC = _count > 0 ? Math.Round(_sum / _count, 1) : null,
            SampleCount = _count,
            Observed = Observed,
            AboveWarn = AboveWarn,
            AboveCrit = AboveCrit,
            LongestEpisodes = _episodes.OrderByDescending(e => e.Duration).Take(3)
                .Select(e => new ThermalEpisode
                {
                    Start = e.Start,
                    Minutes = Math.Round(e.Duration.TotalMinutes, 1),
                    PeakC = Math.Round(e.Peak, 1),
                }).ToList(),
        };
    }

    // ------------------------------------------------------------------

    /// <summary>Formule « 40 minutes », « 2 h 15 » — lisible sans effort.</summary>
    public static string Humanize(TimeSpan d)
    {
        if (d < TimeSpan.FromMinutes(1)) return $"{(int)d.TotalSeconds} s";
        if (d < TimeSpan.FromHours(1)) return $"{(int)d.TotalMinutes} min";
        int h = (int)d.TotalHours, m = d.Minutes;
        return m == 0 ? $"{h} h" : $"{h} h {m:00}";
    }
}
