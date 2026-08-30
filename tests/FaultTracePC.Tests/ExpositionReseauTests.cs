using FaultTracePC.Core;
using Xunit;

namespace FaultTracePC.Tests;

/// <summary>
/// Ce que le service doit exposer, et quand il doit repartir sur de nouvelles bases.
///
/// DÉFAUT CONSTATÉ LE 30/08/2026 en relisant la séquence d'un déploiement par
/// stratégie de groupe : le service lisait sa configuration UNE fois au démarrage
/// et se terminait si le mode n'était pas « Client ». Or le MSI démarre le service
/// AVANT que le script d'ouverture n'écrive remote.json. Le poste se retrouvait
/// configuré, la commande rendait 0, et rien ne répondait jusqu'au redémarrage
/// suivant — c'est-à-dire : rien le jour du déploiement, tout le lendemain.
///
/// La décision « faut-il exposer ? » et la décision « la configuration a-t-elle
/// changé ? » vivent donc dans Core, où elles se testent — le service, lui, n'est
/// pas compilé par « dotnet test ».
/// </summary>
public class ExpositionReseauTests
{
    private static RemoteConfig Cfg(string mode, string token, int port = 58620) =>
        new() { Mode = mode, Token = token, Port = port };

    [Fact]
    public void Le_fichier_ne_porte_que_les_trois_champs_qui_font_foi()
    {
        // DÉFAUT CONSTATÉ LE 30/08/2026 dans le remote.json d'une machine réelle :
        // « ModeClientActif », propriété CALCULÉE, était sérialisée dans le
        // fichier. Inoffensive à la relecture — pas de setter — mais un champ
        // redondant capable de contredire les autres n'a rien à faire dans un
        // fichier qu'un administrateur peut ouvrir et modifier.
        var json = System.Text.Json.JsonSerializer.Serialize(
            new RemoteConfig { Mode = "Client", Port = 58620, Token = "ABCDEF" });

        Assert.Contains("\"Mode\"", json);
        Assert.Contains("\"Port\"", json);
        Assert.Contains("\"Token\"", json);
        Assert.DoesNotContain("ModeClientActif", json);
        Assert.DoesNotContain("ConfigEstProtegee", json);
    }

    [Fact]
    public void Le_mode_client_avec_un_jeton_expose()
    {
        Assert.True(Cfg("Client", "ABCDEF").ModeClientActif);
    }

    [Theory]
    [InlineData("client")]
    [InlineData("CLIENT")]
    public void La_casse_du_mode_ne_change_rien(string mode)
    {
        // Le fichier est écrit par trois programmes et parfois à la main.
        Assert.True(Cfg(mode, "ABCDEF").ModeClientActif);
    }

    [Fact]
    public void Le_mode_local_n_expose_rien()
    {
        Assert.False(Cfg("Local", "ABCDEF").ModeClientActif);
    }

    [Fact]
    public void Le_mode_client_sans_jeton_n_expose_rien()
    {
        // C'EST le cas du poste installé par MSI mais pas encore configuré.
        // Ouvrir un port sans clé serait le pire des deux mondes.
        Assert.False(Cfg("Client", "").ModeClientActif);
    }

    [Fact]
    public void Une_configuration_reecrite_a_l_identique_ne_coupe_rien()
    {
        // Sinon la moindre réécriture du fichier — un script GPO qui repasse —
        // couperait les connexions en cours sans raison.
        Assert.True(Cfg("Client", "ABCDEF").MemeExpositionQue(Cfg("Client", "ABCDEF")));
        Assert.True(Cfg("Client", "ABCDEF").MemeExpositionQue(Cfg("client", "ABCDEF")));
    }

    [Theory]
    [InlineData("Client", "AUTREJETON", 58620)]
    [InlineData("Client", "ABCDEF", 58700)]
    [InlineData("Local", "ABCDEF", 58620)]
    [InlineData("Client", "", 58620)]
    public void Tout_changement_de_sens_fait_repartir_le_service(string mode, string token, int port)
    {
        Assert.False(Cfg("Client", "ABCDEF").MemeExpositionQue(Cfg(mode, token, port)));
    }

    [Fact]
    public void Une_configuration_absente_compte_comme_un_changement()
    {
        Assert.False(Cfg("Client", "ABCDEF").MemeExpositionQue(null));
    }
}
