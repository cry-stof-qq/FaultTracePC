namespace FaultTracePC.Core.Analysis;

/// <summary>
/// POINT 120, LOT A (go de l'auteur le 07/11/2026) — choisir, parmi les sondes que la
/// bibliothèque de capteurs (LibreHardwareMonitor) expose pour un processeur, la
/// fréquence et la consommation à enregistrer dans la boîte noire.
///
/// POURQUOI. Sans elles, FaultTracePC ne peut pas distinguer un processeur BRIDÉ PAR LA
/// CHALEUR — il baisse sa fréquence pour refroidir, le PC « rame » — d'un processeur
/// simplement occupé. La règle qui le dira (lot C) attendra des relevés réels.
///
/// RÈGLE DE FOND : une valeur absente ou invraisemblable devient null, jamais 0 ni une
/// estimation. Les noms des sondes varient selon la marque du processeur, et ne sont
/// pas connus d'avance : le service écrit ceux qu'il a trouvés dans la ligne de début
/// de session (voir <see cref="DiagnosticCapteurs.EncoderComplet"/>).
///
/// Placé dans Core pour être testé : le service, lui, n'est pas chargé par les tests.
/// </summary>
public static class CapteursProcesseur
{
    /// <summary>Au-delà, une fréquence est une erreur de lecture (aucun processeur grand public n'atteint 7 GHz).</summary>
    public const double FrequenceMaxMHz = 7000;

    /// <summary>Au-delà, une consommation est une erreur de lecture pour un processeur de PC.</summary>
    public const double PuissanceMaxW = 500;

    /// <summary>
    /// La fréquence la plus haute parmi les sondes d'horloge du processeur, sauf celle
    /// du bus (« Bus Speed », environ 100 MHz, qui n'est pas la fréquence d'un cœur).
    /// </summary>
    public static double? Frequence(IEnumerable<(string Nom, double? Valeur)> sondesHorloge)
    {
        double? max = null;
        foreach (var (nom, valeur) in sondesHorloge)
        {
            if (nom.Contains("Bus", StringComparison.OrdinalIgnoreCase)) continue;
            if (Vraisemblable(valeur, FrequenceMaxMHz) is { } v && (max is null || v > max)) max = v;
        }
        return max is { } m ? Math.Round(m) : null;
    }

    /// <summary>
    /// La consommation du processeur entier : la sonde dont le nom contient « Package ».
    /// Les sondes par cœur ne sont pas additionnées — leur somme n'est pas la
    /// consommation du processeur (elle oublie le reste de la puce).
    /// </summary>
    public static double? Puissance(IEnumerable<(string Nom, double? Valeur)> sondesPuissance)
    {
        foreach (var (nom, valeur) in sondesPuissance)
            if (nom.Contains("Package", StringComparison.OrdinalIgnoreCase)
                && Vraisemblable(valeur, PuissanceMaxW) is { } v)
                return Math.Round(v, 1);
        return null;
    }

    private static double? Vraisemblable(double? valeur, double max) =>
        valeur is { } v && !double.IsNaN(v) && v > 0 && v <= max ? v : null;
}
