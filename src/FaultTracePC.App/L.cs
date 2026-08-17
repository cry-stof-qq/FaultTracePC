using FaultTracePC.Core;

namespace FaultTracePC.App;

/// <summary>
/// Libellés de l'interface, destinés au XAML.
///
/// POURQUOI CETTE CLASSE EXISTE
/// Le XAML ne peut pas appeler <c>Lang.T(fr, en)</c> : c'est du balisage, pas du
/// code. Il sait en revanche lire une propriété statique — <c>{x:Static loc:L.X}</c>.
/// Chaque libellé est donc une propriété d'une ligne, avec les deux langues côte à
/// côte, exactement comme partout ailleurs dans le logiciel. Aucun système de clés
/// à maintenir en parallèle du texte, et rien qui puisse « manquer » en silence :
/// une propriété absente ne compile pas.
///
/// UNE LIMITE À CONNAÎTRE
/// <c>{x:Static}</c> est résolu à la CONSTRUCTION de la fenêtre. Changer de langue
/// en cours d'exécution n'affecte donc pas une fenêtre déjà ouverte : il faut la
/// rouvrir. C'est sans conséquence au démarrage, puisque Lang.Initialize s'exécute
/// dans App.OnStartup, avant la création de la première fenêtre.
/// </summary>
public static class L
{
    // ---------------------------------------------------------------- fenêtre principale
    public static string MainTitle => Lang.T("FaultTracePC — Diagnostic de pannes Windows",
                                             "FaultTracePC — Windows fault diagnosis");
    public static string MainTagline => Lang.T(
        "Trouver la cause d'une panne Windows 10/11 — scan post-mortem et surveillance temps réel",
        "Find the cause of a Windows 10/11 failure — post-mortem scan and real-time monitoring");
    public static string MainAboutTip => Lang.T("Version installée et informations utiles au dépannage",
                                                "Installed version and information useful for troubleshooting");

    public static string MainScan => Lang.T("🔍  Analyser cette machine", "🔍  Analyse this machine");
    public static string MainMonitor => Lang.T("📡  Surveillance temps réel", "📡  Real-time monitoring");
    public static string MainMonitorTip => Lang.T(
        "Installe/désinstalle le service boîte noire : journal continu (températures, mémoire, événements) écrit sur disque pour retrouver les secondes précédant un crash. Moins de 1 % de CPU.",
        "Installs/removes the flight recorder service: a continuous log (temperatures, memory, events) written to disk so the seconds before a crash can be recovered. Less than 1% CPU.");
    public static string MainLive => Lang.T("📈  Voir en direct", "📈  Watch live");
    public static string MainLiveTip => Lang.T(
        "Ouvre le visualiseur du journal : relevés en direct (CPU, températures), historique sur plusieurs jours, données brutes, bascule °C/°F.",
        "Opens the log viewer: live readings (CPU, temperatures), history over several days, raw data, °C/°F toggle.");
    public static string MainNetwork => Lang.T("🌐  Réseau", "🌐  Network");
    public static string MainNetworkTip => Lang.T(
        "Mode réseau de cette machine : Local (rien d'exposé) ou Client (télémétrie et rapports en lecture seule, réservés aux adresses privées munies du token).",
        "Network mode of this machine: Local (nothing exposed) or Client (read-only telemetry and reports, restricted to private addresses holding the token).");
    public static string MainFleet => Lang.T("🖥  Parc", "🖥  Fleet");
    public static string MainFleetTip => Lang.T(
        "Console maître : état temps réel et rapports de toutes tes machines clientes.",
        "Master console: live state and reports of all your client machines.");
    public static string MainGuided => Lang.T("🧭  Je ne sais pas ce que j'ai", "🧭  I don't know what's wrong with it");
    public static string MainGuidedTip => Lang.T(
        "Assistant guidé : point de restauration, analyse, réparations sans risque, puis vérification. Une conclusion en une phrase, et les actions qui demandent ton accord proposées une par une.",
        "Guided assistant: restore point, analysis, risk-free repairs, then a re-check. One sentence of conclusion, and the actions needing your agreement offered one at a time.");
    public static string MainTools => Lang.T("🧰  Outils", "🧰  Tools");
    public static string MainToolsTip => Lang.T(
        "Boîte à outils de réparation : désinstaller une mise à jour Windows, réinitialiser Windows Update, sfc/DISM/chkdsk, diagnostic mémoire… à un clic, dans une fenêtre PowerShell visible.",
        "Repair toolbox: uninstall a Windows update, reset Windows Update, sfc/DISM/chkdsk, memory diagnostic… one click each, in a visible PowerShell window.");

