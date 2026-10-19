using FaultTracePC.Core;
using FaultTracePC.Core.Analysis;
using Xunit;

namespace FaultTracePC.Tests;

/// <summary>
/// Constaté le 19/10/2026 sur un poste du parc : un seul plantage (10/03/2026 14:52)
/// figurait DEUX fois dans le rapport — le minidump portait 0x1000007E, MEMORY.DMP
/// 0x7E, avec les mêmes quatre paramètres. Deux cartes critiques, « 3 plantages en
/// 2 séries » au lieu de 2. 0x1000007E est la variante « _M » de 0x7E.
/// </summary>
[Collection("Langue")]
public class VarianteMTests
{
    private static readonly DateTime Quand = new(2026, 3, 10, 14, 52, 0);
    private static readonly ulong[] Parametres = { 0xFFFFFFFFC0000005, 0xFFFFF80685D4A8A4, 0xFFFFA08C3B5D5398, 0xFFFFA08C3B5D4BA0 };

    private static DiagnosticReport Rapport()
    {
        var r = new DiagnosticReport
        {
            GeneratedAt = new DateTime(2026, 10, 19, 9, 15, 0),
            ScanPeriodDays = 30,
            System = new SystemSnapshot { MachineName = "POSTE-TEST" },
        };
        r.Dumps.Add(new DumpFileInfo
        {
            Path = @"C:\WINDOWS\Minidump\031026-0001-01.dmp",
            Kind = DumpKind.KernelMinidump,
            BugCheckCode = 0x1000007E,
            BugCheckParameters = Parametres,
            CrashTimeFromHeader = Quand,
            LastWriteTime = Quand,
        });
        r.Dumps.Add(new DumpFileInfo
        {
            Path = @"C:\WINDOWS\MEMORY.DMP",
            Kind = DumpKind.FullMemoryDump,
            BugCheckCode = 0x7E,
            BugCheckParameters = Parametres,
            CrashTimeFromHeader = Quand,
            LastWriteTime = Quand,
        });
        return r;
    }

    private static void Analyser(DiagnosticReport r)
    {
        var initial = Lang.Current;
        try { Lang.Apply(AppLanguage.French); new RulesEngine().Analyze(r); }
        finally { Lang.Apply(initial); }
    }

    [Theory]
    [InlineData(0x1000007Eu, 0x7Eu)]
    [InlineData(0x1000007Fu, 0x7Fu)]
    [InlineData(0x1000008Eu, 0x8Eu)]
    [InlineData(0x100000EAu, 0xEAu)]
    public void Une_variante_M_revient_a_son_code_de_base(uint variante, uint base_)
        => Assert.Equal(base_, BugCheckCatalog.Canonique(variante));

    [Theory]
    [InlineData(0x7Eu)]
    [InlineData(0x124u)]
    [InlineData(0xC000021Au)]
    [InlineData(0xDEADDEADu)]
    [InlineData(0x10000000u)]
    public void Les_autres_codes_ne_changent_pas(uint code)
        => Assert.Equal(code, BugCheckCatalog.Canonique(code));

    [Fact]
    public void Minidump_et_MEMORY_DMP_d_un_meme_plantage_font_un_seul_incident()
    {
        var r = Rapport();
        Analyser(r);

        var incident = Assert.Single(r.Bsods);
        Assert.Equal((uint?)0x7Eu, incident.BugCheckCode);
        Assert.Contains("Minidump", incident.Sources);
        Assert.Contains("MEMORY.DMP", incident.Sources);
    }

    [Fact]
    public void Une_seule_carte_d_ecran_bleu_et_elle_porte_le_nom_du_code()
    {
        var r = Rapport();
        Analyser(r);

        var cartes = r.Findings.Where(f => f.Code.StartsWith(RulesEngine.PrefixeCodeArret)).ToList();
        var carte = Assert.Single(cartes);
        Assert.Contains("SYSTEM_THREAD_EXCEPTION_NOT_HANDLED", carte.Title);
    }
}
