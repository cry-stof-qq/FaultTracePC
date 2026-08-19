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