    public static string MainPeriod => Lang.T("Période :", "Period:");
    public static string MainDays7 => Lang.T("7 jours", "7 days");
    public static string MainDays30 => Lang.T("30 jours", "30 days");
    public static string MainDays90 => Lang.T("90 jours", "90 days");
    public static string MainDriverInventory => Lang.T("Inventaire des pilotes", "Driver inventory");
    // Libellé cité par le rapport et par le moteur de règles — voir docs/GLOSSAIRE_EN.md.
    public static string MainDeepAnalysis => Lang.T("Analyse profonde (WinDbg)", "Deep analysis (WinDbg)");
    public static string MainDeepAnalysisTip => Lang.T(
        "Exécute !analyze -v sur les 5 derniers dumps via CDB pour nommer le pilote fautif. Nécessite WinDbg (winget install Microsoft.WinDbg) ; ignoré proprement s'il est absent.",
        "Runs !analyze -v on the last 5 dumps through CDB to name the faulting driver. Requires WinDbg (winget install Microsoft.WinDbg); skipped cleanly if absent.");

    public static string MainReady => Lang.T(
        "Prêt. Lance l'analyse pour examiner dumps, journaux d'événements, fiabilité et état matériel.",
        "Ready. Start the analysis to examine dumps, event logs, reliability and hardware state.");
    public static string MainFooterTip => Lang.T(
        "Diagnostic des pannes, boîte noire temps réel, alertes préventives, console de parc et aide à la réparation.",
        "Fault diagnosis, real-time flight recorder, preventive alerts, fleet console and repair assistance.");

    public static string MainCheckUpdates => Lang.T("🔄 Vérifier les mises à jour", "🔄 Check for updates");
    public static string MainCheckUpdatesTip => Lang.T(
        "Interroge la page des versions publiées sur GitHub. FaultTracePC ne télécharge et n'installe jamais rien tout seul : il te dit ce qui existe, tu décides.",
        "Queries the published releases page on GitHub. FaultTracePC never downloads or installs anything by itself: it tells you what exists, you decide.");
    public static string MainAtStartup => Lang.T("au démarrage", "at startup");
    public static string MainAtStartupTip => Lang.T(
        "Décoché par défaut : sans cette case, FaultTracePC ne contacte jamais Internet de lui-même. À laisser décoché sur un poste d'établissement si les sorties réseau sont contrôlées.",
        "Unticked by default: without this box, FaultTracePC never contacts the Internet on its own. Leave it unticked on a managed machine if outbound traffic is controlled.");

    public static string MainRunRepair => Lang.T("🛠  Lancer la réparation", "🛠  Run the repair");
    public static string MainRunRepairTip => Lang.T(
        "Exécute le script PowerShell de réparation généré par le dernier scan (tests en lecture seule automatiques, actions avec confirmation O/N).",
        "Runs the PowerShell repair script produced by the last scan (read-only checks automatic, actions with a Y/N confirmation).");
    public static string MainPdf => Lang.T("📑  PDF", "📑  PDF");
    public static string MainPdfTip => Lang.T(
        "Crée un PDF du dernier rapport, à la demande uniquement — aucun PDF n'est généré automatiquement. Le document contient le rapport COMPLET, détails techniques inclus, pour être joint à un ticket ou transmis à un réparateur.",
        "Creates a PDF of the last report, on demand only — no PDF is ever generated automatically. The document holds the COMPLETE report, technical detail included, to attach to a ticket or hand to a repairer.");
    public static string MainOpenReport => Lang.T("📄  Ouvrir le dernier rapport", "📄  Open the last report");

    // ---------------------------------------------------------------- boîte à outils
    //
    // ATTENTION : onze de ces libellés sont CITÉS mot pour mot par le rapport HTML
    // (« bouton 🧰 Tools, puis : “💽 Check the system drive (read-only)” »). Ils sont
    // figés dans docs/GLOSSAIRE_EN.md. Les changer ici sans changer le rapport ferait
    // chercher un bouton qui n'existe pas.
    public static string ToolTitle => Lang.T("FaultTracePC — Boîte à outils de réparation",
                                             "FaultTracePC — Repair toolbox");
    public static string ToolHeader => Lang.T(
        "🧰 Boîte à outils — chaque action s'exécute dans une fenêtre PowerShell visible",
        "🧰 Toolbox — every action runs in a visible PowerShell window");

