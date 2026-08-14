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
                ? "Aucun poste n'a encore transmis de résumé d'analyse : le comparateur n'a rien à comparer."
                : "Un seul poste dispose d'un résumé d'analyse. La comparaison de parc demande au moins deux machines — "
                + "c'est précisément ce qu'elle apporte par rapport à un diagnostic individuel.";
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
                    Title = $"{oldOnes.Count} poste(s) sur {machines.Count} partagent le même pilote ancien : {file}",
                    Details = $"Version la plus ancienne datée du {oldest:dd/MM/yyyy}"
                            + (kb is not null ? $". Ce pilote appartient à {kb.Owner}. {kb.Context}" : ". Ce pilote n'est pas documenté dans la base de FaultTracePC.")
                            + " Un pilote identique sur plusieurs postes vient presque toujours de l'image de déploiement ou d'une installation groupée : "
                            + "la corriger une fois règle le problème partout.",
                    Action = kb?.Fix ?? "Identifier le logiciel ou le matériel associé, puis mettre à jour l'ensemble du parc en une opération.",
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
                    Title = $"{file} existe en {versions.Count} versions différentes sur le parc",
                    Details = "Répartition : " + string.Join(" · ", versions.Select(g => $"{g.Key} sur {g.Count()} poste(s)"))
                            + ". Une version majoritaire et quelques retardataires : ce sont en général ces derniers qui remontent des incidents.",
                    Action = $"Aligner les postes en retard sur la version majoritaire ({versions[0].Key}), "
                           + "sauf si c'est justement la version récente qui a introduit le problème — le rapport de chaque poste le dira.",
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
                Title = $"Le même écran bleu frappe {names.Count} postes : {BugCheckCatalog.NameOf(g.Key)}",
                Details = $"Code 0x{g.Key:X8}, observé sur {names.Count} machines distinctes. "
                        + "Un code d'arrêt identique sur plusieurs postes désigne une cause partagée — pilote déployé en masse, "
                        + "mise à jour commune, ou modèle de matériel identique — et non un incident isolé.",
                Action = "Comparer les rapports de ces postes pour isoler ce qu'ils ont en commun et que les autres n'ont pas : "
                       + "même pilote, même mise à jour récente, même modèle de machine.",
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
                Title = $"Le pilote {g.Key} est mis en cause sur {names.Count} postes",
                Details = (kb is not null ? $"{kb.Owner} — {kb.Context} " : "")
                        + "L'analyse symbolique le désigne sur plusieurs machines : ce n'est plus une coïncidence.",
                Action = kb?.Fix ?? "Mettre ce pilote à jour sur l'ensemble du parc, ou retirer le logiciel qui l'installe.",
                Machines = names,
            });
        }

        // ---------------------------------------------------------------
        // 5. Même modèle de disque en dégradation
        // ---------------------------------------------------------------
        var byDisk = machines
            .SelectMany(m => m.Summary.Disks
                .Where(d => (d.BadSectors ?? 0) > 0 || d.Health.Equals("Avertissement", StringComparison.OrdinalIgnoreCase)
                                                    || d.Health.Equals("Défaillant", StringComparison.OrdinalIgnoreCase))
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
                Title = $"Le modèle de disque {g.Key} se dégrade sur {names.Count} postes",
                Details = "Plusieurs exemplaires du même modèle montrent des secteurs défectueux ou un état de santé dégradé. "
                        + "Un modèle qui faiblit en série relève du défaut de série ou d'un lot arrivé en fin de vie au même moment, "
                        + "pas de la malchance.",
                Action = "Sauvegarder ces postes en priorité, vérifier l'existence d'une mise à jour de firmware chez le fabricant, "
                       + "et anticiper le remplacement des autres exemplaires du même modèle avant qu'ils ne lâchent à leur tour.",
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
                Kind = "isolé",
                Severity = "warn",
                Title = $"{m.Name} concentre {m.Summary.CriticalFindings.Count} problèmes critiques, bien plus que le reste du parc",
                Details = $"Moyenne du parc : {avgCritical:0.#} problème(s) critique(s) par poste. "
                        + "Concernant ce poste : " + string.Join(" · ", m.Summary.CriticalFindings.Take(5)) + ".",
                Action = "Traiter cette machine individuellement : son problème lui est propre et ne se réglera pas par une action de parc.",
                Machines = [m.Name],
            });

        // ---------------------------------------------------------------

        int crit = a.Correlations.Count(c => c.Severity == "crit");
        a.Summary = a.Correlations.Count == 0
            ? $"Aucune corrélation entre les {machines.Count} postes analysés : les problèmes éventuels sont propres à chaque machine."
            : crit > 0
                ? $"{crit} corrélation(s) critique(s) sur {machines.Count} postes : une même cause touche plusieurs machines, "
                + "et une seule action peut donc les corriger ensemble."
                : $"{a.Correlations.Count} point(s) de convergence relevés sur {machines.Count} postes, sans gravité immédiate.";

        a.Correlations.Sort((x, y) => Rank(x.Severity).CompareTo(Rank(y.Severity)));
        return a;
    }

    private static int Rank(string severity) => severity switch { "crit" => 0, "warn" => 1, _ => 2 };

    /// <summary>Le résumé stocke « version|aaaa-mm-jj » ; on le redécompose.</summary>
    private static (string Version, DateTime? Date) Unpack(string packed)
    {
        var parts = (packed ?? "").Split('|');
        var version = parts.Length > 0 ? parts[0].Trim() : "";
        DateTime? date = parts.Length > 1 && DateTime.TryParse(parts[1], out var d) ? d : null;
        return (version, date);
    }
}
