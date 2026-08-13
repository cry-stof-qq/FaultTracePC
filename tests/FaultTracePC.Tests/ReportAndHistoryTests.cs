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

    // ==================================================================
    // Régression 1.1 : « je n'ai rien mesuré » ne doit JAMAIS s'afficher
    // comme « tout va bien ». Un objet SMART vide produisait une ligne de
    // tirets indiscernable d'un disque sain, et l'historique enregistrait
    // BadSectors = 0 comme s'il s'agissait d'une mesure.
    // ==================================================================

    [Fact]
    public void SmartInfo_Vide_NAPasDeDonnees()
    {
        Assert.False(new SmartInfo().HasData);
    }

    [Theory]
    [InlineData("temperature")]
    [InlineData("usure")]
    [InlineData("secteurs")]
    [InlineData("crc")]
    public void SmartInfo_UnSeulCompteurSuffitAValoirMesure(string champ)
    {
        var s = new SmartInfo();
        switch (champ)
        {
            case "temperature": s.TemperatureC = 29; break;
            case "usure": s.SsdLifeLeftPercent = 100; break;
            case "secteurs": s.ReallocatedSectors = 0; break;
            case "crc": s.UdmaCrcErrors = 0; break;
        }
        Assert.True(s.HasData);
    }

    [Fact]
    public void SmartInfo_ZeroMesureEstUneDonnee_PasUneAbsence()
    {
        // Un compteur qui vaut 0 est une VRAIE mesure (disque sain) ; il doit
        // être distingué d'un compteur absent, qui ne dit rien du tout.
        var mesure = new SmartInfo { ReallocatedSectors = 0, PendingSectors = 0, UncorrectableSectors = 0 };
        Assert.True(mesure.HasData);
        Assert.Equal(0UL, mesure.BadSectors);

        var absence = new SmartInfo();
        Assert.False(absence.HasData);
        // BadSectors vaut aussi 0 par construction : c'est précisément pourquoi
        // il ne faut jamais se fier à lui seul pour décider d'afficher une ligne.
        Assert.Equal(0UL, absence.BadSectors);
    }

    [Fact]
    public void RapportHtml_SansMesureSmart_DitQueRienNaEteLu()
    {
        var r = SampleReport();
        r.System.Disks.Clear();
        r.System.Disks.Add(new DiskInfo { Model = "RPEYJ1T24MML1AWX", MediaType = "SSD", HealthStatus = "Sain", Smart = null });
        new RulesEngine().Analyze(r);

        var html = HtmlReportGenerator.Generate(r);
        Assert.Contains("Aucun compteur n'a pu être lu", html);
        // Et surtout : pas de tableau de tirets qui ferait croire à un contrôle.
        Assert.DoesNotContain("<th>Erreurs CRC (câble)</th>", html);
    }

    [Fact]
    public void RapportHtml_AvecMesureSmart_AfficheLeTableau()
    {
        var r = SampleReport();
        r.System.Disks.Clear();
        r.System.Disks.Add(new DiskInfo
        {
            Model = "RPEYJ1T24MML1AWX",
            Smart = new SmartInfo { TemperatureC = 29, SsdLifeLeftPercent = 100, Source = "Compteurs de fiabilité Windows" },
        });
        new RulesEngine().Analyze(r);

        var html = HtmlReportGenerator.Generate(r);
        Assert.Contains("<th>Erreurs CRC (câble)</th>", html);
        Assert.DoesNotContain("Aucun compteur n'a pu être lu", html);
    }

    // ==================================================================
    // NVMe : décodage du journal de santé (page 0x02) et règles associées.
    // Ce code parle au matériel : impossible de le tester sur la machine de
    // développement, donc on le teste sur une page synthétique conforme à la
    // norme NVMe. C'est le seul filet possible sur du P/Invoke.
    // ==================================================================

    /// <summary>Construit une page de log NVMe 0x02 valide (512 octets).</summary>
    private static byte[] NvmeLog(
        byte warning = 0, int kelvin = 305, byte spare = 100, byte spareThreshold = 10,
        byte used = 3, ulong powerCycles = 500, ulong hours = 1200, ulong unsafeShutdowns = 12,
        ulong mediaErrors = 0, ulong errorEntries = 0)
    {
        var b = new byte[512];
        b[0] = warning;
        b[1] = (byte)(kelvin & 0xFF);
        b[2] = (byte)((kelvin >> 8) & 0xFF);
        b[3] = spare;
        b[4] = spareThreshold;
        b[5] = used;
        void W(int off, ulong v) { for (int i = 0; i < 8; i++) b[off + i] = (byte)(v >> (8 * i)); }
        W(112, powerCycles);
        W(128, hours);
        W(144, unsafeShutdowns);
        W(160, mediaErrors);
        W(176, errorEntries);
        return b;
    }

    [Fact]
    public void NvmeLog_DecodeLesChampsDeLaNorme()
    {
        var h = FaultTracePC.Core.Collectors.NvmeSmartReader.Parse(NvmeLog(), 0);

        Assert.NotNull(h);
        Assert.Equal(0, h!.CriticalWarning);
        Assert.Equal(32, h.TemperatureC);            // 305 K - 273
        Assert.Equal(100, h.AvailableSparePercent);
        Assert.Equal(10, h.AvailableSpareThresholdPercent);
        Assert.Equal(3, h.PercentageUsed);
        Assert.Equal(500UL, h.PowerCycles);
        Assert.Equal(1200UL, h.PowerOnHours);
        Assert.Equal(12UL, h.UnsafeShutdowns);
    }

    [Fact]
    public void NvmeLog_EntierementNul_EstRefuse()
    {
        // Un pilote peut répondre « OK » sans rien remplir : ce n'est pas une mesure.
        Assert.Null(FaultTracePC.Core.Collectors.NvmeSmartReader.Parse(new byte[512], 0));
    }

    [Fact]
    public void NvmeLog_LuAvecUnDecalage()
    {
        // Dans la vraie vie la page est précédée de l'en-tête du descripteur.
        var buffer = new byte[48 + 512];
        NvmeLog(kelvin: 310).CopyTo(buffer, 48);
        var h = FaultTracePC.Core.Collectors.NvmeSmartReader.Parse(buffer, 48);
        Assert.Equal(37, h!.TemperatureC);
    }

    [Theory]
    [InlineData(0x01, "réserve")]
    [InlineData(0x02, "température")]
    [InlineData(0x04, "fiabilité")]
    [InlineData(0x08, "LECTURE SEULE")]
    public void NvmeAlerte_EstTraduiteEnClair(byte flags, string fragment)
    {
        var texte = FaultTracePC.Core.Collectors.NvmeSmartReader.DescribeWarning(flags);
        Assert.Contains(fragment, texte, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NvmeAlerte_ZeroNeDitRien() =>
        Assert.Equal("", FaultTracePC.Core.Collectors.NvmeSmartReader.DescribeWarning(0));

    [Fact]
    public void ReserveNvme_EpuiseeSeulementSousLeSeuil()
    {
        Assert.True(new SmartInfo { AvailableSparePercent = 5, AvailableSpareThresholdPercent = 10 }.SpareExhausted);
        Assert.False(new SmartInfo { AvailableSparePercent = 100, AvailableSpareThresholdPercent = 10 }.SpareExhausted);
        // Sans seuil connu, on ne conclut pas : un disque n'est pas condamné faute d'information.
        Assert.False(new SmartInfo { AvailableSparePercent = 5 }.SpareExhausted);
    }

    [Fact]
    public void RegleNvme_ReserveEpuisee_EstCritique()
    {
        var r = SampleReport();
        r.System.Disks.Clear();
        r.System.Disks.Add(new DiskInfo
        {
            Model = "SSD-NVME",
            Smart = new SmartInfo
            {
                Source = "SMART NVMe (journal de santé)",
                AvailableSparePercent = 4, AvailableSpareThresholdPercent = 10, SsdLifeLeftPercent = 40,
            },
        });
        new RulesEngine().Analyze(r);

        var f = r.Findings.FirstOrDefault(x => x.Title.Contains("SSD-NVME", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(f);
        Assert.Equal(Severity.Critical, f!.Severity);
        Assert.Contains("réserve", f.Details, StringComparison.OrdinalIgnoreCase);
        // Un NVMe ne compte pas en secteurs : le mot ne doit pas apparaître.
        Assert.DoesNotContain("secteur(s) instable", f.Details);
    }

    [Fact]
    public void RegleNvme_ErreursIntegrite_SontSignalees()
    {
        var r = SampleReport();
        r.System.Disks.Clear();
        r.System.Disks.Add(new DiskInfo
        {
            Model = "SSD-NVME",
            Smart = new SmartInfo
            {
                Source = "SMART NVMe (journal de santé)",
                AvailableSparePercent = 100, AvailableSpareThresholdPercent = 10,
                UncorrectableSectors = 12, SsdLifeLeftPercent = 90,
            },
        });
        new RulesEngine().Analyze(r);

        var f = r.Findings.FirstOrDefault(x => x.Title.Contains("SSD-NVME", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(f);
        Assert.Equal(Severity.Critical, f!.Severity);   // >= 10 erreurs
        Assert.Contains("intégrité", f.Details, StringComparison.OrdinalIgnoreCase);
    }
}
