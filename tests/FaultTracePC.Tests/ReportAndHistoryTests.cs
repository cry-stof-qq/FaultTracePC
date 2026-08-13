using FaultTracePC.Core;
using FaultTracePC.Core.Analysis;
using FaultTracePC.Core.Report;
using Xunit;

namespace FaultTracePC.Tests;

/// <summary>
/// Génération du rapport et résumé d'historique : on vérifie surtout qu'aucune
/// donnée sensible n'est mal échappée et que le rapport reste exploitable.
/// </summary>
public class ReportAndHistoryTests
{
    private static DiagnosticReport SampleReport()
    {
        var r = new DiagnosticReport
        {
            ScanPeriodDays = 30,
            System = new SystemSnapshot
            {
                MachineName = "POSTE-01",
                Os = new OsInfo { Caption = "Windows 11", TotalVisibleMemoryKB = 16 * 1024 * 1024, FreePhysicalMemoryKB = 8 * 1024 * 1024 },
                Cpu = new CpuInfo { Name = "AMD Ryzen 7", Cores = 8, LogicalProcessors = 16 },
            },
            Dumps =
            [
                new DumpFileInfo
                {
                    Path = @"C:\Windows\Minidump\010126-1-01.dmp",
                    Kind = DumpKind.KernelMinidump,
                    BugCheckCode = 0x50,
                    BugCheckParameters = [1, 2, 3, 4],
                    CrashTimeFromHeader = DateTime.Now.AddDays(-2),
                    LastWriteTime = DateTime.Now.AddDays(-2),
                    DeepAnalyzed = true,
                    FaultingModule = "bindflt.sys",
                    StackExcerpt = "nt!KeBugCheckEx\nbindflt!memcpy+0x104",
                },
            ],
        };
        new RulesEngine().Analyze(r);
        return r;
    }

    [Fact]
    public void Le_rapport_html_est_complet_et_bien_forme()
    {
        var html = HtmlReportGenerator.Generate(SampleReport());

        Assert.StartsWith("<!DOCTYPE html>", html);
        Assert.EndsWith("</html>", html);
        Assert.Contains("POSTE-01", html);
        Assert.Contains("bindflt.sys", html);
        Assert.Contains("Conclusions du diagnostic", html);
        // Le mode simple est actif par défaut (l'essentiel d'abord).
        Assert.Contains("<body class=\"simple\">", html);
    }

    [Fact]
    public void Le_contenu_dynamique_est_echappe()
    {
        var r = SampleReport();
        r.System.MachineName = "<script>alert('x')</script>";

        var html = HtmlReportGenerator.Generate(r);

        Assert.DoesNotContain("<script>alert", html);
        Assert.Contains("&lt;script&gt;", html);
    }

    [Fact]
    public void Un_script_de_reparation_est_propose_quand_il_y_a_un_probleme()
    {
        var r = SampleReport();
        Assert.True(RepairScriptGenerator.IsRepairable(r));

        var script = RepairScriptGenerator.Generate(r);
        Assert.Contains("#Requires -RunAsAdministrator", script);
        Assert.Contains("function Ask", script);       // toute action modifiante est confirmée
        Assert.Contains("Start-Transcript", script);   // et journalisée
    }

    [Fact]
    public void Aucun_script_pour_une_machine_saine()
    {
        var r = new DiagnosticReport { ScanPeriodDays = 30 };
        new RulesEngine().Analyze(r);

        Assert.False(RepairScriptGenerator.IsRepairable(r));
    }

    [Fact]
    public void Le_script_echappe_les_apostrophes_powershell()
    {
        var r = SampleReport();
        r.System.Drivers =
        [
            new DriverInfo
            {
                Name = "x", DisplayName = "Pilote d'un éditeur", CompanyName = "L'éditeur",
                Path = @"C:\Windows\System32\drivers\x.sys", State = "Running",
                FileDate = DateTime.Now.AddYears(-9), IsMicrosoft = false,
            },
        ];

        var script = RepairScriptGenerator.Generate(r);

        // Les apostrophes doivent être doublées pour PowerShell, jamais laissées seules.
        Assert.DoesNotContain("- x.sys — L'éditeur", script);
        Assert.Contains("L''éditeur", script);
    }

    [Fact]
    public void Le_resume_dhistorique_capture_lessentiel()
    {
        var summary = ScanHistory.Summarize(SampleReport());

        Assert.Single(summary.Bsods);
        Assert.Equal("bindflt.sys", summary.Bsods[0].Driver);
        Assert.NotEmpty(summary.CriticalFindings);
    }

    [Theory]
    [InlineData(0UL, "0 o")]
    [InlineData(1024UL, "1 Ko")]
    [InlineData(1536UL, "1,5 Ko")]
    [InlineData(1073741824UL, "1 Go")]
    public void Les_tailles_sont_lisibles(ulong bytes, string expectedFragment)
    {
        var text = RulesEngine.FormatBytes(bytes);
        // La virgule décimale dépend de la culture : on compare sur le nombre + l'unité.
        Assert.Equal(expectedFragment.Replace(',', '.'), text.Replace(',', '.'));
    }
}
