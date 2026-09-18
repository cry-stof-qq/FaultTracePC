using System.Management;
using System.Net.NetworkInformation;

namespace FaultTracePC.Core.Collectors;

/// <summary>
/// POINT 53 de la feuille de route. Le réseau était le seul domaine dont le logiciel
/// ne disait RIEN. Le 14/09/2026, sur MLEAR-031-2024, la panne était « plus aucun
/// réseau Wi-Fi visible » : le rapport a parlé de la batterie et des erreurs disque.
///
/// Trois faits suffisaient à conclure, et ils sont tous lisibles sans outil externe :
/// une carte Wi-Fi présente et active, ZÉRO profil enregistré, et un poste qui n'a
/// jamais vu de câble depuis sa réinstallation. Ce collecteur va chercher ces faits.
///
/// Rien n'est jamais bloquant ici : un poste sans carte Wi-Fi, un dossier de profils
/// inaccessible ou un WMI muet rendent une information vide, pas une erreur de scan.
/// </summary>
public static class NetworkCollector
{
    /// <summary>Services sans lesquels il n'y a pas de réseau, dans l'ordre où ils comptent.</summary>
    private static readonly string[] ServicesSuivis = { "WlanSvc", "Dhcp", "Dnscache", "NlaSvc" };

    /// <summary>Emplacement des profils Wi-Fi enregistrés (un fichier XML par réseau connu).</summary>
    private static string DossierProfilsWifi =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                     "Microsoft", "Wlansvc", "Profiles", "Interfaces");

    public static NetworkInfo Collect(List<string> errors)
    {
        var info = new NetworkInfo();
        CollecterCartes(info, errors);
        CompterProfilsWifi(info);
        CollecterServices(info, errors);
        CollecterDomaine(info, errors);
        return info;
    }

    private static void CollecterCartes(NetworkInfo info, List<string> errors)
    {
        try
        {
            // Les pilotes des cartes réseau, par description : c'est la seule clé
            // commune entre l'API .NET et l'inventaire WMI des pilotes signés.
            var pilotes = new Dictionary<string, (string Version, DateTime? Date)>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "SELECT DeviceName, DriverVersion, DriverDate FROM Win32_PnPSignedDriver WHERE DeviceClass = 'NET'");
                foreach (ManagementObject mo in searcher.Get())
                {
                    var nom = mo["DeviceName"]?.ToString() ?? "";
                    if (nom.Length == 0 || pilotes.ContainsKey(nom)) continue;
                    pilotes[nom] = (mo["DriverVersion"]?.ToString() ?? "", DateWmi(mo["DriverDate"]?.ToString()));
                }
            }
            catch { /* inventaire des pilotes indisponible : les cartes restent listées */ }

            foreach (var n in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (n.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel) continue;

                var carte = new NetworkAdapterInfo
                {
                    Name = n.Name,
                    Description = n.Description,
                    IsWireless = n.NetworkInterfaceType is NetworkInterfaceType.Wireless80211,
                    Status = n.OperationalStatus.ToString(),
                    MacMasked = MasquerMac(n.GetPhysicalAddress().ToString()),
                };
                // « Wi-Fi » et « Ethernet » s'écrivent pareil dans les deux langues :
                // les passer par Lang.T ne ferait qu'ajouter du bruit.
                carte.Kind = carte.IsWireless
                    ? "Wi-Fi"
                    : n.NetworkInterfaceType is NetworkInterfaceType.Ethernet or NetworkInterfaceType.GigabitEthernet or NetworkInterfaceType.FastEthernetT
                        ? "Ethernet"
                        : Lang.T("autre", "other");

                try
                {
                    var p = n.GetIPProperties();
                    carte.HasIpV4 = p.UnicastAddresses.Any(a =>
                        a.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
                        && !System.Net.IPAddress.IsLoopback(a.Address));
                }
                catch { /* une carte désactivée n'a pas de propriétés IP */ }

                if (pilotes.TryGetValue(n.Description, out var pil))
                {
                    carte.DriverVersion = pil.Version;
                    carte.DriverDate = pil.Date;
                }

                info.Adapters.Add(carte);
            }
        }
        catch (Exception ex)
        {
            errors.Add(Lang.T($"Réseau (cartes) : {ex.Message}", $"Network (adapters): {ex.Message}"));
        }
    }

    /// <summary>
    /// Compte les profils Wi-Fi enregistrés : un fichier XML par réseau connu, qu'il
    /// vienne d'une stratégie de groupe ou de l'utilisateur. ZÉRO profil sur une machine
    /// équipée d'une carte Wi-Fi est un fait, pas une absence de données — c'est
    /// exactement ce qui explique une liste de réseaux vide.
    ///
    /// Le dossier appartient au système : il peut être illisible sans élévation. On
    /// distingue alors « aucun profil » de « je n'ai pas pu regarder », parce que les
    /// deux mènent à des conclusions opposées.
    /// </summary>
    private static void CompterProfilsWifi(NetworkInfo info)
    {
        try
        {
            if (!Directory.Exists(DossierProfilsWifi))
            {
                info.WifiProfileCount = 0;
                info.WifiProfileNote = Lang.T("aucun dossier de profils : le service Wi-Fi n'a jamais rien enregistré",
                                              "no profile folder: the Wi-Fi service has never stored anything");
                return;
            }
            info.WifiProfileCount = Directory.EnumerateFiles(DossierProfilsWifi, "*.xml", SearchOption.AllDirectories).Count();
        }
        catch (UnauthorizedAccessException)
        {
            info.WifiProfileCount = -1;
            info.WifiProfileNote = Lang.T("dossier des profils illisible sans élévation — relancer l'analyse en administrateur",
                                          "profile folder unreadable without elevation — run the analysis as administrator");
        }
        catch (Exception ex)
        {
            info.WifiProfileCount = -1;
            info.WifiProfileNote = ex.Message;
        }
    }

    private static void CollecterServices(NetworkInfo info, List<string> errors)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Name, DisplayName, State, StartMode FROM Win32_Service");
            foreach (ManagementObject mo in searcher.Get())
            {
                var nom = mo["Name"]?.ToString() ?? "";
                if (!ServicesSuivis.Contains(nom, StringComparer.OrdinalIgnoreCase)) continue;
                info.Services.Add(new ServiceStateInfo
                {
                    Name = nom,
                    DisplayName = mo["DisplayName"]?.ToString() ?? "",
                    State = mo["State"]?.ToString() ?? "",
                    StartMode = mo["StartMode"]?.ToString() ?? "",
                });
            }
        }
        catch (Exception ex)
        {
            errors.Add(Lang.T($"Réseau (services) : {ex.Message}", $"Network (services): {ex.Message}"));
        }
    }

    private static void CollecterDomaine(NetworkInfo info, List<string> errors)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Domain, PartOfDomain FROM Win32_ComputerSystem");
            foreach (ManagementObject mo in searcher.Get())
            {
                info.PartOfDomain = mo["PartOfDomain"] is bool b && b;
                info.Domain = mo["Domain"]?.ToString() ?? "";
                break;
            }
        }
        catch (Exception ex)
        {
            errors.Add(Lang.T($"Réseau (domaine) : {ex.Message}", $"Network (domain): {ex.Message}"));
        }
    }

    /// <summary>
    /// Ne garde que le constructeur (les trois premiers octets) et masque le reste.
    /// Une adresse MAC complète identifie une machine précise : elle n'a rien à faire
    /// dans un rapport qu'on transmet, alors que le constructeur suffit à reconnaître
    /// la carte. Constat du 17/09/2026, en nettoyant un dépôt public.
    /// </summary>
    internal static string MasquerMac(string brute)
    {
        if (string.IsNullOrWhiteSpace(brute) || brute.Length < 12) return "";
        var hex = new string(brute.Where(Uri.IsHexDigit).ToArray());
        if (hex.Length < 12) return "";
        // Majuscules imposées : Windows rend la même carte tantôt en « BCFCE7 »,
        // tantôt en « bc-fc-e7 ». Deux écritures du même constructeur empêcheraient
        // de reconnaître d'un rapport à l'autre qu'il s'agit du même matériel.
        var oui = hex[..6].ToUpperInvariant();
        return $"{oui[0..2]}:{oui[2..4]}:{oui[4..6]}:xx:xx:xx";
    }

    /// <summary>Date WMI (« 20260625000000.000000+000 ») → DateTime, sans lever si le format surprend.</summary>
    private static DateTime? DateWmi(string? v)
    {
        if (string.IsNullOrWhiteSpace(v) || v.Length < 8) return null;
        try { return ManagementDateTimeConverter.ToDateTime(v); }
        catch { return null; }
    }
}
