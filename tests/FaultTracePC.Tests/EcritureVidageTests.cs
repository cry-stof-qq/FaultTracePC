using FaultTracePC.Core;
using FaultTracePC.Core.Analysis;
using Xunit;

namespace FaultTracePC.Tests;

/// <summary>
/// Point 51 de la feuille de route. Constaté le 14/09/2026 sur POSTE-ELEVE-01 puis
/// le 17/09 sur POSTE-TEMOIN : les messages volmgr qui signalent l'échec d'écriture du
/// vidage de plantage étaient comptés parmi les « erreurs disque » et menaient à un
/// contrôle de disque. Ils disent tout autre chose — le disque va bien, mais les
/// plantages ne laissent aucune trace exploitable.
/// </summary>
[Collection("Langue")]
public class EcritureVidageTests
{
    /// <summary>Les assertions portent sur des textes : la langue doit être fixée.</summary>
    private static void Analyser(DiagnosticReport r)
    {
        var initial = Lang.Current;
        try { Lang.Apply(AppLanguage.French); new RulesEngine().Analyze(r); }
        finally { Lang.Apply(initial); }
    }

    private static DiagnosticReport Rapport()
    {
        return new DiagnosticReport
        {
            GeneratedAt = new DateTime(2026, 9, 17, 17, 38, 0),
            ScanPeriodDays = 30,
            System = new SystemSnapshot { MachineName = "POSTE-TEST" },
        };
    }

    private static void AjouterVolmgr(DiagnosticReport r, params int[] ids)
    {
        foreach (var id in ids)
            r.Events.Add(new WinEvent
            {
                Category = EventCategory.DiskError,
                Provider = "volmgr",
                EventId = id,
                Message = "Echec d'initialisation du vidage sur incident.",
                TimeLocal = new DateTime(2026, 9, 14, 10, 57, 0),
            });
    }

    [Fact]
    public void Les_echecs_d_ecriture_de_vidage_ne_sont_plus_des_erreurs_disque()
    {
        var r = Rapport();
        AjouterVolmgr(r, 46, 161, 162);

        Analyser(r);

        // La conclusion existe, et elle dit ce que c'est.
        var f = r.Findings.First(x => x.Title.Contains("vidage de plantage"));
        Assert.Contains("PAS DES ERREURS DE DISQUE", f.Details);

        // Et surtout : plus aucune conclusion « erreurs disque » bâtie sur ces messages.
        Assert.DoesNotContain(r.Findings, x => x.Code == "disk_event");
    }

    [Fact]
    public void Les_vraies_erreurs_disque_restent_comptees_sans_les_volmgr_de_vidage()
    {
        var r = Rapport();
        AjouterVolmgr(r, 46, 161, 162);
        for (int i = 0; i < 3; i++)
            r.Events.Add(new WinEvent
            {
                Category = EventCategory.DiskError,
                Provider = "disk",
                EventId = 51,
                Message = @"Erreur détectée sur \Device\Harddisk0\DR0 lors d'une opération de pagination.",
                TimeLocal = new DateTime(2026, 9, 15, 9, 0, 0),
            });
        r.System.Disks.Add(new DiskInfo { Model = "ST1000DM014-2UB10D", Index = 0, InterfaceType = "IDE", Health = DiskHealth.Healthy });

        Analyser(r);

        var disque = r.Findings.First(x => x.Code == "disk_event");
        // Trois, pas six : les volmgr de vidage ne gonflent plus le compte.
        Assert.Contains("(3)", disque.Title);
        Assert.Contains(r.Findings, x => x.Title.Contains("vidage de plantage"));
    }

    [Fact]
    public void Sans_plantage_orphelin_le_reglage_reste_une_information()
    {
        var r = Rapport();
        AjouterVolmgr(r, 46);

        Analyser(r);

        var f = r.Findings.First(x => x.Title.Contains("vidage de plantage"));
        Assert.Equal(Severity.Info, f.Severity);
    }

    [Fact]
    public void Un_ecran_bleu_sans_fichier_d_analyse_rend_le_reglage_urgent()
    {
        var r = Rapport();
        AjouterVolmgr(r, 46, 161);
        // Un événement BugCheck 1001 sans vidage associé : le plantage est réel,
        // mais il ne laisse rien à analyser.
        var e = new WinEvent
        {
            Category = EventCategory.Bsod,
            Provider = "Microsoft-Windows-WER-SystemErrorReporting",
            EventId = 1001,
            TimeLocal = new DateTime(2026, 9, 16, 8, 30, 0),
            Message = "Le systeme a redemarre suite a une erreur.",
        };
        e.Extracted["BugCheckCode"] = "0x0000007a";
        r.Events.Add(e);

        Analyser(r);

        var f = r.Findings.First(x => x.Title.Contains("vidage de plantage"));
        Assert.Equal(Severity.Warning, f.Severity);
        Assert.Contains("aucun fichier d", f.Details);
    }
}
