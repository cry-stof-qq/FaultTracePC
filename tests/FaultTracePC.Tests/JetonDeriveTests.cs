using FaultTracePC.Core;
using Xunit;

namespace FaultTracePC.Tests;

/// <summary>
/// Le jeton dérivé du secret maître.
///
/// Il remplace une liste de jetons — un par poste, conservée dans le dossier
/// Documents de la console — par un calcul. Ce qui est vérifié ici n'est pas la
/// cryptographie, qui est celle de .NET, mais les erreurs d'usage qui rendraient
/// le mécanisme inutilisable ou dangereux : un nom de machine dont la casse
/// change, un secret trop faible, une valeur qui ne serait pas reproductible.
/// </summary>
public class JetonDeriveTests
{
    private const string Secret = "8F3C1A9E7B5D2064F8A1C3E5079BD246F8A1C3E5079BD2468F3C1A9E7B5D2064";

    [Fact]
    public void Le_meme_secret_et_la_meme_machine_donnent_toujours_le_meme_jeton()
    {
        // C'est toute la raison d'être du mécanisme : une console reconstruite
        // depuis zéro doit retrouver l'accès sans aucune sauvegarde.
        Assert.Equal(RemoteConfig.DeriveToken(Secret, "POSTE-01"),
                     RemoteConfig.DeriveToken(Secret, "POSTE-01"));
    }

    [Theory]
    [InlineData("poste-01")]
    [InlineData("Poste-01")]
    [InlineData("  POSTE-01  ")]
    public void La_casse_et_les_espaces_du_nom_de_machine_ne_changent_rien(string variante)
    {
        // Windows rend le nom tantôt en majuscules, tantôt tel qu'il a été saisi.
        // Sans normalisation, l'interrogation échouerait sans rien expliquer.
        Assert.Equal(RemoteConfig.DeriveToken(Secret, "POSTE-01"),
                     RemoteConfig.DeriveToken(Secret, variante));
    }

    [Fact]
    public void Deux_machines_n_ont_jamais_le_meme_jeton()
    {
        Assert.NotEqual(RemoteConfig.DeriveToken(Secret, "POSTE-01"),
                        RemoteConfig.DeriveToken(Secret, "POSTE-02"));
    }

    [Fact]
    public void Changer_de_secret_change_tous_les_jetons()
    {
        // C'est ce qui permet de révoquer un parc entier : on change le secret.
        var autre = "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF";
        Assert.NotEqual(RemoteConfig.DeriveToken(Secret, "POSTE-01"),
                        RemoteConfig.DeriveToken(autre, "POSTE-01"));
    }

    [Fact]
    public void Le_jeton_a_la_meme_forme_qu_un_jeton_tire_au_sort()
    {
        // 256 bits en hexadécimal : les postes déjà déployés ne voient aucune
        // différence de format.
        var jeton = RemoteConfig.DeriveToken(Secret, "POSTE-01");
        Assert.Equal(64, jeton.Length);
        Assert.Matches("^[0-9A-F]{64}$", jeton);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("trop court")]
    [InlineData("0123456789ABCDEF0123456789ABCDE")]   // 31 caractères, un de moins
    public void Un_secret_trop_faible_est_refuse(string mauvais)
    {
        // Un secret deviné donne accès à TOUT le parc d'un coup. Le refuser
        // franchement vaut mieux que l'accepter en espérant qu'il soit bon.
        Assert.Throws<ArgumentException>(() => RemoteConfig.DeriveToken(mauvais, "POSTE-01"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Un_nom_de_machine_vide_est_refuse(string mauvais)
    {
        Assert.Throws<ArgumentException>(() => RemoteConfig.DeriveToken(Secret, mauvais));
    }

    [Fact]
    public void Le_secret_maitre_genere_est_solide_et_jamais_deux_fois_le_meme()
    {
        var a = RemoteConfig.GenerateMasterSecret();
        var b = RemoteConfig.GenerateMasterSecret();

        Assert.Matches("^[0-9A-F]{64}$", a);
        Assert.NotEqual(a, b);
        Assert.True(a.Length >= RemoteConfig.MasterSecretMinLength);
    }
}
