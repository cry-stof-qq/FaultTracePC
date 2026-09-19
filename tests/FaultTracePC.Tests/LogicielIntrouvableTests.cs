using FaultTracePC.Core;
using FaultTracePC.Core.Analysis;
using FaultTracePC.Core.Collectors;
using Xunit;

namespace FaultTracePC.Tests;

/// <summary>
/// Constaté le 18/09/2026 sur le poste de l'auteur, en 1.6.1 : le rapport écrivait
/// « MicrosoftEdgeUpdate.exe (27 crashs) — ce logiciel ne figure plus parmi les
/// programmes installés — problème probablement sans objet », pendant que six
/// processus msedge tournaient, listés dans le même rapport, quelques sections plus bas.
///
/// Deux fautes distinctes, et les deux sont corrigées ici :
///   1. la correspondance échouait sur un espace — « Microsoft Edge » contre
///      « MicrosoftEdgeUpdate » ;
///   2. l'absence de la liste des programmes installés était présentée comme une
///      preuve de désinstallation, alors que les composants de Windows, les
///      applications du Microsoft Store et les logiciels portables n'y figurent pas.
/// </summary>
[Collection("Langue")]
public class LogicielIntrouvableTests
{
    private static InstalledApp App(string nom, string version = "") =>
        new() { Name = nom, Version = version };

    [Theory]
    [InlineData("Microsoft Edge", "microsoftedge")]
    [InlineData("MicrosoftEdgeUpdate", "microsoftedgeupdate")]
    [InlineData("Adobe Photoshop 2024", "adobephotoshop2024")]
    [InlineData("Notepad++", "notepad")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void Le_nom_comparable_ne_garde_que_lettres_et_chiffres(string? valeur, string attendu)
        => Assert.Equal(attendu, InstalledSoftwareCollector.NomComparable(valeur));

    [Fact]
    public void Un_espace_dans_le_nom_du_produit_n_empeche_plus_la_correspondance()
    {
        var apps = new List<InstalledApp> { App("Microsoft Edge", "141.0.3537.57") };

        var trouve = InstalledSoftwareCollector.FindByExecutable(apps, "MicrosoftEdgeUpdate.exe");

        Assert.NotNull(trouve);
        Assert.Equal("Microsoft Edge", trouve!.Name);
    }

    [Fact]
    public void La_correspondance_habituelle_tient_toujours()
    {
        var apps = new List<InstalledApp> { App("Adobe Photoshop 2024") };
        Assert.NotNull(InstalledSoftwareCollector.FindByExecutable(apps, "photoshop.exe"));
    }

    [Fact]
    public void Un_nom_de_produit_court_n_attrape_pas_tout_ce_qui_le_contient()
    {
        // « Git » dans « GitHubDesktopHelper » : sans longueur minimale, tout
        // exécutable contenant trois lettres communes serait rattaché au produit.
        var apps = new List<InstalledApp> { App("Git") };
        Assert.Null(InstalledSoftwareCollector.FindByExecutable(apps, "GitHubDesktopHelper.exe"));
    }

    [Fact]
    public void Un_logiciel_introuvable_n_est_plus_declare_desinstalle()
    {
        var r = new DiagnosticReport
        {
            GeneratedAt = new DateTime(2026, 9, 18, 8, 4, 0),
            ScanPeriodDays = 30,
            System = new SystemSnapshot { MachineName = "POSTE-TEST" },
        };
        for (int i = 0; i < 4; i++)
            r.Events.Add(new WinEvent
            {
                Category = EventCategory.AppCrash,
                Provider = "Application Error",
                EventId = 1000,
                TimeLocal = new DateTime(2026, 8, 22, 10, 0, 0).AddMinutes(i),
                Extracted = { ["App"] = "UnLogicielPortable.exe", ["Module"] = "ntdll.dll" },
            });
        // L'inventaire n'est pas vide : c'est bien une absence de CE nom, pas une
        // absence d'inventaire — les deux ne se disent pas de la même façon.
        r.System.InstalledApps.Add(App("Microsoft Edge"));

        var initial = Lang.Current;
        try { Lang.Apply(AppLanguage.French); new RulesEngine().Analyze(r); }
        finally { Lang.Apply(initial); }

        var fait = r.Findings.First(f => f.Title.Contains("UnLogicielPortable.exe"));
        Assert.Contains("ne prouve pas une désinstallation", fait.Details);
        // La phrase qui closait le dossier sur une absence de preuve a disparu.
        Assert.DoesNotContain("problème probablement sans objet", fait.Details);
        Assert.DoesNotContain("semble avoir été désinstallé", fait.Recommendation);
    }
}
