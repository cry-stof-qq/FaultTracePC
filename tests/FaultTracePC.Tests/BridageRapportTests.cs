using FaultTracePC.Core;
using FaultTracePC.Core.Analysis;
using Xunit;

namespace FaultTracePC.Tests;

/// <summary>
/// Point 120, lot C2 : les conclusions sur la température du processeur s'appuient sur
/// sa fréquence. Un processeur BRIDÉ (essai Intel du 11/11/2026 : 13 % plus lent à
/// 95–99 °C) donne une carte « bridé » ; un processeur chaud qui garde sa fréquence
/// (poste AMD, jusqu'à 100,4 °C) n'est plus « critique » et ne « s'éteint » plus.
/// </summary>
[Collection("Langue")]
public class BridageRapportTests
{
    private static List<Finding> Conclure(BilanBridage? bridage, TimeSpan auDessusAlerte, TimeSpan auDessusCritique)
    {
        var initial = Lang.Current;
        try
        {
            Lang.Apply(AppLanguage.French);
            var r = new DiagnosticReport
            {
                ScanPeriodDays = 30,
                System = new SystemSnapshot
                {
                    MachineName = "POSTE-TEST",
                    Os = new OsInfo { Caption = "Windows 11", TotalVisibleMemoryKB = 16 * 1024 * 1024, FreePhysicalMemoryKB = 8 * 1024 * 1024 },
                    Cpu = new CpuInfo { Name = "Processeur de test", Cores = 4, LogicalProcessors = 8 },
                },
            };
            r.Flight.JournalFound = true;
            r.Flight.Bridage = bridage;
            r.Flight.Thermal.Add(new ThermalStats
            {
                Sensor = ThermalHistory.CapteurCpu, WarnThreshold = 85, CritThreshold = 95,
                MaxC = 100.4, AverageC = 50.8, SampleCount = 20000,
                Observed = TimeSpan.FromHours(54), AboveWarn = auDessusAlerte, AboveCrit = auDessusCritique,
            });
            new RulesEngine().Analyze(r);
            return r.Findings;
        }
        finally { Lang.Apply(initial); }
    }

    /// <summary>L'épisode de l'essai Intel réel du 11/11/2026.</summary>
    private static EpisodeBridage EpisodeIntel(bool arret = false, double rapport = 0.865) => new()
    {
        Debut = new DateTime(2026, 11, 11, 12, 7, 24), Fin = new DateTime(2026, 11, 11, 12, 8, 52),
        FrequenceMoyenneMHz = 3466, TemperatureMax = 99, Rapport = rapport, SuiviDArretBrutal = arret,
    };

    private static Finding? Thermique(List<Finding> f) => f.SingleOrDefault(x =>
        x.Title.StartsWith("Surchauffe", StringComparison.Ordinal)
        || x.Title.StartsWith("Températures élevées", StringComparison.Ordinal)
        || x.Title.StartsWith("Processeur", StringComparison.Ordinal));

    [Fact]
    public void Un_bridage_mesure_donne_la_carte_bride_et_plus_la_carte_surchauffe()
    {
        var bilan = new BilanBridage { ReferenceMHz = 4007, ReferenceSousCharge = true, Episodes = { EpisodeIntel() } };

        var f = Thermique(Conclure(bilan, TimeSpan.FromMinutes(14), TimeSpan.FromMinutes(5)));

        Assert.NotNull(f);
        Assert.Equal("Processeur bridé par la chaleur : jusqu'à 14 % plus lent sous forte charge", f!.Title);
        Assert.Equal(Severity.Warning, f.Severity);
        Assert.Equal(Confidence.High, f.Confidence);
        Assert.Contains("n'a tenu que 3,47 GHz au lieu de 4,01 GHz", f.Details);
        Assert.Contains("pâte thermique", f.Recommendation);
        Assert.Contains("pas l'âge de la machine", f.Recommendation);
    }

    [Theory]
    [InlineData(true, 0.865)]  // un arrêt brutal a suivi
    [InlineData(false, 0.65)]  // bridage fort
    public void Arret_brutal_ou_bridage_fort_rendent_la_carte_critique(bool arret, double rapport)
    {
        var bilan = new BilanBridage { ReferenceMHz = 4007, ReferenceSousCharge = true, Episodes = { EpisodeIntel(arret, rapport) } };

        Assert.Equal(Severity.Critical, Thermique(Conclure(bilan, TimeSpan.FromMinutes(14), TimeSpan.FromMinutes(5)))!.Severity);
    }

