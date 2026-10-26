using FaultTracePC.Core.Collectors;

namespace FaultTracePC.Core.Analysis;

/// <summary>
/// POINT 15, LOT 1c de la 1.10.0 (go de l'auteur le 26/10/2026) — ce que le rapport
/// dit de la liste de winget. Trois usages, décidés au point 6 du plan du lot 1 :
///   · une conclusion de niveau « Information » qui donne le nombre ;
///   · une section dans les détails techniques (voir HtmlReportGenerator) ;
///   · sur la carte d'une application qui plante, « une mise à jour est disponible » —
///     le lien entre un logiciel resté ancien et ses plantages, ce que l'auteur
///     attend de winget (dit le 21/10/2026).
///
/// RÈGLE TENUE PARTOUT (point 5 du plan) : une étendue qui n'a pas répondu
/// normalement est dite, avec sa raison. Le rapport n'écrit jamais « tout est à
/// jour » ; au mieux « winget ne signale aucune mise à jour », en citant winget.
/// </summary>
public static class MisesAJourLogiciels
{
    /// <summary>Identifiant stable de la conclusion (voir <see cref="Finding.Code"/>).</summary>
    public const string CodeConclusion = "winget.misesajour";

    /// <summary>Au-delà, la conclusion renvoie à la section des détails techniques.</summary>
    internal const int NomsDansLaConclusion = 8;

    /// <summary>
    /// La mise à jour qui concerne l'exécutable qui plante, s'il y en a une.
    ///
    /// Même règle de rapprochement que pour la liste des programmes installés
    /// (<see cref="InstalledSoftwareCollector.FindByExecutable"/>) : nom du paquet et
    /// nom de l'exécutable comparés sans espaces ni ponctuation. C'est une
    /// RESSEMBLANCE DE NOMS, pas une preuve : la phrase du rapport nomme donc le paquet
    /// retenu, pour que le lecteur juge. Rien n'est rapproché quand le bilan est absent.
    /// </summary>
    public static LogicielAMettreAJour? MiseAJourPour(BilanWinget? bilan, string executable)
    {
        if (bilan is null || bilan.Logiciels.Count == 0 || string.IsNullOrWhiteSpace(executable)) return null;

        var parNom = new Dictionary<InstalledApp, LogicielAMettreAJour>(ReferenceEqualityComparer.Instance);
        foreach (var l in bilan.Logiciels)
            parNom[new InstalledApp { Name = l.Nom, Version = l.VersionInstallee }] = l;

        var trouve = InstalledSoftwareCollector.FindByExecutable(parNom.Keys.ToList(), executable);
        return trouve is null ? null : parNom[trouve];
    }

    /// <summary>La phrase ajoutée à la carte d'une application qui plante.</summary>
    public static string PhraseSurLaCarte(LogicielAMettreAJour l) => Lang.T(
        $" Une mise à jour est disponible pour « {l.Nom} » selon winget : {l.VersionInstallee} → {l.VersionDisponible}. Rapprochement fait sur le nom : vérifier qu'il s'agit bien du même logiciel.",
        $" An update is available for “{l.Nom}” according to winget: {l.VersionInstallee} → {l.VersionDisponible}. Matched by name: check that it is indeed the same program.");

    public static string NomEtendue(EtendueWinget e) => e == EtendueWinget.Machine
        ? Lang.T("pour tout le poste", "for the whole computer")
        : Lang.T("pour ce compte", "for this account");

    public static string NomEtendues(EtenduesWinget e) => e switch
    {
        EtenduesWinget.Machine => Lang.T("tout le poste", "whole computer"),
        EtenduesWinget.Utilisateur => Lang.T("ce compte", "this account"),
        _ => Lang.T("tout le poste et ce compte", "whole computer and this account"),
    };

