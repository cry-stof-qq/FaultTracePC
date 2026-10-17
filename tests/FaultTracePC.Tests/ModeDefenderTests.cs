using FaultTracePC.Core;
using FaultTracePC.Core.Analysis;
using Xunit;

namespace FaultTracePC.Tests;

/// <summary>
/// Constaté le 17/10/2026 dans le bac à sable Windows : « (mode Not running) »
/// dans un rapport français.
/// </summary>
[Collection("Langue")]
public class ModeDefenderTests
{
    private static T En<T>(AppLanguage langue, Func<T> f)
    {
        var initial = Lang.Current;
        try { Lang.Apply(langue); return f(); }
        finally { Lang.Apply(initial); }
    }

    [Theory]
    [InlineData("Not running", "arrêté")]
    [InlineData("Normal", "normal")]
    [InlineData("Passive", "passif")]
    [InlineData("SxS Passive Mode", "passif, à côté d'un autre antivirus")]
    [InlineData("EDR Block Mode", "blocage EDR")]
    [InlineData("Mode inconnu XYZ", "Mode inconnu XYZ")]   // jamais deviné
    public void Le_mode_est_dit_en_francais(string brut, string attendu) =>
        Assert.Equal(attendu, En(AppLanguage.French, () => ModeDefender.Nom(brut)));

    [Fact]
    public void Le_mode_est_dit_en_anglais()
    {
        Assert.Equal(" (mode: not running)", En(AppLanguage.English, () => ModeDefender.EntreParentheses("Not running")));
    }

    [Fact]
    public void Mode_non_lu_rien_n_est_ecrit() =>
        Assert.Equal("", En(AppLanguage.French, () => ModeDefender.EntreParentheses("")));

    [Fact]
    public void La_conclusion_ne_contient_plus_la_valeur_anglaise()
    {
        var p = new EtatProtection { CentreSecuriteLu = true, DefenderLu = true, DefenderTempsReel = false, DefenderMode = "Not running" };
        var details = En(AppLanguage.French, () =>
        {
            var r = new DiagnosticReport { System = new SystemSnapshot { Protection = p } };
            RulesEngine.AnalyzeProtection(r);
            return r.Findings.Single(f => f.Code == "protection.aucune").Details;
        });
        Assert.Contains("(mode arrêté)", details);
        Assert.DoesNotContain("Not running", details);
    }
}
