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
