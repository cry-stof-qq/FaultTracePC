using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FaultTracePC.Core;

/// <summary>
/// POINT 64, LOT A — la liste des postes, en lecture pure.
///
/// Trois sources, et elles n'ont pas le même statut. La confusion entre les deux
/// premières est le piège de ce lot, et l'en-tête du script de déploiement le dit
/// déjà en majuscules :
///
///   « ON N'INSTALLE QUE SUR CE QU'ON NOMME. postes.csv n'est PAS une liste de
///     cibles : c'est un annuaire d'adresses MAC […] Il peut donc contenir sans
///     risque les téléphones et tablettes rendus par le DHCP. »
///
/// D'où la règle retenue le 18/09/2026 :
///
///   - l'ACTIVE DIRECTORY et PARC.JSON créent des postes ;
///   - POSTES.CSV n'en crée aucun : il ajoute une adresse MAC à un poste déjà
///     listé, et rien de plus. Un téléphone présent dans le CSV n'apparaît donc
///     nulle part, sans qu'aucun filtre ait à le reconnaître comme téléphone.
///
/// Rien ici ne contacte un poste ni ne modifie quoi que ce soit : on lit des
/// fichiers, on fusionne, on rend une liste.
/// </summary>
public static class ParkInventory
{
    /// <summary>Port d'écoute du mode parc, quand aucune source ne le précise.</summary>
    public const int PortParDefaut = 58620;

    /// <summary>Nom du fichier d'adresses MAC, écrit par le script de déploiement.</summary>
    public const string NomAnnuaireMac = "postes.csv";

    /// <summary>
    /// Où lire l'annuaire d'adresses MAC.
    ///
    /// POURQUOI CE RÉGLAGE EXISTE
    /// Le script de déploiement écrit <c>postes.csv</c> À CÔTÉ DE LUI : il est fait
    /// pour tourner depuis une clé USB, et se trouver tout seul. La console, elle,
    /// lit ses données dans <c>Documents\FaultTracePC</c>. Sans ce réglage, les deux
    /// fichiers ne se voient jamais et la colonne des adresses MAC reste vide sans
    /// qu'on sache pourquoi — constaté le 19/09/2026 sur un parc réel.
    ///
    /// UN DOSSIER EST ACCEPTÉ AUTANT QU'UN FICHIER. Coller le chemin du dossier où
    /// tourne le script est le geste naturel ; le refuser n'aiderait personne, alors
    /// que compléter par le nom du fichier coûte une ligne. Les guillemets d'un
    /// « Copier en tant que chemin d'accès » de l'Explorateur sont retirés pour la
    /// même raison.
    /// </summary>
    public static string CheminAdressesMac(string? reglage, string dossierParDefaut)
    {
        var chemin = (reglage ?? "").Trim().Trim('"').Trim();
        if (chemin.Length == 0) return Path.Combine(dossierParDefaut, NomAnnuaireMac);
        if (Directory.Exists(chemin)) return Path.Combine(chemin, NomAnnuaireMac);
        return chemin;
    }

    // ------------------------------------------------------------------
    // Nom Windows : la clé de tout
    // ------------------------------------------------------------------

    /// <summary>
    /// Ramène au nom Windows court, en majuscules — la seule forme sous laquelle
    /// les trois sources peuvent se reconnaître.
    ///
    /// L'Active Directory rend le nom de trois façons selon l'attribut interrogé :
    /// « POSTE-01 » (cn), « POSTE-01$ » (sAMAccountName d'un compte d'ordinateur) et
    /// « poste-01.domaine.fr » (dNSHostName). Le DHCP écrit la forme longue dans
    /// postes.csv. Sans cette normalisation, le même poste apparaîtrait trois fois.
    ///
    /// La casse ne compte pas : les noms Windows ne sont pas sensibles à la casse,
    /// et un « Poste-01 » saisi à la main dans la console doit rejoindre le
    /// « POSTE-01 » de l'annuaire, pas ouvrir une deuxième ligne.
    /// </summary>
    public static string NomWindows(string? brut)
    {
        var n = (brut ?? "").Trim();
        if (n.Length == 0) return "";

        // Le « $ » final d'un compte d'ordinateur ne fait pas partie du nom.
        if (n.EndsWith('$')) n = n[..^1];

        // Forme longue : on ne garde que ce qui précède le premier point.
        var point = n.IndexOf('.');
        if (point > 0) n = n[..point];

        return n.ToUpperInvariant();
    }

    // ------------------------------------------------------------------
    // Fusion
    // ------------------------------------------------------------------

