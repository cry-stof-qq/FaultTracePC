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
        AnalyzeStorage(r);
        AnalyzeGpu(r);
        AnalyzePowerLoss(r);
        AnalyzeAppCrashes(r);
        AnalyzeServiceFailures(r);
        AnalyzeUpdateCorrelation(r);
        AnalyzeDiskSpace(r);

        if (r.Findings.Count == 0)
        {
            r.Findings.Add(new Finding
            {
                Severity = Severity.Info,
                Confidence = Confidence.High,
                Category = FaultCategory.None,
                Title = "Aucune anomalie significative détectée",
                Details = $"Aucun BSOD, aucune erreur matérielle WHEA, aucune erreur disque et aucun arrêt inattendu sur les {r.ScanPeriodDays} derniers jours.",
                Recommendation = "Si un problème persiste malgré tout, augmenter la période d'analyse ou activer la surveillance temps réel (mode 2) pour capturer le prochain incident."
            });
        }

        // Tri : critiques d'abord, puis avertissements, puis infos.
        r.Findings = r.Findings
            .OrderBy(f => f.Severity)
            .ThenBy(f => f.Confidence)
            .ToList();

        ComputeVerdict(r);
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

        foreach (var e in r.Events.Where(e => e.Category == EventCategory.Bsod))
        {
            uint? code = ParseHex(e.Extracted.GetValueOrDefault("BugCheckCode"));
            var existing = incidents.FirstOrDefault(i =>
                Math.Abs((i.TimeLocal - e.TimeLocal).TotalMinutes) < 10 &&
                (code is null || i.BugCheckCode == code));

            if (existing is not null)
            {
                if (!existing.Sources.Contains("Événement BugCheck 1001"))
                    existing.Sources.Add("Événement BugCheck 1001");
            }
            else
            {
                incidents.Add(new BsodIncident
                {
                    TimeLocal = e.TimeLocal,
                    BugCheckCode = code,
                    BugCheckName = code is null ? "(code non extrait)" : BugCheckCatalog.NameOf(code.Value),
                    DumpPath = e.Extracted.GetValueOrDefault("DumpPath"),
                    Sources = { "Événement BugCheck 1001" },
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

            var details = $"{n} occurrence(s) de l'écran bleu {BugCheckCatalog.NameOf(group.Key)} (0x{group.Key:X8}), dernière le {last:dd/MM/yyyy HH:mm}.";
            if (entry is not null) details += " " + entry.Description;
            if (drivers.Count > 0) details += $" Pilote suspect : {string.Join(", ", drivers)}.";

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
                    ? $"BSOD récurrent : {BugCheckCatalog.NameOf(group.Key)} ({n}×)"
                    : $"BSOD : {BugCheckCatalog.NameOf(group.Key)}",
                Details = details,
                Recommendation = entry?.Advice ?? "Analyser le dump avec WinDbg (!analyze -v) pour identifier le module fautif — automatisé en Phase 2.",
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
                Title = $"{noCode} crash(s) sans code STOP extrait",
                Details = "Un redémarrage après erreur a été journalisé mais le code n'a pas pu être lu (dump absent ou purgé).",
                Recommendation = "Vérifier que la création de dumps est activée : Système > Paramètres avancés > Démarrage et récupération > « Image mémoire du noyau »."
            });
        }
    }

    private static void AnalyzeWhea(DiagnosticReport r)
    {
        var whea = r.Events.Where(e => e.Category == EventCategory.Whea).ToList();
        if (whea.Count == 0) return;

        bool fatal = r.Bsods.Any(b => b.BugCheckCode == 0x124);
        r.Findings.Add(new Finding
        {
            Severity = fatal || whea.Count >= 5 ? Severity.Critical : Severity.Warning,
            Confidence = fatal ? Confidence.High : Confidence.Medium,
            Category = FaultCategory.Hardware,
            Title = $"Erreurs matérielles WHEA détectées ({whea.Count})",
            Details = $"Le processeur a signalé {whea.Count} erreur(s) matérielle(s) (WHEA-Logger) sur la période."
                      + (fatal ? " Un BSOD WHEA_UNCORRECTABLE_ERROR (0x124) confirme une erreur matérielle fatale." : "")
                      + $" Dernier événement : {whea.Max(e => e.TimeLocal):dd/MM/yyyy HH:mm}."
                      + $" Matériel concerné : CPU {r.System.Cpu.Name} · carte mère {r.System.Bios.BaseboardManufacturer} {r.System.Bios.BaseboardProduct} (BIOS {r.System.Bios.Version}).",
            Recommendation = "Vérifier les températures et la stabilité de l'alimentation ; retirer tout overclocking/XMP ; mettre à jour le BIOS. "
                           + "Des WHEA récurrentes pointent vers CPU, carte mère, alimentation ou RAM — à tester dans cet ordre."
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
                Title = "RAM défectueuse confirmée par le diagnostic mémoire Windows",
                Details = "Le diagnostic mémoire Windows (mdsched) a détecté des erreurs matérielles sur la période analysée." + HardwareRamList(r),
                Recommendation = "Tester les barrettes une par une (MemTest86, plusieurs passes) et remplacer la barrette fautive. Désactiver XMP le temps du test."
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
                    ? "BSOD mémoire récurrents — RAM défectueuse OU pénurie causée par la virtualisation"
                    : "Suspicion de RAM défectueuse (BSOD mémoire récurrents)",
                Details = $"{memBsods.Count} BSOD de type mémoire (MEMORY_MANAGEMENT / PAGE_FAULT…) sur la période."
                          + (diagOk ? " Le dernier diagnostic mémoire Windows n'avait rien détecté — MemTest86 est plus sensible." : "")
                          + (vmHeavy ? " ATTENTION : la virtualisation (vmmem) réserve une grosse part de la RAM — voir la conclusion dédiée ; un manque de mémoire peut produire ces mêmes écrans bleus sans que la RAM soit défectueuse." : "")
                          + HardwareRamList(r),
                Recommendation = (vmHeavy ? "1) Limiter la mémoire de la virtualisation (voir conclusion dédiée). 2) " : "")
                               + "Lancer MemTest86 (4+ passes) pour exclure la RAM physique. Si XMP/DOCP est actif, le désactiver et re-tester. "
                               + "L'analyse symbolique WinDbg (case « Analyse profonde » cochée) nommera le module fautif et permettra de trancher."
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
                : $" Pilote installé : {inv.DisplayName} — {inv.CompanyName} v{inv.FileVersion}"
                  + (inv.FileDate is { } fd ? $" du {fd:dd/MM/yyyy}" : "") + ".";
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
                     + (updatedSince ? " Note : le pilote a déjà été mis à jour depuis le dernier crash — le correctif est peut-être déjà en place ; surveiller." : "");
            }
            else if (isMicrosoft)
            {
                reco = $"{g.Key} est un composant de Windows : la correction passe par Windows Update, pas par un site d'éditeur. "
                     + "Le script de réparation vérifie et applique lui-même ces mises à jour (WSL, Windows Update) et t'affiche le résultat. "
                     + (updatedSince
                         ? "Bonne nouvelle : le pilote a été mis à jour depuis le dernier crash — le correctif est peut-être déjà en place ; surveiller si le crash se reproduit."
                         : "Si le crash persiste système à jour, limiter la charge du composant déclencheur en attendant un correctif Microsoft.");
            }
            else
            {
                reco = $"Mettre à jour {g.Key} depuis le site de l'éditeur"
                     + (inv is not null && !string.IsNullOrEmpty(inv.CompanyName) ? $" ({inv.CompanyName})" : "")
                     + ", ou désinstaller le logiciel associé s'il ne sert plus. "
                     + "Si le crash persiste avec la dernière version, revenir à une version antérieure stable."
                     + (updatedSince ? " Note : le pilote a déjà été mis à jour depuis le dernier crash — le problème est peut-être déjà résolu." : "");
            }

            r.Findings.Add(new Finding
            {
                Severity = Severity.Critical,
                Confidence = g.Count() >= 2 ? Confidence.High : Confidence.Medium,
                Category = FaultCategory.Driver,
                Title = g.Count() >= 2
                    ? $"Pilote fautif identifié (récurrent) : {g.Key} — {g.Count()} crashs"
                    : $"Pilote fautif identifié : {g.Key}",
                Details = $"L'analyse symbolique WinDbg (!analyze) désigne {g.Key} dans {g.Count()} dump(s)."
                          + (g.First().FailureBucket is { } b ? $" Signature : {b}." : "")
                          + (processes.Count > 0 ? $" Processus déclencheur : {string.Join(", ", processes)}." : "")
                          + invInfo
                          + (updatedSince ? $" ⚠ Le pilote a été mis à jour APRÈS le dernier crash ({lastCrash:dd/MM/yyyy})." : ""),
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
                Title = $"Corruption mémoire détectée par l'analyse symbolique ({memCorruption} dump(s))",
                Details = "WinDbg conclut à « memory_corruption » : la mémoire a été altérée sans qu'un pilote précis "
                          + "puisse être incriminé. Ce verdict pointe le plus souvent vers la RAM physique (ou un "
                          + "overclocking/XMP instable), parfois vers un pilote qui écrit hors de sa zone."
                          + HardwareRamList(r),
                Recommendation = "MemTest86 en priorité (4+ passes, XMP désactivé). Si la RAM est saine, activer le "
                               + "vérificateur de pilotes avec précaution (voir le script de réparation)."
            });
        }
        else if (pseudo.Count > 0 && analyzed.Count == pseudo.Count)
        {
            r.Findings.Add(new Finding
            {
                Severity = Severity.Warning,
                Confidence = Confidence.Low,
                Category = FaultCategory.None,
                Title = "Analyse symbolique sans coupable direct",
                Details = $"CDB désigne le noyau Windows ({string.Join(", ", pseudo.Select(d => d.FaultingModule).Distinct())}) — "
                          + "cela signifie généralement que le vrai fautif (RAM, matériel ou pilote masqué) a corrompu "
                          + "l'état du système avant le crash, pas que Windows lui-même est en cause.",
                Recommendation = "Croiser avec les autres conclusions (WHEA, mémoire, disque) ; tester la RAM."
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
            Title = $"Mémoire saturée : Windows a détecté l'épuisement de la mémoire virtuelle ({events.Count}×)",
            Details = "Windows a diagnostiqué une pénurie de mémoire virtuelle (événement Resource-Exhaustion-Detector 2004). "
                      + (culprits.Count > 0
                          ? $"Processus les plus gourmands identifiés par Windows : {string.Join(", ", culprits)}. "
                          : "")
                      + "Ce profil est LOGICIEL : un programme consomme toute la mémoire (virtualisation, fuite mémoire, trop d'applications), "
                      + "ce qui provoque gels, plantages d'applications et parfois des BSOD mémoire — sans que la RAM soit défectueuse."
                      + $" Dernière occurrence : {events.Max(e => e.TimeLocal):dd/MM/yyyy HH:mm}.",
            Recommendation = "Limiter la mémoire du processus en cause (ex. pour la virtualisation : réduire la RAM allouée aux VM, "
                           + "ou fichier .wslconfig pour WSL/Docker avec « memory=8GB »). Vérifier la taille du fichier d'échange "
                           + "(recommandé : géré automatiquement). Le script de réparation inclut ces vérifications."
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
            Title = $"Pression mémoire ÉLEVÉE en ce moment ({commitUsedPct:0} % de la mémoire virtuelle utilisée)",
            Details = $"Au moment du scan : mémoire physique utilisée à {physUsedPct:0} %, mémoire virtuelle (RAM + fichier d'échange) à {commitUsedPct:0} %. "
                      + (top.Count > 0 ? $"Plus gros consommateurs actuels : {string.Join(", ", top)}." : ""),
            Recommendation = "Voir la section « Processus en cours » du rapport pour le détail complet, et réduire la consommation du ou des processus en tête."
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
            Title = $"La virtualisation réserve {FormatBytes((ulong)vmBytes)} de RAM ({pct:0} %) — {vmNames.Split(' ')[0]}",
            Details = $"Les processus de virtualisation ({vmNames}) occupent {pct:0} % de la mémoire de la machine. "
                      + "« vmmem » héberge WSL2, Docker Desktop ou les machines virtuelles Hyper-V : par défaut il peut "
                      + "grossir jusqu'à consommer presque toute la RAM, ce qui provoque gels et plantages d'applications"
                      + (memCrashes
                          ? " — et des BSOD de type mémoire peuvent en découler quand un pilote gère mal la pénurie. "
                            + "Vu les crashs mémoire relevés sur cette machine, cette piste LOGICIELLE doit être vérifiée "
                            + "AVANT de conclure à une RAM défectueuse."
                          : "."),
            Recommendation = "Limiter la mémoire de la virtualisation : pour WSL2/Docker, créer le fichier "
                           + @"%USERPROFILE%\.wslconfig contenant deux lignes « [wsl2] » puis « memory=8GB » (à adapter), "
                           + "puis exécuter « wsl --shutdown ». Pour une VM Hyper-V : réduire sa RAM ou activer la mémoire dynamique."
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
            Title = $"{older.Count} crash(s) antérieurs à la période analysée ({r.ScanPeriodDays} jours)",
            Details = $"Des dumps de crash datent d'avant la fenêtre d'analyse (le plus récent : {older.Max(b => b.TimeLocal):dd/MM/yyyy}). "
                      + "Le journal d'événements de ces dates n'a donc pas été examiné : le diagnostic de ces crashs est incomplet.",
            Recommendation = "Relancer le scan avec une période de 90 jours pour corréler ces crashs avec les événements de l'époque."
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
                Category = latest.RuleId switch
                {
                    "cpu_temp" or "gpu_temp" or "whea" or "power41" => FaultCategory.Hardware,
                    "commit" or "exhaustion" => FaultCategory.Software,
                    _ when latest.RuleId.StartsWith("disk", StringComparison.Ordinal) => FaultCategory.Storage,
                    _ => FaultCategory.None,
                },
                Title = g.Count() > 1
                    ? $"⚠ Alerte préventive répétée ({g.Count()}×) : {latest.Title}"
                    : $"⚠ Alerte préventive : {latest.Title}",
                Details = latest.Details + $" Détecté en temps réel par la surveillance, dernière fois le {latest.Time:dd/MM/yyyy à HH:mm}.",
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
                    Title = "Surveillance temps réel non installée — le contexte des crashs est perdu",
                    Details = "Cette machine a des crashs mais aucune boîte noire : impossible de savoir quelles étaient les températures, la mémoire et les processus au moment exact des pannes.",
                    Recommendation = "Activer la surveillance temps réel (bouton « 📡 » dans FaultTracePC) : le prochain crash sera capturé avec ses dernières secondes de contexte."
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
                    Title = $"SURCHAUFFE mesurée juste avant le crash du {ctx.CrashTime:dd/MM HH:mm} "
                          + $"({(maxCpuTemp >= 90 ? $"CPU {maxCpuTemp:0} °C" : $"GPU {maxGpuTemp:0} °C")})",
                    Details = $"La boîte noire montre {(maxCpuTemp >= 90 ? $"le CPU à {maxCpuTemp:0} °C" : $"le GPU à {maxGpuTemp:0} °C")} "
                            + "dans les secondes précédant le crash — une surchauffe qui déclenche la protection matérielle. "
                            + $"Derniers relevés : CPU {last.CpuLoad:0} % / {last.CpuTemp:0} °C, mémoire {last.MemPct:0} %.",
                    Recommendation = "Dépoussiérer radiateurs et ventilateurs, vérifier leur rotation, renouveler la pâte thermique si la machine a plusieurs années, contrôler la ventilation du boîtier."
                });
            }
            else if (maxCommit >= 95)
            {
                r.Findings.Add(new Finding
                {
                    Severity = Severity.Critical,
                    Confidence = Confidence.High,
                    Category = FaultCategory.Software,
                    Title = $"Mémoire virtuelle SATURÉE juste avant le crash du {ctx.CrashTime:dd/MM HH:mm} ({maxCommit:0} %)",
                    Details = $"La boîte noire montre la mémoire virtuelle à {maxCommit:0} % dans les secondes précédant le crash. "
                            + $"Processus dominants alors : {ctx.Samples.LastOrDefault(s => s.TopProcesses is not null)?.TopProcesses ?? "non relevés"}.",
                    Recommendation = "Identifier le processus dominant ci-dessus et limiter sa consommation (virtualisation → .wslconfig ; fuite mémoire → mise à jour de l'application)."
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
                Title = $"{f.AbruptSessionEnds} arrêt(s) brutal(aux) détecté(s) par la boîte noire",
                Details = "Le journal de surveillance s'est interrompu sans arrêt propre — la machine s'est éteinte brutalement. Voir la section « Boîte noire » pour les derniers relevés.",
                Recommendation = "Croiser avec les conclusions alimentation/température ci-dessus."
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
                    ? $"le disque lève lui-même une ALERTE CRITIQUE : {detail}"
                    : "le disque signale lui-même une DÉFAILLANCE IMMINENTE (SMART)");
                reco = "Sauvegarder les données MAINTENANT et remplacer ce disque. Ne pas attendre.";
            }

            if (isNvme)
            {
                // La réserve de blocs de remplacement est LE signal de fin de vie d'un NVMe.
                if (s.SpareExhausted)
                {
                    severity = Severity.Critical;
                    facts.Add($"réserve de blocs de remplacement épuisée : {s.AvailableSparePercent} % restants "
                            + $"pour un seuil constructeur de {s.AvailableSpareThresholdPercent} %");
                    if (reco.Length == 0)
                        reco = "Le disque n'a plus de blocs de rechange pour compenser l'usure : sauvegarder et remplacer.";
                }
                else if (s.AvailableSparePercent is { } sp && s.AvailableSpareThresholdPercent is { } th && th > 0 && sp <= th + 10)
                {
                    severity = severity == Severity.Critical ? severity : Severity.Warning;
                    facts.Add($"réserve de blocs proche du seuil : {sp} % pour un seuil de {th} %");
                    if (reco.Length == 0)
                        reco = "La réserve approche du seuil constructeur : surveiller son évolution à chaque scan et prévoir le remplacement.";
                }

                // Media and Data Integrity Errors : des données que le contrôleur
                // n'a pas su restituer. C'est l'équivalent NVMe d'un secteur illisible.
                if (s.UncorrectableSectors is { } media && media > 0)
                {
                    severity = media >= 10 ? Severity.Critical
                             : severity == Severity.Critical ? severity : Severity.Warning;
                    facts.Add($"{media} erreur(s) d'intégrité des données non corrigée(s)");
                    reco += (reco.Length > 0 ? " " : "")
                         + "Ces erreurs signifient que le disque n'a pas pu restituer des données qu'il avait écrites. "
                         + "Sauvegarder, vérifier l'intégrité des fichiers importants, et surveiller si le compteur augmente : "
                         + "une progression d'un scan à l'autre condamne le disque.";
                }

                if (s.UnsafeShutdowns is { } us && s.PowerCycles is { } pc && pc > 10 && us > pc / 2)
                    facts.Add($"{us} arrêt(s) brutal(s) sur {pc} démarrages — coupures d'alimentation fréquentes");
            }

            // Secteurs défectueux : le cœur de la question « mon disque est-il bon ? »
            if (isNvme) { /* le vocabulaire « secteurs » ne s'applique pas au NVMe */ }
            else if (s.PendingSectors is > 0)
            {
                severity = Severity.Critical;
                facts.Add($"{s.PendingSectors} secteur(s) instable(s) en attente de réallocation");
                reco = "Secteurs en cours de dégradation : sauvegarder sans tarder, puis lancer une vérification complète du disque (chkdsk /r) qui forcera leur traitement. Si le nombre augmente d'un scan à l'autre, remplacer le disque.";
            }
            else if (s.ReallocatedSectors is > 0 || s.UncorrectableSectors is > 0)
            {
                severity = severity == Severity.Critical ? severity : Severity.Warning;
                var bad = s.BadSectors;
                facts.Add($"{bad} secteur(s) défectueux déjà remplacés par la réserve");
                if (reco.Length == 0)
                    reco = bad >= 50
                        ? "Le nombre de secteurs défectueux est élevé : prévoir le remplacement du disque et surveiller son évolution à chaque scan."
                        : "Quelques secteurs défectueux isolés sont tolérables sur un disque ancien ; ce qui compte est leur ÉVOLUTION — FaultTracePC la suivra d'un scan à l'autre.";
            }

            // Attribut 199 : presque toujours un problème de câble, pas de disque.
            if (s.UdmaCrcErrors is > 0)
            {
                severity = severity == Severity.Critical ? severity : Severity.Warning;
                facts.Add($"{s.UdmaCrcErrors} erreur(s) de transmission (CRC)");
                reco += (reco.Length > 0 ? " " : "")
                     + "Les erreurs CRC viennent presque toujours du CÂBLE SATA ou de son connecteur, pas du disque : rebrancher fermement des deux côtés, ou remplacer le câble (quelques euros) avant d'envisager autre chose.";
            }

            // Usure SSD
            if (s.SsdLifeLeftPercent is { } life)
            {
                if (life <= 10)
                {
                    severity = Severity.Critical;
                    facts.Add($"durée de vie restante du SSD : {life} %");
                    reco += (reco.Length > 0 ? " " : "") + "Le SSD arrive en fin de vie : prévoir son remplacement.";
                }
                else if (life <= 25)
                {
                    severity = severity == Severity.Critical ? severity : Severity.Warning;
                    facts.Add($"durée de vie restante du SSD : {life} %");
                    reco += (reco.Length > 0 ? " " : "") + "Usure avancée : surveiller et prévoir le remplacement à moyen terme.";
                }
            }

            if (facts.Count == 0) continue;

            var age = s.PowerOnHours is { } h ? $" Disque en service depuis {h / 24 / 365.0:0.#} an(s) ({h} heures)." : "";
            r.Findings.Add(new Finding
            {
                Severity = severity,
                Confidence = Confidence.High,
                Category = FaultCategory.Storage,
                Title = severity == Severity.Critical
                    ? $"Disque à remplacer : {d.Model}"
                    : $"Disque à surveiller : {d.Model}",
                Details = $"Analyse SMART — {string.Join(" ; ", facts)}.{age} Source : {s.Source}.",
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
                ? $" Le plus long épisode a duré {longest.Minutes:0.#} minute(s) le {longest.Start:dd/MM à HH:mm}, avec une pointe à {longest.PeakC:0.#} °C."
                : "";
            var context = $" Mesuré sur {ThermalHistory.Humanize(t.Observed)} de relevés"
                        + (t.MaxC is { } mx ? $", maximum {mx:0.#} °C le {t.MaxAt:dd/MM à HH:mm}" : "") + ".";

            if (crit >= TimeSpan.FromMinutes(5))
            {
                r.Findings.Add(new Finding
                {
                    Severity = Severity.Critical,
                    Confidence = Confidence.High,
                    Category = FaultCategory.Hardware,
                    Title = $"Surchauffe — {t.Sensor} : {ThermalHistory.Humanize(crit)} au-dessus de {t.CritThreshold:0} °C",
                    Details = $"Le {t.Sensor.ToLowerInvariant()} a passé {ThermalHistory.Humanize(crit)} au-delà du seuil critique "
                            + $"de {t.CritThreshold:0} °C, et {ThermalHistory.Humanize(warn)} au-delà de {t.WarnThreshold:0} °C "
                            + $"({t.WarnPercent:0.#} % du temps mesuré)." + episode + context
                            + " À ces températures, la machine se protège en ralentissant, puis s'éteint brutalement — "
                            + "des arrêts que rien, dans les journaux, ne relie spontanément à la chaleur.",
                    Recommendation = "Dépoussiérer les ventilateurs et les grilles d'aération, vérifier qu'aucune sortie d'air n'est obstruée, "
                                   + "et sur une machine de plus de trois ans envisager le remplacement de la pâte thermique. "
                                   + "Retirer tout overclocking. Sur un portable, éviter de l'utiliser posé sur un lit ou un canapé, qui bouchent les aérations.",
                });
            }
            else if (warn >= TimeSpan.FromMinutes(30) || t.WarnPercent >= 20)
            {
                r.Findings.Add(new Finding
                {
                    Severity = Severity.Warning,
                    Confidence = Confidence.High,
                    Category = FaultCategory.Hardware,
                    Title = $"Températures élevées — {t.Sensor} : {ThermalHistory.Humanize(warn)} au-dessus de {t.WarnThreshold:0} °C",
                    Details = $"Le {t.Sensor.ToLowerInvariant()} a passé {ThermalHistory.Humanize(warn)} au-delà de {t.WarnThreshold:0} °C, "
                            + $"soit {t.WarnPercent:0.#} % du temps mesuré." + episode + context
                            + " Ce n'est pas une panne, mais c'est le signe avant-coureur des arrêts thermiques.",
                    Recommendation = "Dépoussiérer les aérations et surveiller l'évolution : si la durée passée trop haut augmente d'un scan à l'autre, "
                                   + "le refroidissement se dégrade. Vérifier aussi qu'un logiciel ne sollicite pas le matériel en permanence "
                                   + "(voir les processus en cours dans ce rapport).",
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
                    Title = "Usure de la batterie non mesurable",
                    Details = $"La batterie « {b.Name} » est détectée, mais son firmware n'expose pas les capacités nécessaires au calcul d'usure.",
                    Recommendation = "Utiliser le rapport de batterie Windows (bouton dédié dans la boîte à outils) pour une analyse détaillée.",
                });
                continue;
            }

            var health = 100 - wear;
            var capacity = b.DesignedCapacity is { } dc && b.FullChargedCapacity is { } fc
                ? $" Elle ne retient plus que {fc} mWh sur les {dc} mWh prévus d'origine."
                : "";
            var cycles = b.CycleCount is { } c and > 0 ? $" {c} cycles de charge." : "";

            var (sev, title, reco) = wear switch
            {
                >= 70 => (Severity.Critical, $"Batterie HORS D'USAGE — {health} % de santé restante",
                          "La batterie ne tient pratiquement plus la charge : la machine s'éteindra dès qu'elle sera débranchée. Remplacement nécessaire."),
                >= 40 => (Severity.Warning, $"Batterie très usée — {health} % de santé restante",
                          "L'autonomie est fortement réduite. Prévoir le remplacement de la batterie ; en attendant, éviter de compter sur elle en déplacement."),
                >= 20 => (Severity.Info, $"Batterie usée — {health} % de santé restante",
                          "Usure normale pour une batterie de quelques années. Rien d'urgent : surveiller l'évolution."),
                _ => (Severity.Info, $"Batterie en bon état — {health} % de santé restante",
                      "Aucune action nécessaire."),
            };

            r.Findings.Add(new Finding
            {
                Severity = sev,
                Confidence = Confidence.High,
                Category = FaultCategory.Hardware,
                Title = title,
                Details = $"Usure mesurée : {wear} %.{capacity}{cycles}"
                        + (b.ChargeRemainingPercent is { } ch ? $" Charge actuelle : {ch} %." : ""),
                Recommendation = reco,
            });
        }
    }

    private static void AnalyzeStorage(DiagnosticReport r)
    {
        var diskEvents = r.Events.Where(e => e.Category == EventCategory.DiskError).ToList();
        var badDisks = r.System.Disks.Where(d =>
            (d.HealthStatus is "Avertissement" or "Défaillant") ||
            (!string.IsNullOrEmpty(d.WmiStatus) && !d.WmiStatus.Equals("OK", StringComparison.OrdinalIgnoreCase))).ToList();
        var storageBsods = r.Bsods.Where(b => b.BugCheckCode is 0x24 or 0x7A or 0xF4 or 0x154 or 0xDE).ToList();

        foreach (var d in badDisks)
        {
            r.Findings.Add(new Finding
            {
                Severity = Severity.Critical,
                Confidence = Confidence.High,
                Category = FaultCategory.Storage,
                Title = $"Disque en mauvaise santé : {d.Model}",
                Details = $"État signalé : {(string.IsNullOrEmpty(d.HealthStatus) ? d.WmiStatus : d.HealthStatus)}."
                          + (d.ReadErrorsTotal > 0 ? $" {d.ReadErrorsTotal} erreurs de lecture cumulées." : ""),
                Recommendation = "Sauvegarder immédiatement les données puis remplacer le disque. Vérifier le rapport SMART complet (CrystalDiskInfo) pour confirmation."
            });
        }

        if (diskEvents.Count >= 3 || (diskEvents.Count > 0 && storageBsods.Count > 0))
        {
            var byProvider = diskEvents.GroupBy(e => e.Provider)
                .Select(g => $"{g.Key} ×{g.Count()}").ToList();
            r.Findings.Add(new Finding
            {
                Severity = storageBsods.Count > 0 ? Severity.Critical : Severity.Warning,
                Confidence = storageBsods.Count > 0 ? Confidence.High : Confidence.Medium,
                Category = FaultCategory.Storage,
                Title = $"Erreurs disque répétées ({diskEvents.Count})",
                Details = $"Sources : {string.Join(", ", byProvider)}."
                          + (storageBsods.Count > 0 ? $" Corrélées à {storageBsods.Count} BSOD de type stockage." : "")
                          + " L'événement disk 153 / stornvme 129 signale des opérations retentées : disque, câble ou contrôleur en cause.",
                Recommendation = "Vérifier câbles SATA/alimentation, mettre à jour le firmware du SSD, exécuter chkdsk, surveiller le SMART."
            });
        }
    }

    private static void AnalyzeGpu(DiagnosticReport r)
    {
        var tdr = r.Events.Where(e => e.Category == EventCategory.Tdr).ToList();
        var gpuBsods = r.Bsods.Where(b => b.BugCheckCode is 0x116 or 0x117 or 0x119 or 0xEA).ToList();
        if (tdr.Count == 0 && gpuBsods.Count == 0) return;

        var drivers = tdr.Select(e => e.Extracted.GetValueOrDefault("Driver"))
                         .Where(d => !string.IsNullOrWhiteSpace(d)).Distinct().ToList();

        r.Findings.Add(new Finding
        {
            Severity = gpuBsods.Count > 0 ? Severity.Critical : Severity.Warning,
            Confidence = (tdr.Count + gpuBsods.Count) >= 3 ? Confidence.High : Confidence.Medium,
            Category = FaultCategory.GpuDriver,
            Title = $"Instabilité du pilote graphique ({tdr.Count} réinitialisation(s), {gpuBsods.Count} BSOD)",
            Details = $"Le pilote d'affichage a cessé de répondre puis a été récupéré (TDR){(drivers.Count > 0 ? $" — pilote : {string.Join(", ", drivers!)}" : "")}."
                      + " Des TDR répétés indiquent pilote GPU instable, surchauffe GPU ou carte défaillante."
                      + (r.System.Gpus.Count > 0
                          ? $" Matériel concerné : {string.Join(" ; ", r.System.Gpus.Select(g => $"{g.Name} (pilote {g.DriverVersion} du {g.DriverDate:dd/MM/yyyy})"))}."
                          : ""),
            Recommendation = "Désinstallation propre du pilote (DDU en mode sans échec) puis installation de la dernière version stable ; surveiller la température GPU en charge ; tester sans overclocking."
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
            Title = $"{hardLosses.Count} coupure(s) brutale(s) sans écran bleu",
            Details = "Le système s'est éteint sans arrêt propre ni BSOD enregistré (Kernel-Power 41, code 0). "
                      + "Causes typiques : alimentation (PSU) défaillante ou sous-dimensionnée, surchauffe déclenchant la protection thermique, "
                      + "câble/prise, ou blocage matériel complet. Ce profil n'est PAS un bug logiciel classique."
                      + $" Dernière occurrence : {hardLosses.Max(e => e.TimeLocal):dd/MM/yyyy HH:mm}.",
            Recommendation = "Vérifier températures CPU/GPU en charge, dépoussiérer, contrôler les branchements. Si récurrent, tester avec une autre alimentation. "
                           + "La surveillance temps réel (mode 2) enregistrera les températures juste avant la prochaine coupure."
        });
    }

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

            r.Findings.Add(new Finding
            {
                Severity = stillActive ? Severity.Warning : Severity.Info,
                Confidence = Confidence.High,
                Category = FaultCategory.Software,
                Title = stillActive
                    ? $"Application instable : {g.Key} ({g.Count()} crashs)"
                    : $"Application anciennement instable : {g.Key} ({g.Count()} crashs) — {statusText}",
                Details = $"{g.Count()} plantages sur la période, dernier le {lastCrash:dd/MM/yyyy}"
                          + (modules.Count > 0 ? $", module(s) fautif(s) : {string.Join(", ", modules!)}" : "")
                          + $". État actuel : {statusText}",
                Recommendation = statusReco,
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
                Title = $"Module fautif commun à plusieurs applications : {crossModule.Key}",
                Details = $"Ce module apparaît dans les crashs de {crossModule.Select(e => e.Extracted.GetValueOrDefault("App", "")).Distinct().Count()} applications différentes — la cause est probablement ce composant, pas les applications.",
                Recommendation = "Identifier à quoi appartient ce module (pilote, runtime, antivirus, overlay) et le mettre à jour ou le désinstaller."
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
            return ("non vérifié (inventaire des logiciels indisponible)",
                    "Réinstaller ou mettre à jour l'application.", true);

        var app = Collectors.InstalledSoftwareCollector.FindByExecutable(r.System.InstalledApps, exeName);
        if (app is null)
        {
            return ("ce logiciel ne figure plus parmi les programmes installés — problème probablement sans objet",
                    "Aucune action : le logiciel semble avoir été désinstallé depuis. Si les crashs persistent, c'est qu'il subsiste sous une autre forme (application portable ou du Microsoft Store).",
                    false);
        }

        var version = string.IsNullOrEmpty(app.Version) ? "" : $" v{app.Version}";
        if (app.InstallDate is { } installed && installed.Date > lastCrash.Date)
        {
            return ($"toujours installé ({app.Name}{version}), mais RÉINSTALLÉ ou MIS À JOUR le {installed:dd/MM/yyyy}, après le dernier crash — le problème est peut-être déjà corrigé",
                    "Surveiller : si aucun nouveau crash n'apparaît au prochain scan, l'affaire est close.",
                    false);
        }

        return ($"toujours installé ({app.Name}{version}"
                + (app.InstallDate is { } d ? $", installé le {d:dd/MM/yyyy}" : "") + ") — problème toujours d'actualité",
                $"Mettre à jour {app.Name} vers sa dernière version, ou le réinstaller proprement. "
                + "Si le module fautif est une DLL système ou de pilote (graphique, antivirus), traiter ce composant en priorité.",
                true);
    }

    private static void AnalyzeServiceFailures(DiagnosticReport r)
    {
        var fails = r.Events.Where(e => e.Category == EventCategory.ServiceFailure).ToList();
        if (fails.Count < 5) return;
        r.Findings.Add(new Finding
        {
            Severity = Severity.Info,
            Confidence = Confidence.Medium,
            Category = FaultCategory.Software,
            Title = $"Échecs de services Windows répétés ({fails.Count})",
            Details = "Des services n'ont pas démarré ou se sont arrêtés de façon inattendue de manière répétée.",
            Recommendation = "Consulter le détail dans la section Événements pour identifier le(s) service(s) concerné(s)."
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
                Title = $"{correlated.Count} crash(s) survenus dans les 48 h après une mise à jour Windows",
                Details = "Corrélation temporelle uniquement — ce n'est pas une preuve de causalité, mais un point à vérifier si les crashs ont commencé après une mise à jour précise.",
                Recommendation = "Si le début des crashs coïncide avec une mise à jour, envisager sa désinstallation (Paramètres > Windows Update > Historique) ou une mise à jour des pilotes concernés."
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
                Title = $"Espace disque faible sur {v.Letter} ({v.PercentFree} % libre)",
                Details = $"Volume {v.Letter} ({v.Label}) : {FormatBytes(v.FreeBytes)} libres sur {FormatBytes(v.SizeBytes)}. Un disque système saturé provoque lenteurs et échecs d'écriture du fichier d'échange ou des dumps.",
                Recommendation = "Libérer de l'espace (nettoyage de disque, %TEMP%, anciens dumps volumineux comme MEMORY.DMP une fois analysé)."
            });
        }
    }

    private static void ComputeVerdict(DiagnosticReport r)
    {
        var critical = r.Findings.Where(f => f.Severity == Severity.Critical).ToList();
        if (critical.Count == 0)
        {
            var warnings = r.Findings.Where(f => f.Severity == Severity.Warning).ToList();
            if (warnings.Count == 0)
            {
                r.Verdict = "Système sain sur la période analysée : aucun crash ni signe de défaillance détecté.";
                r.VerdictCategory = FaultCategory.None;
                return;
            }
            var w = warnings.First();
            r.VerdictCategory = w.Category;
            r.Verdict = $"Pas de panne critique, mais des points de vigilance — le plus notable : {w.Title}.";
            return;
        }

        // Priorité à la preuve la plus forte : un pilote nommé par l'analyse symbolique
        // l'emporte sur les catégories déduites des seuls codes STOP.
        var identified = critical.FirstOrDefault(f => f.Title.StartsWith("Pilote fautif identifié", StringComparison.Ordinal));
        if (identified is not null)
        {
            r.VerdictCategory = FaultCategory.Driver;
            var name = identified.Title.Split(':').Length > 1 ? identified.Title.Split(':')[1].Split('—')[0].Trim() : "";
            r.Verdict = $"Cause identifiée : PILOTE {name} (analyse symbolique des dumps — voir la conclusion dédiée pour la marche à suivre). ({critical.Count} conclusion(s) critique(s))";
            return;
        }

        var top = critical.GroupBy(f => f.Category).OrderByDescending(g => g.Count()).First().Key;
        r.VerdictCategory = top;
        r.Verdict = top switch
        {
            FaultCategory.Hardware => "Cause la plus probable : MATÉRIELLE (CPU/carte mère/alimentation ou surchauffe). Les erreurs WHEA et/ou codes STOP matériels dominent.",
            FaultCategory.Memory => "Cause la plus probable : MÉMOIRE RAM. Les codes STOP et/ou diagnostics pointent vers la RAM.",
            FaultCategory.Storage => "Cause la plus probable : STOCKAGE (disque/SSD, câblage ou firmware).",
            FaultCategory.GpuDriver => "Cause la plus probable : PILOTE GRAPHIQUE ou carte graphique (TDR/BSOD vidéo).",
            FaultCategory.Driver => "Cause la plus probable : PILOTE défectueux (voir le détail des BSOD pour le module concerné).",
            FaultCategory.Power => "Cause la plus probable : ALIMENTATION ou surchauffe (coupures brutales sans écran bleu).",
            FaultCategory.Software => "Cause la plus probable : LOGICIELLE (corruption système ou application).",
            _ => "Pannes détectées — voir le détail des conclusions ci-dessous.",
        } + $" ({critical.Count} conclusion(s) critique(s))";
    }

    /// <summary>Liste marque/modèle des barrettes RAM installées, pour les conclusions mémoire.</summary>
    private static string HardwareRamList(DiagnosticReport r) =>
        r.System.RamModules.Count == 0 ? ""
        : " Barrettes installées : " + string.Join(" ; ",
            r.System.RamModules.Select(m => $"{m.DeviceLocator} {m.Manufacturer} {m.PartNumber} ({FormatBytes(m.CapacityBytes)})")) + ".";

    internal static string FormatBytes(ulong bytes)
    {
        string[] units = ["o", "Ko", "Mo", "Go", "To"];
        double v = bytes; int u = 0;
        while (v >= 1024 && u < units.Length - 1) { v /= 1024; u++; }
        return $"{v:0.#} {units[u]}";
    }
}
