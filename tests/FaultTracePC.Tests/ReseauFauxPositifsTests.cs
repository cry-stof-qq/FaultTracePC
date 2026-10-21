using FaultTracePC.Core;
using FaultTracePC.Core.Analysis;
using FaultTracePC.Core.Collectors;
using Xunit;

namespace FaultTracePC.Tests;

/// <summary>
/// Constaté le 18/09/2026 sur deux machines en parfait état de marche, dès la
/// première utilisation de la 1.6.0. Deux faux positifs, et un faux positif qui
/// tombe sur chaque poste d'un parc décrédibilise tout le reste du rapport.
/// </summary>
[Collection("Langue")]
public class ReseauFauxPositifsTests
{
    private static DiagnosticReport Rapport(NetworkInfo net)
    {
        var r = new DiagnosticReport
        {
            GeneratedAt = new DateTime(2026, 9, 18, 5, 16, 0),
            ScanPeriodDays = 30,
            System = new SystemSnapshot { MachineName = "POSTE-TEST" },
        };
        r.System.Network = net;
        return r;
    }

    private static void Analyser(DiagnosticReport r)
    {
        var initial = Lang.Current;
        try { Lang.Apply(AppLanguage.French); new RulesEngine().Analyze(r); }
        finally { Lang.Apply(initial); }
    }

    private static ServiceStateInfo Service(string nom, string etat, string demarrage) =>
        new() { Name = nom, DisplayName = nom, State = etat, StartMode = demarrage };

    [Fact]
    public void Un_service_a_la_demande_arrete_n_est_pas_un_defaut()
    {
        // NlaSvc, arrêté et en démarrage « Manual » : c'est son fonctionnement normal.
        var net = new NetworkInfo { WifiProfileCount = 10 };
        net.Adapters.Add(new NetworkAdapterInfo { Name = "Ethernet", Kind = "Ethernet", HasIpV4 = true });
        net.Services.Add(Service("NlaSvc", "Stopped", "Manual"));

        var r = Rapport(net);
        Analyser(r);

        Assert.DoesNotContain(r.Findings, f => f.Title.Contains("NlaSvc"));
    }

    [Fact]
    public void Un_service_automatique_arrete_reste_signale()
    {
        var net = new NetworkInfo { WifiProfileCount = 10 };
        net.Adapters.Add(new NetworkAdapterInfo { Name = "Ethernet", Kind = "Ethernet", HasIpV4 = true });
        net.Services.Add(Service("Dhcp", "Stopped", "Auto"));

        var r = Rapport(net);
        Analyser(r);

        var f = r.Findings.First(x => x.Title.Contains("Dhcp"));
        Assert.Equal(Severity.Warning, f.Severity);
        // Et l'état s'écrit en français, pas en anglais de WMI.
        Assert.Contains("arrêté", f.Details);
        Assert.DoesNotContain("Stopped", f.Details);
    }

    [Theory]
    [InlineData("Fortinet SSL VPN Virtual Ethernet Adapter", true)]
    [InlineData("Microsoft Wi-Fi Direct Virtual Adapter #2", true)]
    [InlineData("VirtualBox Host-Only Ethernet Adapter", true)]
    [InlineData("WAN Miniport (IP)", true)]
    [InlineData("MediaTek Wi-Fi 6E MT7922 (RZ616) 160MHz PCIe Adapter", false)]
    [InlineData("Intel(R) Ethernet Connection (7) I219-LM", false)]
    public void Une_carte_virtuelle_est_reconnue_comme_telle(string description, bool virtuelle)
    {
        Assert.Equal(virtuelle, NetworkCollector.EstVirtuelle(description));
    }

    // Point 111 — constaté le 20/10/2026 sur le rapport du poste de l'auteur : 65
    // couches de filtrage listées comme des cartes. Noms relevés tels
    // quels dans ce rapport (ce sont des noms de pilotes, pas des noms de machines).
    [Theory]
    [InlineData("Ethernet 7-QoS Packet Scheduler-0000", "UsbNcm Host Device-QoS Packet Scheduler-0000")]
    [InlineData("Ethernet-WFP Native MAC Layer LightWeight Filter-0000", "Realtek PCIe GbE Family Controller-WFP Native MAC Layer LightWeight Filter-0000")]
    [InlineData("Ethernet 2-Npcap Packet Driver (NPCAP)-0000", "VirtualBox Host-Only Ethernet Adapter-Npcap Packet Driver (NPCAP)-0000")]
    [InlineData("Ethernet 3-VirtualBox NDIS Light-Weight Filter-0000", "Fortinet Virtual Ethernet Adapter (NDIS 6.30)-VirtualBox NDIS Light-Weight Filter-0000")]
    [InlineData("vEthernet (Default Switch)-WFP 802.3 MAC Layer LightWeight Filter-0000", "Hyper-V Virtual Ethernet Adapter-WFP 802.3 MAC Layer LightWeight Filter-0000")]
    [InlineData("vSwitch (Default Switch)-Hyper-V Virtual Switch Extension Filter-0000", "Hyper-V Virtual Switch Extension Adapter-Hyper-V Virtual Switch Extension Filter-0000")]
    [InlineData("Connexion au réseau local* 8-Fortinet NDIS 6.0 LightWeight Filter-0000", "WAN Miniport (IP)-Fortinet NDIS 6.0 LightWeight Filter-0000")]
    public void Une_couche_de_filtrage_n_est_pas_une_carte(string nom, string description)
    {
        Assert.True(NetworkCollector.EstCoucheDeFiltrage(nom, description));
    }

