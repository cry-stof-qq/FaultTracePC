using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.Versioning;
using System.Security.Principal;
using System.Text;

namespace FaultTracePC.Core.Collectors;

/// <summary>Les deux étendues d'installation que winget distingue.</summary>
public enum EtendueWinget
{
    /// <summary>« --scope machine » : installé pour tous les comptes du poste.</summary>
    Machine,

    /// <summary>« --scope user » : installé dans le profil du compte qui lance winget.</summary>
    Utilisateur,
}

/// <summary>Les étendues où un même logiciel a été vu (il peut l'être dans les deux).</summary>
[Flags]
public enum EtenduesWinget
{
    Aucune = 0,
    Machine = 1,
    Utilisateur = 2,
}

/// <summary>
/// Ce qu'une interrogation de winget a donné. Plan du lot 1, point 5 : chaque état
/// qui n'est pas <see cref="Lu"/> dit POURQUOI on ne sait pas — aucun ne veut dire
/// « tout est à jour ».
/// </summary>
public enum EtatWinget
{
    /// <summary>Code de sortie 0 et au moins un tableau lu.</summary>
    Lu,

    /// <summary>
    /// Code de sortie 0 et aucun tableau. winget n'a rien signalé ; sa propre phrase
    /// est gardée dans <see cref="InterrogationWinget.Message"/>, pour que le rapport
    /// la CITE plutôt que de conclure à sa place.
    /// </summary>
    RienDeSignale,

    /// <summary>Code de sortie 0, mais un tableau dont l'en-tête n'a pas la forme attendue.</summary>
    TableauIllisible,

    /// <summary>Code de sortie différent de 0 : source injoignable, filtrage, erreur de winget…</summary>
    Echec,

    /// <summary>winget n'a pas rendu la main dans le délai ; il a été arrêté.</summary>
    DelaiDepasse,

    /// <summary>winget introuvable pour ce compte (absent, ou non enregistré pour lui).</summary>
    WingetAbsent,

    /// <summary>
    /// FaultTracePC tourne sous le compte système (diagnostic à distance) : la ligne de
    /// commande winget n'y est pas prise en charge (Microsoft) ; elle n'est pas lancée.
    /// </summary>
    NonVerifieSousSysteme,
}

/// <summary>Une interrogation de winget, pour une étendue.</summary>
/// <param name="CodeDeSortie">Code rendu par winget, s'il a tourné jusqu'au bout.</param>
/// <param name="Message">
/// Première ligne non vide de ce que winget a écrit, telle quelle (dans la langue de
/// winget), ou la description de l'exception. Sert à dire la raison d'un échec.
/// </param>
public sealed record InterrogationWinget(
    EtendueWinget Etendue,
    EtatWinget Etat,
    int? CodeDeSortie,
    string? Message,
    LectureWinget? Lecture)
{
    /// <summary>
    /// Le code au format où Microsoft documente les erreurs de winget (« 0x8A15… »).
    /// winget rend un HRESULT : en entier signé il est négatif et illisible.
    /// </summary>
    public string? CodeHexadecimal => CodeDeSortie is int c ? $"0x{unchecked((uint)c):X8}" : null;
}

/// <summary>Un logiciel qui a une mise à jour, après réunion des deux étendues.</summary>
/// <param name="Tableau">Plus petit numéro de tableau où il a été lu (voir <see cref="MiseAJourWinget.Tableau"/>).</param>
public sealed record LogicielAMettreAJour(
    string Nom,
    string? Identifiant,
    string VersionInstallee,
    string VersionDisponible,
    string Source,
    EtenduesWinget Etendues,
    int Tableau,
    bool NomCoupe,
    bool VersionCoupee);

/// <summary>Le bilan des deux interrogations, et la liste réunie.</summary>
public sealed record BilanWinget(
    IReadOnlyList<InterrogationWinget> Interrogations,
    IReadOnlyList<LogicielAMettreAJour> Logiciels)
{
    /// <summary>
    /// Vrai seulement si les DEUX étendues ont répondu normalement. Faux : la liste est
    /// partielle, et le rapport doit le dire (point 5).
    /// </summary>
    public bool Complet =>
        Interrogations.Count > 0 &&
        Interrogations.All(i => i.Etat is EtatWinget.Lu or EtatWinget.RienDeSignale);

    /// <summary>Lignes de tableau qu'on n'a pas pu lire, toutes étendues confondues.</summary>
    public int LignesIllisibles => Interrogations.Sum(i => i.Lecture?.LignesIllisibles ?? 0);
}