    public static string ToolGrpBefore => Lang.T(" Avant toute réparation ", " Before any repair ");
    public static string ToolBeforeText => Lang.T(
        "Un point de restauration permet de revenir en arrière si une réparation tourne mal. C'est trente secondes, et c'est le seul filet de sécurité qui existe.",
        "A restore point makes it possible to go back if a repair turns out badly. It takes thirty seconds, and it is the only safety net there is.");
    public static string ToolRestorePoint => Lang.T("💾 Créer un point de restauration", "💾 Create a restore point");
    public static string ToolRestorePointTip => Lang.T(
        "Crée un point de restauration système nommé « FaultTracePC — avant réparation ».",
        "Creates a system restore point named “FaultTracePC — before repair”.");
    public static string ToolOpenRestore => Lang.T("↩ Ouvrir la restauration du système", "↩ Open System Restore");
    public static string ToolOpenRestoreTip => Lang.T(
        "Ouvre l'assistant Windows pour revenir à un point de restauration existant.",
        "Opens the Windows wizard to roll back to an existing restore point.");

    public static string ToolGrpSpace => Lang.T(" Libérer de l'espace disque ", " Free up disk space ");
    public static string ToolSpaceText => Lang.T(
        "Ces nettoyages récupèrent des gigaoctets réels — contrairement au « nettoyage de registre », qui ne libère rien et casse parfois des choses.",
        "These cleanups recover real gigabytes — unlike “registry cleaning”, which frees nothing and sometimes breaks things.");
    public static string ToolWhereSpace => Lang.T("📊 Où est passée la place ?", "📊 Where did the space go?");
    public static string ToolWhereSpaceTip => Lang.T(
        "Mesure l'espace occupé par les composants Windows, les temporaires, la corbeille et Windows.old.",
        "Measures the space taken by the Windows components, temporary files, the recycle bin and Windows.old.");
    public static string ToolCleanComponents => Lang.T("🧹 Purger les composants Windows obsolètes",
                                                       "🧹 Purge obsolete Windows components");
    public static string ToolCleanComponentsTip => Lang.T(
        "DISM /StartComponentCleanup : supprime les anciennes versions des composants remplacés par les mises à jour. Récupère souvent plusieurs Go.",
        "DISM /StartComponentCleanup: removes the old versions of components replaced by updates. Often recovers several GB.");
    public static string ToolCleanTemp => Lang.T("🗑 Vider les fichiers temporaires", "🗑 Empty the temporary files");
    public static string ToolCleanTempTip => Lang.T(
        "Vide %TEMP% et le dossier Temp de Windows (les fichiers en cours d'utilisation sont ignorés).",
        "Empties %TEMP% and the Windows Temp folder (files in use are skipped).");
    public static string ToolDiskCleanup => Lang.T("🧰 Nettoyage de disque Windows", "🧰 Windows Disk Cleanup");
    public static string ToolDiskCleanupTip => Lang.T(
        "Ouvre l'outil intégré, avec les options système (Windows.old, anciennes mises à jour).",
        "Opens the built-in tool, with the system options (Windows.old, old updates).");

    public static string ToolGrpStartup => Lang.T(" Démarrage, sécurité et réseau ", " Startup, security and network ");
    public static string ToolStartupApps => Lang.T("🚀 Programmes lancés au démarrage", "🚀 Programs launched at startup");
    public static string ToolStartupAppsTip => Lang.T(
        "Liste ce qui se lance au démarrage (registre, dossier Démarrage) — la vraie cause d'un PC lent à démarrer. Lecture seule.",
        "Lists what starts with Windows (registry, Startup folder) — the real cause of a slow-booting PC. Read-only.");
    public static string ToolQuickScan => Lang.T("🛡 Analyse rapide Microsoft Defender", "🛡 Microsoft Defender quick scan");
    public static string ToolQuickScanTip => Lang.T(
        "Start-MpScan -ScanType QuickScan : quelques minutes, sans redémarrage.",
        "Start-MpScan -ScanType QuickScan: a few minutes, no restart.");
    public static string ToolFullScan => Lang.T("🛡 Analyse complète (longue)", "🛡 Full scan (long)");
    public static string ToolFullScanTip => Lang.T("Analyse complète du système : peut durer plus d'une heure.",
                                                   "Full system scan: can take more than an hour.");
    public static string ToolThreats => Lang.T("📜 Menaces détectées récemment", "📜 Threats detected recently");
    public static string ToolThreatsTip => Lang.T(
        "Historique des détections de Microsoft Defender et état de la protection.",
        "History of Microsoft Defender detections and protection status.");
    public static string ToolNetReset => Lang.T("🌐 Réinitialiser la pile réseau", "🌐 Reset the network stack");
    public static string ToolNetResetTip => Lang.T(
        "netsh winsock reset + réinitialisation IP + vidage du cache DNS. Nécessite un redémarrage.",
        "netsh winsock reset + IP reset + DNS cache flush. Requires a restart.");
    public static string ToolBatteryReport => Lang.T("🔋 Rapport de batterie détaillé", "🔋 Detailed battery report");
    public static string ToolBatteryReportTip => Lang.T(
        "powercfg /batteryreport : historique complet de capacité et d'autonomie.",
        "powercfg /batteryreport: full history of capacity and runtime.");
    public static string ToolResMon => Lang.T("📈 Moniteur de ressources", "📈 Resource Monitor");
    public static string ToolResMonTip => Lang.T(
        "Ouvre l'outil Windows : processeur, disque, réseau et mémoire par processus, en direct.",
        "Opens the Windows tool: CPU, disk, network and memory per process, live.");

