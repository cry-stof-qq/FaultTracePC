using System.Diagnostics;
using System.Runtime.Versioning;

namespace FaultTracePC.Core;

/// <summary>
/// Règle de pare-feu entrante du mode parc, limitée aux plages privées.
///
/// POURQUOI CE CODE A DÉMÉNAGÉ DANS Core
/// Il ne vivait que dans le projet WPF, appelé par la fenêtre « Mode réseau ».
/// Conséquence constatée le 30/08/2026 en écrivant la procédure de déploiement :
/// un parc installé par stratégie de groupe — MSI, puis
/// <c>--configure-remote</c> — obtenait des postes qui écoutent correctement et
/// que RIEN ne peut joindre. Le MSI ne crée pas la règle, la ligne de commande
/// non plus, et personne ne s'en aperçoit avant le jour du déploiement.
///
/// CE QUE CETTE RÈGLE EST, ET CE QU'ELLE N'EST PAS
/// C'est de la défense en profondeur, pas la sécurité du mode parc : le service
/// refuse de toute façon les adresses non privées et exige une signature sur
/// chaque requête. Restreindre <c>remoteip</c> évite simplement d'ouvrir un port
/// à un réseau entier.
///
/// EN DOMAINE, ELLE PEUT ÊTRE IGNORÉE. Selon la configuration du profil de
/// domaine (« appliquer les règles de pare-feu locales »), une règle posée par
/// netsh n'est pas prise en compte. C'est pourquoi elle reste un SECOURS pour les
/// parcs hors domaine, et pourquoi <c>--configure-remote</c> dit ce qu'il a fait
/// au lieu de le laisser supposer.
/// </summary>
[SupportedOSPlatform("windows")]
public static class FirewallRule
{
    public const string Nom = "FaultTracePC Telemetry";

    /// <summary>Plages acceptées : boucle locale + RFC 1918. Les mêmes que le service.</summary>
    public const string PlagesPrivees = "127.0.0.1,10.0.0.0/8,172.16.0.0/12,192.168.0.0/16";

    /// <summary>Arguments de création. Fonction pure : c'est elle qui se teste.</summary>
    public static string ArgumentsAjout(int port) =>
        $"advfirewall firewall add rule name=\"{Nom}\" dir=in action=allow protocol=TCP localport={port} " +
        $"remoteip={PlagesPrivees}";

    /// <summary>Arguments de suppression.</summary>
    public static string ArgumentsSuppression() =>
        $"advfirewall firewall delete rule name=\"{Nom}\"";

    /// <summary>
    /// Pose la règle, en remplaçant celle qui existe. Renvoie faux et remplit
    /// <paramref name="erreur"/> plutôt que de lever : un pare-feu récalcitrant ne
    /// doit pas faire échouer une configuration par ailleurs correcte — mais
    /// l'appelant doit pouvoir le DIRE.
    /// </summary>
    public static bool Poser(int port, out string erreur)
    {
        Netsh(ArgumentsSuppression(), out _);              // idempotence : on ne cumule pas les règles
        return Netsh(ArgumentsAjout(port), out erreur);
    }

    /// <summary>Retire la règle. Vrai aussi s'il n'y en avait pas.</summary>
    public static bool Retirer(out string erreur) => Netsh(ArgumentsSuppression(), out erreur);

    private static bool Netsh(string args, out string erreur)
    {
        erreur = "";
        try
        {
            var psi = new ProcessStartInfo("netsh.exe", args)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            using var p = Process.Start(psi)!;
            var sortie = p.StandardOutput.ReadToEnd();
            var err = p.StandardError.ReadToEnd();
            if (!p.WaitForExit(10000)) { erreur = "netsh n'a pas rendu la main"; return false; }  // pas-de-traduction : erreurs.log
            if (p.ExitCode == 0) return true;

            erreur = (err.Trim().Length > 0 ? err : sortie).Trim();
            return false;
        }
        catch (Exception ex)
        {
            erreur = ErrorLog.Describe(ex);
            return false;
        }
    }
}
