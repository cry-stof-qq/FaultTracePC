using FaultTracePC.Core;
using FaultTracePC.Core.Analysis;
using Xunit;

namespace FaultTracePC.Tests;

/// <summary>
/// POINT 105, validé le 19/10/2026. Constaté sur un poste du parc : verdict
/// « PILOTE défectueux » tiré d'un unique écran bleu du 09/12/2025, analysé le
/// 19/10/2026 sur 30 jours — une machine saine par ailleurs. Une conclusion dont
/// la dernière trace précède la période reste affichée, mais ne décide plus du
/// verdict.
/// </summary>
[Collection("Langue")]
public class PlantagesAnciensTests
{
    private static DiagnosticReport Rapport(params (DateTime Quand, uint Code)[] plantages)
    {
        var r = new DiagnosticReport
        {
            GeneratedAt = new DateTime(2026, 10, 19, 9, 1, 0),
            ScanPeriodDays = 30,
            System = new SystemSnapshot { MachineName = "POSTE-TEST" },
        };
        int i = 0;
        foreach (var (quand, code) in plantages)
            r.Dumps.Add(new DumpFileInfo
            {
                Path = $@"C:\WINDOWS\Minidump\test-{i++}.dmp",
                Kind = DumpKind.KernelMinidump,
                BugCheckCode = code,
                CrashTimeFromHeader = quand,
                LastWriteTime = quand,
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
    public void Un_ecran_bleu_ancien_ne_decide_plus_du_verdict()
    {
        var r = Rapport((new DateTime(2025, 12, 9, 14, 47, 0), 0x23));
        Analyser(r);

        Assert.NotEqual(FaultCategory.Driver, r.VerdictCategory);
        Assert.DoesNotContain("Cause la plus probable", r.Verdict);
        Assert.Contains("plus anciens que la période analysée (le dernier le 09/12/2025)", r.Verdict);
    }

    [Fact]
    public void L_ecran_bleu_ancien_reste_affiche_et_dit_pourquoi_il_ne_compte_pas()
    {
        var r = Rapport((new DateTime(2025, 12, 9, 14, 47, 0), 0x23));
        Analyser(r);

        var carte = Assert.Single(r.Findings, f => f.Code.StartsWith(RulesEngine.PrefixeCodeArret));
        Assert.Equal(Severity.Critical, carte.Severity);
        Assert.Contains("Antérieur à la période analysée (30 jours)", carte.Details);
    }

    [Fact]
    public void Un_ecran_bleu_de_la_periode_decide_toujours_et_l_ancien_est_signale()
    {
        var r = Rapport((new DateTime(2025, 12, 9, 14, 47, 0), 0x23),
                        (new DateTime(2026, 10, 15, 10, 0, 0), 0x7E));
        Analyser(r);

        Assert.StartsWith("Cause la plus probable", r.Verdict);
        Assert.Equal(FaultCategory.Driver, r.VerdictCategory);
        Assert.Contains("plus anciens que la période analysée", r.Verdict);
    }

    [Fact]
    public void Une_conclusion_sans_date_n_est_jamais_hors_periode()
    {
        var r = new DiagnosticReport { GeneratedAt = new DateTime(2026, 10, 19), ScanPeriodDays = 30 };
        Assert.False(RulesEngine.HorsPeriode(r, new Finding { Severity = Severity.Critical }));
    }
}
