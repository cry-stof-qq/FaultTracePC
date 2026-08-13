using FaultTracePC.Core;
using FaultTracePC.Core.Analysis;
using Xunit;

namespace FaultTracePC.Tests;

/// <summary>
/// Le moteur de corrélation : c'est lui qui décide du verdict affiché à
/// l'utilisateur. Ces tests figent les comportements appris sur des cas réels
/// (dédoublonnage des dumps, priorité au pilote identifié, piste virtualisation
/// avant RAM défectueuse…) pour qu'une évolution des règles ne les casse pas.
/// </summary>
public class DiagnosticRulesTests
{
    /// <summary>Construit un rapport minimal analysable.</summary>
    private static DiagnosticReport NewReport(int days = 30) => new()
    {
        ScanPeriodDays = days,
        System = new SystemSnapshot
        {
            MachineName = "TEST-PC",
            Os = new OsInfo
            {
                Caption = "Windows 11", TotalVisibleMemoryKB = 32 * 1024 * 1024,
                FreePhysicalMemoryKB = 20 * 1024 * 1024,
                TotalVirtualMemoryKB = 40 * 1024 * 1024, FreeVirtualMemoryKB = 25 * 1024 * 1024,
            },
        },
    };

    private static DumpFileInfo Dump(DateTime time, uint code, string? module = null, DumpKind kind = DumpKind.KernelMinidump) => new()
    {
        Path = $@"C:\Windows\Minidump\{time:MMddyy}-1-01.dmp",
        Kind = kind,
        BugCheckCode = code,
        BugCheckParameters = [0, 0, 0, 0],
        CrashTimeFromHeader = time,
        LastWriteTime = time,
        DeepAnalyzed = module is not null,
        FaultingModule = module,
    };

    // ------------------------------------------------------------------

    [Fact]
    public void Un_meme_crash_vu_dans_deux_dumps_ne_compte_quune_fois()
    {
        // Cas réel : le minidump ET MEMORY.DMP décrivent le même écran bleu.
        var t = DateTime.Now.AddDays(-3);
        var r = NewReport();
        r.Dumps = [Dump(t, 0x50), Dump(t.AddSeconds(30), 0x50, kind: DumpKind.FullMemoryDump)];

        new RulesEngine().Analyze(r);

        Assert.Single(r.Bsods);
        Assert.Equal(2, r.Bsods[0].Sources.Count);   // les deux sources sont conservées
    }

    [Fact]
    public void Deux_crashs_distincts_restent_deux_incidents()
    {
        var r = NewReport();
        r.Dumps = [Dump(DateTime.Now.AddDays(-2), 0x50), Dump(DateTime.Now.AddDays(-20), 0x50)];

        new RulesEngine().Analyze(r);

        Assert.Equal(2, r.Bsods.Count);
    }

    [Fact]
    public void Le_pilote_identifie_par_lanalyse_symbolique_devient_le_verdict()
    {
        var r = NewReport();
        r.Dumps =
        [
            Dump(DateTime.Now.AddDays(-2), 0x50, "bindflt.sys"),
            Dump(DateTime.Now.AddDays(-20), 0x50, "bindflt.sys"),
        ];

        new RulesEngine().Analyze(r);

        Assert.Equal(FaultCategory.Driver, r.VerdictCategory);
        Assert.Contains("bindflt.sys", r.Verdict);
        Assert.Contains(r.Findings, f => f.Title.Contains("Pilote fautif identifié"));
    }

    [Fact]
    public void Pas_de_suspicion_RAM_quand_le_pilote_fautif_est_connu()
    {
        // Régression : un 0x50 avec pilote nommé ne doit plus faire accuser la RAM.
        var r = NewReport();
        r.Dumps =
        [
            Dump(DateTime.Now.AddDays(-2), 0x50, "bindflt.sys"),
            Dump(DateTime.Now.AddDays(-20), 0x50, "bindflt.sys"),
        ];

        new RulesEngine().Analyze(r);

        Assert.DoesNotContain(r.Findings, f => f.Title.Contains("RAM défectueuse"));
    }

    [Fact]
    public void Sans_pilote_identifie_les_BSOD_memoire_evoquent_la_RAM()
    {
        var r = NewReport();
        r.Dumps = [Dump(DateTime.Now.AddDays(-2), 0x1A), Dump(DateTime.Now.AddDays(-9), 0x50)];

        new RulesEngine().Analyze(r);

        Assert.Contains(r.Findings, f => f.Category == FaultCategory.Memory);
    }

    [Fact]
    public void La_virtualisation_gourmande_est_signalee_et_nuance_la_piste_RAM()
    {
        var r = NewReport();
        r.Dumps = [Dump(DateTime.Now.AddDays(-2), 0x1A), Dump(DateTime.Now.AddDays(-9), 0x50)];
        r.Processes =
        [
            new ProcessInfo { Name = "vmmem", Pid = 1, PrivateBytes = 12L * 1024 * 1024 * 1024 },
            new ProcessInfo { Name = "explorer", Pid = 2, PrivateBytes = 120L * 1024 * 1024 },
        ];

        new RulesEngine().Analyze(r);

        // Conclusion dédiée à la virtualisation (la conclusion RAM mentionne aussi le
        // mot « virtualisation » : on cible donc précisément celle-ci).
        var vm = Assert.Single(r.Findings, f => f.Category == FaultCategory.Software &&
                                                f.Title.StartsWith("La virtualisation réserve", StringComparison.Ordinal));
        Assert.Contains("wslconfig", vm.Recommendation, StringComparison.OrdinalIgnoreCase);

        // La conclusion « BSOD mémoire récurrents » doit exister avec une confiance abaissée.
        var ram = r.Findings.FirstOrDefault(f => f.Category == FaultCategory.Memory &&
                                                 f.Title.Contains("BSOD mémoire récurrents", StringComparison.Ordinal));
        Assert.NotNull(ram);
        Assert.Equal(Confidence.Low, ram!.Confidence);
    }

