using FaultTracePC.Core;
using FaultTracePC.Core.Analysis;
using Xunit;

namespace FaultTracePC.Tests;

/// <summary>
/// POINT 83 — les plantages regroupés en séries dans le temps.
///
/// Reproduit le profil de la machine du 24/09/2026 : trois plantages en juillet,
/// 74 jours sans rien, puis quatre en septembre — dates, heures et codes relevés
/// dans le tableau des écrans bleus de son rapport. (Première version de ce test,
/// le 10/10/2026 : jours de juillet et début de septembre reconstitués à tort,
/// la série de septembre y commençait le 21/09 au lieu du 22/09.)
/// </summary>
[Collection("Langue")]
public class SeriesDePlantagesTests
{
    private static BsodIncident P(int mois, int jour, uint code, int heure = 12, int minute = 0) =>
        new() { TimeLocal = new DateTime(2026, mois, jour, heure, minute, 0), BugCheckCode = code };

    internal static List<BsodIncident> ProfilDu24Septembre() =>
    [
        P(7, 6, 0x154, 18, 49), P(7, 6, 0x1A, 22, 45), P(7, 9, 0x1A, 22, 7),
        P(9, 22, 0x7A, 20, 35), P(9, 22, 0x154, 20, 57), P(9, 23, 0x154, 10, 34), P(9, 24, 0x154, 7, 49),
    ];

    private static Finding? Conclure(List<BsodIncident> plantages)
    {
        var initial = Lang.Current;
        try
        {
            Lang.Apply(AppLanguage.French);
            var r = new DiagnosticReport { Bsods = plantages };
            RulesEngine.AnalyzeSeriesDePlantages(r);
            return r.Findings.SingleOrDefault(f => f.Code == "plantages.series");
        }
        finally { Lang.Apply(initial); }
    }

    [Fact]
    public void Le_profil_du_24_09_donne_deux_series_separees_de_74_jours()
    {
        var series = RulesEngine.SeriesDePlantages(ProfilDu24Septembre());
        Assert.Equal(2, series.Count);
        Assert.Equal(3, series[0].Count);
        Assert.Equal(4, series[1].Count);

        var f = Conclure(ProfilDu24Septembre());
        Assert.NotNull(f);
        Assert.Equal(Severity.Info, f.Severity);
        Assert.Contains("7 plantages en 2 séries", f.Title);
        Assert.Contains("74 jours", f.Title);
        Assert.Contains("— 74 jours sans plantage —", f.Details);
        Assert.Contains("UNEXPECTED_STORE_EXCEPTION ×3", f.Details);
        Assert.Contains("le 22/09/2026", f.Recommendation);
    }

    [Fact]
    public void Une_seule_serie_ne_dit_rien()
    {
        Assert.Null(Conclure([P(9, 21, 0x154), P(9, 22, 0x154), P(9, 24, 0x7A)]));
    }

    [Fact]
    public void Moins_de_trois_plantages_ne_dit_rien()
    {
        Assert.Null(Conclure([P(7, 5, 0x1A), P(9, 21, 0x154)]));
    }

    [Fact]
    public void Treize_jours_d_ecart_restent_dans_la_meme_serie()
    {
        var series = RulesEngine.SeriesDePlantages([P(9, 1, 0x154), P(9, 14, 0x154), P(9, 28, 0x154)]);
        // 1 → 14 : 13 jours, même série ; 14 → 28 : 14 jours, nouvelle série.
        Assert.Equal(2, series.Count);
        Assert.Equal(2, series[0].Count);
    }

    [Fact]
    public void Un_plantage_sans_code_est_compte_et_nomme()
    {
        var f = Conclure([P(7, 5, 0x1A), new BsodIncident { TimeLocal = new DateTime(2026, 7, 6) }, P(9, 21, 0x154)]);
        Assert.NotNull(f);
        Assert.Contains("sans code ×1", f.Details);
    }
}
