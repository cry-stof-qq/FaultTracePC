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

    // ==================================================================
    // v1.2 — base de correspondance pilote → logiciel → action
    // ==================================================================

    [Theory]
    [InlineData("nvlddmkm.sys", "NVIDIA")]
    [InlineData("bindflt.sys", "Windows")]
    [InlineData("sptd.sys", "SPTD")]
    [InlineData("rtcore64.sys", "Afterburner")]
    [InlineData("tm.sys", "transactions")]
    public void BasePilotes_ConnaitLesSuspectsClassiques(string fichier, string fragment)
    {
        var e = DriverKnowledgeBase.Lookup(fichier);
        Assert.NotNull(e);
        Assert.Contains(fragment, e!.Owner + " " + e.Context, StringComparison.OrdinalIgnoreCase);
        Assert.False(string.IsNullOrWhiteSpace(e.Fix));
    }

    [Fact]
    public void BasePilotes_ToleranteSurLaFormeDuNom()
    {
        // L'analyse symbolique renvoie tantôt « nvlddmkm », tantôt « nvlddmkm.sys ».
        Assert.NotNull(DriverKnowledgeBase.Lookup("nvlddmkm"));
        Assert.NotNull(DriverKnowledgeBase.Lookup("NVLDDMKM.SYS"));
    }

    [Fact]
    public void BasePilotes_TmSysNEstPasTrendMicro()
    {
        // Piège réel : le nom évoque Trend Micro alors que c'est un composant
        // indispensable de Windows. Se tromper ici ferait supprimer un fichier
        // système à l'utilisateur.
        var e = DriverKnowledgeBase.Lookup("tm.sys")!;
        Assert.Contains("Windows", e.Owner, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Trend Micro", e.Owner, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("jamais supprimer", e.Fix, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BasePilotes_PiloteInconnu_SeRabatSurLEditeur()
    {
        var e = DriverKnowledgeBase.Describe("pilotebidon42.sys", "Contoso Devices");
        Assert.Equal("Contoso Devices", e.Owner);
        Assert.Contains("ne figure pas dans la base", e.Context, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Contoso Devices", e.Fix, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BasePilotes_PiloteInconnuSansEditeur_NInventeRien()
    {
        var e = DriverKnowledgeBase.Describe("pilotebidon42.sys", null);
        Assert.Equal("éditeur inconnu", e.Owner);
        Assert.Contains("pnputil", e.Fix, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BasePilotes_ComposantMicrosoftInconnu_RenvoieVersWindowsUpdate()
    {
        var e = DriverKnowledgeBase.Describe("inconnu99.sys", "Microsoft Corporation");
        Assert.Contains("Windows Update", e.Fix, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("amdgpio2.sys", "Advanced Micro Devices, Inc", "AMD")]
    [InlineData("AtihdWT6.sys", "Advanced Micro Devices", "AMD")]
    [InlineData("netwtw12.sys", "Intel Corporation", "Intel")]
    [InlineData("rtkvhd64.sys", "Realtek Semiconductor Corp.", "Realtek")]
    public void BasePilotes_ReconnaitLesFamillesDePlateforme(string fichier, string editeur, string attendu)
    {
        var e = DriverKnowledgeBase.LookupFamily(fichier, editeur);
        Assert.NotNull(e);
        Assert.Contains(attendu, e!.Owner, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BaseFamilles_ExigeLaConcordanceDeLEditeur()
    {
        // Le seul nom de fichier ne suffit pas : un pilote tiers commençant par
        // « amd » ne doit pas être attribué à AMD sur cette base.
        Assert.Null(DriverKnowledgeBase.LookupFamily("amdsuspect.sys", "Contoso Devices"));
        Assert.Null(DriverKnowledgeBase.LookupFamily("amdsuspect.sys", null));
    }

    [Fact]
    public void BaseFamilles_LaCorrespondanceNominativePrime()
    {
        // amdkmdag.sys est listé nommément : on doit obtenir le correctif précis
        // (DDU), pas le conseil générique de plateforme.
        var any = DriverKnowledgeBase.LookupAny("amdkmdag.sys", "Advanced Micro Devices");
        Assert.NotNull(any);
        Assert.True(any!.Value.Exact);
        Assert.Contains("DDU", any.Value.Entry.Fix, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("VBoxNetLwf.sys", "Oracle and/or its affiliates", "VirtualBox")]
    [InlineData("ftvnic.sys", "Fortinet Corporation", "Fortinet")]
    [InlineData("HpqKbFiltr.sys", "HP Inc.", "fabricant")]
    public void BaseFamilles_CouvreVirtualisationVpnEtOem(string fichier, string editeur, string attendu)
    {
        var e = DriverKnowledgeBase.LookupFamily(fichier, editeur);
        Assert.NotNull(e);
        Assert.Contains(attendu, e!.Owner, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BasePilotes_ConnaitLesDeuxVariantesDeSptd()
    {
        // sptd.sys et sptd2.sys sont le même pilote à deux générations : rater la
        // seconde laisserait passer une cause d'écran bleu connue.
        foreach (var f in new[] { "sptd.sys", "sptd2.sys" })
        {
            var e = DriverKnowledgeBase.Lookup(f);
            Assert.NotNull(e);
            Assert.Contains("standalone installer", e!.Fix, StringComparison.OrdinalIgnoreCase);
        }
    }

    // ==================================================================
    // Bandeau « par où commencer ? »
    // ==================================================================

    [Fact]
    public void Rapport_AvecProbleme_IndiqueLAssistantGuide()
    {
        var r = SampleReport();
        new RulesEngine().Analyze(r);
        Assert.Contains(r.Findings, f => f.Severity is Severity.Critical or Severity.Warning);

        var html = HtmlReportGenerator.Generate(r);
        Assert.Contains("Je ne sais pas ce que j'ai", html);
        Assert.Contains("Tu ne sais pas par où commencer", html);
    }

    [Fact]
    public void Rapport_MachineSaine_NAffichePasLeBandeau()
    {
        // Sur une machine sans rien à traiter, proposer un assistant de réparation
        // n'est pas une aide : c'est du bruit qui fait douter sans raison.
        var r = new DiagnosticReport
        {
            ScanPeriodDays = 30,
            System = new SystemSnapshot { MachineName = "SAIN-01", Os = new OsInfo { Caption = "Windows 11" } },
        };
        new RulesEngine().Analyze(r);
        Assert.DoesNotContain(r.Findings, f => f.Severity is Severity.Critical or Severity.Warning);

        var html = HtmlReportGenerator.Generate(r);
        Assert.DoesNotContain("Tu ne sais pas par où commencer", html);
    }

    // ==================================================================
    // v1.2 — historique des dépassements de température
    // ==================================================================

    private static ThermalHistory Feed(params (int Sec, double Temp)[] points)
    {
        var h = new ThermalHistory("Processeur", warnThreshold: 85, critThreshold: 95);
        var t0 = new DateTime(2026, 8, 14, 10, 0, 0);
        foreach (var (sec, temp) in points) h.Add(t0.AddSeconds(sec), temp);
        return h;
    }

    [Fact]
    public void Thermique_CumuleLeTempsPasseAuDessusDuSeuil()
    {
        // 4 relevés de 30 s : 3 intervalles, dont 2 avec les deux bornes > 85.
        var s = Feed((0, 90), (30, 92), (60, 91), (90, 70)).Build();
        Assert.Equal(TimeSpan.FromSeconds(60), s.AboveWarn);
        Assert.Equal(TimeSpan.FromSeconds(90), s.Observed);
        Assert.Equal(92, s.MaxC);
    }

    [Fact]
    public void Thermique_UneSeuleBorneAuDessus_NeComptePas()
    {
        // Choix assumé : on sous-estime plutôt que de gonfler un chiffre qui alarme.
        var s = Feed((0, 70), (30, 90), (60, 70)).Build();
        Assert.Equal(TimeSpan.Zero, s.AboveWarn);
        Assert.Equal(TimeSpan.FromSeconds(60), s.Observed);
    }

    [Fact]
    public void Thermique_CoupureDeMesure_NEstPasComptee()
    {
        // Machine éteinte huit heures entre deux relevés chauds : sans ce garde-fou,
        // le rapport annoncerait huit heures de surchauffe qui n'ont pas eu lieu.
        var s = Feed((0, 95), (8 * 3600, 96)).Build();
        Assert.Equal(TimeSpan.Zero, s.AboveWarn);
        Assert.Equal(TimeSpan.Zero, s.Observed);
    }

    [Fact]
    public void Thermique_SeuilCritiqueCompteAPart()
    {
        var s = Feed((0, 96), (30, 97), (60, 88), (90, 87)).Build();
        Assert.Equal(TimeSpan.FromSeconds(30), s.AboveCrit);
        Assert.Equal(TimeSpan.FromSeconds(90), s.AboveWarn);  // tout reste au-dessus de 85
    }

    [Fact]
    public void Thermique_CapteurMuetOuAberrant_EstIgnore()
    {
        var h = new ThermalHistory("Processeur", 85, 95);
        var t0 = new DateTime(2026, 8, 14, 10, 0, 0);
        h.Add(t0, null);
        h.Add(t0.AddSeconds(30), 0);
        h.Add(t0.AddSeconds(60), 999);
        var s = h.Build();
        Assert.False(s.HasData);
        Assert.Null(s.MaxC);
    }

    [Fact]
    public void Thermique_EpisodeContinu_EstRetenuAvecSaPointe()
    {
        var s = Feed((0, 88), (30, 91), (60, 93), (90, 89), (120, 60)).Build();
        var ep = Assert.Single(s.LongestEpisodes);
        Assert.Equal(1.5, ep.Minutes);   // 0 s → 90 s
        Assert.Equal(93, ep.PeakC);
    }

    [Theory]
    [InlineData(45, "45 s")]
    [InlineData(600, "10 min")]
    [InlineData(3600, "1 h")]
    [InlineData(8100, "2 h 15")]
    public void Thermique_DureesLisiblesSansEffort(int seconds, string attendu) =>
        Assert.Equal(attendu, ThermalHistory.Humanize(TimeSpan.FromSeconds(seconds)));

    [Fact]
    public void RegleThermique_SurchauffePrologee_EstCritique()
    {
        var r = SampleReport();
        r.Flight.JournalFound = true;
        r.Flight.Thermal.Add(new ThermalStats
        {
            Sensor = "Processeur", WarnThreshold = 85, CritThreshold = 95,
            MaxC = 99, AverageC = 78, SampleCount = 500,
            Observed = TimeSpan.FromHours(3),
            AboveWarn = TimeSpan.FromMinutes(40),
            AboveCrit = TimeSpan.FromMinutes(12),
        });
        new RulesEngine().Analyze(r);

        var f = r.Findings.FirstOrDefault(x => x.Title.StartsWith("Surchauffe", StringComparison.Ordinal));
        Assert.NotNull(f);
        Assert.Equal(Severity.Critical, f!.Severity);
        Assert.Contains("12 min", f.Title);
    }

    [Fact]
    public void RegleThermique_PointeBreve_NAlertePas()
    {
        // Trente secondes au-dessus du seuil pendant trois heures d'usage : normal.
        var r = SampleReport();
        r.Flight.JournalFound = true;
        r.Flight.Thermal.Add(new ThermalStats
        {
            Sensor = "Processeur", WarnThreshold = 85, CritThreshold = 95,
            MaxC = 88, SampleCount = 500,
            Observed = TimeSpan.FromHours(3),
            AboveWarn = TimeSpan.FromSeconds(30),
            AboveCrit = TimeSpan.Zero,
        });
        new RulesEngine().Analyze(r);
        Assert.DoesNotContain(r.Findings, x => x.Title.Contains("Surchauffe", StringComparison.OrdinalIgnoreCase)
                                            || x.Title.Contains("Températures élevées", StringComparison.OrdinalIgnoreCase));
    }

    // ==================================================================
    // v1.2 — comparateur de parc
    // ==================================================================

    private static ParkComparator.MachineSummary Poste(
        string nom, (string File, string Version, string Date)[]? pilotes = null,
        uint[]? codes = null, string[]? critiques = null, (string Model, ulong Bad)[]? disques = null)
    {
        var s = new ScanHistory.ScanSummary { GeneratedAt = DateTime.Now };
        foreach (var (f, v, d) in pilotes ?? []) s.DriverVersions[f] = $"{v}|{d}";
        foreach (var c in codes ?? []) s.Bsods.Add(new ScanHistory.BsodBrief { Code = c, Time = DateTime.Now });
        foreach (var c in critiques ?? []) s.CriticalFindings.Add(c);
        foreach (var (m, b) in disques ?? []) s.Disks.Add(new ScanHistory.DiskBrief { Model = m, BadSectors = b });
        return new ParkComparator.MachineSummary(nom, s);
    }

    [Fact]
    public void Parc_UnSeulPoste_NeCompareRien()
    {
        // La comparaison n'a de sens qu'à partir de deux machines : le dire vaut
        // mieux qu'afficher un tableau vide qui laisse croire à une absence de problème.
        var a = ParkComparator.Analyze([Poste("PC-01")]);
        Assert.Empty(a.Correlations);
        Assert.Contains("au moins deux machines", a.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parc_PiloteAncienPartage_EstSignale()
    {
        var vieux = new[] { ("sptd.sys", "1.0.0", "2016-05-02") };
        var a = ParkComparator.Analyze([Poste("PC-01", vieux), Poste("PC-02", vieux), Poste("PC-03")]);

        var c = a.Correlations.FirstOrDefault(x => x.Title.Contains("sptd.sys", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(c);
        Assert.Equal("commun", c!.Kind);
        Assert.Equal(2, c.Machines.Count);
        // Le correctif vient de la base de pilotes, pas d'un conseil générique.
        Assert.Contains("standalone installer", c.Action, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parc_PiloteRecentPartage_NEstPasSignale()
    {
        var recent = new[] { ("amdkmdag.sys", "32.0.1", DateTime.Now.AddMonths(-3).ToString("yyyy-MM-dd")) };
        var a = ParkComparator.Analyze([Poste("PC-01", recent), Poste("PC-02", recent)]);
        Assert.DoesNotContain(a.Correlations, x => x.Kind == "commun" && x.Title.Contains("ancien"));
    }

    [Fact]
    public void Parc_VersionsDivergentes_SontRelevees()
    {
        var a = ParkComparator.Analyze([
            Poste("PC-01", [("rt640x64.sys", "10.5", "2024-01-01")]),
            Poste("PC-02", [("rt640x64.sys", "10.5", "2024-01-01")]),
            Poste("PC-03", [("rt640x64.sys", "9.1", "2019-01-01")]),
        ]);
        var c = a.Correlations.FirstOrDefault(x => x.Kind == "divergence");
        Assert.NotNull(c);
        Assert.Contains("PC-03", c!.Machines);           // le retardataire est nommé
        Assert.DoesNotContain("PC-01", c.Machines);      // pas la version majoritaire
    }

    [Fact]
    public void Parc_MemeEcranBleuSurPlusieursPostes_EstCritique()
    {
        var a = ParkComparator.Analyze([
            Poste("PC-01", codes: [0x50]),
            Poste("PC-02", codes: [0x50]),
            Poste("PC-03", codes: [0x1E]),
        ]);
        var c = a.Correlations.FirstOrDefault(x => x.Title.Contains("même écran bleu", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(c);
        Assert.Equal("crit", c!.Severity);
        Assert.Equal(2, c.Machines.Count);
    }

    [Fact]
    public void Parc_MemeModeleDeDisqueQuiSeDegrade_EstCritique()
    {
        var a = ParkComparator.Analyze([
            Poste("PC-01", disques: [("SSD-XY 500", 12)]),
            Poste("PC-02", disques: [("SSD-XY 500", 3)]),
        ]);
        var c = a.Correlations.FirstOrDefault(x => x.Title.Contains("SSD-XY 500", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(c);
        Assert.Equal("crit", c!.Severity);
        Assert.Contains("firmware", c.Action, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parc_PosteQuiAccumule_EstIsole()
    {
        var a = ParkComparator.Analyze([
            Poste("PC-MALADE", critiques: ["Disque", "RAM", "Pilote", "Surchauffe"]),
            Poste("PC-02"), Poste("PC-03"), Poste("PC-04"),
        ]);
        var c = a.Correlations.FirstOrDefault(x => x.Kind == "isolé");
        Assert.NotNull(c);
        Assert.Contains("PC-MALADE", c!.Machines);
        Assert.Contains("individuellement", c.Action, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parc_AucuneCorrelation_LeDitClairement()
    {
        var a = ParkComparator.Analyze([Poste("PC-01"), Poste("PC-02")]);
        Assert.Empty(a.Correlations);
        Assert.Contains("propres à chaque machine", a.Summary, StringComparison.OrdinalIgnoreCase);
    }

    // ==================================================================
    // Verdict de comparaison : la santé matérielle doit peser autant que
    // les plantages.
    //
    // Jusqu'à la 1.2.0, la conclusion ne se calculait qu'à partir des crashs :
    // une machine n'ayant jamais planté mais dont le disque perdait des secteurs
    // était titrée « Machine stable ». Ces tests verrouillent la correction.
    // ==================================================================

    /// <summary>Scan précédent minimal : machine saine, disque intact.</summary>
    private static ScanHistory.ScanSummary PrecedentSain(ulong badSectors = 0, ulong crc = 0, string health = "Sain") => new()
    {
        GeneratedAt = new DateTime(2026, 8, 1, 10, 0, 0),
        ScanPeriodDays = 30,
        Disks = [new ScanHistory.DiskBrief { Model = "Samsung SSD 980", Health = health, BadSectors = badSectors, CrcErrors = crc, WearPercent = 1 }],
    };

    /// <summary>Scan courant : aucun crash, un seul disque dont on pilote l'état.</summary>
    private static DiagnosticReport ScanActuel(SmartInfo? smart = null, string health = "Sain", int? wear = 1)
    {
        // Volontairement sans RulesEngine : on isole l'effet de l'ÉVOLUTION sur le
        // verdict, sans qu'un constat absolu vienne le masquer.
        return new DiagnosticReport
        {
            GeneratedAt = new DateTime(2026, 8, 15, 10, 0, 0),
            ScanPeriodDays = 30,
            System = new SystemSnapshot
            {
                MachineName = "POSTE-01",
                Disks = [new DiskInfo { Model = "Samsung SSD 980", HealthStatus = health, WearPercent = wear, Smart = smart }],
            },
        };
    }

    [Fact]
    public void Verdict_SecteursDefectueuxEnHausse_SansAucunCrash_NeDitPlusStable()
    {
        // LE cas du bug : zéro plantage avant comme après, mais le disque se dégrade.
        var c = ScanHistory.Compare(
            ScanActuel(new SmartInfo { ReallocatedSectors = 7 }),
            PrecedentSain(badSectors: 0));

        Assert.DoesNotContain("stable", c.Assessment, StringComparison.OrdinalIgnoreCase);
        Assert.NotEqual("ok", c.Tone);
        Assert.Equal("crit", c.HardwareSeverity);
        // La dégradation doit être dans le TITRE, pas reléguée dans le détail.
        Assert.Contains("0 à 7", c.Assessment);
    }

    // ------------------------------------------------------------------
    // Purge de l'historique : les DEUX conditions, jamais une seule.
    // ------------------------------------------------------------------

    private static List<(string Path, DateTime Modifie)> Resumes(int nombre, DateTime plusRecent, int joursEntreScans)
    {
        var l = new List<(string, DateTime)>();
        for (var i = 0; i < nombre; i++)
        {
            var d = plusRecent.AddDays(-i * (double)joursEntreScans);
            l.Add(($"Scan_{d:yyyy-MM-dd_HHmmss}.json", d));
        }
        return l;
    }

    [Fact]
    public void Purge_NeTouchePasAuxDixDerniers_MemeTresAnciens()
    {
        // Machine analysée une fois par an : tout est vieux, mais rien ne doit
        // partir — sinon elle perd la réponse à « est-ce que c'est réglé ? ».
        var now = new DateTime(2026, 8, 16);
        var fichiers = Resumes(8, now.AddYears(-3), joursEntreScans: 365);

        Assert.Empty(ScanHistory.ACandidats(fichiers, now));
    }

    [Fact]
    public void Purge_SupprimeCeQuiEstAncienEtAuDelaDesDixDerniers()
    {
        var now = new DateTime(2026, 8, 16);
        var fichiers = Resumes(15, now.AddDays(-100), joursEntreScans: 10); // tous > 90 jours

        var candidats = ScanHistory.ACandidats(fichiers, now);

        Assert.Equal(5, candidats.Count);          // 15 - les 10 conservés
        Assert.All(candidats, c => Assert.Contains("Scan_", c));
    }

    [Fact]
    public void Purge_NeSupprimeRienDeRecent_MemeAuDelaDesDixDerniers()
    {
        // 30 scans en un mois : au-delà des 10 derniers, mais tous récents.
        var now = new DateTime(2026, 8, 16);
        var fichiers = Resumes(30, now, joursEntreScans: 1);

        Assert.Empty(ScanHistory.ACandidats(fichiers, now));
    }

    [Fact]
    public void Verdict_DeuxScansTropRapproches_NeConcluentPas()
    {
        // Le cas remonté par un utilisateur : deux scans à quelques minutes
        // d'intervalle affichaient « Bon signe », alors que la machine n'avait
        // rien eu le temps de faire entre les deux.
        var prev = PrecedentSain();
        prev.Bsods.Add(new ScanHistory.BsodBrief { Time = new DateTime(2026, 7, 30), Code = 0x50 });

        var r = ScanActuel(new SmartInfo { ReallocatedSectors = 0 });
        r.GeneratedAt = prev.GeneratedAt.AddMinutes(12);

        var c = ScanHistory.Compare(r, prev);

        Assert.Contains("trop récent pour conclure", c.Assessment);
        Assert.DoesNotContain("Bon signe", c.Assessment);
    }

    [Fact]
    public void Verdict_ApresQuelquesJours_ConclutNormalement()
    {
        // Le garde-fou inverse : au-delà du plancher, le comportement ne change pas.
        var prev = PrecedentSain();
        prev.Bsods.Add(new ScanHistory.BsodBrief { Time = new DateTime(2026, 7, 30), Code = 0x50 });

        var r = ScanActuel(new SmartInfo { ReallocatedSectors = 0 });
        r.GeneratedAt = prev.GeneratedAt.AddDays(3);

        var c = ScanHistory.Compare(r, prev);

        Assert.DoesNotContain("trop récent", c.Assessment);
        Assert.Contains("Bon signe", c.Assessment);
    }

    [Fact]
    public void Verdict_RienNaBouge_ResteVertEtDitStable()
    {
        // Le garde-fou inverse : on ne doit pas inquiéter une machine réellement saine.
        var c = ScanHistory.Compare(ScanActuel(new SmartInfo { ReallocatedSectors = 0 }), PrecedentSain());

        Assert.Equal("ok", c.Tone);
        Assert.Contains("stable", c.Assessment, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(c.HardwareConcerns);
    }

    [Fact]
    public void Verdict_ErreursCrc_AccusentLeCableEtPasLeDisque()
    {
        var c = ScanHistory.Compare(
            ScanActuel(new SmartInfo { UdmaCrcErrors = 12 }),
            PrecedentSain(crc: 4));

        Assert.Equal("warn", c.HardwareSeverity);
        var msg = Assert.Single(c.HardwareConcerns).Message;
        Assert.Contains("câble", msg, StringComparison.OrdinalIgnoreCase);
        // Une erreur CRC ne doit JAMAIS être présentée comme un disque qui meurt :
        // c'est ce qui fait remplacer un disque sain à la place d'un câble à 5 €.
        Assert.DoesNotContain("secteurs défectueux", msg, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Verdict_SanteQuiSameliore_NestPasUneAlerte()
    {
        var c = ScanHistory.Compare(
            ScanActuel(new SmartInfo { ReallocatedSectors = 0 }, health: "Sain"),
            PrecedentSain(health: "Avertissement"));

        Assert.Empty(c.HardwareConcerns);
        Assert.Equal("ok", c.Tone);
        // Le changement reste affiché dans le détail : on informe sans alarmer.
        Assert.Contains(c.DiskChanges, s => s.Contains("santé"));
    }

    [Fact]
    public void Verdict_SanteQuiSaggrave_FaitBasculerLaCouleur()
    {
        var c = ScanHistory.Compare(
            ScanActuel(new SmartInfo { ReallocatedSectors = 0 }, health: "Défaillant"),
            PrecedentSain(health: "Sain"));

        Assert.Equal("crit", c.HardwareSeverity);
        Assert.Equal("crit", c.Tone);
    }

    [Fact]
    public void Verdict_NouvellesErreursWhea_SansCrash_NeSontPlusIgnorees()
    {
        var r = ScanActuel(new SmartInfo { ReallocatedSectors = 0 });
        r.Events.Add(new WinEvent { Category = EventCategory.Whea, TimeLocal = new DateTime(2026, 8, 10) });
        r.Events.Add(new WinEvent { Category = EventCategory.Whea, TimeLocal = new DateTime(2026, 8, 12) });

        var c = ScanHistory.Compare(r, PrecedentSain());

        Assert.Equal(2, c.NewWheaEvents);
        Assert.NotEqual("ok", c.Tone);
        Assert.DoesNotContain("stable", c.Assessment, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Verdict_UsureSsdDunPoint_NestPasUneAlerte()
    {
        // Un point d'usure de plus est le fonctionnement normal d'un SSD.
        var c = ScanHistory.Compare(
            ScanActuel(new SmartInfo { ReallocatedSectors = 0 }, wear: 2),
            PrecedentSain());

        Assert.Empty(c.HardwareConcerns);
        Assert.Contains(c.DiskChanges, s => s.Contains("usure"));
    }

    [Fact]
    public void Verdict_ProblemeCritiqueQuiPersiste_NestPasPresenteCommeStable()
    {
        var r = ScanActuel(new SmartInfo { ReallocatedSectors = 0 });
        r.Findings.Add(new Finding { Severity = Severity.Critical, Title = "Disque en fin de vie" });

        var c = ScanHistory.Compare(r, PrecedentSain());

        // Rien ne s'est aggravé — mais rien n'est réglé, et le titre doit le dire.
        Assert.DoesNotContain("Machine stable", c.Assessment);
        Assert.NotEqual("ok", c.Tone);
    }

    [Fact]
    public void Rapport_LaDegradationMaterielleApparaitDansLeHtml()
    {
        var r = ScanActuel(new SmartInfo { ReallocatedSectors = 7 });
        r.Comparison = ScanHistory.Compare(r, PrecedentSain());

        var html = HtmlReportGenerator.Generate(r);

        Assert.Contains("class=\"concerns\"", html);
        Assert.Contains("Sauvegardez maintenant", html);
    }

    // ==================================================================
    // Erreurs disque : nommer le périphérique, et conseiller ce qui
    // correspond au matériel réellement présent.
    //
    // En 1.2.1, la conclusion annonçait « 28 erreurs disque » sans jamais
    // dire lesquelles ni sur quoi, citait des identifiants d'événements
    // absents du rapport, et conseillait de vérifier un câble SATA à des
    // machines dont le seul disque est un NVMe.
    // ==================================================================

    private static DiagnosticReport AvecErreursDisque(params (string Provider, int Id, string Message)[] events)
    {
        var r = new DiagnosticReport
        {
            GeneratedAt = new DateTime(2026, 8, 15, 12, 0, 0),
            ScanPeriodDays = 30,
            System = new SystemSnapshot { MachineName = "POSTE-01" },
        };
        foreach (var (p, id, msg) in events)
            r.Events.Add(new WinEvent
            {
                Category = EventCategory.DiskError,
                Provider = p,
                EventId = id,
                Message = msg,
                TimeLocal = new DateTime(2026, 8, 10),
            });
        return r;
    }

    /// <summary>Le NVMe du poste de test : réserve exposée = NVMe, donc aucun câble SATA.</summary>
    private static DiskInfo NvmeSain(int index = 0) => new()
    {
        Model = "RPEYJ1T24MML1AWX",
        Index = index,
        HealthStatus = "Sain",
        Smart = new SmartInfo { AvailableSparePercent = 100, AvailableSpareThresholdPercent = 10, Source = "SMART NVMe (journal de santé)" },
    };

    private static Finding ErreursDisque(DiagnosticReport r)
    {
        new RulesEngine().Analyze(r);
        return r.Findings.First(f => f.Title.StartsWith("Erreurs disque répétées"));
    }

    [Fact]
    public void ErreursDisque_CitentLesIdentifiantsReellementObserves()
    {
        var r = AvecErreursDisque(
            ("storahci", 129, @"Une réinitialisation au périphérique, \Device\RaidPort1, a été émise."),
            ("storahci", 129, @"Une réinitialisation au périphérique, \Device\RaidPort1, a été émise."),
            ("storahci", 129, @"Une réinitialisation au périphérique, \Device\RaidPort1, a été émise."));
        r.System.Disks.Add(NvmeSain());

        var f = ErreursDisque(r);

        Assert.Contains("storahci 129", f.Details);
        // Ne doit plus citer des identifiants que la machine n'a pas produits.
        Assert.DoesNotContain("disk 153", f.Details);
        Assert.DoesNotContain("stornvme 129", f.Details);
    }

    [Fact]
    public void ErreursDisque_SignalentUnPeripheriqueNonInventorie()
    {
        // Un support absent ET un port de contrôleur : la machine reste concernée,
        // donc le conseil « identifier avant de réparer » garde tout son sens.
        // (Le cas où TOUT se rapporte à des supports débranchés est couvert par
        // ErreursDisque_ToutesSurDesSupportsDebranches_NAlarmentPlusLaMachine, qui
        // vérifie qu'on cesse alors d'alarmer la machine.)
        var r = AvecErreursDisque(
            ("storahci", 129, @"Réinitialisation au périphérique, \Device\RaidPort1, a été émise."),
            ("disk", 51, @"Une erreur a été détectée sur le périphérique \Device\Harddisk1\DR1 lors d'une opération de pagination."),
            ("disk", 51, @"Une erreur a été détectée sur le périphérique \Device\Harddisk1\DR1 lors d'une opération de pagination."));
        r.System.Disks.Add(NvmeSain(index: 0));   // le seul disque connu est Harddisk0

        var f = ErreursDisque(r);

        Assert.Contains(@"\Device\Harddisk1", f.Details);
        Assert.Contains("ABSENT", f.Details);
        // Et le conseil doit dire de l'identifier AVANT de réparer quoi que ce soit.
        Assert.Contains("Identifier le périphérique non inventorié", f.Recommendation);
    }

    [Fact]
    public void ErreursDisque_NommentLeDisqueQuandIlEstConnu()
    {
        var r = AvecErreursDisque(
            ("disk", 51, @"Erreur sur \Device\Harddisk0\DR0 lors d'une opération de pagination."),
            ("disk", 51, @"Erreur sur \Device\Harddisk0\DR0 lors d'une opération de pagination."),
            ("disk", 51, @"Erreur sur \Device\Harddisk0\DR0 lors d'une opération de pagination."));
        var d = NvmeSain(index: 0);
        d.Letters.Add("C:");
        r.System.Disks.Add(d);

        var f = ErreursDisque(r);

        // Numéro du Gestionnaire de disques, lettre, modèle : les trois désignations,
        // pour que n'importe quel niveau de lecteur reconnaisse le disque.
        Assert.Contains("Disque 0", f.Details);
        Assert.Contains("C:", f.Details);
        Assert.Contains("RPEYJ1T24MML1AWX", f.Details);
    }

    [Fact]
    public void ErreursDisque_NInvententPasUnNumeroPourUnDisqueAbsent()
    {
        // LE piège : les numéros de disque sont attribués au branchement. Écrire
        // « Disque 1 » pour un support débranché enverrait l'utilisateur ouvrir le
        // Gestionnaire de disques, n'y rien trouver, et douter du rapport.
        var r = AvecErreursDisque(
            ("disk", 51, @"Erreur sur \Device\Harddisk1\DR1."),
            ("disk", 51, @"Erreur sur \Device\Harddisk1\DR2."),
            ("disk", 51, @"Erreur sur \Device\Harddisk1\DR2."));
        r.System.Disks.Add(NvmeSain(index: 0));

        var f = ErreursDisque(r);

        Assert.Contains("ABSENT", f.Details);
        // Ni « Disque 1 » sec, ni renvoi vers un Gestionnaire de disques qui ne
        // l'affichera pas.
        Assert.DoesNotContain("Disque 1 ", f.Details);
        // Les dates sont la seule information exploitable pour un support disparu.
        Assert.Contains("Vu ", f.Details);
        // Deux instances DR distinctes = deux branchements = support amovible.
        Assert.Contains("branché puis débranché", f.Details);
    }

    [Fact]
    public void ErreursDisque_ToutesSurDesSupportsDebranches_NAlarmentPlusLaMachine()
    {
        // Cas du technicien qui branche des disques à réparer : les erreurs
        // concernent le disque en réparation, pas la machine qui l'analyse.
        var r = AvecErreursDisque(
            ("disk", 51, @"Erreur sur \Device\Harddisk3\DR7."),
            ("disk", 51, @"Erreur sur \Device\Harddisk3\DR7."),
            ("disk", 51, @"Erreur sur \Device\Harddisk3\DR7."));
        r.System.Disks.Add(NvmeSain(index: 0));

        var f = ErreursDisque(r);

        Assert.Equal(Severity.Info, f.Severity);
        Assert.Contains("Aucun disque actuellement monté", f.Details);
        Assert.Contains("Rien à réparer sur cette machine", f.Recommendation);
    }

    [Fact]
    public void ErreursDisque_SurUnPortDeControleur_RestentUnAvertissement()
    {
        // Un RaidPort appartient bien à la machine : lui, on ne le minimise pas.
        var r = AvecErreursDisque(
            ("storahci", 129, @"Réinitialisation au périphérique, \Device\RaidPort1, a été émise."),
            ("storahci", 129, @"Réinitialisation au périphérique, \Device\RaidPort1, a été émise."),
            ("disk", 51, @"Erreur sur \Device\Harddisk9\DR9."));
        r.System.Disks.Add(NvmeSain(index: 0));

        var f = ErreursDisque(r);

        Assert.Equal(Severity.Warning, f.Severity);
    }

    [Fact]
    public void ErreursDisque_SurNvme_NeConseillentPasDeCableSata()
    {
        var r = AvecErreursDisque(
            ("storahci", 129, @"Réinitialisation au périphérique, \Device\RaidPort1, a été émise."),
            ("storahci", 129, @"Réinitialisation au périphérique, \Device\RaidPort1, a été émise."),
            ("storahci", 129, @"Réinitialisation au périphérique, \Device\RaidPort1, a été émise."));
        r.System.Disks.Add(NvmeSain());

        var f = ErreursDisque(r);

        // Chercher un câble SATA sur une machine qui n'en a pas, c'est envoyer
        // l'utilisateur dans le mur.
        Assert.DoesNotContain("SATA", f.Recommendation);
        // La cause la plus documentée de ces réinitialisations arrive en premier.
        Assert.Contains("PCI Express", f.Recommendation);
    }

    [Fact]
    public void ErreursDisque_SurSata_ConseillentBienDeVerifierLeCable()
    {
        var r = AvecErreursDisque(
            ("disk", 51, @"Erreur sur \Device\Harddisk0\DR0."),
            ("disk", 51, @"Erreur sur \Device\Harddisk0\DR0."),
            ("disk", 51, @"Erreur sur \Device\Harddisk0\DR0."));
        r.System.Disks.Add(new DiskInfo
        {
            Model = "WDC WD10EZEX", Index = 0, HealthStatus = "Sain",
            Smart = new SmartInfo { ReallocatedSectors = 0, Source = "SMART (SATA)" },
        });

        var f = ErreursDisque(r);

        Assert.Contains("SATA", f.Recommendation);
    }

    [Fact]
    public void Rapport_IndiqueQuelBoutonDeLaBoiteAOutilsUtiliser()
    {
        var r = AvecErreursDisque(
            ("storahci", 129, @"Réinitialisation au périphérique, \Device\RaidPort1."),
            ("storahci", 129, @"Réinitialisation au périphérique, \Device\RaidPort1."),
            ("storahci", 129, @"Réinitialisation au périphérique, \Device\RaidPort1."));
        r.System.Disks.Add(NvmeSain());
        new RulesEngine().Analyze(r);

        var html = HtmlReportGenerator.Generate(r);

        // Le mot « Outils » n'apparaissait nulle part dans le rapport.
        Assert.Contains("Dans FaultTracePC", html);
        Assert.Contains("Alimentation des liens", html);

        // ATTENTION à qui voudra renforcer ce test : le texte des conclusions passe
        // par WebUtility.HtmlEncode, qui convertit TOUT caractère non-ASCII en entité
        // numérique — « Vérifier » devient « V&#233;rifier » dans la source. Le
        // navigateur l'affiche correctement, mais une assertion sur une chaîne
        // accentuée échoue toujours. On vérifie donc un fragment ASCII du libellé.
        Assert.Contains("lecture seule", html);
        Assert.Contains("SMART", html);
    }
}