    public static string ToolGrpUpdate => Lang.T(" Mise à jour Windows qui s'est mal passée ",
                                                 " A Windows update that went wrong ");
    public static string ToolUpdateText => Lang.T(
        "Démarche recommandée : 1) désinstaller la mise à jour fautive ci-dessous · 2) si Windows Update lui-même est cassé, réinitialiser ses composants · 3) en dernier recours, réparation sur place.",
        "Recommended order: 1) uninstall the offending update below · 2) if Windows Update itself is broken, reset its components · 3) as a last resort, an in-place repair.");
    public static string ToolColType => Lang.T("Type", "Type");
    public static string ToolColInstalled => Lang.T("Installée le", "Installed on");
    public static string ToolRefresh => Lang.T("🔄 Actualiser la liste", "🔄 Refresh the list");
    public static string ToolUninstallUpdate => Lang.T("🗑 Désinstaller la mise à jour sélectionnée",
                                                       "🗑 Uninstall the selected update");
    public static string ToolUninstallUpdateTip => Lang.T(
        "wusa /uninstall /kb:… — Windows demandera un redémarrage. La mise à jour se réinstallera ensuite via Windows Update, sauf si elle est mise en pause.",
        "wusa /uninstall /kb:… — Windows will ask for a restart. The update will reinstall later through Windows Update, unless it is paused.");
    public static string ToolResetWu => Lang.T("♻ Réinitialiser les composants Windows Update",
                                               "♻ Reset the Windows Update components");
    public static string ToolResetWuTip => Lang.T(
        "Arrête les services, purge SoftwareDistribution et catroot2 (caches de téléchargement), redémarre les services.",
        "Stops the services, purges SoftwareDistribution and catroot2 (download caches), restarts the services.");
    public static string ToolInPlace => Lang.T("🩹 Réparation sur place (réinstaller Windows sans perte)",
                                               "🩹 In-place repair (reinstall Windows without losing anything)");
    public static string ToolInPlaceTip => Lang.T(
        "Ouvre Paramètres → Récupération : « Résoudre les problèmes à l'aide de Windows Update » réinstalle la même version en conservant fichiers et applications.",
        "Opens Settings → Recovery: “Fix problems using Windows Update” reinstalls the same version while keeping files and applications.");
    public static string ToolFindUpdates => Lang.T("⬇ Rechercher et installer les mises à jour (optionnelles et pilotes inclus)",
                                                   "⬇ Find and install updates (optional and driver updates included)");
    public static string ToolFindUpdatesTip => Lang.T(
        "Ouvre la fenêtre de mise à jour : liste tout ce qui est disponible, y compris les mises à jour optionnelles et les pilotes que la page Paramètres masque.",
        "Opens the update window: lists everything available, including the optional updates and drivers the Settings page hides.");

