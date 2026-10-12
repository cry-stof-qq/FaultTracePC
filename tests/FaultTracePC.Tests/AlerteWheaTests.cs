using FaultTracePC.Core;
using FaultTracePC.Core.Analysis;
using Xunit;

namespace FaultTracePC.Tests;

/// <summary>
/// Point 98 de la feuille de route. Mesuré le 12/10/2026 sur un poste du parc :
/// 50 événements WHEA, tous d'identifiant 17 et de niveau « Avertissement » —
/// des erreurs corrigées par le matériel. La surveillance les avait pourtant
/// toutes archivées en « alerte critique », sans garder leur numéro.
/// </summary>
[Collection("Langue")]
public class AlerteWheaTests
{
    private static void EnLangue(AppLanguage langue, Action action)
    {
        var initial = Lang.Current;
        try { Lang.Apply(langue); action(); }
        finally { Lang.Apply(initial); }
    }

    private static PreventiveAlert Whea(int? niveauWindows, int numero, DateTime quand) => new()
    {
        Time = quand,
        RuleId = "whea",
        Level = AlertCatalog.NiveauWhea(niveauWindows),
        Value = numero,
    };

    [Theory]
    [InlineData(3, "warn")]   // avertissement : erreur corrigée
    [InlineData(4, "warn")]   // information
    [InlineData(2, "crit")]   // erreur
    [InlineData(1, "crit")]   // critique
    [InlineData(null, "crit")] // inconnu : on ne baisse pas l'alarme sans savoir
    public void Le_niveau_suit_celui_de_Windows(int? niveauWindows, string attendu) =>
        Assert.Equal(attendu, AlertCatalog.NiveauWhea(niveauWindows));

    [Fact]
    public void Une_erreur_corrigee_le_dit_et_garde_son_numero()
    {
        var a = Whea(3, 17, new DateTime(2026, 10, 12, 13, 56, 0));
        EnLangue(AppLanguage.French, () =>
        {
            Assert.True(AlertCatalog.Localize(a));
            Assert.Contains("corrigée", a.Title);
            Assert.Contains("17", a.Title);
            Assert.DoesNotContain("processeur", a.Title);
            Assert.DoesNotContain("défaillance", a.Details);
        });
        EnLangue(AppLanguage.English, () =>
        {
            Assert.True(AlertCatalog.Localize(a));
            Assert.Contains("Corrected", a.Title);
            Assert.Contains("17", a.Title);
        });
    }

    [Fact]
    public void Une_erreur_grave_reste_critique_et_le_dit()
    {
        var a = Whea(2, 18, new DateTime(2026, 10, 12, 13, 56, 0));
        Assert.Equal("crit", a.Level);
        EnLangue(AppLanguage.French, () =>
        {
            Assert.True(AlertCatalog.Localize(a));
            Assert.Contains("grave", a.Title);
            Assert.Contains("18", a.Title);
            Assert.DoesNotContain("corrigée", a.Title);
        });
    }

    [Fact]
    public void Des_erreurs_toutes_corrigees_donnent_une_carte_a_surveiller()
    {
        var r = new DiagnosticReport
        {
            ScanPeriodDays = 30,
            System = new SystemSnapshot { MachineName = "POSTE-TEST" },
        };
        for (var i = 0; i < 5; i++)
            r.Flight.Alerts.Add(Whea(3, 17, new DateTime(2026, 10, 12, 9, 0, 0).AddHours(i)));

        EnLangue(AppLanguage.French, () =>
        {
            AlertCatalog.LocalizeAll(r.Flight.Alerts);
            new RulesEngine().Analyze(r);
            var carte = r.Findings.FirstOrDefault(f => f.Code == "whea");
            Assert.NotNull(carte);
            Assert.Equal(Severity.Warning, carte!.Severity);
            Assert.Contains("corrigée", carte.Title);
        });
    }

    [Fact]
    public void Une_erreur_grave_parmi_des_corrigees_donne_une_carte_critique_coherente()
    {
        // L'erreur grave est la plus ANCIENNE : la carte doit quand même être
        // critique ET titrée sur l'erreur grave, pas sur la dernière corrigée.
        var r = new DiagnosticReport
        {
            ScanPeriodDays = 30,
            System = new SystemSnapshot { MachineName = "POSTE-TEST" },
        };
        r.Flight.Alerts.Add(Whea(2, 18, new DateTime(2026, 10, 12, 8, 0, 0)));
        r.Flight.Alerts.Add(Whea(3, 17, new DateTime(2026, 10, 12, 9, 0, 0)));
        r.Flight.Alerts.Add(Whea(3, 17, new DateTime(2026, 10, 12, 10, 0, 0)));

        EnLangue(AppLanguage.French, () =>
        {
            AlertCatalog.LocalizeAll(r.Flight.Alerts);
            new RulesEngine().Analyze(r);
            var carte = r.Findings.FirstOrDefault(f => f.Code == "whea");
            Assert.NotNull(carte);
            Assert.Equal(Severity.Critical, carte!.Severity);
            Assert.Contains("grave", carte.Title);
        });
    }
}
