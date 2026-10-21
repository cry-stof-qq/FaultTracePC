using FaultTracePC.Core;
using FaultTracePC.Core.Analysis;
using FaultTracePC.Core.Collectors;
using Xunit;

namespace FaultTracePC.Tests;

/// <summary>
/// Point 113 de la feuille de route. Constaté le 21/10/2026 sur un poste du parc :
/// « Application instable : MicrosoftSearchInBing.exe (33 crashs) », puis
/// « Application anciennement instable : (inconnue) (33 crashs) », dernier plantage
/// le jour même. Chaque plantage d'une application .NET laisse deux événements à la
/// même minute — « .NET Runtime » 1026 et « Application Error » 1000 — et le 1026,
/// dont le nom d'application n'était pas lu, était compté une seconde fois.
/// </summary>
[Collection("Langue")]
public class PlantageDotNetTests
{
    private static WinEvent Erreur1000(string app, DateTime quand) => new()
    {
        Category = EventCategory.AppCrash,
        Provider = "Application Error",
        EventId = 1000,
        TimeLocal = quand,
        Extracted = { ["App"] = app, ["Module"] = "KERNELBASE.dll" },
    };

    private static WinEvent DotNet1026(string? app, DateTime quand)
    {
        var e = new WinEvent
        {
            Category = EventCategory.AppCrash,
            Provider = ".NET Runtime",
            EventId = 1026,
            TimeLocal = quand,
        };
        if (app is not null) e.Extracted["App"] = app;
        return e;
    }

    [Theory]
    [InlineData("Application: MicrosoftSearchInBing.exe\nFramework Version: v4.0.30319\nDescription: The process was terminated", "MicrosoftSearchInBing.exe")]
    [InlineData("Application : MicrosoftSearchInBing.exe", "MicrosoftSearchInBing.exe")]
    [InlineData("Application: KeePass.exe\r\nCoreCLR Version: 8.0.10", "KeePass.exe")]
    public void Le_nom_de_l_application_se_lit_dans_le_1026(string texte, string attendu) =>
        Assert.Equal(attendu, EventLogCollector.NomApplicationDotNet(texte));

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("Description: The process was terminated due to an unhandled exception.")]
    public void Sans_nom_lisible_rien_n_est_invente(string? texte) =>
        Assert.Null(EventLogCollector.NomApplicationDotNet(texte));

    [Fact]
    public void Un_1026_qui_accompagne_un_1000_n_est_pas_recompte()
    {
        var t = new DateTime(2026, 10, 21, 8, 59, 10);
        var crashes = new List<WinEvent>
        {
            DotNet1026("MicrosoftSearchInBing.exe", t),
            Erreur1000("MicrosoftSearchInBing.exe", t.AddSeconds(1)),
        };

        var reste = Assert.Single(RulesEngine.SansDoublonsDotNet(crashes));
        Assert.Equal(1000, reste.EventId);
    }

    [Fact]
    public void Un_1026_seul_reste_compte()
    {
        // Sans 1000, le 1026 est la seule trace du plantage.
        var crashes = new List<WinEvent> { DotNet1026("KeePass.exe", new DateTime(2026, 10, 21, 9, 0, 0)) };

        Assert.Single(RulesEngine.SansDoublonsDotNet(crashes));
    }

    [Fact]
    public void Un_1026_d_une_autre_application_n_est_pas_absorbe()
    {
        var t = new DateTime(2026, 10, 21, 9, 0, 0);
        var crashes = new List<WinEvent> { DotNet1026("KeePass.exe", t), Erreur1000("veyon-server.exe", t) };

        Assert.Equal(2, RulesEngine.SansDoublonsDotNet(crashes).Count);
    }

    [Fact]
    public void Un_1026_sans_nom_pres_d_un_1000_est_le_meme_plantage()
    {
        var t = new DateTime(2026, 10, 21, 9, 0, 0);
        var crashes = new List<WinEvent> { DotNet1026(null, t), Erreur1000("MicrosoftSearchInBing.exe", t.AddSeconds(2)) };

        Assert.Single(RulesEngine.SansDoublonsDotNet(crashes));
    }

    [Fact]
    public void Le_cas_reel_ne_donne_plus_qu_une_carte()
    {
        var r = new DiagnosticReport
        {
            GeneratedAt = new DateTime(2026, 10, 21, 9, 2, 0),
            ScanPeriodDays = 30,
            System = new SystemSnapshot { MachineName = "POSTE-TEST" },
        };
        var debut = new DateTime(2026, 10, 5, 8, 0, 0);
        for (int i = 0; i < 33; i++)
        {
            var t = debut.AddHours(11 * i);
            r.Events.Add(DotNet1026("MicrosoftSearchInBing.exe", t));
            r.Events.Add(Erreur1000("MicrosoftSearchInBing.exe", t.AddSeconds(1)));
        }

        var initial = Lang.Current;
        try { Lang.Apply(AppLanguage.French); new RulesEngine().Analyze(r); }
        finally { Lang.Apply(initial); }

        Assert.Single(r.Findings, f => f.Title.Contains("MicrosoftSearchInBing.exe (33 crashs)"));
        Assert.DoesNotContain(r.Findings, f => f.Title.Contains("(inconnue)"));
    }
}
