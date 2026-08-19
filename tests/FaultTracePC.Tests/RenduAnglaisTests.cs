using System.Text.RegularExpressions;
using FaultTracePC.Core;
using FaultTracePC.Core.Analysis;
using FaultTracePC.Core.Report;
using Xunit;

namespace FaultTracePC.Tests;

/// <summary>
/// Vérification du RENDU, en complément de <see cref="TraductionTests"/>.
///
/// TraductionTests lit le code source : il attrape un littéral oublié. Il ne peut
/// rien dire, en revanche, du français qui arrive par les DONNÉES — le catalogue
/// des codes d'arrêt, la base des pilotes, les libellés d'état de disque. Ces
/// trois-là sont justement exemptés du contrôle de source parce qu'ils stockent
/// les deux langues côte à côte ; c'est ici qu'on vérifie que la bonne des deux
/// ressort.
///
/// On produit donc le rapport complet EN ANGLAIS et on relit le texte rendu.
/// </summary>
[Collection("Langue")]
public class RenduAnglaisTests
{
    // ==================================================================
    // Un rapport de démonstration qui traverse un maximum de sections
    // ==================================================================

    private static DiagnosticReport RapportRiche()
    {
        var r = new DiagnosticReport
        {
            ScanPeriodDays = 30,
            System = new SystemSnapshot
            {
                MachineName = "POSTE-01",
                Os = new OsInfo
                {
                    Caption = "Windows 11",
                    TotalVisibleMemoryKB = 16 * 1024 * 1024,
                    FreePhysicalMemoryKB = 2 * 1024 * 1024,
                },
                Cpu = new CpuInfo { Name = "AMD Ryzen 7", Cores = 8, LogicalProcessors = 16 },
                Disks =
                [
                    new DiskInfo
                    {
                        Model = "Samsung SSD 980",
                        Index = 0,
                        SizeBytes = 512UL * 1024 * 1024 * 1024,
                        InterfaceType = "SCSI",
                        MediaType = "SSD",
                        WmiStatus = "OK",
                        Health = DiskHealth.Warning,
                        TemperatureC = 44,
                        WearPercent = 3,
                    },
                    // Health non rapporté ET WmiStatus vide : c'est le chemin qui
                    // retombait sur « inconnue » en dur avant le lot 7a.
                    new DiskInfo { Model = "WDC WD10EZEX", Index = 1, SizeBytes = 1000UL * 1024 * 1024 * 1024 },
                ],
                Drivers =
                [
                    // Un pilote de la base documentée, un pilote de famille, un inconnu :
                    // les trois branches de DriverKnowledgeBase.
                    new DriverInfo
                    {
                        Name = "nvlddmkm", DisplayName = "nvlddmkm", CompanyName = "NVIDIA Corporation",
                        FileVersion = "31.0.15.3742", FileDate = DateTime.Now.AddYears(-6),
                        State = "Running", StartMode = "Auto", IsMicrosoft = false,
                        Path = @"C:\Windows\System32\drivers\nvlddmkm.sys",
                    },
                    new DriverInfo
                    {
                        Name = "amdpsp", DisplayName = "amdpsp", CompanyName = "Advanced Micro Devices",
                        FileVersion = "5.17.0.0", FileDate = DateTime.Now.AddYears(-2),
                        State = "Running", StartMode = "Auto", IsMicrosoft = false,
                        Path = @"C:\Windows\System32\drivers\amdpsp.sys",
                    },
                    new DriverInfo
                    {
                        Name = "zzunknown", DisplayName = "zzunknown", CompanyName = "",
                        FileVersion = "1.0.0.0", FileDate = DateTime.Now.AddYears(-1),
                        State = "Stopped", StartMode = "Manual", IsMicrosoft = false,
                        Path = @"C:\Windows\System32\drivers\zzunknown.sys",
                    },
                ],
            },
            Dumps =
            [
                new DumpFileInfo
                {
                    Path = @"C:\Windows\Minidump\010126-1-01.dmp",
                    Kind = DumpKind.KernelMinidump,
                    BugCheckCode = 0x50,                 // PAGE_FAULT_IN_NONPAGED_AREA
                    BugCheckParameters = [1, 2, 3, 4],
                    CrashTimeFromHeader = DateTime.Now.AddDays(-2),
                    LastWriteTime = DateTime.Now.AddDays(-2),
                    DeepAnalyzed = true,
                    FaultingModule = "nvlddmkm.sys",
                    StackExcerpt = "nt!KeBugCheckEx\nnvlddmkm+0x104",
                },
                new DumpFileInfo
                {
                    Path = @"C:\Windows\Minidump\010226-1-01.dmp",
                    Kind = DumpKind.KernelMinidump,
                    BugCheckCode = 0x133,                // DPC_WATCHDOG_VIOLATION
                    BugCheckParameters = [0, 0, 0, 0],
                    CrashTimeFromHeader = DateTime.Now.AddDays(-1),
                    LastWriteTime = DateTime.Now.AddDays(-1),
                    DeepAnalyzed = true,
                    FaultingModule = "nvlddmkm.sys",
                    StackExcerpt = "nt!KeBugCheckEx",
                },
            ],
        };

        new RulesEngine().Analyze(r);
        return r;
    }

