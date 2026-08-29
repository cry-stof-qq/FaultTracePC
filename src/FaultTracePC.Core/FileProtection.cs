using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;

namespace FaultTracePC.Core;

/// <summary>
/// Restreint un fichier au compte SYSTEM et au groupe Administrateurs.
///
/// POURQUOI
/// <c>remote.json</c> vit dans <c>C:\ProgramData\FaultTracePC</c>. Les permissions
/// par défaut de ProgramData laissent le groupe Utilisateurs LIRE ce qui s'y
/// trouve : sur un poste partagé, n'importe quelle session ouvre le fichier et
/// lit le jeton de la machine. Le jeton ne circule pourtant jamais sur le réseau
/// (les requêtes sont signées) — le protéger sur le disque était le maillon
/// manquant.
///
/// CE QUE CELA NE CASSE PAS, ET POURQUOI ON PEUT L'AFFIRMER
/// Le service de surveillance tourne en LocalSystem ; l'application et la ligne
/// de commande portent toutes deux <c>requestedExecutionLevel
/// level="requireAdministrator"</c> dans leur manifeste. Les trois lecteurs du
/// fichier sont donc dans les deux comptes conservés.
/// </summary>
[SupportedOSPlatform("windows")]
public static class FileProtection
{
    /// <summary>
    /// Applique la restriction. Ne lève jamais : renvoie faux et remplit
    /// <paramref name="erreur"/>, à l'appelant de décider s'il le signale.
    /// </summary>
    public static bool RestrictToSystemAndAdministrators(string path, out string erreur)
    {
        erreur = "";
        try
        {
            var fichier = new FileInfo(path);
            if (!fichier.Exists)
            {
                // pas-de-traduction : ne part que dans erreurs.log (voir ErrorLog).
                erreur = $"{path} : fichier absent";
                return false;
            }

            var acces = fichier.GetAccessControl();

            // LES SID, PAS LES NOMS. Sur un Windows français le groupe s'appelle
            // « Administrateurs », sur un Windows allemand « Administratoren » :
            // écrire le nom en dur produirait une exception sur la moitié des
            // machines d'un établissement.
            var systeme = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
            var admins = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);

            // 1. Couper l'héritage SANS conserver les règles héritées : les garder
            //    laisserait précisément le groupe Utilisateurs qu'on vient retirer.
            acces.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);

            // 2. Repartir de zéro : le fichier peut porter des règles explicites
            //    posées par une version antérieure, ou par une main humaine.
            foreach (FileSystemAccessRule regle in
                     acces.GetAccessRules(includeExplicit: true, includeInherited: false, typeof(SecurityIdentifier)))
                acces.RemoveAccessRuleSpecific(regle);

            acces.AddAccessRule(new FileSystemAccessRule(systeme, FileSystemRights.FullControl, AccessControlType.Allow));
            acces.AddAccessRule(new FileSystemAccessRule(admins, FileSystemRights.FullControl, AccessControlType.Allow));

            fichier.SetAccessControl(acces);
            return true;
        }
        catch (Exception ex)
        {
            erreur = ErrorLog.Describe(ex);
            return false;
        }
    }

    /// <summary>
    /// Vrai si le fichier n'hérite plus de rien et n'accorde d'accès qu'à SYSTEM
    /// et aux Administrateurs. Sert au test, et à répondre honnêtement à
    /// « est-ce que c'est protégé ? » plutôt qu'à le supposer parce qu'on a
    /// appelé la méthode plus haut.
    /// </summary>
    public static bool IsRestricted(string path)
    {
        try
        {
            var acces = new FileInfo(path).GetAccessControl();
            var regles = acces.GetAccessRules(includeExplicit: true, includeInherited: true, typeof(SecurityIdentifier));

            // Une seule règle héritée suffit à rouvrir le fichier : on exige donc
            // que l'héritage soit coupé, et pas seulement que la liste soit courte.
            if (regles.Cast<FileSystemAccessRule>().Any(r => r.IsInherited)) return false;

            var autorises = new[]
            {
                new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
                new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
            };

            return regles.Cast<FileSystemAccessRule>()
                         .All(r => autorises.Any(sid => sid.Equals(r.IdentityReference)));
        }
        catch { return false; }
    }
}