/// <summary>
/// POINT 15, LOT 1b de la 1.10.0 (go de l'auteur le 26/10/2026).
/// Lance « winget upgrade » pour chaque étendue, lit la réponse avec
/// <see cref="LecteurWinget"/>, et réunit les deux listes.
///
/// CE QUE CE CODE NE FAIT PAS
/// Il n'installe rien : « winget upgrade » SANS identifiant ni « --all » ne fait que
/// lister. Les options qui agissent (--all, --allow-reboot, --force…) ne sont jamais
/// passées ici. Et il n'est appelé par rien tant que la case à cocher du lot 1d
/// n'existe pas : winget contacte Internet, et le logiciel promet de ne jamais le
/// faire sans case cochée.
///
/// POURQUOI DEUX INTERROGATIONS (mesures du 26/10/2026, poste de l'auteur)
/// La liste sans « --scope » n'est pas la réunion des deux étendues : Notepad++,
/// en retard côté utilisateur, n'y figurait pas. Et un même logiciel peut apparaître
/// des deux côtés — Microsoft.WSL, sous deux noms différents. On interroge donc
/// « machine » puis « user », et on réunit PAR IDENTIFIANT.
///
/// POURQUOI PAS SOUS LE COMPTE SYSTÈME
/// Documentation Microsoft (WinGet, « Troubleshooting ») : la ligne de commande winget
/// n'est pas prise en charge sous SYSTEM. Le diagnostic à distance tourne sous SYSTEM :
/// il rend « non vérifié » sans rien lancer. Le module PowerShell Microsoft.WinGet.Client,
/// documenté pour SYSTEM, est prévu dans un lot suivant, après essai sur le parc.
/// </summary>
[SupportedOSPlatform("windows")]
public static class WingetCollector
{
    /// <summary>Délai par interrogation (plan du lot 1, point 3).</summary>
    public static readonly TimeSpan Delai = TimeSpan.FromMinutes(1);

    /// <summary>Longueur au-delà de laquelle un message de winget est raccourci.</summary>
    internal const int LongueurMessageMax = 300;

    /// <summary>
    /// Arguments d'une interrogation. Fonction pure : testée.
    /// « --disable-interactivity » : sans elle, winget peut attendre une réponse au
    /// clavier que personne ne donnera. « --accept-source-agreements » : sans elle, la
    /// première utilisation de la source s'arrête sur une question d'acceptation.
    /// « --include-unknown » n'est PAS passé (point 25 de la feuille de route).
    /// </summary>
    public static List<string> Arguments(EtendueWinget etendue) => new()
    {
        "upgrade",
        "--scope", etendue == EtendueWinget.Machine ? "machine" : "user",
        "--disable-interactivity",
        "--accept-source-agreements",
    };

    /// <summary>
    /// Interroge les deux étendues, l'une après l'autre (deux minutes au plus).
    /// Ne lève pas : chaque échec devient un état, pour que le rapport puisse le dire.
    /// </summary>
    public static BilanWinget Collecter()
    {
        var etendues = new[] { EtendueWinget.Machine, EtendueWinget.Utilisateur };

        if (EstLeCompteSysteme())
            return Bilan(etendues.Select(e => new InterrogationWinget(e, EtatWinget.NonVerifieSousSysteme, null, null, null)).ToList());

        var interrogations = new List<InterrogationWinget>();
        foreach (var etendue in etendues)
        {
            // winget introuvable pour la première étendue : il le sera pour la seconde.
            if (interrogations.Count > 0 && interrogations[0].Etat == EtatWinget.WingetAbsent)
            {
                interrogations.Add(interrogations[0] with { Etendue = etendue });
                continue;
            }
            interrogations.Add(Interroger(etendue));
        }
        return Bilan(interrogations);
    }

    /// <summary>Construit le bilan à partir d'interrogations déjà faites. Fonction pure : testée.</summary>
    public static BilanWinget Bilan(IReadOnlyList<InterrogationWinget> interrogations) =>
        new(interrogations, Reunir(interrogations));

    /// <summary>
    /// Donne un état à ce que winget a rendu. Fonction pure : testée sur les captures
    /// réelles du 26/10/2026 (code 0, tableau lu).
    /// </summary>
    public static InterrogationWinget Interpreter(EtendueWinget etendue, int codeDeSortie, string? sortie, string? erreurs = null)
    {
        var lecture = LecteurWinget.Lire(sortie);
        var message = PremiereLigne(sortie) ?? PremiereLigne(erreurs);

        var etat = codeDeSortie != 0 ? EtatWinget.Echec
                 : lecture.Etat switch
                 {
                     EtatLectureWinget.Lu => EtatWinget.Lu,
                     EtatLectureWinget.EnTeteInattendu => EtatWinget.TableauIllisible,
                     _ => EtatWinget.RienDeSignale,
                 };

        // Le message ne sert qu'à expliquer : inutile quand le tableau a été lu.
        return new InterrogationWinget(etendue, etat, codeDeSortie, etat == EtatWinget.Lu ? null : message, lecture);
    }

