using System.DirectoryServices;

namespace FaultTracePC.Core;

/// <summary>
/// POINT 64, LOT A-2 — les postes lus dans l'Active Directory.
///
/// AUCUN MOT DE PASSE, NULLE PART
/// La requête part avec le ticket Kerberos du compte qui a lancé le logiciel.
/// C'est la décision du 17/09/2026, et elle vaut ici comme ailleurs : ce logiciel
/// ne demande pas de mot de passe, n'en stocke pas, n'en journalise pas.
///
/// AUCUN OUTIL À INSTALLER
/// <see cref="DirectorySearcher"/> parle LDAP directement. Ni RSAT, ni module
/// PowerShell ActiveDirectory : un poste du domaine suffit.
///
/// LECTURE SEULE
/// Une seule requête, et rien n'est écrit dans l'annuaire.
/// </summary>
public static class ParkDirectory
{
    /// <summary>Bit « compte désactivé » de userAccountControl (ADS_UF_ACCOUNTDISABLE).</summary>
    internal const int CompteDesactive = 0x2;

    /// <summary>
    /// Ne remonte que les comptes d'ORDINATEUR. Les deux conditions sont
    /// redondantes dans un annuaire sain, et c'est voulu : la première s'appuie sur
    /// l'index, la seconde protège d'un schéma modifié.
    /// </summary>
    internal const string FiltreOrdinateurs = "(&(objectCategory=computer)(objectClass=computer))";

