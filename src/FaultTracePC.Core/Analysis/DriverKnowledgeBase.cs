namespace FaultTracePC.Core.Analysis;

/// <summary>
/// Une fiche de pilote, dans les deux langues.
///
/// MÊME PIÈGE QUE LE CATALOGUE DES CODES STOP : la table est un « static readonly »
/// construit une seule fois. Un Lang.T() écrit à l'intérieur aurait figé la langue
/// au premier accès. Le choix se fait donc à la lecture, dans les propriétés.
///
/// LES CHAMPS ANGLAIS SONT FACULTATIFS, ET C'EST DÉLIBÉRÉ : cette base compte
/// près de soixante-dix fiches. Les traduire d'un bloc obligerait à tout livrer
/// en une fois, sans compilation intermédiaire. Une fiche pas encore traduite
/// rend donc son texte FRANÇAIS, y compris en anglais — un texte utile dans la
/// mauvaise langue plutôt qu'une case vide, et un défaut visible à l'œil plutôt
/// qu'un silence.
/// </summary>
public sealed record DriverKbEntry(
    string OwnerFr, string ContextFr, string FixFr,
    string OwnerEn = "", string ContextEn = "", string FixEn = "")
{
    public string Owner => Pick(OwnerFr, OwnerEn);
    public string Context => Pick(ContextFr, ContextEn);
    public string Fix => Pick(FixFr, FixEn);

    private static string Pick(string fr, string en) => Lang.IsFrench || en.Length == 0 ? fr : en;

    /// <summary>Fiche entièrement traduite ? Sert au décompte de la couverture.</summary>
    public bool Translated => OwnerEn.Length > 0 && ContextEn.Length > 0 && FixEn.Length > 0;
}

/// <summary>
/// Base de connaissances des pilotes fréquemment impliqués dans les BSOD :
/// à qui appartient le pilote, dans quel contexte il plante, et le correctif
/// PRÉCIS qui a fait ses preuves — au lieu d'un générique « mettez à jour ».
/// Clé : nom de fichier .sys en minuscules.
/// </summary>
public static class DriverKnowledgeBase
{
    public static DriverKbEntry? Lookup(string? sysFileName)
    {
        if (string.IsNullOrWhiteSpace(sysFileName)) return null;
        var key = sysFileName.Trim().ToLowerInvariant();
        if (!key.EndsWith(".sys")) key += ".sys";
        return Entries.TryGetValue(key, out var e) ? e : null;
    }

    public static bool IsKnown(string? sysFileName) => Lookup(sysFileName) is not null;