    // Neuf des vraies interfaces du même rapport : aucune ne doit être écartée.
    [Theory]
    [InlineData("vEthernet (Default Switch)", "Hyper-V Virtual Ethernet Adapter")]
    [InlineData("Ethernet 2", "VirtualBox Host-Only Ethernet Adapter")]
    [InlineData("Connexion au réseau local* 1", "Microsoft Wi-Fi Direct Virtual Adapter")]
    [InlineData("Connexion au réseau local* 2", "Microsoft Wi-Fi Direct Virtual Adapter #2")]
    [InlineData("Ethernet 3", "Fortinet Virtual Ethernet Adapter (NDIS 6.30)")]
    [InlineData("Wi-Fi", "MediaTek Wi-Fi 6E MT7922 (RZ616) 160MHz PCIe Adapter")]
    [InlineData("Connexion réseau Bluetooth", "Bluetooth Device (Personal Area Network)")]
    [InlineData("Ethernet", "Realtek PCIe GbE Family Controller")]
    [InlineData("Ethernet 7", "UsbNcm Host Device")]
    public void Une_vraie_carte_n_est_pas_une_couche_de_filtrage(string nom, string description)
    {
        Assert.False(NetworkCollector.EstCoucheDeFiltrage(nom, description));
    }

    [Theory]
    // Fin numérotée d'un seul côté : rien ne permet de conclure.
    [InlineData("Ethernet-0000", "Realtek PCIe GbE Family Controller")]
    // Même fin numérotée, mais pas de nom de filtre devant : pas une couche.
    [InlineData("Carte-0001", "Pilote-0001")]
    [InlineData("", "")]
    public void Une_fin_numerotee_seule_ne_suffit_pas(string nom, string description)
    {
        Assert.False(NetworkCollector.EstCoucheDeFiltrage(nom, description));
    }

    [Fact]
    public void Un_adaptateur_wifi_direct_ne_declenche_pas_une_panne_de_wifi()
    {
        // LE cas du faux positif : poste fixe sans radio, mais Windows expose deux
        // « Microsoft Wi-Fi Direct Virtual Adapter ». Sans le filtre, le rapport
        // annonçait une panne de Wi-Fi sur une machine qui n'en a jamais eu.
        var net = new NetworkInfo { WifiProfileCount = 0, PartOfDomain = true, Domain = "ecole.local" };
        net.Adapters.Add(new NetworkAdapterInfo { Name = "Ethernet", Kind = "Ethernet", HasIpV4 = true, IsPhysical = true });
        net.Adapters.Add(new NetworkAdapterInfo
        {
            Name = "Connexion au réseau local* 1", Description = "Microsoft Wi-Fi Direct Virtual Adapter",
            Kind = "Wi-Fi", IsWireless = true, IsPhysical = false,
        });

        var r = Rapport(net);
        Analyser(r);

        Assert.False(net.HasWireless);
        Assert.DoesNotContain(r.Findings, f => f.Code == "wifi_sans_profil");
    }

    [Fact]
    public void Une_vraie_carte_sans_fil_declenche_toujours_la_conclusion()
    {
        var net = new NetworkInfo { WifiProfileCount = 0, PartOfDomain = true, Domain = "ecole.local" };
        net.Adapters.Add(new NetworkAdapterInfo
        {
            Name = "Wi-Fi", Description = "Intel(R) Wireless-AC 9560",
            Kind = "Wi-Fi", IsWireless = true, IsPhysical = true, HasIpV4 = true,
        });

        var r = Rapport(net);
        Analyser(r);

        Assert.True(net.HasWireless);
        Assert.Contains(r.Findings, f => f.Code == "wifi_sans_profil");
    }
}
