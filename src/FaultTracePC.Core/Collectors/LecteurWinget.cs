using System.Text.RegularExpressions;

namespace FaultTracePC.Core.Collectors;

/// <summary>Une ligne du tableau de « winget upgrade » : un logiciel qui a une mise à jour.</summary>
/// <param name="Nom">Nom affiché par winget. Peut finir par « … » s'il a été coupé (voir <paramref name="NomCoupe"/>).</param>
/// <param name="Identifiant">
/// Identifiant winget (« Notepad++.Notepad++ »). <c>null</c> quand winget l'a coupé :
/// un identifiant coupé n'est JAMAIS deviné (plan du lot 1, point 4) — c'est lui qui
/// servira plus tard à lancer la mise à jour, et un identifiant deviné pourrait en
/// désigner un autre.
/// </param>
/// <param name="VersionInstallee">Version installée, telle qu'écrite par winget.</param>
/// <param name="VersionDisponible">Version proposée, telle qu'écrite par winget.</param>
/// <param name="Source">Source winget (« winget », « msstore »…).</param>
/// <param name="Tableau">
/// 0 pour le premier tableau de la sortie, 1 pour le suivant… winget peut afficher un
/// second tableau après le premier ; ce qu'il signifie n'est pas encore mesuré, la
/// présentation en décidera (lot 1c).
/// </param>
/// <param name="NomCoupe">Le nom finit par « … » : winget l'a raccourci pour tenir dans sa colonne.</param>
/// <param name="VersionCoupee">Une des deux versions finit par « … ».</param>
public sealed record MiseAJourWinget(
    string Nom,
    string? Identifiant,
    string VersionInstallee,
    string VersionDisponible,
    string Source,
    int Tableau,
    bool NomCoupe,
    bool VersionCoupee);

/// <summary>Ce que la lecture a pu établir. Aucun de ces états ne veut dire « tout est à jour ».</summary>
public enum EtatLectureWinget
{
    /// <summary>Au moins un tableau a été trouvé et lu.</summary>
    Lu,

    /// <summary>
    /// Aucun tableau dans la sortie. Ce peut être « aucune mise à jour », une source
    /// injoignable ou un message d'erreur : la lecture du texte NE TRANCHE PAS. C'est
    /// au lancement de winget (lot 1b), avec son code de sortie, de le dire.
    /// </summary>
    AucunTableau,

    /// <summary>
    /// Un tableau a été repéré, mais son en-tête n'a pas les cinq colonnes attendues :
    /// on refuse de le lire plutôt que de mal le lire (plan du lot 1, point 5).
    /// </summary>
    EnTeteInattendu,
}

/// <summary>Résultat de la lecture d'une sortie de « winget upgrade ».</summary>
/// <param name="Etat">Ce que la lecture a pu établir.</param>
/// <param name="MisesAJour">Les lignes lues, dans l'ordre de la sortie.</param>
/// <param name="LignesIllisibles">
/// Lignes du tableau qu'on n'a pas pu lire (colonnes décalées). Elles ne sont ni
/// devinées ni oubliées : leur nombre est rendu pour que le rapport le dise.
/// </param>
/// <param name="NombreAnnonce">
/// Le nombre que winget annonce sous son premier tableau (« 7 mises à jour
/// disponibles. »), quand la ligne commence par un nombre ; sinon <c>null</c>.
/// Sert à réconcilier les comptes (point 28) : « lu 7 sur 7 ».
/// </param>
/// <param name="EnTetesRefuses">Tableaux repérés mais refusés pour un en-tête inattendu.</param>
public sealed record LectureWinget(
    EtatLectureWinget Etat,
    IReadOnlyList<MiseAJourWinget> MisesAJour,
    int LignesIllisibles,
    int? NombreAnnonce,
    int EnTetesRefuses);

