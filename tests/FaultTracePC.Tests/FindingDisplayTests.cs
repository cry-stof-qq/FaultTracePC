using FaultTracePC.Core;
using FaultTracePC.Core.Analysis;
using Xunit;

namespace FaultTracePC.Tests;

/// <summary>
/// Ce qui s'affiche d'emblée, et ce qui se replie.
///
/// Le risque de cette fonctionnalité n'est pas le code : c'est de cacher la
/// mauvaise chose. Ces tests verrouillent d'abord ce qui ne doit JAMAIS être
/// replié.
/// </summary>
[Collection("Langue")]
public class FindingDisplayTests
{
    private static Finding F(Severity s, string titre = "x") =>
        new() { Severity = s, Title = titre, Details = "d" };

    [Fact]
    public void Aucune_conclusion_critique_n_est_jamais_repliee()
    {
        // La règle non négociable : replier un problème grave serait exactement le
        // défaut que ce logiciel corrige ailleurs — annoncer moins qu'on ne sait.
        var findings = new List<Finding>
        {
            F(Severity.Critical, "c1"), F(Severity.Critical, "c2"), F(Severity.Critical, "c3"),
            F(Severity.Warning), F(Severity.Warning), F(Severity.Warning), F(Severity.Info),
        };

        var split = FindingDisplay.Split(findings);

        Assert.DoesNotContain(split.Repliees, f => f.Severity == Severity.Critical);
        Assert.Equal(3, split.Visibles.Count(f => f.Severity == Severity.Critical));
    }

    [Fact]
    public void Le_premier_avertissement_reste_visible()
    {
        var findings = new List<Finding>
        {
            F(Severity.Critical), F(Severity.Warning, "premier"), F(Severity.Warning, "second"), F(Severity.Info),
        };

        var split = FindingDisplay.Split(findings);

        Assert.Contains(split.Visibles, f => f.Title == "premier");
        Assert.Contains(split.Repliees, f => f.Title == "second");
    }

    [Fact]
    public void Sous_le_seuil_rien_n_est_replie()
    {
        // Replier une seule ligne fait cliquer pour découvrir une seule ligne.
        var findings = new List<Finding> { F(Severity.Critical), F(Severity.Warning), F(Severity.Info) };

        var split = FindingDisplay.Split(findings);

        Assert.Empty(split.Repliees);
        Assert.Equal(3, split.Visibles.Count);
    }

    [Fact]
    public void Un_rapport_sain_ne_replie_rien()
    {
        Assert.Empty(FindingDisplay.Split([]).Repliees);
        Assert.Empty(FindingDisplay.Split([]).Visibles);
    }

    [Fact]
    public void L_ordre_d_origine_est_conserve()
    {
        var findings = new List<Finding> { F(Severity.Critical, "a"), F(Severity.Warning, "b"), F(Severity.Info, "c") };

        var split = FindingDisplay.Split(findings);

        Assert.Equal(["a", "b", "c"], split.Visibles.Select(f => f.Title));
    }

    [Fact]
    public void Le_bouton_annonce_le_nombre_et_la_repartition()
    {
        // Masquer sans dire combien est ce que le logiciel reproche aux autres.
        var repliees = new List<Finding> { F(Severity.Warning), F(Severity.Warning), F(Severity.Info) };

        var libelle = FindingDisplay.FoldLabel(repliees);

        Assert.Contains("3", libelle);
        Assert.Contains("2", libelle);
    }

    [Fact]
    public void Le_bouton_ne_mentionne_pas_une_categorie_vide()
    {
        var libelle = FindingDisplay.FoldLabel([F(Severity.Warning), F(Severity.Warning)]);

        Assert.DoesNotContain("0", libelle);
    }

    [Fact]
    public void Le_bouton_existe_dans_les_deux_langues()
    {
        var repliees = new List<Finding> { F(Severity.Warning), F(Severity.Info) };

        var initial = Lang.Current;
        try
        {
            Lang.Apply(AppLanguage.English);
            var en = FindingDisplay.FoldLabel(repliees);
            Assert.Contains("Show the", en);
            Assert.DoesNotContain("Voir les", en);
        }
        finally { Lang.Apply(initial); }
    }
}
