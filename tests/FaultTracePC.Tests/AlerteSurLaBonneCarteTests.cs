using FaultTracePC.Core;
using FaultTracePC.Core.Analysis;
using Xunit;

namespace FaultTracePC.Tests;

/// <summary>
/// Constaté le 18/09/2026 sur le poste de l'auteur. L'alerte de la surveillance temps réel
/// — « une défaillance a été détectée dans la structure du système de fichiers sur le
/// volume D: » — était collée à la carte du PORT DE CONTRÔLEUR SATA.
///
/// La faute n'est pas dans la fusion des doublons : c'est le point 65 qui a changé le
/// sens de l'identifiant « disk_event ». Il désignait LA carte des erreurs disque, il
/// désigne maintenant la première nature présente. Le mélange que le point 65 devait
/// faire cesser était revenu par une autre porte.
/// </summary>
[Collection("Langue")]
public class AlerteSurLaBonneCarteTests
{
    private static DiagnosticReport CasReel()
    {
        var r = new DiagnosticReport
        {
            GeneratedAt = new DateTime(2026, 9, 18, 10, 6, 0),
            ScanPeriodDays = 30,
            System = new SystemSnapshot { MachineName = "POSTE-TEST" },
        };
        r.System.Disks.Add(new DiskInfo { Model = "RPEYJ1T24MML1AWX", Index = 0, InterfaceType = "SCSI", Health = DiskHealth.Healthy });

        // Un port de contrôleur : c'est la première nature de l'ordre de priorité
        // présente ici, donc celle qui portera « disk_event ».
        for (int i = 0; i < 3; i++)
            r.Events.Add(new WinEvent
            {
                Category = EventCategory.DiskError, Provider = "storahci", EventId = 129,
                TimeLocal = new DateTime(2026, 9, 12, 8, 0, 0).AddMinutes(i),
                Message = @"Une réinitialisation au périphérique, \Device\RaidPort1, a été émise.",
            });

        // Un volume, sans périphérique nommé.
        r.Events.Add(new WinEvent
        {
            Category = EventCategory.DiskError, Provider = "Ntfs", EventId = 55,
            TimeLocal = new DateTime(2026, 9, 14, 11, 8, 0),
            Message = "Une défaillance a été détectée dans la structure du système de fichiers sur le volume D:.",
        });

        // Et la même chose, vue par la surveillance temps réel.
        r.Flight.Alerts.Add(new PreventiveAlert
        {
            Time = new DateTime(2026, 9, 14, 11, 8, 0),
            RuleId = "disk_event",
            Level = "warn",
            Title = "Erreur d'entrée/sortie sur un disque",
            Details = "Windows vient d'enregistrer une erreur d'entrée/sortie sur un disque : Une défaillance a été détectée dans la structure du système de fichiers sur le volume D:.",
            Recommendation = "Vérifier le disque concerné.",
        });
        return r;
    }

    private static void Analyser(DiagnosticReport r)
    {
        var initial = Lang.Current;
        try { Lang.Apply(AppLanguage.French); new RulesEngine().Analyze(r); }
        finally { Lang.Apply(initial); }
    }

    [Fact]
    public void L_alerte_qui_cite_un_volume_ne_va_pas_sur_la_carte_du_controleur()
    {
        var r = CasReel();
        Analyser(r);

        var controleur = r.Findings.First(f => f.Code == "disk_event");
        Assert.Contains("RaidPort1", controleur.Details);
        // Le volume D: n'a rien à faire sur la carte d'un port SATA.
        Assert.DoesNotContain("volume D:", controleur.Details);
    }

    [Fact]
    public void L_alerte_rejoint_la_carte_du_peripherique_qu_elle_cite()
    {
        var r = CasReel();
        Analyser(r);

        var volume = r.Findings.First(f => f.Code == "disk_event_sans_peripherique");
        Assert.Contains("volume D:", volume.Details);
        // Les deux chemins se confirment l'un l'autre : c'est tout l'intérêt de la fusion.
        Assert.Contains("deux chemins indépendants", volume.Details);
    }

    [Fact]
    public void Une_seule_nature_laisse_l_alerte_rejoindre_la_carte_unique()
    {
        // Sans séparation, le comportement d'avant le point 65 doit être intact.
        var r = new DiagnosticReport
        {
            GeneratedAt = new DateTime(2026, 9, 18, 10, 6, 0),
            ScanPeriodDays = 30,
            System = new SystemSnapshot { MachineName = "POSTE-TEST" },
        };
        for (int i = 0; i < 3; i++)
            r.Events.Add(new WinEvent
            {
                Category = EventCategory.DiskError, Provider = "Ntfs", EventId = 55,
                TimeLocal = new DateTime(2026, 9, 14, 11, 8, 0).AddMinutes(i),
                Message = "Défaillance détectée sur le volume D:.",
            });
        r.Flight.Alerts.Add(new PreventiveAlert
        {
            Time = new DateTime(2026, 9, 14, 11, 8, 0),
            RuleId = "disk_event",
            Level = "warn",
            Title = "Erreur d'entrée/sortie sur un disque",
            Details = "Défaillance détectée sur le volume D:.",
            Recommendation = "Vérifier le disque concerné.",
        });

        Analyser(r);

        var carte = Assert.Single(r.Findings, f => f.Code.StartsWith("disk_event"));
        Assert.Contains("deux chemins indépendants", carte.Details);
    }
}