    /// <summary>
    /// Reconnaissance par FAMILLE, appliquée quand le fichier n'est pas listé
    /// nominativement.
    ///
    /// Sur une machine réelle, l'essentiel des pilotes tiers ne sont pas les
    /// suspects classiques de BSOD mais les composants de plateforme du fabricant :
    /// gestion d'énergie, bus I2C, boutons, audio intégré… Les lister un par un
    /// serait sans fin et sans intérêt. En revanche, leur préfixe et leur éditeur
    /// les rattachent sans ambiguïté à un ensemble qui se met à jour d'un bloc,
    /// par le logiciel chipset du fabricant — et ça, c'est une action utile.
    ///
    /// Le nom du fichier NE SUFFIT PAS : on exige aussi la concordance de
    /// l'éditeur inscrit dans le fichier. Sans cette double condition, un pilote
    /// tiers commençant par « amd » serait attribué à AMD à tort.
    /// </summary>
    public static DriverKbEntry? LookupFamily(string? sysFileName, string? companyName)
    {
        if (string.IsNullOrWhiteSpace(sysFileName)) return null;
        var file = sysFileName.Trim().ToLowerInvariant();
        var vendor = (companyName ?? "").Trim();
        if (vendor.Length == 0) return null;

        bool Vendor(params string[] needles) =>
            needles.Any(n => vendor.Contains(n, StringComparison.OrdinalIgnoreCase));
        bool File(params string[] prefixes) =>
            prefixes.Any(pfx => file.StartsWith(pfx, StringComparison.Ordinal));

        if (Vendor("Advanced Micro Devices", "ATI Technologies") && File("amd", "ati"))
            return new DriverKbEntry("AMD (pilote de plateforme)",
                "Composant de la plateforme AMD : gestion d'énergie, bus interne, audio intégré ou périphérique de la carte mère. Ces pilotes se mettent à jour ensemble, pas un par un.",
                "Installer le paquet « AMD Chipset Software » depuis le site d'AMD, ou la mise à jour de plateforme proposée par le fabricant de l'ordinateur. "
                + "Sur un portable, privilégier la version du fabricant : elle est validée pour ce modèle précis.",
                "AMD (platform driver)",
                "An AMD platform component: power management, internal bus, integrated audio or a motherboard device. These drivers are updated together, not one by one.",
                "Install the “AMD Chipset Software” package from AMD's website, or the platform update offered by the computer manufacturer. On a laptop, prefer the manufacturer's version: it is validated for that exact model.");

        if (Vendor("NVIDIA") && File("nv"))
            return new DriverKbEntry("NVIDIA (pilote de plateforme)",
                "Composant annexe du pilote NVIDIA (audio HDMI, gestion d'énergie, périphérique virtuel).",
                "Ces composants sont installés et mis à jour par le pilote graphique NVIDIA : le réinstaller proprement (DDU en mode sans échec, puis dernier pilote) corrige l'ensemble.",
                "NVIDIA (platform driver)",
                "An ancillary component of the NVIDIA driver (HDMI audio, power management, virtual device).",
                "These components are installed and updated by the NVIDIA display driver: reinstalling it cleanly (DDU in safe mode, then the latest driver) fixes the whole set.");

        if (Vendor("Intel") && File("intel", "ial", "e1", "netwtw", "netwbw", "ibt", "iagpio", "iai2c", "iauart", "iaspi"))
            return new DriverKbEntry("Intel (pilote de plateforme)",
                "Composant de la plateforme Intel : carte réseau, Wi-Fi, Bluetooth, bus interne ou gestion d'énergie.",
                "Utiliser « Intel Driver & Support Assistant », ou la page de support du fabricant de l'ordinateur. "
                + "Sur un portable, la version du fabricant prime : la version générique Intel est parfois incompatible avec la configuration d'usine.",
                "Intel (platform driver)",
                "An Intel platform component: network adapter, Wi-Fi, Bluetooth, internal bus or power management.",
                "Use “Intel Driver & Support Assistant”, or the computer manufacturer's support page. On a laptop the manufacturer's version wins: the generic Intel one is sometimes incompatible with the factory configuration.");

        if (Vendor("Realtek") && File("rt", "rtk"))
            return new DriverKbEntry("Realtek (réseau ou audio)",
                "Composant Realtek : carte réseau filaire, Wi-Fi ou audio intégré.",
                "Télécharger le pilote sur le site du fabricant de la carte mère ou de l'ordinateur plutôt que via Windows Update, dont la version est souvent plus ancienne.",
                "Realtek (network or audio)",
                "A Realtek component: wired network adapter, Wi-Fi or integrated audio.",
                "Download the driver from the motherboard or computer manufacturer's website rather than through Windows Update, whose version is often older.");

        if (Vendor("Qualcomm", "Atheros") && File("qc", "ath", "qca"))
            return new DriverKbEntry("Qualcomm / Atheros (réseau sans fil)",
                "Composant Wi-Fi ou Bluetooth Qualcomm-Atheros.",
                "Mettre à jour depuis la page de support du fabricant de l'ordinateur. Si les coupures Wi-Fi accompagnent les plantages, tester en désactivant l'économie d'énergie de la carte dans le Gestionnaire de périphériques.",
                "Qualcomm / Atheros (wireless)",
                "A Qualcomm-Atheros Wi-Fi or Bluetooth component.",
                "Update from the computer manufacturer's support page. If Wi-Fi drops accompany the crashes, test with the adapter's power saving turned off in Device Manager.");

        if (Vendor("MediaTek") && File("mtk", "mt"))
            return new DriverKbEntry("MediaTek (réseau sans fil)",
                "Composant Wi-Fi ou Bluetooth MediaTek.",
                "Mettre à jour depuis la page de support du fabricant de l'ordinateur.",
                "MediaTek (wireless)",
                "A MediaTek Wi-Fi or Bluetooth component.",
                "Update from the computer manufacturer's support page.");

        if (Vendor("Oracle") && File("vbox"))
            return new DriverKbEntry("VirtualBox (Oracle)",
                "Composant de VirtualBox : carte réseau virtuelle, filtre réseau, support noyau ou passerelle USB. Ces pilotes s'intercalent très bas dans le système.",
                "Mettre VirtualBox à jour dans sa dernière version. Faire cohabiter deux hyperviseurs (Hyper-V, WSL2, VMware, VirtualBox) sur la même machine est une cause classique de plantage : "
                + "si VirtualBox ne sert plus, le désinstaller retire aussi ses filtres réseau, qui survivent souvent aux désinstallations partielles.",
                "VirtualBox (Oracle)",
                "A VirtualBox component: virtual network adapter, network filter, kernel support or USB bridge. These drivers insert themselves very low in the system.",
                "Update VirtualBox to its latest version. Running two hypervisors (Hyper-V, WSL2, VMware, VirtualBox) side by side on the same machine is a classic cause of crashes: if VirtualBox is no longer used, uninstalling it also removes its network filters, which often survive partial removals.");

        if (Vendor("Fortinet"))
            return new DriverKbEntry("Fortinet (VPN ou sécurité d'entreprise)",
                "Composant FortiClient : filtre réseau ou carte virtuelle de VPN d'entreprise. Ces filtres s'insèrent dans la pile réseau et figurent parmi les causes fréquentes de plantage réseau.",
                "Pilote généralement géré par l'administration de l'établissement : ne pas le désinstaller de son propre chef. "
                + "Signaler l'incident au service informatique avec la date et le code d'arrêt ; la correction passe par une mise à jour de FortiClient.",
                "Fortinet (VPN or corporate security)",
                "A FortiClient component: network filter or corporate VPN virtual adapter. These filters sit inside the network stack and are among the frequent causes of network crashes.",
                "This driver is usually managed by the organisation's IT department: do not uninstall it on your own. Report the incident to IT with the date and the stop code; the fix comes through a FortiClient update.");

        if (Vendor("Hewlett", "HP Inc", "Dell ", "Lenovo", "ASUSTeK", "Acer ", "Micro-Star", "Gigabyte"))
            return new DriverKbEntry("Pilote de plateforme du fabricant",
                "Composant installé par le fabricant de l'ordinateur : touches spéciales, capteurs, gestion d'alimentation ou utilitaire maison.",
                "Mettre à jour depuis l'assistant de support du fabricant (HP Support Assistant, Dell SupportAssist, Lenovo Vantage…), qui installe la version validée pour ce modèle précis. "
                + "Ces pilotes sont rarement en cause dans un écran bleu, mais leurs utilitaires associés peuvent l'être.",
                "Manufacturer platform driver",
                "A component installed by the computer manufacturer: special keys, sensors, power management or an in-house utility.",
                "Update from the manufacturer's support assistant (HP Support Assistant, Dell SupportAssist, Lenovo Vantage…), which installs the version validated for that exact model. These drivers are rarely behind a blue screen, but their companion utilities can be.");

        if (Vendor("Synaptics", "ELAN", "Alps"))
            return new DriverKbEntry("Pilote de pavé tactile",
                "Pilote du pavé tactile ou du dispositif de pointage du portable.",
                "Mettre à jour depuis la page de support du fabricant de l'ordinateur. Ces pilotes sont rarement en cause dans un écran bleu.",
                "Touchpad driver",
                "The driver of the laptop's touchpad or pointing device.",
                "Update from the computer manufacturer's support page. These drivers are rarely behind a blue screen.");

        return null;
    }

    /// <summary>Correspondance nominative d'abord, famille ensuite.</summary>
    public static (DriverKbEntry Entry, bool Exact)? LookupAny(string? sysFileName, string? companyName)
    {
        if (Lookup(sysFileName) is { } exact) return (exact, true);
        if (LookupFamily(sysFileName, companyName) is { } fam) return (fam, false);
        return null;
    }

