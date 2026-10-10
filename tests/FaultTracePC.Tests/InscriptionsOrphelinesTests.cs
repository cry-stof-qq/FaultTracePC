using FaultTracePC.Core;
using FaultTracePC.Core.Analysis;
using FaultTracePC.Core.Collectors;
using Xunit;

namespace FaultTracePC.Tests;

/// <summary>
/// POINT 85, LOT 2 — services et pilotes inscrits dont le programme n'existe plus.
/// Le service « Apex One NT WSC Service » (TmWSCSvc), son chemin et son mode de
/// démarrage sont ceux relevés le 10/10/2026 dans le rapport du poste de l'auteur.
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
            Chemin = @"C:\Program Files (x86)\Trend Micro\Security Agent\TmWSCSvc.exe",
            Demarrage = "Auto", Etat = "Stopped",
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

    private static DiagnosticReport ServiceQuiNeDemarrePas(int fois)
    {
        var r = new DiagnosticReport { ScanPeriodDays = 30, System = new SystemSnapshot { MachineName = "POSTE-TEST" } };
        for (int i = 0; i < fois; i++)
        {
            var e = new WinEvent
            {
                Category = EventCategory.ServiceFailure, Provider = "Service Control Manager", EventId = 7000,
                TimeLocal = new DateTime(2026, 9, 24 + i % 6, 8, 27, 0), Message = "Echec.",
            };
            e.Extracted["Service"] = "TmWSCSvc";
            r.Events.Add(e);
        }
        r.System.ServicesLus = true;
        return r;
    }

    private static Finding CarteServices(DiagnosticReport r)
    {
        var initial = Lang.Current;
        try { Lang.Apply(AppLanguage.French); new RulesEngine().Analyze(r); }
        finally { Lang.Apply(initial); }
        return r.Findings.Single(f => f.Title.Contains("services Windows"));
    }

    [Fact]
    public void La_carte_des_services_dit_ce_qui_a_ete_verifie_au_lieu_de_le_demander()
    {
        // Constaté le 10/10/2026 : la carte demandait de vérifier le programme de
        // TmWSCSvc alors qu'une autre carte établissait qu'il n'existait plus.
        var r = ServiceQuiNeDemarrePas(7);
        r.System.ServicesOrphelins.Add(new ServiceOrphelin
        {
            Nom = "TmWSCSvc", NomAffiche = "Apex One NT WSC Service",
            Chemin = @"C:\Program Files (x86)\Trend Micro\Security Agent\TmWSCSvc.exe", Demarrage = "Auto", Etat = "Stopped",
        });

        var f = CarteServices(r);
        Assert.Contains("Vérifié : le programme de TmWSCSvc n'existe plus", f.Details);
        Assert.DoesNotContain("Le point à vérifier", f.Details);
    }

    [Fact]
    public void Programme_present_la_piste_de_l_inscription_orpheline_est_ecartee()
    {
        var f = CarteServices(ServiceQuiNeDemarrePas(7));
        Assert.Contains("existe encore", f.Details);
        Assert.DoesNotContain("Le point à vérifier", f.Details);
    }
}
