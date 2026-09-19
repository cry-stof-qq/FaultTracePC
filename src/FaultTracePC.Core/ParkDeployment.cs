using System.Text.Json;

namespace FaultTracePC.Core;

/// <summary>
/// POINT 64, LOT B — le canal entre le script de déploiement et la console.
///
/// POURQUOI UN FICHIER, ET PAS LA SORTIE STANDARD
/// Décision du 17/09/2026. PowerShell 5.1 décode la sortie standard avec la page
/// de code de la console — 850 ou 1252 selon la machine — et un « é » en ressort
/// en charabia. Le script écrit donc ses lignes dans un FICHIER en UTF-8, que la
/// console relit. C'est aussi ce qui permet de relire un déploiement après coup,
/// et de reprendre les seuls postes en échec (lot D).
///
/// POURQUOI DES CODES ET PAS DES PHRASES
/// Le script parle français ; la console parle deux langues. Les champs
/// <c>etape</c> et <c>etat</c> sont donc des codes invariants, et c'est la console
/// qui fabrique la phrase. Le champ <c>detail</c> reste le texte brut du script :
/// il est affiché tel quel, jamais traduit, et jamais interprété.
///
/// CE QUE CETTE LECTURE NE FAIT PAS
/// Elle ne juge pas, ne recompose rien et n'invente aucune ligne. Une ligne
/// illisible est COMPTÉE, pas effacée : un déploiement dont trois lignes sur
/// quarante n'ont pas pu être lues n'est pas un déploiement de trente-sept postes,
/// et personne ne doit pouvoir le confondre avec ça.
/// </summary>
public static class ParkDeployment
{
    // ------------------------------------------------------------------
    // Le vocabulaire du canal. Codes invariants, écrits par le script.
    // ------------------------------------------------------------------

    /// <summary>Le nom correspond à un compte d'ordinateur du domaine.</summary>
    public const string EtapeCompte = "compte";
    /// <summary>Le poste répond — ping ou port 445.</summary>
    public const string EtapeReponse = "reponse";
    /// <summary>Le partage administratif répond — un simple test de chemin.</summary>
    public const string EtapePartageAdmin = "partage";
    /// <summary>La gestion à distance répond (WinRM, 5985) : c'est elle qui installe.</summary>
    public const string EtapeGestionADistance = "winrm";
    /// <summary>Réveil réseau tenté.</summary>
    public const string EtapeReveil = "reveil";
    /// <summary>Copie du paquet sur le poste.</summary>
    public const string EtapeCopie = "copie";
    /// <summary>Installation du paquet.</summary>
    public const string EtapeInstallation = "installation";
    /// <summary>Bascule en mode parc.</summary>
    public const string EtapeParc = "parc";

    public const string EtatOk = "ok";
    public const string EtatEchec = "echec";
    /// <summary>Étape volontairement sautée — ce n'est ni une réussite ni un échec.</summary>
    public const string EtatIgnore = "ignore";
    /// <summary>Renseignement, sans verdict.</summary>
    public const string EtatInfo = "info";

    /// <summary>
    /// LES ÉTAPES DU MODE « VÉRIFIER SEULEMENT ». Aucune n'écrit quoi que ce soit
    /// sur le poste distant : c'est ce qui rend ce mode sûr par construction, et
    /// pas seulement par intention. Le lot C n'y touchera pas — il en ajoutera
    /// d'autres à côté.
    /// </summary>
    public static readonly string[] EtapesInoffensives =
        [EtapeCompte, EtapeReponse, EtapePartageAdmin, EtapeGestionADistance];

    public static bool EstInoffensive(string? etape) =>
        EtapesInoffensives.Contains((etape ?? "").Trim(), StringComparer.OrdinalIgnoreCase);

    // ------------------------------------------------------------------
    // Le paquet à déployer
    // ------------------------------------------------------------------

    /// <summary>
    /// Ce qui cloche avec le paquet indiqué, ou une chaîne vide si tout va bien.
    ///
    /// CONTRÔLÉ ICI, ET PAS SUR LE POSTE DISTANT. Un chemin fautif découvert au
    /// milieu d'un lot de cinquante machines laisse la moitié du parc dans un état
    /// et l'autre moitié dans un autre. Autant s'en apercevoir avant le premier
    /// octet copié.
    /// </summary>
    public static string VerifierLePaquet(string? chemin)
    {
        var c = (chemin ?? "").Trim().Trim('"').Trim();

        if (c.Length == 0)
            return Lang.T("Aucun paquet indiqué : renseigner le chemin du fichier .msi de FaultTracePC.",
                          "No package given: set the path to the FaultTracePC .msi file.");

        if (!c.EndsWith(".msi", StringComparison.OrdinalIgnoreCase))
            return Lang.T($"Ce n'est pas un paquet d'installation : {c} ne se termine pas par .msi.",
                          $"This is not an installer package: {c} does not end with .msi.");

        try
        {
            if (!File.Exists(c))
                return Lang.T($"Paquet introuvable : {c}. Vérifier le partage et les droits de lecture.",
                              $"Package not found: {c}. Check the share and the read permissions.");
        }
        catch (Exception ex)
        {
            // Un chemin réseau injoignable lève au lieu de rendre « false » : le dire
            // vaut mieux que de le traduire en « introuvable », qui enverrait
            // chercher au mauvais endroit.
            return Lang.T($"Paquet illisible : {ex.Message}", $"Package unreadable: {ex.Message}");
        }

        return "";
    }

