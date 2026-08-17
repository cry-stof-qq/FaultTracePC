namespace FaultTracePC.Core.Analysis;

/// <summary>
/// Une entrée du catalogue, dans les DEUX langues.
///
/// POURQUOI LES DEUX TEXTES SONT PORTÉS PAR L'ENTRÉE, ET NON RÉSOLUS À LA CONSTRUCTION
/// Le catalogue est un « static readonly » : il est construit UNE FOIS, au premier
/// accès au type, et jamais reconstruit. Un Lang.T() écrit à l'intérieur du
/// dictionnaire aurait donc figé la langue au tout premier accès — celle en cours
/// à cet instant précis, quoi qu'il arrive ensuite. Le sélecteur de langue de
/// l'application n'aurait alors plus aucun effet sur les descriptions de codes
/// STOP, et un test qui bascule la langue aurait obtenu l'autre selon l'ORDRE
/// d'exécution des tests. Le choix se fait donc à la LECTURE, pas au chargement.
/// </summary>
public sealed record BugCheckEntry(
    string Name,
    FaultCategory Category,
    string DescriptionFr,
    string DescriptionEn,
    string AdviceFr,
    string AdviceEn)
{
    /// <summary>Description dans la langue en cours.</summary>
    public string Description => Lang.T(DescriptionFr, DescriptionEn);

    /// <summary>Conseil dans la langue en cours.</summary>
    public string Advice => Lang.T(AdviceFr, AdviceEn);
}

/// <summary>
/// Catalogue des codes STOP les plus fréquents, avec catégorie de cause probable
/// et conseils. Référence : documentation « Bug Check Code Reference » de
/// Microsoft (learn.microsoft.com).
/// </summary>
public static class BugCheckCatalog
{
    public static BugCheckEntry? Lookup(uint code) =>
        Entries.TryGetValue(code, out var e) ? e : null;

    public static string NameOf(uint code) =>
        Lookup(code)?.Name ?? $"BUGCODE_0x{code:X}";