    [Fact]
    public void Reference_prise_a_faible_charge_la_confiance_est_faible_et_le_dit()
    {
        var bilan = new BilanBridage { ReferenceMHz = 4007, ReferenceSousCharge = false, Episodes = { EpisodeIntel() } };

        var f = Thermique(Conclure(bilan, TimeSpan.FromMinutes(14), TimeSpan.FromMinutes(5)))!;

        Assert.Equal(Confidence.Low, f.Confidence);
        Assert.Contains("Confiance faible", f.Details);
    }

    [Fact]
    public void Chaud_sans_ralentir_n_est_qu_une_information()
    {
        // Le cas du poste AMD : 14 min au-dessus de 85 °C, 5 min au-dessus de 95 °C, fréquence tenue.
        var bilan = new BilanBridage { ReferenceMHz = 4716, ReferenceSousCharge = true, RelevesChaudsAvecFrequence = 96, RapportMedianAuChaud = 0.98 };

        var f = Thermique(Conclure(bilan, TimeSpan.FromMinutes(14), TimeSpan.FromMinutes(5)));

        Assert.NotNull(f);
        Assert.Equal(Severity.Info, f!.Severity);
        Assert.StartsWith("Processeur à sa limite de température sous charge, sans ralentir", f.Title);
        Assert.Contains("a gardé 98 % de sa fréquence habituelle (4,72 GHz)", f.Details);
        Assert.DoesNotContain("s'éteint brutalement", f.Details);
        Assert.DoesNotContain("pâte thermique", f.Recommendation);
    }

    [Fact]
    public void Sans_frequence_exploitable_l_ancienne_regle_s_applique_sans_conseil_d_age()
    {
        // Journal antérieur à la 1.11.0 (pas de bilan), ou fréquence au chaud trop basse pour conclure.
        foreach (var bilan in new[] { null, new BilanBridage { ReferenceMHz = 4007, RapportMedianAuChaud = 0.80, RelevesChaudsAvecFrequence = 10 } })
        {
            var f = Thermique(Conclure(bilan, TimeSpan.FromMinutes(40), TimeSpan.FromMinutes(12)));

            Assert.NotNull(f);
            Assert.StartsWith("Surchauffe", f!.Title);
            Assert.Equal(Severity.Critical, f.Severity);
            Assert.DoesNotContain("pâte thermique", f.Recommendation);
        }
    }

    [Fact]
    public void Peu_de_chaleur_aucune_carte_meme_avec_une_frequence_tenue()
    {
        var bilan = new BilanBridage { ReferenceMHz = 4716, ReferenceSousCharge = true, RapportMedianAuChaud = 0.98, RelevesChaudsAvecFrequence = 3 };

        Assert.Null(Thermique(Conclure(bilan, TimeSpan.FromSeconds(30), TimeSpan.Zero)));
    }

    [Fact]
    public void Surchauffe_avant_un_crash_la_pate_thermique_seulement_si_la_surchauffe_revient()
    {
        // Aligné le 11/11/2026 à la demande de l'auteur : plus de conseil selon l'âge.
        var initial = Lang.Current;
        try
        {
            Lang.Apply(AppLanguage.French);
            var r = new DiagnosticReport { ScanPeriodDays = 30, System = new SystemSnapshot { MachineName = "POSTE-TEST" } };
            r.Flight.JournalFound = true;
            var crash = new DateTime(2026, 11, 11, 12, 10, 0);
            r.Flight.Contexts.Add(new FlightCrashContext
            {
                CrashTime = crash,
                Samples = { new FlightSample { Time = crash.AddSeconds(-10), CpuLoad = 100, CpuTemp = 97, MemPct = 40 } },
            });
            new RulesEngine().Analyze(r);

            var f = Assert.Single(r.Findings, x => x.Title.StartsWith("SURCHAUFFE mesurée juste avant le crash", StringComparison.Ordinal));
            Assert.DoesNotContain("plusieurs années", f.Recommendation);
            Assert.Contains("Si la surchauffe revient après dépoussiérage, faire renouveler la pâte thermique.", f.Recommendation);
        }
        finally { Lang.Apply(initial); }
    }

    [Fact]
    public void L_alerte_temps_reel_ne_conseille_plus_la_pate_thermique_selon_l_age()
    {
        var initial = Lang.Current;
        try
        {
            Lang.Apply(AppLanguage.French);
            var a = new PreventiveAlert { RuleId = "cpu_temp", Level = "crit", Value = 100 };
            Assert.True(AlertCatalog.Localize(a));
            Assert.DoesNotContain("pâte thermique", a.Recommendation);
            Assert.StartsWith("Vérifier la ventilation", a.Recommendation);
        }
        finally { Lang.Apply(initial); }
    }
}
