using FaultTracePC.Core;
using FaultTracePC.Core.Analysis;
using Xunit;

namespace FaultTracePC.Tests;

/// <summary>
/// Base de connaissances des pilotes : 59 fiches, chacune dans deux langues.
/// </summary>
[Collection("Langue")]
public class DriverKnowledgeBaseTests
{
    [Fact]
    public void Toutes_les_fiches_sont_traduites()
    {
        // Une fiche ajoutée plus tard sans texte anglais rendrait du français au
        // milieu d'un rapport anglais. Le repli est volontaire — un texte utile
        // dans la mauvaise langue vaut mieux qu'une case vide — mais il ne doit
        // pas devenir un moyen commode d'oublier la traduction.
        var manquantes = DriverKnowledgeBase.All
            .Where(kv => !kv.Value.Translated)
            .Select(kv => kv.Key)
            .ToList();

        Assert.True(manquantes.Count == 0,
            "Fiches sans texte anglais : " + string.Join(", ", manquantes));
    }

    [Fact]
    public void La_fiche_change_de_langue_a_la_lecture()
    {
        // Même piège que le catalogue des codes STOP : la table est un
        // « static readonly » construit une seule fois.
        var initial = Lang.Current;
        try
        {
            Lang.Apply(AppLanguage.French);
            var fr = DriverKnowledgeBase.Lookup("nvlddmkm.sys")!.Owner;
            Lang.Apply(AppLanguage.English);
            var en = DriverKnowledgeBase.Lookup("nvlddmkm.sys")!.Owner;
            Lang.Apply(AppLanguage.French);
            var fr2 = DriverKnowledgeBase.Lookup("nvlddmkm.sys")!.Owner;

            Assert.Equal("NVIDIA (pilote graphique)", fr);
            Assert.Equal("NVIDIA (display driver)", en);
            Assert.Equal(fr, fr2);
        }
        finally
        {
            Lang.Apply(initial);
        }
    }

    [Fact]
    public void La_reconnaissance_par_famille_est_traduite_aussi()
    {
        var initial = Lang.Current;
        try
        {
            // Rappel de la règle : le nom de fichier NE SUFFIT PAS, l'éditeur
            // inscrit dans le fichier doit concorder.
            Assert.Null(DriverKnowledgeBase.LookupFamily("amdppm.sys", "Un Éditeur Quelconque"));

            Lang.Apply(AppLanguage.English);
            var f = DriverKnowledgeBase.LookupFamily("amdppm.sys", "Advanced Micro Devices, Inc.")!;
            Assert.Equal("AMD (platform driver)", f.Owner);
            Assert.Contains("Chipset Software", f.Fix);
        }
        finally
        {
            Lang.Apply(initial);
        }
    }
}
