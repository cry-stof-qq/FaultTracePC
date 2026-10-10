using FaultTracePC.Core;
using FaultTracePC.Core.Analysis;
using Xunit;

namespace FaultTracePC.Tests;

/// <summary>
/// POINT 82 — un verdict « stockage » confronté à la mesure du disque.
///
/// Cas réel du 24/09/2026 : codes d'arrêt 0x154 et 0x7A, verdict « STOCKAGE »,
/// et pourtant un SSD mesuré sans aucun défaut (0 secteur réalloué, 0 erreur de
/// transfert, 0 % d'usure). Le même rapport contenait aussi 10 réinitialisations
/// signalées par le contrôleur de stockage : c'est le second cas testé ici.
/// </summary>
[Collection("Langue")]
public class StockageContreMesureTests
{
    private static Finding ParCodeArret(uint code, FaultCategory categorie = FaultCategory.Storage) => new()
    {
        Severity = Severity.Critical,
        Confidence = Confidence.High,
        Category = categorie,
        Code = $"{RulesEngine.PrefixeCodeArret}{code:X}",
        Title = "BSOD",
        Details = "Codes.",
    };

    private static DiskInfo Disque(string lettre, ulong reallouees = 0) => new()
    {
        Model = "SSD de test",
        Letters = { lettre },
        WearPercent = 0,
        Smart = new SmartInfo { ReallocatedSectors = reallouees, PendingSectors = 0, UncorrectableSectors = 0, UdmaCrcErrors = 0 },
    };

    private static DiagnosticReport Rapport(DiskInfo? disque, params Finding[] conclusions)
    {
        var r = new DiagnosticReport();
        if (disque is not null) r.System.Disks.Add(disque);
        r.Findings.AddRange(conclusions);
        return r;
    }

    private static void EnFrancais(Action a)
    {
        var initial = Lang.Current;
        try { Lang.Apply(AppLanguage.French); a(); }
        finally { Lang.Apply(initial); }
    }

    [Fact]
    public void Codes_seuls_contre_un_disque_sain_la_confiance_tombe()
    {
        var f = ParCodeArret(0x154);
        EnFrancais(() => RulesEngine.ConfronterStockageALaMesure(Rapport(Disque("C:"), f), "C:"));

        Assert.Equal(Confidence.Low, f.Confidence);
        Assert.Contains("la mesure ne le confirme pas", f.Details);
        Assert.Contains("SSD de test (disque système)", f.Details);
        Assert.Contains("Ne pas remplacer le disque", f.Details);
    }

    [Fact]
    public void Avec_d_autres_alertes_de_stockage_la_piste_tient_mais_le_disque_est_dit_sain()
    {
        // Le cas réel du 24/09/2026 : des réinitialisations du contrôleur.
        var f = ParCodeArret(0x154);
        var controleur = new Finding { Severity = Severity.Critical, Category = FaultCategory.Storage, Title = "Contrôleur" };
        EnFrancais(() => RulesEngine.ConfronterStockageALaMesure(Rapport(Disque("C:"), f, controleur), "C:"));

        Assert.Equal(Confidence.High, f.Confidence);
        Assert.Contains("D'autres alertes de stockage existent", f.Details);
    }

    [Fact]
    public void Un_disque_qui_a_des_secteurs_reallouees_ne_contredit_rien()
    {
        var f = ParCodeArret(0x7A);
        RulesEngine.ConfronterStockageALaMesure(Rapport(Disque("C:", reallouees: 5), f), "C:");
        Assert.Equal(Confidence.High, f.Confidence);
        Assert.Equal("Codes.", f.Details);
    }

    [Fact]
    public void Une_mesure_absente_n_est_pas_une_mesure_saine()
    {
        var sansSmart = new DiskInfo { Model = "SSD", Letters = { "C:" } };
        var f = ParCodeArret(0x154);
        RulesEngine.ConfronterStockageALaMesure(Rapport(sansSmart, f), "C:");
        Assert.Equal("Codes.", f.Details);

        // Disque système non identifié : rien non plus.
        var g = ParCodeArret(0x154);
        RulesEngine.ConfronterStockageALaMesure(Rapport(Disque("D:"), g), "C:");
        Assert.Equal("Codes.", g.Details);
    }

    [Fact]
    public void Un_code_d_arret_memoire_n_est_pas_touche()
    {
        var f = ParCodeArret(0x1A, FaultCategory.Memory);
        RulesEngine.ConfronterStockageALaMesure(Rapport(Disque("C:"), f), "C:");
        Assert.Equal(Confidence.High, f.Confidence);
        Assert.Equal("Codes.", f.Details);
    }

    [Fact]
    public void Le_verdict_stockage_dit_que_le_disque_ne_montre_rien()
    {
        // Le verdict identifie le disque système par la variable SystemDrive de la
        // machine qui exécute les tests ; le disque de test porte donc cette lettre.
        var lettre = Environment.GetEnvironmentVariable("SystemDrive") ?? "C:";
        var r = Rapport(Disque(lettre), ParCodeArret(0x154));
        EnFrancais(() => RulesEngine.ComputeVerdict(r));

        Assert.StartsWith("Cause la plus probable : STOCKAGE", r.Verdict);
        Assert.Contains("ne montre aucun défaut mesurable", r.Verdict);
    }
}
