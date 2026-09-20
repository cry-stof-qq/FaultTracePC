using FaultTracePC.Core;
using FaultTracePC.Core.Analysis;
using Xunit;

namespace FaultTracePC.Tests;

/// <summary>
/// Point 53, seconde moitié. Le 14/09/2026 sur POSTE-ELEVE-01, la panne était « plus
/// aucun réseau Wi-Fi visible » et il avait fallu la reconstituer à la main : carte
/// présente, zéro profil enregistré, poste du domaine jamais revu depuis sa
/// réinstallation. La correction tenait en un câble et un gpupdate /force.
/// </summary>
[Collection("Langue")]
public class ReseauConclusionTests
{
    private static DiagnosticReport Rapport(NetworkInfo net)
    {
        var r = new DiagnosticReport
        {
            GeneratedAt = new DateTime(2026, 9, 14, 10, 57, 0),
            ScanPeriodDays = 30,
            System = new SystemSnapshot { MachineName = "POSTE-ELEVE-01" },
        };
        r.System.Network = net;
        return r;
    }

    private static NetworkAdapterInfo Wifi(bool ip = false) => new()
    {
        Name = "Wi-Fi", Description = "Intel(R) Wireless-AC 9560", Kind = "Wi-Fi",
        IsWireless = true, Status = "Up", HasIpV4 = ip,
    };

    private static NetworkAdapterInfo Ethernet(bool ip) => new()
    {
        Name = "Ethernet", Description = "Realtek Gaming GbE", Kind = "Ethernet",
        IsWireless = false, Status = ip ? "Up" : "Down", HasIpV4 = ip,
    };

    private static void Analyser(DiagnosticReport r)
    {
        var initial = Lang.Current;
        try { Lang.Apply(AppLanguage.French); new RulesEngine().Analyze(r); }
        finally { Lang.Apply(initial); }
    }

    [Fact]
    public void Carte_wifi_sans_aucun_profil_donne_la_conclusion_et_le_geste()
    {
        var net = new NetworkInfo { WifiProfileCount = 0, PartOfDomain = true, Domain = "ecole.local" };
        net.Adapters.Add(Wifi());
        net.Adapters.Add(Ethernet(ip: true));
        var r = Rapport(net);

        Analyser(r);

        var f = r.Findings.First(x => x.Code == "wifi_sans_profil");
        Assert.Contains("AUCUN réseau", f.Details);
        Assert.Contains("ecole.local", f.Details);
        // La boucle, et le seul geste qui la casse.
        Assert.Contains("gpupdate /force", f.Recommendation);
        // Un câble est branché : le rapport doit le dire, ça change le moment d'agir.
        Assert.Contains("câble est actuellement connecté", f.Details);
        // La machine a du réseau filaire : c'est gênant, pas critique.
        Assert.Equal(Severity.Warning, f.Severity);
    }

    [Fact]
    public void Sans_la_moindre_adresse_ip_la_conclusion_devient_critique()
    {
        var net = new NetworkInfo { WifiProfileCount = 0, PartOfDomain = true, Domain = "ecole.local" };
        net.Adapters.Add(Wifi());
        net.Adapters.Add(Ethernet(ip: false));
        var r = Rapport(net);

        Analyser(r);

        Assert.Equal(Severity.Critical, r.Findings.First(x => x.Code == "wifi_sans_profil").Severity);
    }

    [Fact]
    public void Un_dossier_de_profils_illisible_ne_permet_de_rien_conclure()
    {
        // -1 veut dire « pas pu regarder ». Annoncer une panne ici serait pire que se taire.
        var net = new NetworkInfo { WifiProfileCount = -1, PartOfDomain = true, Domain = "ecole.local" };
        net.Adapters.Add(Wifi(ip: true));
        var r = Rapport(net);

        Analyser(r);

        Assert.DoesNotContain(r.Findings, x => x.Code == "wifi_sans_profil");
    }

    [Fact]
    public void Des_profils_enregistres_ne_declenchent_rien()
    {
        var net = new NetworkInfo { WifiProfileCount = 4, PartOfDomain = true, Domain = "ecole.local" };
        net.Adapters.Add(Wifi(ip: true));
        var r = Rapport(net);

        Analyser(r);

        Assert.DoesNotContain(r.Findings, x => x.Code == "wifi_sans_profil");
    }

    [Fact]
    public void Le_service_wifi_arrete_est_signale_seulement_s_il_y_a_une_carte_sans_fil()
    {
        var avecCarte = new NetworkInfo { WifiProfileCount = 3 };
        avecCarte.Adapters.Add(Wifi(ip: true));
        avecCarte.Services.Add(new ServiceStateInfo { Name = "WlanSvc", DisplayName = "Service WLAN", State = "Stopped", StartMode = "Auto" });
        var r1 = Rapport(avecCarte);
        Analyser(r1);
        var f = r1.Findings.First(x => x.Title.Contains("WlanSvc"));
        Assert.Equal(Severity.Warning, f.Severity);
        Assert.Contains("sc.exe start WlanSvc", f.Recommendation);

        // Sur un poste fixe sans carte sans fil, ce service arrêté est normal.
        var sansCarte = new NetworkInfo { WifiProfileCount = 0 };
        sansCarte.Adapters.Add(Ethernet(ip: true));
        sansCarte.Services.Add(new ServiceStateInfo { Name = "WlanSvc", DisplayName = "Service WLAN", State = "Stopped", StartMode = "Auto" });
        var r2 = Rapport(sansCarte);
        Analyser(r2);
        Assert.DoesNotContain(r2.Findings, x => x.Title.Contains("WlanSvc"));
    }

    [Fact]
    public void Un_service_desactive_volontairement_reste_une_information()
    {
        var net = new NetworkInfo { WifiProfileCount = 2 };
        net.Adapters.Add(Wifi(ip: true));
        net.Services.Add(new ServiceStateInfo { Name = "Dnscache", DisplayName = "Client DNS", State = "Stopped", StartMode = "Disabled" });
        var r = Rapport(net);

        Analyser(r);

        Assert.Equal(Severity.Info, r.Findings.First(x => x.Title.Contains("Dnscache")).Severity);
    }
}
