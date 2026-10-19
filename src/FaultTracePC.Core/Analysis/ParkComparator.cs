using FaultTracePC.Core.Report;

namespace FaultTracePC.Core.Analysis;

/// <summary>
/// Comparateur de parc : ce qu'aucun diagnostic individuel ne peut voir.
///
/// Sur une machine isolée, un pilote de 2019 est « un suspect potentiel ». Quand
/// six postes sur douze portent exactement le même, ce n'est plus une hypothèse :
/// c'est une image de déploiement à corriger, et la réparation se fait une fois
/// pour tout le parc au lieu de douze fois à l'aveugle.
///
/// Trois familles de corrélations, dans l'ordre de ce qu'elles font gagner :
///
///  · CE QUI EST COMMUN — un pilote ancien, un code d'arrêt ou un modèle de
///    disque partagés par plusieurs postes. Une cause unique, une action unique.
///  · CE QUI DIVERGE — le même pilote en trois versions différentes. Le parc a
///    dérivé ; les postes en retard sont ceux qui plantent.
///  · CE QUI EST ISOLÉ — un poste qui accumule seul les conclusions critiques.
///    Là, c'est la machine qu'il faut regarder, pas le parc.
///
/// Aucune de ces corrélations n'a de sens sur une seule machine : le comparateur
/// ne produit rien en dessous de deux postes, et le dit plutôt que d'afficher un
/// tableau vide.
/// </summary>
public static class ParkComparator
{
    /// <summary>Un poste et son dernier résumé de scan.</summary>
    public sealed record MachineSummary(string Name, ScanHistory.ScanSummary Summary);

    public sealed class Correlation
    {
        public required string Kind { get; init; }        // "commun", "divergence", "isolé"
        public required string Severity { get; init; }    // "crit", "warn", "info"
        public required string Title { get; init; }
        public required string Details { get; init; }
        public required string Action { get; init; }
        /// <summary>Postes concernés — nommer permet d'agir tout de suite.</summary>
        public List<string> Machines { get; init; } = new();
    }

    public sealed class ParkAnalysis
    {
        public int MachineCount { get; set; }
        public int WithSummary { get; set; }
        public List<Correlation> Correlations { get; } = new();
        public string Summary { get; set; } = "";
        /// <summary>Point 97 : les postes réellement comparés, nommés.</summary>
        public List<string> Compares { get; } = new();
        /// <summary>Point 97 : les postes restés hors de la comparaison, avec la raison. Rempli par la console.</summary>
        public List<PosteNonCompare> NonCompares { get; } = new();
    }

    /// <summary>Pourquoi un poste du parc n'est pas entré dans la comparaison.</summary>
    public enum MotifAbsence
    {
        /// <summary>Le poste répond, mais n'a jamais eu de diagnostic complet.</summary>
        JamaisAnalyse,
        /// <summary>Son dernier diagnostic est dans un format que cette console ne lit pas.</summary>
        FormatIncompatible,
        /// <summary>La réponse ne se lit pas du tout.</summary>
        Illisible,
        /// <summary>Le poste a refusé la demande (code HTTP d'échec).</summary>
        Refuse,
        /// <summary>Pas de réponse réseau.</summary>
        Injoignable,
        /// <summary>Ni jeton ni secret maître : la console n'a pas pu signer la demande.</summary>
        SecretAbsent,
    }

    public sealed record PosteNonCompare(string Nom, MotifAbsence Motif);

    /// <summary>Âge à partir duquel un pilote mérite d'être signalé quand il est partagé.</summary>
    private static readonly TimeSpan OldDriver = TimeSpan.FromDays(4 * 365);

