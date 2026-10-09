using FaultTracePC.Core;
using FaultTracePC.Core.Collectors;
using FaultTracePC.Core.Report;
using Xunit;

namespace FaultTracePC.Tests;

/// <summary>
/// POINT 81 — lecture de l'état antivirus.
///
/// La lecture WMI elle-même ne se teste pas ici (elle dépend de la machine). Ce qui
/// se teste, c'est le décodage de <c>productState</c>, avec les deux valeurs
/// RÉELLEMENT mesurées sur la machine du 24/09/2026 : ce décodage n'est pas
/// documenté par Microsoft, il ne vaut que parce qu'il a concordé avec Defender.
///
/// Lot 2 (09/10/2026) : la carte « Protection antivirus » du rapport, testée avec
/// les deux configurations réellement mesurées — la machine du 24/09/2026 et le
/// poste de l'auteur, où un Apex One mal désinstallé restait déclaré actif.
/// </summary>
[Collection("Langue")]
public class ProtectionTests
{
    private static T EnFrancais<T>(Func<T> f)
    {
        var initial = Lang.Current;
        try { Lang.Apply(AppLanguage.French); return f(); }
        finally { Lang.Apply(initial); }
    }

    private const string CheminApexOne = @"C:\Program Files (x86)\Trend Micro\Security Agent\Pccntmon.exe";

    [Fact]
    public void Defender_en_retrait_le_24_09_est_decode_inactif()
    {
        // 393472 = 0x060100, alors que Defender disait « SxS Passive Mode ».
        var defender = new AntivirusInscrit { Nom = "Windows Defender", EtatBrut = 393472 };
        Assert.False(defender.Actif);
    }

    [Fact]
    public void Avast_le_24_09_est_decode_actif_et_a_jour()
    {
        // 266240 = 0x041000.
        var avast = new AntivirusInscrit { Nom = "Avast Antivirus", EtatBrut = 266240 };
        Assert.True(avast.Actif);
        Assert.True(avast.SignaturesAJour);
    }

    [Fact]
    public void Des_signatures_perimees_sont_reperees()
    {
        // Même valeur qu'Avast, avec le bit « périmé » (0x10) en plus.
        var perime = new AntivirusInscrit { EtatBrut = 0x041010 };
        Assert.True(perime.Actif);
        Assert.False(perime.SignaturesAJour);
    }

    [Fact]
    public void Rien_n_est_presume_lu_tant_que_la_collecte_n_a_pas_reussi()
    {
        // Un état neuf ne doit pas pouvoir passer pour « aucun antivirus » :
        // c'est la différence entre « rien trouvé » et « rien pu lire ».
        var etat = new EtatProtection();
        Assert.False(etat.CentreSecuriteLu);
        Assert.False(etat.DefenderLu);
        Assert.Null(etat.DefenderTempsReel);
        Assert.Empty(etat.Antivirus);
    }

    [Fact]
    public void Defender_actif_le_09_10_est_decode_actif()
    {
        // 397568 = 0x061100, alors que Defender disait « Normal », temps réel actif.
        Assert.True(new AntivirusInscrit { EtatBrut = 397568 }.Actif);
    }

    [Fact]
    public void Defender_se_reconnait_a_son_chemin_reserve()
    {
        Assert.True(new AntivirusInscrit { CheminProduit = "windowsdefender://" }.EstDefender);
        Assert.False(new AntivirusInscrit { CheminProduit = CheminApexOne }.EstDefender);
    }

    [Fact]
    public void La_presence_du_programme_n_est_verifiee_que_pour_un_vrai_fichier()
    {
        Assert.Null(ProtectionCollector.ProgrammePresent("windowsdefender://"));
        Assert.Null(ProtectionCollector.ProgrammePresent(""));
        Assert.False(ProtectionCollector.ProgrammePresent(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "absent.exe")));

        var fichier = Path.GetTempFileName();
        try { Assert.True(ProtectionCollector.ProgrammePresent($"\"{fichier}\"")); }
        finally { File.Delete(fichier); }
    }

    [Fact]
    public void Machine_du_24_09_Defender_en_retrait_et_Avast_actif()
    {
        var html = EnFrancais(() => HtmlReportGenerator.LignesProtection(new EtatProtection
        {
            DefenderLu = true, DefenderMode = "SxS Passive Mode", DefenderTempsReel = false,
            CentreSecuriteLu = true,
            Antivirus =
            {
                new AntivirusInscrit { Nom = "Windows Defender", CheminProduit = "windowsdefender://", EtatBrut = 393472 },
                // Chemin d'Avast NON mesuré le 24/09/2026 : seul compte ici que le
                // programme existe (ProgrammePresent = true).
                new AntivirusInscrit { Nom = "Avast Antivirus", CheminProduit = @"C:\Programme\avast.exe", EtatBrut = 266240, ProgrammePresent = true },
            },
        }));

        Assert.Contains("pas de protection en temps réel", html);
        Assert.Contains("en retrait", html);
        Assert.Contains("Avast Antivirus : actif, signatures à jour", html);
        Assert.DoesNotContain("orpheline", html);
    }

    [Fact]
    public void Poste_du_09_10_Apex_One_mal_desinstalle_n_est_pas_une_protection()
    {
        var html = EnFrancais(() => HtmlReportGenerator.LignesProtection(new EtatProtection
        {
            DefenderLu = true, DefenderMode = "Normal", DefenderTempsReel = true,
            CentreSecuriteLu = true,
            Antivirus =
            {
                new AntivirusInscrit { Nom = "Windows Defender", CheminProduit = "windowsdefender://", EtatBrut = 397568 },
                new AntivirusInscrit { Nom = "Trend Micro Apex One Antivirus", CheminProduit = CheminApexOne, EtatBrut = 266240, ProgrammePresent = false },
            },
        }));

        Assert.Contains("protection en temps réel <strong>active</strong>", html);
        Assert.Contains("inscription orpheline", html);
        Assert.Contains("ne protège pas la machine", html);
        // Defender n'est décrit qu'une fois, d'après sa propre lecture.
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(html, "Windows Defender"));
    }

    [Fact]
    public void Rien_lu_rien_affirme()
    {
        var html = EnFrancais(() => HtmlReportGenerator.LignesProtection(new EtatProtection()));
        Assert.Contains("état non lu", html);
        Assert.Contains("inconnus", html);
        Assert.DoesNotContain("Aucun autre antivirus", html);
    }
}
