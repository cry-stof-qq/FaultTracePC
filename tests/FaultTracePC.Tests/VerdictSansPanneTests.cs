using FaultTracePC.Core;
using FaultTracePC.Core.Analysis;
using Xunit;

namespace FaultTracePC.Tests;

/// <summary>
/// Point 120, lot A et son élargissement (ordre de travail décidé par l'auteur le
/// 17/11/2026). Deux constats réels :
///  - poste de l'auteur, 12/11/2026 : carte « Alerte préventive répétée (17×) :
///    Température du processeur élevée : 100 °C », critique, à côté de la carte
///    « Processeur à sa limite de température sous charge, sans ralentir », et un
///    verdict « MATÉRIELLE … » sans aucune panne ;
///  - poste d'une salle de cours, 17/11/2026 : « Cause la plus probable : SATURATION
///    DE LA MÉMOIRE » portée par la seule alerte « Mémoire virtuelle presque saturée :
///    97 % », sans plantage ni arrêt anormal.
/// </summary>
[Collection("Langue")]
public class VerdictSansPanneTests
{
    private static DiagnosticReport Rapport() => new()
    {
        GeneratedAt = new DateTime(2026, 11, 17, 8, 6, 0),
        ScanPeriodDays = 30,
        System = new SystemSnapshot { MachineName = "POSTE-TEST" },
    };

    private static Finding AlerteMemoire() => new()
    {
        Severity = Severity.Critical,
        Confidence = Confidence.High,
        Category = FaultCategory.Resources,
        Code = "commit",
        Title = "⚠ Alerte préventive répétée (8×) : Mémoire virtuelle presque saturée : 97 %",
        AlerteTempsReel = true,
    };

    private static void EnFrancais(Action action)
    {
        var initial = Lang.Current;
        try { Lang.Apply(AppLanguage.French); action(); }
        finally { Lang.Apply(initial); }
    }

    [Fact]
    public void Une_alerte_seule_sans_panne_ne_fait_pas_ecrire_de_cause()
    {
        var r = Rapport();
        r.Findings.Add(AlerteMemoire());
        EnFrancais(() => RulesEngine.ComputeVerdict(r));

        Assert.StartsWith("Pas de panne sur la période : aucun plantage, aucun arrêt anormal.", r.Verdict);
        Assert.Contains("Mémoire virtuelle presque saturée : 97 %", r.Verdict);
        Assert.DoesNotContain("Cause la plus probable", r.Verdict);
        // La catégorie reste celle de l'alerte : le script de réparation regarde la mémoire.
        Assert.Equal(FaultCategory.Resources, r.VerdictCategory);
    }

    [Fact]
    public void Avec_un_ecran_bleu_dans_la_periode_la_cause_reste_nommee()
    {
        var r = Rapport();
        r.Findings.Add(AlerteMemoire());
        r.Bsods.Add(new BsodIncident { TimeLocal = new DateTime(2026, 11, 13, 14, 45, 0) });
        EnFrancais(() => RulesEngine.ComputeVerdict(r));

        Assert.StartsWith("Cause la plus probable : SATURATION DE LA MÉMOIRE", r.Verdict);
    }

    [Fact]
    public void Une_conclusion_critique_venue_d_un_autre_chemin_garde_le_verdict_d_avant()
    {
        var r = Rapport();
        r.Findings.Add(AlerteMemoire());
        r.Findings.Add(new Finding { Severity = Severity.Critical, Category = FaultCategory.Storage, Code = "disk.test", Title = "Disque" });
        EnFrancais(() => RulesEngine.ComputeVerdict(r));

        Assert.StartsWith("Cause la plus probable", r.Verdict);
    }

    [Fact]
    public void Un_fait_confirme_par_le_journal_n_est_plus_une_simple_alerte()
    {
        var alerte = new Finding { Severity = Severity.Critical, Code = "whea", Title = "Alerte", Details = "court", AlerteTempsReel = true };
        var journal = new Finding { Severity = Severity.Warning, Code = "whea", Title = "Journal", Details = "plus long, avec le détail", Recommendation = "Faire ceci." };
        var liste = new List<Finding> { alerte, journal };

        EnFrancais(() => RulesEngine.FusionnerLesDoublons(liste));

        var gardee = Assert.Single(liste);
        Assert.False(gardee.AlerteTempsReel);
    }

    [Fact]
    public void Quand_la_frequence_a_conclu_l_alerte_de_temperature_n_est_plus_doublee()
    {
        var r = Rapport();
        for (var i = 0; i < 17; i++)
            r.Flight.Alerts.Add(new PreventiveAlert { Time = new DateTime(2026, 11, 1, 8, 0, 0).AddHours(i), RuleId = "cpu_temp", Level = "crit", Value = 100 });
        r.Findings.Add(new Finding
        {
            Severity = Severity.Critical, Category = FaultCategory.Hardware, Code = "cpu_temp", AlerteTempsReel = true,
            Title = "⚠ Alerte préventive répétée (17×) : Température du processeur élevée : 100 °C",
        });
        r.Findings.Add(new Finding
        {
            Severity = Severity.Info, Category = FaultCategory.Hardware, Code = RulesEngine.CodeProcesseurSansRalentir,
            Title = "Processeur à sa limite de température sous charge, sans ralentir — 31 min au-dessus de 85 °C",
            Details = "Au-dessus de 85 °C, le processeur a gardé 100 % de sa fréquence habituelle (4,59 GHz).",
        });

        EnFrancais(() =>
        {
            RulesEngine.NePasDoublerLAlerteDeTemperature(r);
            RulesEngine.ComputeVerdict(r);
        });

        Assert.DoesNotContain(r.Findings, f => f.Code == "cpu_temp");
        var frequence = Assert.Single(r.Findings);
        Assert.Contains("Les alertes en temps réel « Température du processeur élevée » (17) restent listées dans la boîte noire", frequence.Details);
        Assert.DoesNotContain("MATÉRIELLE", r.Verdict);
        Assert.Equal(17, r.Flight.Alerts.Count);
    }

    [Fact]
    public void Sans_conclusion_sur_la_frequence_l_alerte_de_temperature_reste()
    {
        var r = Rapport();
        r.Findings.Add(new Finding { Severity = Severity.Critical, Category = FaultCategory.Hardware, Code = "cpu_temp", AlerteTempsReel = true, Title = "Alerte" });

        EnFrancais(() => RulesEngine.NePasDoublerLAlerteDeTemperature(r));

        Assert.Single(r.Findings, f => f.Code == "cpu_temp");
    }
}
