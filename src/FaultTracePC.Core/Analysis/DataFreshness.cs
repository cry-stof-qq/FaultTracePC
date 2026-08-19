namespace FaultTracePC.Core.Analysis;

/// <summary>
/// Ce que l'analyse a réellement vu, et de quand cela date.
///
/// LE PROBLÈME QU'ELLE RÉSOUT
/// Un rapport daté d'aujourd'hui laisse croire qu'il décrit aujourd'hui. Sur une
/// machine éteinte depuis des mois, il décrit un passé lointain — et il n'y a
/// aujourd'hui aucune façon de s'en apercevoir à la lecture. « Aucun problème
/// détecté » sur trente jours dont vingt-huit machine éteinte n'est pas une
/// bonne nouvelle : c'est une absence de mesure.
///
/// Deux faits manquaient, ils sont ici : l'âge du fait le plus récent, et la
/// couverture réelle de la période.
///
/// CE QU'ON NE PRÉTEND PAS SAVOIR
/// La durée d'allumage exacte n'est connue que si la surveillance temps réel
/// tourne — elle seule écrit à intervalle régulier. Sans elle, on le dit, au
/// lieu d'avancer un chiffre déduit d'événements que Windows n'écrit pas
/// toujours.
/// </summary>
public sealed record Freshness(
    DateTime? MostRecentFact,
    /// <summary>Origine du fait le plus récent. Code stable, jamais traduit ni affiché tel quel.</summary>
    string? Source,
    int PeriodDays,
    /// <summary>Jours réellement enregistrés par la boîte noire, null si elle n'a pas tourné.</summary>
    int? DaysRecorded)
{
    /// <summary>Vrai quand l'analyse n'a trouvé aucun fait daté sur toute la période.</summary>
    public bool Empty => MostRecentFact is null;
}

public static class DataFreshness
{
    /// <summary>Relève le fait le plus récent, toutes sources confondues.</summary>
    public static Freshness Of(DiagnosticReport r)
    {
        (DateTime When, string Source)? plusRecent = null;

        void Retenir(DateTime? t, string source)
        {
            if (t is not { } d) return;
            if (plusRecent is null || d > plusRecent.Value.When) plusRecent = (d, source);
        }

        foreach (var b in r.Bsods) Retenir(b.TimeLocal, "bsod");
        foreach (var e in r.Events) Retenir(e.TimeLocal, "event");
        foreach (var rr in r.ReliabilityRecords) Retenir(rr.TimeLocal, "reliability");
        if (r.Flight.JournalFound) Retenir(r.Flight.LastSampleTime, "flight");

        return new Freshness(
            plusRecent?.When,
            plusRecent?.Source,
            r.ScanPeriodDays,
            r.Flight.JournalFound ? r.Flight.DaysCovered : null);
    }

    /// <summary>
    /// La phrase affichée, dans la langue en cours. Séparée du relevé pour être
    /// vérifiable sans construire un rapport entier.
    /// </summary>
    public static string Sentence(Freshness f, DateTime now)
    {
        var couverture = f.DaysRecorded is { } jours
            ? Lang.T($" La surveillance temps réel a enregistré {jours} jour(s) sur les {f.PeriodDays} analysés.",
                     $" Real-time monitoring recorded {jours} day(s) out of the {f.PeriodDays} analysed.")
            : Lang.T(" Sans la surveillance temps réel, la durée d'allumage réelle sur cette période n'est pas connue.",
                     " Without real-time monitoring, the actual powered-on time over this period is not known.");

        if (f.Empty)
            return Lang.T($"Aucun fait daté n'a été trouvé sur les {f.PeriodDays} jours analysés — la machine n'a peut-être pas servi, ou les journaux ont été vidés. Une absence de problème n'est alors pas une bonne nouvelle, c'est une absence de mesure.",
                          $"No dated fact was found over the {f.PeriodDays} days analysed — the machine may not have been used, or the logs were cleared. An absence of problems is then not good news, it is an absence of measurement.")
                   + couverture;

        var age = now - f.MostRecentFact!.Value;
        return Lang.T($"Fait le plus récent observé : {Age(age)}, le {Lang.Date(f.MostRecentFact.Value)}.",
                      $"Most recent fact observed: {Age(age)}, on {Lang.Date(f.MostRecentFact.Value)}.")
               + couverture;
    }

    /// <summary>
    /// Âge en langage courant. On ne descend pas sous l'heure : à cette échelle,
    /// la précision n'apporte rien à qui se demande si le rapport est frais.
    /// </summary>
    private static string Age(TimeSpan t)
    {
        if (t.TotalHours < 1) return Lang.T("il y a moins d'une heure", "less than an hour ago");
        if (t.TotalDays < 1) return Lang.T($"il y a {(int)t.TotalHours} heure(s)", $"{(int)t.TotalHours} hour(s) ago");
        if (t.TotalDays < 31) return Lang.T($"il y a {(int)t.TotalDays} jour(s)", $"{(int)t.TotalDays} day(s) ago");
        var mois = (int)(t.TotalDays / 30.44);
        return Lang.T($"il y a environ {mois} mois", $"about {mois} month(s) ago");
    }
}
