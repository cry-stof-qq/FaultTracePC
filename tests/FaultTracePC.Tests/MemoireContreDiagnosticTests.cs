using FaultTracePC.Core;
using FaultTracePC.Core.Analysis;
using FaultTracePC.Core.Collectors;
using Xunit;

namespace FaultTracePC.Tests;

/// <summary>
/// POINT 82, LOT 2 — les codes d'arrêt « mémoire » confrontés au diagnostic
/// mémoire Windows (mdsched), et la lecture corrigée de son résultat.
/// </summary>
[Collection("Langue")]
public class MemoireContreDiagnosticTests
{
    private static Finding ParCodeArret(uint code, FaultCategory categorie = FaultCategory.Memory) => new()
    {
        Severity = Severity.Critical,
        Confidence = Confidence.High,
        Category = categorie,
        Code = $"{RulesEngine.PrefixeCodeArret}{code:X}",
        Title = "BSOD",
        Details = "Codes.",
    };

    private static WinEvent Diagnostic(bool enErreur, DateTime quand) => new()
    {
        TimeLocal = quand,
        Category = EventCategory.MemoryDiag,
        EventId = enErreur ? 1202 : 1201,
        Extracted = { ["HasErrors"] = enErreur.ToString() },
    };

    private static DiagnosticReport Rapport(Finding f, params WinEvent[] evenements)
    {
        var r = new DiagnosticReport();
        r.Findings.Add(f);
        r.Events.AddRange(evenements);
        return r;
    }

    private static void EnFrancais(Action a)
    {
        var initial = Lang.Current;
        try { Lang.Apply(AppLanguage.French); a(); }
        finally { Lang.Apply(initial); }
    }

    [Theory]
    [InlineData(1101, false)]
    [InlineData(1201, false)]
    [InlineData(1102, true)]
    [InlineData(1202, true)]
    public void Les_quatre_identifiants_du_diagnostic_sont_lus(int id, bool enErreur)
    {
        // Jusqu'au 10/10/2026, 1102 était lu « aucune erreur ».
        Assert.Equal(enErreur, EventLogCollector.DiagnosticMemoireEnErreur(id));
    }

    [Fact]
    public void Un_identifiant_inconnu_n_est_pas_interprete()
    {
        Assert.Null(EventLogCollector.DiagnosticMemoireEnErreur(1001));
    }

    [Fact]
    public void Diagnostic_sans_erreur_la_confiance_tombe()
    {
        var f = ParCodeArret(0x1A);
        EnFrancais(() => RulesEngine.ConfronterMemoireAuDiagnostic(Rapport(f, Diagnostic(false, new DateTime(2026, 9, 20)))));

        Assert.Equal(Confidence.Low, f.Confidence);
        Assert.Contains("du 20/09/2026 n'a trouvé aucune erreur", f.Details);
        Assert.Contains("MemTest86", f.Details);
    }

    [Fact]
    public void Sans_diagnostic_le_rapport_dit_que_la_memoire_n_a_pas_ete_testee()
    {
        var f = ParCodeArret(0x1A);
        EnFrancais(() => RulesEngine.ConfronterMemoireAuDiagnostic(Rapport(f)));

        Assert.Equal(Confidence.High, f.Confidence);
        Assert.Contains("la mémoire n'a pas été testée", f.Details);
    }

    [Fact]
    public void Diagnostic_en_erreur_rien_n_est_ajoute()
    {
        var f = ParCodeArret(0x1A);
        RulesEngine.ConfronterMemoireAuDiagnostic(Rapport(f, Diagnostic(true, new DateTime(2026, 9, 20))));
        Assert.Equal(Confidence.High, f.Confidence);
        Assert.Equal("Codes.", f.Details);
    }

    [Fact]
    public void Un_code_d_arret_stockage_n_est_pas_touche()
    {
        var f = ParCodeArret(0x154, FaultCategory.Storage);
        RulesEngine.ConfronterMemoireAuDiagnostic(Rapport(f));
        Assert.Equal("Codes.", f.Details);
    }

    [Fact]
    public void Le_verdict_memoire_cite_le_diagnostic_sans_erreur()
    {
        var r = Rapport(ParCodeArret(0x1A), Diagnostic(false, new DateTime(2026, 9, 20)));
        EnFrancais(() => RulesEngine.ComputeVerdict(r));

        Assert.StartsWith("Cause la plus probable : MÉMOIRE RAM", r.Verdict);
        Assert.Contains("du 20/09/2026 n'a trouvé aucune erreur", r.Verdict);
    }

    [Fact]
    public void Le_verdict_memoire_dit_quand_rien_n_a_ete_teste()
    {
        var r = Rapport(ParCodeArret(0x1A));
        EnFrancais(() => RulesEngine.ComputeVerdict(r));
        Assert.Contains("La mémoire n'a pas été testée", r.Verdict);
    }
}
