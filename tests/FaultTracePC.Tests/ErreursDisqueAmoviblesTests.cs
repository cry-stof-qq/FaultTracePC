using FaultTracePC.Core;
using FaultTracePC.Core.Analysis;
using Xunit;

namespace FaultTracePC.Tests;

/// <summary>
/// Point 60 de la feuille de route. Constaté le 17/09/2026 sur PC-W10-11 : douze
/// « erreurs disque » réunies sous un seul chiffre, réparties entre une clé USB, un
/// volume et un port de contrôleur — puis une recommandation qui commençait par la
/// gestion d'alimentation du lien PCI Express et les câbles SATA. Deux conseils sans
/// le moindre sens pour une clé USB.
/// </summary>
public class ErreursDisqueAmoviblesTests
{
    private static DiagnosticReport Rapport(params (string Provider, int Id, string Message)[] events)
    {
        var r = new DiagnosticReport
        {
            GeneratedAt = new DateTime(2026, 9, 17, 17, 38, 0),
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
                TimeLocal = new DateTime(2026, 9, 14),
            });
        return r;
    }

    private static DiskInfo CleUsb(int index) => new()
    {
        Model = "Generic Flash Disk USB Device",
        Index = index,
        InterfaceType = "USB",
        Health = DiskHealth.Healthy,
    };

    private static DiskInfo DisqueFixeSata(int index) => new()
    {
        Model = "ST1000DM014-2UB10D",
        Index = index,
        InterfaceType = "IDE",
        MediaType = "HDD",
        Health = DiskHealth.Healthy,
    };

    private static Finding Conclusion(DiagnosticReport r)
    {
        new RulesEngine().Analyze(r);
        return r.Findings.First(f => f.Code == "disk_event");
    }

    [Fact]
    public void Une_cle_usb_seule_en_cause_ne_declenche_pas_les_conseils_machine()
    {
        var r = Rapport(
            ("disk", 51, @"Erreur détectée sur \Device\Harddisk2\DR3 lors d'une opération de pagination."),
            ("Ntfs", 55, @"Défaillance de structure sur \Device\Harddisk2\DR3."),
            ("storahci", 129, @"Réinitialisation au périphérique \Device\Harddisk2\DR3 émise."));
        r.System.Disks.Add(CleUsb(2));

        var f = Conclusion(r);

        Assert.Contains("AMOVIBLES", f.Details);
        Assert.Contains("Generic Flash Disk USB Device", f.Recommendation);
        // Les gestes qui n'ont aucun sens sur une clé USB doivent avoir disparu.
        Assert.DoesNotContain("gestion d'alimentation des liens", f.Recommendation);
        Assert.DoesNotContain("câble de données", f.Recommendation);
        Assert.DoesNotContain("firmware du SSD", f.Recommendation);
        // Et une clé fatiguée n'est pas un avertissement sur la machine.
        Assert.Equal(Severity.Info, f.Severity);
    }

    [Fact]
    public void Un_disque_fixe_egalement_en_cause_ramene_les_conseils_machine()
    {
        var r = Rapport(
            ("disk", 51, @"Erreur détectée sur \Device\Harddisk2\DR3 lors d'une opération de pagination."),
            ("storahci", 129, @"Réinitialisation au périphérique \Device\Harddisk0\DR0 émise."),
            ("Ntfs", 55, @"Défaillance de structure sur \Device\Harddisk0\DR0."));
        r.System.Disks.Add(CleUsb(2));
        r.System.Disks.Add(DisqueFixeSata(0));

        // POINT 65 (18/09/2026) : deux natures de support, donc deux cartes. Le point 60
        // avait séparé les CONSEILS dans un texte commun ; les additionner sous un seul
        // titre et une seule gravité restait trompeur — une clé fatiguée et un disque
        // système ne se traitent ni avec les mêmes gestes ni avec la même urgence.
        new RulesEngine().Analyze(r);

        var machine = r.Findings.First(f => f.Code == "disk_event");
        Assert.Contains("gestion d'alimentation des liens", machine.Recommendation);
        Assert.Contains("câble de données", machine.Recommendation);
        Assert.Equal(Severity.Warning, machine.Severity);
        // Les gestes machine ne parlent plus de la clé : elle a sa propre carte.
        Assert.DoesNotContain("Generic Flash Disk USB Device", machine.Recommendation);

        // La clé reste signalée, avec son conseil à elle et sans alarmer sur la machine.
        var cle = r.Findings.First(f => f.Code == "disk_event_amovible");
        Assert.Contains("Generic Flash Disk USB Device", cle.Recommendation);
        Assert.DoesNotContain("gestion d'alimentation des liens", cle.Recommendation);
        Assert.Equal(Severity.Info, cle.Severity);
    }

    [Fact]
    public void Sans_support_amovible_rien_ne_change()
    {
        var r = Rapport(
            ("storahci", 129, @"Réinitialisation au périphérique \Device\Harddisk0\DR0 émise."),
            ("storahci", 129, @"Réinitialisation au périphérique \Device\Harddisk0\DR0 émise."),
            ("disk", 51, @"Erreur détectée sur \Device\Harddisk0\DR0 lors d'une opération de pagination."));
        r.System.Disks.Add(DisqueFixeSata(0));

        var f = Conclusion(r);

        Assert.DoesNotContain("AMOVIBLES", f.Details);
        Assert.Contains("gestion d'alimentation des liens", f.Recommendation);
        Assert.Equal(Severity.Warning, f.Severity);
    }
}
