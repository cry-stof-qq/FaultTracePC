using FaultTracePC.Core;
using FaultTracePC.Core.Analysis;
using FaultTracePC.Core.Collectors;
using Xunit;

namespace FaultTracePC.Tests;

/// <summary>
/// POINT 85, LOT 2 — services et pilotes inscrits dont le programme n'existe plus.
/// Le service « Apex One NT WSC Service » (TmWSCSvc) est celui constaté le
/// 09/10/2026 sur le poste de l'auteur ; son chemin, lui, n'a pas été relevé : celui
/// du test est une donnée de test.
/// </summary>
[Collection("Langue")]
public class InscriptionsOrphelinesTests
{
    [Theory]
    [InlineData(@"""C:\Program Files\Editeur\service.exe"" -k groupe", @"C:\Program Files\Editeur\service.exe")]
    [InlineData(@"C:\Program Files\Editeur\service.exe -k groupe", @"C:\Program Files\Editeur\service.exe")]
    [InlineData(@"C:\Program Files\Editeur\service.EXE", @"C:\Program Files\Editeur\service.EXE")]
    [InlineData(@"\??\C:\Program Files\Editeur\pilote.sys", @"C:\Program Files\Editeur\pilote.sys")]
    [InlineData("", "")]
    [InlineData("sans extension connue", "")]
    public void Le_chemin_du_programme_est_isole_de_la_ligne_de_commande(string ligne, string attendu)
    {
        Assert.Equal(attendu, ServiceCollector.CheminDuProgramme(ligne));
    }

    private static Finding? Conclure(DiagnosticReport r)
    {
        var initial = Lang.Current;
        try { Lang.Apply(AppLanguage.French); RulesEngine.AnalyzeInscriptionsOrphelines(r); }
        finally { Lang.Apply(initial); }
        return r.Findings.SingleOrDefault(f => f.Code == "inscriptions.orphelines");
    }

    [Fact]
    public void Un_service_et_un_pilote_orphelins_sont_nommes_en_information()
    {
        var r = new DiagnosticReport();
        r.System.ServicesOrphelins.Add(new ServiceOrphelin
        {
            Nom = "TmWSCSvc", NomAffiche = "Apex One NT WSC Service",
            Chemin = @"C:\Program Files (x86)\Trend Micro\Security Agent\service-de-test.exe",
            Demarrage = "Manual", Etat = "Stopped",
        });
        r.System.Drivers.Add(new DriverInfo { Name = "pilotetest", DisplayName = "Pilote de test", Path = @"C:\Windows\System32\drivers\pilotetest.sys", StartMode = "System", FichierPresent = false });
        r.System.Drivers.Add(new DriverInfo { Name = "present", Path = @"C:\Windows\System32\drivers\present.sys", FichierPresent = true });

        var f = Conclure(r);
        Assert.NotNull(f);
        Assert.Equal(Severity.Info, f.Severity);
        Assert.Equal(FaultCategory.None, f.Category);
        Assert.Contains("2 service(s) ou pilote(s)", f.Title);
        Assert.Contains("Apex One NT WSC Service", f.Details);
        Assert.Contains("pilotetest", f.Details);
        Assert.DoesNotContain("present.sys", f.Details);
        Assert.Contains("Ne pas effacer une inscription à la main", f.Recommendation);
    }

    [Fact]
    public void Rien_d_orphelin_rien_n_est_dit()
    {
        var r = new DiagnosticReport();
        r.System.Drivers.Add(new DriverInfo { Name = "sanschemin", FichierPresent = null });
        Assert.Null(Conclure(r));
    }
}
