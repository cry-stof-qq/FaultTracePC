using FaultTracePC.Core;
using FaultTracePC.Core.Analysis;
using FaultTracePC.Core.Collectors;
using Xunit;

namespace FaultTracePC.Tests;

/// <summary>
/// Points 48 et 49 de la feuille de route. Constatés le 06/09/2026 sur S2-00-32-2025 :
/// vingt-sept erreurs WHEA identiques, toutes CORRIGÉES et toutes sur un port racine
/// PCI Express, étaient annoncées comme « le processeur a signalé 27 erreurs
/// matérielles », classées critiques, et suivies d'une recommandation qui faisait
/// retirer l'XMP et suspecter l'alimentation. Le triplet bus:appareil:fonction, seule
/// donnée qui nomme le lien fautif, était coupé par la troncature du message.
/// </summary>
[Collection("Langue")]
public class WheaSourceTests
{
    /// <summary>Un message WHEA réaliste : le triplet est tout à la fin, après 600 caractères.</summary>
    private static string MessageWhea(string composant, string bdf) =>
        "Une erreur matérielle corrigée s'est produite. "
        + new string('.', 620)
        + $" Composant : {composant}"
        + " Source de l'erreur : Advanced Error Reporting (PCI Express)"
        + $" Bus principal :Appareil :Fonction : {bdf}";

    private static WinEvent Whea(int id, string composant, string bdf, DateTime quand)
    {
        var message = MessageWhea(composant, bdf);
        var e = new WinEvent
        {
            Category = EventCategory.Whea,
            Provider = "Microsoft-Windows-WHEA-Logger",
            EventId = id,
            TimeLocal = quand,
            Message = message.Length > 600 ? message[..600] : message,
        };
        EventLogCollector.ExtraireWhea(e, message);   // sur le message COMPLET
        return e;
    }

    private static DiagnosticReport Rapport() => new()
    {
        GeneratedAt = new DateTime(2026, 9, 6, 12, 0, 0),
        ScanPeriodDays = 30,
        System = new SystemSnapshot { MachineName = "POSTE-TEST" },
    };

    private static Finding Conclusion(DiagnosticReport r)
    {
        var initial = Lang.Current;
        try { Lang.Apply(AppLanguage.French); new RulesEngine().Analyze(r); }
        finally { Lang.Apply(initial); }
        return r.Findings.First(f => f.Code == "whea");
    }

    [Fact]
    public void Le_triplet_survit_a_la_troncature_du_message()
    {
        var e = Whea(17, "PCI Express Root Port", "0x0:0x1:0x0", DateTime.Now);

        // Le message affiché est bien coupé…
        Assert.Equal(600, e.Message.Length);
        Assert.DoesNotContain("0x0:0x1:0x0", e.Message);
        // …mais l'information décisive a été mise de côté avant la coupe.
        Assert.Equal("0x0:0x1:0x0", e.Extracted["Bdf"]);
        Assert.Equal("PCI Express Root Port", e.Extracted["Composant"]);
    }

    [Fact]
    public void Vingt_sept_erreurs_corrigees_sur_un_lien_pcie_ne_sont_pas_critiques()
    {
        var r = Rapport();
        for (int i = 0; i < 27; i++)
            r.Events.Add(Whea(17, "PCI Express Root Port", "0x0:0x1:0x0", DateTime.Now.AddHours(-i)));

        var f = Conclusion(r);

        // Corrigée veut dire que le matériel a récupéré : ce n'est pas une panne fatale.
        Assert.Equal(Severity.Warning, f.Severity);
        Assert.Contains("CORRIG", f.Title);
        Assert.Contains("PCI Express", f.Title);
    }

    [Fact]
    public void La_conclusion_nomme_le_lien_et_non_le_processeur()
    {
        var r = Rapport();
        for (int i = 0; i < 5; i++)
            r.Events.Add(Whea(17, "PCI Express Root Port", "0x0:0x1:0x0", DateTime.Now.AddHours(-i)));

        var f = Conclusion(r);

        Assert.Contains("0x0:0x1:0x0", f.Details);
        Assert.Contains("PCI Express Root Port", f.Details);
        // Et la recommandation cesse d'envoyer au mauvais endroit.
        Assert.Contains("erreurs de LIEN", f.Recommendation);
        Assert.DoesNotContain("MemTest", f.Recommendation);
    }

    [Fact]
    public void Une_erreur_fatale_reste_critique_et_garde_le_conseil_materiel()
    {
        var r = Rapport();
        r.Events.Add(Whea(18, "Processor Core", "0x0:0x0:0x0", DateTime.Now));

        var f = Conclusion(r);

        Assert.Equal(Severity.Critical, f.Severity);
        Assert.Contains("températures", f.Recommendation);
    }
}