    /// <summary>
    /// Numéro de version lu DANS LE NOM DU FICHIER — « FaultTracePC-1.6.2.msi »
    /// donne « 1.6.2 ». Rend une chaîne vide si le nom ne suit pas cette forme.
    ///
    /// CE N'EST PAS LA VERSION DU PAQUET, c'est celle que son nom annonce. Un
    /// fichier renommé mentirait, et c'est pourquoi tout ce qui s'appuie là-dessus
    /// doit le présenter comme une indication, jamais comme un fait établi.
    /// </summary>
    public static string VersionAnnonceeParLeNom(string? chemin)
    {
        try
        {
            var nom = Path.GetFileNameWithoutExtension((chemin ?? "").Trim().Trim('"').Trim());
            var m = System.Text.RegularExpressions.Regex.Match(nom, @"^FaultTracePC-(\d+\.\d+\.\d+)$");
            return m.Success ? m.Groups[1].Value : "";
        }
        catch { return ""; }
    }

    // ------------------------------------------------------------------
    // Lecture
    // ------------------------------------------------------------------

    /// <summary>
    /// Relit le journal écrit par le script. Fichier absent : ce n'est pas une
    /// erreur, c'est un déploiement qui n'a pas encore commencé — et les deux se
    /// disent différemment.
    /// </summary>
    public static LectureDuJournal Lire(string chemin)
    {
        if (string.IsNullOrWhiteSpace(chemin) || !File.Exists(chemin))
            return new LectureDuJournal { FichierAbsent = true };

        try
        {
            // Lecture par défaut : l'encodage se déduit de la marque d'ordre des
            // octets si elle est là, UTF-8 sinon. C'est ce que le script écrit.
            return LireLignes(File.ReadAllLines(chemin));
        }
        catch (Exception ex)
        {
            var lecture = new LectureDuJournal();
            lecture.Notes.Add(Lang.T($"Journal de déploiement illisible : {ex.Message}",
                                     $"Deployment journal could not be read: {ex.Message}"));
            return lecture;
        }
    }

    /// <summary>
    /// Le cœur de la lecture, sans fichier — c'est cette forme-là qui se teste.
    /// </summary>
    public static LectureDuJournal LireLignes(IEnumerable<string> lignes)
    {
        var lecture = new LectureDuJournal();
        var toutes = lignes.ToList();

        for (int i = 0; i < toutes.Count; i++)
        {
            var brute = (toutes[i] ?? "").Trim();
            if (brute.Length == 0) continue;   // une ligne vide n'est pas une ligne

            var ligne = Analyser(brute, out var jsonInvalide);
            if (ligne is not null) { lecture.Lignes.Add(ligne); continue; }

            // UNE DERNIÈRE LIGNE TRONQUÉE N'EST PAS UNE ERREUR. Le script écrit
            // pendant qu'on relit : la dernière ligne peut être à moitié écrite.
            // La compter comme illisible ferait croire à un défaut là où il n'y a
            // qu'une lecture prise en cours de route.
            //
            // MAIS SEULEMENT SI LE JSON EST INVALIDE. Une ligne de JSON PARFAITEMENT
            // FORMÉE qui ne nomme aucun poste n'est pas une écriture en cours : c'est
            // un défaut du journal, et il reste un défaut même en dernière position.
            // Sans cette distinction, un journal d'une seule ligne fautive se lisait
            // « lecture prise en cours de route » — rassurant, et faux. Constaté par
            // le test le 19/09/2026, avant tout usage réel.
            bool derniere = toutes.Skip(i + 1).All(l => string.IsNullOrWhiteSpace(l));
            if (jsonInvalide && derniere) lecture.DerniereLigneIncomplete = true;
            else lecture.LignesIllisibles++;
        }

        if (lecture.LignesIllisibles > 0)
            lecture.Notes.Add(Lang.T(
                $"{lecture.LignesIllisibles} ligne(s) du journal n'ont pas pu être lues : le décompte ci-dessus est donc incomplet.",
                $"{lecture.LignesIllisibles} journal line(s) could not be read: the counts above are therefore incomplete."));

        return lecture;
    }

