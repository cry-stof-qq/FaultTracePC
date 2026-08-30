using System.Net;
using FaultTracePC.Core;
using Xunit;

namespace FaultTracePC.Tests;

/// <summary>
/// Règle de pare-feu du mode parc.
///
/// DÉFAUT CONSTATÉ LE 30/08/2026 en écrivant la procédure de déploiement : ni le
/// MSI ni la ligne de commande ne posaient cette règle. Un parc déployé par
/// stratégie de groupe obtenait des postes qui écoutent correctement et que rien
/// ne peut joindre — et l'on ne s'en aperçoit que le jour du déploiement.
///
/// Ce qui se teste ici n'est pas netsh, qui n'est pas à nous, mais la LIGNE
/// D'ARGUMENTS produite — encore un texte écrit pour qu'un autre programme le
/// relise.
/// </summary>
public class RegleParefeuTests
{
    [Fact]
    public void La_regle_ouvre_le_port_demande_en_entree_seulement()
    {
        var args = FirewallRule.ArgumentsAjout(58700);

        Assert.Contains("localport=58700", args);
        Assert.Contains("dir=in", args);
        Assert.Contains("action=allow", args);
        Assert.Contains("protocol=TCP", args);
    }

    [Fact]
    public void Le_nom_de_la_regle_est_entre_guillemets()
    {
        // Il contient une espace : sans guillemets, netsh comprend deux arguments
        // et crée une règle nommée « FaultTracePC » — que la suppression ne
        // retrouverait jamais.
        Assert.Contains("name=\"FaultTracePC Telemetry\"", FirewallRule.ArgumentsAjout(58620));
        Assert.Contains("name=\"FaultTracePC Telemetry\"", FirewallRule.ArgumentsSuppression());
    }

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("10.1.2.3")]
    [InlineData("172.16.0.1")]
    [InlineData("172.31.255.254")]
    [InlineData("192.168.1.10")]
    public void Ce_que_le_pare_feu_laisse_entrer_est_ce_que_le_service_accepte(string adresse)
    {
        // LA cohérence qui compte : deux listes de plages écrites à deux endroits
        // finiraient par diverger, et la divergence ouvrirait un port à des
        // adresses que le service refuse — ou l'inverse, plus vicieux : un poste
        // injoignable sans que rien ne l'explique.
        Assert.True(RemoteConfig.IsPrivateOrLoopback(IPAddress.Parse(adresse)));
        Assert.Contains(RacineDe(adresse), FirewallRule.PlagesPrivees);
    }

    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("172.32.0.1")]
    [InlineData("11.0.0.1")]
    public void Une_adresse_publique_n_est_ni_dans_la_regle_ni_acceptee(string adresse)
    {
        Assert.False(RemoteConfig.IsPrivateOrLoopback(IPAddress.Parse(adresse)));
    }

    /// <summary>« 172.31.255.254 » → « 172.16 » : la forme sous laquelle la plage
    /// figure dans la règle netsh.</summary>
    private static string RacineDe(string adresse)
    {
        var o = adresse.Split('.');
        return o[0] switch
        {
            "127" => "127.0.0.1",
            "10" => "10.0.0.0/8",
            "172" => "172.16.0.0/12",
            _ => "192.168.0.0/16",
        };
    }
}
