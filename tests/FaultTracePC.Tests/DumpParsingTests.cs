using System.Buffers.Binary;
using System.Text;
using FaultTracePC.Core;
using FaultTracePC.Core.Analysis;
using FaultTracePC.Core.Collectors;
using Xunit;

namespace FaultTracePC.Tests;

/// <summary>
/// Lecture native de l'en-tête des dumps noyau. On fabrique de faux dumps
/// (en-têtes conformes) pour vérifier que le code STOP et ses paramètres sont
/// extraits aux bons décalages — c'est ce qui permet à FaultTracePC de lire un
/// crash sans WinDbg.
/// </summary>
public class DumpParsingTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "ftpc_tests_" + Guid.NewGuid().ToString("N"));

    public DumpParsingTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
        GC.SuppressFinalize(this);
    }

    /// <summary>Fabrique un dump noyau 64 bits factice (signature PAGEDU64).</summary>
    private string CreateDump64(uint bugCheckCode, ulong[] parameters, string name = "test.dmp")
    {
        var buffer = new byte[0x2000];
        Encoding.ASCII.GetBytes("PAGE").CopyTo(buffer, 0);
        Encoding.ASCII.GetBytes("DU64").CopyTo(buffer, 4);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(0x38), bugCheckCode);
        for (int i = 0; i < 4; i++)
            BinaryPrimitives.WriteUInt64LittleEndian(buffer.AsSpan(0x40 + i * 8), parameters[i]);

        var path = Path.Combine(_dir, name);
        File.WriteAllBytes(path, buffer);
        return path;
    }

    /// <summary>Collect() balaie C:\Windows ; on teste la logique de parsing sur un fichier maîtrisé.</summary>
    private static DumpFileInfo Parse(string path) =>
        DumpCollector.InspectFile(path, DumpKind.KernelMinidump);

    [Fact]
    public void Lit_le_code_stop_et_les_parametres_dun_dump_64_bits()
    {
        var path = CreateDump64(0x0000007E, [0xFFFFFFFFC0000005, 0xFFFFF80001234567, 0x1, 0x2]);
        var info = Parse(path);

        Assert.True(info.Is64Bit);
        Assert.Equal(0x7Eu, info.BugCheckCode);
        Assert.NotNull(info.BugCheckParameters);
        Assert.Equal(0xFFFFFFFFC0000005UL, info.BugCheckParameters![0]);
        Assert.Equal(0xFFFFF80001234567UL, info.BugCheckParameters[1]);
        Assert.Null(info.ParseError);
    }

    [Fact]
    public void Reconnait_un_dump_applicatif_MDMP()
    {
        var buffer = new byte[0x1000];
        Encoding.ASCII.GetBytes("MDMP").CopyTo(buffer, 0);
        var path = Path.Combine(_dir, "app.dmp");
        File.WriteAllBytes(path, buffer);

        var info = Parse(path);
        Assert.Equal(DumpKind.UserModeMinidump, info.Kind);
        Assert.Null(info.BugCheckCode);   // un dump applicatif n'a pas de code STOP
    }

    [Fact]
    public void Signale_un_fichier_non_reconnu_sans_lever_dexception()
    {
        var path = Path.Combine(_dir, "bidon.dmp");
        File.WriteAllBytes(path, Encoding.ASCII.GetBytes(new string('X', 4096)));

        var info = Parse(path);
        Assert.Equal(DumpKind.Unknown, info.Kind);
        Assert.NotNull(info.ParseError);
    }

    [Fact]
    public void Fichier_trop_court_est_signale_proprement()
    {
        var path = Path.Combine(_dir, "court.dmp");
        File.WriteAllBytes(path, [0x50, 0x41, 0x47, 0x45]);   // « PAGE » seul

        var info = Parse(path);
        Assert.NotNull(info.ParseError);
        Assert.Null(info.BugCheckCode);
    }

    [Theory]
    [InlineData(0x0Au, "IRQL_NOT_LESS_OR_EQUAL")]
    [InlineData(0x50u, "PAGE_FAULT_IN_NONPAGED_AREA")]
    [InlineData(0x124u, "WHEA_UNCORRECTABLE_ERROR")]
    [InlineData(0x133u, "DPC_WATCHDOG_VIOLATION")]
    public void Le_catalogue_nomme_les_codes_stop_courants(uint code, string expected) =>
        Assert.Equal(expected, BugCheckCatalog.NameOf(code));

    [Fact]
    public void Un_code_inconnu_reste_lisible()
    {
        var name = BugCheckCatalog.NameOf(0xDEADBEEF);
        Assert.Contains("DEADBEEF", name, StringComparison.OrdinalIgnoreCase);
    }
}