    public static string ToolGrpRepair => Lang.T(" Réparations système ", " System repairs ");
    public static string ToolSfc => Lang.T("🧪 sfc /scannow (fichiers système)", "🧪 sfc /scannow (system files)");
    public static string ToolSfcTip => Lang.T("Vérifie et répare les fichiers système Windows (~10 min).",
                                              "Checks and repairs the Windows system files (~10 min).");
    public static string ToolDismScan => Lang.T("🔍 DISM — vérifier l'image Windows", "🔍 DISM — check the Windows image");
    public static string ToolDismScanTip => Lang.T("DISM /Online /Cleanup-Image /ScanHealth (~5 min, lecture seule).",
                                                   "DISM /Online /Cleanup-Image /ScanHealth (~5 min, read-only).");
    public static string ToolDismRestore => Lang.T("🔧 DISM — réparer l'image Windows", "🔧 DISM — repair the Windows image");
    public static string ToolDismRestoreTip => Lang.T(
        "DISM /Online /Cleanup-Image /RestoreHealth (~15 min, télécharge les fichiers sains — à faire AVANT sfc si sfc échoue).",
        "DISM /Online /Cleanup-Image /RestoreHealth (~15 min, downloads the healthy files — do this BEFORE sfc if sfc fails).");
    public static string ToolCheckDisk => Lang.T("💽 Vérifier le disque système (lecture seule)",
                                                 "💽 Check the system drive (read-only)");
    public static string ToolCheckDiskTip => Lang.T("Repair-Volume -Scan sur C: — sans redémarrage.",
                                                    "Repair-Volume -Scan on C: — no restart.");
    public static string ToolChkdsk => Lang.T("💽 Planifier chkdsk C: /f (au redémarrage)",
                                              "💽 Schedule chkdsk C: /f (at restart)");
    public static string ToolChkdskTip => Lang.T("Répare le système de fichiers au prochain redémarrage.",
                                                 "Repairs the file system at the next restart.");
    public static string ToolMemDiag => Lang.T("🧠 Diagnostic mémoire Windows (redémarre !)",
                                               "🧠 Windows Memory Diagnostic (reboots!)");
    public static string ToolMemDiagTip => Lang.T("mdsched — le PC redémarre immédiatement pour tester la RAM.",
                                                  "mdsched — the PC restarts immediately to test the RAM.");
    public static string ToolEnergy => Lang.T("⚡ Rapport d'énergie (60 s)", "⚡ Energy report (60 s)");
    public static string ToolEnergyTip => Lang.T("powercfg /energy — analyse alimentation/veille pendant 60 secondes.",
                                                 "powercfg /energy — analyses power and sleep for 60 seconds.");
    public static string ToolSmart => Lang.T("🌡 Santé des disques (SMART)", "🌡 Drive health (SMART)");
    public static string ToolSmartTip => Lang.T(
        "Get-PhysicalDisk + compteurs de fiabilité (température, usure, erreurs).",
        "Get-PhysicalDisk plus the reliability counters (temperature, wear, errors).");
    public static string ToolWinDbg => Lang.T("🐞 Installer WinDbg (analyse des dumps)",
                                              "🐞 Install WinDbg (dump analysis)");
    public static string ToolWinDbgTip => Lang.T(
        "Installe les outils de débogage Microsoft via winget. Sans eux, le code d'arrêt est lu mais le pilote fautif reste souvent anonyme.",
        "Installs the Microsoft debugging tools through winget. Without them the stop code is read but the faulting driver often stays anonymous.");
    public static string ToolLinkPower => Lang.T("🔌 Alimentation des liens (réinitialisations de contrôleur)",
                                                 "🔌 Link power management (controller resets)");
    public static string ToolLinkPowerTip => Lang.T(
        "Affiche le réglage actuel de la gestion d'alimentation des liens PCI Express et des disques, puis ouvre le panneau pour le modifier.",
        "Shows the current PCI Express link and disk power management setting, then opens the panel to change it.");

    public static string ToolGrpConsoles => Lang.T(" Consoles Windows utiles ", " Useful Windows consoles ");
    public static string ToolReliability => Lang.T("📊 Moniteur de fiabilité", "📊 Reliability Monitor");
    public static string ToolEventViewer => Lang.T("📋 Observateur d'événements", "📋 Event Viewer");
    public static string ToolWuSettings => Lang.T("🔄 Paramètres Windows Update", "🔄 Windows Update settings");
    public static string ToolDiskMgmt => Lang.T("💾 Gestion des disques", "💾 Disk Management");
    public static string ToolSysInfo => Lang.T("🖥 Informations système", "🖥 System Information");
    public static string ToolFooter => Lang.T(
        "Les actions qui modifient le système demandent confirmation. FaultTracePC étant administrateur, les fenêtres PowerShell le sont aussi.",
        "Actions that change the system ask for confirmation. Since FaultTracePC runs as administrator, the PowerShell windows do too.");