    public static ParkAnalysis Analyze(IReadOnlyList<MachineSummary> machines, DateTime? now = null)
    {
        var today = now ?? DateTime.Now;
        var a = new ParkAnalysis { MachineCount = machines.Count, WithSummary = machines.Count };
        a.Compares.AddRange(machines.Select(m => m.Name).OrderBy(x => x, StringComparer.OrdinalIgnoreCase));

        if (machines.Count < 2)
        {
            a.Summary = machines.Count == 0
                ? Lang.T("Aucun poste n'a encore transmis de résumé d'analyse : le comparateur n'a rien à comparer.",
                         "No machine has sent an analysis summary yet: the comparator has nothing to compare.")
                : Lang.T(
                    "Un seul poste dispose d'un résumé d'analyse. La comparaison de parc demande au moins deux machines — "
                    + "c'est précisément ce qu'elle apporte par rapport à un diagnostic individuel.",
                    "Only one machine has an analysis summary. Fleet comparison needs at least two machines — "
                    + "that is precisely what it adds over an individual diagnosis.");
            return a;
        }

        // ---------------------------------------------------------------
        // 1. Pilotes anciens partagés — la corrélation la plus rentable
        // ---------------------------------------------------------------
        var byDriver = new Dictionary<string, List<(string Machine, string Version, DateTime? Date)>>(StringComparer.OrdinalIgnoreCase);
        foreach (var m in machines)
            foreach (var (file, packed) in m.Summary.DriverVersions)
            {
                var (version, date) = Unpack(packed);
                if (!byDriver.TryGetValue(file, out var list)) byDriver[file] = list = new();
                list.Add((m.Name, version, date));
            }

        // Point 96 : divergences mises de côté pour être regroupées (voir plus bas).
        var divergencesWindows = new List<(string Fichier, List<IGrouping<string, (string Machine, string Version, DateTime? Date)>> Versions)>();
        var divergencesDefender = new List<(string Fichier, List<IGrouping<string, (string Machine, string Version, DateTime? Date)>> Versions)>();

        foreach (var (file, entries) in byDriver.OrderByDescending(e => e.Value.Count))
        {
            if (entries.Count < 2) continue;

            var oldOnes = entries.Where(e => e.Date is { } d && today - d > OldDriver).ToList();
            if (oldOnes.Count >= 2)
            {
                var oldest = oldOnes.Min(e => e.Date!.Value);
                var kb = DriverKnowledgeBase.Lookup(file);
                a.Correlations.Add(new Correlation
                {
                    Kind = "commun",
                    Severity = oldOnes.Count >= machines.Count / 2.0 ? "warn" : "info",
                    Title = Lang.T($"{oldOnes.Count} poste(s) sur {machines.Count} partagent le même pilote ancien : {file}", $"{oldOnes.Count} machine(s) out of {machines.Count} share the same old driver: {file}"),
                    Details = Lang.T($"Version la plus ancienne datée du {oldest:dd/MM/yyyy}", $"Oldest version dated {oldest:yyyy-MM-dd}")
                            + (kb is not null ? Lang.T($". Ce pilote appartient à {kb.Owner}. {kb.Context}", $". This driver belongs to {kb.Owner}. {kb.Context}") : Lang.T(". Ce pilote n'est pas documenté dans la base de FaultTracePC.", ". This driver is not documented in the FaultTracePC knowledge base."))
                            + Lang.T(" Un pilote identique sur plusieurs postes vient presque toujours de l'image de déploiement ou d'une installation groupée : la corriger une fois règle le problème partout.",
                                     " An identical driver on several machines almost always comes from the deployment image or a bulk installation: fixing it once solves the problem everywhere."),
                    Action = kb?.Fix ?? Lang.T("Identifier le logiciel ou le matériel associé, puis mettre à jour l'ensemble du parc en une opération.", "Identify the associated software or hardware, then update the whole fleet in a single operation."),
                    Machines = oldOnes.Select(e => e.Machine).Distinct().OrderBy(x => x).ToList(),
                });
            }

            // -----------------------------------------------------------
            // 2. Divergence de version — le parc a dérivé
            // -----------------------------------------------------------
            var versions = entries.Where(e => e.Version.Length > 0)
                                  .GroupBy(e => e.Version)
                                  .OrderByDescending(g => g.Count()).ToList();
            if (versions.Count >= 2 && entries.Count >= 3)
            {
                switch (Famille(file, versions.Select(g => g.Key)))
                {
                    case FamillePilote.Windows: divergencesWindows.Add((file, versions)); break;
                    case FamillePilote.Defender: divergencesDefender.Add((file, versions)); break;
                    default: a.Correlations.Add(Divergence(file, versions)); break;
                }
            }
        }

        // POINT 96 — constaté sur les rapports du parc du 12/10/2026 (88 cartes) et du
        // 19/10/2026 (175 cartes, dont 145 pour des pilotes de Windows). Les pilotes
        // livrés avec Windows changent TOUS ENSEMBLE à chaque mise à jour cumulative ;
        // ceux de Defender, avec sa plateforme. Une carte par fichier noyait les vrais
        // écarts — ceux des pilotes de constructeurs — sous un seul fait répété cent
        // fois. Regroupés en une carte par famille, à partir de DEUX fichiers : un seul
        // fichier garde sa carte, qui dit déjà tout.
        AjouterGroupe(a, divergencesWindows, FamillePilote.Windows);
        AjouterGroupe(a, divergencesDefender, FamillePilote.Defender);

        // ---------------------------------------------------------------
        // 3. Même code d'arrêt sur plusieurs postes
        // ---------------------------------------------------------------
        var byCode = machines
            .SelectMany(m => m.Summary.Bsods.Where(b => b.Code is not null).Select(b => (m.Name, b.Code!.Value)))
            .GroupBy(x => x.Value)
            .Where(g => g.Select(x => x.Name).Distinct().Count() >= 2);

        foreach (var g in byCode)
        {
            var names = g.Select(x => x.Name).Distinct().OrderBy(x => x).ToList();
            a.Correlations.Add(new Correlation
            {
                Kind = "commun",
                Severity = "crit",
                Title = Lang.T($"Le même écran bleu frappe {names.Count} postes : {BugCheckCatalog.NameOf(g.Key)}", $"The same blue screen hits {names.Count} machines: {BugCheckCatalog.NameOf(g.Key)}"),
                Details = Lang.T($"Code 0x{g.Key:X8}, observé sur {names.Count} machines distinctes. ", $"Code 0x{g.Key:X8}, seen on {names.Count} distinct machines. ")
                        + Lang.T("Un code d'arrêt identique sur plusieurs postes désigne une cause partagée — pilote déployé en masse, mise à jour commune, ou modèle de matériel identique — et non un incident isolé.",
                                 "An identical stop code on several machines points to a shared cause — a mass-deployed driver, a common update, or the same hardware model — not an isolated incident."),
                Action = Lang.T("Comparer les rapports de ces postes pour isoler ce qu'ils ont en commun et que les autres n'ont pas : ", "Compare these machines' reports to isolate what they share and the others do not: ")
                       + Lang.T("même pilote, même mise à jour récente, même modèle de machine.", "same driver, same recent update, same machine model."),
                Machines = names,
            });
        }

        // ---------------------------------------------------------------
        // 4. Même pilote incriminé sur plusieurs postes
        // ---------------------------------------------------------------
        var bySuspect = machines
            .SelectMany(m => m.Summary.Bsods.Where(b => !string.IsNullOrEmpty(b.Driver)).Select(b => (m.Name, Driver: b.Driver!)))
            .GroupBy(x => x.Driver, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Select(x => x.Name).Distinct().Count() >= 2);

        foreach (var g in bySuspect)
        {
            var names = g.Select(x => x.Name).Distinct().OrderBy(x => x).ToList();
            var kb = DriverKnowledgeBase.Lookup(g.Key);
            a.Correlations.Add(new Correlation
            {
                Kind = "commun",
                Severity = "crit",
                Title = Lang.T($"Le pilote {g.Key} est mis en cause sur {names.Count} postes", $"Driver {g.Key} is implicated on {names.Count} machines"),
                Details = (kb is not null ? $"{kb.Owner} — {kb.Context} " : "")
                        + Lang.T("L'analyse symbolique le désigne sur plusieurs machines : ce n'est plus une coïncidence.", "Symbolic analysis names it on several machines: this is no longer a coincidence."),
                Action = kb?.Fix ?? Lang.T("Mettre ce pilote à jour sur l'ensemble du parc, ou retirer le logiciel qui l'installe.", "Update this driver across the whole fleet, or remove the software that installs it."),
                Machines = names,
            });
        }

        // ---------------------------------------------------------------
        // 5. Même modèle de disque en dégradation
        // ---------------------------------------------------------------
        var byDisk = machines
            .SelectMany(m => m.Summary.Disks
                .Where(d => (d.BadSectors ?? 0) > 0 || d.Health.IsDegraded())
                .Select(d => (m.Name, d.Model)))
            .Where(x => !string.IsNullOrWhiteSpace(x.Model))
            .GroupBy(x => x.Model, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Select(x => x.Name).Distinct().Count() >= 2);

        foreach (var g in byDisk)
        {
            var names = g.Select(x => x.Name).Distinct().OrderBy(x => x).ToList();
            a.Correlations.Add(new Correlation
            {
                Kind = "commun",
                Severity = "crit",
                Title = Lang.T($"Le modèle de disque {g.Key} se dégrade sur {names.Count} postes", $"Drive model {g.Key} is degrading on {names.Count} machines"),
                Details = Lang.T("Plusieurs exemplaires du même modèle montrent des secteurs défectueux ou un état de santé dégradé. ", "Several units of the same model show bad sectors or a degraded health status. ")
                        + Lang.T("Un modèle qui faiblit en série relève du défaut de série ou d'un lot arrivé en fin de vie au même moment, pas de la malchance.",
                                 "A model failing in series points to a batch defect or a batch reaching end of life at the same time, not to bad luck."),
                Action = Lang.T("Sauvegarder ces postes en priorité, vérifier l'existence d'une mise à jour de firmware chez le fabricant, et anticiper le remplacement des autres exemplaires du même modèle avant qu'ils ne lâchent à leur tour.",
                                "Back up these machines first, check whether the manufacturer has a firmware update, and plan the replacement of the other units of the same model before they fail in turn."),
                Machines = names,
            });
        }

        // ---------------------------------------------------------------
        // 6. Poste isolé qui accumule — là, c'est la machine
        // ---------------------------------------------------------------
        var avgCritical = machines.Average(m => m.Summary.CriticalFindings.Count);
        foreach (var m in machines.Where(m => m.Summary.CriticalFindings.Count >= 3
                                           && m.Summary.CriticalFindings.Count >= avgCritical * 2))
            a.Correlations.Add(new Correlation
            {
                Kind = "isolé",   // pas-de-traduction : clé interne, lue par ParkReportGenerator.
                Severity = "warn",
                Title = Lang.T($"{m.Name} concentre {m.Summary.CriticalFindings.Count} problèmes critiques, bien plus que le reste du parc", $"{m.Name} concentrates {m.Summary.CriticalFindings.Count} critical problems, far more than the rest of the fleet"),
                Details = Lang.T($"Moyenne du parc : {avgCritical:0.#} problème(s) critique(s) par poste. ", $"Fleet average: {avgCritical.ToString("0.#", Lang.Culture)} critical problem(s) per machine. ")
                        + Lang.T("Concernant ce poste : ", "On this machine: ") + string.Join(" · ", m.Summary.CriticalFindings.Take(5)) + ".",
                Action = Lang.T("Traiter cette machine individuellement : son problème lui est propre et ne se réglera pas par une action de parc.", "Treat this machine individually: its problem is its own and will not be solved by a fleet-wide action."),
                Machines = [m.Name],
            });

        // ---------------------------------------------------------------

        int crit = a.Correlations.Count(c => c.Severity == "crit");
        a.Summary = a.Correlations.Count == 0
            ? Lang.T($"Aucune corrélation entre les {machines.Count} postes analysés : les problèmes éventuels sont propres à chaque machine.", $"No correlation between the {machines.Count} machines analysed: any problems are specific to each machine.")
            : crit > 0
                ? Lang.T($"{crit} corrélation(s) critique(s) sur {machines.Count} postes : une même cause touche plusieurs machines, ", $"{crit} critical correlation(s) across {machines.Count} machines: one cause affects several machines, ")
                + Lang.T("et une seule action peut donc les corriger ensemble.", "so a single action can fix them together.")
                : Lang.T($"{a.Correlations.Count} point(s) de convergence relevés sur {machines.Count} postes, sans gravité immédiate.", $"{a.Correlations.Count} point(s) of convergence found across {machines.Count} machines, none immediately serious.");

        a.Correlations.Sort((x, y) => Rank(x.Severity).CompareTo(Rank(y.Severity)));
        return a;
    }

    /// <summary>
    /// Point 97, constaté le 12/10/2026 : un poste jamais analysé répond
    /// <c>{"available":false,…}</c>. Lu comme un résumé, cela donnait un format 0,
    /// et la console le comptait parmi les postes « écartés : résumé produit par
    /// une version antérieure » — six postes sur un parc de dix, faussement.
    /// Ici, la réponse de <c>/api/summary</c> est classée : résumé lisible, poste
    /// jamais analysé, format incompatible, ou réponse illisible.
    /// </summary>
    public static (ScanHistory.ScanSummary? Resume, MotifAbsence? Motif) LireReponse(string json)
    {
        if (ScanHistory.Lire(json) is { } resume) return (resume, null);
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != System.Text.Json.JsonValueKind.Object)
                return (null, MotifAbsence.Illisible);
            foreach (var p in doc.RootElement.EnumerateObject())
                if (string.Equals(p.Name, "available", StringComparison.OrdinalIgnoreCase)
                    && p.Value.ValueKind == System.Text.Json.JsonValueKind.False)
                    return (null, MotifAbsence.JamaisAnalyse);
            return (null, MotifAbsence.FormatIncompatible);
        }
        catch { return (null, MotifAbsence.Illisible); }
    }

    /// <summary>Le motif d'absence, dit dans la langue en cours, avec ce qu'il faut faire.</summary>
    public static string TexteMotif(MotifAbsence motif) => motif switch
    {
        MotifAbsence.JamaisAnalyse => Lang.T("jamais analysé — lancer un diagnostic à distance", "never analysed — run a remote diagnosis"),
        MotifAbsence.FormatIncompatible => Lang.T("dernier diagnostic dans un format que cette console ne lit pas — mettre le poste et la console à la même version, puis relancer un diagnostic",
                                                  "last diagnosis in a format this console cannot read — bring the machine and the console to the same version, then run a new diagnosis"),
        MotifAbsence.Illisible => Lang.T("réponse illisible", "unreadable answer"),
        MotifAbsence.Refuse => Lang.T("demande refusée par le poste", "request refused by the machine"),
        MotifAbsence.Injoignable => Lang.T("injoignable", "unreachable"),
        _ => Lang.T("secret maître absent", "master secret missing"),
    };

    /// <summary>
    /// Point 97 : la phrase qui dit sur quels postes porte la comparaison. Sans
    /// elle, « 2 poste(s) sur 3 » se lisait « deux tiers du parc » alors qu'il
    /// s'agissait de 2 des 3 postes comparés, sur un parc de 10.
    /// </summary>
    public static string Perimetre(ParkAnalysis a)
    {
        var total = a.Compares.Count + a.NonCompares.Count;
        var texte = Lang.T($"Comparaison faite sur {a.Compares.Count} poste(s) sur {total}", $"Comparison made on {a.Compares.Count} machine(s) out of {total}")
                  + (a.Compares.Count > 0 ? Lang.T(" : ", ": ") + string.Join(", ", a.Compares) : "")
                  + ".";
        if (a.NonCompares.Count == 0) return texte;

        return texte
             + Lang.T($" Non comparés ({a.NonCompares.Count}) : ", $" Not compared ({a.NonCompares.Count}): ")
             + string.Join(Lang.T(" ; ", "; "), a.NonCompares
                   .OrderBy(p => p.Nom, StringComparer.OrdinalIgnoreCase)
                   .Select(p => $"{p.Nom} ({TexteMotif(p.Motif)})"))
             + Lang.T(". Les chiffres « N poste(s) sur M » ci-dessous comptent seulement les postes comparés.",
                      ". The “N machine(s) out of M” figures below count the compared machines only.");
    }

    /// <summary>
    /// Point 95, constaté le 12/10/2026 sur un rapport réel : la version
    /// « majoritaire » était la plus fréquente, et tous les autres postes étaient
    /// dits « en retard ». Le poste qui avait un mois de mises à jour Windows
    /// d'avance était ainsi désigné comme retardataire, et le conseil revenait à
    /// lui faire installer une version plus ancienne. Avec trois versions sur
    /// trois postes, la « majorité » dépendait de l'ordre de lecture.
    ///
    /// Désormais : si toutes les versions se comparent, les postes en retard sont
    /// ceux qui n'ont pas la PLUS RÉCENTE. Sinon, on ne désigne personne : on
    /// donne la répartition et on dit qu'il faut vérifier laquelle est à jour.
    /// </summary>
    private static Correlation Divergence(string file, List<IGrouping<string, (string Machine, string Version, DateTime? Date)>> versions)
    {
        var repartition = Lang.T("Répartition : ", "Spread: ")
            + string.Join(" · ", versions.Select(g => Lang.T($"{g.Key} sur {g.Count()} poste(s)", $"{g.Key} on {g.Count()} machine(s)")));
        var titre = Lang.T($"{file} existe en {versions.Count} versions différentes sur le parc", $"{file} exists in {versions.Count} different versions across the fleet");

        var numeros = versions.Select(g => (Groupe: g, Numero: NumeroDeVersion(g.Key))).ToList();
        if (numeros.All(x => x.Numero is not null))
        {
            var plusRecente = numeros.OrderByDescending(x => x.Numero).First();
            var enRetard = numeros.Where(x => x.Numero < plusRecente.Numero)
                                  .SelectMany(x => x.Groupe.Select(e => e.Machine))
                                  .Distinct().OrderBy(x => x).ToList();
            return new Correlation
            {
                Kind = "divergence",
                Severity = "info",
                Title = titre,
                Details = repartition
                        + Lang.T($". La plus récente est {plusRecente.Groupe.Key}, présente sur {plusRecente.Groupe.Count()} poste(s) ; les postes nommés ci-dessous ont une version plus ancienne.",
                                 $". The newest is {plusRecente.Groupe.Key}, found on {plusRecente.Groupe.Count()} machine(s); the machines named below have an older version."),
                Action = Lang.T($"Mettre à jour les postes en retard vers la version la plus récente ({plusRecente.Groupe.Key}), ", $"Update the lagging machines to the newest version ({plusRecente.Groupe.Key}), ")
                       + Lang.T("sauf si c'est justement cette version qui a introduit le problème — le rapport de chaque poste le dira.", "unless it is precisely this version that introduced the problem — each machine's own report will say."),
                Machines = enRetard,
            };
        }

        return new Correlation
        {
            Kind = "divergence",
            Severity = "info",
            Title = titre,
            Details = repartition
                    + Lang.T(". Ces numéros de version ne se comparent pas entre eux : le logiciel ne peut pas dire lequel est le plus récent.",
                             ". These version numbers cannot be compared with each other: the software cannot tell which one is the newest."),
            Action = Lang.T("Vérifier chez l'éditeur quelle version est la plus récente, puis aligner les autres postes dessus.",
                            "Check with the vendor which version is the newest, then bring the other machines in line with it."),
            Machines = versions.SelectMany(g => g.Select(e => e.Machine)).Distinct().OrderBy(x => x).ToList(),
        };
    }

    internal enum FamillePilote { Constructeur, Windows, Defender }

    /// <summary>
    /// Point 96. Famille d'un pilote, reconnue à ce que Windows en dit lui-même :
    /// la mention « WinBuild » que portent les versions des fichiers livrés avec
    /// Windows (« 10.0.26100.9549 (WinBuild.160101.0800) ») ; pour Defender, un nom
    /// en « Wd » et une version de plateforme « 4.18.x » (WdFilter, WdBoot,
    /// WdNisDrv…). Tout le reste est un pilote de constructeur et garde sa carte.
    /// </summary>
    internal static FamillePilote Famille(string fichier, IEnumerable<string> versions)
    {
        var liste = versions.ToList();
        if (liste.Any(v => v.Contains("(WinBuild.", StringComparison.OrdinalIgnoreCase)))
            return FamillePilote.Windows;
        if (fichier.StartsWith("Wd", StringComparison.OrdinalIgnoreCase)
            && liste.All(v => v.TrimStart().StartsWith("4.18.", StringComparison.Ordinal)))
            return FamillePilote.Defender;
        return FamillePilote.Constructeur;
    }

    /// <summary>Postes dont la version de ce fichier est plus ancienne que la plus récente observée — vide si les versions ne se comparent pas.</summary>
    private static List<string> EnRetard(List<IGrouping<string, (string Machine, string Version, DateTime? Date)>> versions)
    {
        var numeros = versions.Select(g => (Groupe: g, Numero: NumeroDeVersion(g.Key))).ToList();
        if (!numeros.All(x => x.Numero is not null)) return new();
        var max = numeros.Max(x => x.Numero)!;
        return numeros.Where(x => x.Numero < max).SelectMany(x => x.Groupe.Select(e => e.Machine)).Distinct().ToList();
    }

    private static void AjouterGroupe(ParkAnalysis a,
        List<(string Fichier, List<IGrouping<string, (string Machine, string Version, DateTime? Date)>> Versions)> groupe,
        FamillePilote famille)
    {
        if (groupe.Count == 0) return;
        if (groupe.Count == 1) { a.Correlations.Add(Divergence(groupe[0].Fichier, groupe[0].Versions)); return; }

        // Pour chaque poste : sur combien de ces fichiers il est en retard.
        var retards = groupe.SelectMany(d => EnRetard(d.Versions))
                            .GroupBy(m => m, StringComparer.OrdinalIgnoreCase)
                            .OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
                            .ToList();
        var fichiers = groupe.Select(d => d.Fichier).OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToList();
        var exemples = string.Join(", ", fichiers.Take(5)) + (fichiers.Count > 5 ? Lang.T($" et {fichiers.Count - 5} autre(s)", $" and {fichiers.Count - 5} more") : "");
        var postes = retards.Count == 0
            ? Lang.T(" Les numéros de version ne permettent pas de dire quel poste est en retard.", " The version numbers do not tell which machine is behind.")
            : Lang.T(" Postes en retard sur au moins un de ces fichiers : ", " Machines behind on at least one of these files: ")
              + string.Join(", ", retards.Select(g => Lang.T($"{g.Key} ({g.Count()} fichier(s))", $"{g.Key} ({g.Count()} file(s))"))) + ".";

        bool windows = famille == FamillePilote.Windows;
        a.Correlations.Add(new Correlation
        {
            Kind = "divergence",
            Severity = "info",
            Title = windows
                ? Lang.T($"{fichiers.Count} pilotes de Windows en versions différentes : le niveau de mise à jour de Windows n'est pas le même partout",
                         $"{fichiers.Count} Windows drivers in different versions: Windows is not at the same update level everywhere")
                : Lang.T($"{fichiers.Count} pilotes de Microsoft Defender en versions différentes : la plateforme Defender n'est pas la même partout",
                         $"{fichiers.Count} Microsoft Defender drivers in different versions: the Defender platform is not the same everywhere"),
            Details = (windows
                ? Lang.T("Ces pilotes sont livrés avec Windows et changent tous ensemble à chaque mise à jour cumulative : un écart sur l'un est un écart sur tous, une seule carte le dit. ",
                         "These drivers ship with Windows and all change together with each cumulative update: a gap on one is a gap on all, so a single card says it. ")
                : Lang.T("Ces pilotes appartiennent à l'antivirus de Windows et changent ensemble quand sa plateforme se met à jour : une seule carte le dit. ",
                         "These drivers belong to the Windows antivirus and change together when its platform updates: a single card says it. "))
                + Lang.T($"Fichiers concernés : {exemples}.", $"Files involved: {exemples}.")
                + postes,
            Action = windows
                ? Lang.T("Installer les mises à jour de Windows sur les postes nommés : leurs pilotes suivront. Un poste en retard sur quelques fichiers seulement a souvent une mise à jour facultative non installée.",
                         "Install the Windows updates on the machines named: their drivers will follow. A machine behind on only a few files often has an optional update not installed.")
                : Lang.T("Vérifier que Windows Update s'applique sur les postes nommés : c'est par lui que la plateforme Defender se met à jour.",
                         "Check that Windows Update is applied on the machines named: that is how the Defender platform gets updated."),
            Machines = retards.Select(g => g.Key).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList(),
        });
    }

    /// <summary>
    /// Le numéro en tête d'une version de fichier : « 10.0.26100.9549 (WinBuild…) »,
    /// « 6.0.8923.1 built by: WinDDK », « 4.18.26080.4 (empreinte) ». Null si la
    /// chaîne ne commence pas par un numéro à deux à quatre parties.
    /// </summary>
    internal static Version? NumeroDeVersion(string texte)
    {
        var m = System.Text.RegularExpressions.Regex.Match(texte ?? "", @"^\s*(\d+(?:\.\d+){1,3})");
        return m.Success && Version.TryParse(m.Groups[1].Value, out var v) ? v : null;
    }

    private static int Rank(string severity) => severity switch { "crit" => 0, "warn" => 1, _ => 2 };

    /// <summary>Le résumé stocke « version|aaaa-mm-jj » ; on le redécompose.</summary>
    private static (string Version, DateTime? Date) Unpack(string packed)
    {
        var parts = (packed ?? "").Split('|');
        var version = parts.Length > 0 ? parts[0].Trim() : "";
        // InvariantCulture explicite : cette valeur est ÉCRITE par une machine et
        // RELUE par une autre. Un poste dont les paramètres régionaux diffèrent de
        // ceux de la console ne doit pas disparaître silencieusement du comparateur.
        DateTime? date = parts.Length > 1 && DateTime.TryParse(
            parts[1], System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var d) ? d : null;
        return (version, date);
    }
}
