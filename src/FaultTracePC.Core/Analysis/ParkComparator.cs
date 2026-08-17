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
    }

    /// <summary>Âge à partir duquel un pilote mérite d'être signalé quand il est partagé.</summary>
    private static readonly TimeSpan OldDriver = TimeSpan.FromDays(4 * 365);

    public static ParkAnalysis Analyze(IReadOnlyList<MachineSummary> machines, DateTime? now = null)
    {
        var today = now ?? DateTime.Now;
        var a = new ParkAnalysis { MachineCount = machines.Count, WithSummary = machines.Count };

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
                var behind = versions.Skip(1).SelectMany(g => g.Select(e => e.Machine)).Distinct().OrderBy(x => x).ToList();
                a.Correlations.Add(new Correlation
                {
                    Kind = "divergence",
                    Severity = "info",
                    Title = Lang.T($"{file} existe en {versions.Count} versions différentes sur le parc", $"{file} exists in {versions.Count} different versions across the fleet"),
                    Details = Lang.T("Répartition : ", "Spread: ") + string.Join(" · ", versions.Select(g => Lang.T($"{g.Key} sur {g.Count()} poste(s)", $"{g.Key} on {g.Count()} machine(s)")))
                            + Lang.T(". Une version majoritaire et quelques retardataires : ce sont en général ces derniers qui remontent des incidents.", ". One majority version and a few stragglers: it is usually the latter that report incidents."),
                    Action = Lang.T($"Aligner les postes en retard sur la version majoritaire ({versions[0].Key}), ", $"Bring the lagging machines up to the majority version ({versions[0].Key}), ")
                           + Lang.T("sauf si c'est justement la version récente qui a introduit le problème — le rapport de chaque poste le dira.", "unless it is precisely the newer version that introduced the problem — each machine's own report will say."),
                    Machines = behind,
                });
            }
        }

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
