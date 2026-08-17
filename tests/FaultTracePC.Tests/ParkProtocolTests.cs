using FaultTracePC.Core;
using Xunit;

namespace FaultTracePC.Tests;

/// <summary>
/// Protocole de parc : ce qui circule entre un poste et la console.
/// Le poste transmet un CODE, la console écrit la phrase — dans la langue de
/// l'administrateur, qui n'est pas forcément celle du poste.
/// </summary>
[Collection("Langue")]
public class ParkProtocolTests
{
    [Fact]
    public void Poste_a_jour_transmet_un_code_et_des_compteurs()
    {
        var r = ParkProtocol.ReadScanResponse("""
            {"ok":true,"report":"Diagnostic_PC_2026-08-17_0930.html","verdict":"3 problèmes critiques détectés.",
             "level":"crit","critical":3,"warnings":5,"findings":8}
            """);

        Assert.True(r.Ok);
        Assert.Equal(ScanLevel.Critical, r.Level);
        Assert.Equal(3, r.Critical);
        Assert.Equal(5, r.Warnings);
        Assert.Equal("Diagnostic_PC_2026-08-17_0930.html", r.ReportName);
    }

    [Fact]
    public void Poste_reste_en_1_2_3_reste_exploitable()
    {
        // Un parc ne se met pas à jour en un jour. Une console qui refuse de
        // parler aux postes d'hier est inutilisable le jour du déploiement.
        var r = ParkProtocol.ReadScanResponse("""
            {"ok":true,"report":"Diagnostic_PC_2026-08-01_1200.html","verdict":"Machine stable.","findings":0}
            """);

        Assert.True(r.Ok);
        Assert.Null(r.Level);                                // pas de code : on le sait
        Assert.Equal("Machine stable.", r.RemoteSentence);   // et on garde sa phrase
    }

    [Fact]
    public void Echec_distant_et_reponse_illisible_ne_produisent_aucun_verdict()
    {
        var echec = ParkProtocol.ReadScanResponse("""{"ok":false,"error":"Accès refusé"}""");
        Assert.False(echec.Ok);
        Assert.Equal("Accès refusé", echec.Error);

        // Une réponse tronquée ou venue d'un tout autre service ne doit surtout
        // pas ressortir en « aucun problème significatif ».
        foreach (var brut in new[] { "", "pas du json", "{\"ok\":true", "<html>403</html>" })
        {
            var r = ParkProtocol.ReadScanResponse(brut);
            Assert.False(r.Ok);
            Assert.Null(r.Level);
        }
    }

    [Fact]
    public void Le_code_de_sortie_et_le_code_reseau_viennent_de_la_meme_regle()
    {
        // Sans règle commune, un poste pourrait annoncer « critique » à la console
        // et rendre 0 à la tâche planifiée qui l'a lancé.
        var r = new DiagnosticReport();
        Assert.Equal(ScanLevel.Healthy, ScanLevelInfo.Of(r));
        Assert.Equal(0, ScanLevelInfo.Of(r).ExitCode());
        Assert.Equal("ok", ScanLevelInfo.Of(r).Code());

        r.Findings.Add(new Finding { Severity = Severity.Info, Title = "x" });
        Assert.Equal(ScanLevel.Healthy, ScanLevelInfo.Of(r));

        r.Findings.Add(new Finding { Severity = Severity.Warning, Title = "y" });
        Assert.Equal(ScanLevel.Warnings, ScanLevelInfo.Of(r));
        Assert.Equal(1, ScanLevelInfo.Of(r).ExitCode());
        Assert.Equal("warn", ScanLevelInfo.Of(r).Code());

        r.Findings.Add(new Finding { Severity = Severity.Critical, Title = "z" });
        Assert.Equal(ScanLevel.Critical, ScanLevelInfo.Of(r));
        Assert.Equal(2, ScanLevelInfo.Of(r).ExitCode());
        Assert.Equal("crit", ScanLevelInfo.Of(r).Code());
    }

    [Theory]
    [InlineData("crit", ScanLevel.Critical)]
    [InlineData("WARN", ScanLevel.Warnings)]
    [InlineData(" ok ", ScanLevel.Healthy)]
    public void Codes_relus(string code, ScanLevel attendu) =>
        Assert.Equal(attendu, ScanLevelInfo.ParseCode(code));

    [Theory]
    [InlineData("")]
    [InlineData("critique")]
    // Cast obligatoire : « [InlineData(null)] » nu se compile en tableau
    // d'arguments NUL, pas en argument nul — le test ne recevrait rien.
    [InlineData((string?)null)]
    public void Code_inconnu_vaut_absence_de_code_pas_machine_saine(string? code) =>
        Assert.Null(ScanLevelInfo.ParseCode(code));

    [Fact]
    public void La_phrase_est_ecrite_dans_la_langue_de_celui_qui_lit()
    {
        var initial = Lang.Current;
        try
        {
            Lang.Apply(AppLanguage.English);
            Assert.Contains("critical problem", ScanLevel.Critical.Sentence(3, 5));

            Lang.Apply(AppLanguage.French);
            Assert.Contains("problème(s) critique(s)", ScanLevel.Critical.Sentence(3, 5));
        }
        finally
        {
            Lang.Apply(initial);
        }
    }
}
