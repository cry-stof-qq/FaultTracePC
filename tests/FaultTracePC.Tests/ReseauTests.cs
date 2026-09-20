using FaultTracePC.Core;
using FaultTracePC.Core.Collectors;
using Xunit;

namespace FaultTracePC.Tests;

/// <summary>
/// Point 53 de la feuille de route. Le réseau était le seul domaine dont le rapport ne
/// disait rien : le 14/09/2026, sur POSTE-ELEVE-01, la panne était « plus aucun réseau
/// Wi-Fi visible » et le rapport a parlé de la batterie et des erreurs disque.
/// </summary>
public class ReseauTests
{
    [Theory]
    [InlineData("BCFCE712AB34", "BC:FC:E7:xx:xx:xx")]
    [InlineData("bc-fc-e7-12-ab-34", "BC:FC:E7:xx:xx:xx")]
    [InlineData("", "")]
    [InlineData("ABC", "")]
    public void Une_adresse_mac_ne_sort_jamais_entiere_du_rapport(string brute, string attendu)
    {
        // Une MAC complète identifie une machine précise. Le constructeur suffit à
        // reconnaître la carte, et c'est tout ce dont le lecteur a besoin.
        Assert.Equal(attendu, NetworkCollector.MasquerMac(brute));
    }

    [Fact]
    public void Zero_profil_et_profil_illisible_ne_veulent_pas_dire_la_meme_chose()
    {
        // Zéro EST la conclusion : la liste des réseaux restera vide.
        var connu = new NetworkInfo { WifiProfileCount = 0 };
        // -1 veut dire « je n'ai pas pu regarder » : rien ne peut en être conclu.
        var inconnu = new NetworkInfo { WifiProfileCount = -1 };

        Assert.NotEqual(connu.WifiProfileCount, inconnu.WifiProfileCount);
        Assert.True(inconnu.WifiProfileCount < 0);
        Assert.False(connu.WifiProfileCount < 0);
    }

    [Fact]
    public void Une_machine_sans_carte_sans_fil_est_reconnue_comme_telle()
    {
        var r = new NetworkInfo();
        r.Adapters.Add(new NetworkAdapterInfo { Kind = "Ethernet", IsWireless = false, Name = "Ethernet" });
        Assert.False(r.HasWireless);

        r.Adapters.Add(new NetworkAdapterInfo { Kind = "Wi-Fi", IsWireless = true, Name = "Wi-Fi" });
        Assert.True(r.HasWireless);
    }

    [Fact]
    public void Le_collecteur_rend_toujours_un_resultat_meme_si_tout_echoue()
    {
        // Aucune panne de collecte ne doit faire tomber une analyse : le réseau est
        // une information de plus, pas une condition de fonctionnement.
        var erreurs = new List<string>();
        var info = NetworkCollector.Collect(erreurs);

        Assert.NotNull(info);
        Assert.NotNull(info.Adapters);
        Assert.NotNull(info.Services);
    }
}