    [Fact]
    public void Les_erreurs_materielles_WHEA_donnent_un_verdict_materiel()
    {
        var r = NewReport();
        r.Events = Enumerable.Range(0, 6).Select(i => new WinEvent
        {
            TimeLocal = DateTime.Now.AddDays(-i),
            Category = EventCategory.Whea,
            Provider = "Microsoft-Windows-WHEA-Logger",
            EventId = 18,
        }).ToList();

        new RulesEngine().Analyze(r);

        Assert.Equal(FaultCategory.Hardware, r.VerdictCategory);
    }

    [Fact]
    public void Saturation_memoire_detectee_par_Windows_est_classee_logicielle()
    {
        var r = NewReport();
        r.Events =
        [
            new WinEvent
            {
                TimeLocal = DateTime.Now.AddDays(-1),
                Category = EventCategory.ResourceExhaustion,
                Provider = "Microsoft-Windows-Resource-Exhaustion-Detector",
                EventId = 2004,
                Extracted = { ["Processus"] = "vmmem.exe" },
            },
            new WinEvent
            {
                TimeLocal = DateTime.Now.AddDays(-2),
                Category = EventCategory.ResourceExhaustion,
                Provider = "Microsoft-Windows-Resource-Exhaustion-Detector",
                EventId = 2004,
                Extracted = { ["Processus"] = "vmmem.exe" },
            },
        ];

        new RulesEngine().Analyze(r);

        var finding = Assert.Single(r.Findings, f => f.Title.Contains("Mémoire saturée"));
        Assert.Equal(FaultCategory.Software, finding.Category);
        Assert.Contains("vmmem.exe", finding.Details);
    }

    [Fact]
    public void Machine_saine_produit_un_verdict_rassurant()
    {
        var r = NewReport();

        new RulesEngine().Analyze(r);

        Assert.Equal(FaultCategory.None, r.VerdictCategory);
        Assert.Contains("sain", r.Verdict, StringComparison.OrdinalIgnoreCase);
        Assert.All(r.Findings, f => Assert.Equal(Severity.Info, f.Severity));
    }

    [Fact]
    public void Un_crash_hors_periode_est_signale_comme_incomplet()
    {
        var r = NewReport(days: 7);
        r.Dumps = [Dump(DateTime.Now.AddDays(-40), 0x50)];

        new RulesEngine().Analyze(r);

        Assert.Contains(r.Findings, f => f.Title.Contains("antérieurs à la période"));
    }

    [Fact]
    public void Une_surchauffe_relevee_avant_le_crash_donne_une_conclusion_materielle()
    {
        var crash = DateTime.Now.AddDays(-1);
        var r = NewReport();
        r.Dumps = [Dump(crash, 0x101)];
        r.Flight = new FlightInfo
        {
            JournalFound = true,
            Contexts =
            [
                new FlightCrashContext
                {
                    CrashTime = crash,
                    Samples =
                    [
                        new FlightSample { Time = crash.AddSeconds(-20), Kind = "s", CpuTemp = 88, CpuLoad = 95 },
                        new FlightSample { Time = crash.AddSeconds(-10), Kind = "s", CpuTemp = 97, CpuLoad = 99 },
                    ],
                },
            ],
        };

        new RulesEngine().Analyze(r);

        Assert.Contains(r.Findings, f => f.Title.Contains("SURCHAUFFE") && f.Category == FaultCategory.Hardware);
    }

    [Fact]
    public void Les_alertes_preventives_deviennent_des_conclusions()
    {
        var r = NewReport();
        r.Flight = new FlightInfo
        {
            JournalFound = true,
            Alerts =
            [
                new PreventiveAlert
                {
                    Time = DateTime.Now.AddHours(-2), RuleId = "cpu_temp", Level = "crit",
                    Title = "Température du processeur élevée : 96 °C",
                    Details = "…", Recommendation = "Dépoussiérer", Value = 96,
                },
            ],
        };

        new RulesEngine().Analyze(r);

        var f = Assert.Single(r.Findings, x => x.Title.Contains("Alerte préventive"));
        Assert.Equal(Severity.Critical, f.Severity);
        Assert.Equal(FaultCategory.Hardware, f.Category);
    }

    [Fact]
    public void Les_conclusions_sont_triees_du_plus_grave_au_moins_grave()
    {
        var r = NewReport();
        r.Dumps = [Dump(DateTime.Now.AddDays(-1), 0x124)];
        r.Events =
        [
            new WinEvent { TimeLocal = DateTime.Now.AddDays(-1), Category = EventCategory.Whea, Provider = "WHEA", EventId = 18 },
        ];
        r.System.Volumes = [new VolumeInfo { Letter = "C:", SizeBytes = 500_000_000_000, FreeBytes = 10_000_000_000 }];

        new RulesEngine().Analyze(r);

        var severities = r.Findings.Select(f => (int)f.Severity).ToList();
        Assert.Equal(severities.OrderBy(s => s), severities);
    }
}
