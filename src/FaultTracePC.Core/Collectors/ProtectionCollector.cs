using System.Management;

namespace FaultTracePC.Core.Collectors;

/// <summary>
/// POINT 81 — lit l'état réel de la protection antivirus, au lieu de le laisser
/// deviner d'après la liste des processus (voir <see cref="EtatProtection"/>).
///
/// Deux lectures indépendantes : si l'une échoue, l'autre est quand même faite,
/// et l'échec est dit UNE fois dans les limitations du rapport. Aucune des deux
/// ne demande de droit particulier ni ne modifie quoi que ce soit.
/// </summary>
public static class ProtectionCollector
{
    public static EtatProtection Collect(List<string> errors)
    {
        var etat = new EtatProtection();
        LireCentreSecurite(etat, errors);
        LireDefender(etat, errors);
        return etat;
    }

    private static void LireCentreSecurite(EtatProtection etat, List<string> errors)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(@"root\SecurityCenter2", "SELECT * FROM AntiVirusProduct");
            foreach (ManagementObject mo in searcher.Get())
            {
                etat.Antivirus.Add(new AntivirusInscrit
                {
                    Nom = Texte(mo, "displayName"),
                    CheminProduit = Texte(mo, "pathToSignedProductExe"),
                    EtatBrut = Entier(mo, "productState"),
                });
            }
            etat.CentreSecuriteLu = true;
        }
        catch (Exception ex)
        {
            errors.Add(Lang.T(
                $"Centre de sécurité Windows illisible ({ex.Message}) : la liste des antivirus inscrits n'est pas disponible. Ce Centre n'existe pas sur les éditions Serveur de Windows.",
                $"Windows Security Center unreadable ({ex.Message}): the list of registered antivirus products is not available. This Center does not exist on Windows Server editions."));
        }
    }

    private static void LireDefender(EtatProtection etat, List<string> errors)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(@"root\Microsoft\Windows\Defender", "SELECT * FROM MSFT_MpComputerStatus");
            foreach (ManagementObject mo in searcher.Get())
            {
                etat.DefenderMode = Texte(mo, "AMRunningMode");
                if (Existe(mo, "RealTimeProtectionEnabled") && mo["RealTimeProtectionEnabled"] is bool tempsReel)
                    etat.DefenderTempsReel = tempsReel;
                etat.DefenderLu = true;
                break; // une seule instance
            }
        }
        catch (Exception ex)
        {
            errors.Add(Lang.T(
                $"État de Windows Defender illisible ({ex.Message}).",
                $"Windows Defender status unreadable ({ex.Message})."));
        }
    }

    private static bool Existe(ManagementObject mo, string prop) =>
        mo.Properties.Cast<PropertyData>().Any(p => p.Name == prop);

    private static string Texte(ManagementObject mo, string prop) =>
        Existe(mo, prop) ? mo[prop]?.ToString()?.Trim() ?? "" : "";

    private static uint Entier(ManagementObject mo, string prop)
    {
        try { return Existe(mo, prop) && mo[prop] is { } v ? Convert.ToUInt32(v) : 0u; }
        catch { return 0u; }
    }
}
