using FaultTracePC.Core;
using FaultTracePC.Core.Analysis;
using FaultTracePC.Core.Report;
using Xunit;

namespace FaultTracePC.Tests;

/// <summary>
/// POINT 85, LOT 1 — décidé le 10/10/2026 : dans les conclusions, seulement les
/// logiciels qui servent au diagnostic (installés juste avant le début d'une série
/// de plantages) ; la liste complète, repliée, en bas du rapport.
///
/// Les logiciels ci-dessous sont des données de test : la liste des logiciels de
/// la machine du 24/09/2026 n'apparaissait pas dans son rapport.
/// </summary>
[Collection("Langue")]
public class LogicielsInstallesTests
{
    private static InstalledApp Logiciel(string nom, DateTime? date) =>
        new() { Name = nom, Version = "1.0", Publisher = "Éditeur de test", InstallDate = date };

    private static T EnFrancais<T>(Func<T> f)
    {
        var initial = Lang.Current;
        try { Lang.Apply(AppLanguage.French); return f(); }
        finally { Lang.Apply(initial); }
    }

    private static List<Finding> Conclure(params InstalledApp[] logiciels) => EnFrancais(() =>
    {
        var r = new DiagnosticReport { Bsods = SeriesDePlantagesTests.ProfilDu24Septembre() };
        r.System.InstalledApps.AddRange(logiciels);
        RulesEngine.AnalyzeLogicielsAuDebutDesSeries(r);
        return r.Findings;
    });

    [Fact]
    public void Un_logiciel_installe_le_jour_du_premier_plantage_est_une_piste()
    {
        var f = Assert.Single(Conclure(
            Logiciel("Logiciel posé ce jour-là", new DateTime(2026, 9, 22)),
            Logiciel("Logiciel ancien", new DateTime(2025, 1, 15))));

        Assert.Equal("logiciels.debut_serie", f.Code);
        Assert.Equal(Severity.Warning, f.Severity);
        Assert.Equal(Confidence.Low, f.Confidence);
        Assert.Equal(FaultCategory.None, f.Category);
        Assert.Contains("Logiciel posé ce jour-là", f.Title);
        Assert.DoesNotContain("Logiciel ancien", f.Title);
        Assert.Contains("le 22/09/2026", f.Details);
        Assert.Contains("pas une preuve", f.Recommendation);
    }

    [Fact]
    public void Hors_de_la_fenetre_ou_sans_date_rien_n_est_dit()
    {
        Assert.Empty(Conclure(
            Logiciel("Trois jours avant", new DateTime(2026, 9, 19)),
            Logiciel("Le lendemain", new DateTime(2026, 9, 23)),
            Logiciel("Sans date", null)));
    }

    [Fact]
    public void La_liste_complete_est_repliee_les_plus_recents_d_abord()
    {
        var r = new DiagnosticReport();
        r.System.InstalledApps.AddRange(
        [
            Logiciel("Ancien", new DateTime(2024, 3, 1)),
            Logiciel("Sans date", null),
            Logiciel("Recent", new DateTime(2026, 9, 22)),
        ]);
        // Le rapport encode les accents en entités HTML : on relit le texte décodé.
        var html = System.Net.WebUtility.HtmlDecode(EnFrancais(() => HtmlReportGenerator.SectionLogicielsInstalles(r)));

        Assert.Contains("<details", html);
        Assert.Contains("Afficher les 3 logiciels installés", html);
        Assert.True(html.IndexOf("Recent", StringComparison.Ordinal) < html.IndexOf("Ancien", StringComparison.Ordinal));
        Assert.True(html.IndexOf("Ancien", StringComparison.Ordinal) < html.IndexOf("Sans date", StringComparison.Ordinal));
    }

    [Fact]
    public void Sans_logiciel_lu_pas_de_section()
    {
        Assert.Equal("", HtmlReportGenerator.SectionLogicielsInstalles(new DiagnosticReport()));
    }
}
