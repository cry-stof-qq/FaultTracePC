using System.Reflection;

namespace FaultTracePC.Core;

/// <summary>
/// POINT 64, LOT B — le script de déploiement, embarqué dans le logiciel.
///
/// POURQUOI IL VOYAGE AVEC LE LOGICIEL
/// Décidé le 19/09/2026. Tant qu'il était distribué à part, il existait en deux
/// exemplaires qui ont fini par diverger — l'un des deux portait encore un exemple
/// périmé après correction de l'autre. Surtout, le contrat JSON du lot B lie le
/// script et la console : deux fichiers livrés séparément peuvent se désynchroniser
/// sans que rien ne le signale, et le symptôme serait un journal illisible. Embarqué,
/// le script livré est celui qui a été éprouvé avec cette version-là.
///
/// LE SCRIPT RESTE UTILISABLE SEUL. Extrait, il se comporte exactement comme avant :
/// il se trouve tout seul par <c>$PSScriptRoot</c> et tourne depuis n'importe quel
/// dossier, y compris une clé USB. Le logiciel le pilote, il ne le remplace pas.
/// </summary>
public static class ScriptDeDeploiement
{
    /// <summary>Nom logique de la ressource, tel que le projet du noyau le déclare.</summary>
    public const string NomRessource = "FaultTracePC.Deployer-FaultTracePC.ps1";

    /// <summary>Nom du fichier une fois écrit sur disque.</summary>
    public const string NomFichier = "Deployer-FaultTracePC.ps1";

    /// <summary>
    /// Le script tel qu'il a été compilé, octet pour octet.
    ///
    /// LES OCTETS BRUTS, PAS DU TEXTE RECOMPOSÉ. Le fichier commence par une marque
    /// d'ordre des octets UTF-8, et elle n'est pas décorative : <b>PowerShell 5.1 lit
    /// un .ps1 sans marque comme s'il était en page de code ANSI</b>, et tous les
    /// accents du script en ressortent abîmés. Relire en texte puis réécrire ferait
    /// courir le risque de la perdre ; copier les octets ne le fait pas courir.
    /// </summary>
    public static byte[] Octets()
    {
        using var flux = typeof(ScriptDeDeploiement).Assembly.GetManifestResourceStream(NomRessource)
            ?? throw new InvalidOperationException(Lang.T(
                $"Ressource introuvable : {NomRessource}. Le projet du noyau ne l'a pas compilée.",
                $"Resource not found: {NomRessource}. The core project did not compile it."));

        using var memoire = new MemoryStream();
        flux.CopyTo(memoire);
        return memoire.ToArray();
    }

    /// <summary>
    /// Le script en texte, pour l'examiner — pas pour l'écrire. Voir
    /// <see cref="Octets"/> pour la raison.
    /// </summary>
    public static string Lire()
    {
        using var flux = new MemoryStream(Octets());
        using var lecteur = new StreamReader(flux, System.Text.Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return lecteur.ReadToEnd();
    }

    /// <summary>
    /// Écrit le script dans <paramref name="dossier"/> et rend son chemin complet.
    ///
    /// ÉCRASE À CHAQUE FOIS, volontairement : la version embarquée fait foi. Une
    /// copie laissée là par une version précédente du logiciel ne doit jamais
    /// l'emporter sur celle qui correspond au contrat JSON de la version en cours.
    ///
    /// Le dossier est créé s'il manque. Une écriture impossible lève : l'appelant
    /// doit pouvoir le dire, pas continuer avec un chemin qui ne mène à rien.
    /// </summary>
    public static string Extraire(string dossier)
    {
        if (string.IsNullOrWhiteSpace(dossier))
            throw new ArgumentException(
                Lang.T("Dossier de destination vide.", "Destination folder is empty."), nameof(dossier));

        Directory.CreateDirectory(dossier);

        var chemin = Path.Combine(dossier, NomFichier);
        File.WriteAllBytes(chemin, Octets());
        return chemin;
    }

    /// <summary>
    /// Dossier de travail par défaut : à côté des données de la console, jamais dans
    /// le dossier d'installation. Écrire dans <c>Program Files</c> demanderait des
    /// droits que le logiciel n'a pas à réclamer pour ça, et laisserait une trace
    /// dans un dossier géré par l'installateur.
    /// </summary>
    public static string DossierParDefaut => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "FaultTracePC", "Deploiement");
}
