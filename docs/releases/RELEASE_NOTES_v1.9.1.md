## In English

**This release fixes what nine real diagnostics showed, two days after 1.9.0.** Nine reports from a school network were re-read card by card. On five of them, the verdict blamed the CPU, motherboard or power supply for hardware errors that the hardware had corrected by itself. Other reports counted the same .NET application crash twice, showed the same power loss in two cards, listed network filter layers as network adapters, and called a driver rollback an update. Five fixes, no new feature: the installation is the same as 1.9.0.

The notes below are in French. The full English description of the software is here: **[FaultTracePC — diagnose, monitor and repair a Windows PC](https://palisser.fr/spip.php?article31)**

| File | For whom |
|---|---|
| `FaultTracePC-1.9.1.msi` | Classic installation, or Group Policy deployment |
| `FaultTracePC-1.9.1-portable.zip` | No installation: unzip and run |

The `Source code` archives are generated automatically by GitHub: they contain the code, not the compiled software.

Set the language at install time: `msiexec /i FaultTracePC-1.9.1.msi FTPCLANG=en /qn`

Fleet console at install time: `msiexec /i FaultTracePC-1.9.1.msi /qn ADDLOCAL=Main,ParkFeature` (or `ADDLOCAL=ALL`).

**These files are not digitally signed.** Windows will show "Unknown publisher" — click *More info* then *Run anyway*. Checking the SHA-256 fingerprint at the bottom of this page is the only way to be sure the file you downloaded is the one published here.

---

Deux jours après la 1.9.0, neuf diagnostics faits sur le parc d'un établissement ont été relus carte par carte. Sur cinq d'entre eux, le verdict accusait le processeur, la carte mère ou l'alimentation pour des erreurs que le matériel avait corrigées lui-même. Cette version corrige ce que ces rapports ont montré. Aucune fonction nouvelle : l'installation est la même qu'en 1.9.0.

## Un verdict « MATÉRIELLE » pour des erreurs toutes corrigées — point 112

Cinq verdicts sur neuf disaient « MATÉRIELLE (CPU/carte mère/alimentation ou surchauffe) ». Chaque fois, la seule raison était une carte « erreurs matérielles CORRIGÉES — lien PCI Express » : le matériel avait récupéré à chaque fois, rien n'avait été perdu. La carte se contredisait même : « défaillance CPU, mémoire, carte mère ou alimentation », puis « ce sont des erreurs de LIEN, pas de processeur ».

La cause : le rapport réunit en une seule carte ce que voient le journal de Windows et la surveillance temps réel, et garde la gravité la plus forte des deux. Or les alertes de la surveillance écrites **avant la 1.8.1** ne gardaient pas le numéro de l'événement, et valaient donc « critique » par défaut. Elles restaient dans la période analysée.

Désormais, quand le journal de Windows de la période ne contient **que** des erreurs corrigées, une alerte ancienne de cette période est relue comme ce qu'elle est : une erreur corrigée. Rien ne change si le journal contient une seule erreur fatale, ou si l'alerte est plus ancienne que le journal lu. Les alertes de la boîte noire, elles, restent telles qu'elles ont été écrites.

## Un plantage d'application .NET compté deux fois — point 113

Quand une application construite avec .NET plante, Windows écrit deux événements à la même seconde. Le rapport comptait les deux : « MicrosoftSearchInBing.exe (33 crashs) », puis « (inconnue) (33 crashs) », cette seconde carte se disant même « anciennement instable » pour un plantage du matin.

Le nom de l'application est maintenant lu dans les deux événements, et un plantage n'est compté qu'une fois. Au passage, « (inconnue) » apparaissait en français dans le rapport anglais ; il devient « (unknown) ».

## Une même coupure dans deux cartes — point 114

Une coupure sans écran bleu apparaissait deux fois : « coupures brutales » (critique) et « arrêts inattendus » (avertissement), avec les mêmes dates. Elle n'en fait plus qu'une. La carte qui reste dit que la coupure a été vue par deux journaux de Windows, et reprend la piste que seule l'autre donnait : quelqu'un a peut-être maintenu le bouton d'alimentation — sur un poste partagé, demander d'abord aux utilisateurs.

## Des couches de filtrage listées comme des cartes réseau — point 111

La fiche « Cartes réseau » listait, après les vraies cartes, plus de soixante lignes comme « Ethernet-QoS Packet Scheduler-0000 » : les couches de filtrage posées sur chaque carte (pare-feu de Windows, QoS, capture réseau, filtres de VirtualBox ou de Fortinet…). Ce ne sont pas des cartes. Elles ne sont plus listées, et la fiche dit combien il y en a.

## Un pilote revenu en arrière n'est plus « mis à jour » — point 110

La section « Évolution depuis le dernier scan » rangeait sous « Pilotes mis à jour » un pilote passé à une version **plus ancienne**. Les numéros sont maintenant comparés dans le bon sens, et un retour en arrière a sa propre ligne : « Pilotes revenus à une version plus ancienne ».

## Ce qu'il faut mettre à jour, et où

Les cinq corrections portent sur le **rapport d'un poste**. Un diagnostic lancé à distance depuis la console est produit **par le poste lui-même** : il faut donc mettre à jour les postes, pas seulement la console. Le rapport du parc en profite dès que les postes ont été rediagnostiqués en 1.9.1.

## Ce qui ne change pas

- L'installation est la même qu'en 1.9.0. La case « Mode parc » garde le choix fait à l'installation précédente.
- Rappel : une liste `ADDLOCAL` installe exactement ce qu'elle nomme. `ADDLOCAL=Main` installe **sans** la console, même en mise à jour ; pour la garder, `ADDLOCAL=Main,ParkFeature`.
- FaultTracePC demande un Windows 10 ou 11 à jour.
- Les analyses enregistrées par les versions précédentes restent lisibles.