    /// <summary>
    /// Réunit les lignes des interrogations LUES. Fonction pure : testée.
    ///
    /// Même identifiant ET même version installée → une seule ligne, qui porte les deux
    /// étendues. Même identifiant, versions différentes → deux lignes : ce sont deux
    /// installations distinctes, chacune en retard. Sans identifiant (coupé par
    /// winget) → jamais réunie : on ne rapproche pas ce qu'on ne sait pas nommer.
    ///
    /// Le nom : winget peut nommer le même paquet de deux façons
    /// (« MicrosoftCorporationII.WindowsSubsystemForLinux » côté machine, « Sous-système
    /// Windows pour Linux » côté utilisateur). On garde celui qui contient un espace —
    /// un nom écrit pour être lu — quand l'autre n'en a pas. Choix d'affichage seulement.
    /// </summary>
    public static List<LogicielAMettreAJour> Reunir(IEnumerable<InterrogationWinget> interrogations)
    {
        var liste = new List<LogicielAMettreAJour>();
        var index = new Dictionary<(string, string), int>();

        foreach (var interrogation in interrogations)
        {
            if (interrogation.Etat != EtatWinget.Lu || interrogation.Lecture is null) continue;
            var etendue = interrogation.Etendue == EtendueWinget.Machine ? EtenduesWinget.Machine : EtenduesWinget.Utilisateur;

            foreach (var m in interrogation.Lecture.MisesAJour)
            {
                if (m.Identifiant is { } id)
                {
                    var cle = (id.ToLowerInvariant(), m.VersionInstallee);
                    if (index.TryGetValue(cle, out int k))
                    {
                        var deja = liste[k];
                        bool nomPlusLisible = !deja.Nom.Contains(' ') && m.Nom.Contains(' ');
                        liste[k] = deja with
                        {
                            Etendues = deja.Etendues | etendue,
                            Nom = nomPlusLisible ? m.Nom : deja.Nom,
                            NomCoupe = nomPlusLisible ? m.NomCoupe : deja.NomCoupe,
                            Tableau = Math.Min(deja.Tableau, m.Tableau),
                        };
                        continue;
                    }
                    index[cle] = liste.Count;
                }

                liste.Add(new LogicielAMettreAJour(m.Nom, m.Identifiant, m.VersionInstallee, m.VersionDisponible,
                    m.Source, etendue, m.Tableau, m.NomCoupe, m.VersionCoupee));
            }
        }
        return liste;
    }

    /// <summary>Première ligne non vide, raccourcie. Null s'il n'y en a pas.</summary>
    internal static string? PremiereLigne(string? texte)
    {
        var ligne = LecteurWinget.Lignes(texte).Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0);
        if (ligne is null) return null;
        return ligne.Length <= LongueurMessageMax ? ligne : ligne[..LongueurMessageMax] + LecteurWinget.PointsDeSuspension;
    }

    private static bool EstLeCompteSysteme()
    {
        try
        {
            using var identite = WindowsIdentity.GetCurrent();
            return identite.IsSystem;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Lance winget sans fenêtre et lit sa réponse EN UTF-8 : c'est l'encodage mesuré
    /// sur la sortie redirigée le 26/10/2026 ; lue autrement, « Sous-système » serait
    /// abîmé. Sorties lues en parallèle, pour que rien ne bloque.
    /// </summary>
    private static InterrogationWinget Interroger(EtendueWinget etendue)
    {
        try
        {
            var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
            var psi = new ProcessStartInfo("winget.exe")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = utf8,
                StandardErrorEncoding = utf8,
            };
            foreach (var a in Arguments(etendue)) psi.ArgumentList.Add(a);

            using var p = Process.Start(psi);
            if (p is null)
                return new InterrogationWinget(etendue, EtatWinget.WingetAbsent, null, null, null);

            var sortie = p.StandardOutput.ReadToEndAsync();
            var erreurs = p.StandardError.ReadToEndAsync();
            if (!p.WaitForExit(Delai))
            {
                try { p.Kill(entireProcessTree: true); } catch { /* déjà terminé */ }
                return new InterrogationWinget(etendue, EtatWinget.DelaiDepasse, null, null, null);
            }
            p.WaitForExit(); // termine la lecture des sorties redirigées
            return Interpreter(etendue, p.ExitCode, sortie.Result, erreurs.Result);
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 2 || ex.NativeErrorCode == 3)
        {
            // 2 = fichier introuvable, 3 = chemin introuvable : winget n'est pas là
            // pour ce compte (absent, ou application non enregistrée pour lui).
            return new InterrogationWinget(etendue, EtatWinget.WingetAbsent, null, ErrorLog.Describe(ex), null);
        }
        catch (Exception ex)
        {
            return new InterrogationWinget(etendue, EtatWinget.Echec, null, ErrorLog.Describe(ex), null);
        }
    }
}
