namespace FaultTracePC.Core.Analysis;

public sealed record DriverKbEntry(string Owner, string Context, string Fix);

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
                + "Sur un portable, privilégier la version du fabricant : elle est validée pour ce modèle précis.");

        if (Vendor("NVIDIA") && File("nv"))
            return new DriverKbEntry("NVIDIA (pilote de plateforme)",
                "Composant annexe du pilote NVIDIA (audio HDMI, gestion d'énergie, périphérique virtuel).",
                "Ces composants sont installés et mis à jour par le pilote graphique NVIDIA : le réinstaller proprement (DDU en mode sans échec, puis dernier pilote) corrige l'ensemble.");

        if (Vendor("Intel") && File("intel", "ial", "e1", "netwtw", "netwbw", "ibt", "iagpio", "iai2c", "iauart", "iaspi"))
            return new DriverKbEntry("Intel (pilote de plateforme)",
                "Composant de la plateforme Intel : carte réseau, Wi-Fi, Bluetooth, bus interne ou gestion d'énergie.",
                "Utiliser « Intel Driver & Support Assistant », ou la page de support du fabricant de l'ordinateur. "
                + "Sur un portable, la version du fabricant prime : la version générique Intel est parfois incompatible avec la configuration d'usine.");

        if (Vendor("Realtek") && File("rt", "rtk"))
            return new DriverKbEntry("Realtek (réseau ou audio)",
                "Composant Realtek : carte réseau filaire, Wi-Fi ou audio intégré.",
                "Télécharger le pilote sur le site du fabricant de la carte mère ou de l'ordinateur plutôt que via Windows Update, dont la version est souvent plus ancienne.");

        if (Vendor("Qualcomm", "Atheros") && File("qc", "ath", "qca"))
            return new DriverKbEntry("Qualcomm / Atheros (réseau sans fil)",
                "Composant Wi-Fi ou Bluetooth Qualcomm-Atheros.",
                "Mettre à jour depuis la page de support du fabricant de l'ordinateur. Si les coupures Wi-Fi accompagnent les plantages, tester en désactivant l'économie d'énergie de la carte dans le Gestionnaire de périphériques.");

        if (Vendor("MediaTek") && File("mtk", "mt"))
            return new DriverKbEntry("MediaTek (réseau sans fil)",
                "Composant Wi-Fi ou Bluetooth MediaTek.",
                "Mettre à jour depuis la page de support du fabricant de l'ordinateur.");

        if (Vendor("Oracle") && File("vbox"))
            return new DriverKbEntry("VirtualBox (Oracle)",
                "Composant de VirtualBox : carte réseau virtuelle, filtre réseau, support noyau ou passerelle USB. Ces pilotes s'intercalent très bas dans le système.",
                "Mettre VirtualBox à jour dans sa dernière version. Faire cohabiter deux hyperviseurs (Hyper-V, WSL2, VMware, VirtualBox) sur la même machine est une cause classique de plantage : "
                + "si VirtualBox ne sert plus, le désinstaller retire aussi ses filtres réseau, qui survivent souvent aux désinstallations partielles.");

        if (Vendor("Fortinet"))
            return new DriverKbEntry("Fortinet (VPN ou sécurité d'entreprise)",
                "Composant FortiClient : filtre réseau ou carte virtuelle de VPN d'entreprise. Ces filtres s'insèrent dans la pile réseau et figurent parmi les causes fréquentes de plantage réseau.",
                "Pilote généralement géré par l'administration de l'établissement : ne pas le désinstaller de son propre chef. "
                + "Signaler l'incident au service informatique avec la date et le code d'arrêt ; la correction passe par une mise à jour de FortiClient.");

        if (Vendor("Hewlett", "HP Inc", "Dell ", "Lenovo", "ASUSTeK", "Acer ", "Micro-Star", "Gigabyte"))
            return new DriverKbEntry("Pilote de plateforme du fabricant",
                "Composant installé par le fabricant de l'ordinateur : touches spéciales, capteurs, gestion d'alimentation ou utilitaire maison.",
                "Mettre à jour depuis l'assistant de support du fabricant (HP Support Assistant, Dell SupportAssist, Lenovo Vantage…), qui installe la version validée pour ce modèle précis. "
                + "Ces pilotes sont rarement en cause dans un écran bleu, mais leurs utilitaires associés peuvent l'être.");

        if (Vendor("Synaptics", "ELAN", "Alps"))
            return new DriverKbEntry("Pilote de pavé tactile",
                "Pilote du pavé tactile ou du dispositif de pointage du portable.",
                "Mettre à jour depuis la page de support du fabricant de l'ordinateur. Ces pilotes sont rarement en cause dans un écran bleu.");

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
            "Désinstallation propre avec DDU en mode sans échec, puis installer le dernier pilote « Game Ready » ou « Studio » SANS GeForce Experience. Vérifier la température GPU en charge ; si crash en jeu uniquement, tester sans overclocking."),
        ["amdkmdag.sys"] = new("AMD (pilote graphique)",
            "Le pilote d'affichage AMD Radeon.",
            "DDU en mode sans échec puis dernier pilote Adrenalin (ou la version « recommandée » plutôt qu'« optionnelle » si crashs répétés). Désactiver l'overclocking auto de la carte pour tester."),
        ["atikmdag.sys"] = new("AMD (pilote graphique, ancien)",
            "Ancien pilote d'affichage AMD.",
            "DDU puis pilote AMD à jour ; si le matériel est ancien, prendre la dernière version LTS supportant la carte."),
        ["igdkmd64.sys"] = new("Intel (graphique intégré)",
            "Le pilote graphique intégré Intel.",
            "Installer le pilote depuis Intel Driver & Support Assistant (pas seulement Windows Update) ; sur portable, essayer aussi la version du constructeur du PC."),
        ["dxgkrnl.sys"] = new("Windows (noyau DirectX)",
            "Composant Windows victime : le vrai fautif est presque toujours le pilote graphique en dessous.",
            "Traiter comme un crash de pilote GPU : DDU + réinstallation propre du pilote NVIDIA/AMD/Intel."),
        ["dxgmms2.sys"] = new("Windows (gestion mémoire vidéo)",
            "Composant Windows victime — pointe vers le pilote graphique ou la VRAM.",
            "Réinstallation propre du pilote GPU ; si récurrent, tester la carte dans une autre machine ou surveiller sa température."),

        // ---- Réseau --------------------------------------------------------
        ["e1d65x64.sys"] = new("Intel (carte réseau Ethernet)",
            "Pilote Ethernet Intel.",
            "Mettre à jour depuis Intel (ou le site de la carte mère). Si crash en veille/reprise : désactiver « Autoriser l'ordinateur à éteindre ce périphérique » dans les propriétés de la carte."),
        ["rt640x64.sys"] = new("Realtek (carte réseau Ethernet)",
            "Pilote Ethernet Realtek — crashs connus en veille ou fort trafic sur d'anciennes versions.",
            "Installer le dernier pilote depuis le site de la carte mère/portable ; éviter le pilote Windows Update générique si le problème persiste."),
        ["rtwlane.sys"] = new("Realtek (Wi-Fi)",
            "Pilote Wi-Fi Realtek.",
            "Dernier pilote constructeur ; si instable, désactiver l'économie d'énergie de la carte Wi-Fi."),
        ["netwtw10.sys"] = new("Intel (Wi-Fi)",
            "Pilote Wi-Fi Intel (série AX/AC).",
            "Mettre à jour via Intel Driver & Support Assistant — les correctifs Wi-Fi Intel sont fréquents. Mettre aussi à jour le Bluetooth Intel (même puce)."),
        ["nwifi.sys"] = new("Windows (Wi-Fi) — victime",
            "Composant Wi-Fi de Windows : le fautif réel est généralement le pilote de la carte en dessous.",
            "Mettre à jour le pilote Wi-Fi du constructeur (Intel/Realtek/MediaTek)."),

        // ---- Stockage ------------------------------------------------------
        ["stornvme.sys"] = new("Windows (NVMe) — souvent victime",
            "Pilote NVMe de Windows : les crashs pointent souvent vers le firmware du SSD ou son alimentation.",
            "Mettre à jour le FIRMWARE du SSD (outil du fabricant : Samsung Magician, WD Dashboard…) et le BIOS ; vérifier que le SSD ne surchauffe pas."),
        ["iastora.sys"] = new("Intel RST (stockage)",
            "Pilote Intel Rapid Storage.",
            "Mettre à jour Intel RST depuis le site de la carte mère ; si non utilisé (pas de RAID), envisager de repasser sur le pilote NVMe/AHCI standard de Windows."),
        ["ntfs.sys"] = new("Windows (système de fichiers) — victime",
            "NTFS plante généralement à cause d'un disque défaillant, d'un filtre (antivirus) ou d'une corruption.",
            "chkdsk /f sur le volume système, contrôle SMART, et regarder quel antivirus/filtre de fichiers est installé."),

        // ---- Virtualisation ------------------------------------------------
        ["bindflt.sys"] = new("Windows (Bind Filter — WSL/conteneurs)",
            "Le filtre de liaison de fichiers utilisé par WSL2, Docker Desktop et les conteneurs Windows ; déclenché par vmwp.exe (Hyper-V).",
            "Composant Windows : Windows Update + « wsl --update » (le script de réparation les exécute). Si le crash persiste système à jour, réduire les montages de fichiers Windows↔WSL/Docker (travailler dans le système de fichiers Linux) en attendant le correctif."),
        ["wcifs.sys"] = new("Windows (filtre conteneurs)",
            "Filtre d'isolation de fichiers des conteneurs Windows (même famille que bindflt).",
            "Windows Update + wsl --update ; problème connu sur certaines builds, corrigé par mises à jour cumulatives."),
        ["vmci.sys"] = new("VMware",
            "Pilote de communication VMware Workstation/Player.",
            "Mettre à jour VMware ; si désinstallé, nettoyer les restes avec l'outil officiel VMware."),
        ["vboxdrv.sys"] = new("Oracle VirtualBox",
            "Pilote noyau de VirtualBox.",
            "Mettre à jour VirtualBox (les versions anciennes crashent sur les Windows récents) ; le désinstaller proprement s'il ne sert plus."),

        // ---- Sécurité / antivirus -----------------------------------------
        ["klif.sys"] = new("Kaspersky",
            "Filtre système Kaspersky.",
            "Mettre à jour Kaspersky ; en cas de crashs répétés, désinstaller avec kavremover (outil officiel) et réinstaller la dernière version."),
        ["aswsp.sys"] = new("Avast/AVG",
            "Module d'auto-protection Avast/AVG.",
            "Mettre à jour le produit ; si crashs répétés, désinstaller avec avastclear/AVG Clear (outils officiels)."),
        ["mbamswissarmy.sys"] = new("Malwarebytes",
            "Pilote Malwarebytes.",
            "Mettre à jour Malwarebytes ou le retirer avec le Malwarebytes Support Tool ; conflits connus avec d'autres antivirus en temps réel simultanés."),
        ["csagent.sys"] = new("CrowdStrike Falcon",
            "Capteur EDR CrowdStrike.",
            "À traiter avec l'équipe sécurité : mise à jour du capteur via la console Falcon — ne pas supprimer manuellement."),
        ["tmcomm.sys"] = new("Trend Micro",
            "Pilote commun Trend Micro (Apex One/Deep Security).",
            "Mettre à jour l'agent depuis la console, ou le retirer proprement avec l'outil CUT de Trend Micro si le produit n'est plus utilisé."),
        ["wdfilter.sys"] = new("Windows Defender — victime",
            "Filtre de Microsoft Defender : rarement fautif lui-même ; souvent un conflit avec un autre antivirus.",
            "Vérifier qu'un seul antivirus temps réel est actif ; mettre Windows à jour ; retirer les restes d'anciens antivirus (outils de nettoyage officiels)."),

        // ---- Anti-triche ---------------------------------------------------
        ["easyanticheat.sys"] = new("Easy Anti-Cheat (jeux)",
            "Pilote anti-triche utilisé par de nombreux jeux.",
            "Réparer EAC (EasyAntiCheat_Setup.exe → Repair dans le dossier du jeu) ou réinstaller le jeu ; vérifier la compatibilité après une grosse mise à jour Windows."),
        ["bedaisy.sys"] = new("BattlEye (jeux)",
            "Pilote anti-triche BattlEye.",
            "Mettre à jour le jeu (BattlEye se met à jour avec) ; crashs souvent liés à un conflit avec un autre logiciel bas niveau (RGB, overlay, antivirus)."),
        ["vgk.sys"] = new("Riot Vanguard (Valorant/LoL)",
            "Anti-triche Riot, chargé au démarrage.",
            "Désinstaller/réinstaller Riot Vanguard ; s'il n'est plus utilisé, le désinstaller (il tourne en permanence)."),

        // ---- Périphériques / RGB / outils constructeur ---------------------
        ["ene.sys"] = new("ENE (éclairage RGB)",
            "Pilote RGB (RAM/cartes mères) tristement célèbre pour ses BSOD.",
            "Mettre à jour ou désinstaller le logiciel RGB associé (Armoury Crate, MSI Center, RGB Fusion…) ; ce pilote est dispensable."),
        ["asio3.sys"] = new("ASUS (Armoury Crate / AI Suite)",
            "Pilote bas niveau des utilitaires ASUS — source connue de BSOD.",
            "Mettre à jour Armoury Crate ou, mieux, désinstaller les utilitaires ASUS non indispensables avec leur outil de désinstallation officiel."),
        ["iocbios2.sys"] = new("Intel (Extreme Tuning Utility)",
            "Pilote d'overclocking Intel XTU.",
            "Désinstaller XTU s'il ne sert plus ; retirer tout profil d'overclocking pour tester la stabilité."),
        ["scdemu.sys"] = new("PowerISO (lecteur virtuel)",
            "Pilote de lecteur CD/DVD virtuel de PowerISO — versions anciennes incompatibles avec les Windows récents.",
            "Mettre à jour PowerISO vers la dernière version, ou le désinstaller si le montage d'images ne sert plus (Windows monte les ISO nativement)."),

        // ---- Lecteurs virtuels et pilotes hérités --------------------------
        ["dtsoftbus01.sys"] = new("DAEMON Tools (lecteur virtuel)",
            "Pilote de lecteur de disque virtuel de DAEMON Tools.",
            "Mettre à jour DAEMON Tools, ou le désinstaller : Windows 10 et 11 montent nativement les fichiers ISO par un clic droit, ce qui rend l'outil dispensable."),
        ["sptd.sys"] = new("SPTD (DAEMON Tools / Alcohol 120 %)",
            "Pilote d'accès bas niveau installé par d'anciennes versions de DAEMON Tools et d'Alcohol — cause d'écran bleu bien identifiée sur Windows 10 et 11.",
            "Le supprimer avec l'utilitaire officiel « SPTD standalone installer », PAS par le Gestionnaire de périphériques : une suppression manuelle laisse le pilote enregistré au démarrage."),

        // ---- Stockage ------------------------------------------------------
        ["storahci.sys"] = new("Windows (contrôleur SATA)",
            "Pilote AHCI intégré à Windows : victime plutôt que coupable, il plante quand le disque ou son câble répond mal.",
            "Suspecter le disque, son câble SATA ou le contrôleur : contrôler les erreurs CRC dans la section SMART de ce rapport (elles désignent le câble), puis l'état général du disque."),
        ["iastorac.sys"] = new("Intel Rapid Storage Technology",
            "Pilote de gestion des disques Intel, version récente — présent sur beaucoup de portables.",
            "Installer la version fournie par le FABRICANT DE L'ORDINATEUR (Dell, HP, Lenovo…) plutôt que la version générique Intel, souvent incompatible avec la configuration d'usine."),
        ["volsnap.sys"] = new("Windows (clichés instantanés)",
            "Composant gérant points de restauration et sauvegardes.",
            "Composant Windows : chercher le vrai fautif du côté des outils de sauvegarde tiers et de l'antivirus. Vérifier aussi l'espace disque réservé aux points de restauration."),

        // ---- Réseau --------------------------------------------------------
        ["tcpip.sys"] = new("Windows (pile réseau)",
            "Composant victime : il plante quand un VPN, un pare-feu tiers ou un pilote de carte réseau lui transmet des données incorrectes.",
            "Passer en revue les VPN, antivirus à filtrage réseau et pilotes de carte réseau installés ou mis à jour récemment. « Réinitialiser la pile réseau » dans la boîte à outils remet les composants Windows d'aplomb."),
        ["ndis.sys"] = new("Windows (couche réseau)",
            "Couche entre Windows et les cartes réseau — également victime.",
            "Le responsable est un pilote de carte réseau, un VPN ou un filtre réseau tiers : les mettre à jour ou retirer ceux qui ne servent plus."),
        ["tap0901.sys"] = new("OpenVPN (adaptateur virtuel)",
            "Carte réseau virtuelle d'OpenVPN, également utilisée par de nombreux clients VPN commerciaux.",
            "Mettre à jour le client VPN. S'il n'est plus utilisé, le désinstaller : ce pilote survit souvent à la désinstallation du logiciel et continue de s'intercaler dans le trafic."),
        ["wintun.sys"] = new("WireGuard (adaptateur virtuel)",
            "Carte réseau virtuelle de WireGuard et de plusieurs VPN modernes.",
            "Mettre le client VPN à jour, ou le désinstaller s'il ne sert plus."),

        // ---- Virtualisation ------------------------------------------------
        ["vmswitch.sys"] = new("Windows (commutateur Hyper-V)",
            "Réseau virtuel d'Hyper-V, également utilisé par WSL2 et le sous-système Android.",
            "Mettre à jour Windows ET le pilote de la carte réseau PHYSIQUE : le commutateur virtuel plante le plus souvent à cause du pilote réel qu'il utilise dessous."),
        ["vmx86.sys"] = new("VMware Workstation / Player",
            "Moteur de virtualisation VMware.",
            "Mettre VMware à jour. Faire cohabiter deux hyperviseurs (Hyper-V, VMware, VirtualBox) sur la même machine est une cause classique de plantage : n'en garder qu'un actif."),

        // ---- Sécurité ------------------------------------------------------
        ["ehdrv.sys"] = new("ESET (antivirus)",
            "Pilote noyau d'ESET.",
            "Mettre le produit à jour. S'il ne sert plus, le retirer avec l'outil de désinstallation officiel de l'éditeur : une désinstallation classique laisse souvent le pilote noyau en place."),
        ["mfehidk.sys"] = new("McAfee (antivirus)",
            "Pilote de protection McAfee.",
            "Mettre à jour McAfee, ou le supprimer avec MCPR (l'outil officiel de suppression) s'il n'est plus utilisé."),
        ["trufos.sys"] = new("Bitdefender (antivirus)",
            "Pilote d'analyse en temps réel de Bitdefender.",
            "Mettre Bitdefender à jour, ou utiliser son outil de désinstallation officiel s'il ne sert plus."),
        ["symefa.sys"] = new("Norton / Symantec Endpoint",
            "Pilote de protection Norton ou Symantec.",
            "Mettre à jour le produit, ou le supprimer avec l'outil officiel (Norton Remove and Reinstall) s'il n'est plus utilisé."),
        ["tmxpflt.sys"] = new("Trend Micro (antivirus)",
            "Pilote de filtrage de fichiers Trend Micro.",
            "Mettre à jour Trend Micro, ou le supprimer avec son outil de désinstallation officiel. Une désinstallation incomplète laisse des services et pilotes orphelins qui continuent de planter."),

        // ---- Périphériques et surcouches ------------------------------------
        ["rtcore64.sys"] = new("MSI Afterburner / RivaTuner",
            "Pilote d'accès matériel des utilitaires d'overclocking et d'affichage à l'écran — référencé par Microsoft parmi les pilotes vulnérables.",
            "Mettre MSI Afterburner à jour, ou le désinstaller s'il ne sert plus. Retirer tout profil d'overclocking avant de conclure : c'est une cause d'instabilité au moins aussi fréquente que le pilote lui-même."),
        ["winring0x64.sys"] = new("WinRing0 (utilitaires de supervision)",
            "Pilote d'accès matériel bas niveau utilisé par de nombreux outils de température et de ventilation — ancien et connu pour être vulnérable.",
            "Mettre à jour l'utilitaire qui l'installe, ou le remplacer par une alternative s'appuyant sur PawnIO, son remplaçant moderne."),
        ["rzpnk.sys"] = new("Razer Synapse",
            "Pilote des périphériques Razer.",
            "Mettre Razer Synapse à jour. Si le logiciel ne sert qu'à régler l'éclairage, envisager de le désinstaller : ses pilotes s'installent très bas dans le système."),
        ["nvhda64v.sys"] = new("NVIDIA (audio HDMI)",
            "Partie son de la carte graphique NVIDIA, utilisée quand l'audio passe par HDMI ou DisplayPort.",
            "Traiter comme un crash de pilote graphique : DDU en mode sans échec, puis réinstallation propre du pilote NVIDIA."),
        ["cldflt.sys"] = new("Windows (OneDrive — fichiers à la demande)",
            "Composant affichant les fichiers OneDrive sans les télécharger.",
            "Mettre OneDrive à jour. En cas de plantages répétés, désactiver temporairement « Fichiers à la demande » dans ses paramètres pour confirmer la piste."),

        // ---- Noyau : victimes, jamais coupables ------------------------------
        ["ntoskrnl.exe"] = new("Windows (noyau)",
            "Le cœur de Windows. Il n'est presque jamais la cause : il est ce qui CONSTATE l'erreur provoquée par autre chose.",
            "Quand l'analyse ne désigne que le noyau, la piste principale est la MÉMOIRE (MemTest86, 4 passes minimum, XMP désactivé), puis un pilote tiers que l'analyse n'a pas su nommer."),
        ["wdf01000.sys"] = new("Windows (framework de pilotes)",
            "Socle sur lequel s'appuient de nombreux pilotes tiers — victime du pilote qu'il héberge.",
            "Chercher le vrai responsable parmi les pilotes tiers récemment installés ou mis à jour, puis vérifier l'intégrité du système (sfc puis DISM)."),
        ["fltmgr.sys"] = new("Windows (gestionnaire de filtres de fichiers)",
            "Couche qui orchestre les pilotes s'intercalant dans l'accès aux fichiers : antivirus, sauvegarde, chiffrement, synchronisation.",
            "Le fautif est l'un des filtres qu'il pilote. La commande « fltmc filters » dans un terminal administrateur en donne la liste complète — y chercher antivirus, outils de sauvegarde et clients de synchronisation."),
        ["tm.sys"] = new("Windows (gestionnaire de transactions du noyau)",
            "Composant interne garantissant la cohérence des opérations sur le registre et les fichiers. Son nom évoque Trend Micro : c'est une COÏNCIDENCE.",
            "Ne jamais supprimer ce fichier : il est indispensable au fonctionnement de Windows. Le traiter comme une victime et chercher la cause ailleurs, notamment du côté des filtres de fichiers tiers."),
        ["sptd2.sys"] = new("SPTD (DAEMON Tools / Alcohol 120 %)",
            "Variante récente du pilote d'accès bas niveau SPTD, installée par DAEMON Tools et Alcohol. Même famille que sptd.sys, historiquement impliquée dans des écrans bleus.",
            "Le supprimer avec l'utilitaire officiel « SPTD standalone installer », PAS par le Gestionnaire de périphériques. Si DAEMON Tools ne sert plus, le désinstaller : Windows monte les images ISO nativement."),
        ["npcap.sys"] = new("Npcap (Wireshark, Nmap)",
            "Pilote de capture de paquets réseau installé par Wireshark ou Nmap. Il s'intercale dans toutes les communications réseau.",
            "Mettre à jour Npcap dans sa dernière version. S'il n'est plus utilisé, le désinstaller : un pilote de capture actif en permanence n'a d'intérêt que si l'on analyse effectivement le réseau."),
        ["intelppm.sys"] = new("Windows (gestion d'énergie du processeur Intel)",
            "Composant régulant fréquence et consommation du processeur.",
            "Composant Windows : vérifier le BIOS et les réglages d'alimentation, et retirer tout overclocking ou sous-voltage appliqué par un utilitaire tiers."),
    };
}
