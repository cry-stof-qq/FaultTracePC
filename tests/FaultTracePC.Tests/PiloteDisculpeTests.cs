using FaultTracePC.Core;
using FaultTracePC.Core.Report;
using Xunit;

namespace FaultTracePC.Tests;

/// <summary>
/// Point 55 de la feuille de route. Constaté le 14/09/2026 sur POSTE-TEMOIN, trois
/// rapports : nvlddmkm.sys apparaît en 32.0.15.8216 dans le premier et en
/// 32.0.15.8278 dans le second, cinq heures plus tard — le pilote avait été mis à
/// jour entre les deux. Quatre écrans bleus de MÊME signature ont suivi.
///
/// Le logiciel disait « le problème PERSISTE, la réparation n'a pas suffi ». Il ne
/// disait pas la seule chose qui clôt le dossier : le pilote accusé a été remplacé,
/// les plantages ont continué, ce n'est donc pas le pilote.
/// </summary>
[Collection("Langue")]
public class PiloteDisculpeTests
{
    private const string Pilote = "nvlddmkm.sys";

    private static ScanHistory.ScanSummary Precedent(string versionPilote) => new()
    {
        GeneratedAt = new DateTime(2026, 8, 24, 7, 38, 0),
        ScanPeriodDays = 30,
        Bsods = [new ScanHistory.BsodBrief { Time = new DateTime(2026, 7, 9, 16, 10, 0), Code = 0x116, Driver = Pilote }],
        // La clé est le nom de fichier, et la valeur porte « version|date » :
        // c'est le format exact que Summarize écrit, sans quoi la comparaison
        // verrait un changement là où il n'y en a pas.
        DriverVersions = { ["nvlddmkm.sys"] = $"{versionPilote}|2026-06-25" },
    };

    private static DiagnosticReport Actuel(string versionPilote)
    {
        var r = new DiagnosticReport
        {
            GeneratedAt = new DateTime(2026, 9, 14, 10, 57, 0),
            ScanPeriodDays = 30,
            System = new SystemSnapshot { MachineName = "PC-TEST" },
        };
        r.System.Drivers.Add(new DriverInfo
        {
            Name = "nvlddmkm",
            DisplayName = "NVIDIA Windows Kernel Mode Driver",
            Path = @"C:\WINDOWS\System32\drivers\nvlddmkm.sys",
            CompanyName = "NVIDIA Corporation",
            FileVersion = versionPilote,
            FileDate = new DateTime(2026, 6, 25),
            State = "Running",
        });
        // Un nouveau plantage, même code et même pilote que la fois précédente.
        r.Bsods.Add(new BsodIncident
        {
            TimeLocal = new DateTime(2026, 9, 4, 12, 47, 0),
            BugCheckCode = 0x116,
            BugCheckName = "VIDEO_TDR_FAILURE",
            SuspectDriver = Pilote,
        });
        return r;
    }

    private static ScanComparison Comparer(DiagnosticReport r, ScanHistory.ScanSummary prev)
    {
        var initial = Lang.Current;
        try { Lang.Apply(AppLanguage.French); return ScanHistory.Compare(r, prev); }
        finally { Lang.Apply(initial); }
    }

    [Fact]
    public void Un_pilote_remplace_dont_les_plantages_continuent_est_disculpe()
    {
        var c = Comparer(Actuel("32.0.15.8278"), Precedent("32.0.15.8216"));

        Assert.True(c.SameSignatureRecurred);
        Assert.Equal(Pilote, c.SuspectDriverReplacedInVain);
        // La phrase doit dire l'essentiel, et rediriger.
        Assert.Contains("REMPLACÉ", c.Assessment);
        Assert.Contains("ce n'est donc pas le pilote", c.Assessment, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("matériel", c.Assessment);
    }

    [Fact]
    public void Un_pilote_inchange_reste_accuse()
    {
        // Même version des deux côtés : rien n'a été tenté, la conclusion ne change pas.
        var c = Comparer(Actuel("32.0.15.8216"), Precedent("32.0.15.8216"));

        Assert.True(c.SameSignatureRecurred);
        Assert.Null(c.SuspectDriverReplacedInVain);
        Assert.DoesNotContain("REMPLACÉ", c.Assessment);
    }

    [Fact]
    public void Sans_recurrence_de_signature_aucun_rapprochement_n_est_fait()
    {
        var r = Actuel("32.0.15.8278");
        // Le nouveau plantage n'a plus rien à voir avec le précédent.
        r.Bsods[0].BugCheckCode = 0x1A;
        r.Bsods[0].SuspectDriver = "ntfs.sys";

        var c = Comparer(r, Precedent("32.0.15.8216"));

        Assert.False(c.SameSignatureRecurred);
        Assert.Null(c.SuspectDriverReplacedInVain);
    }
}
