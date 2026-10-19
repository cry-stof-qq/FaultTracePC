using FaultTracePC.Core;
using FaultTracePC.Core.Analysis;
using FaultTracePC.Core.Report;
using Xunit;

namespace FaultTracePC.Tests;

/// <summary>
/// POINT 96. Rapport du parc du 19/10/2026 : 175 cartes « divergence de version »,
/// dont 145 pour des pilotes livrés avec Windows et 4 pour ceux de Defender. Ces
/// pilotes changent ensemble : une carte par famille suffit, et les pilotes de
/// constructeurs gardent la leur.
/// </summary>
[Collection("Langue")]
public class ParcRegroupementTests
{
    private const string Recent = "10.0.26100.9711 (WinBuild.160101.0800)";
    private const string Ancien = "10.0.26100.9278 (WinBuild.160101.0800)";

    private static ParkComparator.MachineSummary Poste(string nom, params (string Fichier, string Version)[] pilotes)
    {
        var s = new ScanHistory.ScanSummary { GeneratedAt = DateTime.Now };
        foreach (var (f, v) in pilotes)
            s.DriverVersions[f] = $"{v}|{DateTime.Now.AddMonths(-1):yyyy-MM-dd}";
        return new ParkComparator.MachineSummary(nom, s);
    }

    private static List<ParkComparator.Correlation> Divergences(params ParkComparator.MachineSummary[] postes)
    {
        var initial = Lang.Current;
        try
        {
            Lang.Apply(AppLanguage.French);
            return ParkComparator.Analyze(postes).Correlations.Where(c => c.Kind == "divergence").ToList();
        }
        finally { Lang.Apply(initial); }
    }

    private static readonly string[] FichiersWindows = { "tcpip.sys", "usbccgp.sys", "UcmCx.sys", "tpm.sys", "stornvme.sys", "volmgr.sys" };

    [Fact]
    public void Les_pilotes_de_Windows_tiennent_en_une_seule_carte()
    {
        var c = Divergences(
            Poste("POSTE-A", FichiersWindows.Select(f => (f, Recent)).ToArray()),
            Poste("POSTE-B", FichiersWindows.Select(f => (f, Ancien)).ToArray()),
            Poste("POSTE-C", FichiersWindows.Select(f => (f, f == "tcpip.sys" || f == "tpm.sys" ? Ancien : Recent)).ToArray()));

        var carte = Assert.Single(c);
        Assert.Contains("6 pilotes de Windows", carte.Title);
        Assert.Equal(new[] { "POSTE-B", "POSTE-C" }, carte.Machines);
        Assert.Contains("POSTE-B (6 fichier(s))", carte.Details);
        Assert.Contains("POSTE-C (2 fichier(s))", carte.Details);
        Assert.Contains("et 1 autre(s)", carte.Details);
        Assert.Contains("mises à jour de Windows", carte.Action);
    }

    [Fact]
    public void Les_pilotes_de_Defender_tiennent_en_une_seule_carte()
    {
        string[] wd = { "WdFilter.sys", "WdBoot.sys", "WdNisDrv.sys" };
        var c = Divergences(
            Poste("POSTE-A", wd.Select(f => (f, "4.18.26090.9 (4df2f7dd)")).ToArray()),
            Poste("POSTE-B", wd.Select(f => (f, "4.18.25070.5 (1bc206b8)")).ToArray()),
            Poste("POSTE-C", wd.Select(f => (f, "4.18.26090.9 (4df2f7dd)")).ToArray()));

        var carte = Assert.Single(c);
        Assert.Contains("3 pilotes de Microsoft Defender", carte.Title);
        Assert.Equal(new[] { "POSTE-B" }, carte.Machines);
    }

    [Fact]
    public void Un_pilote_de_constructeur_garde_sa_propre_carte()
    {
        var c = Divergences(
            Poste("POSTE-A", ("tcpip.sys", Recent), ("tpm.sys", Recent), ("rt640x64.sys", "10.073.0815.2024")),
            Poste("POSTE-B", ("tcpip.sys", Ancien), ("tpm.sys", Ancien), ("rt640x64.sys", "10.060.0615.2022")),
            Poste("POSTE-C", ("tcpip.sys", Recent), ("tpm.sys", Recent), ("rt640x64.sys", "10.073.0815.2024")));

        Assert.Equal(2, c.Count);
        Assert.Contains(c, x => x.Title.StartsWith("rt640x64.sys existe en 2 versions"));
        Assert.Contains(c, x => x.Title.Contains("2 pilotes de Windows"));
    }

    [Theory]
    [InlineData("tcpip.sys", "10.0.26100.9549 (WinBuild.160101.0800)", "Windows")]
    [InlineData("WdFilter.sys", "4.18.26090.9 (4df2f7dd)", "Defender")]
    [InlineData("rt640x64.sys", "10.073.0815.2024", "Constructeur")]
    [InlineData("WdcSata.sys", "2.1.0.0", "Constructeur")]
    public void La_famille_se_reconnait_a_ce_que_Windows_dit_du_fichier(string fichier, string version, string attendu)
        => Assert.Equal(attendu, ParkComparator.Famille(fichier, new[] { version }).ToString());
}
