using System.Globalization;
using FaultTracePC.Core.Collectors;

namespace FaultTracePC.Core.Analysis;

/// <summary>
/// Moteur de corrélation : croise dumps, journaux d'événements, fiabilité et
/// état matériel pour produire des conclusions hiérarchisées, avec un niveau
/// de confiance affiché honnêtement (élevée / moyenne / faible).
/// </summary>
public sealed class RulesEngine
{
    public void Analyze(DiagnosticReport r)
    {
        r.Bsods = BuildIncidents(r);

        AnalyzeBsodPatterns(r);
        AnalyzeFaultingDrivers(r);
        AnalyzeWhea(r);
        AnalyzeMemory(r);
        AnalyzeResourceExhaustion(r);
        AnalyzeMemoryPressureNow(r);
        AnalyzeVirtualizationMemory(r);
        AnalyzeDumpWindow(r);
        AnalyzeFlightRecorder(r);
        AnalyzeSmart(r);
        AnalyzeBattery(r);
        AnalyzeThermal(r);
        AnalyzeEcritureVidage(r);
        AnalyzeStorage(r);
        AnalyzeGpu(r);
        AnalyzePowerLoss(r);
        AnalyzeAppCrashes(r);
        AnalyzeArretsInattendus(r);
        AnalyzeServiceFailures(r);
        AnalyzeUpdateCorrelation(r);
        AnalyzeReseau(r);
        AnalyzeDiskSpace(r);

        if (r.Findings.Count == 0)
        {
            r.Findings.Add(new Finding
            {
                Severity = Severity.Info,
                Confidence = Confidence.High,
                Category = FaultCategory.None,
                Title = Lang.T("Aucune anomalie significative détectée", "No significant anomaly detected"),
                Details = Lang.T($"Aucun BSOD, aucune erreur matérielle WHEA, aucune erreur disque et aucun arrêt inattendu sur les {r.ScanPeriodDays} derniers jours.", $"No BSOD, no WHEA hardware error, no disk error and no unexpected shutdown over the last {r.ScanPeriodDays} days."),
                Recommendation = Lang.T("Si un problème persiste malgré tout, augmenter la période d'analyse ou activer la surveillance temps réel (mode 2) pour capturer le prochain incident.", "If a problem persists anyway, widen the analysis period or turn on real-time monitoring (mode 2) to capture the next incident.")
            });
        }

        // Deux chemins peuvent rapporter le même fait : le journal de Windows et la
        // surveillance temps réel. Le lecteur voyait alors deux problèmes là où il
        // n'y en a qu'un, avec deux gravités contradictoires. Constaté sur un
        // rapport réel du 19/08/2026 : « Erreurs matérielles WHEA détectées (3) »
        // en avertissement, et « Alerte préventive répétée (2×) : erreur matérielle
        // signalée par le processeur » en critique — même matériel, même dernière
        // occurrence. Fait ICI, après toutes les règles : l'ordre dans lequel elles
        // s'exécutent ne doit pas décider de ce qui survit.
        FusionnerLesDoublons(r.Findings);

        // Tri : critiques d'abord, puis avertissements, puis infos.
        r.Findings = r.Findings
            .OrderBy(f => f.Severity)
            .ThenBy(f => f.Confidence)
            .ToList();

        ComputeVerdict(r);
    }

    /// <summary>
    /// Fusionne les conclusions qui portent le MÊME identifiant de fait.
    ///
    /// La conclusion conservée est la plus utile au lecteur : celle qui porte une
    /// recommandation, à défaut celle dont le détail est le plus long. Elle hérite
    /// de la gravité et de la confiance les plus fortes des deux, puis annonce que
    /// le fait vient de deux chemins indépendants — ce qui le confirme au lieu de
    /// le dédoubler.
    ///
    /// PIÈGE À NE PAS REPRODUIRE : dans <see cref="Severity"/> comme dans
    /// <see cref="Confidence"/>, la valeur la plus BASSE est la plus grave et la
    /// plus sûre (Critical = 0, High = 0). Écrire « &gt; » ici dégraderait
    /// silencieusement une conclusion critique en avertissement.
    /// </summary>
    internal static void FusionnerLesDoublons(List<Finding> findings)
    {
        var groupes = findings.Where(f => f.Code.Length > 0)
                              .GroupBy(f => f.Code)
                              .Where(g => g.Count() > 1)
                              .ToList();

        foreach (var groupe in groupes)
        {
            var gardee = groupe.OrderBy(f => string.IsNullOrEmpty(f.Recommendation) ? 1 : 0)
                               .ThenByDescending(f => f.Details.Length)
                               .First();

            foreach (var autre in groupe.Where(f => !ReferenceEquals(f, gardee)))
            {
                if (autre.Severity < gardee.Severity) gardee.Severity = autre.Severity;
                if (autre.Confidence < gardee.Confidence) gardee.Confidence = autre.Confidence;

                // ON N'EFFACE PAS CE QUE L'AUTRE CHEMIN AVAIT CONSTATÉ.
                // Première version de ce code : la carte perdante disparaissait
                // entièrement. Vérifié sur un rapport réel, le rapport y perdait le
                // nombre d'occurrences relevées sur la période (3 événements, quand
                // la carte gardée n'annonçait que 2 alertes) et le matériel nommé —
                // ni l'un ni l'autre n'étant écrits ailleurs en mode simple. Un
                // outil de diagnostic ne jette pas un fait qu'il a collecté, et
                // sous-estimer une magnitude est pire que répéter une date.
                if (!string.IsNullOrWhiteSpace(autre.Details)
                    && !gardee.Details.Contains(autre.Details, StringComparison.Ordinal))
                    gardee.Details = gardee.Details.TrimEnd() + " " + autre.Details.Trim();

                if (string.IsNullOrWhiteSpace(gardee.Recommendation))
                    gardee.Recommendation = autre.Recommendation;

                findings.Remove(autre);
            }

            gardee.Details += Lang.T(
                " Ce fait a été signalé par deux chemins indépendants — le journal de Windows et la surveillance temps réel — ce qui le confirme.",
                " This fact was reported through two independent paths — the Windows log and real-time monitoring — which confirms it.");
        }
    }

    // ------------------------------------------------------------------
    // Construction des incidents BSOD (fusion dumps + événements 1001)
    // ------------------------------------------------------------------

    private static List<BsodIncident> BuildIncidents(DiagnosticReport r)
    {
        var incidents = new List<BsodIncident>();

        foreach (var d in r.Dumps.Where(d =>
                     d.Kind is DumpKind.KernelMinidump or DumpKind.FullMemoryDump && d.BugCheckCode is not null))
        {
            incidents.Add(new BsodIncident
            {
                TimeLocal = d.CrashTimeFromHeader ?? d.LastWriteTime,
                BugCheckCode = d.BugCheckCode,
                Parameters = d.BugCheckParameters,
                BugCheckName = BugCheckCatalog.NameOf(d.BugCheckCode!.Value),
                DumpPath = d.Path,
                // Analyse symbolique (Phase 2) : le module fautif nommé par CDB fait foi.
                SuspectDriver = d.FaultingModule,
                Sources = { d.Kind == DumpKind.FullMemoryDump ? "MEMORY.DMP" : "Minidump" },
            });
        }

        // POINT 50. Un Kernel-Power 41 porte un champ BugcheckCode. S'il n'est pas nul,
        // il y a EU un écran bleu — même si aucun fichier de vidage n'a pu être écrit.
        // Ne se fier qu'aux vidages revenait à confondre « je n'ai pas de trace » et
        // « il ne s'est rien passé » : sur MLEAR-031-2024, le 14/09/2026, quinze
        // plantages réels étaient annoncés comme « aucun BSOD détecté sur la période ».
        //
        // Le code est écrit en DÉCIMAL dans le XML de l'événement, contrairement au
        // 1001 qui le donne en hexadécimal. Le confondre ferait nommer n'importe quoi.
        var sourceKernelPower = Lang.T("Événement Kernel-Power 41", "Kernel-Power event 41");
        foreach (var e in r.Events.Where(e => e.Category == EventCategory.PowerLoss))
        {
            if (!uint.TryParse(e.Extracted.GetValueOrDefault("BugcheckCode"), out var codeKp) || codeKp == 0) continue;

            // L'événement 41 est journalisé au REDÉMARRAGE : il suit le plantage de
            // quelques instants. On regroupe donc large plutôt que de compter double.
            var deja = incidents.FirstOrDefault(i => Math.Abs((i.TimeLocal - e.TimeLocal).TotalMinutes) < 15);
            if (deja is not null)
            {
                if (!deja.Sources.Contains(sourceKernelPower)) deja.Sources.Add(sourceKernelPower);
                deja.BugCheckCode ??= codeKp;
                continue;
            }

            incidents.Add(new BsodIncident
            {
                TimeLocal = e.TimeLocal,
                BugCheckCode = codeKp,
                BugCheckName = BugCheckCatalog.NameOf(codeKp),
                Sources = { sourceKernelPower },
            });
        }

        foreach (var e in r.Events.Where(e => e.Category == EventCategory.Bsod))
        {
            uint? code = ParseHex(e.Extracted.GetValueOrDefault("BugCheckCode"));
            var existing = incidents.FirstOrDefault(i =>
                Math.Abs((i.TimeLocal - e.TimeLocal).TotalMinutes) < 10 &&
                (code is null || i.BugCheckCode == code));

            var sourceBugCheck = Lang.T("Événement BugCheck 1001", "BugCheck event 1001");
            if (existing is not null)
            {
                if (!existing.Sources.Contains(sourceBugCheck))
                    existing.Sources.Add(sourceBugCheck);
            }
            else
            {
                incidents.Add(new BsodIncident
                {
                    TimeLocal = e.TimeLocal,
                    BugCheckCode = code,
                    BugCheckName = code is null ? Lang.T("(code non extrait)", "(code not extracted)") : BugCheckCatalog.NameOf(code.Value),
                    DumpPath = e.Extracted.GetValueOrDefault("DumpPath"),
                    Sources = { sourceBugCheck },
                });
            }
        }

        // Pilote suspect via TDR proche (cas GPU) — utilisé seulement si CDB n'a rien nommé.
        foreach (var i in incidents.Where(i => i.SuspectDriver is null &&
                                               i.BugCheckCode is 0x116 or 0x117 or 0x119 or 0xEA))
        {
            var tdr = r.Events.FirstOrDefault(e => e.Category == EventCategory.Tdr &&
                Math.Abs((e.TimeLocal - i.TimeLocal).TotalMinutes) < 30);
            if (tdr is not null && tdr.Extracted.TryGetValue("Driver", out var drv) && !string.IsNullOrWhiteSpace(drv))
                i.SuspectDriver = drv;
        }

        // Dédoublonnage : un même crash apparaît souvent deux fois (Minidump + MEMORY.DMP,
        // et parfois l'événement 1001). Même code + horodatages à moins de 5 min = un seul incident.
        var deduped = new List<BsodIncident>();
        foreach (var i in incidents.OrderByDescending(x => x.TimeLocal))
        {
            var dup = deduped.FirstOrDefault(x =>
                x.BugCheckCode == i.BugCheckCode &&
                Math.Abs((x.TimeLocal - i.TimeLocal).TotalMinutes) < 5);
            if (dup is null) { deduped.Add(i); continue; }
            foreach (var s in i.Sources.Where(s => !dup.Sources.Contains(s))) dup.Sources.Add(s);
            dup.SuspectDriver ??= i.SuspectDriver;
            dup.DumpPath ??= i.DumpPath;
        }
        return deduped;
    }

