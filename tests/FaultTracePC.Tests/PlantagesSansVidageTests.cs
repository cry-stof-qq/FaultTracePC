using FaultTracePC.Core;
using FaultTracePC.Core.Analysis;
using Xunit;

namespace FaultTracePC.Tests;

/// <summary>
/// Points 50 et 52 de la feuille de route. Constatés le 14/09/2026 sur
/// MLEAR-031-2024 : le rapport annonçait « Aucun BSOD détecté sur la période » et
/// « Pas de panne critique » sur une machine qui avait planté quinze fois. Les
/// événements Kernel-Power 41 portaient pourtant un BugcheckCode renseigné, et cinq
/// arrêts inattendus figuraient dans le tableau des événements sans peser sur rien.
/// </summary>
[Collection("Langue")]
public class PlantagesSansVidageTests
{
    private static DiagnosticReport Rapport() => new()
    {
        GeneratedAt = new DateTime(2026, 9, 14, 10, 57, 0),
        ScanPeriodDays = 30,
        System = new SystemSnapshot { MachineName = "POSTE-TEST" },
    };

    private static void Analyser(DiagnosticReport r)
    {
        var initial = Lang.Current;
        try { Lang.Apply(AppLanguage.French); new RulesEngine().Analyze(r); }
        finally { Lang.Apply(initial); }
    }

    /// <summary>Kernel-Power 41 : le code est écrit en DÉCIMAL dans le XML de l'événement.</summary>
    private static void AjouterKernelPower(DiagnosticReport r, string codeDecimal, DateTime quand)
    {
        var e = new WinEvent
        {
            Category = EventCategory.PowerLoss,
            Provider = "Microsoft-Windows-Kernel-Power",
            EventId = 41,
            TimeLocal = quand,
            Message = "Le systeme a redemarre sans s'etre arrete correctement au prealable.",
        };
        e.Extracted["BugcheckCode"] = codeDecimal;
        r.Events.Add(e);
    }

    [Fact]
    public void Un_plantage_sans_vidage_devient_un_ecran_bleu_nomme()
    {
        var r = Rapport();
        // 122 en décimal = 0x7A = KERNEL_DATA_INPAGE_ERROR.
        AjouterKernelPower(r, "122", new DateTime(2026, 9, 10, 8, 15, 0));

        Analyser(r);

        var incident = Assert.Single(r.Bsods);
        Assert.Equal(0x7Au, incident.BugCheckCode);
        Assert.Equal("KERNEL_DATA_INPAGE_ERROR", incident.BugCheckName);
        // Aucun fichier de vidage : c'est bien le journal qui a parlé.
        Assert.Null(incident.DumpPath);
        Assert.Contains(incident.Sources, x => x.Contains("Kernel-Power"));
    }

    [Fact]
    public void Un_code_nul_reste_une_coupure_et_pas_un_ecran_bleu()
    {
        var r = Rapport();
        AjouterKernelPower(r, "0", new DateTime(2026, 9, 11, 16, 2, 0));
        AjouterKernelPower(r, "0", new DateTime(2026, 9, 12, 16, 5, 0));

        Analyser(r);

        // Un bouton maintenu ou une coupure secteur n'est pas un plantage de Windows.
        Assert.Empty(r.Bsods);
        Assert.Contains(r.Findings, f => f.Title.Contains("coupure"));
    }

    [Fact]
    public void Un_vidage_et_l_evenement_41_du_meme_plantage_ne_comptent_qu_une_fois()
    {
        var r = Rapport();
        var quand = new DateTime(2026, 9, 10, 8, 15, 0);
        r.Dumps.Add(new DumpFileInfo
        {
            Path = @"C:\WINDOWS\Minidump\091026-0001-01.dmp",
            Kind = DumpKind.KernelMinidump,
            BugCheckCode = 0x7A,
            CrashTimeFromHeader = quand,
            LastWriteTime = quand,
        });
        AjouterKernelPower(r, "122", quand.AddMinutes(3));

        Analyser(r);

        var incident = Assert.Single(r.Bsods);
        Assert.Equal(2, incident.Sources.Count);
    }

    [Fact]
    public void Des_arrets_inattendus_empechent_le_verdict_de_rassurer()
    {
        var r = Rapport();
        for (int i = 0; i < 3; i++)
            r.Events.Add(new WinEvent
            {
                Category = EventCategory.UnexpectedShutdown,
                Provider = "EventLog",
                EventId = 6008,
                TimeLocal = new DateTime(2026, 9, 5 + i, 14, 0, 0),
                Message = "L'arret systeme precedent a 14:00:00 n'etait pas prevu.",
            });

        Analyser(r);

        Assert.Contains(r.Findings, f => f.Title.Contains("arrêt(s) inattendu"));
        Assert.DoesNotContain("Pas de panne critique", r.Verdict);
        Assert.DoesNotContain("Système sain", r.Verdict);
        Assert.Contains("anormalement", r.Verdict);
    }

    [Fact]
    public void Une_machine_reellement_saine_reste_annoncee_saine()
    {
        var r = Rapport();

        Analyser(r);

        Assert.Contains("Système sain", r.Verdict);
    }
}
