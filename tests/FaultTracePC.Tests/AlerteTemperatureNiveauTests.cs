using FaultTracePC.Core;
using FaultTracePC.Core.Analysis;
using Xunit;

namespace FaultTracePC.Tests;

/// <summary>
/// Point 120, lot C (variante « niveau », décidée par l'auteur le 11/11/2026) : une
/// alerte fondée sur la seule température du processeur n'est plus jamais critique.
/// Côté lecteur : les alertes « crit » écrites avant (jusqu'à la 1.12.0) sont relues
/// « à surveiller ». Cas réel : poste de l'auteur, 17 alertes jusqu'à 100 °C, sans
/// aucune panne.
/// </summary>
[Collection("Langue")]
public class AlerteTemperatureNiveauTests
{
    private static DiagnosticReport AvecAlertesDeTemperature()
    {
        var r = new DiagnosticReport
        {
            GeneratedAt = new DateTime(2026, 11, 12, 21, 31, 0),
            ScanPeriodDays = 30,
            System = new SystemSnapshot { MachineName = "POSTE-TEST" },
        };
        r.Flight.Alerts.Add(new PreventiveAlert { Time = new DateTime(2026, 11, 11, 20, 8, 0), RuleId = "cpu_temp", Level = "warn", Value = 85 });
        r.Flight.Alerts.Add(new PreventiveAlert { Time = new DateTime(2026, 11, 12, 20, 28, 0), RuleId = "cpu_temp", Level = "crit", Value = 100 });
        return r;
    }

    [Fact]
    public void Une_ancienne_alerte_critique_de_temperature_est_relue_a_surveiller()
    {
        var r = AvecAlertesDeTemperature();
        var initial = Lang.Current;
        try
        {
            Lang.Apply(AppLanguage.French);
            AlertCatalog.LocalizeAll(r.Flight.Alerts);
            new RulesEngine().Analyze(r);
        }
        finally { Lang.Apply(initial); }

        var carte = Assert.Single(r.Findings, f => f.Code == "cpu_temp");
        Assert.Equal(Severity.Warning, carte.Severity);
        // Le texte de la carte reste celui de la plus forte valeur.
        Assert.Contains("100 °C", carte.Title);
        Assert.StartsWith("Pas de panne critique", r.Verdict);
        // La boîte noire garde ce qui a été écrit.
        Assert.Contains(r.Flight.Alerts, a => a.Level == "crit");
    }

    [Fact]
    public void Les_autres_alertes_critiques_ne_sont_pas_touchees()
    {
        var a = new PreventiveAlert { RuleId = "commit", Level = "crit", Value = 97 };
        Assert.Same(a, RulesEngine.RelireTemperatureProcesseur(a));
        var g = new PreventiveAlert { RuleId = "gpu_temp", Level = "crit", Value = 96 };
        Assert.Same(g, RulesEngine.RelireTemperatureProcesseur(g));
    }
}
