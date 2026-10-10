using FaultTracePC.Core;
using FaultTracePC.Core.Analysis;
using Xunit;

namespace FaultTracePC.Tests;

/// <summary>
/// POINT 84 — la date des pilotes tiers rapprochée du début d'une série de plantages.
///
/// Données relevées dans le rapport de la machine du 24/09/2026 : quatorze pilotes
/// Gen Digital datés du 22/09/2026, un quinzième (aswElam.sys) daté du 07/10/2025,
/// et la série de septembre qui commence le 22/09/2026 à 20 h 35.
/// </summary>
[Collection("Langue")]
public class PilotesDebutSerieTests
{
    private static readonly string[] Avast22Septembre =
    [
        "aswArPot", "aswbidsdriver", "aswbidsh", "aswbuniv", "aswDrvBrg", "aswKbd", "aswMonFlt",
        "aswNetHub", "aswRdr2", "aswRvrt", "aswSnx", "aswSP", "aswStm", "aswVmm",
    ];

    private static DriverInfo Pilote(string fichier, string editeur, DateTime date, bool microsoft = false) => new()
    {
        Name = fichier,
        Path = $@"C:\Windows\System32\drivers\{fichier}.sys",
        CompanyName = editeur,
        FileDate = date,
        IsMicrosoft = microsoft,
    };

    private static List<DriverInfo> PilotesDu24Septembre()
    {
        var liste = Avast22Septembre.Select(f => Pilote(f, "Gen Digital Inc.", new DateTime(2026, 9, 22))).ToList();
        liste.Add(Pilote("aswElam", "Gen Digital Inc.", new DateTime(2025, 10, 7)));
        liste.Add(Pilote("SynTP", "Synaptics Incorporated", new DateTime(2017, 5, 4)));
        liste.Add(Pilote("drfec", "Dynabook Inc.", new DateTime(2025, 12, 14)));
        return liste;
    }

    private static List<Finding> Conclure(List<BsodIncident> plantages, List<DriverInfo> pilotes)
    {
        var initial = Lang.Current;
        try
        {
            Lang.Apply(AppLanguage.French);
            var r = new DiagnosticReport { Bsods = plantages };
            r.System.Drivers.AddRange(pilotes);
            RulesEngine.AnalyzePilotesAuDebutDesSeries(r);
            return r.Findings.Where(f => f.Code == "pilotes.debut_serie").ToList();
        }
        finally { Lang.Apply(initial); }
    }

    [Fact]
    public void Machine_du_24_09_les_quatorze_pilotes_Avast_sont_rapproches_de_la_serie()
    {
        var f = Assert.Single(Conclure(SeriesDePlantagesTests.ProfilDu24Septembre(), PilotesDu24Septembre()));

        Assert.Equal(Severity.Warning, f.Severity);
        Assert.Equal(Confidence.Low, f.Confidence);
        Assert.Equal(FaultCategory.None, f.Category);
        Assert.Contains("Gen Digital Inc. (14)", f.Title);
        Assert.Contains("commence le 22/09/2026 à 20:35", f.Details);
        Assert.Contains("14 pilote(s) de Gen Digital Inc. daté(s) du 22/09/2026", f.Details);
        Assert.Contains("et 8 autre(s)", f.Details);
        Assert.Contains("aswElam.sys, 07/10/2025", f.Details);
        Assert.Contains("pas une preuve", f.Recommendation);
        // La série de juillet n'a aucun pilote daté juste avant : une seule conclusion.
        Assert.DoesNotContain("Synaptics", f.Title);
    }

    [Fact]
    public void Un_pilote_date_trois_jours_avant_n_est_pas_retenu()
    {
        var pilotes = new List<DriverInfo> { Pilote("aswSP", "Gen Digital Inc.", new DateTime(2026, 9, 19)) };
        Assert.Empty(Conclure(SeriesDePlantagesTests.ProfilDu24Septembre(), pilotes));
    }

    [Fact]
    public void Un_pilote_date_apres_le_debut_n_est_pas_retenu()
    {
        var pilotes = new List<DriverInfo> { Pilote("aswSP", "Gen Digital Inc.", new DateTime(2026, 9, 23)) };
        Assert.Empty(Conclure(SeriesDePlantagesTests.ProfilDu24Septembre(), pilotes));
    }

    [Fact]
    public void Un_pilote_Microsoft_n_est_pas_une_piste()
    {
        var pilotes = new List<DriverInfo> { Pilote("storahci", "Microsoft Corporation", new DateTime(2026, 9, 22), microsoft: true) };
        Assert.Empty(Conclure(SeriesDePlantagesTests.ProfilDu24Septembre(), pilotes));
    }

    [Fact]
    public void Un_plantage_isole_ne_suffit_pas()
    {
        var seul = new List<BsodIncident> { new() { TimeLocal = new DateTime(2026, 9, 22, 20, 35, 0), BugCheckCode = 0x7A } };
        Assert.Empty(Conclure(seul, PilotesDu24Septembre()));
    }
}
