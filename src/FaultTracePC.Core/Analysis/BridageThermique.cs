namespace FaultTracePC.Core.Analysis;

/// <summary>
/// POINT 120, LOT C1 (plan validé par l'auteur le 11/11/2026) — reconnaître dans le
/// journal de la boîte noire un processeur BRIDÉ PAR LA CHALEUR : il baisse sa
/// fréquence pour ne pas dépasser sa limite de température, et le poste « rame ».
///
/// POURQUOI LA FRÉQUENCE, ET PAS LA SEULE TEMPÉRATURE. Mesuré le 11/11/2026 sur les
/// deux postes de l'auteur :
///  · un portable AMD (Ryzen 7 7735U) montait jusqu'à 100,4 °C en gardant sa fréquence
///    maximale (4,4 à 4,77 GHz) : il travaillait, il ne se bridait pas ;
///  · un portable Intel (Core i5-8300H, 2018), chargé à 100 % pendant 3 minutes, restait
///    à 95–99 °C pendant que sa fréquence tombait de 3,8 à 3,3–3,5 GHz et sa
///    consommation de 52 à 33 W : lui se bridait.
/// La température seule ne distingue pas ces deux cas ; la fréquence, si.
///
/// LA RÈGLE (seuils PROVISOIRES, tirés de ces deux postes — à revoir avec le parc) :
///  · fréquence de RÉFÉRENCE du poste : le 95e centile des fréquences relevées quand le
///    processeur est encore frais (&lt; 80 °C) ET chargé (≥ 50 %) — au moins 6 relevés ;
///    à défaut, le 95e centile de tous les relevés frais — au moins 30 relevés — mais
///    la conclusion est alors moins sûre (<see cref="BilanBridage.ReferenceSousCharge"/>) :
///    un processeur tient souvent sur tous ses cœurs une fréquence un peu plus basse que
///    sur un seul ;
///  · ÉPISODE DE BRIDAGE : au moins 60 secondes de relevés CONSÉCUTIFS avec charge ≥ 80 %,
///    température ≥ 90 °C et fréquence ≤ 90 % de la référence.
///
/// Comme <see cref="ThermalHistory"/>, l'objet se remplit au fil de la lecture du
/// journal, en un seul passage ; la référence n'étant connue qu'à la fin, les relevés
/// candidats sont gardés, et seulement eux.
/// </summary>
public sealed class BridageThermique
{
    public const double ChargeMin = 80;
    public const double TemperatureMin = 90;
    public const double ChargeReferenceMin = 50;
    public const double TemperatureReferenceMax = 80;
    /// <summary>Fréquence ≤ ce rapport de la référence : le processeur est bridé.</summary>
    public const double RapportBride = 0.90;
    /// <summary>Sous ce rapport, le bridage est FORT (carte critique).</summary>
    public const double RapportFort = 0.70;
    public const int ReferenceSousChargeMin = 6;
    public const int ReferenceFroideMin = 30;
    public static readonly TimeSpan DureeMin = TimeSpan.FromSeconds(60);
    /// <summary>Au-delà de cet écart entre deux relevés, l'épisode est coupé (relevé toutes les 10 s).</summary>
    public static readonly TimeSpan EcartMax = TimeSpan.FromSeconds(25);
    /// <summary>Un arrêt brutal dans ce délai après un épisode lui est rattaché.</summary>
    public static readonly TimeSpan ArretRattache = TimeSpan.FromMinutes(2);

    /// <summary>Garde-fou mémoire : un journal de 30 jours compte au plus ~260 000 relevés.</summary>
    private const int Plafond = 300_000;

    private readonly double _seuilAlerte;
    private long _rang;
    private readonly List<double> _froidsCharges = new();
    private readonly List<double> _froids = new();
    private readonly List<(long Rang, DateTime T, double Freq, double Temp)> _chargesChauds = new();
    private readonly List<double> _chaudsFrequence = new();
    private readonly List<DateTime> _arrets = new();

    /// <param name="seuilAlerte">Seuil d'alerte de température du processeur (85 °C par défaut) :
    /// sert à dire si, au-dessus, la fréquence est restée haute.</param>
    public BridageThermique(double seuilAlerte) => _seuilAlerte = seuilAlerte;

