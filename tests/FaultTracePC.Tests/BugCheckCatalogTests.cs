using FaultTracePC.Core;
using FaultTracePC.Core.Analysis;
using Xunit;

namespace FaultTracePC.Tests;

/// <summary>
/// Catalogue des codes STOP. Le point de vigilance n'est pas la traduction
/// elle-même : c'est le fait que ce catalogue soit un « static readonly »,
/// construit une seule fois pour toute la durée du processus.
/// </summary>
[Collection("Langue")]
public class BugCheckCatalogTests
{
    [Fact]
    public void Le_catalogue_ne_fige_pas_la_langue_au_premier_acces()
    {
        // Le piège : un Lang.T() écrit DANS le dictionnaire aurait figé la langue
        // au tout premier accès au type. Le sélecteur de langue n'aurait alors plus
        // eu d'effet sur les descriptions, et ce test aurait réussi ou échoué selon
        // l'ORDRE d'exécution des tests — le pire des symptômes.
        var initial = Lang.Current;
        try
        {
            Lang.Apply(AppLanguage.French);
            var fr1 = BugCheckCatalog.Lookup(0x124)!.Description;

            Lang.Apply(AppLanguage.English);
            var en = BugCheckCatalog.Lookup(0x124)!.Description;

            Lang.Apply(AppLanguage.French);
            var fr2 = BugCheckCatalog.Lookup(0x124)!.Description;

            Assert.NotEqual(fr1, en);   // la bascule a bien eu lieu…
            Assert.Equal(fr1, fr2);     // …et elle se fait dans les deux sens
            Assert.Contains("WHEA", en);
        }
        finally
        {
            Lang.Apply(initial);
        }
    }

    [Fact]
    public void Le_nom_du_code_stop_ne_se_traduit_jamais()
    {
        // Un nom de code STOP est un identifiant Microsoft : le traduire le rendrait
        // introuvable dans la documentation et sur les forums, qui sont la raison
        // pour laquelle on l'affiche.
        var initial = Lang.Current;
        try
        {
            Lang.Apply(AppLanguage.English);
            Assert.Equal("DPC_WATCHDOG_VIOLATION", BugCheckCatalog.NameOf(0x133));
            Lang.Apply(AppLanguage.French);
            Assert.Equal("DPC_WATCHDOG_VIOLATION", BugCheckCatalog.NameOf(0x133));
            // Code absent du catalogue : même forme dans les deux langues.
            Assert.Equal("BUGCODE_0xABCDEF", BugCheckCatalog.NameOf(0xABCDEF));
        }
        finally
        {
            Lang.Apply(initial);
        }
    }
}
