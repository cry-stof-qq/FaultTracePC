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
}
