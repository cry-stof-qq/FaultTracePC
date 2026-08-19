using FaultTracePC.Core;
using FaultTracePC.Core.Analysis;
using Xunit;

namespace FaultTracePC.Tests;

/// <summary>
/// Les tailles affichées.
///
/// Défaut constaté sur un rapport réel, pas sur un rapport de test : la version
/// anglaise portait 147 tailles en « Ko », « Mo » et « Go ». Des unités de deux
/// lettres n'ont ni accent, ni mot outil français, ni espace avant une
/// ponctuation — les trois signaux du contrôle de traduction sont aveugles à
/// cette forme-là. Il fallait donc un test nominatif.
/// </summary>
[Collection("Langue")]
public class FormatBytesTests
{
    private static string EnLangue(AppLanguage l, Func<string> f)
    {
        var initial = Lang.Current;
        try { Lang.Apply(l); return f(); }
        finally { Lang.Apply(initial); }
    }

    [Fact]
    public void Les_unites_suivent_la_langue()
    {
        Assert.EndsWith("Mo", EnLangue(AppLanguage.French, () => RulesEngine.FormatBytes(5UL * 1024 * 1024)));
        Assert.EndsWith("MB", EnLangue(AppLanguage.English, () => RulesEngine.FormatBytes(5UL * 1024 * 1024)));
    }

    [Theory]
    [InlineData(512UL, "o", "B")]
    [InlineData(2048UL, "Ko", "KB")]
    [InlineData(3UL * 1024 * 1024 * 1024, "Go", "GB")]
    public void Chaque_palier_a_ses_deux_unites(ulong octets, string fr, string en)
    {
        Assert.EndsWith(fr, EnLangue(AppLanguage.French, () => RulesEngine.FormatBytes(octets)));
        Assert.EndsWith(en, EnLangue(AppLanguage.English, () => RulesEngine.FormatBytes(octets)));
    }

    [Fact]
    public void Le_separateur_decimal_suit_la_langue_aussi()
    {
        // Le séparateur compte autant que l'unité : un lecteur américain peut
        // prendre « 4,2 » pour un séparateur de milliers, soit dix fois la valeur.
        var taille = (ulong)(4.2 * 1024 * 1024 * 1024);

        Assert.Contains(",", EnLangue(AppLanguage.French, () => RulesEngine.FormatBytes(taille)));
        var en = EnLangue(AppLanguage.English, () => RulesEngine.FormatBytes(taille));
        Assert.Contains(".", en);
        Assert.DoesNotContain(",", en);
    }
}