    /// <summary>
    /// Pourquoi une étendue n'a pas donné de liste. <c>null</c> quand elle a répondu
    /// normalement (liste lue, ou rien de signalé par winget).
    /// </summary>
    public static string? Raison(InterrogationWinget i)
    {
        var citation = string.IsNullOrWhiteSpace(i.Message) ? "" : Lang.T($" : « {i.Message} »", $": “{i.Message}”");
        return i.Etat switch
        {
            EtatWinget.Lu or EtatWinget.RienDeSignale => null,
            EtatWinget.TableauIllisible => Lang.T(
                "la réponse de winget n'a pas la forme attendue ; elle n'a pas été lue plutôt que mal lue",
                "winget's answer is not in the expected form; it was left unread rather than misread"),
            EtatWinget.Echec => Lang.T($"winget a échoué (code {i.CodeHexadecimal ?? "?"}){citation}",
                                       $"winget failed (code {i.CodeHexadecimal ?? "?"}){citation}")
                                + Explication(i.CodeDeSortie),
            EtatWinget.DelaiDepasse => Lang.T("winget n'a pas répondu en une minute ; il a été arrêté",
                                              "winget did not answer within one minute; it was stopped"),
            EtatWinget.WingetAbsent => Lang.T("winget est introuvable pour ce compte (absent, ou pas installé pour ce compte)",
                                              "winget cannot be found for this account (missing, or not installed for this account)"),
            EtatWinget.NonVerifieSousSysteme => Lang.T(
                "non vérifié : sous le compte système (diagnostic à distance), winget ne peut pas être utilisé — prévu dans une version suivante",
                "not checked: under the system account (remote diagnosis), winget cannot be used — planned for a later version"),
            _ => Lang.T("état inconnu", "unknown state"),
        };
    }

    /// <summary>
    /// 0x8A15004B, APPINSTALLER_CLI_ERROR_FAILED_TO_OPEN_ALL_SOURCES (« Failed to open one
    /// or more sources », table des codes de winget). Mesuré le 26/10/2026 dans une VM
    /// sans réseau. winget conseille alors « source reset » — conseil trompeur quand la
    /// cause est l'absence d'Internet ou le filtrage, cas fréquent en établissement.
    /// </summary>
    internal const int CodeSourceInjoignable = unchecked((int)0x8A15004B);

    /// <summary>
    /// 0x80072EE7 : l'erreur Windows 12007, ERROR_INTERNET_NAME_NOT_RESOLVED, « The server
    /// name could not be resolved » (documentation Microsoft des erreurs WinINet). Mesuré
    /// le 26/10/2026 dans une VM sans réseau, plus de 15 minutes après le dernier passage
    /// en ligne : winget annonce « Failed in attempting to update the source: winget » et
    /// s'arrête. Même cause que 0x8A15004B : même explication (go de l'auteur le même jour).
    /// </summary>
    internal const int CodeNomNonResolu = unchecked((int)0x80072EE7);

    /// <summary>
    /// Ce que veut dire un code connu, pour le lecteur. Vide pour un code inconnu : on
    /// n'invente pas d'explication. Texte validé par l'auteur le 26/10/2026.
    /// </summary>
    internal static string Explication(int? code) => code is CodeSourceInjoignable or CodeNomNonResolu
        ? Lang.T(" — winget n'a pas pu joindre sa source : pas d'accès à Internet, ou accès filtré. Réessayer avec une connexion qui l'autorise",
                 " — winget could not reach its source: no Internet access, or filtered access. Try again with a connection that allows it")
        : "";

    /// <summary>
    /// Écart entre ce que winget annonce sous son tableau et ce qui a été lu (point 28 :
    /// « lu N sur N »). <c>null</c> quand les comptes concordent ou ne se comparent pas.
    /// </summary>
    public static string? EcartDeCompte(InterrogationWinget i)
    {
        if (i.Etat != EtatWinget.Lu || i.Lecture is not { NombreAnnonce: int annonce } lecture) return null;
        // Le nombre annoncé ne porte que sur le PREMIER tableau.
        int lues = lecture.MisesAJour.Count(m => m.Tableau == 0);
        if (lecture.MisesAJour.Any(m => m.Tableau > 0) || annonce == lues + lecture.LignesIllisibles) return null;
        return Lang.T($"winget en annonce {annonce} {NomEtendue(i.Etendue)}, {lues} ont été lues",
                      $"winget announces {annonce} {NomEtendue(i.Etendue)}, {lues} were read");
    }

