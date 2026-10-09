using FaultTracePC.Core;
using Xunit;

namespace FaultTracePC.Tests;

/// <summary>
/// POINT 81 — lecture de l'état antivirus.
///
/// La lecture WMI elle-même ne se teste pas ici (elle dépend de la machine). Ce qui
/// se teste, c'est le décodage de <c>productState</c>, avec les deux valeurs
/// RÉELLEMENT mesurées sur la machine du 24/09/2026 : ce décodage n'est pas
/// documenté par Microsoft, il ne vaut que parce qu'il a concordé avec Defender.
/// </summary>
public class ProtectionTests
{
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
}
