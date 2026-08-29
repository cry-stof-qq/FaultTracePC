using System.Text;
using FaultTracePC.Core;
using Xunit;

namespace FaultTracePC.Tests;

/// <summary>
/// Coffre du secret maître de parc (DPAPI).
///
/// Ces tests passent par les entrées internes de chiffrement : écrire dans
/// %LOCALAPPDATA% écraserait le secret réel de la machine qui exécute les tests.
/// </summary>
public class SecretParcTests
{
    private const string Secret = "9F2C7A1B4E6D8035AC91BE47D25F0836";   // 32 caractères, comme en produit GenerateMasterSecret

    [Fact]
    public void Le_secret_se_retrouve_apres_un_aller_retour()
    {
        var chiffre = ParkSecret.Proteger(Encoding.UTF8.GetBytes(Secret));

        Assert.Equal(Secret, Encoding.UTF8.GetString(ParkSecret.Deproteger(chiffre)));
    }

    [Fact]
    public void Le_contenu_chiffre_ne_laisse_pas_voir_le_secret()
    {
        var chiffre = ParkSecret.Proteger(Encoding.UTF8.GetBytes(Secret));

        // Recherche de la SUITE D'OCTETS du secret dans le contenu chiffré — ce que
        // ferait quelqu'un qui ouvre le fichier dans un éditeur hexadécimal. Chercher
        // dans une conversion en texte ne prouverait rien : des octets aléatoires
        // n'ont pas de représentation textuelle fidèle.
        Assert.False(ContientLaSuite(chiffre, Encoding.UTF8.GetBytes(Secret)));
        Assert.True(chiffre.Length > Secret.Length);
    }

    private static bool ContientLaSuite(byte[] meule, byte[] aiguille)
    {
        for (var i = 0; i + aiguille.Length <= meule.Length; i++)
        {
            var trouve = true;
            for (var j = 0; j < aiguille.Length && trouve; j++)
                trouve = meule[i + j] == aiguille[j];
            if (trouve) return true;
        }
        return false;
    }

    [Fact]
    public void Un_contenu_altere_ne_se_dechiffre_pas()
    {
        var chiffre = ParkSecret.Proteger(Encoding.UTF8.GetBytes(Secret));
        chiffre[^3] ^= 0xFF;

        // DPAPI refuse un contenu modifié au lieu de rendre des octets faux :
        // un secret silencieusement corrompu produirait des jetons faux sur tout
        // le parc, et une panne impossible à comprendre.
        Assert.ThrowsAny<Exception>(() => ParkSecret.Deproteger(chiffre));
    }

    [Fact]
    public void Un_secret_trop_court_est_refuse_avant_toute_ecriture()
    {
        Assert.False(ParkSecret.Save("trop court", out var erreur));
        Assert.False(string.IsNullOrWhiteSpace(erreur));
    }
}