    public static int Count => Entries.Count;

    /// <summary>Toutes les fiches, pour le contrôle de couverture des tests.</summary>
    internal static IEnumerable<KeyValuePair<string, DriverKbEntry>> All => Entries;

    /// <summary>
    /// Réponse exploitable MÊME hors base : un pilote inconnu ne doit pas laisser
    /// l'utilisateur devant un nom de fichier brut. On se rabat alors sur l'éditeur
    /// et le nom lisible inscrits dans le fichier — que le scan collecte déjà — en
    /// disant clairement que la base ne connaît pas ce pilote plutôt qu'en inventant
    /// un correctif.
    /// </summary>
    public static DriverKbEntry Describe(string? sysFileName, string? companyName, string? displayName = null)
    {
        if (Lookup(sysFileName) is { } known) return known;

        var vendor = (companyName ?? "").Trim();
        var file = (sysFileName ?? "").Trim();
        bool isMicrosoft = vendor.Contains("Microsoft", StringComparison.OrdinalIgnoreCase);

        var owner = !string.IsNullOrWhiteSpace(displayName) ? displayName!.Trim()
                  : vendor.Length > 0 ? vendor
                  : "éditeur inconnu";

        var context = vendor.Length > 0
            ? $"Pilote édité par {vendor}. Il ne figure pas dans la base de correspondance de FaultTracePC : le contexte précis de ses plantages n'est pas documenté ici."
            : "Ce pilote ne figure pas dans la base de correspondance, et son fichier n'indique aucun éditeur.";

        var fix = isMicrosoft
            ? "Composant de Windows : la correction passe par Windows Update, pas par un site d'éditeur. Ces fichiers sont presque toujours des victimes — chercher le vrai responsable parmi les pilotes tiers récents."
            : vendor.Length > 0
                ? $"Identifier le logiciel ou le matériel {vendor} présent sur cette machine, puis le mettre à jour depuis le site officiel de l'éditeur — ou le désinstaller s'il ne sert plus. "
                  + "La commande « pnputil /enum-drivers » dans un terminal administrateur relie chaque pilote au périphérique et à l'éditeur d'origine."
                : $"Rechercher le nom « {file} » pour identifier le logiciel qui l'a installé, puis mettre ce logiciel à jour ou le désinstaller. "
                  + "La commande « pnputil /enum-drivers » dans un terminal administrateur donne l'inventaire complet des pilotes et de leur origine.";

        return new DriverKbEntry(owner, context, fix);
    }