    // ---------------------------------------------------------------- surveillance en direct
    public static string MonTitle => Lang.T("FaultTracePC — Surveillance en direct", "FaultTracePC — Live monitoring");
    public static string MonHeader => Lang.T("📈 Journal de la boîte noire", "📈 Flight recorder log");
    public static string MonFahrenheit => Lang.T("Afficher en Fahrenheit (°F)", "Show in Fahrenheit (°F)");
    public static string MonNoCpuTemp => Lang.T(
        "ℹ La température CPU n'est pas exposée par les capteurs de cette machine.",
        "ℹ CPU temperature is not exposed by the sensors on this machine.");
    public static string MonTabLive => Lang.T("  En direct  ", "  Live  ");
    public static string MonLoading => Lang.T("Chargement du journal…", "Loading the log…");
    public static string MonColTime => Lang.T("Heure", "Time");
    public static string MonColCpu => Lang.T("CPU %", "CPU %");
    public static string MonColCpuTemp => Lang.T("Temp. CPU", "CPU temp.");
    public static string MonColGpuTemp => Lang.T("Temp. GPU", "GPU temp.");
    public static string MonColRam => Lang.T("RAM %", "RAM %");
    public static string MonColInfo => Lang.T("Infos (événements, processus…)", "Info (events, processes…)");
    public static string MonTabCharts => Lang.T("  Courbes  ", "  Charts  ");
    public static string MonPeriod => Lang.T("Période :", "Period:");
    public static string Mon1h => Lang.T("1 heure", "1 hour");
    public static string Mon6h => Lang.T("6 heures", "6 hours");
    public static string Mon24h => Lang.T("24 heures", "24 hours");
    public static string Mon7d => Lang.T("7 jours", "7 days");
    public static string MonCpuTempLabel => Lang.T("Température CPU", "CPU temperature");
    public static string MonGpuTempLabel => Lang.T("Température GPU", "GPU temperature");
    public static string MonMemLabel => Lang.T("Mémoire utilisée (%)", "Memory used (%)");
    public static string MonChartHint => Lang.T(
        "Survole la courbe pour lire les valeurs. Les incidents (alertes, événements) apparaissent en rouge.",
        "Hover the curve to read the values. Incidents (alerts, events) appear in red.");
    public static string MonTabHistory => Lang.T("  Historique  ", "  History  ");
    public static string MonHist24h => Lang.T("24 heures", "24 hours");
    public static string MonHist3d => Lang.T("3 jours", "3 days");
    public static string MonHist7d => Lang.T("7 jours", "7 days");
    public static string MonHist14d => Lang.T("14 jours", "14 days");
    public static string MonLoad => Lang.T("Charger", "Load");
    public static string MonColCpuAvg => Lang.T("CPU moy %", "CPU avg %");
    public static string MonColCpuMax => Lang.T("CPU max %", "CPU max %");
    public static string MonColCpuTempMax => Lang.T("T° CPU max", "CPU temp. max");
    public static string MonColGpuTempMax => Lang.T("T° GPU max", "GPU temp. max");
    public static string MonColRamMax => Lang.T("RAM max %", "RAM max %");
    public static string MonColCommitMax => Lang.T("Mém. virt. max %", "Virtual mem. max %");
    public static string MonColEvents => Lang.T("Événements", "Events");
    public static string MonTabRaw => Lang.T("  Données brutes (avancé)  ", "  Raw data (advanced)  ");
    public static string MonRefresh => Lang.T("Actualiser", "Refresh");

    // ---------------------------------------------------------------- mode réseau
    public static string NetTitle => Lang.T("FaultTracePC — Mode réseau", "FaultTracePC — Network mode");
    public static string NetHeader => Lang.T("🌐 Mode réseau de cette machine", "🌐 Network mode of this machine");
    public static string NetLocal => Lang.T(
        "Local — rien n'est exposé sur le réseau (défaut, recommandé pour un poste isolé)",
        "Local — nothing is exposed on the network (default, recommended for a standalone machine)");
    public static string NetClient => Lang.T(
        "Client — cette machine publie sa télémétrie et ses rapports en LECTURE SEULE",
        "Client — this machine publishes its telemetry and reports READ-ONLY");
    public static string NetPort => Lang.T("Port TCP :", "TCP port:");
    public static string NetToken => Lang.T("Token :", "Token:");
    public static string NetGenerate => Lang.T("Générer", "Generate");
    public static string NetCopy => Lang.T("Copier", "Copy");
    public static string NetApply => Lang.T("Appliquer", "Apply");
    public static string NetClose => Lang.T("Fermer", "Close");