    /// <summary>La conclusion « Information » du rapport. Fonction pure : testée.</summary>
    public static Finding Conclusion(BilanWinget bilan)
    {
        var logiciels = bilan.Logiciels;
        var raisons = bilan.Interrogations
            .Select(i => Raison(i) is { } r ? Lang.T($"Recherche {NomEtendue(i.Etendue)} : {r}.", $"Search {NomEtendue(i.Etendue)}: {r}.") : null)
            .Where(r => r is not null).ToList();
        var ecarts = bilan.Interrogations.Select(EcartDeCompte).Where(e => e is not null).ToList();
        var illisibles = bilan.LignesIllisibles;

        var complements = new List<string>();
        if (raisons.Count > 0)
            complements.Add(Lang.T("La liste est INCOMPLÈTE.", "The list is INCOMPLETE.") + " " + string.Join(" ", raisons));
        if (illisibles > 0)
            complements.Add(Lang.T($"{illisibles} ligne(s) de la réponse de winget n'ont pas pu être lues ; elles ne sont pas devinées.",
                                   $"{illisibles} line(s) of winget's answer could not be read; they are not guessed."));
        foreach (var e in ecarts)
            complements.Add(Lang.T($"Comptes à vérifier : {e}.", $"Counts to check: {e}."));
        var suite = complements.Count == 0 ? "" : " " + string.Join(" ", complements);

        string titre, details, reco;
        if (logiciels.Count > 0)
        {
            var noms = logiciels.Take(NomsDansLaConclusion)
                .Select(l => $"{l.Nom} ({l.VersionInstallee} → {l.VersionDisponible})");
            var reste = logiciels.Count > NomsDansLaConclusion
                ? Lang.T($" et {logiciels.Count - NomsDansLaConclusion} autre(s)", $" and {logiciels.Count - NomsDansLaConclusion} more")
                : "";
            titre = Lang.T($"{logiciels.Count} logiciel(s) avec une mise à jour disponible", $"{logiciels.Count} program(s) with an update available")
                    + (raisons.Count > 0 ? Lang.T(" — liste incomplète", " — incomplete list") : "");
            details = Lang.T($"Selon winget : {string.Join(" ; ", noms)}{reste}.", $"According to winget: {string.Join("; ", noms)}{reste}.") + suite;
            reco = Lang.T(
                "Mettre ces logiciels à jour, en commençant par ceux qui plantent (voir les conclusions ci-dessus). Le détail — identifiant, versions, pour tout le poste ou pour ce compte — est dans les détails techniques, section « Mises à jour des logiciels (winget) ».",
                "Update these programs, starting with those that crash (see the findings above). The details — identifier, versions, for the whole computer or for this account — are in the technical details, section “Software updates (winget)”.");
        }
        else if (raisons.Count == 0)
        {
            var citations = bilan.Interrogations
                .Where(i => !string.IsNullOrWhiteSpace(i.Message))
                .Select(i => Lang.T($"{NomEtendue(i.Etendue)} : « {i.Message} »", $"{NomEtendue(i.Etendue)}: “{i.Message}”"))
                .ToList();
            titre = Lang.T("Logiciels : winget ne signale aucune mise à jour", "Software: winget reports no update");
            details = Lang.T("winget a répondu normalement pour tout le poste et pour ce compte, sans liste de mises à jour.",
                             "winget answered normally for the whole computer and for this account, with no list of updates.")
                      + (citations.Count > 0 ? Lang.T($" Sa réponse — {string.Join(" ; ", citations)}.", $" Its answer — {string.Join("; ", citations)}.") : "")
                      + suite;
            reco = "";
        }
        else
        {
            bool systeme = bilan.Interrogations.All(i => i.Etat == EtatWinget.NonVerifieSousSysteme);
            titre = systeme
                ? Lang.T("Mises à jour des logiciels : non vérifiées", "Software updates: not checked")
                : Lang.T("Mises à jour des logiciels : vérification incomplète", "Software updates: incomplete check");
            details = Lang.T("Aucune mise à jour n'a pu être listée — ce qui ne veut PAS dire que tout est à jour.",
                             "No update could be listed — which does NOT mean everything is up to date.") + suite;
            reco = systeme
                ? Lang.T("Pour le savoir, lancer le diagnostic sur le poste lui-même, avec la case de recherche des mises à jour cochée.",
                         "To find out, run the diagnosis on the computer itself, with the update-search box ticked.")
                : Lang.T("Relancer le diagnostic plus tard. Si l'échec persiste, taper « winget upgrade » dans une invite de commandes pour voir la réponse de winget.",
                         "Run the diagnosis again later. If the failure persists, type “winget upgrade” in a command prompt to see winget's answer.");
        }

        return new Finding
        {
            Severity = Severity.Info,
            Confidence = raisons.Count == 0 && illisibles == 0 && ecarts.Count == 0 ? Confidence.High : Confidence.Medium,
            Category = FaultCategory.None,
            Code = CodeConclusion,
            Title = titre,
            Details = details,
            Recommendation = reco,
        };
    }
}
