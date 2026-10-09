using FaultTracePC.Core;
using FaultTracePC.Core.Analysis;
using Xunit;

namespace FaultTracePC.Tests;

/// <summary>
/// POINT 81, LOT 2b — les conclusions sur la protection antivirus.
///
/// Les deux premiers cas sont les configurations RÉELLEMENT mesurées : la machine
/// du 24/09/2026 (Defender en retrait, Avast actif — aucun problème, alors qu'on
/// avait cru y voir deux antivirus) et le poste de l'auteur le 09/10/2026
/// (Defender actif, Apex One mal désinstallé — une inscription orpheline, pas un
/// second antivirus). Les autres fixent les seuils.
/// </summary>
[Collection("Langue")]
public class ConclusionsProtectionTests
{
    private const string CheminApexOne = @"C:\Program Files (x86)\Trend Micro\Security Agent\Pccntmon.exe";

    private static AntivirusInscrit Defender(uint etat) =>
        new() { Nom = "Windows Defender", CheminProduit = "windowsdefender://", EtatBrut = etat };

    // Chemin NON mesuré : seul compte ProgrammePresent.
    private static AntivirusInscrit Tiers(string nom, bool actif, bool? present) =>
        new() { Nom = nom, CheminProduit = @"C:\Programme\antivirus.exe", EtatBrut = actif ? 266240u : 262144u, ProgrammePresent = present };

    private static List<Finding> Conclure(EtatProtection p)
    {
        var initial = Lang.Current;
        try
        {
            Lang.Apply(AppLanguage.French);
            var r = new DiagnosticReport { System = new SystemSnapshot { Protection = p } };
            RulesEngine.AnalyzeProtection(r);
            return r.Findings;
        }
        finally { Lang.Apply(initial); }
    }

    private static EtatProtection Lu(bool defenderTempsReel, string mode, params AntivirusInscrit[] antivirus)
    {
        var p = new EtatProtection { CentreSecuriteLu = true, DefenderLu = true, DefenderTempsReel = defenderTempsReel, DefenderMode = mode };
        p.Antivirus.AddRange(antivirus);
        return p;
    }

    [Fact]
    public void Machine_du_24_09_rien_a_signaler()
    {
        var f = Conclure(Lu(false, "SxS Passive Mode", Defender(393472), Tiers("Avast Antivirus", actif: true, present: true)));
        Assert.Empty(f);
    }

    [Fact]
    public void Poste_du_09_10_une_inscription_orpheline_et_rien_d_autre()
    {
        var apex = new AntivirusInscrit { Nom = "Trend Micro Apex One Antivirus", CheminProduit = CheminApexOne, EtatBrut = 266240, ProgrammePresent = false };
        var f = Conclure(Lu(true, "Normal", Defender(397568), apex));

        var seule = Assert.Single(f);
        Assert.Equal("protection.orpheline", seule.Code);
        Assert.Equal(Severity.Info, seule.Severity);
        Assert.Contains("Trend Micro Apex One Antivirus", seule.Title);
        // Constaté le 09/10/2026 : en catégorie « Logiciel », le rapport proposait
        // sfc /scannow et DISM, qui ne retirent pas un antivirus.
        Assert.Equal(FaultCategory.None, seule.Category);
    }

    [Fact]
    public void Aucune_conclusion_de_protection_ne_promet_un_outil_de_reparation_systeme()
    {
        var f = Conclure(Lu(false, "Normal", Defender(393472), Tiers("Ancien antivirus", actif: true, present: false)));
        Assert.NotEmpty(f);
        Assert.All(f, x => Assert.Equal(FaultCategory.None, x.Category));
    }

    [Fact]
    public void Defender_actif_et_un_antivirus_present_donnent_deux_protections()
    {
        var f = Conclure(Lu(true, "Normal", Defender(397568), Tiers("Avast Antivirus", actif: true, present: true)));
        var deux = Assert.Single(f);
        Assert.Equal("protection.deux", deux.Code);
        Assert.Equal(Severity.Warning, deux.Severity);
        Assert.Contains("Windows Defender et Avast Antivirus", deux.Details);
    }

    [Fact]
    public void Sans_rien_d_actif_aucune_protection()
    {
        var f = Conclure(Lu(false, "Normal", Defender(393472)));
        Assert.Equal("protection.aucune", Assert.Single(f).Code);
    }

    [Fact]
    public void Une_inscription_orpheline_ne_compte_pas_comme_protection()
    {
        var f = Conclure(Lu(false, "Passive Mode", Defender(393472), Tiers("Ancien antivirus", actif: true, present: false)));
        Assert.Contains(f, x => x.Code == "protection.aucune");
        Assert.Contains(f, x => x.Code == "protection.orpheline");
    }

    [Fact]
    public void Dans_le_doute_on_ne_crie_dans_aucun_sens()
    {
        // Programme non vérifiable (null) : ni « deux antivirus », ni « aucune protection ».
        Assert.Empty(Conclure(Lu(true, "Normal", Defender(397568), Tiers("Antivirus inconnu", actif: true, present: null))));
        Assert.Empty(Conclure(Lu(false, "Passive Mode", Defender(393472), Tiers("Antivirus inconnu", actif: true, present: null))));
    }

    [Fact]
    public void Rien_pu_lire_ne_devient_jamais_rien_trouve()
    {
        // Defender non lu : aucune conclusion d'ensemble.
        var p = new EtatProtection { CentreSecuriteLu = true };
        Assert.Empty(Conclure(p));
        // Centre de sécurité non lu : aucune conclusion du tout.
        Assert.Empty(Conclure(new EtatProtection { DefenderLu = true, DefenderTempsReel = false }));
    }
}