    /// <summary>
    /// Fusionne les postes des sources qui en créent, puis leur attache les adresses
    /// MAC connues. Dédoublonnage par nom Windows, tri par nom.
    ///
    /// Quelle source gagne, champ par champ :
    ///   - l'adresse (<c>Host</c>) et le port viennent de la CONSOLE quand elle en a
    ///     un, parce qu'ils ont été saisis exprès — souvent une adresse IP fixe que
    ///     le DNS ne rendrait pas ;
    ///   - l'unité d'organisation et la date de dernière ouverture ne peuvent venir
    ///     que de l'ANNUAIRE ;
    ///   - un poste désactivé dans l'annuaire le reste, quoi qu'en dise la console.
    /// </summary>
    public static List<PosteDuParc> Fusionner(
        IEnumerable<PosteDuParc>? annuaire,
        IEnumerable<PosteDuParc>? console,
        IReadOnlyDictionary<string, string>? adressesMac = null)
    {
        var parNom = new Dictionary<string, PosteDuParc>(StringComparer.OrdinalIgnoreCase);

        void Absorber(IEnumerable<PosteDuParc>? source)
        {
            foreach (var p in source ?? [])
            {
                var nom = NomWindows(p.Name);
                // Un poste sans nom n'est pas un poste : le nom est ce dont le jeton
                // du mode parc se déduit, et ce que le déploiement va nommer.
                if (nom.Length == 0) continue;

                if (!parNom.TryGetValue(nom, out var existant))
                {
                    parNom[nom] = new PosteDuParc
                    {
                        Name = nom,
                        Sources = p.Sources,
                        Host = p.Host.Trim(),
                        Port = p.Port > 0 ? p.Port : PortParDefaut,
                        OrganizationalUnit = p.OrganizationalUnit,
                        LastLogon = p.LastLogon,
                        Disabled = p.Disabled,
                    };
                    continue;
                }

                existant.Sources |= p.Sources;

                // La console a saisi une adresse : elle passe devant celle de l'annuaire.
                if (p.Sources.HasFlag(SourcesDuPoste.Console) && p.Host.Trim().Length > 0)
                {
                    existant.Host = p.Host.Trim();
                    if (p.Port > 0) existant.Port = p.Port;
                }
                else if (existant.Host.Length == 0)
                {
                    existant.Host = p.Host.Trim();
                }

                if (existant.OrganizationalUnit.Length == 0) existant.OrganizationalUnit = p.OrganizationalUnit;
                if (p.LastLogon is { } d && (existant.LastLogon is null || d > existant.LastLogon)) existant.LastLogon = d;
                // Désactivé dans l'annuaire : l'information ne se perd jamais.
                existant.Disabled |= p.Disabled;
            }
        }

        // La console en premier pour que son adresse soit celle du poste créé ;
        // l'absorption ci-dessus la protège de toute façon si l'ordre change.
        Absorber(console);
        Absorber(annuaire);

        if (adressesMac is not null)
            foreach (var (nom, poste) in parNom)
                if (adressesMac.TryGetValue(nom, out var mac) && mac.Trim().Length > 0)
                    poste.Mac = mac.Trim();

        return parNom.Values.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    // ------------------------------------------------------------------
    // postes.csv — un annuaire d'adresses MAC, rien d'autre
    // ------------------------------------------------------------------

    /// <summary>
    /// Lit l'annuaire d'adresses MAC écrit par le script de déploiement :
    /// séparateur « ; », colonnes <c>Nom</c> et <c>MAC</c>, en-tête sur la première
    /// ligne. La casse des en-têtes et l'ordre des colonnes ne comptent pas — le
    /// fichier est relu par des mains humaines entre deux exécutions.
    ///
    /// Le résultat est indexé par nom Windows normalisé. Un fichier absent rend un
    /// dictionnaire vide sans rien signaler : ce fichier est facultatif, son absence
    /// n'est pas un incident.
    /// </summary>
    public static Dictionary<string, string> LireAnnuaireMac(string chemin, List<string>? notes = null)
    {
        var macs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(chemin) || !File.Exists(chemin)) return macs;

        try
        {
            var lignes = File.ReadAllLines(chemin, Encoding.UTF8);
            if (lignes.Length < 2) return macs;

            var entetes = DecouperCsv(lignes[0]);
            int iNom = IndexDe(entetes, "nom", "name", "hostname");
            int iMac = IndexDe(entetes, "mac", "adresse mac", "macaddress");
            if (iNom < 0 || iMac < 0)
            {
                notes?.Add(Lang.T(
                    $"Annuaire d'adresses MAC ignoré : les colonnes « Nom » et « MAC » n'y sont pas ({Path.GetFileName(chemin)}).",
                    $"MAC address directory ignored: it has no “Nom” and “MAC” columns ({Path.GetFileName(chemin)})."));
                return macs;
            }

            foreach (var ligne in lignes.Skip(1))
            {
                if (ligne.Trim().Length == 0) continue;
                var champs = DecouperCsv(ligne);
                if (champs.Count <= iNom || champs.Count <= iMac) continue;

                var nom = NomWindows(champs[iNom]);
                var mac = champs[iMac].Trim();
                // Dernière ligne gagnante : le script réécrit la ligne d'un poste
                // quand le DHCP lui donne une adresse plus fraîche.
                if (nom.Length > 0 && mac.Length > 0) macs[nom] = mac;
            }
        }
        catch (Exception ex)
        {
            notes?.Add(Lang.T($"Annuaire d'adresses MAC illisible : {ex.Message}",
                              $"MAC address directory could not be read: {ex.Message}"));
        }
        return macs;
    }