    private static uint? ParseHex(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        s = s.Trim();
        if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) s = s[2..];
        return uint.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v) ? v : null;
    }

    // ------------------------------------------------------------------
    // Règles
    // ------------------------------------------------------------------

    private static void AnalyzeBsodPatterns(DiagnosticReport r)
    {
        if (r.Bsods.Count == 0) return;

        // Un finding par code STOP distinct, avec détection de récurrence.
        foreach (var group in r.Bsods.Where(b => b.BugCheckCode is not null).GroupBy(b => b.BugCheckCode!.Value))
        {
            var entry = BugCheckCatalog.Lookup(group.Key);
            int n = group.Count();
            var last = group.Max(b => b.TimeLocal);
            var drivers = group.Where(b => b.SuspectDriver is not null).Select(b => b.SuspectDriver!).Distinct().ToList();

            var details = Lang.T($"{n} occurrence(s) de l'écran bleu {BugCheckCatalog.NameOf(group.Key)} (0x{group.Key:X8}), dernière le {last:dd/MM/yyyy HH:mm}.", $"{n} occurrence(s) of the {BugCheckCatalog.NameOf(group.Key)} blue screen (0x{group.Key:X8}), last one on {last:yyyy-MM-dd HH:mm}.");
            if (entry is not null) details += " " + entry.Description;
            if (drivers.Count > 0) details += Lang.T($" Pilote suspect : {string.Join(", ", drivers)}.", $" Suspect driver: {string.Join(", ", drivers)}.");

            // Si l'analyse symbolique a nommé un pilote pour TOUS les incidents du groupe,
            // la catégorie de la cause est « Pilote », pas celle générique du code STOP.
            bool allDriverIdentified = group.All(b => b.SuspectDriver is not null &&
                                                     !KernelPseudoModules.Contains(b.SuspectDriver));
            r.Findings.Add(new Finding
            {
                Severity = Severity.Critical,
                Confidence = n >= 2 ? Confidence.High : Confidence.Medium,
                Category = allDriverIdentified ? FaultCategory.Driver : entry?.Category ?? FaultCategory.Driver,
                Title = n >= 2
                    ? Lang.T($"BSOD récurrent : {BugCheckCatalog.NameOf(group.Key)} ({n}×)", $"Recurring BSOD: {BugCheckCatalog.NameOf(group.Key)} ({n}×)")
                    : Lang.T($"BSOD : {BugCheckCatalog.NameOf(group.Key)}", $"BSOD: {BugCheckCatalog.NameOf(group.Key)}"),
                Details = details,
                Recommendation = entry?.Advice ?? Lang.T("Analyser le dump avec WinDbg (!analyze -v) pour identifier le module fautif — automatisé en Phase 2.", "Analyse the dump with WinDbg (!analyze -v) to identify the faulting module."),
            });
        }

        var noCode = r.Bsods.Count(b => b.BugCheckCode is null);
        if (noCode > 0)
        {
            r.Findings.Add(new Finding
            {
                Severity = Severity.Warning,
                Confidence = Confidence.Medium,
                Category = FaultCategory.None,
                Title = Lang.T($"{noCode} crash(s) sans code STOP extrait", $"{noCode} crash(es) with no STOP code extracted"),
                Details = Lang.T("Un redémarrage après erreur a été journalisé mais le code n'a pas pu être lu (dump absent ou purgé).", "A restart after an error was logged, but the code could not be read (dump missing or purged)."),
                Recommendation = Lang.T("Vérifier que la création de dumps est activée : Système > Paramètres avancés > Démarrage et récupération > « Image mémoire du noyau ».", "Check that dump creation is enabled: System > Advanced system settings > Startup and Recovery > “Kernel memory dump”.")
            });
        }
    }

    /// <summary>
    /// Identifiants WHEA dont la gravité est établie : 17 = erreur matérielle CORRIGÉE,
    /// 18 = erreur matérielle FATALE. Les autres ne sont pas classés ici — on préfère
    /// ne rien affirmer plutôt que de deviner la gravité d'un code qu'on n'a pas vérifié.
    /// </summary>
    private static readonly int[] WheaCorrige = { 17 };
    private static readonly int[] WheaFatal = { 18 };

    private static void AnalyzeWhea(DiagnosticReport r)
    {
        var whea = r.Events.Where(e => e.Category == EventCategory.Whea).ToList();
        if (whea.Count == 0) return;

        bool bsod124 = r.Bsods.Any(b => b.BugCheckCode == 0x124);
        var corriges = whea.Where(e => WheaCorrige.Contains(e.EventId)).ToList();
        var fatales = whea.Where(e => WheaFatal.Contains(e.EventId)).ToList();
        bool tousCorriges = corriges.Count == whea.Count;

        // POINT 48. Le composant est écrit dans l'événement ; il était lu, puis ignoré.
        // « Le processeur a signalé N erreurs » n'est pas faux — sur Intel les ports
        // racines PCIe sont dans le paquet du processeur — mais pour qui lit, c'est
        // trompeur : le fautif est un lien et son périphérique, pas un cœur.
        var composants = whea.Select(e => e.Extracted.GetValueOrDefault("Composant"))
                             .Where(c => !string.IsNullOrEmpty(c)).Distinct().ToList();
        var triplets = whea.Select(e => e.Extracted.GetValueOrDefault("Bdf"))
                           .Where(b => !string.IsNullOrEmpty(b)).Distinct().Take(3).ToList();

        bool pcie = composants.Any(c => c!.Contains("PCI Express", StringComparison.OrdinalIgnoreCase));
        bool coeur = composants.Any(c => c!.Contains("Processor Core", StringComparison.OrdinalIgnoreCase)
                                      || c!.Contains("Cache Hierarchy", StringComparison.OrdinalIgnoreCase));
        bool memoire = composants.Any(c => c!.Contains("Memory Controller", StringComparison.OrdinalIgnoreCase));

        // Une erreur CORRIGÉE veut dire que le matériel a récupéré : rien n'a été perdu.
        // La classer critique au même titre qu'une erreur fatale faisait paniquer pour
        // un lien qui fonctionne — et détournait de la seule piste utile.
        var severite = (fatales.Count > 0 || bsod124) ? Severity.Critical
                     : tousCorriges ? Severity.Warning
                     : whea.Count >= 5 ? Severity.Critical
                     : Severity.Warning;

        var quoi = pcie ? Lang.T("lien PCI Express", "PCI Express link")
                 : memoire ? Lang.T("contrôleur mémoire", "memory controller")
                 : coeur ? Lang.T("processeur", "processor")
                 : Lang.T("matériel", "hardware");

        var titre = tousCorriges
            ? Lang.T($"{whea.Count} erreur(s) matérielle(s) CORRIGÉE(S) — {quoi}", $"{whea.Count} CORRECTED hardware error(s) — {quoi}")
            : Lang.T($"Erreurs matérielles WHEA détectées ({whea.Count}) — {quoi}", $"Hardware errors reported by WHEA ({whea.Count}) — {quoi}");

        var details = new List<string>();
        details.Add(tousCorriges
            ? Lang.T($"Windows a enregistré {whea.Count} erreur(s) matérielle(s) CORRIGÉE(S) (WHEA-Logger {string.Join(", ", whea.Select(e => e.EventId).Distinct().OrderBy(x => x))}) : le matériel a récupéré à chaque fois, aucune donnée n'a été perdue.",
                     $"Windows recorded {whea.Count} CORRECTED hardware error(s) (WHEA-Logger {string.Join(", ", whea.Select(e => e.EventId).Distinct().OrderBy(x => x))}): the hardware recovered every time and no data was lost.")
            : Lang.T($"Windows a enregistré {whea.Count} erreur(s) matérielle(s) (WHEA-Logger) sur la période.",
                     $"Windows recorded {whea.Count} hardware error(s) (WHEA-Logger) over the period."));

        if (composants.Count > 0)
            details.Add(Lang.T($"Composant mis en cause : {string.Join(", ", composants!)}.", $"Component implicated: {string.Join(", ", composants!)}."));
        if (triplets.Count > 0)
            details.Add(Lang.T($"Lien concerné (bus:appareil:fonction) : {string.Join(", ", triplets!)}. C'est cette adresse qui désigne le périphérique fautif, pas le processeur qui la rapporte.",
                               $"Link involved (bus:device:function): {string.Join(", ", triplets!)}. That address names the faulty device — not the processor reporting it."));
        if (bsod124)
            details.Add(Lang.T("Un écran bleu WHEA_UNCORRECTABLE_ERROR (0x124) confirme une erreur matérielle fatale.", "A WHEA_UNCORRECTABLE_ERROR (0x124) blue screen confirms a fatal hardware error."));
        details.Add(Lang.T($"Dernier événement : {whea.Max(e => e.TimeLocal):dd/MM/yyyy HH:mm}.", $"Last event: {whea.Max(e => e.TimeLocal):yyyy-MM-dd HH:mm}."));
        if (!pcie)
            details.Add(Lang.T($"Matériel de la machine : CPU {r.System.Cpu.Name} · carte mère {r.System.Bios.BaseboardManufacturer} {r.System.Bios.BaseboardProduct} (BIOS {r.System.Bios.Version}).",
                               $"Machine hardware: CPU {r.System.Cpu.Name} · motherboard {r.System.Bios.BaseboardManufacturer} {r.System.Bios.BaseboardProduct} (BIOS {r.System.Bios.Version})."));

        string reco;
        if (pcie && tousCorriges)
            reco = Lang.T(
                "Ce sont des erreurs de LIEN, pas de processeur : ni l'overclocking, ni la mémoire XMP, ni l'alimentation ne sont en cause ici. "
                + "Dans l'ordre : réenfoncer la carte dans son connecteur après avoir débranché le secteur ; désactiver la gestion d'alimentation du lien "
                + "(Options d'alimentation → Paramètres avancés → PCI Express → Gestion de l'alimentation de l'état de liaison → Désactivé) ; "
                + "mettre à jour le pilote du périphérique concerné ; enfin, forcer une génération PCIe inférieure dans le BIOS. "
                + "Mesurer le nombre d'erreurs par heure sous tension avant et après chaque étape : c'est le seul moyen de savoir laquelle a servi.",
                "These are LINK errors, not processor errors: neither overclocking, nor XMP memory, nor the power supply is involved here. "
                + "In order: reseat the card in its slot after unplugging the mains; turn off link state power management "
                + "(Power Options → Advanced settings → PCI Express → Link State Power Management → Off); "
                + "update the driver of the device concerned; finally, force a lower PCIe generation in the BIOS. "
                + "Measure the number of errors per powered hour before and after each step: that is the only way to know which one helped.");
        else if (memoire)
            reco = Lang.T(
                "Le contrôleur mémoire est mis en cause : retirer tout profil XMP/EXPO, tester les barrettes une par une avec MemTest86, et mettre à jour le BIOS.",
                "The memory controller is implicated: remove any XMP/EXPO profile, test the sticks one at a time with MemTest86, and update the BIOS.");
        else
            reco = Lang.T(
                "Vérifier les températures et la stabilité de l'alimentation ; retirer tout overclocking/XMP ; mettre à jour le BIOS. "
                + "Des WHEA récurrentes pointent vers CPU, carte mère, alimentation ou RAM — à tester dans cet ordre.",
                "Check temperatures and power supply stability; remove any overclocking/XMP; update the BIOS. "
                + "Recurring WHEA errors point to the CPU, motherboard, power supply or RAM — test in that order.");

        r.Findings.Add(new Finding
        {
            Severity = severite,
            Confidence = (fatales.Count > 0 || bsod124) ? Confidence.High : Confidence.Medium,
            Category = FaultCategory.Hardware,
            // Même identifiant de fait que la règle d'alerte « whea » : les deux
            // rapportent la même erreur, vue par deux chemins. Voir FusionnerLesDoublons.
            Code = "whea",
            Title = titre,
            Details = string.Join(" ", details),
            Recommendation = reco
        });
    }

    private static void AnalyzeMemory(DiagnosticReport r)
    {
        // Les incidents dont l'analyse symbolique a nommé un vrai pilote ne comptent PAS
        // comme suspicion RAM : leur cause est connue, inutile d'inquiéter sur le matériel.
        var memBsods = r.Bsods.Where(b => b.BugCheckCode is 0x1A or 0x50 or 0x4E or 0x12B &&
                                          (b.SuspectDriver is null || KernelPseudoModules.Contains(b.SuspectDriver)))
                              .ToList();
        var diagErrors = r.Events.Any(e => e.Category == EventCategory.MemoryDiag &&
                                           e.Extracted.GetValueOrDefault("HasErrors") == "True");
        var diagOk = r.Events.Any(e => e.Category == EventCategory.MemoryDiag &&
                                       e.Extracted.GetValueOrDefault("HasErrors") == "False");

        if (diagErrors)
        {
            r.Findings.Add(new Finding
            {
                Severity = Severity.Critical,
                Confidence = Confidence.High,
                Category = FaultCategory.Memory,
                Title = Lang.T("RAM défectueuse confirmée par le diagnostic mémoire Windows", "Faulty RAM confirmed by the Windows memory diagnostic"),
                Details = Lang.T("Le diagnostic mémoire Windows (mdsched) a détecté des erreurs matérielles sur la période analysée.", "The Windows memory diagnostic (mdsched) found hardware errors over the period analysed.") + HardwareRamList(r),
                Recommendation = Lang.T("Tester les barrettes une par une (MemTest86, plusieurs passes) et remplacer la barrette fautive. Désactiver XMP le temps du test.", "Test the sticks one at a time (MemTest86, several passes) and replace the faulty one. Disable XMP for the duration of the test.")
            });
        }
        else if (memBsods.Count >= 2)
        {
            // Nuance importante : si la virtualisation monopolise la RAM, la piste
            // logicielle est au moins aussi crédible que la RAM physique — on le dit.
            bool vmHeavy = VirtualizationBytes(r) > (long)r.System.Os.TotalVisibleMemoryKB * 1024 / 5;
            r.Findings.Add(new Finding
            {
                Severity = Severity.Critical,
                Confidence = vmHeavy ? Confidence.Low : Confidence.Medium,
                Category = FaultCategory.Memory,
                Title = vmHeavy
                    ? Lang.T("BSOD mémoire récurrents — RAM défectueuse OU pénurie causée par la virtualisation", "Recurring memory BSODs — faulty RAM OR a shortage caused by virtualisation")
                    : Lang.T("Suspicion de RAM défectueuse (BSOD mémoire récurrents)", "Suspected faulty RAM (recurring memory BSODs)"),
                Details = Lang.T($"{memBsods.Count} BSOD de type mémoire (MEMORY_MANAGEMENT / PAGE_FAULT…) sur la période.", $"{memBsods.Count} memory-type BSOD (MEMORY_MANAGEMENT / PAGE_FAULT…) over the period.")
                          + (diagOk ? Lang.T(" Le dernier diagnostic mémoire Windows n'avait rien détecté — MemTest86 est plus sensible.", " The last Windows memory diagnostic found nothing — MemTest86 is more sensitive.") : "")
                          + (vmHeavy ? Lang.T(" ATTENTION : la virtualisation (vmmem) réserve une grosse part de la RAM — voir la conclusion dédiée ; un manque de mémoire peut produire ces mêmes écrans bleus sans que la RAM soit défectueuse.", " CAUTION: virtualisation (vmmem) reserves a large share of the RAM — see the dedicated conclusion; a memory shortage can produce these very same blue screens without the RAM being faulty.") : "")
                          + HardwareRamList(r),
                Recommendation = (vmHeavy ? Lang.T("1) Limiter la mémoire de la virtualisation (voir conclusion dédiée). 2) ", "1) Cap the memory given to virtualisation (see the dedicated conclusion). 2) ") : "")
                               + Lang.T(
                                   "Lancer MemTest86 (4+ passes) pour exclure la RAM physique. Si XMP/DOCP est actif, le désactiver et re-tester. "
                                   + "L'analyse symbolique WinDbg (case « Analyse profonde » cochée) nommera le module fautif et permettra de trancher.",
                                   "Run MemTest86 (4+ passes) to rule out the physical RAM. If XMP/DOCP is enabled, disable it and test again. "
                                   + "Symbolic analysis with WinDbg (the “Deep analysis” box ticked) will name the faulting module and settle the question.")
            });
        }
    }

    /// <summary>Modules « fautifs » de CDB qui ne désignent pas un vrai pilote tiers.</summary>
    private static readonly HashSet<string> KernelPseudoModules = new(StringComparer.OrdinalIgnoreCase)
    {
        "ntoskrnl.exe", "ntkrnlmp.exe", "ntkrnlpa.exe", "ntkrpamp.exe",
        "memory_corruption", "hardware", "Unknown_Image", "Pool_Corruption", "win32k.sys", "win32kfull.sys",
    };

    /// <summary>
    /// Exploite les verdicts de l'analyse symbolique CDB (Phase 2) :
    /// pilote fautif nommé, récurrence par pilote, et interprétation honnête des
    /// pseudo-modules (memory_corruption → RAM, ntoskrnl → souvent RAM/matériel).
    /// </summary>
    private static void AnalyzeFaultingDrivers(DiagnosticReport r)
    {
        var analyzed = r.Dumps.Where(d => d.DeepAnalyzed && !string.IsNullOrEmpty(d.FaultingModule)).ToList();
        if (analyzed.Count == 0) return;

        // 1) Vrais pilotes désignés par l'analyse symbolique
        foreach (var g in analyzed
                     .Where(d => !KernelPseudoModules.Contains(d.FaultingModule!))
                     .GroupBy(d => d.FaultingModule!, StringComparer.OrdinalIgnoreCase))
        {
            var inv = Collectors.DriverCollector.FindBySysName(r.System.Drivers, g.Key);
            var invInfo = inv is null ? ""
                : Lang.T($" Pilote installé : {inv.DisplayName} — {inv.CompanyName} v{inv.FileVersion}", $" Installed driver: {inv.DisplayName} — {inv.CompanyName} v{inv.FileVersion}")
                  // Deux dates coexistent pour un même pilote : celle du FICHIER .sys et
                  // celle du PAQUET déclaré par Windows. Les afficher toutes deux comme
                  // « le pilote du … » donnait deux dates contradictoires dans le même
                  // rapport. On dit désormais laquelle on montre.
                  + (inv.FileDate is { } fd ? Lang.T($" (fichier du {fd:dd/MM/yyyy})", $" (file dated {fd:yyyy-MM-dd})") : "") + ".";
            bool isMicrosoft = inv?.IsMicrosoft ?? false;

            // Le pilote a-t-il été mis à jour APRÈS le dernier crash ? Si oui, le correctif
            // est peut-être déjà en place — information précieuse, on la donne.
            var lastCrash = g.Max(d => d.CrashTimeFromHeader ?? d.LastWriteTime);
            bool updatedSince = inv?.FileDate is { } fdate && fdate > lastCrash;

            var processes = g.Select(d => d.CrashProcessName)
                             .Where(p => !string.IsNullOrEmpty(p)).Distinct().ToList();

            string reco;
            var kb = DriverKnowledgeBase.Lookup(g.Key);
            if (kb is not null)
            {
                // Signature connue : contexte et correctif précis, éprouvés.
                invInfo += $" [{kb.Owner}] {kb.Context}";
                reco = kb.Fix
                     + (updatedSince ? Lang.T(" Note : le pilote a déjà été mis à jour depuis le dernier crash — le correctif est peut-être déjà en place ; surveiller.", " Note: the driver has already been updated since the last crash — the fix may already be in place; keep an eye on it.") : "");
            }
            else if (isMicrosoft)
            {
                reco = Lang.T($"{g.Key} est un composant de Windows : la correction passe par Windows Update, pas par un site d'éditeur. ", $"{g.Key} is a Windows component: the fix comes through Windows Update, not from a vendor website. ")
                     + Lang.T("Le script de réparation vérifie et applique lui-même ces mises à jour (WSL, Windows Update) et t'affiche le résultat. ", "The repair script checks and applies those updates itself (WSL, Windows Update) and shows you the result. ")
                     + (updatedSince
                         ? Lang.T("Bonne nouvelle : le pilote a été mis à jour depuis le dernier crash — le correctif est peut-être déjà en place ; surveiller si le crash se reproduit.", "Good news: the driver has been updated since the last crash — the fix may already be in place; watch whether the crash comes back.")
                         : Lang.T("Si le crash persiste système à jour, limiter la charge du composant déclencheur en attendant un correctif Microsoft.", "If the crash persists on an up-to-date system, reduce the load on the triggering component while waiting for a Microsoft fix."));
            }
            else
            {
                reco = Lang.T($"Mettre à jour {g.Key} depuis le site de l'éditeur", $"Update {g.Key} from the vendor's website")
                     + (inv is not null && !string.IsNullOrEmpty(inv.CompanyName) ? $" ({inv.CompanyName})" : "")
                     + Lang.T(", ou désinstaller le logiciel associé s'il ne sert plus. ", ", or uninstall the associated software if it is no longer used. ")
                     + Lang.T("Si le crash persiste avec la dernière version, revenir à une version antérieure stable.", "If the crash persists with the latest version, roll back to an earlier stable one.")
                     + (updatedSince ? Lang.T(" Note : le pilote a déjà été mis à jour depuis le dernier crash — le problème est peut-être déjà résolu.", " Note: the driver has already been updated since the last crash — the problem may already be solved.") : "");
            }

            r.Findings.Add(new Finding
            {
                Severity = Severity.Critical,
                Confidence = g.Count() >= 2 ? Confidence.High : Confidence.Medium,
                Category = FaultCategory.Driver,
                Code = "driver.identified",
                Subject = g.Key,
                Title = g.Count() >= 2
                    ? Lang.T($"Pilote fautif identifié (récurrent) : {g.Key} — {g.Count()} crashs", $"Faulting driver identified (recurring): {g.Key} — {g.Count()} crashes")
                    : Lang.T($"Pilote fautif identifié : {g.Key}", $"Faulting driver identified: {g.Key}"),
                Details = Lang.T($"L'analyse symbolique WinDbg (!analyze) désigne {g.Key} dans {g.Count()} dump(s).", $"Symbolic analysis with WinDbg (!analyze) names {g.Key} in {g.Count()} dump(s).")
                          + (g.First().FailureBucket is { } b ? Lang.T($" Signature : {b}.", $" Signature: {b}.") : "")
                          + (processes.Count > 0 ? Lang.T($" Processus déclencheur : {string.Join(", ", processes)}.", $" Triggering process: {string.Join(", ", processes)}.") : "")
                          + invInfo
                          + (updatedSince ? Lang.T($" ⚠ Le pilote a été mis à jour APRÈS le dernier crash ({lastCrash:dd/MM/yyyy}).", $" ⚠ The driver was updated AFTER the last crash ({lastCrash:yyyy-MM-dd}).") : ""),
                Recommendation = reco
            });
        }

        // 2) Pseudo-modules : CDB n'a pas pu incriminer un pilote → lecture honnête
        var pseudo = analyzed.Where(d => KernelPseudoModules.Contains(d.FaultingModule!)).ToList();
        var memCorruption = pseudo.Count(d =>
            d.FaultingModule!.Equals("memory_corruption", StringComparison.OrdinalIgnoreCase));
        if (memCorruption > 0)
        {
            r.Findings.Add(new Finding
            {
                Severity = Severity.Critical,
                Confidence = memCorruption >= 2 ? Confidence.High : Confidence.Medium,
                Category = FaultCategory.Memory,
                Title = Lang.T($"Corruption mémoire détectée par l'analyse symbolique ({memCorruption} dump(s))", $"Memory corruption found by symbolic analysis ({memCorruption} dump(s))"),
                Details = Lang.T(
                              "WinDbg conclut à « memory_corruption » : la mémoire a été altérée sans qu'un pilote précis "
                              + "puisse être incriminé. Ce verdict pointe le plus souvent vers la RAM physique (ou un "
                              + "overclocking/XMP instable), parfois vers un pilote qui écrit hors de sa zone.",
                              "WinDbg concludes “memory_corruption”: memory was altered without any specific driver "
                              + "being blamed. That verdict most often points to the physical RAM (or unstable "
                              + "overclocking/XMP), sometimes to a driver writing outside its own area.")
                          + HardwareRamList(r),
                Recommendation = Lang.T(
                                 "MemTest86 en priorité (4+ passes, XMP désactivé). Si la RAM est saine, activer le "
                                 + "vérificateur de pilotes avec précaution (voir le script de réparation).",
                                 "MemTest86 first (4+ passes, XMP disabled). If the RAM is sound, enable Driver Verifier "
                                 + "carefully (see the repair script).")
            });
        }
        else if (pseudo.Count > 0 && analyzed.Count == pseudo.Count)
        {
            r.Findings.Add(new Finding
            {
                Severity = Severity.Warning,
                Confidence = Confidence.Low,
                Category = FaultCategory.None,
                Title = Lang.T("Analyse symbolique sans coupable direct", "Symbolic analysis with no direct culprit"),
                Details = Lang.T(
                    $"CDB désigne le noyau Windows ({string.Join(", ", pseudo.Select(d => d.FaultingModule).Distinct())}) — "
                    + "cela signifie généralement que le vrai fautif (RAM, matériel ou pilote masqué) a corrompu "
                    + "l'état du système avant le crash, pas que Windows lui-même est en cause.",
                    $"CDB names the Windows kernel ({string.Join(", ", pseudo.Select(d => d.FaultingModule).Distinct())}) — "
                    + "this usually means the real culprit (RAM, hardware or a hidden driver) corrupted the system "
                    + "state before the crash, not that Windows itself is at fault."),
                Recommendation = Lang.T("Croiser avec les autres conclusions (WHEA, mémoire, disque) ; tester la RAM.",
                                        "Cross-check with the other conclusions (WHEA, memory, disk); test the RAM.")
            });
        }
    }

    /// <summary>
    /// Saturation de la mémoire virtuelle détectée par Windows lui-même
    /// (Resource-Exhaustion-Detector 2004) : cause LOGICIELLE, avec les processus
    /// coupables nommés — cas typique d'un logiciel de virtualisation ou d'une fuite mémoire.
    /// </summary>
    private static void AnalyzeResourceExhaustion(DiagnosticReport r)
    {
        var events = r.Events.Where(e => e.Category == EventCategory.ResourceExhaustion).ToList();
        if (events.Count == 0) return;

        var culprits = events
            .SelectMany(e => (e.Extracted.GetValueOrDefault("Processus") ?? "").Split(", ", StringSplitOptions.RemoveEmptyEntries))
            .GroupBy(p => p, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count())
            .Select(g => $"{g.Key} ({g.Count()}×)")
            .Take(5).ToList();

        r.Findings.Add(new Finding
        {
            Severity = events.Count >= 2 ? Severity.Critical : Severity.Warning,
            Confidence = Confidence.High,
            Category = FaultCategory.Software,
            // Même identifiant de fait que la règle d'alerte « exhaustion ».
            Code = "exhaustion",
            Title = Lang.T($"Mémoire saturée : Windows a détecté l'épuisement de la mémoire virtuelle ({events.Count}×)", $"Memory exhausted: Windows detected virtual memory exhaustion ({events.Count}×)"),
            Details = Lang.T("Windows a diagnostiqué une pénurie de mémoire virtuelle (événement Resource-Exhaustion-Detector 2004). ", "Windows diagnosed a virtual memory shortage (Resource-Exhaustion-Detector event 2004). ")
                      + (culprits.Count > 0
                          ? Lang.T($"Processus les plus gourmands identifiés par Windows : {string.Join(", ", culprits)}. ", $"Most demanding processes identified by Windows: {string.Join(", ", culprits)}. ")
                          : "")
                      + Lang.T(
                          "Ce profil est LOGICIEL : un programme consomme toute la mémoire (virtualisation, fuite mémoire, trop d'applications), "
                          + "ce qui provoque gels, plantages d'applications et parfois des BSOD mémoire — sans que la RAM soit défectueuse.",
                          "This profile is a SOFTWARE one: a program eats all the memory (virtualisation, memory leak, too many applications), "
                          + "which causes freezes, application crashes and sometimes memory BSODs — without the RAM being faulty.")
                      + Lang.T($" Dernière occurrence : {events.Max(e => e.TimeLocal):dd/MM/yyyy HH:mm}.", $" Last occurrence: {events.Max(e => e.TimeLocal):yyyy-MM-dd HH:mm}."),
            Recommendation = Lang.T(
                "Limiter la mémoire du processus en cause (ex. pour la virtualisation : réduire la RAM allouée aux VM, "
                + "ou fichier .wslconfig pour WSL/Docker avec « memory=8GB »). Vérifier la taille du fichier d'échange "
                + "(recommandé : géré automatiquement). Le script de réparation inclut ces vérifications.",
                "Cap the memory of the process at fault (for virtualisation: reduce the RAM given to the VMs, "
                + "or a .wslconfig file for WSL/Docker with “memory=8GB”). Check the page file size "
                + "(recommended: managed automatically). The repair script includes those checks.")
        });
    }

    /// <summary>État mémoire au moment du scan (instantané) : signale une pression mémoire en cours.</summary>
    private static void AnalyzeMemoryPressureNow(DiagnosticReport r)
    {
        var os = r.System.Os;
        if (os.TotalVirtualMemoryKB == 0) return;
        var commitUsedPct = 100.0 * (os.TotalVirtualMemoryKB - os.FreeVirtualMemoryKB) / os.TotalVirtualMemoryKB;
        var physUsedPct = os.TotalVisibleMemoryKB == 0 ? 0
            : 100.0 * (os.TotalVisibleMemoryKB - os.FreePhysicalMemoryKB) / os.TotalVisibleMemoryKB;
        if (commitUsedPct < 90 && physUsedPct < 92) return;

        var top = r.Processes.Take(3)
            .Select(p => $"{p.Name} ({FormatBytes((ulong)p.PrivateBytes)})").ToList();

        r.Findings.Add(new Finding
        {
            Severity = Severity.Warning,
            Confidence = Confidence.High,
            Category = FaultCategory.Software,
            Title = Lang.T($"Pression mémoire ÉLEVÉE en ce moment ({commitUsedPct:0} % de la mémoire virtuelle utilisée)", $"Memory pressure is HIGH right now ({commitUsedPct:0}% of virtual memory in use)"),
            Details = Lang.T($"Au moment du scan : mémoire physique utilisée à {physUsedPct:0} %, mémoire virtuelle (RAM + fichier d'échange) à {commitUsedPct:0} %. ", $"At scan time: physical memory {physUsedPct:0}% used, virtual memory (RAM + page file) {commitUsedPct:0}% used. ")
                      + (top.Count > 0 ? Lang.T($"Plus gros consommateurs actuels : {string.Join(", ", top)}.", $"Largest current consumers: {string.Join(", ", top)}.") : ""),
            Recommendation = Lang.T("Voir la section « Processus en cours » du rapport pour le détail complet, et réduire la consommation du ou des processus en tête.", "See the “Running processes” section of the report for the full detail, and reduce the consumption of the leading process(es).")
        });
    }

    /// <summary>Mémoire privée cumulée des conteneurs de virtualisation (WSL2/Docker/Hyper-V).</summary>
    internal static long VirtualizationBytes(DiagnosticReport r) =>
        r.Processes.Where(p => p.Name.StartsWith("vmmem", StringComparison.OrdinalIgnoreCase) ||
                               p.Name.Equals("vmwp", StringComparison.OrdinalIgnoreCase))
                   .Sum(p => p.PrivateBytes);

    /// <summary>
    /// Nomme explicitement la virtualisation (vmmem…) quand elle réserve une part
    /// importante de la RAM — même sans saturation au moment du scan. C'est la cause
    /// la plus fréquente des « plus de mémoire » incompris sur les postes de dev/admin.
    /// </summary>
    private static void AnalyzeVirtualizationMemory(DiagnosticReport r)
    {
        var vmBytes = VirtualizationBytes(r);
        var totalBytes = (long)r.System.Os.TotalVisibleMemoryKB * 1024;
        if (totalBytes == 0 || vmBytes < totalBytes * 0.20) return;

        var pct = 100.0 * vmBytes / totalBytes;
        var vmNames = string.Join(", ", r.Processes
            .Where(p => p.Name.StartsWith("vmmem", StringComparison.OrdinalIgnoreCase))
            .Select(p => $"{p.Name} ({FormatBytes((ulong)p.PrivateBytes)})"));
        bool memCrashes = r.Bsods.Any(b => b.BugCheckCode is 0x1A or 0x50 or 0x4E or 0x12B) ||
                          r.Events.Any(e => e.Category == EventCategory.ResourceExhaustion);

        r.Findings.Add(new Finding
        {
            Severity = memCrashes ? Severity.Warning : Severity.Info,
            Confidence = Confidence.High,
            Category = FaultCategory.Software,
            Title = Lang.T($"La virtualisation réserve {FormatBytes((ulong)vmBytes)} de RAM ({pct:0} %) — {vmNames.Split(' ')[0]}", $"Virtualisation is reserving {FormatBytes((ulong)vmBytes)} of RAM ({pct:0}%) — {vmNames.Split(' ')[0]}"),
            Details = Lang.T($"Les processus de virtualisation ({vmNames}) occupent {pct:0} % de la mémoire de la machine. ", $"The virtualisation processes ({vmNames}) are using {pct:0}% of the machine's memory. ")
                      + Lang.T(
                          "« vmmem » héberge WSL2, Docker Desktop ou les machines virtuelles Hyper-V : par défaut il peut "
                          + "grossir jusqu'à consommer presque toute la RAM, ce qui provoque gels et plantages d'applications",
                          "“vmmem” hosts WSL2, Docker Desktop or Hyper-V virtual machines: by default it can grow until "
                          + "it consumes nearly all the RAM, which causes freezes and application crashes")
                      + (memCrashes
                          ? Lang.T(
                              " — et des BSOD de type mémoire peuvent en découler quand un pilote gère mal la pénurie. "
                              + "Vu les crashs mémoire relevés sur cette machine, cette piste LOGICIELLE doit être vérifiée "
                              + "AVANT de conclure à une RAM défectueuse.",
                              " — and memory-type BSODs can follow when a driver handles the shortage badly. "
                              + "Given the memory crashes seen on this machine, this SOFTWARE lead must be checked "
                              + "BEFORE concluding that the RAM is faulty.")
                          : "."),
            Recommendation = Lang.T(
                "Limiter la mémoire de la virtualisation : pour WSL2/Docker, créer le fichier "
                + @"%USERPROFILE%\.wslconfig contenant deux lignes « [wsl2] » puis « memory=8GB » (à adapter), "
                + "puis exécuter « wsl --shutdown ». Pour une VM Hyper-V : réduire sa RAM ou activer la mémoire dynamique.",
                "Cap the memory given to virtualisation: for WSL2/Docker, create the file "
                + @"%USERPROFILE%\.wslconfig containing two lines, “[wsl2]” then “memory=8GB” (adjust to taste), "
                + "then run “wsl --shutdown”. For a Hyper-V VM: reduce its RAM or enable dynamic memory.")
        });
    }

    /// <summary>
    /// Signale les crashs plus anciens que la fenêtre d'analyse : leurs événements
    /// (BugCheck 1001, saturation mémoire…) n'ont pas pu être corrélés.
    /// </summary>
    private static void AnalyzeDumpWindow(DiagnosticReport r)
    {
        var cutoff = DateTime.Now.AddDays(-r.ScanPeriodDays);
        var older = r.Bsods.Where(b => b.TimeLocal < cutoff).ToList();
        if (older.Count == 0) return;

        r.Findings.Add(new Finding
        {
            Severity = Severity.Info,
            Confidence = Confidence.High,
            Category = FaultCategory.None,
            Title = Lang.T($"{older.Count} crash(s) antérieurs à la période analysée ({r.ScanPeriodDays} jours)", $"{older.Count} crash(es) older than the period analysed ({r.ScanPeriodDays} days)"),
            Details = Lang.T($"Des dumps de crash datent d'avant la fenêtre d'analyse (le plus récent : {older.Max(b => b.TimeLocal):dd/MM/yyyy}). ", $"Some crash dumps predate the analysis window (most recent: {older.Max(b => b.TimeLocal):yyyy-MM-dd}). ")
                      + Lang.T("Le journal d'événements de ces dates n'a donc pas été examiné : le diagnostic de ces crashs est incomplet.", "The event log for those dates was therefore not examined: the diagnosis of those crashes is incomplete."),
            Recommendation = Lang.T("Relancer le scan avec une période de 90 jours pour corréler ces crashs avec les événements de l'époque.", "Run the scan again with a 90-day period to correlate those crashes with the events of the time.")
        });
    }

    /// <summary>
    /// Transforme les alertes préventives (émises en temps réel par le service) en
    /// conclusions du diagnostic. Une alerte déjà couverte par une analyse de contexte
    /// de crash n'est pas répétée : on évite les doublons dans le rapport.
    /// </summary>
    private static void AnalyzePreventiveAlerts(DiagnosticReport r, FlightInfo f)
    {
        if (f.Alerts.Count == 0) return;

        // Catégories déjà expliquées par un contexte de crash (surchauffe/saturation mesurées).
        var covered = new HashSet<string>(StringComparer.Ordinal);
        foreach (var ctx in f.Contexts)
        {
            if (ctx.Samples.Any(s => s.CpuTemp >= 90)) covered.Add("cpu_temp");
            if (ctx.Samples.Any(s => s.GpuTemp >= 92)) covered.Add("gpu_temp");
            if (ctx.Samples.Any(s => s.CommitPct >= 95)) covered.Add("commit");
        }

        foreach (var g in f.Alerts.GroupBy(a => a.RuleId))
        {
            if (covered.Contains(g.Key)) continue;

            var latest = g.OrderByDescending(a => a.Time).First();
            r.Findings.Add(new Finding
            {
                Severity = g.Any(a => a.Level == "crit") ? Severity.Critical : Severity.Warning,
                Confidence = Confidence.High,
                // L'identifiant de la règle EST l'identifiant du fait : c'est lui qui
                // permet de reconnaître la même erreur rapportée par le journal de
                // Windows. Voir FusionnerLesDoublons.
                Code = g.Key,
                Category = latest.RuleId switch
                {
                    "cpu_temp" or "gpu_temp" or "whea" or "power41" => FaultCategory.Hardware,
                    "commit" or "exhaustion" => FaultCategory.Software,
                    _ when latest.RuleId.StartsWith("disk", StringComparison.Ordinal) => FaultCategory.Storage,
                    _ => FaultCategory.None,
                },
                Title = g.Count() > 1
                    ? Lang.T($"⚠ Alerte préventive répétée ({g.Count()}×) : {latest.Title}", $"⚠ Repeated preventive alert ({g.Count()}×): {latest.Title}")
                    : Lang.T($"⚠ Alerte préventive : {latest.Title}", $"⚠ Preventive alert: {latest.Title}"),
                Details = latest.Details + Lang.T($" Détecté en temps réel par la surveillance, dernière fois le {latest.Time:dd/MM/yyyy à HH:mm}.", $" Detected live by the monitoring service, most recently on {latest.Time:yyyy-MM-dd HH:mm}."),
                Recommendation = latest.Recommendation,
            });
        }
    }

    /// <summary>
    /// Exploite la boîte noire : que se passait-il dans les secondes AVANT chaque crash ?
    /// Surchauffe, saturation mémoire ou rien d'anormal — chaque cas a sa conclusion.
    /// </summary>
    private static void AnalyzeFlightRecorder(DiagnosticReport r)
    {
        var f = r.Flight;

        // Les alertes préventives sont exploitées même si le journal de vol a été purgé
        // (rotation 14 jours) : elles se suffisent à elles-mêmes.
        AnalyzePreventiveAlerts(r, f);

        // Pas de journal : proposer la surveillance seulement s'il y a des pannes à élucider.
        if (!f.JournalFound)
        {
            if (r.Bsods.Count > 0 || r.Events.Any(e => e.Category == EventCategory.PowerLoss))
            {
                r.Findings.Add(new Finding
                {
                    Severity = Severity.Info,
                    Confidence = Confidence.High,
                    Category = FaultCategory.None,
                    Title = Lang.T("Surveillance temps réel non installée — le contexte des crashs est perdu", "Real-time monitoring not installed — the context of the crashes is lost"),
                    Details = Lang.T("Cette machine a des crashs mais aucune boîte noire : impossible de savoir quelles étaient les températures, la mémoire et les processus au moment exact des pannes.", "This machine has crashes but no flight recorder: there is no way to know what the temperatures, the memory and the processes were at the exact moment of the failures."),
                    Recommendation = Lang.T("Activer la surveillance temps réel (bouton « 📡 » dans FaultTracePC) : le prochain crash sera capturé avec ses dernières secondes de contexte.", "Turn on real-time monitoring (the “📡” button in FaultTracePC): the next crash will be captured with its last seconds of context.")
                });
            }
            return;
        }

        foreach (var ctx in f.Contexts)
        {
            var last = ctx.Samples.LastOrDefault();
            if (last is null) continue;
            var maxCpuTemp = ctx.Samples.Max(s => s.CpuTemp ?? 0);
            var maxGpuTemp = ctx.Samples.Max(s => s.GpuTemp ?? 0);
            var maxCommit = ctx.Samples.Max(s => s.CommitPct ?? 0);

            if (maxCpuTemp >= 90 || maxGpuTemp >= 92)
            {
                r.Findings.Add(new Finding
                {
                    Severity = Severity.Critical,
                    Confidence = Confidence.High,
                    Category = FaultCategory.Hardware,
                    Title = Lang.T($"SURCHAUFFE mesurée juste avant le crash du {ctx.CrashTime:dd/MM HH:mm} ",
                                    $"OVERHEATING measured just before the crash of {ctx.CrashTime:MM-dd HH:mm} ")
                          + $"({(maxCpuTemp >= 90 ? $"CPU {maxCpuTemp:0} °C" : $"GPU {maxGpuTemp:0} °C")})",
                    Details = Lang.T(
                                  $"La boîte noire montre {(maxCpuTemp >= 90 ? $"le CPU à {maxCpuTemp:0} °C" : $"le GPU à {maxGpuTemp:0} °C")} "
                                  + "dans les secondes précédant le crash — une surchauffe qui déclenche la protection matérielle. "
                                  + $"Derniers relevés : CPU {last.CpuLoad:0} % / {last.CpuTemp:0} °C, mémoire {last.MemPct:0} %.",
                                  $"The flight recorder shows {(maxCpuTemp >= 90 ? $"the CPU at {maxCpuTemp:0} °C" : $"the GPU at {maxGpuTemp:0} °C")} "
                                  + "in the seconds before the crash — overheating that trips the hardware protection. "
                                  + $"Last readings: CPU {last.CpuLoad:0}% / {last.CpuTemp:0} °C, memory {last.MemPct:0}%."),
                    Recommendation = Lang.T(
                        "Dépoussiérer radiateurs et ventilateurs, vérifier leur rotation, renouveler la pâte thermique si la machine a plusieurs années, contrôler la ventilation du boîtier.",
                        "Clear the dust from heatsinks and fans, check that they still spin, renew the thermal paste if the machine is a few years old, and check the case airflow.")
                });
            }
            else if (maxCommit >= 95)
            {
                r.Findings.Add(new Finding
                {
                    Severity = Severity.Critical,
                    Confidence = Confidence.High,
                    Category = FaultCategory.Software,
                    Title = Lang.T($"Mémoire virtuelle SATURÉE juste avant le crash du {ctx.CrashTime:dd/MM HH:mm} ({maxCommit:0} %)",
                                    $"Virtual memory EXHAUSTED just before the crash of {ctx.CrashTime:MM-dd HH:mm} ({maxCommit:0}%)"),
                    Details = Lang.T(
                        $"La boîte noire montre la mémoire virtuelle à {maxCommit:0} % dans les secondes précédant le crash. "
                        + $"Processus dominants alors : {ctx.Samples.LastOrDefault(s => s.TopProcesses is not null)?.TopProcesses ?? "non relevés"}.",
                        $"The flight recorder shows virtual memory at {maxCommit:0}% in the seconds before the crash. "
                        + $"Leading processes at that moment: {ctx.Samples.LastOrDefault(s => s.TopProcesses is not null)?.TopProcesses ?? "not recorded"}."),
                    Recommendation = Lang.T(
                        "Identifier le processus dominant ci-dessus et limiter sa consommation (virtualisation → .wslconfig ; fuite mémoire → mise à jour de l'application).",
                        "Identify the leading process above and cap its consumption (virtualisation → .wslconfig; memory leak → update the application).")
                });
            }
        }

        if (f.AbruptSessionEnds > 0 && f.Contexts.Count == 0)
        {
            r.Findings.Add(new Finding
            {
                Severity = Severity.Info,
                Confidence = Confidence.Medium,
                Category = FaultCategory.None,
                Title = Lang.T($"{f.AbruptSessionEnds} arrêt(s) brutal(aux) détecté(s) par la boîte noire", $"{f.AbruptSessionEnds} abrupt shutdown(s) detected by the flight recorder"),
                Details = Lang.T("Le journal de surveillance s'est interrompu sans arrêt propre — la machine s'est éteinte brutalement. Voir la section « Boîte noire » pour les derniers relevés.", "The monitoring log stopped without a clean shutdown — the machine went off abruptly. See the “Flight recorder” section for the last readings."),
                Recommendation = Lang.T("Croiser avec les conclusions alimentation/température ci-dessus.", "Cross-check with the power and temperature conclusions above.")
            });
        }
    }

    /// <summary>
    /// Verdict SMART par disque. On ne parle QUE des indicateurs à valeur
    /// prédictive démontrée : secteurs défectueux, erreurs de câble, usure SSD.
    /// </summary>
    private static void AnalyzeSmart(DiagnosticReport r)
    {
        foreach (var d in r.System.Disks)
        {
            if (d.Smart is not { } s) continue;

            var facts = new List<string>();
            var severity = Severity.Info;
            var reco = "";

            // Un NVMe ne compte pas en « secteurs » : ses indicateurs de dégradation
            // sont la réserve de blocs et les erreurs d'intégrité. On adapte donc le
            // vocabulaire, sous peine de dire des choses fausses à l'utilisateur.
            bool isNvme = s.AvailableSparePercent is not null
                       || s.Source.StartsWith("SMART NVMe", StringComparison.OrdinalIgnoreCase);

            // Le disque annonce lui-même sa fin : c'est le signal le plus grave qui existe.
            if (s.PredictedFailure == true)
            {
                severity = Severity.Critical;
                var detail = s.CriticalWarning is { } w && w != 0
                    ? Collectors.NvmeSmartReader.DescribeWarning(w)
                    : "";
                facts.Add(detail.Length > 0
                    ? Lang.T($"le disque lève lui-même une ALERTE CRITIQUE : {detail}", $"the drive raises a CRITICAL ALERT itself: {detail}")
                    : Lang.T("le disque signale lui-même une DÉFAILLANCE IMMINENTE (SMART)", "the drive reports IMMINENT FAILURE itself (SMART)"));
                reco = Lang.T("Sauvegarder les données MAINTENANT et remplacer ce disque. Ne pas attendre.", "Back up the data NOW and replace this drive. Do not wait.");
            }

            if (isNvme)
            {
                // La réserve de blocs de remplacement est LE signal de fin de vie d'un NVMe.
                if (s.SpareExhausted)
                {
                    severity = Severity.Critical;
                    facts.Add(Lang.T($"réserve de blocs de remplacement épuisée : {s.AvailableSparePercent} % restants ", $"spare block reserve exhausted: {s.AvailableSparePercent}% left ")
                            + Lang.T($"pour un seuil constructeur de {s.AvailableSpareThresholdPercent} %", $"against a manufacturer threshold of {s.AvailableSpareThresholdPercent}%"));
                    if (reco.Length == 0)
                        reco = Lang.T("Le disque n'a plus de blocs de rechange pour compenser l'usure : sauvegarder et remplacer.", "The drive has no spare blocks left to compensate for wear: back up and replace it.");
                }
                else if (s.AvailableSparePercent is { } sp && s.AvailableSpareThresholdPercent is { } th && th > 0 && sp <= th + 10)
                {
                    severity = severity == Severity.Critical ? severity : Severity.Warning;
                    facts.Add(Lang.T($"réserve de blocs proche du seuil : {sp} % pour un seuil de {th} %", $"spare reserve close to the threshold: {sp}% against a threshold of {th}%"));
                    if (reco.Length == 0)
                        reco = Lang.T("La réserve approche du seuil constructeur : surveiller son évolution à chaque scan et prévoir le remplacement.", "The reserve is approaching the manufacturer threshold: watch it at every scan and plan the replacement.");
                }

                // Media and Data Integrity Errors : des données que le contrôleur
                // n'a pas su restituer. C'est l'équivalent NVMe d'un secteur illisible.
                if (s.UncorrectableSectors is { } media && media > 0)
                {
                    severity = media >= 10 ? Severity.Critical
                             : severity == Severity.Critical ? severity : Severity.Warning;
                    facts.Add(Lang.T($"{media} erreur(s) d'intégrité des données non corrigée(s)", $"{media} uncorrected data integrity error(s)"));
                    reco += (reco.Length > 0 ? " " : "")
                         + Lang.T("Ces erreurs signifient que le disque n'a pas pu restituer des données qu'il avait écrites. "
                                  + "Sauvegarder, vérifier l'intégrité des fichiers importants, et surveiller si le compteur augmente : "
                                  + "une progression d'un scan à l'autre condamne le disque.",
                                    "These errors mean the drive could not return data it had written. "
                                  + "Back up, check the integrity of the important files, and watch whether the counter grows: "
                                  + "a progression from one scan to the next condemns the drive.");
                }

                if (s.UnsafeShutdowns is { } us && s.PowerCycles is { } pc && pc > 10 && us > pc / 2)
                    facts.Add(Lang.T($"{us} arrêt(s) brutal(s) sur {pc} démarrages — coupures d'alimentation fréquentes", $"{us} abrupt shutdown(s) out of {pc} power-ups — frequent power losses"));
            }

            // Secteurs défectueux : le cœur de la question « mon disque est-il bon ? »
            if (isNvme) { /* le vocabulaire « secteurs » ne s'applique pas au NVMe */ }
            else if (s.PendingSectors is > 0)
            {
                severity = Severity.Critical;
                facts.Add(Lang.T($"{s.PendingSectors} secteur(s) instable(s) en attente de réallocation", $"{s.PendingSectors} unstable sector(s) awaiting reallocation"));
                reco = Lang.T("Secteurs en cours de dégradation : sauvegarder sans tarder, puis lancer une vérification complète du disque (chkdsk /r) qui forcera leur traitement. Si le nombre augmente d'un scan à l'autre, remplacer le disque.", "Sectors are degrading: back up without delay, then run a full disk check (chkdsk /r), which will force them to be dealt with. If the number grows from one scan to the next, replace the drive.");
            }
            else if (s.ReallocatedSectors is > 0 || s.UncorrectableSectors is > 0)
            {
                severity = severity == Severity.Critical ? severity : Severity.Warning;
                var bad = s.BadSectors;
                facts.Add(Lang.T($"{bad} secteur(s) défectueux déjà remplacés par la réserve", $"{bad} bad sector(s) already replaced from the spare area"));
                if (reco.Length == 0)
                    reco = bad >= 50
                        ? Lang.T("Le nombre de secteurs défectueux est élevé : prévoir le remplacement du disque et surveiller son évolution à chaque scan.", "The number of bad sectors is high: plan to replace the drive and watch it at every scan.")
                        : Lang.T("Quelques secteurs défectueux isolés sont tolérables sur un disque ancien ; ce qui compte est leur ÉVOLUTION — FaultTracePC la suivra d'un scan à l'autre.", "A few isolated bad sectors are tolerable on an old drive; what matters is their TREND — FaultTracePC will follow it from one scan to the next.");
            }

            // Attribut 199 : presque toujours un problème de câble, pas de disque.
            if (s.UdmaCrcErrors is > 0)
            {
                severity = severity == Severity.Critical ? severity : Severity.Warning;
                facts.Add(Lang.T($"{s.UdmaCrcErrors} erreur(s) de transmission (CRC)", $"{s.UdmaCrcErrors} transmission error(s) (CRC)"));
                reco += (reco.Length > 0 ? " " : "")
                     + Lang.T("Les erreurs CRC viennent presque toujours du CÂBLE SATA ou de son connecteur, pas du disque : rebrancher fermement des deux côtés, ou remplacer le câble (quelques euros) avant d'envisager autre chose.", "CRC errors almost always come from the SATA CABLE or its connector, not from the drive: reseat it firmly at both ends, or replace the cable (a few euros) before considering anything else.");
            }

            // Usure SSD
            if (s.SsdLifeLeftPercent is { } life)
            {
                if (life <= 10)
                {
                    severity = Severity.Critical;
                    facts.Add(Lang.T($"durée de vie restante du SSD : {life} %", $"SSD life remaining: {life}%"));
                    reco += (reco.Length > 0 ? " " : "") + Lang.T("Le SSD arrive en fin de vie : prévoir son remplacement.", "The SSD is reaching end of life: plan its replacement.");
                }
                else if (life <= 25)
                {
                    severity = severity == Severity.Critical ? severity : Severity.Warning;
                    facts.Add(Lang.T($"durée de vie restante du SSD : {life} %", $"SSD life remaining: {life}%"));
                    reco += (reco.Length > 0 ? " " : "") + Lang.T("Usure avancée : surveiller et prévoir le remplacement à moyen terme.", "Advanced wear: monitor it and plan a replacement in the medium term.");
                }
            }

            if (facts.Count == 0) continue;

            var age = s.PowerOnHours is { } h ? Lang.T($" Disque en service depuis {h / 24 / 365.0:0.#} an(s) ({h} heures).", $" Drive in service for {(h / 24 / 365.0).ToString("0.#", Lang.Culture)} year(s) ({h} hours).") : "";
            r.Findings.Add(new Finding
            {
                Severity = severity,
                Confidence = Confidence.High,
                Category = FaultCategory.Storage,
                Title = severity == Severity.Critical
                    ? Lang.T($"Disque à remplacer : {d.Model}", $"Drive to replace: {d.Model}")
                    : Lang.T($"Disque à surveiller : {d.Model}", $"Drive to watch: {d.Model}"),
                Details = Lang.T($"Analyse SMART — {string.Join(" ; ", facts)}.{age} Source : {s.Source}.", $"SMART analysis — {string.Join(" ; ", facts)}.{age} Source: {s.Source}."),
                Recommendation = reco,
            });
        }
    }

    /// <summary>
    /// Surchauffe dans la durée. Une pointe à 95 °C pendant dix secondes est sans
    /// conséquence ; une heure cumulée au-dessus de 90 °C use le matériel et
    /// provoque des arrêts de protection que rien, dans les journaux, ne relie
    /// spontanément à la température.
    /// </summary>
    private static void AnalyzeThermal(DiagnosticReport r)
    {
        foreach (var t in r.System is not null ? r.Flight.Thermal : [])
        {
            if (!t.HasData || t.Observed < TimeSpan.FromMinutes(10)) continue;

            var crit = t.AboveCrit;
            var warn = t.AboveWarn;
            if (warn < TimeSpan.FromMinutes(2)) continue; // rien de significatif

            var longest = t.LongestEpisodes.FirstOrDefault();
            var episode = longest is not null
                ? Lang.T($" Le plus long épisode a duré {longest.Minutes:0.#} minute(s) le {longest.Start:dd/MM à HH:mm}, avec une pointe à {longest.PeakC:0.#} °C.", $" The longest episode lasted {longest.Minutes.ToString("0.#", Lang.Culture)} minute(s) on {longest.Start:MM-dd HH:mm}, peaking at {longest.PeakC.ToString("0.#", Lang.Culture)} °C.")
                : "";
            var context = Lang.T($" Mesuré sur {ThermalHistory.Humanize(t.Observed)} de relevés", $" Measured over {ThermalHistory.Humanize(t.Observed)} of readings")
                        + (t.MaxC is { } mx ? Lang.T($", maximum {mx:0.#} °C le {t.MaxAt:dd/MM à HH:mm}", $", peak {mx.ToString("0.#", Lang.Culture)} °C on {t.MaxAt:MM-dd HH:mm}") : "") + ".";

            if (crit >= TimeSpan.FromMinutes(5))
            {
                r.Findings.Add(new Finding
                {
                    Severity = Severity.Critical,
                    Confidence = Confidence.High,
                    Category = FaultCategory.Hardware,
                    Title = Lang.T(
                        $"Surchauffe — {t.Sensor} : {ThermalHistory.Humanize(crit)} au-dessus de {t.CritThreshold:0} °C",
                        $"Overheating — {t.Sensor}: {ThermalHistory.Humanize(crit)} above {t.CritThreshold:0} °C"),
                    Details = Lang.T(
                                $"Le {t.Sensor.ToLowerInvariant()} a passé {ThermalHistory.Humanize(crit)} au-delà du seuil critique "
                                + $"de {t.CritThreshold:0} °C, et {ThermalHistory.Humanize(warn)} au-delà de {t.WarnThreshold:0} °C "
                                + $"({t.WarnPercent:0.#} % du temps mesuré).",
                                $"The {t.Sensor.ToLowerInvariant()} spent {ThermalHistory.Humanize(crit)} past the critical threshold "
                                + $"of {t.CritThreshold:0} °C, and {ThermalHistory.Humanize(warn)} past {t.WarnThreshold:0} °C "
                                + $"({t.WarnPercent.ToString("0.#", Lang.Culture)}% of the measured time).") + episode + context
                            + Lang.T(
                                " À ces températures, la machine se protège en ralentissant, puis s'éteint brutalement — "
                                + "des arrêts que rien, dans les journaux, ne relie spontanément à la chaleur.",
                                " At those temperatures the machine protects itself by slowing down, then shuts off abruptly — "
                                + "shutdowns that nothing in the logs spontaneously links to heat."),
                    Recommendation = Lang.T(
                        "Dépoussiérer les ventilateurs et les grilles d'aération, vérifier qu'aucune sortie d'air n'est obstruée, "
                        + "et sur une machine de plus de trois ans envisager le remplacement de la pâte thermique. "
                        + "Retirer tout overclocking. Sur un portable, éviter de l'utiliser posé sur un lit ou un canapé, qui bouchent les aérations.",
                        "Clear the dust from the fans and the air vents, check that no air outlet is blocked, "
                        + "and on a machine more than three years old consider renewing the thermal paste. "
                        + "Remove any overclocking. On a laptop, avoid using it on a bed or a sofa, which block the vents."),
                });
            }
            else if (warn >= TimeSpan.FromMinutes(30) || t.WarnPercent >= 20)
            {
                r.Findings.Add(new Finding
                {
                    Severity = Severity.Warning,
                    Confidence = Confidence.High,
                    Category = FaultCategory.Hardware,
                    Title = Lang.T(
                        $"Températures élevées — {t.Sensor} : {ThermalHistory.Humanize(warn)} au-dessus de {t.WarnThreshold:0} °C",
                        $"High temperatures — {t.Sensor}: {ThermalHistory.Humanize(warn)} above {t.WarnThreshold:0} °C"),
                    Details = Lang.T(
                                $"Le {t.Sensor.ToLowerInvariant()} a passé {ThermalHistory.Humanize(warn)} au-delà de {t.WarnThreshold:0} °C, "
                                + $"soit {t.WarnPercent:0.#} % du temps mesuré.",
                                $"The {t.Sensor.ToLowerInvariant()} spent {ThermalHistory.Humanize(warn)} past {t.WarnThreshold:0} °C, "
                                + $"that is {t.WarnPercent.ToString("0.#", Lang.Culture)}% of the measured time.") + episode + context
                            + Lang.T(
                                " Ce n'est pas une panne, mais c'est le signe avant-coureur des arrêts thermiques.",
                                " This is not a failure, but it is the early sign of thermal shutdowns."),
                    Recommendation = Lang.T(
                        "Dépoussiérer les aérations et surveiller l'évolution : si la durée passée trop haut augmente d'un scan à l'autre, "
                        + "le refroidissement se dégrade. Vérifier aussi qu'un logiciel ne sollicite pas le matériel en permanence "
                        + "(voir les processus en cours dans ce rapport).",
                        "Clear the vents and watch the trend: if the time spent too high grows from one scan to the next, "
                        + "the cooling is degrading. Also check that no software is loading the hardware permanently "
                        + "(see the running processes in this report)."),
                });
            }
        }
    }

    /// <summary>Usure de la batterie, exprimée simplement.</summary>
    private static void AnalyzeBattery(DiagnosticReport r)
    {
        foreach (var b in r.System.Batteries)
        {
            if (b.WearPercent is not { } wear)
            {
                // Batterie détectée mais capacités non exposées par le firmware.
                r.Findings.Add(new Finding
                {
                    Severity = Severity.Info,
                    Confidence = Confidence.Low,
                    Category = FaultCategory.Hardware,
                    Title = Lang.T("Usure de la batterie non mesurable", "Battery wear cannot be measured"),
                    Details = Lang.T($"La batterie « {b.Name} » est détectée, mais son firmware n'expose pas les capacités nécessaires au calcul d'usure.", $"The battery “{b.Name}” is detected, but its firmware does not expose the capacities needed to compute wear."),
                    Recommendation = Lang.T("Utiliser le rapport de batterie Windows (bouton dédié dans la boîte à outils) pour une analyse détaillée.", "Use the Windows battery report (dedicated button in the toolbox) for a detailed analysis."),
                });
                continue;
            }

            var health = 100 - wear;
            var capacity = b.DesignedCapacity is { } dc && b.FullChargedCapacity is { } fc
                ? Lang.T($" Elle ne retient plus que {fc} mWh sur les {dc} mWh prévus d'origine.", $" It now holds only {fc} mWh out of the {dc} mWh it was designed for.")
                : "";
            var cycles = b.CycleCount is { } c and > 0 ? Lang.T($" {c} cycles de charge.", $" {c} charge cycles.") : "";

            var (sev, title, reco) = wear switch
            {
                >= 70 => (Severity.Critical, Lang.T($"Batterie HORS D'USAGE — {health} % de santé restante", $"Battery WORN OUT — {health}% health left"),
                          Lang.T("La batterie ne tient pratiquement plus la charge : la machine s'éteindra dès qu'elle sera débranchée. Remplacement nécessaire.", "The battery barely holds a charge any more: the machine will switch off as soon as it is unplugged. It needs replacing.")),
                >= 40 => (Severity.Warning, Lang.T($"Batterie très usée — {health} % de santé restante", $"Battery heavily worn — {health}% health left"),
                          Lang.T("L'autonomie est fortement réduite. Prévoir le remplacement de la batterie ; en attendant, éviter de compter sur elle en déplacement.", "Battery life is much reduced. Plan to replace the battery; until then, do not rely on it away from a socket.")),
                >= 20 => (Severity.Info, Lang.T($"Batterie usée — {health} % de santé restante", $"Battery worn — {health}% health left"),
                          Lang.T("Usure normale pour une batterie de quelques années. Rien d'urgent : surveiller l'évolution.", "Normal wear for a battery a few years old. Nothing urgent: keep an eye on the trend.")),
                _ => (Severity.Info, Lang.T($"Batterie en bon état — {health} % de santé restante", $"Battery in good condition — {health}% health left"),
                      Lang.T("Aucune action nécessaire.", "No action needed.")),
            };

            r.Findings.Add(new Finding
            {
                Severity = sev,
                Confidence = Confidence.High,
                Category = FaultCategory.Hardware,
                Title = title,
                Details = Lang.T($"Usure mesurée : {wear} %.{capacity}{cycles}", $"Measured wear: {wear}%.{capacity}{cycles}")
                        + (b.ChargeRemainingPercent is { } ch ? Lang.T($" Charge actuelle : {ch} %.", $" Current charge: {ch}%.") : ""),
                Recommendation = reco,
            });
        }
    }

    /// <summary>
    /// Support amovible (clé USB, disque externe, lecteur de cartes) ?
    ///
    /// La distinction n'est pas cosmétique : sur une clé USB, les conseils habituels
    /// — gestion d'alimentation du lien PCI Express, câble SATA, firmware du SSD —
    /// n'ont aucun sens. Les servir quand même envoie l'utilisateur démonter une
    /// machine saine pour un support qu'il suffisait de remplacer.
    /// </summary>
    private static bool EstAmovible(DiskInfo d) =>
        d.InterfaceType.Contains("USB", StringComparison.OrdinalIgnoreCase)
        || d.InterfaceType.Contains("1394", StringComparison.OrdinalIgnoreCase)
        || d.Model.Contains("USB Device", StringComparison.OrdinalIgnoreCase)
        || d.Model.Contains("Flash Disk", StringComparison.OrdinalIgnoreCase)
        || d.Model.Contains("Card Reader", StringComparison.OrdinalIgnoreCase);

    /// <summary>Disques de la machine réellement mis en cause par les événements cités.</summary>
    private static List<DiskInfo> DisquesMisEnCause(List<(string Device, int Count)> devices, List<DiskInfo> inventaire)
    {
        var trouves = new List<DiskInfo>();
        foreach (var (device, _) in devices)
        {
            var m = System.Text.RegularExpressions.Regex.Match(device, @"Harddisk(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (!m.Success || !int.TryParse(m.Groups[1].Value, out var idx)) continue;
            var d = inventaire.FirstOrDefault(x => x.Index == idx);
            if (d is not null && !trouves.Contains(d)) trouves.Add(d);
        }
        return trouves;
    }

    /// <summary>
    /// Identifiants par lesquels volmgr signale un problème d'ÉCRITURE du vidage de
    /// plantage — pas un défaut du disque. Microsoft attribue l'événement 46
    /// (« Crash dump initialization failed ») à un fichier d'échange absent ou non
    /// configuré, et recommande d'en terminer la configuration.
    /// </summary>
    private static readonly int[] IdsEcritureVidage = { 45, 46, 49, 161, 162 };

    private static bool EstEcritureVidage(WinEvent e) =>
        e.Provider.Equals("volmgr", StringComparison.OrdinalIgnoreCase)
        && IdsEcritureVidage.Contains(e.EventId);

    /// <summary>
    /// Constaté le 14/09/2026 sur MLEAR-031-2024, puis le 17/09 sur PC-W10-11 : ces
    /// messages étaient comptés parmi les « erreurs disque » et menaient à un contrôle
    /// de disque. Ils disent en réalité que Windows n'a pas pu écrire le fichier qui
    /// aurait permis de diagnostiquer les plantages — et c'est tout autre chose : le
    /// disque va bien, mais les crashs ne laissent aucune trace exploitable.
    /// </summary>
    private static void AnalyzeEcritureVidage(DiagnosticReport r)
    {
        var events = r.Events.Where(EstEcritureVidage).ToList();
        if (events.Count == 0) return;

        var ids = events.GroupBy(e => e.EventId).OrderByDescending(g => g.Count())
                        .Select(g => $"volmgr {g.Key} ×{g.Count()}").ToList();

        // Des plantages sans fichier d'analyse : c'est la conséquence qui compte.
        var sansVidage = r.Bsods.Count(b => string.IsNullOrEmpty(b.DumpPath));

        var details = Lang.T(
            $"Windows a signalé {events.Count} fois qu'il ne parvenait pas à préparer ou à écrire le fichier de diagnostic de plantage ({string.Join(", ", ids)}). CE NE SONT PAS DES ERREURS DE DISQUE : le disque n'est pas mis en cause, c'est la destination du vidage qui manque ou qui est trop petite.",
            $"Windows reported {events.Count} times that it could not prepare or write the crash diagnostic file ({string.Join(", ", ids)}). THESE ARE NOT DISK ERRORS: the drive is not implicated — the destination for the dump is missing or too small.")
            + Lang.T($" Fichier d'échange déclaré : {r.System.Os.PageFileInfo}.", $" Declared page file: {r.System.Os.PageFileInfo}.")
            + (sansVidage > 0
                ? Lang.T($" Conséquence directe : {sansVidage} écran(s) bleu(s) ne disposent d'aucun fichier d'analyse, leur cause restera indéterminée tant que ce réglage n'est pas corrigé.",
                         $" Direct consequence: {sansVidage} blue screen(s) have no analysis file, and their cause will stay unknown until this setting is fixed.")
                : "");

        r.Findings.Add(new Finding
        {
            Severity = sansVidage > 0 ? Severity.Warning : Severity.Info,
            Confidence = Confidence.High,
            Category = FaultCategory.Software,
            Title = Lang.T($"Le vidage de plantage n'a pas pu être écrit ({events.Count} événement(s) volmgr)", $"The crash dump could not be written ({events.Count} volmgr event(s))"),
            Details = details,
            Recommendation = Lang.T(
                "Deux réglages, et aucun n'est risqué : activer les petits vidages mémoire (Paramètres système avancés → Démarrage et récupération → Écriture des informations de débogage → « Petit vidage mémoire »), et laisser le fichier d'échange géré automatiquement par Windows sur le disque système. Sans cela, chaque nouveau plantage sera aussi muet que les précédents.",
                "Two settings, neither of them risky: enable small memory dumps (Advanced system settings → Startup and Recovery → Write debugging information → “Small memory dump”), and let Windows manage the page file automatically on the system drive. Without this, every new crash will be as silent as the last.")
        });
    }


    // ------------------------------------------------------------------
    // Plafond de collecte : dire « au moins » plutôt qu'un chiffre rond
    // ------------------------------------------------------------------
    //
    // EventLogCollector s'arrête à MaxEvenementsParRequete événements par requête.
    // Une machine a donc rapporté « Erreurs disque répétées (500) » alors que 500
    // était la limite, pas un comptage — le vrai total pouvait être 501 comme
    // 40 000. Un chiffre rond présenté comme une mesure fausse l'appréciation de
    // la gravité, et c'est exactement ce que ce logiciel doit éviter.

    /// <summary>
    /// Écrit un nombre d'événements en tenant compte du plafond : « 500 » devient
    /// « au moins 500 » quand la collecte de cette catégorie a été coupée.
    /// </summary>
    internal static string Denombrer(DiagnosticReport r, EventCategory categorie, int nombre) =>
        r.TruncatedEventCategories.Contains(categorie)
            ? Lang.T($"au moins {nombre}", $"at least {nombre}")
            : nombre.ToString();

    /// <summary>
    /// Une seule phrase pour dire d'où vient le nombre affiché sur une carte de
    /// stockage : combien la machine a enregistré, si ce total est un plancher, et
    /// pourquoi il est réparti sur plusieurs cartes.
    ///
    /// POURQUOI ELLE EN REMPLACE DEUX
    /// Constaté le 18/09/2026 sur TECH-INFO-2025, en 1.6.1. La carte des 20
    /// réinitialisations de contrôleur portait « Le journal Windows en contient
    /// davantage », qui se lit « il y en a plus de 20 » — ce que rien n'établit. Ce
    /// qui est établi, c'est que la CATÉGORIE a buté sur son plafond ; rien ne dit
    /// que la coupe a touché ces 20-là plutôt que les autres. La phrase suivante
    /// disait déjà la même chose, correctement. Les deux n'en font plus qu'une, qui
    /// parle du total de la catégorie et de lui seul.
    ///
    /// Chaîne vide quand il n'y a ni plafond atteint ni répartition : un rapport ne
    /// commente pas une limite qui n'a pas joué.
    /// </summary>
    internal static string ContexteDuComptage(DiagnosticReport r, EventCategory categorie, int total, bool separe)
    {
        bool tronque = r.TruncatedEventCategories.Contains(categorie);
        if (!tronque && !separe) return "";

        var nombre = Denombrer(r, categorie, total);

        var plafond = tronque
            ? Lang.T($" La collecte s'arrête à {EventLogCollector.MaxEvenementsParRequete} événements par catégorie : ce nombre est un plancher, pas un décompte.",
                     $" Collection stops at {EventLogCollector.MaxEvenementsParRequete} events per category: this number is a floor, not a count.")
            : "";

        var repartition = separe
            ? Lang.T(" Elles sont séparées ici par périphérique concerné : les gestes ne sont pas les mêmes selon la nature du support.",
                     " They are split here by the device concerned: the actions differ with the kind of medium.")
            : "";

        return Lang.T($" Cette machine a enregistré {nombre} erreur(s) de stockage sur la période.",
                      $" This machine recorded {nombre} storage error(s) over the period.")
               + plafond + repartition;
    }

    private static void AnalyzeStorage(DiagnosticReport r)
    {
        // Point 51 : les événements d'écriture de vidage sortent du comptage. Les laisser
        // ici gonflait le nombre d'« erreurs disque » avec des messages qui ne parlent
        // pas du disque, et poussait vers un chkdsk là où il fallait un fichier d'échange.
        var diskEvents = r.Events.Where(e => e.Category == EventCategory.DiskError && !EstEcritureVidage(e)).ToList();
        var badDisks = r.System.Disks.Where(d =>
            d.Health.IsDegraded() ||
            (!string.IsNullOrEmpty(d.WmiStatus) && !d.WmiStatus.Equals("OK", StringComparison.OrdinalIgnoreCase))).ToList();
        var storageBsods = r.Bsods.Where(b => b.BugCheckCode is 0x24 or 0x7A or 0xF4 or 0x154 or 0xDE).ToList();

        foreach (var d in badDisks)
        {
            r.Findings.Add(new Finding
            {
                Severity = Severity.Critical,
                Confidence = Confidence.High,
                Category = FaultCategory.Storage,
                Title = Lang.T($"Disque en mauvaise santé : {d.Model}", $"Drive in poor health: {d.Model}"),
                Details = Lang.T($"État signalé : {(d.Health == DiskHealth.NotReported ? d.WmiStatus : d.Health.Label())}.", $"Reported status: {(d.Health == DiskHealth.NotReported ? d.WmiStatus : d.Health.Label())}.")
                          + (d.ReadErrorsTotal > 0 ? Lang.T($" {d.ReadErrorsTotal} erreurs de lecture cumulées.", $" {d.ReadErrorsTotal} cumulative read errors.") : ""),
                Recommendation = Lang.T("Sauvegarder immédiatement les données puis remplacer le disque. Vérifier le rapport SMART complet (CrystalDiskInfo) pour confirmation.", "Back up the data immediately, then replace the drive. Check the full SMART report (CrystalDiskInfo) for confirmation.")
            });
        }

        if (diskEvents.Count >= 3 || (diskEvents.Count > 0 && storageBsods.Count > 0))
        {
            // POINT 65. Une seule carte additionnait des choses de natures différentes.
            // Constaté le 18/09/2026 sur TECH-INFO-2025 : « Erreurs disque répétées (500) »
            // réunissait 479 événements sur un support DÉBRANCHÉ depuis, 20 réinitialisations
            // d'un port de contrôleur SATA, et une erreur de système de fichiers sur un
            // volume. Trois choses sans rapport, un seul chiffre, une seule recommandation —
            // qui commençait par le firmware du SSD, inutile pour les trois.
            //
            // Les événements sont donc répartis par NATURE du périphérique cité, et chaque
            // nature reçoit sa carte, sa gravité et son conseil.
            var parNature = diskEvents
                .GroupBy(e => ClasserEvenement(e, r.System.Disks))
                .ToDictionary(g => g.Key, g => g.ToList());

            var natures = OrdreDesNatures.Where(parNature.ContainsKey).ToList();
            bool separe = natures.Count > 1;

            for (int rang = 0; rang < natures.Count; rang++)
            {
                var nature = natures[rang];
                var evenements = parNature[nature];

                // La première nature de l'ordre de priorité garde l'identifiant historique
                // « disk_event » : c'est celle qui concerne le plus directement la machine,
                // et c'est sur cet identifiant que la fusion des doublons et l'historique
                // se sont toujours appuyés. Les autres reçoivent le leur — deux cartes qui
                // partageraient un identifiant seraient refusionnées en une seule.
                var code = rang == 0 ? "disk_event" : CodeDeNature(nature);

                // Un BSOD de type stockage parle du disque de la machine, pas d'une clé
                // débranchée : la corrélation ne s'attache qu'à la carte principale.
                var bsods = rang == 0 ? storageBsods : new List<BsodIncident>();

                r.Findings.Add(CarteDisque(r, nature, evenements, code, bsods, separe, diskEvents.Count));
            }
        }
    }

    // ------------------------------------------------------------------
    // Point 65 : répartition des erreurs disque par nature de périphérique
    // ------------------------------------------------------------------

    /// <summary>
    /// Nature du périphérique cité par un événement de stockage. Le classement ne
    /// repose que sur le chemin « \Device\… », que Windows n'a jamais traduit.
    /// </summary>
    public enum NatureCitee
    {
        /// <summary>Un disque présent à l'inventaire et non amovible : la machine est concernée.</summary>
        DisqueMonte,
        /// <summary>Un port du contrôleur de stockage (RaidPort, Scsi…) : pas un disque en particulier.</summary>
        PortControleur,
        /// <summary>Un volume nommé (\Device\HarddiskVolumeN) : le système de fichiers, pas le support.</summary>
        VolumeNomme,
        /// <summary>L'événement ne nomme aucun périphérique.</summary>
        NonIdentifie,
        /// <summary>Un disque présent à l'inventaire et amovible : clé, carte, disque externe.</summary>
        SupportAmovible,
        /// <summary>Un numéro de disque qui n'existe plus : le support a été débranché depuis.</summary>
        DisqueAbsent,
    }

    /// <summary>
    /// Ordre d'affichage ET de priorité. Le premier de cette liste présent dans le
    /// rapport garde l'identifiant de fait « disk_event » : d'abord ce qui concerne
    /// le matériel encore monté sur la machine, en dernier ce qui n'y est plus.
    /// </summary>
    private static readonly NatureCitee[] OrdreDesNatures =
    {
        NatureCitee.DisqueMonte, NatureCitee.PortControleur, NatureCitee.VolumeNomme,
        NatureCitee.NonIdentifie, NatureCitee.SupportAmovible, NatureCitee.DisqueAbsent,
    };

    public static NatureCitee ClasserEvenement(WinEvent e, List<DiskInfo> inventaire)
    {
        var m = System.Text.RegularExpressions.Regex.Match(e.Message ?? "", @"\\Device\\([A-Za-z0-9]+)");
        if (!m.Success) return NatureCitee.NonIdentifie;
        var nom = m.Groups[1].Value;

        // « Harddisk1 » seul est un numéro de disque ; « HarddiskVolume3 » est un volume.
        // L'ancre de fin évite de confondre les deux.
        var hd = System.Text.RegularExpressions.Regex.Match(nom, @"^Harddisk(\d+)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (hd.Success && int.TryParse(hd.Groups[1].Value, out var index))
        {
            var disque = inventaire.FirstOrDefault(d => d.Index == index);
            if (disque is null) return NatureCitee.DisqueAbsent;
            return EstAmovible(disque) ? NatureCitee.SupportAmovible : NatureCitee.DisqueMonte;
        }

        if (nom.StartsWith("HarddiskVolume", StringComparison.OrdinalIgnoreCase)) return NatureCitee.VolumeNomme;
        return NatureCitee.PortControleur;
    }

    private static string CodeDeNature(NatureCitee nature) => nature switch
    {
        NatureCitee.DisqueMonte => "disk_event_disque",
        NatureCitee.PortControleur => "disk_event_controleur",
        NatureCitee.VolumeNomme => "disk_event_volume",
        NatureCitee.NonIdentifie => "disk_event_sans_peripherique",
        NatureCitee.SupportAmovible => "disk_event_amovible",
        NatureCitee.DisqueAbsent => "disk_event_absent",
        _ => "disk_event",
    };

    private static string TitreDeNature(NatureCitee nature, string nombre) => nature switch
    {
        NatureCitee.DisqueMonte => Lang.T($"Erreurs disque répétées ({nombre})", $"Repeated disk errors ({nombre})"),
        NatureCitee.PortControleur => Lang.T($"Erreurs signalées par le contrôleur de stockage ({nombre})", $"Errors reported by the storage controller ({nombre})"),
        NatureCitee.VolumeNomme => Lang.T($"Erreurs sur un volume ({nombre})", $"Errors on a volume ({nombre})"),
        NatureCitee.SupportAmovible => Lang.T($"Erreurs sur un support amovible ({nombre})", $"Errors on removable media ({nombre})"),
        NatureCitee.DisqueAbsent => Lang.T($"Erreurs sur un support qui n'est plus connecté ({nombre})", $"Errors on a medium that is no longer connected ({nombre})"),
        _ => Lang.T($"Erreurs disque sans périphérique identifié ({nombre})", $"Disk errors with no device identified ({nombre})"),
    };

    /// <summary>
    /// Noms possibles d'un support qui n'est plus branché.
    ///
    /// Le journal ne laisse qu'un numéro de disque, réattribué à chaque branchement :
    /// « un disque qui portait le numéro 1 » était tout ce que le rapport savait dire.
    /// Windows, lui, retient quelle lettre a été montée par quel matériel. On propose
    /// donc les supports amovibles qu'il connaît et qui ne sont pas montés aujourd'hui.
    ///
    /// LE MOT « PISTE » N'EST PAS UNE PRÉCAUTION DE STYLE. Rien ne relie une lettre de
    /// lecteur au numéro de disque qu'elle portait ce jour-là. Présenter ce
    /// rapprochement comme une identification ferait accuser un support au hasard —
    /// exactement le genre d'erreur que ce logiciel existe pour éviter.
    /// </summary>
    internal static string PistesSupportsAbsents(DiagnosticReport r)
    {
        var historique = r.System.StorageHistory;

        // « Pas pu regarder » n'est pas « rien trouvé » : le dire vaut mieux que de
        // laisser croire que Windows ne connaissait aucun support.
        if (!historique.Readable)
            return historique.Note.Length > 0 ? " " + historique.Note : "";

        var candidats = historique.AmoviblesAbsents;
        if (candidats.Count == 0) return "";

        var liste = string.Join(", ", candidats.Take(4).Select(v => $"{v.Letter} ({v.Label})"));
        var reste = candidats.Count > 4
            ? Lang.T($" (et {candidats.Count - 4} autre(s))", $" (and {candidats.Count - 4} more)")
            : "";

        return Lang.T(
            $" Windows garde la trace des supports déjà montés sur ce poste : {candidats.Count} support(s) amovible(s) qu'il connaît ne sont pas montés aujourd'hui — {liste}{reste}. C'est une PISTE, pas une identification : rien ne relie une lettre de lecteur au numéro de disque qu'elle portait ce jour-là.",
            $" Windows keeps a record of the media already mounted on this machine: {candidats.Count} removable medium/media it knows are not mounted today — {liste}{reste}. This is a LEAD, not an identification: nothing links a drive letter to the disk number it carried on that day.");
    }

    /// <summary>
    /// Construit la carte d'une nature. Tout ce qui est compté, cité ou conseillé ici
    /// ne porte que sur les événements de CETTE nature : c'est précisément ce que le
    /// chiffre unique d'avant rendait impossible.
    /// </summary>
    private static Finding CarteDisque(
        DiagnosticReport r, NatureCitee nature, List<WinEvent> evenements, string code,
        List<BsodIncident> storageBsods, bool separe, int totalToutesNatures)
    {
        // Sources RÉELLEMENT observées, avec leurs identifiants. Jusqu'à la 1.2.1
        // ce texte citait « disk 153 / stornvme 129 » quels que soient les
        // événements collectés — une phrase toute faite qui pouvait nommer des
        // identifiants absents du rapport, deux lignes sous le tableau qui
        // affichait les vrais.
        var bySource = evenements
            .GroupBy(e => $"{e.Provider} {e.EventId}")
            .OrderByDescending(g => g.Count())
            .Select(g => $"{g.Key} ×{g.Count()}").ToList();

        var devices = DevicesCited(evenements);

        var misEnCause = DisquesMisEnCause(devices, r.System.Disks);
        var amovibles = misEnCause.Where(EstAmovible).ToList();
        var fixesMisEnCause = misEnCause.Where(d => !EstAmovible(d)).ToList();
        bool queDeLAmovible = amovibles.Count > 0 && fixesMisEnCause.Count == 0;

        var resets = evenements.Count(e => e.EventId == 129);
        var paging = evenements.Count(e => e.Provider.Equals("disk", StringComparison.OrdinalIgnoreCase) && e.EventId == 51);
        bool tousAbsents = nature == NatureCitee.DisqueAbsent;

        var nombre = Denombrer(r, EventCategory.DiskError, evenements.Count);

        return new Finding
        {
            Severity = storageBsods.Count > 0 ? Severity.Critical
                     // Un support débranché ou une clé fatiguée n'est pas un avertissement
                     // sur la machine : elle se remplace pour trois euros et n'annonce
                     // aucune panne du poste.
                     : nature is NatureCitee.DisqueAbsent or NatureCitee.SupportAmovible ? Severity.Info
                     : queDeLAmovible ? Severity.Info
                     : Severity.Warning,
            Confidence = storageBsods.Count > 0 ? Confidence.High : Confidence.Medium,
            Category = FaultCategory.Storage,
            Code = code,
            Title = TitreDeNature(nature, nombre),
            Details = Lang.T($"Sources : {string.Join(", ", bySource)}.", $"Sources: {string.Join(", ", bySource)}.")
                      + ContexteDuComptage(r, EventCategory.DiskError, totalToutesNatures, separe)
                      + (storageBsods.Count > 0 ? Lang.T($" Corrélées à {storageBsods.Count} BSOD de type stockage.", $" Correlated with {storageBsods.Count} storage-type BSOD.") : "")
                      + " " + DescribeDevices(devices, r.System.Disks, evenements)
                      + (resets > 0 ? Lang.T($" {resets} de ces événements sont des réinitialisations de contrôleur (ID 129) : l'opération a été retentée, pas perdue.", $" {resets} of those events are controller resets (ID 129): the operation was retried, not lost.") : "")
                      + (paging > 0 ? Lang.T($" {paging} concernent une opération de pagination (disk 51) — Windows lisait ou écrivait le fichier d'échange.", $" {paging} concern a paging operation (disk 51) — Windows was reading from or writing to the page file.") : "")
                      + (tousAbsents ? Lang.T(" Aucun disque actuellement monté sur cette machine n'est mis en cause : ces erreurs concernent uniquement des supports qui ne sont plus connectés.", " No drive currently mounted on this machine is implicated: these errors concern only media that are no longer connected.") : "")
                      + (tousAbsents ? PistesSupportsAbsents(r) : "")
                      + (nature == NatureCitee.PortControleur ? Lang.T(" Un port de contrôleur ne désigne aucun disque en particulier : ces événements ne disent pas lequel des disques montés était visé.", " A controller port designates no particular drive: these events do not say which of the mounted drives was targeted.") : "")
                      + (amovibles.Count > 0
                          ? Lang.T($" {(queDeLAmovible ? "Tous les disques mis en cause sont des supports AMOVIBLES" : "Une partie des disques mis en cause sont des supports AMOVIBLES")} ({string.Join(", ", amovibles.Select(d => d.Model))}) : sur ce type de support, ni la gestion d'alimentation du lien, ni un câble SATA, ni le firmware d'un SSD ne sont en jeu.",
                                   $" {(queDeLAmovible ? "Every disk implicated is REMOVABLE media" : "Some of the disks implicated are REMOVABLE media")} ({string.Join(", ", amovibles.Select(d => d.Model))}): on that kind of medium, neither link power management, nor a SATA cable, nor SSD firmware is involved.")
                          : ""),
            Recommendation = StorageAdvice(r.System.Disks, devices, resets, paging, tousAbsents, amovibles, fixesMisEnCause)
        };
    }

    /// <summary>
    /// Périphériques cités par les événements, du plus fréquent au moins fréquent.
    ///
    /// Le chemin « \Device\… » n'est jamais traduit par Windows, contrairement au
    /// reste du message : c'est le seul identifiant exploitable quelle que soit la
    /// langue du système, et c'est l'information la plus utile de toute la règle —
    /// sans elle, l'utilisateur sait qu'il a des erreurs mais pas sur quoi agir.
    /// </summary>
    private static List<(string Device, int Count)> DevicesCited(List<WinEvent> events) =>
        events.Select(e => System.Text.RegularExpressions.Regex.Match(e.Message ?? "", @"\\Device\\[A-Za-z0-9]+"))
              .Where(m => m.Success)
              .GroupBy(m => m.Value, StringComparer.OrdinalIgnoreCase)
              .OrderByDescending(g => g.Count())
              .Select(g => (g.Key, g.Count()))
              .ToList();

    /// <summary>
    /// Traduit les chemins « \Device\… » en langage compréhensible.
    ///
    /// PIÈGE ÉVITÉ ICI : le numéro de disque n'est PAS un identifiant stable. Il
    /// est attribué à l'énumération, au démarrage ou au branchement. Écrire
    /// « Disque 1 » pour un périphérique qui n'est plus connecté enverrait le
    /// lecteur ouvrir le Gestionnaire de disques, n'y rien trouver, et conclure que
    /// le rapport se trompe. On ne nomme donc un disque que lorsqu'il est
    /// réellement là ; sinon on donne ce qui reste vrai : quand il était là.
    /// </summary>
    private static string DescribeDevices(
        List<(string Device, int Count)> devices, List<DiskInfo> inventory, List<WinEvent> events)
    {
        if (devices.Count == 0) return Lang.T("Les événements ne nomment aucun périphérique précis.", "The events do not name any specific device.");

        var parts = new List<string>();
        foreach (var (device, count) in devices.Take(4))
        {
            var hd = System.Text.RegularExpressions.Regex.Match(device, @"Harddisk(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (hd.Success && int.TryParse(hd.Groups[1].Value, out var idx))
            {
                var match = inventory.FirstOrDefault(d => d.Index == idx);
                if (match is not null)
                {
                    // Présent : on le nomme de la façon la plus reconnaissable
                    // possible — numéro du Gestionnaire de disques, modèle, lettres.
                    var lettres = match.Letters.Count > 0 ? $" ({string.Join(", ", match.Letters)})" : "";
                    parts.Add(Lang.T($"Disque {idx}{lettres} — {match.Model} (×{count}, « {device} »)", $"Disk {idx}{lettres} — {match.Model} (×{count}, “{device}”)"));
                }
                else
                {
                    parts.Add(Lang.T($"un disque qui portait le numéro {idx} au moment des faits, ABSENT aujourd'hui de la machine ", $"a disk that carried number {idx} at the time, ABSENT from the machine today ")
                            + Lang.T($"(×{count}, « {device} ») — le Gestionnaire de disques ne l'affichera donc pas.", $"(×{count}, “{device}”) — Disk Management will therefore not show it.")
                            + WhenSeen(device, events));
                }
                continue;
            }

            if (device.Contains("RaidPort", StringComparison.OrdinalIgnoreCase))
            {
                parts.Add(Lang.T($"« {device} » (×{count}) désigne un port du contrôleur de stockage, pas un disque en particulier", $"“{device}” (×{count}) designates a port on the storage controller, not one particular disk"));
                continue;
            }

            parts.Add(Lang.T($"« {device} » (×{count})", $"“{device}” (×{count})"));
        }
        // Les fragments peuvent déjà se terminer par un point (WhenSeen rend des
        // phrases complètes) : on ne le double pas.
        var texte = string.Join(Lang.T(" ; ", "; "), parts);
        return Lang.T("Périphériques mis en cause : ", "Devices implicated: ") + texte + (texte.EndsWith('.') ? "" : ".");
    }

    /// <summary>
    /// Quand un périphérique disparu a-t-il été vu, et sur combien de branchements
    /// distincts ?
    ///
    /// Pour un périphérique absent, la date est la SEULE information exploitable :
    /// le numéro ne désigne plus rien, mais « le 21/07 à 08:19 » permet de se
    /// rappeler ce qui était branché. Le suffixe « \DRn » change à chaque
    /// rattachement : deux valeurs distinctes signalent deux branchements, donc un
    /// support amovible plutôt qu'un disque fixe.
    /// </summary>
    private static string WhenSeen(string device, List<WinEvent> events)
    {
        var liés = events.Where(e => (e.Message ?? "").Contains(device, StringComparison.OrdinalIgnoreCase)).ToList();
        if (liés.Count == 0) return "";

        var dates = liés.Select(e => e.TimeLocal).OrderBy(d => d).ToList();
        var instances = liés
            .Select(e => System.Text.RegularExpressions.Regex.Match(e.Message ?? "", System.Text.RegularExpressions.Regex.Escape(device) + @"\\(DR\d+)"))
            .Where(m => m.Success).Select(m => m.Groups[1].Value)
            .Distinct(StringComparer.OrdinalIgnoreCase).Count();

        var quand = dates.Count == 1
            ? Lang.T($" Vu le {dates[0]:dd/MM/yyyy} à {dates[0]:HH:mm}.", $" Seen on {dates[0]:yyyy-MM-dd} at {dates[0]:HH:mm}.")
            : Lang.T($" Vu entre le {dates[0]:dd/MM/yyyy} à {dates[0]:HH:mm} et le {dates[^1]:dd/MM/yyyy} à {dates[^1]:HH:mm}.", $" Seen between {dates[0]:yyyy-MM-dd} at {dates[0]:HH:mm} and {dates[^1]:yyyy-MM-dd} at {dates[^1]:HH:mm}.");

        var branchements = instances >= 2
            ? Lang.T($" Le compteur de rattachement prend {instances} valeurs différentes : c'est un support qui a été branché puis débranché à plusieurs reprises, pas un disque fixe.", $" The attachment counter takes {instances} different values: this is a medium that was plugged and unplugged several times, not a fixed drive.")
            : "";

        return quand + branchements;
    }

    /// <summary>
    /// Conseil bâti sur ce qui a réellement été observé — et sur le matériel
    /// réellement présent. Conseiller de vérifier un câble SATA à quelqu'un dont le
    /// seul disque est un NVMe lui fait chercher un câble qui n'existe pas.
    /// </summary>
    private static string StorageAdvice(
        List<DiskInfo> inventory, List<(string Device, int Count)> devices, int resets, int paging, bool tousAbsents,
        List<DiskInfo> amovibles, List<DiskInfo> fixesMisEnCause)
    {
        // Un conseil ne vaut que pour le matériel qu'il vise. Si les seuls disques mis
        // en cause sont amovibles, les gestes machine — lien PCI Express, câble SATA,
        // firmware SSD — ne s'appliquent à rien de ce qui a produit ces erreurs.
        bool queDeLAmovible = amovibles.Count > 0 && fixesMisEnCause.Count == 0;
        bool anySata = !queDeLAmovible && inventory.Any(d => !IsNvme(d));
        bool unknownDevice = devices.Any(d =>
        {
            var hd = System.Text.RegularExpressions.Regex.Match(d.Device, @"Harddisk(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            return hd.Success && int.TryParse(hd.Groups[1].Value, out var i) && inventory.All(x => x.Index != i);
        });

        // Quand tout se rapporte à des supports débranchés, il n'y a rien à réparer
        // ici : le dire en une phrase vaut mieux qu'une liste d'actions inutiles.
        if (tousAbsents)
            return Lang.T(
                "Rien à réparer sur cette machine : toutes ces erreurs se rapportent à des supports qui n'y sont plus connectés. "
                + "Si l'un d'eux vous appartient, c'est LUI qu'il faut examiner, rebranché, avec un contrôle du disque et une lecture de ses compteurs SMART. "
                + "Les dates ci-dessus permettent de retrouver de quel support il s'agissait.",
                "Nothing to repair on this machine: all these errors relate to media that are no longer connected to it. "
                + "If one of them is yours, THAT is what needs examining, plugged back in, with a disk check and a reading of its SMART counters. "
                + "The dates above make it possible to work out which medium it was.");

        var steps = new List<string>();

        if (amovibles.Count > 0)
            steps.Add(Lang.T(
                $"Traiter d'abord le support amovible mis en cause ({string.Join(", ", amovibles.Select(d => d.Model))}) : le tester sur une autre machine, en recopier le contenu, et le remplacer s'il recommence. "
                + "Une clé ou un disque externe fatigué produit exactement ces erreurs sans que la machine y soit pour quoi que ce soit.",
                $"Deal first with the removable medium implicated ({string.Join(", ", amovibles.Select(d => d.Model))}): test it on another machine, copy its contents off, and replace it if it recurs. "
                + "A worn USB stick or external drive produces exactly these errors without the machine being at fault."));

        // La cause la plus fréquemment documentée des réinitialisations de contrôleur
        // n'est ni un disque mourant ni un câble : c'est la mise en veille du lien.
        if (resets > 0 && !queDeLAmovible)
            steps.Add(Lang.T(
                "Commencer par la gestion d'alimentation des liens, cause la plus fréquemment documentée de ces réinitialisations : "
                + "Options d'alimentation → Modifier les paramètres avancés → PCI Express → Gestion de l'alimentation à l'état de liaison → Désactivé, "
                + "et Disque dur → Arrêter le disque dur après → Jamais. Un redémarrage est nécessaire.",
                "Start with link power management, the most frequently documented cause of these resets: "
                + "Power Options → Change advanced power settings → PCI Express → Link State Power Management → Off, "
                + "and Hard disk → Turn off hard disk after → Never. A restart is required."));

        if (unknownDevice)
            steps.Add(Lang.T(
                "Identifier le périphérique non inventorié avant toute réparation : Gestionnaire de disques, ou brancher/débrancher les supports amovibles et relancer une analyse. "
                + "Tant qu'il n'est pas identifié, une réparation lancée sur le disque système ne corrigera rien.",
                "Identify the uninventoried device before any repair: Disk Management, or plug and unplug the removable media and run another analysis. "
                + "Until it is identified, a repair run on the system drive will fix nothing."));

        if (paging > 0)
            steps.Add(Lang.T("Les erreurs de pagination visent le fichier d'échange : si elles portent sur le disque système, un contrôle du disque est justifié.", "The paging errors target the page file: if they concern the system drive, a disk check is warranted."));

        if (anySata)
            steps.Add(Lang.T("Sur les disques SATA, vérifier le câble de données et l'alimentation — une erreur de liaison se prend souvent pour un disque en fin de vie.", "On SATA drives, check the data cable and the power connector — a link error is often mistaken for a dying drive."));

        if (!queDeLAmovible)
            steps.Add(Lang.T("Mettre à jour le firmware du SSD et les pilotes de contrôleur de stockage du fabricant.", "Update the SSD firmware and the manufacturer's storage controller drivers."));
        steps.Add(Lang.T("Surveiller l'ÉVOLUTION des compteurs SMART d'une analyse à l'autre : c'est la progression qui annonce une panne, pas la valeur atteinte.", "Watch the TREND of the SMART counters from one analysis to the next: it is the progression that announces a failure, not the value reached."));

        return string.Join(" ", steps.Select((s, i) => $"{i + 1}. {s}"));
    }

    /// <summary>
    /// Disque NVMe ? On se fie aux compteurs réellement lus plutôt qu'à
    /// InterfaceType, que Windows renseigne souvent à « SCSI » pour du NVMe.
    /// </summary>
    private static bool IsNvme(DiskInfo d) =>
        d.Smart?.AvailableSparePercent is not null
        || (d.Smart?.Source.Contains("NVMe", StringComparison.OrdinalIgnoreCase) ?? false)
        || d.InterfaceType.Contains("NVMe", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Codes que Windows écrit dans LiveKernelReports quand l'affichage se fige SANS
    /// écran bleu : le moteur graphique n'a pas répondu, Windows l'a réinitialisé.
    /// Ces gels ne laissent aucune trace dans le journal d'événements — ils n'existent
    /// que sous forme de fichier .dmp. Les ignorer revenait à annoncer
    /// « 0 réinitialisation » sur une machine qui en accumulait des dizaines.
    /// </summary>
    private static readonly uint[] CodesGelGpu = { 0x141, 0x117, 0x119, 0x193 };

    /// <summary>Date de l'incident : celle de l'en-tête du dump si elle est lisible, sinon celle du fichier.</summary>
    private static DateTime DateDump(DumpFileInfo d) => d.CrashTimeFromHeader ?? d.LastWriteTime;

    private static void AnalyzeGpu(DiagnosticReport r)
    {
        var tdr = r.Events.Where(e => e.Category == EventCategory.Tdr).ToList();
        var gpuBsods = r.Bsods.Where(b => b.BugCheckCode is 0x116 or 0x117 or 0x119 or 0xEA)
                              .OrderBy(b => b.TimeLocal).ToList();

        var gels = r.Dumps.Where(d => d.Kind == DumpKind.LiveKernelReport
                                      && d.BugCheckCode.HasValue
                                      && CodesGelGpu.Contains(d.BugCheckCode.Value))
                          .OrderBy(DateDump).ToList();

        if (tdr.Count == 0 && gpuBsods.Count == 0 && gels.Count == 0) return;

        var drivers = tdr.Select(e => e.Extracted.GetValueOrDefault("Driver"))
                         .Where(d => !string.IsNullOrWhiteSpace(d)).Distinct().ToList();

        // Un gel suivi de près par un écran bleu vidéo, c'est une réinitialisation qui a
        // ÉCHOUÉ. La proportion de gels qui échouent, et la date à laquelle elle a basculé,
        // séparent un pilote instable (échecs dispersés) d'un matériel qui lâche
        // (échecs rares au début, systématiques à la fin).
        var gelsRates = gels.Where(g => gpuBsods.Any(b => (b.TimeLocal - DateDump(g)).TotalMinutes is >= -2 and <= 10)).ToList();

        var recos = new List<string>();
        var faits = new List<string>();

        if (gels.Count > 0)
        {
            faits.Add(Lang.T(
                $"{gels.Count} gel(s) du moteur graphique enregistrés par Windows sans écran bleu, du {DateDump(gels[0]):dd/MM/yyyy} au {DateDump(gels[^1]):dd/MM/yyyy}. Ces gels n'apparaissent pas dans le journal d'événements : ils ne sont visibles que dans C:\\Windows\\LiveKernelReports.",
                $"{gels.Count} graphics engine hang(s) recorded by Windows with no blue screen, from {DateDump(gels[0]):yyyy-MM-dd} to {DateDump(gels[^1]):yyyy-MM-dd}. These hangs leave no trace in the event log: they exist only in C:\\Windows\\LiveKernelReports."));
        }

        bool materielDesigne = false;

        if (gelsRates.Count > 0 && gels.Count >= 3)
        {
            var premierRate = gelsRates.Min(DateDump);
            var gelsAvant = gels.Count(g => DateDump(g) < premierRate);
            faits.Add(Lang.T(
                $"{gelsRates.Count} de ces gels se sont soldés par un écran bleu, le premier le {premierRate:dd/MM/yyyy}. Les {gelsAvant} gel(s) antérieurs avaient tous été récupérés sans plantage : la réinitialisation du processeur graphique échoue désormais là où elle réussissait.",
                $"{gelsRates.Count} of those hangs ended in a blue screen, the first on {premierRate:yyyy-MM-dd}. The {gelsAvant} earlier hang(s) were all recovered without a crash: resetting the graphics processor now fails where it used to succeed."));
            if (gelsAvant >= 5)
            {
                materielDesigne = true;
                recos.Add(Lang.T(
                    "Le symptôme est ancien et s'aggrave : ce profil désigne la CARTE, pas le pilote. Test décisif, sans rien acheter : retirer la carte graphique et brancher l'écran sur la sortie vidéo de la carte mère si le processeur en possède une, ou permuter la carte avec celle d'un autre poste. Si les gels cessent, la carte est à remplacer.",
                    "The symptom is long-standing and getting worse: this profile points at the CARD, not the driver. Decisive test, at no cost: remove the graphics card and plug the monitor into the motherboard's video output if the processor has one, or swap the card with another machine's. If the hangs stop, the card must be replaced."));
            }
        }

        // Le pilote ne peut pas être responsable d'un symptôme apparu avant son installation.
        if (gels.Count >= 5)
        {
            var premierGel = DateDump(gels[0]);
            var pilotesPosterieurs = r.System.Gpus
                .Where(g => g.DriverDate.HasValue && g.DriverDate.Value > premierGel)
                .ToList();
            if (pilotesPosterieurs.Count > 0 && pilotesPosterieurs.Count == r.System.Gpus.Count)
            {
                materielDesigne = true;
                faits.Add(Lang.T(
                    $"Le premier gel date du {premierGel:dd/MM/yyyy}, soit AVANT l'installation du pilote actuellement en place ({string.Join(" ; ", pilotesPosterieurs.Select(g => $"{g.DriverVersion}, paquet du {g.DriverDate:dd/MM/yyyy}"))}). Le pilote installé aujourd'hui ne peut donc pas avoir causé un symptôme qui lui est antérieur.",
                    $"The first hang dates from {premierGel:yyyy-MM-dd}, i.e. BEFORE the currently installed driver ({string.Join(" ; ", pilotesPosterieurs.Select(g => $"{g.DriverVersion}, package dated {g.DriverDate:yyyy-MM-dd}"))}). The driver in place today therefore cannot have caused a symptom that predates it."));
            }
        }

        // La température mesurée dit si la piste « surchauffe » tient encore. Recommander
        // de surveiller une température que la boîte noire a déjà mesurée pendant des
        // jours, c'est faire refaire à l'utilisateur le travail que le logiciel a fait.
        var thermiqueGpu = r.Flight.Thermal.FirstOrDefault(t => t.Sensor == ThermalHistory.CapteurGpu && t.HasData);
        if (thermiqueGpu is not null && thermiqueGpu.AboveWarn <= TimeSpan.Zero && thermiqueGpu.MaxC.HasValue)
        {
            faits.Add(Lang.T(
                $"La surchauffe est écartée par la mesure : maximum {thermiqueGpu.MaxC:0.#} °C sur {ThermalHistory.Humanize(thermiqueGpu.Observed)} de relevés, jamais au-dessus du seuil d'alerte de {thermiqueGpu.WarnThreshold:0} °C.",
                $"Overheating is ruled out by measurement: peak {thermiqueGpu.MaxC:0.#} °C over {ThermalHistory.Humanize(thermiqueGpu.Observed)} of readings, never above the {thermiqueGpu.WarnThreshold:0} °C warning threshold."));
        }

        if (!materielDesigne)
        {
            recos.Add(Lang.T(
                "Désinstallation propre du pilote (DDU en mode sans échec) puis installation de la dernière version stable ; surveiller la température GPU en charge ; tester sans overclocking.",
                "Clean driver removal (DDU in safe mode) then install the latest stable version; watch the GPU temperature under load; test without overclocking."));
        }
        else if (recos.Count == 0)
        {
            // Le matériel est désigné par l'antériorité du symptôme, sans qu'une escalade
            // ait été mesurée : on ne laisse pas la conclusion sans consigne pour autant.
            recos.Add(Lang.T(
                "Tester la machine sans la carte graphique — écran branché sur la sortie vidéo de la carte mère si le processeur en possède une — ou permuter la carte avec celle d'un autre poste. C'est le seul test qui sépare la carte du reste, et il ne coûte rien.",
                "Test the machine without the graphics card — monitor plugged into the motherboard's video output if the processor has one — or swap the card with another machine's. It is the only test that separates the card from everything else, and it costs nothing."));
        }

        var titre = Lang.T(
            $"Instabilité graphique : {gels.Count} gel(s) du moteur, {tdr.Count} réinitialisation(s) journalisée(s), {gpuBsods.Count} écran(s) bleu(s)",
            $"Graphics instability: {gels.Count} engine hang(s), {tdr.Count} logged reset(s), {gpuBsods.Count} blue screen(s)");

        var details = Lang.T(
            $"Le pilote d'affichage a cessé de répondre{(drivers.Count > 0 ? $" — pilote : {string.Join(", ", drivers!)}" : "")}.",
            $"The display driver stopped responding{(drivers.Count > 0 ? $" — driver: {string.Join(", ", drivers!)}" : "")}.")
            + (r.System.Gpus.Count > 0
                ? Lang.T($" Matériel concerné : {string.Join(" ; ", r.System.Gpus.Select(g => $"{g.Name} (pilote {g.DriverVersion}, paquet du {g.DriverDate:dd/MM/yyyy})"))}.", $" Hardware involved: {string.Join(" ; ", r.System.Gpus.Select(g => $"{g.Name} (driver {g.DriverVersion}, package dated {g.DriverDate:yyyy-MM-dd})"))}.")
                : "")
            + (faits.Count > 0 ? " " + string.Join(" ", faits) : "");

        r.Findings.Add(new Finding
        {
            Severity = gpuBsods.Count > 0 || materielDesigne ? Severity.Critical : Severity.Warning,
            Confidence = (tdr.Count + gpuBsods.Count + gels.Count) >= 3 ? Confidence.High : Confidence.Medium,
            Category = FaultCategory.GpuDriver,
            Title = titre,
            Details = details,
            Recommendation = string.Join(" ", recos)
        });
    }

    private static void AnalyzePowerLoss(DiagnosticReport r)
    {
        // Kernel-Power 41 avec BugcheckCode=0 et sans BSOD proche = coupure d'alimentation brutale.
        var hardLosses = r.Events.Where(e =>
            e.Category == EventCategory.PowerLoss &&
            e.Extracted.GetValueOrDefault("BugcheckCode", "0") == "0" &&
            !r.Bsods.Any(b => Math.Abs((b.TimeLocal - e.TimeLocal).TotalMinutes) < 5)).ToList();

        if (hardLosses.Count == 0) return;

        r.Findings.Add(new Finding
        {
            Severity = hardLosses.Count >= 2 ? Severity.Critical : Severity.Warning,
            Confidence = Confidence.Medium,
            Category = FaultCategory.Power,
            Title = Lang.T($"{hardLosses.Count} coupure(s) brutale(s) sans écran bleu", $"{hardLosses.Count} abrupt power loss(es) with no blue screen"),
            Details = Lang.T("Le système s'est éteint sans arrêt propre ni BSOD enregistré (Kernel-Power 41, code 0). ", "The system switched off without a clean shutdown and without a recorded BSOD (Kernel-Power 41, code 0). ")
                      + Lang.T("Causes typiques : alimentation (PSU) défaillante ou sous-dimensionnée, surchauffe déclenchant la protection thermique, "
                             + "câble/prise, ou blocage matériel complet. Ce profil n'est PAS un bug logiciel classique.",
                               "Typical causes: a failing or undersized power supply, overheating tripping the thermal protection, "
                             + "a cable or socket, or a complete hardware freeze. This profile is NOT a classic software bug.")
                      + Lang.T($" Dernière occurrence : {hardLosses.Max(e => e.TimeLocal):dd/MM/yyyy HH:mm}.", $" Last occurrence: {hardLosses.Max(e => e.TimeLocal):yyyy-MM-dd HH:mm}."),
            Recommendation = Lang.T("Vérifier températures CPU/GPU en charge, dépoussiérer, contrôler les branchements. Si récurrent, tester avec une autre alimentation. ", "Check CPU/GPU temperatures under load, clear the dust, check the connections. If it recurs, test with another power supply. ")
                           + Lang.T("La surveillance temps réel (mode 2) enregistrera les températures juste avant la prochaine coupure.", "Real-time monitoring (mode 2) will record the temperatures right before the next power loss.")
        });
    }

    /// <summary>
    /// Composants de Windows. Ils ne figurent JAMAIS dans la liste des programmes
    /// installés — il n'y a rien à désinstaller. En conclure « ce logiciel n'est plus
    /// installé, problème sans objet » revient à écarter une panne réelle au motif
    /// qu'on n'a pas trouvé son entrée dans le Panneau de configuration.
    /// </summary>
    private static readonly HashSet<string> BinairesSysteme = new(StringComparer.OrdinalIgnoreCase)
    {
        "dwm.exe", "explorer.exe", "csrss.exe", "svchost.exe", "lsass.exe", "winlogon.exe",
        "services.exe", "smss.exe", "wininit.exe", "conhost.exe", "dllhost.exe", "sihost.exe",
        "taskhostw.exe", "ctfmon.exe", "audiodg.exe", "spoolsv.exe", "fontdrvhost.exe",
        "RuntimeBroker.exe", "SearchHost.exe", "SearchIndexer.exe", "ShellExperienceHost.exe",
        "StartMenuExperienceHost.exe", "TextInputHost.exe", "WmiPrvSE.exe", "MsMpEng.exe",
    };

    /// <summary>
    /// Composants de Windows qui dépendent directement de la carte graphique. Quand le
    /// gestionnaire de fenêtres meurt à répétition, ce n'est pas « une application
    /// instable » : c'est l'affichage qui s'effondre sous lui. Le ranger dans les
    /// problèmes logiciels, c'est écarter la meilleure corroboration d'une panne
    /// graphique au moment précis où on en cherche une.
    /// </summary>
    private static readonly HashSet<string> BinairesGraphiques = new(StringComparer.OrdinalIgnoreCase)
    {
        "dwm.exe", "ShellExperienceHost.exe",
    };

    /// <summary>Modules dont la présence dans un plantage désigne la chaîne graphique.</summary>
    private static readonly string[] ModulesGraphiques =
    {
        "dwmcore.dll", "dxgkrnl", "dxgi.dll", "d3d9.dll", "d3d10", "d3d11", "d3d12",
        "nvlddmkm", "nvwgf2um", "igdumd", "igd10", "atidx", "amdxc", "amdvlk", "opengl32.dll", "vulkan-1.dll",
    };

    private static bool EstGraphique(string exe, IEnumerable<string?> modules) =>
        BinairesGraphiques.Contains(exe)
        || modules.Any(m => m is not null && ModulesGraphiques.Any(g => m.Contains(g, StringComparison.OrdinalIgnoreCase)));

    private static void AnalyzeAppCrashes(DiagnosticReport r)
    {
        var crashes = r.Events.Where(e => e.Category == EventCategory.AppCrash).ToList();
        if (crashes.Count == 0) return;

        var topApps = crashes.GroupBy(e => e.Extracted.GetValueOrDefault("App", "(inconnue)"))
            .OrderByDescending(g => g.Count()).Take(5).ToList();

        foreach (var g in topApps.Where(g => g.Count() >= 3))
        {
            var modules = g.Select(e => e.Extracted.GetValueOrDefault("Module"))
                           .Where(m => !string.IsNullOrWhiteSpace(m)).Distinct().Take(3).ToList();

            // « Ce problème existe-t-il encore ? » — on vérifie l'état ACTUEL du
            // logiciel au lieu de laisser l'utilisateur devant un crash périmé.
            var lastCrash = g.Max(e => e.TimeLocal);
            var (statusText, statusReco, stillActive) = CheckAppStatus(r, g.Key, lastCrash);

            // Un composant graphique de Windows qui tombe en série n'est pas un problème
            // logiciel : c'est la panne d'affichage, vue depuis l'autre bout. On le range
            // donc avec le matériel graphique, et on le relie à la conclusion existante
            // au lieu d'en faire une ligne isolée que le lecteur écarte.
            var graphique = EstGraphique(g.Key, modules);
            var conclusionGpu = r.Findings.FirstOrDefault(f => f.Category == FaultCategory.GpuDriver);

            var titre = stillActive
                ? Lang.T($"Application instable : {g.Key} ({g.Count()} crashs)", $"Unstable application: {g.Key} ({g.Count()} crashes)")
                : Lang.T($"Application anciennement instable : {g.Key} ({g.Count()} crashs) — {statusText}", $"Formerly unstable application: {g.Key} ({g.Count()} crashes) — {statusText}");
            var complement = "";
            var reco = statusReco;

            if (graphique)
            {
                titre = Lang.T($"Composant graphique de Windows en échec : {g.Key} ({g.Count()} crashs)", $"Windows graphics component failing: {g.Key} ({g.Count()} crashes)");
                complement = conclusionGpu is not null
                    ? Lang.T($" Ces plantages ne sont pas un problème logiciel distinct : ils CORROBORENT la conclusion graphique ci-dessus (« {conclusionGpu.Title} »). {g.Key} s'appuie directement sur la carte, et tombe quand elle cesse de répondre.",
                             $" These crashes are not a separate software problem: they CORROBORATE the graphics conclusion above (“{conclusionGpu.Title}”). {g.Key} relies directly on the card, and goes down when it stops responding.")
                    : Lang.T($" {g.Key} s'appuie directement sur la carte graphique : des plantages répétés de ce composant désignent le pilote ou la carte, pas un logiciel à réinstaller. Aucune autre trace graphique n'a été relevée dans ce rapport — c'est donc le seul indice, et il mérite une vérification du pilote et de la carte.",
                             $" {g.Key} relies directly on the graphics card: repeated crashes of this component point at the driver or the card, not at some application to reinstall. No other graphics evidence was found in this report — so this is the only clue, and it warrants checking the driver and the card.");
                reco = conclusionGpu is not null
                    ? Lang.T("Traiter la conclusion graphique : c'est elle qui porte la panne. Rien à faire sur ce composant lui-même.", "Deal with the graphics conclusion: that is where the fault lies. Nothing to do on this component itself.")
                    : Lang.T("Vérifier le pilote graphique et l'état de la carte avant toute autre piste.", "Check the display driver and the state of the card before any other lead.");
            }

            r.Findings.Add(new Finding
            {
                Severity = graphique ? Severity.Warning : (stillActive ? Severity.Warning : Severity.Info),
                Confidence = Confidence.High,
                Category = graphique ? FaultCategory.GpuDriver : FaultCategory.Software,
                Title = titre,
                Details = Lang.T($"{g.Count()} plantages sur la période, dernier le {lastCrash:dd/MM/yyyy}", $"{g.Count()} crashes over the period, the last on {lastCrash:yyyy-MM-dd}")
                          + (modules.Count > 0 ? Lang.T($", module(s) fautif(s) : {string.Join(", ", modules!)}", $", faulting module(s): {string.Join(", ", modules!)}") : "")
                          + Lang.T($". État actuel : {statusText}", $". Current state: {statusText}")
                          + complement,
                Recommendation = reco,
            });
        }

        // Module fautif commun à plusieurs applications = cause transversale.
        var crossModule = crashes
            .Where(e => e.Extracted.ContainsKey("Module"))
            .GroupBy(e => e.Extracted["Module"], StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Select(e => e.Extracted.GetValueOrDefault("App", "")).Distinct().Count() >= 3)
            .OrderByDescending(g => g.Count())
            .FirstOrDefault();

        if (crossModule is not null && !crossModule.Key.Equals("unknown", StringComparison.OrdinalIgnoreCase))
        {
            r.Findings.Add(new Finding
            {
                Severity = Severity.Warning,
                Confidence = Confidence.Medium,
                Category = FaultCategory.Software,
                Title = Lang.T($"Module fautif commun à plusieurs applications : {crossModule.Key}", $"Faulting module shared by several applications: {crossModule.Key}"),
                Details = Lang.T($"Ce module apparaît dans les crashs de {crossModule.Select(e => e.Extracted.GetValueOrDefault("App", "")).Distinct().Count()} applications différentes — la cause est probablement ce composant, pas les applications.", $"This module appears in the crashes of {crossModule.Select(e => e.Extracted.GetValueOrDefault("App", "")).Distinct().Count()} different applications — the cause is probably this component, not the applications."),
                Recommendation = Lang.T("Identifier à quoi appartient ce module (pilote, runtime, antivirus, overlay) et le mettre à jour ou le désinstaller.", "Identify what this module belongs to (driver, runtime, antivirus, overlay) and update or uninstall it.")
            });
        }
    }

    /// <summary>
    /// Un crash daté ne dit pas si le problème est toujours là. On confronte le
    /// nom de l'exécutable fautif aux logiciels réellement installés :
    /// désinstallé depuis ? mis à jour depuis le crash ? toujours identique ?
    /// </summary>
    private static (string Status, string Recommendation, bool StillActive) CheckAppStatus(
        DiagnosticReport r, string exeName, DateTime lastCrash)
    {
        if (r.System.InstalledApps.Count == 0)
            return (Lang.T("non vérifié (inventaire des logiciels indisponible)", "not checked (software inventory unavailable)"),
                    Lang.T("Réinstaller ou mettre à jour l'application.", "Reinstall or update the application."), true);

        // Un composant de Windows n'a pas d'entrée de désinstallation : son absence de
        // l'inventaire ne prouve rien et ne doit surtout pas clore le dossier.
        if (BinairesSysteme.Contains(exeName))
        {
            return (Lang.T("composant de Windows — il n'a pas d'entrée de désinstallation, son absence de la liste des programmes ne veut rien dire", "a Windows component — it has no uninstall entry, so its absence from the program list means nothing"),
                    Lang.T("Ne pas chercher à le réinstaller : traiter ce qui le fait tomber. Un composant de Windows qui plante à répétition est le symptôme d'un pilote, d'un matériel ou d'une corruption système, jamais la cause.", "Do not try to reinstall it: deal with whatever is bringing it down. A Windows component crashing repeatedly is the symptom of a driver, a piece of hardware or system corruption — never the cause."),
                    true);
        }

        var app = Collectors.InstalledSoftwareCollector.FindByExecutable(r.System.InstalledApps, exeName);
        if (app is null)
        {
            // ON N'AFFIRME PAS UNE DÉSINSTALLATION QU'ON N'A PAS CONSTATÉE.
            // La liste des programmes installés se lit dans la base de registre : les
            // composants livrés avec Windows, les applications du Microsoft Store et les
            // logiciels portables n'y figurent pas, et n'y ont jamais figuré. Écrire
            // « problème probablement sans objet » sur cette seule base, c'est clore un
            // dossier sur une absence de preuve.
            return (Lang.T("aucun programme installé ne porte ce nom — ce qui ne prouve pas une désinstallation : les composants de Windows, les applications du Microsoft Store et les logiciels portables n'ont pas d'entrée de désinstallation", "no installed program carries this name — which does not prove an uninstall: Windows components, Microsoft Store applications and portable software have no uninstall entry"),
                    Lang.T("Vérifier d'abord si le logiciel est encore là, sous une autre forme : application du Microsoft Store, version portable, ou composant livré avec Windows. S'il a bien été désinstallé, il n'y a rien à faire. S'il est toujours présent, les plantages le sont probablement aussi.", "First check whether the software is still there in another form: a Microsoft Store application, a portable version, or a component shipped with Windows. If it really was uninstalled, there is nothing to do. If it is still present, the crashes probably are too."),
                    false);
        }

        var version = string.IsNullOrEmpty(app.Version) ? "" : $" v{app.Version}";
        if (app.InstallDate is { } installed && installed.Date > lastCrash.Date)
        {
            return (Lang.T($"toujours installé ({app.Name}{version}), mais RÉINSTALLÉ ou MIS À JOUR le {installed:dd/MM/yyyy}, après le dernier crash — le problème est peut-être déjà corrigé", $"still installed ({app.Name}{version}), but REINSTALLED or UPDATED on {installed:yyyy-MM-dd}, after the last crash — the problem may already be fixed"),
                    Lang.T("Surveiller : si aucun nouveau crash n'apparaît au prochain scan, l'affaire est close.", "Keep watching: if no new crash appears at the next scan, the case is closed."),
                    false);
        }

        return (Lang.T($"toujours installé ({app.Name}{version}", $"still installed ({app.Name}{version}")
                + (app.InstallDate is { } d ? Lang.T($", installé le {d:dd/MM/yyyy}", $", installed on {d:yyyy-MM-dd}") : "") + Lang.T(") — problème toujours d'actualité", ") — problem still current"),
                Lang.T($"Mettre à jour {app.Name} vers sa dernière version, ou le réinstaller proprement. ", $"Update {app.Name} to its latest version, or reinstall it cleanly. ")
                + Lang.T("Si le module fautif est une DLL système ou de pilote (graphique, antivirus), traiter ce composant en priorité.", "If the faulting module is a system or driver DLL (display, antivirus), deal with that component first."),
                true);
    }

    /// <summary>
    /// Arrêts inattendus (EventLog 6008) restés SANS explication : ni écran bleu, ni
    /// coupure franche déjà signalée. Ils étaient collectés, affichés dans le tableau
    /// des événements… et n'alimentaient aucune conclusion. Le 14/09/2026, le rapport
    /// de MLEAR-031-2024 a donc rassuré sur une machine qui plantait toutes les
    /// semaines depuis quatre mois. C'est le pire mode de défaillance de cet outil :
    /// se tromper est réparable, rassurer à tort ne l'est pas.
    /// </summary>
    private static List<WinEvent> ArretsInexpliques(DiagnosticReport r) =>
        r.Events.Where(e => e.Category == EventCategory.UnexpectedShutdown
                            && !r.Bsods.Any(b => Math.Abs((b.TimeLocal - e.TimeLocal).TotalMinutes) <= 15))
                .OrderBy(e => e.TimeLocal)
                .ToList();

    private static void AnalyzeArretsInattendus(DiagnosticReport r)
    {
        var arrets = ArretsInexpliques(r);
        if (arrets.Count == 0) return;

        var dates = string.Join(", ", arrets.TakeLast(5).Select(e => Lang.T($"{e.TimeLocal:dd/MM à HH:mm}", $"{e.TimeLocal:yyyy-MM-dd HH:mm}")));

        r.Findings.Add(new Finding
        {
            Severity = Severity.Warning,
            Confidence = Confidence.High,
            Category = FaultCategory.Power,
            Title = Lang.T($"{arrets.Count} arrêt(s) inattendu(s) sans écran bleu associé", $"{arrets.Count} unexpected shutdown(s) with no matching blue screen"),
            Details = Lang.T(
                $"Windows a constaté au démarrage que l'arrêt précédent n'avait pas été propre (événement 6008), {arrets.Count} fois sur la période, sans qu'aucun écran bleu ne soit enregistré au même moment. Derniers cas : {dates}.",
                $"At start-up Windows found that the previous shutdown had not been clean (event 6008), {arrets.Count} time(s) over the period, with no blue screen recorded at the same moment. Most recent: {dates}.")
                + Lang.T(" Un arrêt de ce type n'est jamais normal : soit la machine a perdu son alimentation, soit quelqu'un a maintenu le bouton, soit elle s'est figée au point de ne plus rien pouvoir écrire.",
                         " A shutdown of this kind is never normal: either the machine lost power, or someone held the button down, or it froze so hard it could no longer write anything."),
            Recommendation = Lang.T(
                "Commencer par établir si ces arrêts sont subis ou provoqués : demander aux utilisateurs du poste. Si personne ne force l'extinction, vérifier l'alimentation et les températures, puis s'assurer que les vidages de plantage sont bien activés — sans eux, ces arrêts resteront sans explication à chaque analyse.",
                "Start by establishing whether these shutdowns are suffered or caused: ask the people who use the machine. If nobody forces the power off, check the power supply and the temperatures, then make sure crash dumps are enabled — without them these shutdowns will stay unexplained at every analysis.")
        });
    }

    /// <summary>
    /// Échecs de services répétés.
    ///
    /// Constaté le 18/09/2026 sur TECH-INFO-2025 : « Échecs de services Windows
    /// répétés (29) », suivi de « Consulter le détail dans la section Événements pour
    /// identifier le(s) service(s) concerné(s) ». Le logiciel avait les 29 événements
    /// sous la main, chacun portant le nom du service dans ses données. Il renvoyait
    /// au lecteur un travail qu'il pouvait faire lui-même — le reproche exact du thème
    /// de la 1.6.0.
    ///
    /// Le nom relevé est le nom D'AFFICHAGE, celui de services.msc. C'est pourquoi la
    /// recommandation renvoie à services.msc et jamais à sc.exe, qui attend le nom
    /// court et échouerait sur celui-là.
    /// </summary>
    private static void AnalyzeServiceFailures(DiagnosticReport r)
    {
        var fails = r.Events.Where(e => e.Category == EventCategory.ServiceFailure).ToList();
        if (fails.Count < 5) return;

        var parService = fails
            .GroupBy(e => e.Extracted.GetValueOrDefault("Service", "").Trim())
            .Where(g => g.Key.Length > 0)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        string details, reco;

        if (parService.Count == 0)
        {
            // Aucun nom lisible : on le DIT, au lieu de laisser croire qu'on n'a pas cherché.
            details = Lang.T("Des services n'ont pas démarré ou se sont arrêtés de façon inattendue de manière répétée. Aucun de ces événements ne porte de nom de service exploitable.",
                             "Services failed to start or stopped unexpectedly, repeatedly. None of these events carries a usable service name.");
            reco = Lang.T("Ouvrir l'Observateur d'événements, journal Système, et filtrer sur la source « Service Control Manager » pour retrouver les services concernés.",
                          "Open Event Viewer, System log, and filter on the “Service Control Manager” source to find which services are involved.");
        }
        else
        {
            var nommes = string.Join(", ", parService.Take(4).Select(g =>
                Lang.T($"{g.Key} ({g.Count()}×, dernier le {Lang.ShortDateMinute(g.Max(e => e.TimeLocal))})",
                       $"{g.Key} ({g.Count()}×, last on {Lang.ShortDateMinute(g.Max(e => e.TimeLocal))})")));

            var reste = parService.Count > 4
                ? Lang.T($" et {parService.Count - 4} autre(s) service(s)", $" and {parService.Count - 4} more service(s)")
                : "";

            details = Lang.T($"Services concernés, du plus touché au moins touché : {nommes}{reste}.",
                             $"Services involved, from the most affected to the least: {nommes}{reste}.");

            // Un seul service qui concentre l'essentiel des échecs n'appelle pas le même
            // geste qu'une dispersion sur dix services : le premier se traite, la seconde
            // désigne le système lui-même.
            var premier = parService[0];
            bool domine = parService.Count == 1 || premier.Count() * 2 >= fails.Count;

            details += domine
                ? Lang.T($" {premier.Key} concentre {premier.Count()} des {fails.Count} échecs : c'est lui qu'il faut traiter en premier.",
                         $" {premier.Key} accounts for {premier.Count()} of the {fails.Count} failures: that is the one to deal with first.")
                : Lang.T($" Les échecs se répartissent sur {parService.Count} services différents — aucun ne domine, ce qui désigne plutôt le système que l'un d'eux.",
                         $" The failures are spread over {parService.Count} different services — none dominates, which points at the system rather than at any one of them.");

            reco = domine
                ? Lang.T($"Ouvrir services.msc et y chercher « {premier.Key} » : ses propriétés donnent son compte de démarrage et ses actions de récupération. Un service tiers qui tombe se met à jour ou se désinstalle ; un service de Windows qui tombe est un symptôme, pas une cause — chercher alors du côté des pilotes et des fichiers système.",
                         $"Open services.msc and look for “{premier.Key}”: its properties show its startup account and its recovery actions. A third-party service that keeps failing gets updated or uninstalled; a Windows service that keeps failing is a symptom, not a cause — look at drivers and system files instead.")
                : Lang.T("Des échecs dispersés sur plusieurs services désignent rarement ces services : vérifier d'abord les fichiers système et l'image Windows, puis les mises à jour installées à la même période.",
                         "Failures spread across several services rarely point at those services: check the system files and the Windows image first, then the updates installed over the same period.");
        }

        r.Findings.Add(new Finding
        {
            Severity = Severity.Info,
            Confidence = Confidence.Medium,
            Category = FaultCategory.Software,
            Code = "service_failure",
            Title = Lang.T($"Échecs de services Windows répétés ({fails.Count})", $"Repeated Windows service failures ({fails.Count})"),
            Details = details,
            Recommendation = reco,
        });
    }

    private static void AnalyzeUpdateCorrelation(DiagnosticReport r)
    {
        var updates = r.Events.Where(e => e.Category == EventCategory.WindowsUpdate && e.EventId == 19).ToList();
        if (updates.Count == 0 || r.Bsods.Count == 0) return;

        var correlated = r.Bsods.Where(b => updates.Any(u =>
            b.TimeLocal > u.TimeLocal && b.TimeLocal < u.TimeLocal.AddHours(48))).ToList();

        if (correlated.Count > 0)
        {
            r.Findings.Add(new Finding
            {
                Severity = Severity.Info,
                Confidence = Confidence.Low,
                Category = FaultCategory.WindowsUpdate,
                Title = Lang.T($"{correlated.Count} crash(s) survenus dans les 48 h après une mise à jour Windows", $"{correlated.Count} crash(es) within 48 h of a Windows update"),
                Details = Lang.T("Corrélation temporelle uniquement — ce n'est pas une preuve de causalité, mais un point à vérifier si les crashs ont commencé après une mise à jour précise.", "Timing correlation only — this is not proof of causation, but a point to check if the crashes started after one particular update."),
                Recommendation = Lang.T("Si le début des crashs coïncide avec une mise à jour, envisager sa désinstallation (Paramètres > Windows Update > Historique) ou une mise à jour des pilotes concernés.", "If the start of the crashes coincides with an update, consider uninstalling it (Settings > Windows Update > Update history) or updating the drivers involved.")
            });
        }
    }

    /// <summary>
    /// POINT 53, seconde moitié. Les faits réseau étaient collectés depuis le lot
    /// précédent ; ici on en tire la conclusion que l'auteur avait dû reconstituer à la
    /// main le 14/09/2026 sur MLEAR-031-2024 : carte Wi-Fi présente et active, ZÉRO
    /// réseau enregistré, machine du domaine jamais revue depuis sa réinstallation.
    /// « Plus de Wi-Fi » n'était pas une panne de carte : c'était une absence de profil.
    /// </summary>
    private static void AnalyzeReseau(DiagnosticReport r)
    {
        var net = r.System.Network;
        if (net.Adapters.Count == 0) return;

        // Seules les cartes RÉELLES comptent : un adaptateur VPN ou Wi-Fi Direct ferait
        // conclure à une panne de Wi-Fi sur une machine qui n'a pas de radio.
        var sansFil = net.Adapters.Where(a => a.IsWireless && a.IsPhysical).ToList();
        var cableConnecte = net.Adapters.Any(a => !a.IsWireless && a.HasIpV4);
        var aucuneIp = net.Adapters.All(a => !a.HasIpV4);

        // --- 1. carte Wi-Fi présente, aucun réseau enregistré ---------------------
        // Le compteur vaut -1 quand le dossier des profils n'a pas pu être lu : on ne
        // conclut alors RIEN. Confondre « zéro » et « pas regardé » ferait annoncer une
        // panne sur une machine saine, ce qui est pire que de se taire.
        if (sansFil.Count > 0 && net.WifiProfileCount == 0)
        {
            var noms = string.Join(", ", sansFil.Select(a => a.Name));

            var details = Lang.T(
                $"La machine porte une carte Wi-Fi ({noms}), mais AUCUN réseau n'y est enregistré — ni par stratégie de groupe, ni par l'utilisateur. Ce n'est pas une panne de la carte : Windows n'a simplement rien à proposer, donc la liste des réseaux reste vide.",
                $"The machine has a Wi-Fi adapter ({noms}), but NO network is stored on it — neither by group policy nor by the user. This is not an adapter failure: Windows simply has nothing to offer, so the network list stays empty.");

            if (net.PartOfDomain)
                details += Lang.T(
                    $" Le poste appartient au domaine {net.Domain} : ses profils Wi-Fi viennent normalement des stratégies de groupe. Sans réseau, il ne les reçoit pas ; sans elles, il n'a pas de Wi-Fi. C'est une boucle, et seul un câble la casse.",
                    $" The machine belongs to the {net.Domain} domain: its Wi-Fi profiles normally come from group policy. With no network it never receives them; without them it has no Wi-Fi. That is a loop, and only a cable breaks it.");

            details += cableConnecte
                ? Lang.T(" Un câble est actuellement connecté : la correction peut être lancée tout de suite.", " A cable is currently connected: the fix can be applied right now.")
                : Lang.T(" Aucune connexion filaire active en ce moment.", " No wired connection is active at the moment.");

            var reco = net.PartOfDomain
                ? Lang.T(
                    "Brancher un câble Ethernet, puis ouvrir une invite de commandes en administrateur et lancer « gpupdate /force ». Les stratégies de groupe rapportent les profils Wi-Fi, qui réapparaissent aussitôt dans la liste des réseaux. Vérifier ensuite que le poste reste joignable par le domaine.",
                    "Plug in an Ethernet cable, then open an administrator command prompt and run \"gpupdate /force\". Group policy brings the Wi-Fi profiles back, and they reappear in the network list straight away. Then check that the machine stays reachable from the domain.")
                : Lang.T(
                    "Enregistrer le réseau à la main (Paramètres → Réseau et Internet → Wi-Fi → Gérer les réseaux connus) : hors domaine, aucun mécanisme ne le fera à votre place.",
                    "Add the network by hand (Settings → Network & Internet → Wi-Fi → Manage known networks): off-domain, nothing will do it for you.");

            r.Findings.Add(new Finding
            {
                Severity = aucuneIp ? Severity.Critical : Severity.Warning,
                Confidence = Confidence.High,
                Category = FaultCategory.Network,
                Code = "wifi_sans_profil",
                Title = Lang.T("Aucun réseau Wi-Fi enregistré — la liste des réseaux restera vide", "No Wi-Fi network stored — the network list will stay empty"),
                Details = details,
                Recommendation = reco
            });
        }

        // --- 2. un service sans lequel il n'y a pas de réseau est arrêté -----------
        foreach (var svc in net.Services.Where(x => !x.State.Equals("Running", StringComparison.OrdinalIgnoreCase)))
        {
            // Le service Wi-Fi arrêté sur une machine sans carte sans fil est normal.
            if (svc.Name.Equals("WlanSvc", StringComparison.OrdinalIgnoreCase) && sansFil.Count == 0) continue;

            // Un service à démarrage « à la demande » est CONÇU pour rester arrêté tant
            // que rien ne le réclame. Le signaler revenait à écrire un défaut sur chaque
            // poste du parc — le 18/09/2026, NlaSvc était ainsi accusé sur deux machines
            // en parfait état. Un faux positif universel décrédibilise tout le rapport.
            var voulu = svc.StartMode.Equals("Disabled", StringComparison.OrdinalIgnoreCase);
            if (!voulu && !svc.DoitTourner) continue;

            r.Findings.Add(new Finding
            {
                Severity = voulu ? Severity.Info : Severity.Warning,
                Confidence = Confidence.High,
                Category = FaultCategory.Network,
                Title = Lang.T($"Service réseau arrêté : {svc.Name}", $"Network service stopped: {svc.Name}"),
                Details = Lang.T(
                    $"{svc.DisplayName} ({svc.Name}) est à l'état « {svc.StateLabel} », démarrage « {svc.StartModeLabel} ».",
                    $"{svc.DisplayName} ({svc.Name}) is {svc.StateLabel}, startup {svc.StartModeLabel}.")
                    + (voulu
                        ? Lang.T(" Il a été désactivé volontairement : ce n'est signalé que pour mémoire.", " It has been disabled on purpose: this is reported for the record only.")
                        : Lang.T(" Il devrait tourner. Tant qu'il est arrêté, la fonction qu'il rend est indisponible, quelle que soit la santé du matériel.", " It should be running. While it is stopped, the function it provides is unavailable, whatever the state of the hardware.")),
                Recommendation = voulu
                    ? Lang.T("Aucune action si l'arrêt est voulu. Sinon, repasser le démarrage en automatique.", "No action if the shutdown is intended. Otherwise set the startup back to automatic.")
                    : Lang.T($"Redémarrer le service et vérifier qu'il tient : « sc.exe start {svc.Name} », puis contrôler son démarrage automatique.", $"Restart the service and check that it holds: \"sc.exe start {svc.Name}\", then check its automatic startup.")
            });
        }
    }

    private static void AnalyzeDiskSpace(DiagnosticReport r)
    {
        foreach (var v in r.System.Volumes.Where(v => v.SizeBytes > 0 && v.PercentFree < 8))
        {
            r.Findings.Add(new Finding
            {
                Severity = v.PercentFree < 4 ? Severity.Warning : Severity.Info,
                Confidence = Confidence.High,
                Category = FaultCategory.Storage,
                Title = Lang.T($"Espace disque faible sur {v.Letter} ({v.PercentFree} % libre)", $"Low disk space on {v.Letter} ({v.PercentFree}% free)"),
                Details = Lang.T($"Volume {v.Letter} ({v.Label}) : {FormatBytes(v.FreeBytes)} libres sur {FormatBytes(v.SizeBytes)}. Un disque système saturé provoque lenteurs et échecs d'écriture du fichier d'échange ou des dumps.", $"Volume {v.Letter} ({v.Label}): {FormatBytes(v.FreeBytes)} free out of {FormatBytes(v.SizeBytes)}. A saturated system drive causes slowness and write failures for the page file or the dumps."),
                Recommendation = Lang.T("Libérer de l'espace (nettoyage de disque, %TEMP%, anciens dumps volumineux comme MEMORY.DMP une fois analysé).", "Free up space (disk cleanup, %TEMP%, large old dumps such as MEMORY.DMP once analysed).")
            });
        }
    }

    private static void ComputeVerdict(DiagnosticReport r)
    {
        var critical = r.Findings.Where(f => f.Severity == Severity.Critical).ToList();
        if (critical.Count == 0)
        {
            var warnings = r.Findings.Where(f => f.Severity == Severity.Warning).ToList();
            // POINT 52. Un verdict rassurant est irréversible : l'utilisateur referme le
            // rapport. On compte donc d'abord les arrêts ANORMAUX — écrans bleus, et
            // arrêts inattendus qu'aucun écran bleu n'explique — avant de prononcer quoi
            // que ce soit de rassurant.
            var arretsAnormaux = r.Bsods.Count + ArretsInexpliques(r).Count;

            if (warnings.Count == 0)
            {
                if (arretsAnormaux == 0)
                {
                    r.Verdict = Lang.T("Système sain sur la période analysée : aucun crash ni signe de défaillance détecté.", "System healthy over the period analysed: no crash and no sign of failure detected.");
                    r.VerdictCategory = FaultCategory.None;
                    return;
                }
                r.VerdictCategory = FaultCategory.None;
                r.Verdict = Lang.T(
                    $"La machine s'est arrêtée anormalement {arretsAnormaux} fois sur la période, sans qu'une cause ait pu être établie. Ce n'est pas un système sain : il manque des traces, pas des pannes.",
                    $"The machine shut down abnormally {arretsAnormaux} time(s) over the period, with no cause established. This is not a healthy system: what is missing is evidence, not failures.");
                return;
            }

            var w = warnings.First();
            r.VerdictCategory = w.Category;
            if (arretsAnormaux > 0)
            {
                r.Verdict = Lang.T(
                    $"Aucune cause unique ne se dégage, mais la machine s'est arrêtée anormalement {arretsAnormaux} fois sur la période — ce n'est pas une machine saine. Point le plus notable : {w.Title}.",
                    $"No single cause stands out, but the machine shut down abnormally {arretsAnormaux} time(s) over the period — this is not a healthy machine. Most notable point: {w.Title}.");
                return;
            }
            r.Verdict = Lang.T($"Pas de panne critique, mais des points de vigilance — le plus notable : {w.Title}.", $"No critical failure, but some points to watch — the most notable: {w.Title}.");
            return;
        }

        // Priorité à la preuve la plus forte : un pilote nommé par l'analyse symbolique
        // l'emporte sur les catégories déduites des seuls codes STOP.
        // Reconnaissance par CODE, jamais par le titre : celui-ci est traduit.
        var identified = critical.FirstOrDefault(f => f.Code == "driver.identified");
        if (identified is not null)
        {
            r.VerdictCategory = FaultCategory.Driver;
            // Le nom du pilote était extrait du titre à coups de Split(':') — il est
            // désormais transporté tel quel, sans dépendre d'une ponctuation traduite.
            var name = identified.Subject;
            r.Verdict = Lang.T(
                Lang.T($"Cause identifiée : PILOTE {name} (analyse symbolique des dumps — voir la conclusion dédiée pour la marche à suivre). ({critical.Count} conclusion(s) critique(s))", $" ({critical.Count} critical conclusion(s))"),
                $"Cause identified: DRIVER {name} (symbolic analysis of the dumps — see the dedicated conclusion for what to do). ({critical.Count} critical conclusion(s))");
            return;
        }

        var top = critical.GroupBy(f => f.Category).OrderByDescending(g => g.Count()).First().Key;
        r.VerdictCategory = top;
        r.Verdict = top switch
        {
            FaultCategory.Hardware => Lang.T("Cause la plus probable : MATÉRIELLE (CPU/carte mère/alimentation ou surchauffe). Les erreurs WHEA et/ou codes STOP matériels dominent.", "Most likely cause: HARDWARE (CPU/motherboard/power supply or overheating). WHEA errors and/or hardware STOP codes dominate."),
            FaultCategory.Memory => Lang.T("Cause la plus probable : MÉMOIRE RAM. Les codes STOP et/ou diagnostics pointent vers la RAM.", "Most likely cause: RAM. The STOP codes and/or diagnostics point to the memory."),
            FaultCategory.Storage => Lang.T("Cause la plus probable : STOCKAGE (disque/SSD, câblage ou firmware).", "Most likely cause: STORAGE (disk/SSD, cabling or firmware)."),
            FaultCategory.GpuDriver => Lang.T("Cause la plus probable : PILOTE GRAPHIQUE ou carte graphique (TDR/BSOD vidéo).", "Most likely cause: DISPLAY DRIVER or graphics card (TDR/video BSOD)."),
            FaultCategory.Driver => Lang.T("Cause la plus probable : PILOTE défectueux (voir le détail des BSOD pour le module concerné).", "Most likely cause: a faulty DRIVER (see the BSOD detail for the module involved)."),
            FaultCategory.Power => Lang.T("Cause la plus probable : ALIMENTATION ou surchauffe (coupures brutales sans écran bleu).", "Most likely cause: POWER SUPPLY or overheating (abrupt losses with no blue screen)."),
            FaultCategory.Software => Lang.T("Cause la plus probable : LOGICIELLE (corruption système ou application).", "Most likely cause: SOFTWARE (system corruption or an application)."),
            FaultCategory.Network => Lang.T("Cause la plus probable : RÉSEAU (configuration, profils ou services — pas le matériel).", "Most likely cause: NETWORK (configuration, profiles or services — not the hardware)."),
            _ => Lang.T("Pannes détectées — voir le détail des conclusions ci-dessous.", "Failures detected — see the detail of the conclusions below."),
        } + $" ({critical.Count} conclusion(s) critique(s))";
    }

    /// <summary>Liste marque/modèle des barrettes RAM installées, pour les conclusions mémoire.</summary>
    private static string HardwareRamList(DiagnosticReport r) =>
        r.System.RamModules.Count == 0 ? ""
        : Lang.T(" Barrettes installées : ", " Modules fitted: ") + string.Join(Lang.T(" ; ", "; "),
            r.System.RamModules.Select(m => $"{m.DeviceLocator} {m.Manufacturer} {m.PartNumber} ({FormatBytes(m.CapacityBytes)})")) + ".";

    /// <summary>
    /// Taille lisible, unité et séparateur décimal suivant la langue du rapport.
    ///
    /// Constaté sur un rapport réel du 19/08/2026 : la version anglaise affichait
    /// 147 tailles en « Ko », « Mo » et « Go ». Ni accent, ni mot outil, ni
    /// typographie française — aucun des trois signaux du test de traduction ne
    /// pouvait voir des unités de deux lettres.
    ///
    /// Le séparateur compte autant que l'unité : « 4,2 Go » dans un document
    /// anglais se lit mal, et un lecteur américain peut prendre la virgule pour un
    /// séparateur de milliers — soit dix fois la valeur réelle. C'est le même
    /// raisonnement qui avait fait écarter le jj/mm/aaaa britannique en 1.3.0.
    /// </summary>
    internal static string FormatBytes(ulong bytes)
    {
        string[] units = Lang.IsFrench
            ? ["o", "Ko", "Mo", "Go", "To"]
            : ["B", "KB", "MB", "GB", "TB"];
        double v = bytes; int u = 0;
        while (v >= 1024 && u < units.Length - 1) { v /= 1024; u++; }
        return v.ToString("0.#", Lang.Culture) + " " + units[u];
    }
}
