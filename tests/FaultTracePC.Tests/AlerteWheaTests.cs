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

    // ------------------------------------------------------------------
    // Point 112 — constaté le 21/10/2026 sur cinq postes du parc : des alertes
    // écrites avant la 1.8.1 (sans numéro, « crit » par défaut) remontaient en
    // critique une carte d'erreurs toutes corrigées, et donnaient le verdict
    // « MATÉRIELLE (CPU/carte mère/alimentation ou surchauffe) ».
    // ------------------------------------------------------------------

    /// <summary>Alerte telle que l'écrivait une version antérieure à la 1.8.1.</summary>
    private static PreventiveAlert WheaAncienne(DateTime quand) => new()
    {
        Time = quand,
        RuleId = "whea",
        Level = "crit",
        Value = null,
    };

    private static WinEvent WheaJournal(int numero, DateTime quand) => new()
    {
        Category = EventCategory.Whea,
        Provider = "Microsoft-Windows-WHEA-Logger",
        EventId = numero,
        TimeLocal = quand,
    };

    private static DiagnosticReport RapportWhea() => new()
    {
        ScanPeriodDays = 30,
        System = new SystemSnapshot { MachineName = "POSTE-TEST" },
    };

    [Fact]
    public void Une_alerte_ancienne_ne_rend_pas_critiques_des_erreurs_toutes_corrigees()
    {
        // Le cas réel : trois erreurs 17 dans le journal, la dernière le 28/09 à
        // 13:08, et une alerte de la surveillance à la même minute, écrite en 1.8.0.
        var r = RapportWhea();
        r.Events.Add(WheaJournal(17, new DateTime(2026, 9, 25, 10, 0, 0)));
        r.Events.Add(WheaJournal(17, new DateTime(2026, 9, 27, 10, 0, 0)));
        r.Events.Add(WheaJournal(17, new DateTime(2026, 9, 28, 13, 8, 0)));
        r.Flight.Alerts.Add(WheaAncienne(new DateTime(2026, 9, 28, 13, 8, 30)));

        EnLangue(AppLanguage.French, () =>
        {
            AlertCatalog.LocalizeAll(r.Flight.Alerts);
            new RulesEngine().Analyze(r);
            var carte = Assert.Single(r.Findings, f => f.Code == "whea");
            Assert.Equal(Severity.Warning, carte.Severity);
            // Plus de texte contradictoire : ni « corrigée ou fatale », ni la liste
            // CPU / mémoire / carte mère / alimentation.
            Assert.DoesNotContain("corrigée ou fatale", carte.Details);
            Assert.DoesNotContain("défaillance CPU", carte.Details);
            Assert.DoesNotContain("MATÉRIELLE", r.Verdict);
        });
        // La boîte noire garde l'alerte telle qu'elle a été écrite.
        Assert.Equal("crit", r.Flight.Alerts[0].Level);
        Assert.Null(r.Flight.Alerts[0].Value);
    }

    [Fact]
    public void Une_erreur_fatale_dans_le_journal_laisse_l_alerte_ancienne_critique()
    {
        var r = RapportWhea();
        r.Events.Add(WheaJournal(17, new DateTime(2026, 9, 25, 10, 0, 0)));
        r.Events.Add(WheaJournal(18, new DateTime(2026, 9, 28, 13, 8, 0)));
        r.Flight.Alerts.Add(WheaAncienne(new DateTime(2026, 9, 28, 13, 8, 30)));

        EnLangue(AppLanguage.French, () =>
        {
            AlertCatalog.LocalizeAll(r.Flight.Alerts);
            new RulesEngine().Analyze(r);
            var carte = Assert.Single(r.Findings, f => f.Code == "whea");
            Assert.Equal(Severity.Critical, carte.Severity);
        });
    }

    [Fact]
    public void Une_alerte_plus_ancienne_que_le_journal_reste_critique()
    {
        // Le journal ne couvre plus la date de l'alerte : il ne peut pas répondre pour elle.
        var alerte = WheaAncienne(new DateTime(2026, 9, 20, 8, 0, 0));
        var journal = new List<WinEvent> { WheaJournal(17, new DateTime(2026, 9, 25, 10, 0, 0)) };

        var relue = RulesEngine.RelireWheaAncienne(alerte, journal);

        Assert.Same(alerte, relue);
        Assert.Equal("crit", relue.Level);
    }

    [Fact]
    public void Une_alerte_recente_garde_son_niveau_meme_critique()
    {
        // Depuis la 1.8.1, l'alerte porte son numéro : c'est lui qui fait foi.
        var alerte = Whea(2, 18, new DateTime(2026, 10, 15, 8, 0, 0));
        var journal = new List<WinEvent> { WheaJournal(17, new DateTime(2026, 10, 1, 10, 0, 0)) };

        Assert.Same(alerte, RulesEngine.RelireWheaAncienne(alerte, journal));
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