    // ---------------------------------------------------------------- console de parc
    public static string ParkTitle => Lang.T("FaultTracePC — Console Parc", "FaultTracePC — Fleet console");
    public static string ParkHeader => Lang.T("🖥 Console Parc — état des machines clientes",
                                              "🖥 Fleet console — state of the client machines");
    public static string ParkName => Lang.T("Nom :", "Name:");
    public static string ParkHost => Lang.T("Hôte/IP :", "Host/IP:");
    public static string ParkPort => Lang.T("Port :", "Port:");
    public static string ParkToken => Lang.T("Token :", "Token:");
    public static string ParkAdd => Lang.T("➕ Ajouter", "➕ Add");
    public static string ParkRemove => Lang.T("🗑 Retirer la sélection", "🗑 Remove the selection");
    public static string ParkRefreshAll => Lang.T("🔄 Actualiser tout", "🔄 Refresh all");
    public static string ParkColMachine => Lang.T("Machine", "Machine");
    public static string ParkColHost => Lang.T("Hôte", "Host");
    public static string ParkColState => Lang.T("État", "State");
    public static string ParkColVersion => Lang.T("Version", "Version");
    public static string ParkColLast => Lang.T("Dernier relevé", "Last reading");
    public static string ParkColCpu => Lang.T("CPU %", "CPU %");
    public static string ParkColCpuTemp => Lang.T("T° CPU", "CPU temp.");
    public static string ParkColGpuTemp => Lang.T("T° GPU", "GPU temp.");
    public static string ParkColRam => Lang.T("RAM %", "RAM %");
    public static string ParkColTop => Lang.T("Top processus", "Top processes");
    public static string ParkHint => Lang.T(
        "Ajoute tes machines clientes (mode Client activé sur chacune via 🌐), puis Actualiser.",
        "Add your client machines (Client mode enabled on each through 🌐), then Refresh.");
    public static string ParkReport => Lang.T("📊 Rapport du parc", "📊 Fleet report");
    public static string ParkReportTip => Lang.T(
        "Génère une page HTML récapitulant l'état de toutes les machines (imprimable, transmissible).",
        "Generates an HTML page summarising the state of every machine (printable, shareable).");
    public static string ParkRemoteScan => Lang.T("🩺 Lancer un diagnostic à distance", "🩺 Run a remote diagnosis");
    public static string ParkRemoteScanTip => Lang.T(
        "Déclenche un scan complet sur la machine sélectionnée puis ouvre son rapport HTML. Peut prendre plusieurs minutes.",
        "Triggers a full scan on the selected machine then opens its HTML report. Can take several minutes.");
    public static string ParkOpenReport => Lang.T("📄 Ouvrir le dernier rapport", "📄 Open the last report");

    // ---------------------------------------------------------------- mises à jour Windows
    public static string WuTitle => Lang.T("FaultTracePC — Mises à jour Windows (optionnelles et pilotes inclus)",
                                           "FaultTracePC — Windows updates (optional and drivers included)");
    public static string WuHeader => Lang.T("⬇ Mises à jour Windows", "⬇ Windows updates");
    public static string WuIntro => Lang.T(
        "Cette fenêtre interroge directement le service Windows Update et affiche TOUT ce qui est disponible, y compris ce que la page Paramètres masque.",
        "This window queries the Windows Update service directly and shows EVERYTHING available, including what the Settings page hides.");
    public static string WuSearch => Lang.T("🔍 Rechercher les mises à jour", "🔍 Search for updates");
    public static string WuIncludeDrivers => Lang.T("Inclure les pilotes et autres produits Microsoft",
                                                    "Include drivers and other Microsoft products");
    public static string WuIncludeDriversTip => Lang.T(
        "Utilise le catalogue « Microsoft Update » (pilotes, Office, Visual C++…). Si ce catalogue n'est pas enregistré sur le poste, la recherche se limite à Windows.",
        "Uses the “Microsoft Update” catalogue (drivers, Office, Visual C++…). If that catalogue is not registered on the machine, the search is limited to Windows.");
    public static string WuIncludeHidden => Lang.T("Inclure les mises à jour masquées", "Include hidden updates");
    public static string WuIncludeHiddenTip => Lang.T(
        "Affiche aussi les mises à jour que quelqu'un a explicitement masquées sur ce poste.",
        "Also shows the updates someone has explicitly hidden on this machine.");
    public static string WuCheckImportant => Lang.T("Cocher les importantes", "Tick the important ones");
    public static string WuCheckAll => Lang.T("Tout cocher", "Tick everything");
    public static string WuUncheckAll => Lang.T("Tout décocher", "Untick everything");
    public static string WuColUpdate => Lang.T("Mise à jour", "Update");
    public static string WuColType => Lang.T("Type", "Type");
    public static string WuColCategory => Lang.T("Catégorie", "Category");
    public static string WuColSize => Lang.T("Taille", "Size");
    public static string WuColReboot => Lang.T("Redémarrage", "Restart");
    public static string WuColKb => Lang.T("KB", "KB");
    public static string WuTabDetail => Lang.T(
        "  Détail technique — aussi enregistré dans Documents\\FaultTracePC\\MajWindows_AAAA-MM-JJ.txt  ",
        "  Technical detail — also saved to Documents\\FaultTracePC\\MajWindows_YYYY-MM-DD.txt  ");
    public static string WuHint => Lang.T(
        "Clique sur « Rechercher les mises à jour » pour interroger Windows Update.",
        "Click “Search for updates” to query Windows Update.");
    public static string WuInstall => Lang.T("⬇ Télécharger et installer la sélection",
                                             "⬇ Download and install the selection");