    public void Ajouter(DateTime t, double? charge, double? temperature, double? frequenceMHz)
    {
        var rang = _rang++;
        if (frequenceMHz is not { } f || temperature is not { } c) return;

        if (c < TemperatureReferenceMax)
        {
            if (_froids.Count < Plafond) _froids.Add(f);
            if (charge >= ChargeReferenceMin && _froidsCharges.Count < Plafond) _froidsCharges.Add(f);
        }
        if (c >= _seuilAlerte && _chaudsFrequence.Count < Plafond) _chaudsFrequence.Add(f);
        if (c >= TemperatureMin && charge >= ChargeMin && _chargesChauds.Count < Plafond)
            _chargesChauds.Add((rang, t, f, c));
    }

    /// <summary>Le service s'est arrêté brutalement ; <paramref name="dernierReleve"/> est son dernier relevé.</summary>
    public void ArretBrutal(DateTime? dernierReleve)
    {
        if (dernierReleve is { } d) _arrets.Add(d);
    }

    public BilanBridage Bilan()
    {
        var bilan = new BilanBridage();
        if (_froidsCharges.Count >= ReferenceSousChargeMin)
        {
            bilan.ReferenceMHz = Centile95(_froidsCharges);
            bilan.ReferenceSousCharge = true;
        }
        else if (_froids.Count >= ReferenceFroideMin)
            bilan.ReferenceMHz = Centile95(_froids);

        bilan.RelevesChaudsAvecFrequence = _chaudsFrequence.Count;
        if (bilan.ReferenceMHz is not { } reference || reference <= 0) return bilan;

        if (_chaudsFrequence.Count > 0)
            bilan.RapportMedianAuChaud = Math.Round(Mediane(_chaudsFrequence) / reference, 3);

        var limite = RapportBride * reference;
        var courant = new List<(long Rang, DateTime T, double Freq, double Temp)>();
        void Clore()
        {
            if (courant.Count >= 2 && courant[^1].T - courant[0].T >= DureeMin)
            {
                var moyenne = courant.Average(x => x.Freq);
                var fin = courant[^1].T;
                bilan.Episodes.Add(new EpisodeBridage
                {
                    Debut = courant[0].T,
                    Fin = fin,
                    FrequenceMoyenneMHz = Math.Round(moyenne),
                    TemperatureMax = courant.Max(x => x.Temp),
                    Rapport = Math.Round(moyenne / reference, 3),
                    SuiviDArretBrutal = _arrets.Any(a => a >= fin && a - fin <= ArretRattache),
                });
            }
            courant.Clear();
        }
        foreach (var r in _chargesChauds)
        {
            var enchaine = courant.Count > 0 && r.Rang == courant[^1].Rang + 1 && r.T - courant[^1].T <= EcartMax;
            if (!enchaine) Clore();
            if (r.Freq <= limite) courant.Add(r);
            else Clore();
        }
        Clore();
        return bilan;
    }

    private static double Centile95(List<double> valeurs)
    {
        var tri = valeurs.OrderBy(v => v).ToList();
        return tri[Math.Max(0, (int)Math.Ceiling(0.95 * tri.Count) - 1)];
    }

    private static double Mediane(List<double> valeurs)
    {
        var tri = valeurs.OrderBy(v => v).ToList();
        var m = tri.Count / 2;
        return tri.Count % 2 == 1 ? tri[m] : (tri[m - 1] + tri[m]) / 2;
    }
}

/// <summary>Résultat de <see cref="BridageThermique"/> sur la période lue.</summary>
public sealed class BilanBridage
{
    /// <summary>Fréquence de référence du poste, en MHz ; null si trop peu de relevés frais.</summary>
    public double? ReferenceMHz { get; set; }

    /// <summary>true si la référence vient de relevés frais ET chargés (la plus sûre).</summary>
    public bool ReferenceSousCharge { get; set; }

    /// <summary>Nombre de relevés au-dessus du seuil d'alerte qui portent une fréquence.</summary>
    public int RelevesChaudsAvecFrequence { get; set; }

    /// <summary>
    /// Médiane, au-dessus du seuil d'alerte, du rapport fréquence / référence. Proche de 1 :
    /// le processeur garde sa fréquence quand il chauffe. Null sans référence ou sans relevé chaud.
    /// </summary>
    public double? RapportMedianAuChaud { get; set; }

    public List<EpisodeBridage> Episodes { get; set; } = new();
}

public sealed class EpisodeBridage
{
    public DateTime Debut { get; set; }
    public DateTime Fin { get; set; }
    public TimeSpan Duree => Fin - Debut;
    public double FrequenceMoyenneMHz { get; set; }
    public double TemperatureMax { get; set; }
    /// <summary>Fréquence moyenne de l'épisode rapportée à la référence (0,87 = ralenti de 13 %).</summary>
    public double Rapport { get; set; }
    public bool SuiviDArretBrutal { get; set; }
}
