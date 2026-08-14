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
}