/// <summary>
/// POINT 15, LOT 1a de la 1.10.0 (plan validé par l'auteur le 26/10/2026).
/// Lit le tableau que « winget upgrade » écrit à l'écran. Fonction pure : elle ne
/// lance rien, elle reçoit le texte — c'est ce qui permet de la tester sur des sorties
/// réelles.
///
/// POURQUOI LIRE DU TEXTE
/// winget v1.29.380 (mesuré le 26/10/2026) n'offre toujours aucune sortie structurée
/// pour « upgrade » : ni JSON, ni format. Le module PowerShell Microsoft.WinGet.Client
/// rend des objets, mais il n'est pas présent d'office ; il est réservé au parc, sous
/// SYSTEM, dans un lot suivant.
///
/// POURQUOI AUX POSITIONS DE L'EN-TÊTE (point 24 de la feuille de route)
/// Découper sur « deux espaces ou plus » est faux : quand une valeur remplit sa
/// colonne, il ne reste qu'UN espace avant la suivante — 3 paquets sur 7 dans la
/// mesure du 19/09/2026. Les colonnes se prennent donc là où commence chaque mot de
/// l'en-tête (la ligne juste au-dessus des tirets). Les mots sont traduits
/// (« Nom ID Version Disponible Source » en français) : on ne les lit pas, on ne
/// regarde que leur position. La ligne de tirets, elle, est continue et ne donne
/// aucune position.
///
/// CE QUI EST VÉRIFIÉ SUR CHAQUE LIGNE
/// winget aligne chaque valeur à gauche de sa colonne et laisse au moins un espace
/// avant la suivante. Une ligne qui ne respecte pas ça est décalée — un caractère
/// large dans un nom, par exemple — et toutes ses valeurs sont douteuses : elle est
/// comptée illisible, jamais devinée.
///
/// CE QUI RESTE À MESURER (captures réelles demandées le 26/10/2026)
/// La sortie REDIRIGÉE (celle que FaultTracePC recevra) n'a pas encore été capturée :
/// présence de l'animation d'attente, largeur des colonnes sans console, encodage.
/// Les tests de ce lot sont construits selon la structure mesurée à l'écran.
/// </summary>
public static class LecteurWinget
{
    /// <summary>Le caractère « … » que winget met au bout d'une valeur raccourcie.</summary>
    internal const char PointsDeSuspension = '…';

    /// <summary>Nom, identifiant, version, version disponible, source.</summary>
    internal const int ColonnesAttendues = 5;

    /// <summary>En dessous, une suite de tirets n'est pas la ligne de séparation d'un tableau.</summary>
    private const int TiretsMinimum = 10;

    /// <summary>Séquences de commande du terminal (couleurs, curseur), au cas où winget en écrirait.</summary>
    private static readonly Regex SequenceTerminal = new(@"\x1B\[[0-9;?]*[A-Za-z]", RegexOptions.Compiled);

    private static readonly Regex NombreEnDebut = new(@"^(\d+)\s", RegexOptions.Compiled);

    public static LectureWinget Lire(string? sortie)
    {
        var lignes = Lignes(sortie);
        var misesAJour = new List<MiseAJourWinget>();
        int illisibles = 0, tableau = 0, refuses = 0;
        int? annonce = null;

        for (int i = 1; i < lignes.Count; i++)
        {
            if (!EstLigneDeTirets(lignes[i])) continue;

            var colonnes = DebutsDeColonnes(lignes[i - 1]);
            if (colonnes.Count != ColonnesAttendues)
            {
                refuses++;
                continue;
            }

            int j = i + 1;
            for (; j < lignes.Count; j++)
            {
                var ligne = lignes[j];
                // Fin du tableau : ligne vide, ou trop courte pour atteindre la
                // dernière colonne (« 7 mises à jour disponibles. »).
                if (ligne.Length <= colonnes[^1]) break;

                var lue = LireLigne(ligne, colonnes, tableau, out bool finDuTableau);
                if (finDuTableau) break;
                if (lue is null) illisibles++;
                else misesAJour.Add(lue);
            }

            if (tableau == 0) annonce = NombreSousLeTableau(lignes, j);
            tableau++;
            i = j - 1;
        }

        var etat = tableau > 0 ? EtatLectureWinget.Lu
                 : refuses > 0 ? EtatLectureWinget.EnTeteInattendu
                 : EtatLectureWinget.AucunTableau;
        return new LectureWinget(etat, misesAJour, illisibles, annonce, refuses);
    }

