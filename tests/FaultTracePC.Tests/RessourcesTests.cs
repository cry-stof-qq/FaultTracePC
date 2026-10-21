using FaultTracePC.Core;
using FaultTracePC.Core.Analysis;
using FaultTracePC.Core.Report;
using Xunit;

namespace FaultTracePC.Tests;

/// <summary>
/// Point 115 de la feuille de route. Constaté le 21/10/2026 sur un poste du parc
/// (64 Go de RAM, poste d'une enseignante qui y fait tourner beaucoup de logiciels) :
/// une fois la carte WHEA ramenée à « avertissement », le verdict est devenu
/// « LOGICIELLE (corruption système ou application) », porté par une seule carte
/// critique — « Mémoire virtuelle presque saturée : 99 % » — et la boîte à outils
/// proposait sfc /scannow puis DISM. Une mémoire saturée par un programme n'est pas
/// une corruption de Windows.
/// </summary>
[Collection("Langue")]
public class RessourcesTests
{
    private static DiagnosticReport AvecSaturation()
    {
        var r = new DiagnosticReport
        {
            GeneratedAt = new DateTime(2026, 10, 21, 12, 32, 0),
            ScanPeriodDays = 30,
            System = new SystemSnapshot { MachineName = "POSTE-TEST" },
        };
        r.Flight.Alerts.Add(new PreventiveAlert { Time = new DateTime(2026, 10, 8, 18, 56, 0), RuleId = "commit", Level = "crit", Value = 98 });
        r.Flight.Alerts.Add(new PreventiveAlert { Time = new DateTime(2026, 10, 13, 15, 20, 0), RuleId = "commit", Level = "crit", Value = 99 });
        return r;
    }

    private static void EnFrancais(Action action)
    {
        var initial = Lang.Current;
        try { Lang.Apply(AppLanguage.French); action(); }
        finally { Lang.Apply(initial); }
    }

    [Fact]
    public void Une_memoire_saturee_n_est_pas_une_corruption_de_Windows()
    {
        var r = AvecSaturation();
        EnFrancais(() =>
        {
            AlertCatalog.LocalizeAll(r.Flight.Alerts);
            new RulesEngine().Analyze(r);
        });

        var carte = Assert.Single(r.Findings, f => f.Code == "commit");
        Assert.Equal(FaultCategory.Resources, carte.Category);
        Assert.Equal(FaultCategory.Resources, r.VerdictCategory);
        Assert.Contains("SATURATION DE LA MÉMOIRE", r.Verdict);
        Assert.DoesNotContain("LOGICIELLE", r.Verdict);
        Assert.DoesNotContain("corruption", r.Verdict);
    }

    [Fact]
    public void Le_script_de_reparation_parle_de_memoire_et_pas_de_sfc()
    {
        var r = AvecSaturation();
        string script = "";
        EnFrancais(() =>
        {
            AlertCatalog.LocalizeAll(r.Flight.Alerts);
            new RulesEngine().Analyze(r);
            script = RepairScriptGenerator.Generate(r);
        });

        // La section qui regarde le fichier d'échange et la virtualisation…
        Assert.Contains("Section 'Mémoire'", script);
        // … et pas la réparation des fichiers de Windows.
        Assert.DoesNotContain("sfc /scannow", script);
        // … ni le diagnostic de la RAM physique, qui redémarre la machine.
        Assert.DoesNotContain("mdsched", script);
    }

    [Fact]
    public void La_categorie_s_affiche_dans_les_deux_langues()
    {
        var r = AvecSaturation();
        var initial = Lang.Current;
        try
        {
            Lang.Apply(AppLanguage.English);
            AlertCatalog.LocalizeAll(r.Flight.Alerts);
            new RulesEngine().Analyze(r);
        }
        finally { Lang.Apply(initial); }

        Assert.Contains("MEMORY EXHAUSTION", r.Verdict);
        Assert.DoesNotContain("SATURATION", r.Verdict);
    }
}
