using System.Text.Json;

namespace FaultTracePC.Core;

/// <summary>
/// Réglages de la console de parc, dans <c>Documents\FaultTracePC\parametres.json</c>,
/// à côté de <c>parc.json</c>.
///
/// CE FICHIER NE CONTIENT AUCUN SECRET, ET NE DOIT JAMAIS EN CONTENIR.
/// Le secret maître ne s'écrit nulle part sur disque, et le jeton d'un poste se
/// déduit à chaque interrogation. Ce fichier-ci ne porte que des préférences —
/// il peut être lu, copié ou envoyé sans conséquence, et c'est ce qui permet de
/// le traiter comme un simple réglage plutôt que comme un élément à protéger.
/// </summary>
public sealed class ParametresParc
{
    /// <summary>
    /// Unité d'organisation interrogée dans l'Active Directory, par exemple
    /// <c>OU=Postes,DC=exemple,DC=fr</c>.
    ///
    /// VIDE = LA RACINE DU DOMAINE, découverte toute seule. C'est volontairement le
    /// cas par défaut : dans un établissement dont tous les postes sont sous la même
    /// racine, il n'y a rien à saisir pour que ça marche.
    /// </summary>
    public string UniteOrganisation { get; set; } = "";

    /// <summary>
    /// Où trouver <c>postes.csv</c>, l'annuaire d'adresses MAC écrit par le script
    /// de déploiement. Un dossier convient autant qu'un chemin de fichier.
    ///
    /// VIDE = À CÔTÉ DE <c>parc.json</c>, dans les Documents. Ce réglage n'existe
    /// que parce que le script, prévu pour tourner depuis une clé USB, écrit son
    /// fichier là où il se trouve — pas là où la console lit.
    /// </summary>
    public string FichierAdressesMac { get; set; } = "";

    /// <summary>
    /// Nom du serveur DHCP du site, consulté pour retrouver l'adresse MAC d'un
    /// poste ÉTEINT — la seule source qui ne vieillit pas.
    ///
    /// POURQUOI CE RÉGLAGE VIT ICI ET PAS SEULEMENT DANS LE SCRIPT. Le script sait
    /// demander ce nom au premier lancement, mais seulement quand quelqu'un est
    /// devant l'écran. Lancé par la console, il reçoit <c>-SortieJson</c>, se tait
    /// par construction, et la question n'est jamais posée : le réveil réseau ne
    /// pouvait alors aboutir pour AUCUN poste. Constaté le 21/09/2026 sur quatre
    /// postes que d'autres outils réveillent sans difficulté.
    ///
    /// VIDE = PAS DE DHCP CONSULTÉ. Le script se rabat alors sur postes.csv puis
    /// sur le cache ARP, et le dit.
    ///
    /// CE N'EST PAS UN SECRET, c'est un nom de serveur. Mais parametres.json ne se
    /// diffuse pas pour autant : il nomme votre infrastructure.
    /// </summary>
    public string ServeurDhcp { get; set; } = "";

    /// <summary>
    /// Chemin du paquet <c>.msi</c> à déployer, sur un partage lisible par les
    /// ORDINATEURS du domaine — pas seulement par les utilisateurs : l'installation
    /// se fait sous le compte machine.
    ///
    /// Vide tant qu'on n'a rien déployé. Ce n'est pas un secret : c'est un chemin.
    /// </summary>
    public string CheminDuPaquet { get; set; } = "";

    public static string Chemin => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "FaultTracePC", "parametres.json");

    /// <summary>
    /// Relit les réglages. Fichier absent ou illisible : des réglages par défaut,
    /// sans rien signaler — un réglage manquant n'est pas une panne, et la valeur
    /// par défaut fonctionne.
    /// </summary>
    public static ParametresParc Charger()
    {
        try
        {
            if (File.Exists(Chemin) &&
                JsonSerializer.Deserialize<ParametresParc>(File.ReadAllText(Chemin)) is { } p)
                return p;
        }
        catch { /* fichier abîmé : on repart des valeurs par défaut */ }
        return new ParametresParc();
    }

    /// <summary>
    /// Enregistre les réglages. Rend <c>false</c> en cas d'échec au lieu de lever :
    /// ne pas pouvoir mémoriser une préférence ne doit pas interrompre le travail en
    /// cours, mais l'appelant doit pouvoir le dire à l'utilisateur.
    /// </summary>
    public bool Enregistrer()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Chemin)!);
            File.WriteAllText(Chemin, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
            return true;
        }
        catch { return false; }
    }
}