    // ==================================================================
    // Les vérifications
    // ==================================================================

    [Fact]
    public void Le_rapport_html_anglais_ne_laisse_passer_aucune_phrase_francaise()
    {
        var fautes = EnAnglais(() => PhrasesFrancaises(TexteDuHtml(HtmlReportGenerator.Generate(RapportRiche()))));

        Assert.True(fautes.Count == 0,
            $"{fautes.Count} passage(s) français dans le rapport anglais :\n  " + string.Join("\n  ", fautes));
    }

    /// <summary>
    /// Les faux amis, que les trois signaux ne peuvent pas voir.
    ///
    /// La détection repose sur un accent, trois mots outils français distincts, ou
    /// l'espace avant une ponctuation. Un mot français isolé, sans accent, et dont
    /// la forme anglaise ne diffère que d'une lettre, échappe aux trois — et c'est
    /// exactement ce qui est arrivé à « 💡 Recommandation », resté en français dans
    /// le rapport anglais jusqu'à la 1.4.
    ///
    /// Cette liste est volontairement courte et nominative : elle ne remplace pas
    /// la détection générale, elle bouche les trous qu'on lui connaît.
    /// </summary>
    /// Chaque entrée doit être un mot que l'anglais N'A PAS. « Conclusions »,
    /// « Surveillance », « Information » et « Analyse » en sont aussi — les mettre
    /// ici produirait un échec le jour où une phrase anglaise parfaitement correcte
    /// les emploie.
    [Theory]
    [InlineData("Recommandation")]
    [InlineData("Avertissement")]
    [InlineData("Historique")]
    [InlineData("Entretien")]
    [InlineData("Limitations de")]
    [InlineData("Masquer")]
    [InlineData("Afficher")]
    public void Le_rapport_anglais_ne_contient_aucun_faux_ami(string motFrancais)
    {
        var html = EnAnglais(() => HtmlReportGenerator.Generate(RapportRiche()));

        Assert.DoesNotContain(motFrancais, html, StringComparison.Ordinal);
    }