    // ---------------------------------------------------------------- assistant « Je ne sais pas ce que j'ai »
    public static string GrTitle => Lang.T("FaultTracePC — Je ne sais pas ce que j'ai",
                                           "FaultTracePC — I don't know what's wrong");
    public static string GrHeader => Lang.T("Je ne sais pas ce que j'ai", "I don't know what's wrong");
    public static string GrIntro => Lang.T(
        "Cet assistant examine l'ordinateur, applique les réparations sans risque, puis vérifie si le problème a disparu. Il commence par créer un point de restauration : tout reste annulable. Rien qui puisse casser quelque chose n'est fait sans ton accord.",
        "This assistant examines the computer, applies the risk-free repairs, then checks whether the problem is gone. It starts by creating a restore point: everything stays reversible. Nothing that could break anything is done without your agreement.");
    public static string GrReady => Lang.T(
        "Prêt. Clique sur « Démarrer » : l'assistant s'occupe du reste.",
        "Ready. Click “Start”: the assistant takes care of the rest.");
    public static string GrConclusionLabel => Lang.T("CONCLUSION", "CONCLUSION");
    public static string GrProposalsIntro => Lang.T(
        "Ce qui reste à faire demande ton accord, parce que ces actions ne sont pas anodines. Chacune s'explique ; tu décides une par une.",
        "What is left to do needs your agreement, because these actions are not trivial. Each one is explained; you decide one at a time.");
    public static string GrLogHeader => Lang.T("  Détail technique (ce que l'assistant exécute)",
                                               "  Technical detail (what the assistant runs)");
    public static string GrFooterHint => Lang.T(
        "Compte 20 à 40 minutes. Tu peux laisser tourner et revenir plus tard.",
        "Allow 20 to 40 minutes. You can leave it running and come back later.");
    public static string GrOpenReport => Lang.T("Ouvrir le rapport complet", "Open the full report");
    public static string GrStart => Lang.T("Démarrer", "Start");

    // ---------------------------------------------------------------- sélecteur de langue
    public static string LangTip => Lang.T("Choisir la langue de FaultTracePC", "Choose the FaultTracePC language");
    public static string LangFrench => Lang.T("Français", "French");
    public static string LangEnglish => Lang.T("Anglais", "English");
    public static string LangAuto => Lang.T("Automatique (suivre Windows)", "Automatic (follow Windows)");

    // ------------------------------------------------- encart de sécurité du mode réseau
    // Découpé en fragments parce que le texte porte des mots en gras au milieu :
    // un seul libellé perdrait l'emphase, et la mettre dans le XAML la figerait
    // en français.
    public static string NetSecTitle => Lang.T("Sécurité du mode Client :", "Client mode security:");
    public static string NetSecA => Lang.T(
        " l'API est en lecture seule et refuse toute requête qui ne vient pas d'une adresse privée (127.0.0.1, 10.x, 172.16-31.x, 192.168.x) ",
        " the API is read-only and refuses any request that does not come from a private address (127.0.0.1, 10.x, 172.16-31.x, 192.168.x) ");
    public static string NetSecOr => Lang.T("ou", "or");
    public static string NetSecB => Lang.T(
        " dont la signature est invalide — les deux verrous sont exigés. Le token ne circule ",
        " or whose signature is invalid — both locks are required. The token ");
    public static string NetSecNever => Lang.T("jamais", "never");
    public static string NetSecC => Lang.T(
        " sur le réseau : il sert de clé pour signer chaque requête (HMAC-SHA256), avec horodatage et nonce contre le rejeu. Une règle de pare-feu limitée à ces mêmes plages est ajoutée en plus. Rien n'est accessible depuis Internet.",
        " travels over the network: it is the key used to sign each request (HMAC-SHA256), with a timestamp and a nonce against replay. A firewall rule limited to those same ranges is added on top. Nothing is reachable from the Internet.");
    public static string NetClockTitle => Lang.T("Note :", "Note:");
    public static string NetClockBody => Lang.T(
        " les horloges des deux machines doivent être à moins de 5 minutes d'écart.",
        " the clocks of both machines must be within 5 minutes of each other.");
    public static string NetMasterTitle => Lang.T("Côté machine « maître » :", "On the “master” machine:");
    public static string NetMasterBody => Lang.T(
        " ouvre la console 🖥 Parc, ajoute cette machine avec son nom d'hôte (ou IP), le port et ce token.",
        " open the 🖥 Fleet console, add this machine with its host name (or IP), the port and this token.");
}
