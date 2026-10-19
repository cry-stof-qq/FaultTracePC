using FaultTracePC.Core;
using FaultTracePC.Core.Analysis;
using Xunit;

namespace FaultTracePC.Tests;

/// <summary>
/// POINT 106, validé le 19/10/2026. Deux textes parlaient encore de « Phase 2 » et
/// de « Phase 3 » — des mots de la feuille de route, sans sens pour le lecteur, et
/// faux : l'analyse des vidages et la surveillance existent. Et 0x23, l'écran bleu
/// d'un poste du parc, manquait au catalogue.
/// </summary>
[Collection("Langue")]
public class TextesPerimesTests
{
    [Fact]
    public void Le_code_0x23_est_au_catalogue()
    {
        var e = BugCheckCatalog.Lookup(0x23);
        Assert.NotNull(e);
        Assert.Equal("FAT_FILE_SYSTEM", e!.Name);
        Assert.Equal(FaultCategory.Storage, e.Category);
        Assert.Contains("chkdsk /f /r", e.AdviceFr);
        Assert.Contains("chkdsk /f /r", e.AdviceEn);
    }

    [Fact]
    public void Un_code_inconnu_ne_parle_plus_de_Phase_2()
    {
        var r = new DiagnosticReport
        {
            GeneratedAt = new DateTime(2026, 10, 19, 9, 0, 0),
            ScanPeriodDays = 30,
            System = new SystemSnapshot { MachineName = "POSTE-TEST" },
        };
        var quand = new DateTime(2026, 10, 15, 10, 0, 0);
        // 0x1D4 : un code qui n'est pas au catalogue.
        r.Dumps.Add(new DumpFileInfo { Path = @"C:\WINDOWS\Minidump\test.dmp", Kind = DumpKind.KernelMinidump, BugCheckCode = 0x1D4, CrashTimeFromHeader = quand, LastWriteTime = quand });

        var initial = Lang.Current;
        try { Lang.Apply(AppLanguage.French); new RulesEngine().Analyze(r); }
        finally { Lang.Apply(initial); }

        var carte = Assert.Single(r.Findings, f => f.Code.StartsWith(RulesEngine.PrefixeCodeArret));
        Assert.DoesNotContain("Phase", carte.Recommendation);
        Assert.Contains("Debugging Tools for Windows", carte.Recommendation);
    }
}
