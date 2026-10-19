using FaultTracePC.Core;
using FaultTracePC.Core.Analysis;
using FaultTracePC.Core.Report;
using Xunit;

namespace FaultTracePC.Tests;

/// <summary>
/// POINT 99, lot 99a. Rapports du parc des 12/10 et 19/10/2026 : quatre postes
/// d'une même série avec des erreurs WHEA, et aucune carte qui les rapproche.
/// </summary>
[Collection("Langue")]
public class ParcMemeAlerteTests
{
    private static ParkComparator.MachineSummary Poste(string nom, int whea) =>
        new(nom, new ScanHistory.ScanSummary { GeneratedAt = DateTime.Now, WheaEvents = whea });

    private static List<ParkComparator.Correlation> Analyser(params ParkComparator.MachineSummary[] postes)
    {
        var initial = Lang.Current;
        try { Lang.Apply(AppLanguage.French); return ParkComparator.Analyze(postes).Correlations; }
        finally { Lang.Apply(initial); }
    }

    [Fact]
    public void Plusieurs_postes_en_erreur_WHEA_sont_rapproches_en_une_carte()
    {
        var c = Analyser(Poste("POSTE-A", 12), Poste("POSTE-B", 0), Poste("POSTE-C", 34), Poste("POSTE-D", 3));

        var carte = Assert.Single(c, x => x.Title.StartsWith("Erreurs matérielles (WHEA)"));
        Assert.Contains("sur 3 postes sur 4", carte.Title);
        Assert.Equal(new[] { "POSTE-A", "POSTE-C", "POSTE-D" }, carte.Machines);
        // Du plus touché au moins touché, avec le nombre de chacun.
        Assert.Contains("POSTE-C (34), POSTE-A (12), POSTE-D (3)", carte.Details);
        Assert.Contains("version du BIOS", carte.Action);
        Assert.Equal("warn", carte.Severity);
    }

    [Fact]
    public void Un_seul_poste_en_erreur_ne_fait_pas_de_carte_de_parc()
    {
        var c = Analyser(Poste("POSTE-A", 12), Poste("POSTE-B", 0), Poste("POSTE-C", 0));
        Assert.DoesNotContain(c, x => x.Title.StartsWith("Erreurs matérielles (WHEA)"));
    }

    // ── Lot 99b : modèle, BIOS, composant, erreurs fatales ──────────────────────

    private static ParkComparator.MachineSummary Complet(string nom, int whea, int fatales, string modele, string bios, params string[] composants)
    {
        var s = new ScanHistory.ScanSummary
        {
            GeneratedAt = DateTime.Now, WheaEvents = whea, WheaFatales = fatales,
            Modele = modele, VersionBios = bios,
        };
        s.WheaComposants.AddRange(composants);
        return new(nom, s);
    }

    [Fact]
    public void Meme_modele_meme_BIOS_meme_lien_la_carte_le_dit()
    {
        var c = Analyser(
            Complet("POSTE-A", 12, 0, "FABRICANT MODELE-X", "1.2.3", "PCI Express Root Port 0x0:0x6:0x0", "PCI Express Root Port 0x0:0x1:0x0"),
            Complet("POSTE-B", 3, 0, "FABRICANT MODELE-X", "1.2.3", "PCI Express Root Port 0x0:0x1:0x0"));

        var carte = Assert.Single(c, x => x.Title.StartsWith("Erreurs matérielles (WHEA)"));
        Assert.Contains("Même modèle pour les 2 postes dont il est connu : FABRICANT MODELE-X, même version de BIOS (1.2.3)", carte.Details);
        Assert.Contains("Même composant mis en cause sur ces postes : PCI Express Root Port 0x0:0x1:0x0", carte.Details);
        Assert.DoesNotContain("1.9.0", carte.Details);
        Assert.Equal("warn", carte.Severity);
    }

    [Fact]
    public void Une_erreur_fatale_rend_la_carte_critique()
    {
        var c = Analyser(
            Complet("POSTE-A", 12, 1, "FABRICANT MODELE-X", "1.2.3"),
            Complet("POSTE-B", 3, 0, "FABRICANT MODELE-X", "1.2.4"));

        var carte = Assert.Single(c, x => x.Title.StartsWith("Erreurs matérielles (WHEA)"));
        Assert.Equal("crit", carte.Severity);
        Assert.Contains("POSTE-A (12, dont 1 fatale(s))", carte.Details);
        Assert.Contains("2 versions de BIOS différentes (1.2.3, 1.2.4)", carte.Details);
    }

    [Fact]
    public void Un_poste_plus_ancien_ne_fait_pas_conclure_sur_le_modele()
    {
        var c = Analyser(
            Complet("POSTE-A", 12, 0, "FABRICANT MODELE-X", "1.2.3"),
            Poste("POSTE-B", 3));

        var carte = Assert.Single(c, x => x.Title.StartsWith("Erreurs matérielles (WHEA)"));
        Assert.DoesNotContain("Même modèle", carte.Details);
        Assert.Contains("ne sont transmis que par les postes en 1.9.0 ou plus", carte.Details);
    }

    [Fact]
    public void Le_resume_transmet_modele_BIOS_composant_et_erreurs_fatales()
    {
        var r = new DiagnosticReport { GeneratedAt = new DateTime(2026, 10, 19, 10, 0, 0), ScanPeriodDays = 30 };
        r.System.Bios.SystemManufacturer = "FABRICANT";
        r.System.Bios.SystemModel = "MODELE-X";
        r.System.Bios.Version = "1.2.3";
        WinEvent Whea(int id) => new()
        {
            Category = EventCategory.Whea, Provider = "Microsoft-Windows-WHEA-Logger", EventId = id,
            TimeLocal = new DateTime(2026, 10, 15, 16, 18, 0),
            Extracted = { ["Composant"] = "PCI Express Root Port", ["Bdf"] = "0x0:0x1:0x0" },
        };
        r.Events.Add(Whea(17)); r.Events.Add(Whea(17)); r.Events.Add(Whea(18));

        var s = ScanHistory.Summarize(r);

        Assert.Equal("FABRICANT MODELE-X", s.Modele);
        Assert.Equal("1.2.3", s.VersionBios);
        Assert.Equal(3, s.WheaEvents);
        Assert.Equal(1, s.WheaFatales);
        Assert.Equal(new[] { "PCI Express Root Port 0x0:0x1:0x0" }, s.WheaComposants);
    }
}