    [Fact]
    public void Le_bouton_de_bascule_reste_anglais_apres_un_clic()
    {
        // Le script qui réécrit le libellé au clic est une CONSTANTE : il ne peut
        // contenir aucun appel de traduction, et il portait donc les libellés
        // français en dur. Le libellé initial étant correct, la fuite ne se voyait
        // qu'après un clic — et aucun test ne clique. On vérifie donc les deux
        // états là où ils vivent désormais, dans les données injectées au script.
        var html = EnAnglais(() => HtmlReportGenerator.Generate(RapportRiche()));

        Assert.Contains("Hide the technical detail", html, StringComparison.Ordinal);
        Assert.Contains("Show the technical detail", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Le_meme_bouton_reste_francais_en_francais()
    {
        // Contrôle positif : sans lui, une injection cassée passerait pour une
        // réussite dans le test ci-dessus.
        var html = HtmlReportGenerator.Generate(RapportRiche());

        Assert.Contains("Masquer les d", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Le_controle_des_faux_amis_n_est_pas_vide()
    {
        // Contrôle positif : sans lui, la théorie ci-dessus passerait aussi bien
        // sur un rapport qui n'affiche aucune recommandation. Un test qui ne peut
        // pas échouer ne protège rien.
        var html = EnAnglais(() => HtmlReportGenerator.Generate(RapportRiche()));

        Assert.Contains("Recommendation", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Le_script_de_reparation_anglais_ne_laisse_passer_aucune_phrase_francaise()
    {
        var fautes = EnAnglais(() =>
        {
            var r = RapportRiche();
            Assert.True(RepairScriptGenerator.IsRepairable(r), "le rapport de démonstration doit produire un script");
            return PhrasesFrancaises(RepairScriptGenerator.Generate(r).Split('\n'));
        });

        Assert.True(fautes.Count == 0,
            $"{fautes.Count} passage(s) français dans le script anglais :\n  " + string.Join("\n  ", fautes));
    }

    [Fact]
    public void Le_meme_rapport_en_francais_est_bien_francais()
    {
        // Contrôle positif. Sans lui, un générateur qui renverrait une page vide
        // passerait les deux tests ci-dessus sans que personne ne s'en aperçoive.
        var initial = Lang.Current;
        try
        {
            Lang.Apply(AppLanguage.French);
            var phrases = PhrasesFrancaises(TexteDuHtml(HtmlReportGenerator.Generate(RapportRiche())));
            Assert.True(phrases.Count >= 20,
                $"seulement {phrases.Count} passage(s) reconnus comme français dans le rapport français");
        }
        finally { Lang.Apply(initial); }
    }

    [Fact]
    public void Le_catalogue_des_codes_d_arret_ressort_en_anglais()
    {
        // BugCheckCatalog et DriverKnowledgeBase sont exemptés du contrôle de
        // source : c'est ici, et seulement ici, que leur moitié anglaise est lue.
        var initial = Lang.Current;
        try
        {
            Lang.Apply(AppLanguage.English);
            var html = HtmlReportGenerator.Generate(RapportRiche());

            Assert.Contains("PAGE_FAULT_IN_NONPAGED_AREA", html);   // jamais traduit
            Assert.Contains("DPC_WATCHDOG_VIOLATION", html);
            Assert.Contains("nvlddmkm.sys", html);                  // jamais traduit non plus
        }
        finally { Lang.Apply(initial); }
    }

    // ==================================================================
    // Outils
    // ==================================================================

    /// <summary>Exécute en anglais, puis remet la langue comme on l'a trouvée.</summary>
    private static T EnAnglais<T>(Func<T> action)
    {
        var initial = Lang.Current;
        try
        {
            Lang.Apply(AppLanguage.English);
            return action();
        }
        finally { Lang.Apply(initial); }
    }

    private static readonly Regex BlocsNonTexte =
        new("<(script|style)\\b[^>]*>.*?</\\1>", RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex Balises = new("<[^>]+>", RegexOptions.Compiled);

    /// <summary>Texte visible d'une page : CSS et JS retirés, balises remplacées par des sauts de ligne.</summary>
    private static IEnumerable<string> TexteDuHtml(string html)
    {
        var sansBlocs = BlocsNonTexte.Replace(html, "\n");
        var sansBalises = Balises.Replace(sansBlocs, "\n");
        return System.Net.WebUtility.HtmlDecode(sansBalises).Split('\n');
    }

    /// <summary>
    /// Garde les passages que le détecteur de <see cref="TraductionTests"/> juge
    /// français. Le même détecteur des deux côtés : un test qui utiliserait une
    /// autre règle ne vérifierait pas la même chose.
    /// </summary>
    private static List<string> PhrasesFrancaises(IEnumerable<string> lignes) =>
        lignes.Select(l => l.Trim())
              .Where(l => l.Length >= 8)
              .Where(TraductionTests.SembleFrancais)
              .Distinct(StringComparer.Ordinal)
              .ToList();
}
