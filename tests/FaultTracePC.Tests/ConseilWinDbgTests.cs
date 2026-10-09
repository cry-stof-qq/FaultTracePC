using FaultTracePC.Core;
using FaultTracePC.Core.Analysis;
using Xunit;

namespace FaultTracePC.Tests;

/// <summary>
/// POINT 80, LOT B — la fenêtre qui suit l'analyse.
///
/// Constaté le 24/09/2026 : WinDbg était installé mais Windows refusait de le
/// lancer, et la fenêtre principale affirmait « les outils ne sont pas installés »
/// en proposant de les réinstaller — la même version. Ces tests fixent ce que la
/// fenêtre doit dire selon l'état réel de l'analyse profonde.
/// </summary>
[Collection("Langue")]
public class ConseilWinDbgTests
{
    private static T EnFrancais<T>(Func<T> f)
    {
        var initial = Lang.Current;
        try { Lang.Apply(AppLanguage.French); return f(); }
        finally { Lang.Apply(initial); }
    }

    private static DiagnosticReport Rapport(EtatAnalyseProfonde etat, params DumpKind[] dumps) => new()
    {
        AnalyseProfonde = etat,
        Dumps = dumps.Select((k, i) => new DumpFileInfo { Path = $@"C:\Windows\Minidump\test{i}.dmp", Kind = k }).ToList(),
    };

    [Fact]
    public void WinDbg_absent_propose_la_boite_a_outils()
    {
        var conseil = EnFrancais(() => ConseilWinDbg.ApresAnalyse(
            Rapport(EtatAnalyseProfonde.Absente, DumpKind.KernelMinidump, DumpKind.KernelMinidump)));

        Assert.NotNull(conseil);
        Assert.True(conseil.ProposerBoiteAOutils);
        Assert.Contains("2 fichier(s)", conseil.Message);
        Assert.Contains("ne sont pas installés", conseil.Message);
    }

    [Fact]
    public void WinDbg_present_mais_refuse_ne_dit_jamais_qu_il_manque()
    {
        var conseil = EnFrancais(() => ConseilWinDbg.ApresAnalyse(
            Rapport(EtatAnalyseProfonde.Inaccessible, DumpKind.KernelMinidump)));

        Assert.NotNull(conseil);
        // Le cas du 24/09/2026 : pas de proposition de réinstaller la même version.
        Assert.False(conseil.ProposerBoiteAOutils);
        Assert.Contains("sont bien installés", conseil.Message);
        Assert.DoesNotContain("ne sont pas installés", conseil.Message);
        Assert.Contains("limitations", conseil.Message);
    }

    [Theory]
    [InlineData(EtatAnalyseProfonde.Faite)]
    [InlineData(EtatAnalyseProfonde.NonDemandee)]
    [InlineData(EtatAnalyseProfonde.SansObjet)]
    public void Rien_a_dire_quand_l_outil_n_est_pas_en_cause(EtatAnalyseProfonde etat)
    {
        // « Faite » couvre aussi le cas où CDB s'est lancé mais a dépassé le délai sur
        // chaque dump : l'outil est là et se lance, proposer de l'installer serait faux.
        Assert.Null(ConseilWinDbg.ApresAnalyse(Rapport(etat, DumpKind.KernelMinidump)));
    }

    [Fact]
    public void Sans_dump_noyau_rien_n_est_propose()
    {
        // Les « Live Kernel Reports » et les dumps d'application ne passent pas par
        // l'analyse profonde : ils ne justifient pas d'installer WinDbg.
        Assert.Null(ConseilWinDbg.ApresAnalyse(
            Rapport(EtatAnalyseProfonde.Absente, DumpKind.LiveKernelReport, DumpKind.UserModeMinidump)));
    }
}
