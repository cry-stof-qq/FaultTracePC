using FaultTracePC.Core;
using FaultTracePC.Core.Analysis;
using Xunit;

namespace FaultTracePC.Tests;

/// <summary>
/// Un fait, une conclusion.
///
/// Constaté sur un rapport réel du 19/08/2026 : la même erreur matérielle WHEA
/// apparaissait deux fois — « Erreurs matérielles WHEA détectées (3) » en
/// avertissement, issue du journal de Windows, et « Alerte préventive répétée
/// (2×) » en critique, issue de la surveillance. Même matériel, même dernière
/// occurrence. Le lecteur voyait deux problèmes là où il n'y en a qu'un, avec
/// deux gravités qui se contredisent.
/// </summary>
[Collection("Langue")]
public class FusionDoublonsTests
{
    private static Finding F(string code, Severity s, Confidence c, string reco = "", string details = "d") =>
        new() { Code = code, Severity = s, Confidence = c, Recommendation = reco, Details = details, Title = code };

    [Fact]
    public void Deux_conclusions_du_meme_fait_n_en_font_plus_qu_une()
    {
        var findings = new List<Finding>
        {
            F("whea", Severity.Warning, Confidence.Medium, reco: "vérifier le matériel"),
            F("whea", Severity.Critical, Confidence.High),
        };

        RulesEngine.FusionnerLesDoublons(findings);

        Assert.Single(findings);
    }

    [Fact]
    public void La_gravite_la_plus_forte_l_emporte()
    {
        // LE piège de ce code : dans Severity comme dans Confidence, la valeur la
        // plus BASSE est la plus grave. Une comparaison à l'envers dégraderait
        // silencieusement une conclusion critique en avertissement.
        var findings = new List<Finding>
        {
            F("whea", Severity.Warning, Confidence.Medium, reco: "r"),
            F("whea", Severity.Critical, Confidence.High),
        };

        RulesEngine.FusionnerLesDoublons(findings);

        Assert.Equal(Severity.Critical, findings[0].Severity);
        Assert.Equal(Confidence.High, findings[0].Confidence);
    }

    [Fact]
    public void La_conclusion_qui_porte_une_recommandation_est_conservee()
    {
        // C'est elle qui aide le lecteur : elle nomme le matériel et dit quoi faire.
        var findings = new List<Finding>
        {
            F("whea", Severity.Critical, Confidence.High, details: "détail court"),
            F("whea", Severity.Warning, Confidence.Medium, reco: "changer la RAM", details: "détail long et utile"),
        };

        RulesEngine.FusionnerLesDoublons(findings);

        Assert.Equal("changer la RAM", findings[0].Recommendation);
        Assert.Equal(Severity.Critical, findings[0].Severity);
    }

    [Fact]
    public void La_double_source_est_annoncee_au_lecteur()
    {
        var findings = new List<Finding> { F("whea", Severity.Warning, Confidence.Medium, reco: "r"), F("whea", Severity.Critical, Confidence.High) };

        RulesEngine.FusionnerLesDoublons(findings);

        Assert.Contains("deux chemins indépendants", findings[0].Details);
    }

    [Fact]
    public void Rien_de_ce_qu_a_constate_l_autre_chemin_n_est_perdu()
    {
        // Première version de ce code : la carte perdante disparaissait entièrement.
        // Vérifié sur un rapport réel, on y perdait le nombre d'occurrences relevées
        // sur la période — 3 événements, quand la carte gardée n'annonçait que 2
        // alertes — et le matériel nommé. Sous-estimer une magnitude est pire que
        // répéter une date.
        var findings = new List<Finding>
        {
            F("whea", Severity.Critical, Confidence.High, reco: "r", details: "Detecte 2 fois en direct."),
            F("whea", Severity.Warning, Confidence.Medium, details: "3 erreurs sur la periode. CPU AMD Ryzen 7."),
        };

        RulesEngine.FusionnerLesDoublons(findings);

        Assert.Single(findings);
        Assert.Contains("3 erreurs sur la periode", findings[0].Details);
        Assert.Contains("CPU AMD Ryzen 7", findings[0].Details);
        Assert.Contains("Detecte 2 fois en direct", findings[0].Details);
    }

    [Fact]
    public void Un_detail_deja_present_n_est_pas_recopie()
    {
        var findings = new List<Finding>
        {
            F("whea", Severity.Critical, Confidence.High, reco: "r", details: "Meme phrase exactement."),
            F("whea", Severity.Warning, Confidence.Medium, details: "Meme phrase exactement."),
        };

        RulesEngine.FusionnerLesDoublons(findings);

        Assert.Equal(1, findings[0].Details.Split("Meme phrase exactement").Length - 1);
    }

    [Fact]
    public void Les_conclusions_sans_identifiant_ne_sont_jamais_fusionnees()
    {
        // La grande majorité des conclusions n'ont pas de code. Les regrouper sur
        // une chaîne vide fusionnerait tout le rapport en une seule carte.
        var findings = new List<Finding>
        {
            F("", Severity.Warning, Confidence.High), F("", Severity.Info, Confidence.Low), F("", Severity.Critical, Confidence.High),
        };

        RulesEngine.FusionnerLesDoublons(findings);

        Assert.Equal(3, findings.Count);
    }

    [Fact]
    public void Un_fait_rapporte_une_seule_fois_n_est_pas_touche()
    {
        var findings = new List<Finding> { F("whea", Severity.Warning, Confidence.Medium, details: "intact") };

        RulesEngine.FusionnerLesDoublons(findings);

        Assert.Single(findings);
        Assert.Equal("intact", findings[0].Details);
    }

    [Fact]
    public void Deux_faits_differents_restent_deux_conclusions()
    {
        var findings = new List<Finding>
        {
            F("whea", Severity.Critical, Confidence.High),
            F("disk_event", Severity.Warning, Confidence.Medium),
        };

        RulesEngine.FusionnerLesDoublons(findings);

        Assert.Equal(2, findings.Count);
    }

    [Fact]
    public void L_annonce_existe_dans_les_deux_langues()
    {
        var findings = new List<Finding> { F("whea", Severity.Warning, Confidence.High, reco: "r"), F("whea", Severity.Critical, Confidence.High) };

        var initial = Lang.Current;
        try
        {
            Lang.Apply(AppLanguage.English);
            RulesEngine.FusionnerLesDoublons(findings);
            Assert.Contains("two independent paths", findings[0].Details);
            Assert.DoesNotContain("chemins", findings[0].Details);
        }
        finally { Lang.Apply(initial); }
    }
}
