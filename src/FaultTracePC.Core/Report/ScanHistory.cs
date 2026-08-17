using System.Text.Json;
using System.Text.Json.Serialization;

namespace FaultTracePC.Core.Report;

/// <summary>
/// Historique des scans : chaque analyse est résumée en JSON dans
/// Documents\FaultTracePC\Historique, et le scan suivant se compare au précédent
/// pour répondre à LA question qui suit une réparation : « est-ce que c'est réglé ? ».
/// </summary>
public static class ScanHistory
{
    private static string HistoryDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "FaultTracePC", "Historique");

    // ------------------------------------------------------------------
    // Résumé persisté (volontairement compact : l'essentiel pour comparer)
    // ------------------------------------------------------------------

    public sealed class ScanSummary
    {
        public DateTime GeneratedAt { get; set; }
        public int ScanPeriodDays { get; set; }
        public List<BsodBrief> Bsods { get; set; } = new();
        public Dictionary<string, string> DriverVersions { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public List<DiskBrief> Disks { get; set; } = new();
        public int DiskErrorEvents { get; set; }
        public int WheaEvents { get; set; }
        public int AppCrashEvents { get; set; }
        public double MemUsedPct { get; set; }
        public long VirtualizationBytes { get; set; }
        public List<string> CriticalFindings { get; set; } = new();
    }

    public sealed class BsodBrief
    {
        public DateTime Time { get; set; }
        public uint? Code { get; set; }
        public string? Driver { get; set; }
    }

    public sealed class DiskBrief
    {
        public string Model { get; set; } = "";
        public DiskHealth Health { get; set; } = DiskHealth.NotReported;
        public int? TemperatureC { get; set; }
        public int? WearPercent { get; set; }
        public ulong? ReadErrorsTotal { get; set; }
        /// <summary>Secteurs défectueux cumulés : leur ÉVOLUTION est le vrai signal d'alarme.</summary>
        public ulong? BadSectors { get; set; }
        public ulong? CrcErrors { get; set; }
    }

    // ------------------------------------------------------------------

    public static ScanSummary Summarize(DiagnosticReport r)
    {
        var os = r.System.Os;
        return new ScanSummary
        {
            GeneratedAt = r.GeneratedAt,
            ScanPeriodDays = r.ScanPeriodDays,
            Bsods = r.Bsods.Select(b => new BsodBrief { Time = b.TimeLocal, Code = b.BugCheckCode, Driver = b.SuspectDriver }).ToList(),
            DriverVersions = r.System.Drivers
                .Where(d => !string.IsNullOrEmpty(d.Path) && !string.IsNullOrEmpty(d.FileVersion))
                .GroupBy(d => Path.GetFileName(d.Path), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => $"{g.First().FileVersion}|{g.First().FileDate:yyyy-MM-dd}", StringComparer.OrdinalIgnoreCase),
            Disks = r.System.Disks.Select(d => new DiskBrief
            {
                Model = d.Model, Health = d.Health,
                TemperatureC = d.TemperatureC, WearPercent = d.WearPercent, ReadErrorsTotal = d.ReadErrorsTotal,
                BadSectors = d.Smart?.BadSectors, CrcErrors = d.Smart?.UdmaCrcErrors,
            }).ToList(),
            DiskErrorEvents = r.Events.Count(e => e.Category == EventCategory.DiskError),
            WheaEvents = r.Events.Count(e => e.Category == EventCategory.Whea),
            AppCrashEvents = r.Events.Count(e => e.Category == EventCategory.AppCrash),
            MemUsedPct = os.TotalVisibleMemoryKB == 0 ? 0
                : Math.Round(100.0 * (os.TotalVisibleMemoryKB - os.FreePhysicalMemoryKB) / os.TotalVisibleMemoryKB, 1),
            VirtualizationBytes = Analysis.RulesEngine.VirtualizationBytes(r),
            CriticalFindings = r.Findings.Where(f => f.Severity == Severity.Critical).Select(f => f.Title).ToList(),
        };
    }

    /// <summary>Durée au-delà de laquelle un résumé peut être supprimé.</summary>
    private const int RetentionDays = 90;

    /// <summary>
    /// Nombre de résumés conservés quoi qu'il arrive, même très anciens.
    ///
    /// Purger uniquement sur l'âge ferait perdre son scan précédent à une machine
    /// analysée une fois par an — et avec lui la réponse à « est-ce que c'est
    /// réglé ? », qui est la raison d'être de cet historique. Dix suffisent à
    /// garantir la continuité ; au-delà, sur une machine peu analysée, une pente
    /// calculée sur des points étalés sur dix ans ne serait plus une tendance.
    /// </summary>
    private const int RetentionMinimumCount = 10;

    /// <summary>Sauvegarde le résumé du scan courant (après analyse).</summary>
    public static void Save(DiagnosticReport r, List<string> errors)
    {
        try
        {
            Directory.CreateDirectory(HistoryDir);
            var path = Path.Combine(HistoryDir, $"Scan_{r.GeneratedAt:yyyy-MM-dd_HHmmss}.json");
            var json = JsonSerializer.Serialize(Summarize(r), JsonOpts);
            File.WriteAllText(path, json);
        }
        catch (Exception ex) { errors.Add(Lang.T($"Historique des scans (écriture) : {ex.Message}",
                                                $"Scan history (write): {ex.Message}")); }

        if (Purge() is { } note) r.Notes.Add(note);
    }

    /// <summary>
    /// Supprime les résumés à la fois PLUS VIEUX que la rétention ET au-delà des
    /// derniers conservés — les deux conditions réunies, jamais une seule.
    ///
    /// Retourne une phrase décrivant ce qui a été supprimé, ou null si rien ne
    /// l'a été. La suppression est annoncée dans le rapport : effacer en silence
    /// des données de l'utilisateur détonnerait avec un logiciel qui ne fait rien
    /// d'irréversible sans le dire.
    /// </summary>
    internal static string? Purge(DateTime? now = null)
    {
        try
        {
            if (!Directory.Exists(HistoryDir)) return null;

            var fichiers = Directory.EnumerateFiles(HistoryDir, "Scan_*.json")
                                    .Select(f => (Path: f, Modifie: File.GetLastWriteTime(f)))
                                    .ToList();

            var candidats = ACandidats(fichiers, now ?? DateTime.Now);
            if (candidats.Count == 0) return null;

            var supprimes = 0;
            foreach (var f in candidats)
            {
                try { File.Delete(f); supprimes++; } catch { /* fichier verrouillé : on réessaiera au prochain scan */ }
            }
            if (supprimes == 0) return null;

            return Lang.T(
                $"Historique : {supprimes} résumé(s) de plus de {RetentionDays} jours supprimé(s). "
                + $"Les {RetentionMinimumCount} plus récents sont conservés quel que soit leur âge, "
                + "pour qu'une comparaison reste toujours possible.",
                $"History: {supprimes} summary(ies) older than {RetentionDays} days deleted. "
                + $"The {RetentionMinimumCount} most recent ones are kept whatever their age, "
                + "so that a comparison always stays possible.");
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Résumé du dernier scan de cette machine, ou null si elle n'a jamais été
    /// analysée. C'est ce que le mode parc rapatrie pour comparer les postes entre
    /// eux : il contient déjà versions de pilotes, crashs, disques et conclusions
    /// critiques, sans rien exposer de nominatif.
    /// </summary>
    public static ScanSummary? LoadLatest()
    {
        try
        {
            if (!Directory.Exists(HistoryDir)) return null;
            foreach (var file in Directory.EnumerateFiles(HistoryDir, "Scan_*.json").OrderByDescending(f => f))
            {
                try
                {
                    if (JsonSerializer.Deserialize<ScanSummary>(File.ReadAllText(file), JsonOpts) is { } s) return s;
                }
                catch { /* fichier corrompu : on essaie le précédent */ }
            }
        }
        catch { /* historique illisible */ }
        return null;
    }

    /// <summary>Charge le résumé du scan le plus récent AVANT le scan courant.</summary>
    public static ScanSummary? LoadPrevious(DateTime before, List<string> errors)
    {
        try
        {
            if (!Directory.Exists(HistoryDir)) return null;
            foreach (var file in Directory.EnumerateFiles(HistoryDir, "Scan_*.json").OrderByDescending(f => f))
            {
                try
                {
                    var s = JsonSerializer.Deserialize<ScanSummary>(File.ReadAllText(file), JsonOpts);
                    if (s is not null && s.GeneratedAt < before.AddMinutes(-1)) return s;
                }
                catch { /* fichier corrompu : on passe au précédent */ }
            }
        }
        catch (Exception ex) { errors.Add(Lang.T($"Historique des scans (lecture) : {ex.Message}",
                                                $"Scan history (read): {ex.Message}")); }
        return null;
    }

    // ------------------------------------------------------------------
    // Comparaison
    // ------------------------------------------------------------------

    public static ScanComparison? CompareWithPrevious(DiagnosticReport r, List<string> errors)
    {
        var prev = LoadPrevious(r.GeneratedAt, errors);
        if (prev is null) return null;
        return Compare(r, prev);
    }

    /// <summary>
    /// Cœur de la comparaison, sans aucun accès disque.
    ///
    /// Séparé de <see cref="CompareWithPrevious"/> pour être testable : le verdict
    /// est ce que l'utilisateur lit en premier, c'est donc la dernière chose qui
    /// devrait dépendre du contenu de son dossier Documents pour être vérifiée.
    /// </summary>
    internal static ScanComparison Compare(DiagnosticReport r, ScanSummary prev)
    {
        var c = new ScanComparison { PreviousScanAt = prev.GeneratedAt };

        // Nouveaux crashs = postérieurs au scan précédent.
        var newBsods = r.Bsods.Where(b => b.TimeLocal > prev.GeneratedAt).ToList();
        c.NewBsodCount = newBsods.Count;
        c.NewBsods = newBsods.Select(b =>
            $"{Lang.ShortDateMinute(b.TimeLocal)} — {b.BugCheckName}{(b.SuspectDriver is not null ? $" ({b.SuspectDriver})" : "")}").ToList();

        // Même signature qu'avant ? (code ou pilote déjà vus)
        var prevCodes = prev.Bsods.Where(b => b.Code is not null).Select(b => b.Code!.Value).ToHashSet();
        var prevDrivers = prev.Bsods.Where(b => b.Driver is not null).Select(b => b.Driver!).ToHashSet(StringComparer.OrdinalIgnoreCase);
        c.SameSignatureRecurred = newBsods.Any(b =>
            (b.BugCheckCode is not null && prevCodes.Contains(b.BugCheckCode.Value)) ||
            (b.SuspectDriver is not null && prevDrivers.Contains(b.SuspectDriver)));

        // Pilotes mis à jour depuis le scan précédent.
        // (le dictionnaire relu du JSON perd son comparateur : on le reconstruit insensible à la casse)
        var prevDriverVersions = new Dictionary<string, string>(prev.DriverVersions, StringComparer.OrdinalIgnoreCase);
        foreach (var (sys, cur) in Summarize(r).DriverVersions)
        {
            if (prevDriverVersions.TryGetValue(sys, out var old) && old != cur)
                c.DriverUpdates.Add(Lang.T($"{sys} : {old.Split('|')[0]} → {cur.Split('|')[0]}", $"{sys}: {old.Split('|')[0]} → {cur.Split('|')[0]}"));
        }
        if (c.DriverUpdates.Count > 12) c.DriverUpdates = c.DriverUpdates.Take(12).ToList();

        // Évolution des disques (santé, usure, température, erreurs de lecture).
        //
        // Deux listes sont alimentées ici, et il ne faut pas les confondre :
        //   - DiskChanges      : TOUT ce qui a bougé, y compris anodin — c'est le détail affiché.
        //   - HardwareConcerns : seulement ce qui s'est DÉGRADÉ — c'est ce qui pèse sur le verdict.
        // Une température qui varie ou une santé qui redevient « Sain » appartiennent
        // à la première et pas à la seconde.
        foreach (var d in r.System.Disks)
        {
            var old = prev.Disks.FirstOrDefault(x => x.Model.Equals(d.Model, StringComparison.OrdinalIgnoreCase));
            if (old is null) continue;

            if (old.Health != DiskHealth.NotReported && d.Health != DiskHealth.NotReported && old.Health != d.Health)
            {
                c.DiskChanges.Add(Lang.T($"{d.Model} : santé {old.Health.Label()} → {d.Health.Label()}",
                                         $"{d.Model}: health {old.Health.Label()} → {d.Health.Label()}"));
                // Seule une AGGRAVATION compte. Un disque qui repasse de « Avertissement »
                // à « Sain » ne doit pas déclencher d'alarme, et un état inconnu ne permet
                // de conclure ni dans un sens ni dans l'autre — c'est Rank() qui refuse de
                // classer « inconnu » et « non mesuré », en renvoyant -1.
                int before = old.Health.Rank(), after = d.Health.Rank();
                if (before >= 0 && after > before)
                    AddConcern(c, after >= 2 ? "crit" : "warn",
                        Lang.T($"{d.Model} : l'état de santé rapporté par le disque est passé de « {old.Health.Label()} » à « {d.Health.Label()} » depuis le scan précédent.",
                               $"{d.Model}: the health status reported by the drive went from “{old.Health.Label()}” to “{d.Health.Label()}” since the previous scan."));
            }

            if (old.WearPercent is { } ow && d.WearPercent is { } nw && nw > ow)
            {
                c.DiskChanges.Add(Lang.T($"{d.Model} : usure {ow} % → {nw} %",
                                         $"{d.Model}: wear {ow}% → {nw}%"));
                // Un point d'usure de plus sur un SSD est le fonctionnement normal.
                // Une progression franche entre deux scans, non.
                if (nw - ow >= 2)
                    AddConcern(c, "warn",
                        Lang.T($"{d.Model} : l'usure du SSD est passée de {ow} % à {nw} % entre deux scans — une progression rapide à ce rythme raccourcit nettement la durée de vie annoncée.",
                               $"{d.Model}: SSD wear went from {ow}% to {nw}% between two scans — sustained at that rate, it markedly shortens the announced lifespan."));
            }

            if (old.ReadErrorsTotal is { } oe && d.ReadErrorsTotal is { } ne && ne > oe)
            {
                c.DiskChanges.Add(Lang.T($"{d.Model} : erreurs de lecture {oe} → {ne}",
                                         $"{d.Model}: read errors {oe} → {ne}"));
                AddConcern(c, "warn",
                    Lang.T($"{d.Model} : {ne - oe} nouvelle(s) erreur(s) de lecture depuis le scan précédent. Le disque a dû s'y reprendre à plusieurs fois pour relire des données.",
                           $"{d.Model}: {ne - oe} new read error(s) since the previous scan. The drive had to retry several times to read data back."));
            }

            // L'augmentation des secteurs défectueux est LE signal d'un disque qui meurt.
            if (old.BadSectors is { } ob && d.Smart?.BadSectors is { } nb && nb > ob)
            {
                c.DiskChanges.Add(Lang.T($"⚠ {d.Model} : secteurs défectueux {ob} → {nb} — dégradation en cours, sauvegarder",
                                         $"⚠ {d.Model}: bad sectors {ob} → {nb} — degrading now, back up"));
                AddConcern(c, "crit",
                    Lang.T($"{d.Model} : les secteurs défectueux sont passés de {ob} à {nb}. Un disque qui en perd entre deux scans est en train de se dégrader, même quand son propre auto-diagnostic se déclare sain — c'est la PROGRESSION qui alerte, pas le nombre atteint. Sauvegardez maintenant, avant toute autre manipulation.",
                           $"{d.Model}: bad sectors went from {ob} to {nb}. A drive losing sectors between two scans is degrading, even when its own self-diagnosis declares itself healthy — what raises the alarm is the PROGRESSION, not the number reached. Back up now, before anything else."));
            }

            if (old.CrcErrors is { } oc && d.Smart?.UdmaCrcErrors is { } nc && nc > oc)
            {
                c.DiskChanges.Add(Lang.T($"{d.Model} : erreurs de câble (CRC) {oc} → {nc}",
                                         $"{d.Model}: cable errors (CRC) {oc} → {nc}"));
                // Ce compteur accuse la LIAISON, jamais le disque. Le confondre avec une
                // usure conduit à remplacer un disque sain à la place d'un câble à 5 €.
                AddConcern(c, "warn",
                    Lang.T($"{d.Model} : {nc - oc} nouvelle(s) erreur(s) CRC. Ce compteur met en cause la LIAISON, pas le disque : câble SATA, connecteur ou alimentation. Le disque lui-même peut être parfaitement sain — changez le câble avant d'envisager de le remplacer.",
                           $"{d.Model}: {nc - oc} new CRC error(s). This counter blames the LINK, not the drive: SATA cable, connector or power supply. The drive itself may be perfectly healthy — change the cable before considering replacing it."));
            }

            if (old.TemperatureC is { } ot && d.TemperatureC is { } nt && Math.Abs(nt - ot) >= 8)
                c.DiskChanges.Add(Lang.T($"{d.Model} : température {ot} °C → {nt} °C",
                                         $"{d.Model}: temperature {ot} °C → {nt} °C"));
        }

        // Événements disque/WHEA apparus depuis le scan précédent.
        c.NewDiskErrorEvents = r.Events.Count(e => e.Category == EventCategory.DiskError && e.TimeLocal > prev.GeneratedAt);
        c.NewWheaEvents = r.Events.Count(e => e.Category == EventCategory.Whea && e.TimeLocal > prev.GeneratedAt);

        // Ces deux compteurs étaient déjà affichés, mais n'entraient pas dans la
        // conclusion : une machine accumulant des erreurs matérielles sans planter
        // était déclarée stable. Ils comptent désormais.
        //
        // Sévérité « warn » et non « crit » : le détail et la gravité réelle sont
        // établis par le moteur de règles, qui sait distinguer une erreur corrigée
        // d'une erreur fatale. Ici on constate seulement une évolution défavorable.
        if (c.NewWheaEvents > 0)
            AddConcern(c, "warn",
                Lang.T($"{c.NewWheaEvents} nouvelle(s) erreur(s) matérielle(s) (WHEA) enregistrée(s) depuis le scan précédent — le matériel signale des incidents que Windows a pour l'instant absorbés.",
                       $"{c.NewWheaEvents} new hardware error(s) (WHEA) recorded since the previous scan — the hardware is reporting incidents that Windows has absorbed so far."));

        if (c.NewDiskErrorEvents > 0)
            AddConcern(c, "warn",
                Lang.T($"{c.NewDiskErrorEvents} nouvelle(s) erreur(s) disque dans le journal Windows depuis le scan précédent.",
                       $"{c.NewDiskErrorEvents} new disk error(s) in the Windows event log since the previous scan."));

        // Tendance mémoire (virtualisation).
        var curVm = Analysis.RulesEngine.VirtualizationBytes(r);
        if (prev.VirtualizationBytes > 0 || curVm > 0)
        {
            var deltaGb = (curVm - prev.VirtualizationBytes) / 1024.0 / 1024 / 1024;
            if (Math.Abs(deltaGb) >= 1)
                c.MemoryTrend = Lang.T(
                    $"Virtualisation (vmmem) : {(deltaGb > 0 ? "+" : "")}{deltaGb:0.#} Go depuis le dernier scan.",
                    // Culture explicite : sans elle, « 1.5 GB » sortirait « 1,5 GB »
                    // sur un Windows français basculé en anglais.
                    $"Virtualisation (vmmem): {(deltaGb > 0 ? "+" : "")}{deltaGb.ToString("0.#", Lang.Culture)} GB since the last scan.");
        }

        // ------------------------------------------------------------------
        // Synthèse honnête.
        //
        // RÈGLE : le verdict ne parle pas QUE des plantages.
        //
        // Jusqu'à la 1.2.0, ce bloc ne regardait que les crashs. Conséquence : une
        // machine n'ayant jamais planté mais dont le disque perdait des secteurs
        // recevait une carte verte titrée « Machine stable », l'avertissement étant
        // relégué en petit dessous. C'est le seul cas où l'utilisateur n'a aucun
        // autre signal pour se méfier — donc le pire endroit possible pour le
        // rassurer. Le volet matériel peut désormais faire basculer le verdict à
        // lui seul.
        // ------------------------------------------------------------------
        bool prevHadProblems = prev.Bsods.Count > 0 || prev.CriticalFindings.Count > 0;

        // Un problème critique TOUJOURS présent, mais qui n'empire pas, ne produit
        // aucune « dégradation » : sans ce test, « rien n'a bougé » se dirait
        // « tout va bien ».
        bool standingCritical = r.Findings.Any(f => f.Severity == Severity.Critical);

        string crashTone;
        string crashSentence;
        if (c.SameSignatureRecurred)
        {
            crashTone = "crit";
            // Date : jj/mm/aaaa en français, aaaa-mm-jj en anglais. « 03/04/2026 »
            // se lit à l'envers d'un pays à l'autre ; la forme ISO ne s'ambiguïse nulle part.
            crashSentence = Lang.T(
                $"Le problème PERSISTE : un nouveau crash avec la même signature qu'au scan du {prev.GeneratedAt:dd/MM/yyyy} s'est produit. La réparation n'a pas suffi.",
                $"The problem PERSISTS: a new crash with the same signature as in the scan of {prev.GeneratedAt:yyyy-MM-dd} occurred. The repair was not enough.");
        }
        else if (c.NewBsodCount > 0)
        {
            crashTone = "warn";
            crashSentence = Lang.T(
                $"{c.NewBsodCount} nouveau(x) crash(s) depuis le scan du {prev.GeneratedAt:dd/MM/yyyy}, mais avec une signature DIFFÉRENTE : l'ancien problème semble réglé, un nouveau est apparu.",
                $"{c.NewBsodCount} new crash(es) since the scan of {prev.GeneratedAt:yyyy-MM-dd}, but with a DIFFERENT signature: the old problem seems fixed, a new one has appeared.");
        }
        else if (prevHadProblems)
        {
            // Trois paliers, et non deux.
            //
            // Sous deux heures, la machine n'a pas eu le temps de reproduire quoi que
            // ce soit : « aucun nouveau crash » y est exact et vide de sens. Deux scans
            // à dix minutes d'intervalle affichaient « Bon signe » — un utilisateur en
            // a conclu, à tort, que quelque chose s'était amélioré entre les deux.
            var ecoule = r.GeneratedAt - prev.GeneratedAt;
            var days = ecoule.TotalDays;
            crashTone = "ok";

            if (ecoule < TimeSpan.FromHours(2))
            {
                crashSentence = Lang.T(
                    $"Scan précédent il y a {Humaniser(ecoule)} seulement : "
                    + "trop récent pour conclure quoi que ce soit. Une comparaison n'a de sens qu'après "
                    + "plusieurs heures d'utilisation normale.",
                    $"Previous scan only {Humaniser(ecoule)} ago: "
                    + "far too recent to conclude anything. A comparison only makes sense after "
                    + "several hours of normal use.");
            }
            else
            {
                crashSentence = Lang.T(
                    $"Aucun nouveau crash depuis le scan du {prev.GeneratedAt:dd/MM/yyyy} ({days:0.#} jour(s)). "
                    + (days >= 7 ? "La réparation semble efficace." : "Bon signe — à confirmer sur la durée (recommandé : re-scanner après une semaine d'utilisation normale)."),
                    $"No new crash since the scan of {prev.GeneratedAt:yyyy-MM-dd} ({days.ToString("0.#", Lang.Culture)} day(s)). "
                    + (days >= 7 ? "The repair looks effective." : "Good sign — to be confirmed over time (recommended: scan again after a week of normal use)."));
            }
        }
        else
        {
            crashTone = "ok";
            // « Machine stable » n'est affirmé que si rien d'autre ne le contredit :
            // sinon on se contente de constater l'absence de crash.
            crashSentence = (c.HardwareConcerns.Count > 0 || standingCritical)
                ? Lang.T($"Aucun crash système avant comme après le scan du {prev.GeneratedAt:dd/MM/yyyy}.",
                         $"No system crash either before or after the scan of {prev.GeneratedAt:yyyy-MM-dd}.")
                : Lang.T($"Machine stable depuis le scan du {prev.GeneratedAt:dd/MM/yyyy} : aucun crash avant comme après.",
                         $"Machine stable since the scan of {prev.GeneratedAt:yyyy-MM-dd}: no crash before or after.");
        }

        // La tonalité retenue est la PIRE des deux : matériel ou plantages.
        c.Tone = ToneRank(c.HardwareSeverity) > ToneRank(crashTone) ? c.HardwareSeverity : crashTone;

        if (c.HardwareConcerns.Count > 0)
        {
            // La dégradation la plus grave est reprise dans le titre : c'est elle
            // que l'utilisateur doit lire en premier, pas en note de bas de carte.
            var worst = c.HardwareConcerns.OrderByDescending(h => ToneRank(h.Severity)).First();
            c.Assessment = crashSentence + (c.HardwareSeverity == "crit"
                ? Lang.T(" En revanche, le MATÉRIEL se dégrade : ", " However, the HARDWARE is degrading: ") + worst.Message
                : Lang.T(" Un point de vigilance matériel : ", " One hardware point to watch: ") + worst.Message);
        }
        else if (standingCritical && crashTone == "ok")
        {
            c.Assessment = crashSentence
                + Lang.T(" En revanche, un problème critique signalé dans ce rapport est toujours là : rien ne s'est aggravé depuis le dernier scan, mais rien n'est réglé non plus.",
                         " However, a critical problem reported here is still present: nothing has got worse since the last scan, but nothing is fixed either.");
            c.Tone = "warn";
        }
        else
        {
            c.Assessment = crashSentence;
        }

        return c;
    }

    /// <summary>
    /// Règle de purge, isolée de l'accès disque pour être vérifiable.
    ///
    /// Un fichier n'est candidat que s'il remplit les DEUX conditions : plus vieux
    /// que la rétention, ET au-delà des N derniers. Une seule des deux ne suffit
    /// jamais — c'est ce qui garantit qu'une machine analysée une fois par an
    /// conserve de quoi se comparer.
    /// </summary>
    internal static List<string> ACandidats(IEnumerable<(string Path, DateTime Modifie)> fichiers, DateTime now)
    {
        var limite = now.AddDays(-RetentionDays);

        // Tri par nom : les fichiers sont nommés Scan_aaaa-MM-jj_HHmmss.json, donc
        // l'ordre alphabétique décroissant est l'ordre chronologique inverse.
        return fichiers
            .OrderByDescending(f => f.Path, StringComparer.Ordinal)
            .Skip(RetentionMinimumCount)
            .Where(f => f.Modifie < limite)
            .Select(f => f.Path)
            .ToList();
    }

    /// <summary>Durée courte en langage humain : « 12 minutes », « 1 h 40 ».</summary>
    private static string Humaniser(TimeSpan d) =>
        d.TotalMinutes < 1 ? Lang.T("moins d'une minute", "less than a minute")
        : d.TotalMinutes < 60 ? Lang.T($"{(int)d.TotalMinutes} minute(s)", $"{(int)d.TotalMinutes} minute(s)")
        : Lang.T($"{(int)d.TotalHours} h {d.Minutes:00}", $"{(int)d.TotalHours}h{d.Minutes:00}");

    /// <summary>
    /// Enregistre une dégradation et relève la sévérité globale si nécessaire.
    /// </summary>
    private static void AddConcern(ScanComparison c, string severity, string message)
    {
        c.HardwareConcerns.Add(new HardwareConcern { Severity = severity, Message = message });
        if (ToneRank(severity) > ToneRank(c.HardwareSeverity)) c.HardwareSeverity = severity;
    }

    /// <summary>Ordre de gravité des tonalités d'affichage.</summary>
    private static int ToneRank(string? tone) => tone switch { "crit" => 2, "warn" => 1, _ => 0 };

    /// <summary>
    /// Gravité d'un état de santé disque, pour ne réagir qu'aux AGGRAVATIONS.
    /// Renvoie -1 quand la valeur est inconnue : on préfère ne rien conclure
    /// plutôt que de conclure à tort.
    ///
    /// Les libellés anglais sont acceptés en plus des français : le collecteur les
    /// traduit aujourd'hui, mais un rapport relu depuis un historique plus ancien
    /// ou produit par une future version anglaise ne doit pas passer à travers.
    /// </summary>
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}
