## In English

**This release separates the two audiences of FaultTracePC.** Someone diagnosing their own PC no longer sees the fleet console; an administrator gets it with one tick box at installation — and no machine that already had it loses it.

The fleet console can now act on one machine or on a selection: right-click menu (update this machine, refresh it, black box, alert history, remote diagnosis) and "Refresh selection". The fleet report groups the drivers that ship with Windows and with Microsoft Defender into one card per family instead of one card per file, and brings together the machines that report the same hardware errors. Finally, single-machine reports were re-read card by card against real diagnostics from a school network: ten corrections came out of it, so that the advice matches what was actually measured.

The notes below are in French. The full English description of the software is here: **[FaultTracePC — diagnose, monitor and repair a Windows PC](https://palisser.fr/spip.php?article31)**

| File | For whom |
|---|---|
| `FaultTracePC-1.9.0.msi` | Classic installation, or Group Policy deployment |
| `FaultTracePC-1.9.0-portable.zip` | No installation: unzip and run |

The `Source code` archives are generated automatically by GitHub: they contain the code, not the compiled software.

Set the language at install time: `msiexec /i FaultTracePC-1.9.0.msi FTPCLANG=en /qn`

Fleet console at install time: `msiexec /i FaultTracePC-1.9.0.msi /qn ADDLOCAL=Main,ParkFeature` (or `ADDLOCAL=ALL`).

**These files are not digitally signed.** Windows will show "Unknown publisher" — click *More info* then *Run anyway*. Checking the SHA-256 fingerprint at the bottom of this page is the only way to be sure the file you downloaded is the one published here.

---

## Deux publics, un seul paquet — point 79

Un particulier veut diagnostiquer sa machine ; un administrateur veut piloter un parc. Jusqu'ici, tous deux recevaient la console de parc. Elle devient une **case à cocher de l'installation**, « Mode parc (administration d'un parc) », **non cochée par défaut**.

- **Ce qu'elle change** : l'affichage des boutons **« Parc »** (la console) et **« Réseau »**. Rien d'autre.
- **Ce qu'elle ne change pas** : la surveillance d'un poste. Un poste déjà configuré pour la console reste joignable, que la case soit cochée ou non — sa configuration ne dépend pas de l'installateur.
- **Mise à jour depuis la 1.8.x ou avant** : la case est **cochée d'office**. Personne ne perd la console qu'il avait.
- **Mises à jour suivantes** : le choix fait est conservé, case cochée ou non.
- **Un poste exposé sur le réseau** montre toujours « Réseau », même sans la case : qui s'assoit devant doit pouvoir le voir.
- **La version portable** affiche toujours la console.

**En ligne de commande**, la case s'obtient avec `ADDLOCAL=Main,ParkFeature` ou `ADDLOCAL=ALL`. Attention : une liste `ADDLOCAL` installe **exactement** ce qu'elle nomme — `ADDLOCAL=Main`, forme publiée jusqu'ici pour se passer du raccourci du Bureau, installe donc **sans** la console, même en mise à jour. Le poste reste supervisé ; seuls les deux boutons disparaissent.

**Éprouvé avant publication** sur une machine virtuelle Windows 11 à jour : installation neuve (interactive et silencieuse), ajout de la case, retrait de la case, mise à jour depuis la 1.8.1 (case cochée d'office), mise à jour d'un poste sans la case (elle reste décochée), mise à jour silencieuse avec `ADDLOCAL=Main` d'un poste supervisé (toujours joignable), désinstallation (configuration du poste conservée), version portable.

## Console de parc : agir sur un poste ou sur une sélection — points 93, 94 et 100

- **Clic droit sur un poste** de la Supervision : « Mettre à jour ce poste… », « Actualiser ce poste », boîte noire, historique des alertes, diagnostic à distance. Les boutons du bas restent : le menu est un raccourci de plus.
- **« Mettre à jour ce poste… »** ne déploie rien directement : il ouvre l'onglet Inventaire avec ce seul poste coché — en lisant l'inventaire s'il n'est pas encore affiché —, et le déploiement passe par le chemin habituel, avec sa vérification du paquet et sa confirmation.
- **Sélection de plusieurs postes** (Ctrl+clic, Maj+clic) et bouton **« Actualiser la sélection »**, qui n'interroge que ces postes et les garde sélectionnés. Retirer plusieurs postes demande de taper leur nombre pour confirmer. Les actions qui ne visent qu'un poste le disent quand plusieurs sont sélectionnés, au lieu d'agir sur le premier.
- **Colonne « Version »** : « ⬆ 1.8.0 → 1.9.0 » (la version du poste, et celle vers laquelle le mettre à jour) au lieu d'un texte coupé ; le sens des flèches est expliqué en survolant l'en-tête.

## Rapport du parc : moins de bruit — point 96

Les pilotes livrés avec Windows changent tous ensemble à chaque mise à jour, ceux de Microsoft Defender avec sa plateforme. Le rapport du parc en faisait une carte par fichier — 175 cartes sur un parc de dix postes, dont 145 pour Windows. Ils tiennent désormais en **une carte par famille**, qui nomme les postes en retard et dit sur combien de fichiers. Les pilotes de constructeurs (réseau, son, graphique…) gardent chacun leur carte : ce sont eux qui méritent un regard.

## Rapport du parc : une même erreur matérielle sur plusieurs postes — point 99

Quand plusieurs postes signalent des erreurs matérielles (WHEA) sur leur période d'analyse, le rapport du parc les rapproche désormais en une carte qui les nomme, du plus touché au moins touché, et conseille de chercher d'abord une cause commune — même modèle, même BIOS, même pilote de chipset. Cette carte fonctionne aussi avec des postes encore en 1.8.1.

Une fois les postes en 1.9.0, elle dit aussi ce qu'ils ont en commun : même modèle, même version de BIOS, même composant mis en cause (par exemple le même lien PCI Express) — et elle devient critique dès qu'une erreur est fatale. Le résumé envoyé par chaque poste porte pour cela quatre informations de plus ; une console plus ancienne les ignore simplement.

## Le rapport d'un poste : des conseils alignés sur ce qui est mesuré

Des diagnostics réels, faits sur le parc d'un établissement et sur le poste de l'auteur, ont été relus carte par carte. Voici ce qu'ils ont corrigé.

- Dans le rapport français, la carte antivirus affichait le mode de Windows Defender en anglais (« mode Not running »). Il est désormais traduit (« arrêté », « passif », « normal »…) ; une valeur inconnue est rendue telle quelle.
- Quand des échecs répétés de services viennent d'un ou deux services seulement — souvent un logiciel tiers —, la carte ne propose plus sfc /scannow et DISM, qui réparent Windows et non ce logiciel. Ils restent proposés quand les échecs se dispersent sur de nombreux services, ce qui désigne plutôt le système.
- Un même écran bleu pouvait être compté deux fois quand le minidump et MEMORY.DMP l'enregistraient sous deux formes du même code (par exemple 0x1000007E et 0x7E) : deux conclusions critiques pour un seul plantage. Les deux formes sont désormais reconnues comme un seul arrêt.
- Quand des services échouent de façon répétée, ceux dont le programme a disparu (restes d'une désinstallation) sont mis à part avant de conclure : ils ne font plus croire à un problème de Windows quand les autres échecs se concentrent sur un ou deux services.
- Les plantages répétés d'un logiciel tiers ne proposent plus sfc /scannow et DISM ; ils restent proposés quand c'est un composant de Windows qui plante.
- Sous un verdict matériel, le rapport ne dit plus « le problème principal vient d'un pilote ».
- Un écran bleu plus ancien que la période analysée ne décide plus du verdict : il reste affiché, avec la mention qu'il est antérieur, et le verdict le signale. Constaté sur un parc : « PILOTE défectueux » pour un unique écran bleu vieux de dix mois, sur une machine saine.
- L'écran bleu 0x23 (FAT_FILE_SYSTEM, système de fichiers des clés USB et cartes mémoire en FAT32) est désormais décrit, avec la marche à suivre. Deux textes qui renvoyaient à des étapes internes du projet (« Phase 2 », « Phase 3 ») ont été réécrits.
- Un pilote inscrit pour démarrer avec Windows dont le fichier a disparu (reste d'un antivirus désinstallé, par exemple) est désormais signalé. Windows échouait à le lancer à chaque démarrage, mais ne le montrait pas dans la liste des pilotes que lisait FaultTracePC : il est maintenant lu directement dans le registre.
- Un service coupé pendant l'arrêt ou le redémarrage de Windows n'est plus compté comme un échec. Constaté sur un parc : l'agent d'inventaire « s'arrêtait de manière inattendue » chaque soir, toujours dans la même seconde que l'arrêt de Windows programmé — il était coupé, pas en panne.

## Windows à jour

Constaté pendant les essais : sur un Windows 10 de 2019 jamais mis à jour, le service de surveillance s'arrête aussitôt (« Your Windows doesn't fully support CET »). La cause est la protection CET, que les programmes construits avec .NET utilisent depuis .NET 9, et qu'un Windows aussi ancien ne gère pas entièrement. Installer les mises à jour de Windows règle le problème. FaultTracePC demande donc un **Windows 10 ou 11 à jour**.
