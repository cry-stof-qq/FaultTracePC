using System.Text.Json;
using FaultTracePC.Core;
using FaultTracePC.Core.Report;
using Xunit;

namespace FaultTracePC.Tests;

/// <summary>
/// État de santé des disques. Le point sensible n'est pas la conversion : c'est
/// que les fichiers DÉJÀ écrits sur les postes restent lisibles. L'historique est
/// conservé 90 jours, et une mise à jour par GPO remplace le logiciel sans jamais
/// toucher aux fichiers qu'il a produits.
/// </summary>
[Collection("Langue")]
public class DiskHealthTests
{
    // --- Lecture des codes Windows ----------------------------------------

    [Theory]
    [InlineData(0, DiskHealth.Healthy)]
    [InlineData(1, DiskHealth.Warning)]
    [InlineData(2, DiskHealth.Failing)]
    [InlineData(5, DiskHealth.Unknown)]     // valeur documentée « Unknown »
    [InlineData(42, DiskHealth.Unknown)]    // valeur non documentée : surtout pas « sain »
    public void Codes_wmi_traduits(int code, DiskHealth attendu)
    {
        Assert.Equal(attendu, DiskHealthInfo.FromWmi((ushort)code));
    }

    // --- Compatibilité avec les fichiers déjà écrits -----------------------

    [Theory]
    [InlineData("Sain", DiskHealth.Healthy)]
    [InlineData("Avertissement", DiskHealth.Warning)]
    [InlineData("Défaillant", DiskHealth.Failing)]
    [InlineData("Inconnu", DiskHealth.Unknown)]
    [InlineData("Healthy", DiskHealth.Healthy)]
    [InlineData("Unhealthy", DiskHealth.Failing)]
    [InlineData("", DiskHealth.NotReported)]
    public void Anciens_libelles_relus(string ecrit, DiskHealth attendu)
    {
        var json = $$"""{"Model":"Samsung SSD 980","Health":"{{ecrit}}"}""";
        var brief = JsonSerializer.Deserialize<ScanHistory.DiskBrief>(json)!;
        Assert.Equal(attendu, brief.Health);
    }

    [Fact]
    public void Champ_absent_ou_nul_ne_vaut_pas_sain()
    {
        // La règle de fond du logiciel : une mesure manquante n'est pas un bon
        // résultat. Un « 0 » lu comme « Healthy » serait le pire des bugs ici.
        Assert.Equal(DiskHealth.NotReported,
            JsonSerializer.Deserialize<ScanHistory.DiskBrief>("""{"Model":"x"}""")!.Health);
        Assert.Equal(DiskHealth.NotReported,
            JsonSerializer.Deserialize<ScanHistory.DiskBrief>("""{"Health":null}""")!.Health);
    }

    [Fact]
    public void Ce_qui_est_ecrit_est_relisible()
    {
        var json = JsonSerializer.Serialize(new ScanHistory.DiskBrief { Model = "s", Health = DiskHealth.Failing });
        Assert.Contains("\"Failing\"", json);   // le nom, jamais un numéro
        Assert.Equal(DiskHealth.Failing, JsonSerializer.Deserialize<ScanHistory.DiskBrief>(json)!.Health);
    }

    [Fact]
    public void Un_resume_ecrit_par_la_1_2_3_declenche_encore_l_alerte()
    {
        // LA régression que cette énumération doit empêcher : pendant 90 jours,
        // le scan précédent vient d'une version qui écrivait « Sain » en français.
        var json = """
            {
              "GeneratedAt": "2026-08-01T10:00:00",
              "ScanPeriodDays": 30,
              "Disks": [ { "Model": "Samsung SSD 980", "Health": "Sain", "WearPercent": 1 } ]
            }
            """;
        var precedent = JsonSerializer.Deserialize<ScanHistory.ScanSummary>(json)!;

        var actuel = new DiagnosticReport
        {
            GeneratedAt = new DateTime(2026, 8, 15, 10, 0, 0),
            ScanPeriodDays = 30,
            System = new SystemSnapshot
            {
                MachineName = "POSTE-01",
                Disks = [new DiskInfo { Model = "Samsung SSD 980", Health = DiskHealth.Failing, WearPercent = 1 }],
            },
        };

        var c = ScanHistory.Compare(actuel, precedent);
        Assert.Equal("crit", c.HardwareSeverity);
        Assert.Contains(c.HardwareConcerns, h => h.Message.Contains("état de santé"));
    }

    // --- Classement --------------------------------------------------------

    [Fact]
    public void Un_etat_non_mesure_ne_se_classe_pas()
    {
        // Sans ce refus, passer de « inconnu » à « sain » compterait comme une
        // amélioration, et « sain » → « inconnu » comme une dégradation : deux
        // conclusions tirées d'une mesure qui n'a pas eu lieu.
        Assert.Equal(-1, DiskHealth.NotReported.Rank());
        Assert.Equal(-1, DiskHealth.Unknown.Rank());
        Assert.True(DiskHealth.Failing.Rank() > DiskHealth.Warning.Rank());
        Assert.True(DiskHealth.Warning.Rank() > DiskHealth.Healthy.Rank());
    }

    [Fact]
    public void Seuls_avertissement_et_defaillant_sont_degrades()
    {
        Assert.True(DiskHealth.Warning.IsDegraded());
        Assert.True(DiskHealth.Failing.IsDegraded());
        Assert.False(DiskHealth.Healthy.IsDegraded());
        Assert.False(DiskHealth.Unknown.IsDegraded());
        Assert.False(DiskHealth.NotReported.IsDegraded());
    }

    // --- Affichage ---------------------------------------------------------

    [Fact]
    public void Le_libelle_suit_la_langue_mais_la_decision_non()
    {
        var initial = Lang.Current;
        try
        {
            Lang.Apply(AppLanguage.English);
            Assert.Equal("Failing", DiskHealth.Failing.Label());
            // Le point de tout le lot : la décision ne dépend PAS de la langue.
            Assert.True(DiskHealth.Failing.IsDegraded());

            Lang.Apply(AppLanguage.French);
            Assert.Equal("Défaillant", DiskHealth.Failing.Label());
            Assert.True(DiskHealth.Failing.IsDegraded());
        }
        finally
        {
            Lang.Apply(initial);
        }
    }
}
