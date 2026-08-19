using FaultTracePC.Core;
using FaultTracePC.Core.Analysis;
using Xunit;

namespace FaultTracePC.Tests;

/// <summary>
/// La fraîcheur des données.
///
/// Le défaut d'origine, remonté par un test sur une machine éteinte depuis des
/// mois : « aucun problème détecté » sur trente jours dont vingt-huit machine
/// éteinte se lit comme une bonne nouvelle. C'est une absence de mesure. Ces
/// tests vérifient que le rapport ne laisse plus cette confusion possible.
/// </summary>
[Collection("Langue")]
public class DataFreshnessTests
{
    private static DiagnosticReport Rapport(int periode = 30) =>
        new() { GeneratedAt = new DateTime(2026, 8, 19, 12, 0, 0), ScanPeriodDays = periode };

    [Fact]
    public void Le_fait_le_plus_recent_est_celui_de_toutes_les_sources()
    {
        var r = Rapport();
        r.Bsods.Add(new BsodIncident { TimeLocal = new DateTime(2026, 8, 1) });
        r.Events.Add(new WinEvent { TimeLocal = new DateTime(2026, 8, 15) });
        r.ReliabilityRecords.Add(new ReliabilityRecord { TimeLocal = new DateTime(2026, 8, 10) });

        var f = DataFreshness.Of(r);

        Assert.Equal(new DateTime(2026, 8, 15), f.MostRecentFact);
        Assert.Equal("event", f.Source);
    }

    [Fact]
    public void Le_journal_de_la_boite_noire_compte_comme_une_source()
    {
        var r = Rapport();
        r.Events.Add(new WinEvent { TimeLocal = new DateTime(2026, 8, 1) });
        r.Flight.JournalFound = true;
        r.Flight.LastSampleTime = new DateTime(2026, 8, 19, 11, 58, 0);

        Assert.Equal("flight", DataFreshness.Of(r).Source);
    }

    [Fact]
    public void Un_journal_absent_n_est_pas_une_couverture_de_zero_jour()
    {
        // Distinction qui compte : « la surveillance a couvert 0 jour » serait une
        // mesure, alors qu'on ne sait simplement rien.
        var r = Rapport();
        r.Flight.JournalFound = false;
        r.Flight.DaysCovered = 0;

        Assert.Null(DataFreshness.Of(r).DaysRecorded);
    }

    [Fact]
    public void Sans_aucun_fait_le_rapport_le_dit_au_lieu_de_rassurer()
    {
        var f = DataFreshness.Of(Rapport());
        Assert.True(f.Empty);

        var phrase = DataFreshness.Sentence(f, new DateTime(2026, 8, 19, 12, 0, 0));

        Assert.Contains("absence de mesure", phrase);
        Assert.Contains("30", phrase);
    }

    [Fact]
    public void Une_machine_eteinte_depuis_des_mois_se_voit_a_la_lecture()
    {
        var r = Rapport();
        r.Events.Add(new WinEvent { TimeLocal = new DateTime(2026, 2, 1) });

        var phrase = DataFreshness.Sentence(DataFreshness.Of(r), new DateTime(2026, 8, 19, 12, 0, 0));

        Assert.Contains("mois", phrase);
    }

    [Fact]
    public void La_couverture_reelle_est_annoncee_quand_elle_est_connue()
    {
        var r = Rapport();
        r.Events.Add(new WinEvent { TimeLocal = new DateTime(2026, 8, 18) });
        r.Flight.JournalFound = true;
        r.Flight.DaysCovered = 2;

        var phrase = DataFreshness.Sentence(DataFreshness.Of(r), new DateTime(2026, 8, 19, 12, 0, 0));

        Assert.Contains("2", phrase);
        Assert.Contains("30", phrase);
    }

    [Fact]
    public void Sans_surveillance_on_dit_qu_on_ne_sait_pas()
    {
        var r = Rapport();
        r.Events.Add(new WinEvent { TimeLocal = new DateTime(2026, 8, 18) });

        var phrase = DataFreshness.Sentence(DataFreshness.Of(r), new DateTime(2026, 8, 19, 12, 0, 0));

        Assert.Contains("n'est pas connue", phrase);
    }

    [Fact]
    public void La_phrase_existe_dans_les_deux_langues()
    {
        var r = Rapport();
        r.Events.Add(new WinEvent { TimeLocal = new DateTime(2026, 8, 18) });

        var initial = Lang.Current;
        try
        {
            Lang.Apply(AppLanguage.English);
            var en = DataFreshness.Sentence(DataFreshness.Of(r), new DateTime(2026, 8, 19, 12, 0, 0));
            Assert.Contains("Most recent fact", en);
            Assert.DoesNotContain("Fait le plus", en);
        }
        finally { Lang.Apply(initial); }
    }
}
