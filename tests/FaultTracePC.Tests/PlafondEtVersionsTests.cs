using FaultTracePC.Core;
using FaultTracePC.Core.Analysis;
using FaultTracePC.Core.Collectors;
using FaultTracePC.Core.Report;
using Xunit;

namespace FaultTracePC.Tests;

/// <summary>
/// Deux défauts constatés le 18/09/2026 sur le rapport de TECH-INFO-2025 (1.6.0).
///
/// 1. « Erreurs disque répétées (500) » : 500 n'était pas un comptage mais le
///    plafond de collecte d'EventLogCollector. Un chiffre rond présenté comme une
///    mesure trompe sur la gravité, et dans les deux sens.
/// 2. « afd.sys : 10.0.26100.8875 → 10.0.26100.8875 » dans les pilotes mis à jour :
///    la comparaison portait sur « version|date de fichier », donc une date de
///    fichier réécrite par Windows passait pour une mise à jour. Au-delà de
///    l'affichage, cette liste alimente le point 55 et pouvait disculper un pilote
///    qui n'avait jamais été remplacé.
/// </summary>
[Collection("Langue")]
public class PlafondEtVersionsTests
{
    private static T EnFrancais<T>(Func<T> f)
    {
        var initial = Lang.Current;
        try { Lang.Apply(AppLanguage.French); return f(); }
        finally { Lang.Apply(initial); }
    }

    // ------------------------------------------------------------------
    // 1. Plafond de collecte
    // ------------------------------------------------------------------

    private static DiagnosticReport AvecErreursDisque(int nombre, bool tronque)
    {
        var r = new DiagnosticReport
        {
            GeneratedAt = new DateTime(2026, 9, 18, 5, 7, 0),
            ScanPeriodDays = 30,
            System = new SystemSnapshot { MachineName = "PC-TEST" },
        };
        for (int i = 0; i < nombre; i++)
            r.Events.Add(new WinEvent
            {
                TimeLocal = new DateTime(2026, 9, 12, 9, 24, 0).AddMinutes(i),
                LogName = "System",
                Provider = "disk",
                EventId = 7,
                Category = EventCategory.DiskError,
                Message = "Le périphérique comporte un bloc défectueux.",
            });
        if (tronque) r.TruncatedEventCategories.Add(EventCategory.DiskError);
        return r;
    }

    [Fact]
    public void Un_nombre_non_tronque_s_ecrit_tel_quel()
    {
        var r = AvecErreursDisque(12, tronque: false);
        Assert.Equal("12", EnFrancais(() => RulesEngine.Denombrer(r, EventCategory.DiskError, 12)));
        Assert.Equal("", EnFrancais(() => RulesEngine.PlafondAtteint(r, EventCategory.DiskError)));
    }

    [Fact]
    public void Un_nombre_tronque_s_annonce_comme_un_plancher()
    {
        var r = AvecErreursDisque(3, tronque: true);
        Assert.Equal("au moins 500", EnFrancais(() => RulesEngine.Denombrer(r, EventCategory.DiskError, 500)));
        Assert.Contains("plancher", EnFrancais(() => RulesEngine.PlafondAtteint(r, EventCategory.DiskError)));
    }

    [Fact]
    public void Le_plafond_ne_deborde_pas_sur_une_autre_categorie()
    {
        var r = AvecErreursDisque(3, tronque: true);
        Assert.Equal("4", EnFrancais(() => RulesEngine.Denombrer(r, EventCategory.Whea, 4)));
        Assert.Equal("", EnFrancais(() => RulesEngine.PlafondAtteint(r, EventCategory.Whea)));
    }

    [Fact]
    public void Le_titre_du_fait_disque_dit_au_moins_quand_la_collecte_a_ete_coupee()
    {
        var r = AvecErreursDisque(RulesEnginePlafond, tronque: true);
        EnFrancais(() => { new RulesEngine().Analyze(r); return 0; });

        var fait = Assert.Single(r.Findings, f => f.Code == "disk_event");
        Assert.Contains($"au moins {RulesEnginePlafond}", fait.Title);
        Assert.Contains(RulesEnginePlafond.ToString(), fait.Details);
        Assert.Contains("plancher", fait.Details);
    }

    [Fact]
    public void Sans_troncature_le_titre_donne_le_nombre_exact()
    {
        var r = AvecErreursDisque(12, tronque: false);
        EnFrancais(() => { new RulesEngine().Analyze(r); return 0; });

        var fait = Assert.Single(r.Findings, f => f.Code == "disk_event");
        Assert.Contains("(12)", fait.Title);
        Assert.DoesNotContain("au moins", fait.Title);
        Assert.DoesNotContain("plancher", fait.Details);
    }

    /// <summary>Le plafond réel du collecteur, pour que le test suive si la valeur change.</summary>
    private static int RulesEnginePlafond => EventLogCollector.MaxEvenementsParRequete;

    // ------------------------------------------------------------------
    // 2. Comparaison des pilotes sur la version seule
    // ------------------------------------------------------------------

    private static DiagnosticReport AvecPilote(string version, DateTime dateFichier)
    {
        var r = new DiagnosticReport
        {
            GeneratedAt = new DateTime(2026, 9, 18, 5, 7, 0),
            ScanPeriodDays = 30,
            System = new SystemSnapshot { MachineName = "PC-TEST" },
        };
        r.System.Drivers.Add(new DriverInfo
        {
            Name = "afd",
            DisplayName = "Ancillary Function Driver for WinSock",
            Path = @"C:\WINDOWS\System32\drivers\afd.sys",
            CompanyName = "Microsoft Corporation",
            FileVersion = version,
            FileDate = dateFichier,
            State = "Running",
        });
        return r;
    }

    private static ScanHistory.ScanSummary Precedent(string version, string dateFichier) => new()
    {
        GeneratedAt = new DateTime(2026, 8, 30, 8, 0, 0),
        ScanPeriodDays = 30,
        DriverVersions = { ["afd.sys"] = $"{version}|{dateFichier}" },
    };

    [Fact]
    public void Une_date_de_fichier_qui_change_seule_n_est_pas_une_mise_a_jour()
    {
        var r = AvecPilote("10.0.26100.8875", new DateTime(2026, 9, 11));
        var c = EnFrancais(() => ScanHistory.Compare(r, Precedent("10.0.26100.8875", "2026-07-02")));

        Assert.Empty(c.DriverUpdates);
    }

    [Fact]
    public void Une_version_qui_change_reste_une_mise_a_jour()
    {
        var r = AvecPilote("10.0.26100.9444", new DateTime(2026, 9, 11));
        var c = EnFrancais(() => ScanHistory.Compare(r, Precedent("10.0.26100.9278", "2026-07-02")));

        var ligne = Assert.Single(c.DriverUpdates);
        Assert.Contains("10.0.26100.9278", ligne);
        Assert.Contains("10.0.26100.9444", ligne);
    }

    [Fact]
    public void La_version_seule_se_lit_meme_sans_separateur()
    {
        Assert.Equal("10.0.26100.8875", ScanHistory.VersionSeule("10.0.26100.8875|2026-07-02"));
        Assert.Equal("10.0.26100.8875", ScanHistory.VersionSeule("10.0.26100.8875"));
        Assert.Equal("", ScanHistory.VersionSeule(""));
    }
}