    private static int IndexDe(List<string> entetes, params string[] noms)
    {
        for (int i = 0; i < entetes.Count; i++)
        {
            var e = entetes[i].Trim().Trim('﻿');
            foreach (var n in noms)
                if (e.Equals(n, StringComparison.OrdinalIgnoreCase)) return i;
        }
        return -1;
    }

    /// <summary>
    /// Découpe une ligne CSV à séparateur « ; », en respectant les guillemets —
    /// <c>Export-Csv</c> en met autour de tout champ contenant le séparateur, et un
    /// découpage naïf couperait le champ en deux.
    /// </summary>
    internal static List<string> DecouperCsv(string ligne)
    {
        var champs = new List<string>();
        var courant = new StringBuilder();
        bool entreGuillemets = false;

        for (int i = 0; i < ligne.Length; i++)
        {
            var c = ligne[i];
            if (entreGuillemets)
            {
                // Un guillemet doublé à l'intérieur d'un champ vaut un guillemet.
                if (c == '"' && i + 1 < ligne.Length && ligne[i + 1] == '"') { courant.Append('"'); i++; }
                else if (c == '"') entreGuillemets = false;
                else courant.Append(c);
            }
            else if (c == '"') entreGuillemets = true;
            else if (c == ';') { champs.Add(courant.ToString()); courant.Clear(); }
            else courant.Append(c);
        }
        champs.Add(courant.ToString());
        return champs;
    }

    // ------------------------------------------------------------------
    // parc.json — les postes que la console suit déjà
    // ------------------------------------------------------------------

    /// <summary>
    /// Forme persistée d'un poste dans <c>Documents\FaultTracePC\parc.json</c>.
    ///
    /// LE CHAMP « TOKEN » N'EST PAS REPRIS. Le fichier peut encore en contenir un
    /// pour les postes configurés avant le secret maître ; il n'a rien à faire dans
    /// une liste destinée à être affichée, exportée ou journalisée. Ce qui n'est pas
    /// lu ne peut pas fuir.
    /// </summary>
    private sealed class PosteJson
    {
        [JsonPropertyName("Name")] public string Name { get; set; } = "";
        [JsonPropertyName("Host")] public string Host { get; set; } = "";
        [JsonPropertyName("Port")] public int Port { get; set; }
    }

    /// <summary>
    /// Lit la liste de la console. Fichier absent : liste vide, sans commentaire —
    /// c'est l'état d'une console qui n'a encore rien suivi.
    /// </summary>
    public static List<PosteDuParc> LireParcJson(string chemin, List<string>? notes = null)
    {
        var postes = new List<PosteDuParc>();
        if (string.IsNullOrWhiteSpace(chemin) || !File.Exists(chemin)) return postes;

        try
        {
            var lus = JsonSerializer.Deserialize<List<PosteJson>>(File.ReadAllText(chemin));
            foreach (var p in lus ?? [])
            {
                var nom = NomWindows(p.Name);
                if (nom.Length == 0) continue;
                postes.Add(new PosteDuParc
                {
                    Name = nom,
                    Sources = SourcesDuPoste.Console,
                    Host = (p.Host ?? "").Trim(),
                    Port = p.Port > 0 ? p.Port : PortParDefaut,
                });
            }
        }
        catch (Exception ex)
        {
            // Un JSON abîmé ne doit pas vider la liste en silence : l'Active Directory
            // reste exploitable, et l'utilisateur doit savoir ce qui manque.
            notes?.Add(Lang.T($"Liste de la console illisible : {ex.Message}",
                              $"Console list could not be read: {ex.Message}"));
        }
        return postes;
    }
}