    /// <summary>
    /// Interroge l'annuaire et rend les comptes d'ordinateur trouvés.
    ///
    /// <paramref name="uniteOrganisation"/> vide : la recherche part de la racine du
    /// domaine, découverte toute seule. Sinon, du chemin donné — par exemple
    /// <c>OU=Postes,DC=exemple,DC=fr</c>.
    ///
    /// Un échec ne lève pas : il remplit <paramref name="notes"/> et rend une liste
    /// vide. Un poste hors domaine, un annuaire injoignable ou une unité
    /// d'organisation mal saisie sont des situations normales, pas des incidents.
    /// </summary>
    public static List<PosteDuParc> Interroger(string uniteOrganisation, List<string>? notes = null)
    {
        var postes = new List<PosteDuParc>();

        try
        {
            using var racine = new DirectoryEntry(CheminLdap(uniteOrganisation));

            using var chercheur = new DirectorySearcher(racine)
            {
                Filter = FiltreOrdinateurs,
                SearchScope = SearchScope.Subtree,

                // SANS CETTE LIGNE, L'ANNUAIRE S'ARRÊTE À 1000 RÉSULTATS, EN SILENCE.
                // C'est la limite serveur par défaut d'un contrôleur de domaine : elle
                // ne produit aucune erreur, la liste est simplement tronquée. Sur un
                // parc de plus de mille postes, il manquerait des machines sans que
                // rien ne le signale — exactement le genre de silence que ce logiciel
                // combat ailleurs.
                PageSize = 1000,
            };

            chercheur.PropertiesToLoad.AddRange(
                ["cn", "dNSHostName", "userAccountControl", "lastLogonTimestamp", "distinguishedName"]);

            using var resultats = chercheur.FindAll();

            foreach (SearchResult r in resultats)
            {
                var nom = ParkInventory.NomWindows(Premiere(r, "cn"));
                if (nom.Length == 0) continue;

                postes.Add(new PosteDuParc
                {
                    Name = nom,
                    Sources = SourcesDuPoste.ActiveDirectory,
                    Host = (Premiere(r, "dNSHostName") ?? "").Trim(),
                    OrganizationalUnit = UniteLisible(Premiere(r, "distinguishedName")),
                    Disabled = EstDesactive(Entier(r, "userAccountControl")),
                    LastLogon = DateDeConnexion(Entier64(r, "lastLogonTimestamp")),
                });
            }
        }
        catch (Exception ex)
        {
            // Le message de l'annuaire est plus utile que n'importe quelle reformulation :
            // « serveur introuvable », « nom distinctif non valide », « accès refusé »
            // désignent chacun une cause différente et une correction différente.
            notes?.Add(Lang.T($"Active Directory : {ex.Message}", $"Active Directory: {ex.Message}"));
        }

        return postes.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>Une unité d'organisation telle qu'on la propose à l'utilisateur.</summary>
    public sealed class UniteOrganisation
    {
        /// <summary>Nom distinctif complet, à recopier tel quel dans le réglage.</summary>
        public string NomDistinctif { get; set; } = "";

        /// <summary>Chemin lisible, « Postes / Salle 12 ».</summary>
        public string Libelle { get; set; } = "";

        public override string ToString() => Libelle.Length > 0 ? Libelle : NomDistinctif;
    }

    /// <summary>
    /// Liste les unités d'organisation du domaine, pour que l'utilisateur CHOISISSE
    /// au lieu de SAISIR.
    ///
    /// POURQUOI CETTE MÉTHODE EXISTE
    /// Un nom distinctif se tape mal : il faut la racine du domaine en entier, et
    /// toute virgule contenue dans un nom doit être échappée. Une faute de frappe
    /// ne produit pas un message clair mais une erreur LDAP, ou pire, une liste
    /// vide sans explication. Lire les noms distinctifs tels que l'annuaire les
    /// stocke supprime le problème à la source — ils sont déjà échappés.
    ///
    /// Lecture seule, ticket Kerberos du compte courant, comme le reste.
    /// </summary>
    public static List<UniteOrganisation> ListerUnites(string? depuis = null, List<string>? notes = null)
    {
        var unites = new List<UniteOrganisation>();

        try
        {
            using var racine = new DirectoryEntry(CheminLdap(depuis));
            using var chercheur = new DirectorySearcher(racine)
            {
                Filter = "(objectClass=organizationalUnit)",
                SearchScope = SearchScope.Subtree,
                PageSize = 1000,
            };
            chercheur.PropertiesToLoad.Add("distinguishedName");

            using var resultats = chercheur.FindAll();
            foreach (SearchResult r in resultats)
            {
                var dn = (Premiere(r, "distinguishedName") ?? "").Trim();
                if (dn.Length == 0) continue;
                unites.Add(new UniteOrganisation { NomDistinctif = dn, Libelle = UniteLisible(dn) });
            }
        }
        catch (Exception ex)
        {
            notes?.Add(Lang.T($"Active Directory : {ex.Message}", $"Active Directory: {ex.Message}"));
        }

        return unites.OrderBy(u => u.Libelle, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    // ------------------------------------------------------------------
    // Les morceaux vérifiables sans annuaire
    // ------------------------------------------------------------------

    /// <summary>
    /// Chemin LDAP à interroger. Vide : <c>LDAP://</c> tout court, ce qui laisse
    /// Windows résoudre la racine du domaine auquel le poste appartient — c'est ce
    /// qui évite d'avoir à saisir quoi que ce soit dans le cas courant.
    /// </summary>
    internal static string CheminLdap(string? uniteOrganisation)
    {
        var ou = (uniteOrganisation ?? "").Trim();
        if (ou.Length == 0) return "LDAP://";
        // On accepte les deux écritures : avec ou sans le préfixe.
        return ou.StartsWith("LDAP://", StringComparison.OrdinalIgnoreCase) ? ou : "LDAP://" + ou;
    }

    internal static bool EstDesactive(int userAccountControl) => (userAccountControl & CompteDesactive) != 0;

    /// <summary>
    /// Date de dernière ouverture de session, à partir de <c>lastLogonTimestamp</c>.
    ///
    /// CETTE DATE EST APPROXIMATIVE PAR CONSTRUCTION, et il faut le savoir avant de
    /// s'en servir : l'attribut n'est répliqué entre contrôleurs de domaine qu'au
    /// bout de plusieurs jours — neuf à quatorze avec les réglages d'origine. Elle
    /// répond à « ce poste a-t-il été vu ce mois-ci », jamais à « ce poste est-il
    /// allumé ». Zéro et les valeurs aberrantes rendent « inconnue » plutôt qu'une
    /// date de 1601.
    /// </summary>
    internal static DateTime? DateDeConnexion(long lastLogonTimestamp)
    {
        if (lastLogonTimestamp <= 0) return null;
        try { return DateTime.FromFileTimeUtc(lastLogonTimestamp).ToLocalTime(); }
        catch (ArgumentOutOfRangeException) { return null; }
    }

    /// <summary>
    /// Unité d'organisation en clair, tirée du nom distinctif.
    ///
    /// « CN=POSTE-01,OU=Salle 12,OU=Postes,DC=exemple,DC=fr » donne
    /// « Postes / Salle 12 » — du plus général au plus précis, comme on lit une
    /// arborescence, alors que le nom distinctif l'écrit à l'envers.
    /// </summary>
    internal static string UniteLisible(string? nomDistinctif)
    {
        var dn = (nomDistinctif ?? "").Trim();
        if (dn.Length == 0) return "";

        var unites = new List<string>();
        foreach (var morceau in DecouperNomDistinctif(dn))
        {
            var m = morceau.Trim();
            if (m.StartsWith("OU=", StringComparison.OrdinalIgnoreCase))
                unites.Add(m[3..].Replace("\\,", ",").Trim());
        }

        unites.Reverse();
        return string.Join(" / ", unites);
    }

    /// <summary>
    /// Découpe un nom distinctif sur les virgules, en respectant l'échappement
    /// « \, » — une unité d'organisation peut légitimement contenir une virgule, et
    /// un découpage naïf la couperait en deux.
    /// </summary>
    internal static List<string> DecouperNomDistinctif(string dn)
    {
        var morceaux = new List<string>();
        var courant = new System.Text.StringBuilder();
        for (int i = 0; i < dn.Length; i++)
        {
            if (dn[i] == '\\' && i + 1 < dn.Length) { courant.Append(dn[i]).Append(dn[i + 1]); i++; }
            else if (dn[i] == ',') { morceaux.Add(courant.ToString()); courant.Clear(); }
            else courant.Append(dn[i]);
        }
        morceaux.Add(courant.ToString());
        return morceaux;
    }

    /// <summary>
    /// Vérifie une unité d'organisation saisie à la main, AVANT d'aller déranger
    /// l'annuaire. Rend une chaîne vide si la saisie est exploitable, sinon la
    /// phrase à montrer à l'utilisateur.
    ///
    /// POURQUOI UN CONTRÔLE ICI PLUTÔT QUE LE MESSAGE DE LDAP
    /// Une virgule non échappée ne produit pas « virgule non échappée » : elle
    /// produit « le nom distinctif comporte une syntaxe non valide », ou une liste
    /// vide sans erreur du tout. Le contrôle ci-dessous nomme la faute ET la
    /// correction, ce que l'annuaire ne fera jamais.
    ///
    /// Une saisie VIDE est valide : elle veut dire « la racine du domaine ».
    /// </summary>
    public static string VerifierUnite(string? saisie)
    {
        var ou = (saisie ?? "").Trim();
        if (ou.Length == 0) return "";

        if (ou.StartsWith("LDAP://", StringComparison.OrdinalIgnoreCase)) ou = ou[7..].Trim();
        if (ou.Length == 0) return "";

        var problemes = new List<string>();
        var morceaux = DecouperNomDistinctif(ou);

        // Chaque morceau d'un nom distinctif s'écrit « attribut=valeur ». Un morceau
        // qui n'a pas cette forme est presque toujours la seconde moitié d'une valeur
        // coupée par une virgule qu'on a oublié d'échapper.
        bool malForme = morceaux.Any(m => !EstMorceauValide(m));
        if (malForme)
            problemes.Add(Lang.T(
                "Une virgule de ce chemin n'est pas échappée : dans un nom d'unité, une virgule s'écrit « \\, ». Exemple : OU=Ecole primaire\\, batiment B,DC=exemple,DC=fr.",
                "A comma in this path is not escaped: inside a unit name, a comma is written “\\,”. For example: OU=Primary school\\, building B,DC=example,DC=com."));

        // Sans composant DC=, le chemin ne désigne pas de domaine : l'annuaire ne
        // saura pas où chercher.
        bool racineAbsente = !morceaux.Any(m => m.Trim().StartsWith("DC=", StringComparison.OrdinalIgnoreCase));
        if (racineAbsente)
            problemes.Add(Lang.T(
                "Ce chemin est incomplet : il lui manque la racine du domaine. Un chemin complet se termine par les composants « DC= », par exemple OU=Postes,DC=exemple,DC=fr.",
                "This path is incomplete: the domain root is missing. A complete path ends with the “DC=” components, for example OU=Computers,DC=example,DC=com."));

        if (problemes.Count == 0) return "";

        return string.Join(" ", problemes)
               + Lang.T(" Le plus sûr reste de choisir dans la liste plutôt que de saisir ce chemin.",
                        " The safest option is still to pick from the list rather than typing this path.");
    }

    /// <summary>Un morceau de nom distinctif a la forme « attribut=valeur ».</summary>
    internal static bool EstMorceauValide(string morceau)
    {
        var m = (morceau ?? "").Trim();
        var egal = m.IndexOf('=');
        if (egal <= 0 || egal == m.Length - 1) return false;

        var attribut = m[..egal];
        if (!char.IsLetter(attribut[0])) return false;
        return attribut.All(c => char.IsLetterOrDigit(c) || c == '-');
    }

    private static string? Premiere(SearchResult r, string propriete) =>
        r.Properties.Contains(propriete) && r.Properties[propriete].Count > 0
            ? r.Properties[propriete][0]?.ToString()
            : null;

    private static int Entier(SearchResult r, string propriete) =>
        int.TryParse(Premiere(r, propriete), out var v) ? v : 0;

    private static long Entier64(SearchResult r, string propriete) =>
        long.TryParse(Premiere(r, propriete), out var v) ? v : 0;
}
