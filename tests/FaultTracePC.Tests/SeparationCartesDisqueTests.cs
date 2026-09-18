using FaultTracePC.Core;
using FaultTracePC.Core.Analysis;
using Xunit;

namespace FaultTracePC.Tests;

/// <summary>
/// Point 65. Constaté le 18/09/2026 sur TECH-INFO-2025 : la carte « Erreurs disque
/// répétées (500) » additionnait 479 événements sur un support débranché depuis,
/// 20 réinitialisations d'un port de contrôleur et une erreur de système de fichiers
/// sur un volume. Trois choses sans rapport, un seul chiffre, une seule
/// recommandation — qui commençait par le firmware du SSD.
///
/// Une nature de périphérique = une carte, une gravité, un conseil.
/// </summary>
[Collection("Langue")]
public class SeparationCartesDisqueTests
{
    private static DiagnosticReport Rapport(params (string Provider, int Id, string Message)[] events)
    {
        var r = new DiagnosticReport
        {
            GeneratedAt = new DateTime(2026, 9, 18, 5, 7, 0),
            ScanPeriodDays = 30,
            System = new SystemSnapshot { MachineName = "POSTE-TEST" },
        };
        foreach (var (p, id, msg) in events)
            r.Events.Add(new WinEvent
            {
                Category = EventCategory.DiskError,
                Provider = p,
                EventId = id,
                Message = msg,
                TimeLocal = new DateTime(2026, 9, 11, 9, 24, 0),
            });
        return r;
    }

    private static DiskInfo Nvme(int index) => new()
    {
        Model = "RPEYJ1T24MML1AWX",
        Index = index,
        InterfaceType = "SCSI",
        MediaType = "SSD",
        Health = DiskHealth.Healthy,
    };

    private static void Analyser(DiagnosticReport r)
    {
        var initial = Lang.Current;
        try { Lang.Apply(AppLanguage.French); new RulesEngine().Analyze(r); }
        finally { Lang.Apply(initial); }
    }

    // ------------------------------------------------------------------
    // Classement d'un événement
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(@"Le périphérique \Device\Harddisk0\DR0 comporte un bloc défectueux.", RulesEngine.NatureCitee.DisqueMonte)]
    [InlineData(@"Le périphérique \Device\Harddisk1\DR1 comporte un bloc défectueux.", RulesEngine.NatureCitee.DisqueAbsent)]
    [InlineData(@"Réinitialisation au périphérique \Device\RaidPort1 émise.", RulesEngine.NatureCitee.PortControleur)]
    [InlineData(@"Défaillance sur \Device\HarddiskVolume3.", RulesEngine.NatureCitee.VolumeNomme)]
    [InlineData(@"Une défaillance a été détectée sur le volume D:.", RulesEngine.NatureCitee.NonIdentifie)]
    public void Chaque_message_est_classe_par_la_nature_du_peripherique(string message, RulesEngine.NatureCitee attendue)
    {
        var e = new WinEvent { Category = EventCategory.DiskError, Provider = "disk", EventId = 7, Message = message };
        Assert.Equal(attendue, RulesEngine.ClasserEvenement(e, [Nvme(0)]));
    }

    [Fact]
    public void Un_volume_nomme_n_est_pas_confondu_avec_un_numero_de_disque()
    {
        // « HarddiskVolume3 » commence par « Harddisk » : sans ancre de fin, il aurait
        // été lu comme le disque numéro 3 — absent de l'inventaire, donc classé
        // « support débranché ». Un volume monté aurait disparu du rapport.
        var e = new WinEvent { Message = @"Défaillance sur \Device\HarddiskVolume3." };
        Assert.Equal(RulesEngine.NatureCitee.VolumeNomme, RulesEngine.ClasserEvenement(e, [Nvme(0)]));
    }

    // ------------------------------------------------------------------
    // Séparation des cartes
    // ------------------------------------------------------------------

    /// <summary>Le cas réel de TECH-INFO-2025, en miniature.</summary>
    private static DiagnosticReport CasTechInfo()
    {
        var evenements = new List<(string, int, string)>();
        for (int i = 0; i < 6; i++)
            evenements.Add(("disk", 7, @"Le périphérique \Device\Harddisk1\DR1 comporte un bloc défectueux."));
        for (int i = 0; i < 3; i++)
            evenements.Add(("storahci", 129, @"Réinitialisation au périphérique \Device\RaidPort1 émise."));
        evenements.Add(("Ntfs", 55, "Une défaillance a été détectée dans la structure du système de fichiers sur le volume D:."));

        var r = Rapport(evenements.ToArray());
        r.System.Disks.Add(Nvme(0));
        return r;
    }

    [Fact]
    public void Trois_natures_donnent_trois_cartes_distinctes()
    {
        var r = CasTechInfo();
        Analyser(r);

        var cartes = r.Findings.Where(f => f.Code.StartsWith("disk_event")).ToList();
        Assert.Equal(3, cartes.Count);
        // Chaque carte porte un identifiant de fait différent, sans quoi la fusion
        // des doublons les recollerait en une seule.
        Assert.Equal(3, cartes.Select(f => f.Code).Distinct().Count());
    }

    [Fact]
    public void Le_support_debranche_ne_pese_plus_sur_la_gravite_de_la_machine()
    {
        var r = CasTechInfo();
        Analyser(r);

        var absent = r.Findings.First(f => f.Code == "disk_event_absent");
        Assert.Equal(Severity.Info, absent.Severity);
        Assert.Contains("(6)", absent.Title);
        Assert.Contains("ne sont plus connectés", absent.Details);
        // Aucun geste machine sur un support qui n'est plus là.
        Assert.DoesNotContain("firmware du SSD", absent.Recommendation);
    }

    [Fact]
    public void Le_port_de_controleur_a_sa_carte_et_dit_ce_qu_il_ne_designe_pas()
    {
        var r = CasTechInfo();
        Analyser(r);

        var controleur = r.Findings.First(f => f.Code == "disk_event");
        Assert.Contains("(3)", controleur.Title);
        Assert.Contains("ne désigne aucun disque en particulier", controleur.Details);
        Assert.Contains("réinitialisations de contrôleur", controleur.Details);
    }

    [Fact]
    public void Chaque_carte_rappelle_le_total_de_la_periode()
    {
        var r = CasTechInfo();
        Analyser(r);

        foreach (var f in r.Findings.Where(x => x.Code.StartsWith("disk_event")))
            Assert.Contains("10 erreur(s) de stockage sur la période", f.Details);
    }

    [Fact]
    public void Une_seule_nature_reste_une_seule_carte()
    {
        var r = Rapport(
            ("disk", 51, @"Erreur détectée sur \Device\Harddisk0\DR0 lors d'une opération de pagination."),
            ("disk", 51, @"Erreur détectée sur \Device\Harddisk0\DR0 lors d'une opération de pagination."),
            ("storahci", 129, @"Réinitialisation au périphérique \Device\Harddisk0\DR0 émise."));
        r.System.Disks.Add(Nvme(0));
        Analyser(r);

        var carte = Assert.Single(r.Findings, f => f.Code.StartsWith("disk_event"));
        Assert.Equal("disk_event", carte.Code);
        // Pas de phrase de répartition quand il n'y a rien à répartir.
        Assert.DoesNotContain("séparées ici par périphérique", carte.Details);
    }
}
