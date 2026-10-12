using FaultTracePC.Core;
using FaultTracePC.Core.Analysis;
using FaultTracePC.Core.Report;
using Xunit;

namespace FaultTracePC.Tests;

/// <summary>
/// Point 95 de la feuille de route, constaté le 12/10/2026 sur un rapport du parc
/// réel : le poste qui avait les pilotes de Windows les plus récents
/// (10.0.26100.9549) était désigné « en retard » parce que les deux autres postes
/// avaient la même version, plus ancienne (10.0.26100.9278). Le conseil revenait
/// à lui faire installer une version plus ancienne.
/// </summary>
[Collection("Langue")]
public class ParcDivergenceTests
{
    private static ParkComparator.MachineSummary Poste(string nom, string fichier, string version)
    {
        var s = new ScanHistory.ScanSummary { GeneratedAt = DateTime.Now };
        s.DriverVersions[fichier] = $"{version}|{DateTime.Now.AddMonths(-1):yyyy-MM-dd}";
        return new ParkComparator.MachineSummary(nom, s);
    }

    private static ParkComparator.Correlation Divergence(params ParkComparator.MachineSummary[] postes)
    {
        var c = ParkComparator.Analyze(postes).Correlations.FirstOrDefault(x => x.Kind == "divergence");
        Assert.NotNull(c);
        return c!;
    }

    [Fact]
    public void Le_poste_le_plus_a_jour_n_est_pas_dit_en_retard_meme_minoritaire()
    {
        var initial = Lang.Current;
        try
        {
            Lang.Apply(AppLanguage.French);
            var c = Divergence(
                Poste("POSTE-A", "tcpip.sys", "10.0.26100.9549 (WinBuild.160101.0800)"),
                Poste("POSTE-B", "tcpip.sys", "10.0.26100.9278 (WinBuild.160101.0800)"),
                Poste("POSTE-C", "tcpip.sys", "10.0.26100.9278 (WinBuild.160101.0800)"));

            Assert.Equal(new[] { "POSTE-B", "POSTE-C" }, c.Machines);
            Assert.Contains("10.0.26100.9549", c.Action);
            Assert.DoesNotContain("majoritaire", c.Details + c.Action);
        }
        finally { Lang.Apply(initial); }
    }

    [Fact]
    public void Trois_versions_sur_trois_postes_seuls_les_plus_anciens_sont_nommes()
    {
        var initial = Lang.Current;
        try
        {
            Lang.Apply(AppLanguage.French);
            var c = Divergence(
                Poste("POSTE-A", "RTKVHD64.sys", "6.0.9496.1"),
                Poste("POSTE-B", "RTKVHD64.sys", "6.0.10016.1"),
                Poste("POSTE-C", "RTKVHD64.sys", "6.0.8923.1 built by: WinDDK"));

            // 6.0.10016.1 est plus récente que 6.0.9496.1 : comparaison numérique,
            // pas alphabétique (« 9496 » passerait devant « 10016 » en texte).
            Assert.Equal(new[] { "POSTE-A", "POSTE-C" }, c.Machines);
            Assert.Contains("6.0.10016.1", c.Action);
        }
        finally { Lang.Apply(initial); }
    }

    [Fact]
    public void Versions_incomparables_personne_n_est_designe_en_retard()
    {
        var initial = Lang.Current;
        try
        {
            Lang.Apply(AppLanguage.French);
            var c = Divergence(
                Poste("POSTE-A", "outil.sys", "build-alpha"),
                Poste("POSTE-B", "outil.sys", "build-alpha"),
                Poste("POSTE-C", "outil.sys", "build-beta"));

            Assert.Equal(new[] { "POSTE-A", "POSTE-B", "POSTE-C" }, c.Machines);
            Assert.Contains("ne se comparent pas", c.Details);
            Assert.DoesNotContain("en retard", c.Action);
        }
        finally { Lang.Apply(initial); }
    }

    [Theory]
    [InlineData("10.0.26100.9549 (WinBuild.160101.0800)", "10.0.26100.9549")]
    [InlineData("6.0.8923.1 built by: WinDDK", "6.0.8923.1")]
    [InlineData("4.18.26080.4 (ccacee407da609d5c2c557d13db39624e32a0227)", "4.18.26080.4")]
    [InlineData("32.0.1", "32.0.1")]
    public void Numero_de_version_lu_en_tete(string texte, string attendu) =>
        Assert.Equal(Version.Parse(attendu), ParkComparator.NumeroDeVersion(texte));

    [Theory]
    [InlineData("")]
    [InlineData("build-alpha")]
    [InlineData("v2")]
    public void Numero_de_version_absent(string texte) =>
        Assert.Null(ParkComparator.NumeroDeVersion(texte));
}
