using FaultTracePC.Core;
using FaultTracePC.Core.Analysis;
using FaultTracePC.Core.Report;
using Xunit;

namespace FaultTracePC.Tests;

/// <summary>
/// Constaté le 19/10/2026 sur un poste du parc, dans le même rapport :
/// · point 107 — 10 plantages de veyon-server.exe, et la boîte à outils proposait
///   sfc et DISM, la réparation de Windows pour un logiciel tiers ;
/// · point 108 — verdict « MATÉRIELLE », et juste en dessous « Dans ton cas, le
///   problème principal vient d'un pilote ».
/// </summary>
[Collection("Langue")]
public class ConseilSelonLeVerdictTests
{
    private static DiagnosticReport AvecPlantages(string app, string module, int combien)
    {
        var r = new DiagnosticReport
        {
            GeneratedAt = new DateTime(2026, 10, 19, 10, 14, 0),
            ScanPeriodDays = 30,
            System = new SystemSnapshot { MachineName = "POSTE-TEST" },
        };
        for (int i = 0; i < combien; i++)
            r.Events.Add(new WinEvent
            {
                Category = EventCategory.AppCrash,
                Provider = "Application Error",
                EventId = 1000,
                TimeLocal = new DateTime(2026, 10, 15, 9, 0, 0).AddMinutes(i * 7),
                Extracted = { ["App"] = app, ["Module"] = module },
            });
        return r;
    }

    private static Finding Carte(DiagnosticReport r, string app)
    {
        var initial = Lang.Current;
        try { Lang.Apply(AppLanguage.French); new RulesEngine().Analyze(r); }
        finally { Lang.Apply(initial); }
        return r.Findings.First(f => f.Title.Contains(app));
    }

    [Fact]
    public void Un_logiciel_tiers_qui_plante_ne_renvoie_pas_vers_sfc_et_DISM()
    {
        var f = Carte(AvecPlantages("veyon-server.exe", "windows-platform.dll", 10), "veyon-server.exe");
        Assert.Equal(FaultCategory.None, f.Category);
    }

    [Fact]
    public void Un_composant_de_Windows_qui_plante_garde_la_categorie_Logiciel()
    {
        var f = Carte(AvecPlantages("explorer.exe", "ntdll.dll", 4), "explorer.exe");
        Assert.Equal(FaultCategory.Software, f.Category);
    }

    private static DiagnosticReport AvecVerdict(FaultCategory verdict)
    {
        var r = new DiagnosticReport { VerdictCategory = verdict };
        r.Findings.Add(new Finding { Severity = Severity.Critical, Category = FaultCategory.Driver, Title = "BSOD" });
        return r;
    }

    [Fact]
    public void La_phrase_sur_le_pilote_suit_le_verdict()
    {
        Assert.True(HtmlReportGenerator.PiloteEnCause(AvecVerdict(FaultCategory.Driver)));
        Assert.True(HtmlReportGenerator.PiloteEnCause(AvecVerdict(FaultCategory.GpuDriver)));
    }

    [Fact]
    public void Un_verdict_materiel_ne_dit_pas_que_le_probleme_vient_d_un_pilote()
    {
        Assert.False(HtmlReportGenerator.PiloteEnCause(AvecVerdict(FaultCategory.Hardware)));
    }
}