    public static readonly IReadOnlyDictionary<uint, BugCheckEntry> Entries = new Dictionary<uint, BugCheckEntry>
    {
        [0x0A] = new("IRQL_NOT_LESS_OR_EQUAL", FaultCategory.Driver,
            "Accès mémoire invalide à un niveau d'interruption trop élevé — très souvent un pilote défectueux, parfois la RAM.",
            "Invalid memory access at too high an interrupt level — very often a faulty driver, sometimes the RAM.",
            "Mettre à jour les pilotes récents ; tester la RAM si récurrent sans pilote identifié.",
            "Update recently installed drivers; test the RAM if it recurs with no driver identified."),
        [0x1A] = new("MEMORY_MANAGEMENT", FaultCategory.Memory,
            "Erreur grave du gestionnaire de mémoire — RAM défectueuse, pilote corrompant la mémoire, ou fichier d'échange endommagé.",
            "Serious memory manager error — faulty RAM, a driver corrupting memory, or a damaged page file.",
            "Lancer le diagnostic mémoire Windows (mdsched.exe) puis MemTest86 sur plusieurs passes ; retirer tout overclocking XMP le temps du test.",
            "Run the Windows memory diagnostic (mdsched.exe) then MemTest86 over several passes; remove any XMP overclocking for the duration of the test."),
        [0x1E] = new("KMODE_EXCEPTION_NOT_HANDLED", FaultCategory.Driver,
            "Exception non gérée en mode noyau — pilote ou incompatibilité matérielle.",
            "Unhandled kernel-mode exception — driver or hardware incompatibility.",
            "Identifier le module cité, mettre à jour ou désinstaller le pilote correspondant.",
            "Identify the module named, then update or uninstall the matching driver."),
        [0x24] = new("NTFS_FILE_SYSTEM", FaultCategory.Storage,
            "Erreur dans le pilote NTFS — disque défaillant ou système de fichiers corrompu.",
            "Error in the NTFS driver — failing disk or corrupted file system.",
            "Exécuter chkdsk /f /r et vérifier la santé SMART du disque.",
            "Run chkdsk /f /r and check the drive's SMART health."),
        [0x3B] = new("SYSTEM_SERVICE_EXCEPTION", FaultCategory.Driver,
            "Exception dans un service système — pilote graphique ou antivirus fréquemment en cause.",
            "Exception in a system service — display driver or antivirus frequently to blame.",
            "Mettre à jour pilote graphique et solutions de sécurité ; vérifier les fichiers système (sfc /scannow).",
            "Update the display driver and security software; check the system files (sfc /scannow)."),
        [0x50] = new("PAGE_FAULT_IN_NONPAGED_AREA", FaultCategory.Memory,
            "Référence à une mémoire système invalide — RAM défectueuse, pilote, ou antivirus.",
            "Reference to invalid system memory — faulty RAM, a driver, or antivirus.",
            "Diagnostic mémoire ; désinstaller logiciel/pilote installé juste avant l'apparition du problème.",
            "Memory diagnostic; uninstall any software or driver installed just before the problem appeared."),
        [0x7A] = new("KERNEL_DATA_INPAGE_ERROR", FaultCategory.Storage,
            "Échec de lecture de données noyau depuis le fichier d'échange — disque ou câblage défaillant, parfois RAM.",
            "Failed to read kernel data from the page file — failing disk or cabling, sometimes RAM.",
            "Vérifier SMART, câbles SATA/alimentation, chkdsk ; tester la RAM en second.",
            "Check SMART, SATA and power cables, run chkdsk; test the RAM second."),
        [0x7E] = new("SYSTEM_THREAD_EXCEPTION_NOT_HANDLED", FaultCategory.Driver,
            "Exception non gérée dans un thread système — pilote presque toujours en cause.",
            "Unhandled exception in a system thread — almost always a driver.",
            "Le nom du pilote apparaît souvent dans les paramètres/le dump : le mettre à jour ou le restaurer.",
            "The driver name often appears in the parameters or the dump: update it or roll it back."),
        [0x9F] = new("DRIVER_POWER_STATE_FAILURE", FaultCategory.Driver,
            "Un pilote n'a pas répondu à une transition d'alimentation (veille/reprise).",
            "A driver failed to respond to a power transition (sleep/resume).",
            "Mettre à jour pilotes réseau/graphique/chipset ; tester en désactivant la mise en veille rapide.",
            "Update network, display and chipset drivers; test with fast startup disabled."),
        [0xC2] = new("BAD_POOL_CALLER", FaultCategory.Driver,
            "Allocation mémoire noyau invalide — pilote défectueux.",
            "Invalid kernel memory allocation — faulty driver.",
            "Identifier le pilote via le dump ; vérifier pilotes récemment installés.",
            "Identify the driver from the dump; check recently installed drivers."),
        [0xC5] = new("DRIVER_CORRUPTED_EXPOOL", FaultCategory.Driver,
            "Corruption du pool noyau par un pilote.",
            "Kernel pool corrupted by a driver.",
            "Activer le vérificateur de pilotes (verifier.exe) pour identifier le fautif.",
            "Enable Driver Verifier (verifier.exe) to identify the culprit."),
        [0xD1] = new("DRIVER_IRQL_NOT_LESS_OR_EQUAL", FaultCategory.Driver,
            "Un pilote a accédé à une mémoire paginée à un IRQL trop élevé — pilote réseau/stockage souvent en cause.",
            "A driver accessed pageable memory at too high an IRQL — network or storage driver often to blame.",
            "Mettre à jour le pilote cité dans le dump (souvent un .sys réseau, Wi-Fi ou antivirus).",
            "Update the driver named in the dump (often a network, Wi-Fi or antivirus .sys)."),
        [0xEF] = new("CRITICAL_PROCESS_DIED", FaultCategory.Software,
            "Un processus système critique s'est arrêté (csrss, wininit…) — corruption système, disque, parfois malware.",
            "A critical system process stopped (csrss, wininit…) — system corruption, disk, sometimes malware.",
            "sfc /scannow puis DISM /Online /Cleanup-Image /RestoreHealth ; vérifier le disque système.",
            "sfc /scannow then DISM /Online /Cleanup-Image /RestoreHealth; check the system drive."),
        [0xF4] = new("CRITICAL_OBJECT_TERMINATION", FaultCategory.Storage,
            "Arrêt d'un objet critique — très souvent lié à un disque système défaillant ou déconnecté.",
            "A critical object terminated — very often a failing or disconnected system drive.",
            "Vérifier SMART et câblage du disque système en priorité.",
            "Check the system drive's SMART data and cabling first."),
        [0x101] = new("CLOCK_WATCHDOG_TIMEOUT", FaultCategory.Hardware,
            "Un cœur CPU n'a pas répondu aux interruptions — CPU, surchauffe, overclocking ou BIOS.",
            "A CPU core stopped responding to interrupts — CPU, overheating, overclocking or BIOS.",
            "Retirer tout overclocking, vérifier températures CPU et mettre à jour le BIOS.",
            "Remove any overclocking, check CPU temperatures and update the BIOS."),
        [0x116] = new("VIDEO_TDR_FAILURE", FaultCategory.GpuDriver,
            "Le pilote graphique n'a pas répondu (TDR) — pilote GPU, surchauffe GPU ou carte défaillante.",
            "The display driver stopped responding (TDR) — GPU driver, GPU overheating or a failing card.",
            "Installation propre du pilote GPU (DDU) ; surveiller la température GPU ; tester sans overclocking.",
            "Clean reinstall of the GPU driver (DDU); watch the GPU temperature; test without overclocking."),
        [0x117] = new("VIDEO_TDR_TIMEOUT_DETECTED", FaultCategory.GpuDriver,
            "Réinitialisation du pilote graphique après blocage.",
            "The display driver was reset after hanging.",
            "Mêmes vérifications que VIDEO_TDR_FAILURE (pilote GPU, température, alimentation de la carte).",
            "Same checks as VIDEO_TDR_FAILURE (GPU driver, temperature, power to the card)."),
        [0x119] = new("VIDEO_SCHEDULER_INTERNAL_ERROR", FaultCategory.GpuDriver,
            "Erreur interne du planificateur vidéo — pilote GPU.",
            "Internal video scheduler error — GPU driver.",
            "Installation propre du pilote graphique.",
            "Clean reinstall of the display driver."),
        [0x124] = new("WHEA_UNCORRECTABLE_ERROR", FaultCategory.Hardware,
            "Erreur matérielle fatale remontée par le processeur (WHEA) — CPU, carte mère, alimentation, surchauffe ou overclocking instable.",
            "Fatal hardware error reported by the processor (WHEA) — CPU, motherboard, power supply, overheating or unstable overclocking.",
            "Vérifier températures et alimentation ; retirer l'overclocking/XMP ; mettre à jour le BIOS ; si récurrent, suspecter CPU/carte mère.",
            "Check temperatures and power supply; remove overclocking/XMP; update the BIOS; if it recurs, suspect the CPU or motherboard."),
        [0x133] = new("DPC_WATCHDOG_VIOLATION", FaultCategory.Driver,
            "Une routine différée (DPC) a dépassé le temps imparti — pilote de stockage (SSD/NVMe) ou firmware fréquemment en cause.",
            "A deferred procedure call (DPC) ran past its time limit — storage driver (SSD/NVMe) or firmware frequently to blame.",
            "Mettre à jour firmware SSD et pilotes de stockage (stornvme/iaStor), ainsi que les pilotes chipset.",
            "Update the SSD firmware and storage drivers (stornvme/iaStor), as well as the chipset drivers."),
        [0x139] = new("KERNEL_SECURITY_CHECK_FAILURE", FaultCategory.Driver,
            "Corruption détectée par une vérification de sécurité du noyau — pilote ou RAM.",
            "Corruption caught by a kernel security check — driver or RAM.",
            "Diagnostic mémoire + mise à jour des pilotes ; sfc /scannow.",
            "Memory diagnostic plus driver updates; sfc /scannow."),
        [0x13A] = new("KERNEL_MODE_HEAP_CORRUPTION", FaultCategory.Driver,
            "Corruption du tas noyau — pilote (souvent graphique) ou RAM.",
            "Kernel heap corruption — driver (often the display driver) or RAM.",
            "Mettre à jour le pilote graphique ; tester la RAM.",
            "Update the display driver; test the RAM."),
        [0x154] = new("UNEXPECTED_STORE_EXCEPTION", FaultCategory.Storage,
            "Exception dans le gestionnaire de mémoire compressée — souvent disque/SSD défaillant.",
            "Exception in the compressed memory store — often a failing disk or SSD.",
            "Vérifier la santé du disque système (SMART) et son firmware.",
            "Check the system drive's SMART health and its firmware."),
        [0x1CA] = new("SYNTHETIC_WATCHDOG_TIMEOUT", FaultCategory.Software,
            "Le système n'a plus répondu (watchdog) — cause logicielle ou stockage.",
            "The system stopped responding (watchdog) — software or storage cause.",
            "Examiner les événements disque autour du crash.",
            "Examine the disk events around the crash."),
        [0xA0] = new("INTERNAL_POWER_ERROR", FaultCategory.Power,
            "Erreur interne du gestionnaire d'alimentation.",
            "Internal power manager error.",
            "Mettre à jour BIOS et pilotes chipset ; vérifier les paramètres d'alimentation.",
            "Update the BIOS and chipset drivers; check the power settings."),
        [0x7F] = new("UNEXPECTED_KERNEL_MODE_TRAP", FaultCategory.Hardware,
            "Interruption inattendue en mode noyau — RAM/CPU/overclocking ou pilote bas niveau.",
            "Unexpected kernel-mode trap — RAM, CPU, overclocking or a low-level driver.",
            "Retirer l'overclocking, tester la RAM ; vérifier les températures.",
            "Remove overclocking, test the RAM; check temperatures."),
        [0x4E] = new("PFN_LIST_CORRUPT", FaultCategory.Memory,
            "Liste des pages mémoire corrompue — RAM défectueuse très probable.",
            "The memory page list is corrupted — faulty RAM is very likely.",
            "MemTest86 sur plusieurs passes ; tester les barrettes une par une.",
            "MemTest86 over several passes; test the sticks one at a time."),
        [0x12B] = new("FAULTY_HARDWARE_CORRUPTED_PAGE", FaultCategory.Memory,
            "Page mémoire corrompue détectée — RAM défectueuse probable.",
            "A corrupted memory page was detected — faulty RAM is likely.",
            "Diagnostic mémoire approfondi ; vérifier aussi le fichier d'échange et le disque.",
            "Thorough memory diagnostic; also check the page file and the disk."),
        [0xDE] = new("POOL_CORRUPTION_IN_FILE_AREA", FaultCategory.Storage,
            "Corruption mémoire dans une zone fichier.",
            "Memory corruption in a file area.",
            "chkdsk + vérification SMART.",
            "chkdsk plus a SMART check."),
        [0xEA] = new("THREAD_STUCK_IN_DEVICE_DRIVER", FaultCategory.GpuDriver,
            "Un thread tourne en boucle dans un pilote de périphérique — GPU en général.",
            "A thread is stuck looping inside a device driver — usually the GPU.",
            "Installation propre du pilote graphique ; vérifier la carte.",
            "Clean reinstall of the display driver; check the card."),
        [0xFC] = new("ATTEMPTED_EXECUTE_OF_NOEXECUTE_MEMORY", FaultCategory.Driver,
            "Tentative d'exécution d'une zone non exécutable — pilote défectueux ou RAM.",
            "Attempt to execute non-executable memory — faulty driver or RAM.",
            "Mettre à jour les pilotes ; tester la RAM.",
            "Update the drivers; test the RAM."),
        [0x18B] = new("CRITICAL_STRUCTURE_CORRUPTION_LIVEDUMP", FaultCategory.Driver,
            "Corruption de structure critique détectée (live dump).",
            "Critical structure corruption detected (live dump).",
            "Examiner les pilotes non signés ou récemment mis à jour.",
            "Examine unsigned or recently updated drivers."),
    };
}
