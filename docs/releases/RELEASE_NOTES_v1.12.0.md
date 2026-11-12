## In English

**This release tells a processor slowed down by heat apart from one that is simply working hard, and tells how a computer stopped and started.**

- **Throttling, measured.** The report now judges the processor's temperature by its frequency: a processor that loses speed to stay under its temperature limit gets a "Processor throttled by heat" card, with how much slower it ran; a processor that runs hot but keeps its speed gets a plain information, no longer a "critical overheating". Thermal-paste advice is now given on a measured slowdown, never on the machine's age alone.
- **Shutdowns and startups.** A new report section lists, for each Windows startup, how the previous session ended, how long the computer stayed off, and how it came back. A computer off for five days or more gets a conclusion with the last thing Windows recorded before. A failure that happens while a computer is off is recorded nowhere; this is the closest the report can get.
- **Fleet console.** Columns can be sorted by clicking their header. The fleet report names the machines that stayed off for days while the other machines of the same model were starting, and says what sets them apart — or that nothing does.
- **Fixes.** Informational hardware events are no longer counted as hardware errors; a service whose file is missing is no longer presented as an incomplete uninstall when the software is still installed; preventive alerts in the fleet report are written in the console's language.

The notes below are in French. The full English description of the software is here: **[FaultTracePC — diagnose, monitor and repair a Windows PC](https://palisser.fr/spip.php?article31)**

| File | For whom |
|---|---|
| `FaultTracePC-1.12.0.msi` | Classic installation, or Group Policy deployment |
| `FaultTracePC-1.12.0-portable.zip` | No installation: unzip and run |

The `Source code` archives are generated automatically by GitHub: they contain the code, not the compiled software.

Set the language at install time: `msiexec /i FaultTracePC-1.12.0.msi FTPCLANG=en /qn`

Fleet console at install time: `msiexec /i FaultTracePC-1.12.0.msi /qn ADDLOCAL=Main,ParkFeature` (or `ADDLOCAL=ALL`).

**These files are not digitally signed.** Windows will show "Unknown publisher" — click *More info* then *Run anyway*. Checking the SHA-256 fingerprint at the bottom of this page is the only way to be sure the file you downloaded is the one published here.

---

Trois changements : le rapport distingue un processeur **bridé par la chaleur** d'un processeur qui travaille simplement fort ; il dit **comment le poste s'est arrêté et comment il a redémarré** ; et la console de parc **trie ses colonnes** et repère **les postes restés éteints pendant que leurs voisins démarraient**. L'installation est la même qu'en 1.11.0.

## Un processeur bridé par la chaleur, mesuré — point 120

La 1.11.0 enregistrait la fréquence et la consommation du processeur ; la 1.12.0 s'en sert.

**Pourquoi.** Mesuré sur deux portables réels en novembre 2026 : l'un montait jusqu'à 100 °C **en gardant sa fréquence maximale** — il travaillait, il ne se bridait pas ; l'autre, chargé à 100 % pendant trois minutes, restait à 95–99 °C pendant que sa fréquence **baissait d'environ 13 %** — lui se bridait. La température seule ne distingue pas ces deux cas ; la fréquence, si.

**Ce que dit maintenant le rapport, pour le processeur :**

- **« Processeur bridé par la chaleur : jusqu'à N % plus lent sous forte charge »** quand, pendant au moins une minute, chargé à plus de 80 % et à 90 °C ou plus, il n'a pas tenu 90 % de sa fréquence habituelle à froid. Niveau **Avertissement** ; **Critique** si un arrêt brutal a suivi, ou si la fréquence est tombée sous 70 %. Le poste devient lent, sans panne.
- **« Processeur à sa limite de température sous charge, sans ralentir »** quand il chauffe mais garde sa fréquence : une simple **information** — plus de « surchauffe critique », plus de « la machine s'éteint brutalement ».
- Sans mesure de fréquence (pilote PawnIO absent, journal ancien), l'ancienne règle s'applique.

La « fréquence habituelle à froid » est lue dans le journal de chaque poste. Quand elle n'a pu l'être qu'à faible charge, la conclusion est donnée avec une **confiance faible**, et le dit : un processeur tient souvent un peu moins sur tous ses cœurs que sur un seul.

**Pâte thermique** : le conseil n'est plus donné selon l'âge de la machine. Il l'est sur un ralentissement mesuré, ou quand une surchauffe revient après dépoussiérage.

**Pour comprendre une température très haute**, le service note désormais le nom des sondes de température du processeur, et, dans chaque relevé à 80 °C ou plus, celle qui a donné le maximum.

## Comment le poste s'est arrêté, comment il a redémarré — point 125

**Pourquoi.** Deux postes d'un parc ne se rallumaient plus et ne sont repartis qu'après plusieurs heures ou jours sans courant. Une panne qui survient **pendant que le poste est éteint** ne s'écrit nulle part. Mais la façon dont il s'est éteint juste avant, et la durée pendant laquelle il n'a pas tourné, se lisent dans le journal de Windows — et c'est ce qu'on cherche.

**Nouvelle section « Derniers arrêts et démarrages »** : pour chaque démarrage, comment la session précédente s'était terminée (arrêt propre, demandé par quel programme et au nom de quel compte ; arrêt inattendu ; mise en veille), combien de temps le poste est resté éteint, et comment il a repris (démarrage à froid, « démarrage rapide », sortie de veille).

**Une conclusion quand le poste est resté éteint cinq jours ou plus** — un week-end n'en fait pas : la façon dont la session d'avant s'était terminée, ce que Windows avait enregistré dans les 24 heures avant l'arrêt, et comment le poste a repris. C'est une **information** : un poste rangé pendant les vacances n'est pas en panne.

## Console de parc — points 121, 122 et 126

- **Trier les colonnes** en cliquant sur leur en-tête : une fois, ordre croissant ; deux fois, décroissant ; une flèche ▲ ou ▼ le montre. Les nombres se trient comme des nombres, les dates comme des dates, les noms dans l'ordre naturel (POSTE-2 avant POSTE-10) ; les cases vides restent en bas. Le tri tient après « Actualiser ». Onglet Inventaire compris.
- **Les postes restés éteints pendant que les autres démarraient.** Le rapport du parc nomme un poste resté éteint au moins 48 heures pendant que d'autres postes **du même modèle** démarraient au moins deux jours différents — « soit il n'a pas été allumé, soit il ne s'est pas allumé ». Il compare ensuite ces postes aux autres : version du BIOS, carte graphique et pilote, erreurs matérielles par composant ; il dit ce qui les distingue, ou que rien ne les distingue — auquel cas la piste va vers des exemplaires défaillants plutôt que vers un réglage commun.
- **Les alertes du rapport du parc** sont écrites dans la langue de la console (une alerte apparaissait en anglais dans un rapport en français).

## Corrections — points 123 et 124

- **Un événement matériel de niveau « Information » n'est plus compté comme une erreur.** Sur un poste, un seul événement de ce type faisait passer 18 erreurs corrigées en carte critique, avec des conseils de processeur et d'alimentation qui ne s'appliquaient pas.
- **« Service dont le programme est introuvable »** : le fichier absent est un fait ; « désinstallation incomplète » n'en est qu'une cause possible. Constaté avec un logiciel toujours installé, qui fonctionne et garde l'inscription d'un composant absent : la carte le dit désormais, et précise que ce n'est alors pas une panne.

## Limites, dites franchement

- **L'alerte en temps réel « Température du processeur élevée »** se déclenche toujours sur la seule température : au moment où il alerte, le service ne connaît pas encore la fréquence habituelle du poste. Un portable qui chauffe sans se brider continue donc de la recevoir, et elle peut encore faire pencher le verdict du rapport vers « MATÉRIELLE ». Les conclusions sur le processeur, elles, jugent sur la fréquence.
- **Bridage** : seuils provisoires, tirés de deux postes ; ils seront revus avec les relevés du parc. Sans le pilote PawnIO, pas de fréquence, donc pas de conclusion sur le bridage.
- **La carte des postes restés éteints** n'apparaît qu'entre postes équipés de la 1.12.0, du même modèle, et diagnostiqués récemment.

## Ce qu'il faut mettre à jour, et où

Tous les postes : c'est le service de chaque poste qui note les sondes de température, et c'est l'analyse faite sur le poste qui lit ses arrêts et démarrages. La console de parc, sur le poste d'administration, pour le tri et le rapport du parc.

## Ce qui ne change pas

- L'installation est la même qu'en 1.11.0. La case « Mode parc » garde le choix fait à l'installation précédente.
- Rappel : une liste `ADDLOCAL` installe exactement ce qu'elle nomme. `ADDLOCAL=Main` installe **sans** la console, même en mise à jour ; pour la garder, `ADDLOCAL=Main,ParkFeature`.
- FaultTracePC demande un Windows 10 ou 11 à jour.
- Les analyses enregistrées par les versions précédentes restent lisibles.