    /// <summary>
    /// Découpe la sortie en lignes telles qu'un terminal les afficherait : un retour
    /// chariot seul ramène au début de la ligne, et ce qui suit écrase ce qui précède.
    /// C'est ainsi que winget anime son attente (« - \ | / ») ; sans cette règle,
    /// l'animation collerait à l'en-tête et décalerait toutes les colonnes.
    /// </summary>
    internal static List<string> Lignes(string? sortie)
    {
        var lignes = new List<string>();
        if (string.IsNullOrEmpty(sortie)) return lignes;

        foreach (var brute in sortie.TrimStart('﻿').Split('\n'))
        {
            var ligne = brute.EndsWith('\r') ? brute[..^1] : brute;
            int dernier = ligne.LastIndexOf('\r');
            if (dernier >= 0) ligne = ligne[(dernier + 1)..];
            lignes.Add(SequenceTerminal.Replace(ligne, "").TrimEnd());
        }
        return lignes;
    }

    internal static bool EstLigneDeTirets(string ligne)
    {
        var l = ligne.Trim();
        return l.Length >= TiretsMinimum && l.All(c => c == '-');
    }

    /// <summary>Position du premier caractère de chaque mot de l'en-tête.</summary>
    internal static List<int> DebutsDeColonnes(string enTete)
    {
        var debuts = new List<int>();
        for (int k = 0; k < enTete.Length; k++)
            if (enTete[k] != ' ' && (k == 0 || enTete[k - 1] == ' '))
                debuts.Add(k);
        return debuts;
    }

    /// <summary>
    /// Lit une ligne du tableau. Rend <c>null</c> si elle est décalée (comptée
    /// illisible). <paramref name="finDuTableau"/> passe à vrai quand la « ligne » est
    /// en fait une phrase : la dernière colonne (la source) contient des espaces, ce
    /// qu'aucun nom de source n'a.
    /// </summary>
    private static MiseAJourWinget? LireLigne(string ligne, List<int> colonnes, int tableau, out bool finDuTableau)
    {
        finDuTableau = false;
        var cellules = new string[colonnes.Count];
        for (int c = 0; c < colonnes.Count; c++)
        {
            int debut = colonnes[c];
            int fin = c + 1 < colonnes.Count ? Math.Min(colonnes[c + 1], ligne.Length) : ligne.Length;
            cellules[c] = debut < fin ? ligne[debut..fin].Trim() : "";
        }

        var source = cellules[ColonnesAttendues - 1];
        if (source.Contains(' '))
        {
            finDuTableau = true;
            return null;
        }

        // Alignement : chaque valeur commence pile au début de sa colonne, et un espace
        // la sépare de la précédente.
        for (int c = 0; c < colonnes.Count; c++)
        {
            int debut = colonnes[c];
            if (ligne[debut] == ' ') return null;
            if (c > 0 && ligne[debut - 1] != ' ') return null;
        }

        var nom = cellules[0];
        var identifiant = cellules[1];
        var installee = cellules[2];
        var disponible = cellules[3];
        if (identifiant.Contains(' ') || installee.Length == 0 || disponible.Length == 0 || source.Length == 0)
            return null;

        return new MiseAJourWinget(
            Nom: nom,
            Identifiant: identifiant.EndsWith(PointsDeSuspension) ? null : identifiant,
            VersionInstallee: installee,
            VersionDisponible: disponible,
            Source: source,
            Tableau: tableau,
            NomCoupe: nom.EndsWith(PointsDeSuspension),
            VersionCoupee: installee.EndsWith(PointsDeSuspension) || disponible.EndsWith(PointsDeSuspension));
    }

    /// <summary>
    /// Le nombre par lequel commence la première ligne non vide sous le tableau, s'il y
    /// en a un. La phrase est traduite, le nombre non : on ne lit que lui.
    /// </summary>
    private static int? NombreSousLeTableau(List<string> lignes, int depuis)
    {
        for (int k = depuis; k < lignes.Count; k++)
        {
            var l = lignes[k].Trim();
            if (l.Length == 0) continue;
            var m = NombreEnDebut.Match(l);
            return m.Success && int.TryParse(m.Groups[1].Value, out int n) ? n : null;
        }
        return null;
    }
}
