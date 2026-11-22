using FaultTracePC.Core;
using FaultTracePC.Core.Analysis;
using FaultTracePC.Core.Report;
using Xunit;

namespace FaultTracePC.Tests;

/// <summary>
/// Point 130, constaté au rapport du parc du 17/11/2026 : des pilotes de puce AMD
/// (amdi2c.sys, amdgpio2.sys) « à mettre à jour » sur des postes à processeur Intel,
/// d'après la version installée par le fabricant sur le seul portable AMD du parc.
/// Vérifié le même jour sur un poste Intel : « Stopped », démarrage « Manual » —
/// inscrits par Windows, jamais chargés. Versions reprises du rapport réel.
/// </summary>
[Collection("Langue")]
public class PilotesChargesTests
{
    private static ParkComparator.MachineSummary Poste(string nom, bool amd, bool avecListe)
    {
        var s = new ScanHistory.ScanSummary { GeneratedAt = new DateTime(2026, 11, 17, 8, 15, 0) };
        s.DriverVersions["amdi2c.sys"] = amd ? "1.2.0.126|2024-03-01" : "1.2.0.99|2019-05-01";
        // Témoin : un pilote réseau CHARGÉ partout, en deux versions — sa carte doit rester.
        s.DriverVersions["rt640x64.sys"] = nom.EndsWith("3", StringComparison.Ordinal) ? "10.073.0815.2024|2024-08-15" : "10.060.0615.2022|2022-08-18";
        if (avecListe)
            s.PilotesCharges = amd ? ["amdi2c.sys", "rt640x64.sys"] : ["rt640x64.sys"];
        return new ParkComparator.MachineSummary(nom, s);
    }

    private static List<ParkComparator.Correlation> Cartes(bool avecListe)
    {
        var initial = Lang.Current;
        try
        {
            Lang.Apply(AppLanguage.French);
            return ParkComparator.Analyze(
                [Poste("POSTE-1", false, avecListe), Poste("POSTE-2", false, avecListe), Poste("POSTE-3", false, avecListe), Poste("PORTABLE-AMD", true, avecListe)],
                new DateTime(2026, 11, 17, 9, 0, 0)).Correlations;
        }
        finally { Lang.Apply(initial); }
    }

    [Fact]
    public void Un_pilote_inscrit_mais_jamais_charge_n_est_plus_compare()
    {
        var cartes = Cartes(avecListe: true);

        Assert.DoesNotContain(cartes, c => c.Title.Contains("amdi2c", StringComparison.OrdinalIgnoreCase)
                                        || c.Details.Contains("amdi2c", StringComparison.OrdinalIgnoreCase));
        // Le pilote chargé partout garde sa carte de divergence.
        Assert.Contains(cartes, c => c.Kind == "divergence" && c.Title.Contains("rt640x64.sys", StringComparison.Ordinal));
    }

    [Fact]
    public void Un_resume_ancien_sans_la_liste_garde_le_comportement_d_avant()
    {
        var cartes = Cartes(avecListe: false);

        Assert.Contains(cartes, c => c.Kind == "divergence" && c.Title.Contains("amdi2c.sys", StringComparison.Ordinal));
    }

    [Fact]
    public void Le_resume_ne_garde_que_les_pilotes_charges()
    {
        var pilotes = new List<DriverInfo>
        {
            new() { Name = "amdi2c", Path = @"C:\Windows\system32\drivers\amdi2c.sys", State = "Stopped", StartMode = "Manual", FileVersion = "1.2.0.99" },
            new() { Name = "rt640x64", Path = @"C:\Windows\system32\DRIVERS\rt640x64.sys", State = "Running", StartMode = "Manual", FileVersion = "10.060.0615.2022" },
            new() { Name = "sansChemin", Path = "", State = "Running" },
        };

        Assert.Equal(["rt640x64.sys"], ScanHistory.PilotesCharges(pilotes));
    }

    [Fact]
    public void Sans_aucun_etat_lu_la_liste_est_absente_et_tout_reste_compare()
    {
        var pilotes = new List<DriverInfo>
        {
            new() { Name = "amdi2c", Path = @"C:\Windows\system32\drivers\amdi2c.sys", FileVersion = "1.2.0.99" },
        };

        Assert.Null(ScanHistory.PilotesCharges(pilotes));
    }
}
