namespace FaultTracePC.Core;

/// <summary>
/// POINT 64, LOT B — la liste des postes passée au script de déploiement.
///
/// POURQUOI UN FICHIER PLUTÔT QU'UNE LIGNE DE COMMANDE
/// Une ligne de commande Windows a une longueur maximale, de l'ordre de huit mille
/// caractères. Quelques centaines de noms de postes la dépassent, et l'échec
/// n'arrive pas au moment où on le comprendrait : il arrive le jour où le parc a
/// grossi. Un fichier n'a pas cette limite, et il évite au passage toute question
/// de guillemets et d'échappement.
///
/// LE FORMAT EST DÉLIBÉRÉMENT PAUVRE : un nom par ligne, « # » commence un
/// commentaire, les lignes vides sont ignorées. Il se relit à l'œil, se corrige
/// dans le Bloc-notes, et se produit par les deux côtés sans bibliothèque.
///
/// ET IL NE CONTIENT QUE DE L'ASCII. Les noms de poste s'écrivent en lettres,
/// chiffres et traits d'union — <see cref="EstNomValide"/> refuse le reste. La
/// question de l'encodage ne se pose donc jamais, quelle que soit la façon dont
/// PowerShell relit le fichier.
/// </summary>
public static class ListeDePostes
{
    /// <summary>
    /// Un nom de poste Windows : lettres, chiffres, traits d'union. Rien d'autre.
    ///
    /// CE CONTRÔLE EST UN GARDE-FOU, pas une coquetterie. Ce fichier est lu par un
    /// script qui en fait des paramètres : un nom contenant une apostrophe, un
    /// point-virgule ou un saut de ligne n'aurait aucune chance d'être un vrai
    /// poste, et toutes les chances d'être un accident — ou pire. On refuse avant
    /// d'écrire, pas après.
    ///
    /// La longueur n'est pas contrainte : la limite Windows de quinze caractères
    /// est une convention que ce logiciel n'a pas à faire respecter, et un nom plus
    /// long reste inoffensif dès lors que ses caractères le sont.
    /// </summary>
    public static bool EstNomValide(string? nom)
    {
        var n = (nom ?? "").Trim();
        if (n.Length == 0) return false;
        return n.All(c => char.IsAsciiLetterOrDigit(c) || c == '-');
    }

    /// <summary>
    /// Écrit la liste et rend le nombre de noms retenus. Les noms sont normalisés
    /// comme partout ailleurs, dédoublonnés, et les invalides sont écartés — leur
    /// compte part dans <paramref name="ecartes"/> plutôt que dans le silence.
    /// </summary>
    public static int Ecrire(string chemin, IEnumerable<string> noms, List<string>? ecartes = null)
    {
        var retenus = Retenir(noms, ecartes);

        Directory.CreateDirectory(Path.GetDirectoryName(chemin)!);

        // LES DEUX LANGUES SANS AUCUN ACCENT, VOLONTAIREMENT. Ce fichier doit rester
        // en ASCII pur : un seul « é » dans l'en-tête rouvrirait la question de
        // l'encodage que tout le reste s'emploie à fermer. Le test le vérifie dans
        // les deux langues, octet par octet.
        var lignes = new List<string>
        {
            Lang.T("# FaultTracePC - liste de postes pour Deployer-FaultTracePC.ps1",
                   "# FaultTracePC - computer list for Deployer-FaultTracePC.ps1"),
            Lang.T("# Un nom par ligne. Ce fichier est reecrit a chaque execution.",
                   "# One name per line. This file is rewritten on every run."),
            "",
        };
        lignes.AddRange(retenus);

        // ASCII de bout en bout : le contenu l'est par construction, et l'écrire
        // sans marque d'ordre des octets évite que PowerShell la prenne pour le
        // premier caractère du premier nom.
        File.WriteAllLines(chemin, lignes, new System.Text.UTF8Encoding(false));
        return retenus.Count;
    }

    /// <summary>Relit une liste. Sert au contrôle après écriture, et aux tests.</summary>
    public static List<string> Lire(string chemin) =>
        File.Exists(chemin) ? Analyser(File.ReadAllLines(chemin)) : [];

    /// <summary>
    /// La lecture du format, sans fichier. « # » commence un commentaire, où qu'il
    /// soit sur la ligne ; le reste est un nom.
    /// </summary>
    public static List<string> Analyser(IEnumerable<string> lignes)
    {
        var noms = new List<string>();
        foreach (var ligne in lignes)
        {
            var sansCommentaire = (ligne ?? "").Split('#')[0];
            var nom = ParkInventory.NomWindows(sansCommentaire);
            if (nom.Length > 0 && EstNomValide(nom)) noms.Add(nom);
        }
        return noms.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static List<string> Retenir(IEnumerable<string> noms, List<string>? ecartes)
    {
        var retenus = new List<string>();
        var vus = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var brut in noms ?? [])
        {
            // Une entrée vide n'est pas un poste perdu : rien à signaler.
            if (string.IsNullOrWhiteSpace(brut)) continue;

            var nom = ParkInventory.NomWindows(brut);

            if (nom.Length == 0 || !EstNomValide(nom))
            {
                // On nomme ce qu'on écarte. Une liste silencieusement raccourcie
                // ferait croire à un déploiement complet.
                ecartes?.Add((brut ?? "").Trim());
                continue;
            }

            if (vus.Add(nom)) retenus.Add(nom);
        }

        return retenus;
    }
}