    /// <summary>
    /// Rend <c>null</c> quand la ligne n'est pas exploitable — JSON invalide, objet
    /// attendu, ou aucun poste nommé. Une ligne qui ne nomme pas de poste ne peut
    /// être rattachée à personne : la garder reviendrait à l'attribuer au hasard.
    ///
    /// <paramref name="jsonInvalide"/> sépare les deux familles d'échec, parce
    /// qu'elles ne veulent pas dire la même chose : un JSON incomplet peut être une
    /// écriture en cours, un JSON complet mais incohérent est un défaut.
    /// </summary>
    private static LigneDeDeploiement? Analyser(string brute, out bool jsonInvalide)
    {
        jsonInvalide = false;

        try
        {
            using var doc = JsonDocument.Parse(brute);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return null;

            // Insensible à la casse : PowerShell écrit « Poste » aussi volontiers
            // que « poste » selon la façon dont l'objet a été construit.
            var champs = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in doc.RootElement.EnumerateObject()) champs[p.Name] = p.Value.Clone();

            var poste = ParkInventory.NomWindows(Texte(champs, "poste"));
            if (poste.Length == 0) return null;

            return new LigneDeDeploiement
            {
                Poste = poste,
                Etape = Texte(champs, "etape"),
                Etat = Texte(champs, "etat"),
                Detail = Texte(champs, "detail"),
                Adresse = Texte(champs, "adresse"),
                Code = Entier(champs, "code"),
                Horodatage = Instant(Texte(champs, "horodatage")),
            };
        }
        catch (JsonException)
        {
            jsonInvalide = true;
            return null;
        }
    }

    private static string Texte(Dictionary<string, JsonElement> champs, string nom)
    {
        if (!champs.TryGetValue(nom, out var v)) return "";
        return v.ValueKind switch
        {
            JsonValueKind.String => v.GetString() ?? "",
            JsonValueKind.Number => v.GetRawText(),
            JsonValueKind.True or JsonValueKind.False => v.GetRawText(),
            _ => "",
        };
    }

    /// <summary>
    /// Un code de sortie écrit en texte reste un code de sortie. PowerShell écrit
    /// volontiers <c>"code":"1603"</c> quand la valeur a transité par une chaîne.
    /// </summary>
    private static int? Entier(Dictionary<string, JsonElement> champs, string nom)
    {
        if (!champs.TryGetValue(nom, out var v)) return null;
        if (v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var n)) return n;
        if (v.ValueKind == JsonValueKind.String && int.TryParse(v.GetString(), out var m)) return m;
        return null;
    }

    private static DateTimeOffset? Instant(string valeur) =>
        DateTimeOffset.TryParse(valeur, System.Globalization.CultureInfo.InvariantCulture,
                                System.Globalization.DateTimeStyles.RoundtripKind, out var d)
            ? d : null;
}

/// <summary>Une ligne du journal : un poste, une étape, un verdict.</summary>
public sealed class LigneDeDeploiement
{
    public string Poste { get; set; } = "";
    public string Etape { get; set; } = "";
    public string Etat { get; set; } = "";

    /// <summary>Texte brut du script, dans SA langue. Affiché tel quel, jamais traduit.</summary>
    public string Detail { get; set; } = "";

    /// <summary>Adresse par laquelle le script a réellement joint le poste, si elle est connue.</summary>
    public string Adresse { get; set; } = "";

    /// <summary>Code de sortie, quand l'étape en produit un.</summary>
    public int? Code { get; set; }

    public DateTimeOffset? Horodatage { get; set; }

    /// <summary>
    /// L'échec se lit sur l'ÉTAT, jamais sur le détail ni sur le code. Un code 3010
    /// veut dire « installé, redémarrage demandé » : chercher l'échec ailleurs que
    /// dans le champ prévu pour lui, c'est se tromper tôt ou tard.
    /// </summary>
    public bool EstEchec => string.Equals(Etat, ParkDeployment.EtatEchec, StringComparison.OrdinalIgnoreCase);
}

/// <summary>Ce qu'une relecture du journal a donné — y compris ce qu'elle n'a pas pu lire.</summary>
public sealed class LectureDuJournal
{
    public List<LigneDeDeploiement> Lignes { get; } = [];

    /// <summary>
    /// Lignes présentes mais inexploitables. <b>Jamais silencieuses</b> : c'est ce
    /// nombre qui empêche de prendre un décompte incomplet pour un décompte.
    /// </summary>
    public int LignesIllisibles { get; set; }

    /// <summary>
    /// La dernière ligne était à moitié écrite — lecture faite pendant que le script
    /// tourne. Ce n'est pas un défaut, et ça ne se compte pas comme tel.
    /// </summary>
    public bool DerniereLigneIncomplete { get; set; }

    /// <summary>Le fichier n'existe pas : déploiement pas encore commencé.</summary>
    public bool FichierAbsent { get; set; }

    public List<string> Notes { get; } = [];

    /// <summary>Les postes nommés par le journal, dans l'ordre alphabétique.</summary>
    public List<string> Postes => Lignes.Select(l => l.Poste)
                                        .Distinct(StringComparer.OrdinalIgnoreCase)
                                        .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                                        .ToList();

    /// <summary>Les postes dont au moins une étape a échoué — la liste du lot D.</summary>
    public List<string> PostesEnEchec => Lignes.Where(l => l.EstEchec)
                                               .Select(l => l.Poste)
                                               .Distinct(StringComparer.OrdinalIgnoreCase)
                                               .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                                               .ToList();
}