    private static readonly Dictionary<string, DriverKbEntry> Entries = new()
    {
        // ---- GPU -----------------------------------------------------------
        ["nvlddmkm.sys"] = new("NVIDIA (pilote graphique)",
            "Le pilote d'affichage NVIDIA — l'un des .sys les plus fréquents dans les BSOD, souvent après une mise à jour ratée ou en surchauffe.",
            "Désinstallation propre avec DDU en mode sans échec, puis installer le dernier pilote « Game Ready » ou « Studio » SANS GeForce Experience. Vérifier la température GPU en charge ; si crash en jeu uniquement, tester sans overclocking.",
            "NVIDIA (display driver)",
            "The NVIDIA display driver — one of the .sys files most often seen in BSODs, frequently after a failed update or while overheating.",
            "Clean removal with DDU in safe mode, then install the latest “Game Ready” or “Studio” driver WITHOUT GeForce Experience. Check the GPU temperature under load; if it only crashes in games, test without overclocking."),
        ["amdkmdag.sys"] = new("AMD (pilote graphique)",
            "Le pilote d'affichage AMD Radeon.",
            "DDU en mode sans échec puis dernier pilote Adrenalin (ou la version « recommandée » plutôt qu'« optionnelle » si crashs répétés). Désactiver l'overclocking auto de la carte pour tester.",
            "AMD (display driver)",
            "The AMD Radeon display driver.",
            "DDU in safe mode then the latest Adrenalin driver (or the “recommended” version rather than the “optional” one if crashes repeat). Turn off the card's automatic overclocking to test."),
        ["atikmdag.sys"] = new("AMD (pilote graphique, ancien)",
            "Ancien pilote d'affichage AMD.",
            "DDU puis pilote AMD à jour ; si le matériel est ancien, prendre la dernière version LTS supportant la carte.",
            "AMD (display driver, legacy)",
            "Older AMD display driver.",
            "DDU then an up-to-date AMD driver; on older hardware, take the latest LTS version that still supports the card."),
        ["igdkmd64.sys"] = new("Intel (graphique intégré)",
            "Le pilote graphique intégré Intel.",
            "Installer le pilote depuis Intel Driver & Support Assistant (pas seulement Windows Update) ; sur portable, essayer aussi la version du constructeur du PC.",
            "Intel (integrated graphics)",
            "The Intel integrated graphics driver.",
            "Install the driver from Intel Driver & Support Assistant (not only Windows Update); on a laptop, also try the PC manufacturer's version."),
        ["dxgkrnl.sys"] = new("Windows (noyau DirectX)",
            "Composant Windows victime : le vrai fautif est presque toujours le pilote graphique en dessous.",
            "Traiter comme un crash de pilote GPU : DDU + réinstallation propre du pilote NVIDIA/AMD/Intel.",
            "Windows (DirectX kernel)",
            "A Windows component caught in the crossfire: the real culprit is almost always the display driver underneath.",
            "Treat it as a GPU driver crash: DDU plus a clean reinstall of the NVIDIA/AMD/Intel driver."),
        ["dxgmms2.sys"] = new("Windows (gestion mémoire vidéo)",
            "Composant Windows victime — pointe vers le pilote graphique ou la VRAM.",
            "Réinstallation propre du pilote GPU ; si récurrent, tester la carte dans une autre machine ou surveiller sa température.",
            "Windows (video memory management)",
            "A Windows component caught in the crossfire — points to the display driver or the VRAM.",
            "Clean reinstall of the GPU driver; if it recurs, test the card in another machine or watch its temperature."),

        // ---- Réseau --------------------------------------------------------
        ["e1d65x64.sys"] = new("Intel (carte réseau Ethernet)",
            "Pilote Ethernet Intel.",
            "Mettre à jour depuis Intel (ou le site de la carte mère). Si crash en veille/reprise : désactiver « Autoriser l'ordinateur à éteindre ce périphérique » dans les propriétés de la carte.",
            "Intel (Ethernet adapter)",
            "Intel Ethernet driver.",
            "Update from Intel (or the motherboard website). If it crashes on sleep/resume: untick “Allow the computer to turn off this device” in the adapter properties."),
        ["rt640x64.sys"] = new("Realtek (carte réseau Ethernet)",
            "Pilote Ethernet Realtek — crashs connus en veille ou fort trafic sur d'anciennes versions.",
            "Installer le dernier pilote depuis le site de la carte mère/portable ; éviter le pilote Windows Update générique si le problème persiste.",
            "Realtek (Ethernet adapter)",
            "Realtek Ethernet driver — known crashes on sleep or heavy traffic with older versions.",
            "Install the latest driver from the motherboard or laptop website; avoid the generic Windows Update driver if the problem persists."),
        ["rtwlane.sys"] = new("Realtek (Wi-Fi)",
            "Pilote Wi-Fi Realtek.",
            "Dernier pilote constructeur ; si instable, désactiver l'économie d'énergie de la carte Wi-Fi.",
            "Realtek (Wi-Fi)",
            "Realtek Wi-Fi driver.",
            "Latest manufacturer driver; if unstable, turn off power saving on the Wi-Fi adapter."),
        ["netwtw10.sys"] = new("Intel (Wi-Fi)",
            "Pilote Wi-Fi Intel (série AX/AC).",
            "Mettre à jour via Intel Driver & Support Assistant — les correctifs Wi-Fi Intel sont fréquents. Mettre aussi à jour le Bluetooth Intel (même puce).",
            "Intel (Wi-Fi)",
            "Intel Wi-Fi driver (AX/AC series).",
            "Update through Intel Driver & Support Assistant — Intel Wi-Fi fixes are frequent. Update the Intel Bluetooth too (same chip)."),
        ["nwifi.sys"] = new("Windows (Wi-Fi) — victime",
            "Composant Wi-Fi de Windows : le fautif réel est généralement le pilote de la carte en dessous.",
            "Mettre à jour le pilote Wi-Fi du constructeur (Intel/Realtek/MediaTek).",
            "Windows (Wi-Fi) — victim",
            "The Windows Wi-Fi component: the real culprit is usually the adapter driver underneath.",
            "Update the manufacturer's Wi-Fi driver (Intel/Realtek/MediaTek)."),

        // ---- Stockage ------------------------------------------------------
        ["stornvme.sys"] = new("Windows (NVMe) — souvent victime",
            "Pilote NVMe de Windows : les crashs pointent souvent vers le firmware du SSD ou son alimentation.",
            "Mettre à jour le FIRMWARE du SSD (outil du fabricant : Samsung Magician, WD Dashboard…) et le BIOS ; vérifier que le SSD ne surchauffe pas.",
            "Windows (NVMe) — often a victim",
            "The Windows NVMe driver: crashes often point to the SSD firmware or its power supply.",
            "Update the SSD FIRMWARE (manufacturer tool: Samsung Magician, WD Dashboard…) and the BIOS; check that the SSD is not overheating."),
        ["iastora.sys"] = new("Intel RST (stockage)",
            "Pilote Intel Rapid Storage.",
            "Mettre à jour Intel RST depuis le site de la carte mère ; si non utilisé (pas de RAID), envisager de repasser sur le pilote NVMe/AHCI standard de Windows.",
            "Intel RST (storage)",
            "Intel Rapid Storage driver.",
            "Update Intel RST from the motherboard website; if it is not used (no RAID), consider going back to the standard Windows NVMe/AHCI driver."),
        ["ntfs.sys"] = new("Windows (système de fichiers) — victime",
            "NTFS plante généralement à cause d'un disque défaillant, d'un filtre (antivirus) ou d'une corruption.",
            "chkdsk /f sur le volume système, contrôle SMART, et regarder quel antivirus/filtre de fichiers est installé.",
            "Windows (file system) — victim",
            "NTFS usually crashes because of a failing disk, a filter (antivirus) or corruption.",
            "chkdsk /f on the system volume, a SMART check, and look at which antivirus or file filter is installed."),

        // ---- Virtualisation ------------------------------------------------
        ["bindflt.sys"] = new("Windows (Bind Filter — WSL/conteneurs)",
            "Le filtre de liaison de fichiers utilisé par WSL2, Docker Desktop et les conteneurs Windows ; déclenché par vmwp.exe (Hyper-V).",
            "Composant Windows : Windows Update + « wsl --update » (le script de réparation les exécute). Si le crash persiste système à jour, réduire les montages de fichiers Windows↔WSL/Docker (travailler dans le système de fichiers Linux) en attendant le correctif.",
            "Windows (Bind Filter — WSL/containers)",
            "The file binding filter used by WSL2, Docker Desktop and Windows containers; triggered by vmwp.exe (Hyper-V).",
            "A Windows component: Windows Update plus “wsl --update” (the repair script runs both). If the crash persists on an up-to-date system, reduce Windows↔WSL/Docker file mounts (work inside the Linux file system) while waiting for the fix."),
        ["wcifs.sys"] = new("Windows (filtre conteneurs)",
            "Filtre d'isolation de fichiers des conteneurs Windows (même famille que bindflt).",
            "Windows Update + wsl --update ; problème connu sur certaines builds, corrigé par mises à jour cumulatives.",
            "Windows (container filter)",
            "The file isolation filter for Windows containers (same family as bindflt).",
            "Windows Update plus wsl --update; a known problem on certain builds, fixed by cumulative updates."),
        ["vmci.sys"] = new("VMware",
            "Pilote de communication VMware Workstation/Player.",
            "Mettre à jour VMware ; si désinstallé, nettoyer les restes avec l'outil officiel VMware.",
            "VMware",
            "VMware Workstation/Player communication driver.",
            "Update VMware; if it has been uninstalled, clean up the leftovers with the official VMware tool."),
        ["vboxdrv.sys"] = new("Oracle VirtualBox",
            "Pilote noyau de VirtualBox.",
            "Mettre à jour VirtualBox (les versions anciennes crashent sur les Windows récents) ; le désinstaller proprement s'il ne sert plus.",
            "Oracle VirtualBox",
            "The VirtualBox kernel driver.",
            "Update VirtualBox (older versions crash on recent Windows); uninstall it cleanly if it is no longer used."),

        // ---- Sécurité / antivirus -----------------------------------------
        ["klif.sys"] = new("Kaspersky",
            "Filtre système Kaspersky.",
            "Mettre à jour Kaspersky ; en cas de crashs répétés, désinstaller avec kavremover (outil officiel) et réinstaller la dernière version.",
            "Kaspersky",
            "Kaspersky system filter.",
            "Update Kaspersky; if crashes repeat, uninstall with kavremover (the official tool) and reinstall the latest version."),
        ["aswsp.sys"] = new("Avast/AVG",
            "Module d'auto-protection Avast/AVG.",
            "Mettre à jour le produit ; si crashs répétés, désinstaller avec avastclear/AVG Clear (outils officiels).",
            "Avast/AVG",
            "The Avast/AVG self-protection module.",
            "Update the product; if crashes repeat, uninstall with avastclear/AVG Clear (the official tools)."),
        ["mbamswissarmy.sys"] = new("Malwarebytes",
            "Pilote Malwarebytes.",
            "Mettre à jour Malwarebytes ou le retirer avec le Malwarebytes Support Tool ; conflits connus avec d'autres antivirus en temps réel simultanés.",
            "Malwarebytes",
            "The Malwarebytes driver.",
            "Update Malwarebytes or remove it with the Malwarebytes Support Tool; known conflicts with other real-time antivirus products running at the same time."),
        ["csagent.sys"] = new("CrowdStrike Falcon",
            "Capteur EDR CrowdStrike.",
            "À traiter avec l'équipe sécurité : mise à jour du capteur via la console Falcon — ne pas supprimer manuellement.",
            "CrowdStrike Falcon",
            "The CrowdStrike EDR sensor.",
            "To be handled with the security team: update the sensor from the Falcon console — do not remove it by hand."),
        ["tmcomm.sys"] = new("Trend Micro",
            "Pilote commun Trend Micro (Apex One/Deep Security).",
            "Mettre à jour l'agent depuis la console, ou le retirer proprement avec l'outil CUT de Trend Micro si le produit n'est plus utilisé.",
            "Trend Micro",
            "The Trend Micro common driver (Apex One/Deep Security).",
            "Update the agent from the console, or remove it cleanly with Trend Micro's CUT tool if the product is no longer used."),
        ["wdfilter.sys"] = new("Windows Defender — victime",
            "Filtre de Microsoft Defender : rarement fautif lui-même ; souvent un conflit avec un autre antivirus.",
            "Vérifier qu'un seul antivirus temps réel est actif ; mettre Windows à jour ; retirer les restes d'anciens antivirus (outils de nettoyage officiels).",
            "Windows Defender — victim",
            "The Microsoft Defender filter: rarely at fault itself; usually a conflict with another antivirus.",
            "Check that only one real-time antivirus is active; update Windows; remove the leftovers of former antivirus products (official cleanup tools)."),

        // ---- Anti-triche ---------------------------------------------------
        ["easyanticheat.sys"] = new("Easy Anti-Cheat (jeux)",
            "Pilote anti-triche utilisé par de nombreux jeux.",
            "Réparer EAC (EasyAntiCheat_Setup.exe → Repair dans le dossier du jeu) ou réinstaller le jeu ; vérifier la compatibilité après une grosse mise à jour Windows.",
            "Easy Anti-Cheat (games)",
            "The anti-cheat driver used by many games.",
            "Repair EAC (EasyAntiCheat_Setup.exe → Repair in the game folder) or reinstall the game; check compatibility after a major Windows update."),
        ["bedaisy.sys"] = new("BattlEye (jeux)",
            "Pilote anti-triche BattlEye.",
            "Mettre à jour le jeu (BattlEye se met à jour avec) ; crashs souvent liés à un conflit avec un autre logiciel bas niveau (RGB, overlay, antivirus).",
            "BattlEye (games)",
            "The BattlEye anti-cheat driver.",
            "Update the game (BattlEye updates with it); crashes are often a conflict with another low-level program (RGB, overlay, antivirus)."),
        ["vgk.sys"] = new("Riot Vanguard (Valorant/LoL)",
            "Anti-triche Riot, chargé au démarrage.",
            "Désinstaller/réinstaller Riot Vanguard ; s'il n'est plus utilisé, le désinstaller (il tourne en permanence).",
            "Riot Vanguard (Valorant/LoL)",
            "The Riot anti-cheat, loaded at startup.",
            "Uninstall and reinstall Riot Vanguard; if it is no longer used, uninstall it (it runs permanently)."),

        // ---- Périphériques / RGB / outils constructeur ---------------------
        ["ene.sys"] = new("ENE (éclairage RGB)",
            "Pilote RGB (RAM/cartes mères) tristement célèbre pour ses BSOD.",
            "Mettre à jour ou désinstaller le logiciel RGB associé (Armoury Crate, MSI Center, RGB Fusion…) ; ce pilote est dispensable.",
            "ENE (RGB lighting)",
            "An RGB driver (RAM/motherboards) notorious for its BSODs.",
            "Update or uninstall the associated RGB software (Armoury Crate, MSI Center, RGB Fusion…); this driver is dispensable."),
        ["asio3.sys"] = new("ASUS (Armoury Crate / AI Suite)",
            "Pilote bas niveau des utilitaires ASUS — source connue de BSOD.",
            "Mettre à jour Armoury Crate ou, mieux, désinstaller les utilitaires ASUS non indispensables avec leur outil de désinstallation officiel.",
            "ASUS (Armoury Crate / AI Suite)",
            "The low-level driver of the ASUS utilities — a known source of BSODs.",
            "Update Armoury Crate or, better, uninstall the non-essential ASUS utilities with their official removal tool."),
        ["iocbios2.sys"] = new("Intel (Extreme Tuning Utility)",
            "Pilote d'overclocking Intel XTU.",
            "Désinstaller XTU s'il ne sert plus ; retirer tout profil d'overclocking pour tester la stabilité.",
            "Intel (Extreme Tuning Utility)",
            "The Intel XTU overclocking driver.",
            "Uninstall XTU if it is no longer used; remove any overclocking profile to test stability."),
        ["scdemu.sys"] = new("PowerISO (lecteur virtuel)",
            "Pilote de lecteur CD/DVD virtuel de PowerISO — versions anciennes incompatibles avec les Windows récents.",
            "Mettre à jour PowerISO vers la dernière version, ou le désinstaller si le montage d'images ne sert plus (Windows monte les ISO nativement).",
            "PowerISO (virtual drive)",
            "The PowerISO virtual CD/DVD driver — older versions are incompatible with recent Windows versions.",
            "Update PowerISO to the latest version, or uninstall it if mounting images is no longer needed (Windows mounts ISOs natively)."),

        // ---- Lecteurs virtuels et pilotes hérités --------------------------
        ["dtsoftbus01.sys"] = new("DAEMON Tools (lecteur virtuel)",
            "Pilote de lecteur de disque virtuel de DAEMON Tools.",
            "Mettre à jour DAEMON Tools, ou le désinstaller : Windows 10 et 11 montent nativement les fichiers ISO par un clic droit, ce qui rend l'outil dispensable.",
            "DAEMON Tools (virtual drive)",
            "The DAEMON Tools virtual disk driver.",
            "Update DAEMON Tools, or uninstall it: Windows 10 and 11 mount ISO files natively from a right-click, which makes the tool dispensable."),
        ["sptd.sys"] = new("SPTD (DAEMON Tools / Alcohol 120 %)",
            "Pilote d'accès bas niveau installé par d'anciennes versions de DAEMON Tools et d'Alcohol — cause d'écran bleu bien identifiée sur Windows 10 et 11.",
            "Le supprimer avec l'utilitaire officiel « SPTD standalone installer », PAS par le Gestionnaire de périphériques : une suppression manuelle laisse le pilote enregistré au démarrage.",
            "SPTD (DAEMON Tools / Alcohol 120%)",
            "A low-level access driver installed by old versions of DAEMON Tools and Alcohol — a well-identified cause of blue screens on Windows 10 and 11.",
            "Remove it with the official “SPTD standalone installer” utility, NOT from Device Manager: a manual removal leaves the driver registered at startup."),

        // ---- Stockage ------------------------------------------------------
        ["storahci.sys"] = new("Windows (contrôleur SATA)",
            "Pilote AHCI intégré à Windows : victime plutôt que coupable, il plante quand le disque ou son câble répond mal.",
            "Suspecter le disque, son câble SATA ou le contrôleur : contrôler les erreurs CRC dans la section SMART de ce rapport (elles désignent le câble), puis l'état général du disque.",
            "Windows (SATA controller)",
            "The AHCI driver built into Windows: a victim rather than a culprit, it crashes when the drive or its cable answers badly.",
            "Suspect the drive, its SATA cable or the controller: check the CRC errors in the SMART section of this report (they point at the cable), then the general state of the drive."),
        ["iastorac.sys"] = new("Intel Rapid Storage Technology",
            "Pilote de gestion des disques Intel, version récente — présent sur beaucoup de portables.",
            "Installer la version fournie par le FABRICANT DE L'ORDINATEUR (Dell, HP, Lenovo…) plutôt que la version générique Intel, souvent incompatible avec la configuration d'usine.",
            "Intel Rapid Storage Technology",
            "The Intel disk management driver, recent version — present on many laptops.",
            "Install the version supplied by the COMPUTER MANUFACTURER (Dell, HP, Lenovo…) rather than the generic Intel one, which is often incompatible with the factory configuration."),
        ["volsnap.sys"] = new("Windows (clichés instantanés)",
            "Composant gérant points de restauration et sauvegardes.",
            "Composant Windows : chercher le vrai fautif du côté des outils de sauvegarde tiers et de l'antivirus. Vérifier aussi l'espace disque réservé aux points de restauration.",
            "Windows (shadow copies)",
            "The component handling restore points and backups.",
            "A Windows component: look for the real culprit among third-party backup tools and the antivirus. Also check the disk space reserved for restore points."),

        // ---- Réseau --------------------------------------------------------
        ["tcpip.sys"] = new("Windows (pile réseau)",
            "Composant victime : il plante quand un VPN, un pare-feu tiers ou un pilote de carte réseau lui transmet des données incorrectes.",
            "Passer en revue les VPN, antivirus à filtrage réseau et pilotes de carte réseau installés ou mis à jour récemment. « Réinitialiser la pile réseau » dans la boîte à outils remet les composants Windows d'aplomb.",
            "Windows (network stack)",
            "A victim component: it crashes when a VPN, a third-party firewall or a network adapter driver hands it incorrect data.",
            "Review the VPNs, network-filtering antivirus products and network adapter drivers installed or updated recently. “Reset the network stack” in the toolbox puts the Windows components back in order."),
        ["ndis.sys"] = new("Windows (couche réseau)",
            "Couche entre Windows et les cartes réseau — également victime.",
            "Le responsable est un pilote de carte réseau, un VPN ou un filtre réseau tiers : les mettre à jour ou retirer ceux qui ne servent plus.",
            "Windows (network layer)",
            "The layer between Windows and the network adapters — also a victim.",
            "The culprit is a network adapter driver, a VPN or a third-party network filter: update them or remove the ones no longer used."),
        ["tap0901.sys"] = new("OpenVPN (adaptateur virtuel)",
            "Carte réseau virtuelle d'OpenVPN, également utilisée par de nombreux clients VPN commerciaux.",
            "Mettre à jour le client VPN. S'il n'est plus utilisé, le désinstaller : ce pilote survit souvent à la désinstallation du logiciel et continue de s'intercaler dans le trafic.",
            "OpenVPN (virtual adapter)",
            "The OpenVPN virtual network adapter, also used by many commercial VPN clients.",
            "Update the VPN client. If it is no longer used, uninstall it: this driver often survives the software's removal and keeps inserting itself into the network stack."),
        ["wintun.sys"] = new("WireGuard (adaptateur virtuel)",
            "Carte réseau virtuelle de WireGuard et de plusieurs VPN modernes.",
            "Mettre le client VPN à jour, ou le désinstaller s'il ne sert plus.",
            "WireGuard (virtual adapter)",
            "The virtual network adapter of WireGuard and of several modern VPNs.",
            "Update the VPN client, or uninstall it if it is no longer used."),

        // ---- Virtualisation ------------------------------------------------
        ["vmswitch.sys"] = new("Windows (commutateur Hyper-V)",
            "Réseau virtuel d'Hyper-V, également utilisé par WSL2 et le sous-système Android.",
            "Mettre à jour Windows ET le pilote de la carte réseau PHYSIQUE : le commutateur virtuel plante le plus souvent à cause du pilote réel qu'il utilise dessous.",
            "Windows (Hyper-V switch)",
            "The Hyper-V virtual network, also used by WSL2 and the Android subsystem.",
            "Update Windows AND the PHYSICAL network adapter driver: the virtual switch usually crashes because of the real driver it sits on top of."),
        ["vmx86.sys"] = new("VMware Workstation / Player",
            "Moteur de virtualisation VMware.",
            "Mettre VMware à jour. Faire cohabiter deux hyperviseurs (Hyper-V, VMware, VirtualBox) sur la même machine est une cause classique de plantage : n'en garder qu'un actif.",
            "VMware Workstation / Player",
            "The VMware virtualisation engine.",
            "Update VMware. Running two hypervisors (Hyper-V, VMware, VirtualBox) side by side on the same machine is a classic cause of crashes: keep only one active."),

        // ---- Sécurité ------------------------------------------------------
        ["ehdrv.sys"] = new("ESET (antivirus)",
            "Pilote noyau d'ESET.",
            "Mettre le produit à jour. S'il ne sert plus, le retirer avec l'outil de désinstallation officiel de l'éditeur : une désinstallation classique laisse souvent le pilote noyau en place.",
            "ESET (antivirus)",
            "The ESET kernel driver.",
            "Update the product. If it is no longer used, remove it with the vendor's official removal tool: a normal uninstall often leaves the kernel driver in place."),
        ["mfehidk.sys"] = new("McAfee (antivirus)",
            "Pilote de protection McAfee.",
            "Mettre à jour McAfee, ou le supprimer avec MCPR (l'outil officiel de suppression) s'il n'est plus utilisé.",
            "McAfee (antivirus)",
            "The McAfee protection driver.",
            "Update McAfee, or remove it with MCPR (the official removal tool) if it is no longer used."),
        ["trufos.sys"] = new("Bitdefender (antivirus)",
            "Pilote d'analyse en temps réel de Bitdefender.",
            "Mettre Bitdefender à jour, ou utiliser son outil de désinstallation officiel s'il ne sert plus.",
            "Bitdefender (antivirus)",
            "The Bitdefender real-time scanning driver.",
            "Update Bitdefender, or use its official removal tool if it is no longer used."),
        ["symefa.sys"] = new("Norton / Symantec Endpoint",
            "Pilote de protection Norton ou Symantec.",
            "Mettre à jour le produit, ou le supprimer avec l'outil officiel (Norton Remove and Reinstall) s'il n'est plus utilisé.",
            "Norton / Symantec Endpoint",
            "The Norton or Symantec protection driver.",
            "Update the product, or remove it with the official tool (Norton Remove and Reinstall) if it is no longer used."),
        ["tmxpflt.sys"] = new("Trend Micro (antivirus)",
            "Pilote de filtrage de fichiers Trend Micro.",
            "Mettre à jour Trend Micro, ou le supprimer avec son outil de désinstallation officiel. Une désinstallation incomplète laisse des services et pilotes orphelins qui continuent de planter.",
            "Trend Micro (antivirus)",
            "The Trend Micro file filtering driver.",
            "Update Trend Micro, or remove it with its official removal tool. An incomplete uninstall leaves orphaned services and drivers that keep crashing."),

        // ---- Périphériques et surcouches ------------------------------------
        ["rtcore64.sys"] = new("MSI Afterburner / RivaTuner",
            "Pilote d'accès matériel des utilitaires d'overclocking et d'affichage à l'écran — référencé par Microsoft parmi les pilotes vulnérables.",
            "Mettre MSI Afterburner à jour, ou le désinstaller s'il ne sert plus. Retirer tout profil d'overclocking avant de conclure : c'est une cause d'instabilité au moins aussi fréquente que le pilote lui-même.",
            "MSI Afterburner / RivaTuner",
            "The hardware access driver of overclocking and on-screen display utilities — listed by Microsoft among the vulnerable drivers.",
            "Update MSI Afterburner, or uninstall it if it is no longer used. Remove any overclocking profile before concluding: that is a cause of instability at least as frequent as the driver itself."),
        ["winring0x64.sys"] = new("WinRing0 (utilitaires de supervision)",
            "Pilote d'accès matériel bas niveau utilisé par de nombreux outils de température et de ventilation — ancien et connu pour être vulnérable.",
            "Mettre à jour l'utilitaire qui l'installe, ou le remplacer par une alternative s'appuyant sur PawnIO, son remplaçant moderne.",
            "WinRing0 (monitoring utilities)",
            "A low-level hardware access driver used by many temperature and fan tools — old and known to be vulnerable.",
            "Update the utility that installs it, or replace it with an alternative built on PawnIO, its modern replacement."),
        ["rzpnk.sys"] = new("Razer Synapse",
            "Pilote des périphériques Razer.",
            "Mettre Razer Synapse à jour. Si le logiciel ne sert qu'à régler l'éclairage, envisager de le désinstaller : ses pilotes s'installent très bas dans le système.",
            "Razer Synapse",
            "The driver for Razer peripherals.",
            "Update Razer Synapse. If the software is only used to set the lighting, consider uninstalling it: its drivers install very low in the system."),
        ["nvhda64v.sys"] = new("NVIDIA (audio HDMI)",
            "Partie son de la carte graphique NVIDIA, utilisée quand l'audio passe par HDMI ou DisplayPort.",
            "Traiter comme un crash de pilote graphique : DDU en mode sans échec, puis réinstallation propre du pilote NVIDIA.",
            "NVIDIA (HDMI audio)",
            "The sound part of the NVIDIA graphics card, used when audio goes over HDMI or DisplayPort.",
            "Treat it as a display driver crash: DDU in safe mode, then a clean reinstall of the NVIDIA driver."),
        ["cldflt.sys"] = new("Windows (OneDrive — fichiers à la demande)",
            "Composant affichant les fichiers OneDrive sans les télécharger.",
            "Mettre OneDrive à jour. En cas de plantages répétés, désactiver temporairement « Fichiers à la demande » dans ses paramètres pour confirmer la piste.",
            "Windows (OneDrive — files on demand)",
            "The component that shows OneDrive files without downloading them.",
            "Update OneDrive. If crashes repeat, temporarily turn off “Files On-Demand” in its settings to confirm the lead."),

        // ---- Noyau : victimes, jamais coupables ------------------------------
        ["ntoskrnl.exe"] = new("Windows (noyau)",
            "Le cœur de Windows. Il n'est presque jamais la cause : il est ce qui CONSTATE l'erreur provoquée par autre chose.",
            "Quand l'analyse ne désigne que le noyau, la piste principale est la MÉMOIRE (MemTest86, 4 passes minimum, XMP désactivé), puis un pilote tiers que l'analyse n'a pas su nommer.",
            "Windows (kernel)",
            "The heart of Windows. It is almost never the cause: it is what REPORTS the error caused by something else.",
            "When the analysis names only the kernel, the main lead is MEMORY (MemTest86, at least 4 passes, XMP disabled), then a third-party driver the analysis could not name."),
        ["wdf01000.sys"] = new("Windows (framework de pilotes)",
            "Socle sur lequel s'appuient de nombreux pilotes tiers — victime du pilote qu'il héberge.",
            "Chercher le vrai responsable parmi les pilotes tiers récemment installés ou mis à jour, puis vérifier l'intégrité du système (sfc puis DISM).",
            "Windows (driver framework)",
            "The foundation many third-party drivers are built on — a victim of the driver it hosts.",
            "Look for the real culprit among third-party drivers installed or updated recently, then check the system integrity (sfc then DISM)."),
        ["fltmgr.sys"] = new("Windows (gestionnaire de filtres de fichiers)",
            "Couche qui orchestre les pilotes s'intercalant dans l'accès aux fichiers : antivirus, sauvegarde, chiffrement, synchronisation.",
            "Le fautif est l'un des filtres qu'il pilote. La commande « fltmc filters » dans un terminal administrateur en donne la liste complète — y chercher antivirus, outils de sauvegarde et clients de synchronisation.",
            "Windows (file filter manager)",
            "The layer that orchestrates the drivers inserting themselves into file access: antivirus, backup, encryption, synchronisation.",
            "The culprit is one of the filters it drives. The command “fltmc filters” in an administrator terminal lists them all — look there for antivirus, backup tools and synchronisation clients."),
        ["tm.sys"] = new("Windows (gestionnaire de transactions du noyau)",
            "Composant interne garantissant la cohérence des opérations sur le registre et les fichiers. Son nom évoque Trend Micro : c'est une COÏNCIDENCE.",
            "Ne jamais supprimer ce fichier : il est indispensable au fonctionnement de Windows. Le traiter comme une victime et chercher la cause ailleurs, notamment du côté des filtres de fichiers tiers.",
            "Windows (kernel transaction manager)",
            "An internal component guaranteeing the consistency of registry and file operations. Its name evokes Trend Micro: that is a COINCIDENCE.",
            "Never delete this file: Windows cannot work without it. Treat it as a victim and look for the cause elsewhere, particularly among third-party file filters."),
        ["sptd2.sys"] = new("SPTD (DAEMON Tools / Alcohol 120 %)",
            "Variante récente du pilote d'accès bas niveau SPTD, installée par DAEMON Tools et Alcohol. Même famille que sptd.sys, historiquement impliquée dans des écrans bleus.",
            "Le supprimer avec l'utilitaire officiel « SPTD standalone installer », PAS par le Gestionnaire de périphériques. Si DAEMON Tools ne sert plus, le désinstaller : Windows monte les images ISO nativement.",
            "SPTD (DAEMON Tools / Alcohol 120%)",
            "A recent variant of the SPTD low-level access driver, installed by DAEMON Tools and Alcohol. Same family as sptd.sys, historically involved in blue screens.",
            "Remove it with the official “SPTD standalone installer” utility, NOT from Device Manager. If DAEMON Tools is no longer used, uninstall it: Windows mounts ISO images natively."),
        ["npcap.sys"] = new("Npcap (Wireshark, Nmap)",
            "Pilote de capture de paquets réseau installé par Wireshark ou Nmap. Il s'intercale dans toutes les communications réseau.",
            "Mettre à jour Npcap dans sa dernière version. S'il n'est plus utilisé, le désinstaller : un pilote de capture actif en permanence n'a d'intérêt que si l'on analyse effectivement le réseau.",
            "Npcap (Wireshark, Nmap)",
            "The network packet capture driver installed by Wireshark or Nmap. It inserts itself into all network communications.",
            "Update Npcap to its latest version. If it is no longer used, uninstall it: a capture driver running permanently is only worth it if you actually analyse the network."),
        ["intelppm.sys"] = new("Windows (gestion d'énergie du processeur Intel)",
            "Composant régulant fréquence et consommation du processeur.",
            "Composant Windows : vérifier le BIOS et les réglages d'alimentation, et retirer tout overclocking ou sous-voltage appliqué par un utilitaire tiers.",
            "Windows (Intel processor power management)",
            "The component regulating the processor's frequency and consumption.",
            "A Windows component: check the BIOS and the power settings, and remove any overclocking or undervolting applied by a third-party utility."),
    };
}
