using FaultTracePC.Core;
using FaultTracePC.Core.Analysis;
using FaultTracePC.Core.Report;
using Xunit;

namespace FaultTracePC.Tests;

/// <summary>
/// Point 63 de la feuille de route. Le script de réparation raisonnait par familles
/// de panne : sur POSTE-TEMOIN, le 17/09/2026, il présentait dix pilotes de 2021 comme
/// « les premiers suspects » alors que l'analyse symbolique nommait nvlddmkm.sys dans
/// cinq vidages sur cinq, et demandait de surveiller une température que la boîte
/// noire avait déjà mesurée pendant 94 heures.
/// </summary>
[Collection("Langue")]
public class ScriptMesuresTests
{
    private static DiagnosticReport RapportType()
    {
        var quand = new DateTime(2026, 9, 17, 17, 38, 0);
        var r = new DiagnosticReport
        {
            GeneratedAt = quand,
            ScanPeriodDays = 30,
            System = new SystemSnapshot { MachineName = "POSTE-TEST" },
        };

        // Trois réinitialisations d'affichage : de quoi déclencher la conclusion graphique.
        for (int i = 0; i < 3; i++)
            r.Events.Add(new WinEvent { Category = EventCategory.Tdr, Provider = "Display", EventId = 4101, TimeLocal = quand.AddDays(-i), Message = "" });

        // Deux vidages analysés qui DÉSIGNENT un module.
        for (int i = 0; i < 2; i++)
            r.Dumps.Add(new DumpFileInfo
            {
                Path = $@"C:\WINDOWS\Minidump\09{17 - i}26-0001-01.dmp",
                Kind = DumpKind.KernelMinidump,
                BugCheckCode = 0x116,
                CrashTimeFromHeader = quand.AddDays(-i),
                LastWriteTime = quand.AddDays(-i),
                DeepAnalyzed = true,
                FaultingModule = "nvlddmkm.sys",
            });

        // Un pilote tiers ancien, qui n'a rien à voir avec la panne.
        r.System.Drivers.Add(new DriverInfo
        {
            Name = "tmusa",
            DisplayName = "Trend Micro Osprey Driver",
            Path = @"C:\WINDOWS\System32\drivers\tmusa.sys",
            CompanyName = "Trend Micro, Inc.",
            FileDate = new DateTime(2021, 3, 4),
            State = "Running",
            IsMicrosoft = false,
        });

        // La température GPU, déjà mesurée pendant des jours.
        r.Flight.Thermal.Add(new ThermalStats
        {
            Sensor = ThermalHistory.CapteurGpu,
            WarnThreshold = 85,
            CritThreshold = 95,
            MaxC = 67.7,
            AverageC = 54.8,
            SampleCount = 30000,
            Observed = TimeSpan.FromHours(94.5),
            AboveWarn = TimeSpan.Zero,
            AboveCrit = TimeSpan.Zero,
        });

        return r;
    }

    private static string ScriptFrancais()
    {
        var initial = Lang.Current;
        try
        {
            Lang.Apply(AppLanguage.French);
            var r = RapportType();
            new RulesEngine().Analyze(r);
            return RepairScriptGenerator.Generate(r);
        }
        finally { Lang.Apply(initial); }
    }

    [Fact]
    public void Le_module_designe_par_windbg_passe_avant_la_liste_des_pilotes_anciens()
    {
        var script = ScriptFrancais();

        Assert.Contains("nvlddmkm.sys", script);
        Assert.Contains("analyse symbolique des vidages", script);
        // Le pilote ancien reste listé, mais il n'est plus présenté comme un suspect.
        Assert.Contains("tmusa.sys", script);
        Assert.DoesNotContain("premiers suspects", script);
    }

    [Fact]
    public void La_temperature_deja_mesuree_remplace_la_consigne_d_aller_la_mesurer()
    {
        var script = ScriptFrancais();

        Assert.Contains("GPU d", script);            // « Température GPU déjà mesurée… »
        Assert.Contains("mesur", script);
        // On ne renvoie plus l'utilisateur relever une température qu'on a sous la main.
        Assert.DoesNotContain("HWiNFO", script);
    }

    [Fact]
    public void La_consigne_du_diagnostic_remplace_la_procedure_toute_faite()
    {
        var script = ScriptFrancais();

        Assert.Contains("Consigne issue du diagnostic", script);
        Assert.DoesNotContain("Display Driver Uninstaller", script);
    }

    [Fact]
    public void Le_script_rappelle_les_conclusions_de_cette_machine()
    {
        var script = ScriptFrancais();

        Assert.Contains("Ce que le diagnostic a trouv", script);
    }
}
