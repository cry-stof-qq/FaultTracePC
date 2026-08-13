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
    };
}
