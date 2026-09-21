## In English

**This release makes a fleet of Windows machines visible and serviceable from a single console.** Until now FaultTracePC diagnosed one computer at a time, on that computer. From this version it deploys itself to remote machines, wakes the ones that are switched off, keeps the alert history of every machine it watches, and can replay the last hours of any of them without going there.

The version number jumps from 1.6.2 to **1.7.1** with no 1.7.0 published. That is deliberate: 1.7.0 was built, installed on a real school network, and **five defects were found by using it**. Three of them could only ever be found by running it on a real fleet — a setting that could not be entered, a label that asserted more than the software knew, and a failure that was recorded before being repaired and then counted as a failure. Publishing 1.7.0 and 1.7.1 within days of each other would have said nothing useful. This is 1.7.0, corrected before anyone else had to meet those defects.

The notes below are in French. The full English description of the software is here: **[FaultTracePC — diagnose, monitor and repair a Windows PC](https://palisser.fr/spip.php?article31)**

| File | For whom |
|---|---|
| `FaultTracePC-1.7.1.msi` | Classic installation, or Group Policy deployment |
| `FaultTracePC-1.7.1-portable.zip` | No installation: unzip and run |

The `Source code` archives are generated automatically by GitHub: they contain the code, not the compiled software.

Set the language at install time: `msiexec /i FaultTracePC-1.7.1.msi FTPCLANG=en /qn`

**These files are not digitally signed.** Windows will show "Unknown publisher" — click *More info* then *Run anyway*. Checking the SHA-256 fingerprint at the bottom of this page is the only way to be sure the file you downloaded is the one published here.

---

Jusqu'ici, FaultTracePC diagnostiquait **une** machine, **sur** cette machine. Cette version lui apprend le parc : déployer, réveiller, surveiller, se souvenir, et relire à distance.

## Pourquoi 1.7.1 et pas 1.7.0

La 1.7.0 a existé. Elle a été construite, installée sur un réseau d'établissement réel, et utilisée pendant deux jours sur des postes de salle de classe. **Cinq défauts en sont sortis**, dont trois qu'aucune relecture de code n'aurait trouvés : un réglage indispensable qu'aucun chemin ne permettait de saisir, un libellé qui affirmait plus que le logiciel ne savait, et un échec consigné *avant* d'être réparé puis compté comme un échec.

Publier la 1.7.0 puis la 1.7.1 à trois jours d'intervalle n'aurait rien appris à personne. Ceci **est** la 1.7.0, corrigée avant que quelqu'un d'autre ait à rencontrer ces défauts.

## Déployer sur trente postes sans aller les voir — point 64

La console reçoit un onglet **Inventaire**. Il lit trois sources — l'annuaire Active Directory, la liste de la console, un annuaire d'adresses MAC — et les rapproche. **Il ne contacte aucune machine** : il répond à « quels postes existent », jamais à « lesquels vont bien ». C'est une distinction de fond, et elle est écrite en haut de l'onglet.

De là, deux actions, dans cet ordre :

- **Vérifier** — compte d'ordinateur, réponse réseau, partage administratif, gestion à distance. **Rien n'est modifié, rien n'est réveillé, aucune question n'est posée.** On sait ce qui va se passer avant que ça se passe.
- **Déployer** — copie du paquet, installation silencieuse, effacement du paquet, règle de pare-feu limitée aux adresses privées, mise en mode parc, et vérification que le port répond vraiment.

Un poste **éteint** est réveillé par le réseau. Son adresse MAC est cherchée dans trois sources, par ordre de fiabilité : le bail DHCP du serveur, qui fait foi ; un fichier `postes.csv`, photographie qui vieillit ; le cache ARP local, qui ne vaut que si le poste a parlé récemment. Le DHCP n'est interrogé **que** pour le poste demandé, et seulement quand il ne répond pas — aucun balayage.

**Quand aucune des trois ne répond, le logiciel dit laquelle a manqué et pourquoi.** C'est une leçon payée cher : un refus sans motif coûte des heures.

Chaque exécution écrit un journal lisible et un journal JSON, une ligne par poste et par étape. La console les relit, remet le résultat dans la colonne, et sait recocher les seuls postes à reprendre.

**Le script n'est pas perdu pour ceux qui n'ont pas la console** : il voyage dans le logiciel mais reste utilisable seul, depuis une clé USB, exactement comme avant.

## Se souvenir de ce qu'un poste a signalé — point 43

Un poste réimagé repart vierge. On perd alors la preuve qu'il chauffait depuis six mois, **au moment précis où elle servirait à le faire remplacer**.

La console archive désormais les alertes de chaque poste, à chaque relève, dans ses propres données. Une colonne « Alertes 90 j » donne le compte et la part de critiques ; une fenêtre d'historique les relit sur 7, 30, 90, 365 jours ou depuis toujours, et nomme la règle qui revient le plus souvent.

**Rien n'est jamais effacé**, et cette fenêtre ne contacte aucune machine : elle relit une archive locale. Un poste réinstallé garde donc son passé.

## Relire les dernières heures d'un poste sans s'y rendre — point 46

Le service de surveillance enregistrait déjà un relevé toutes les dix secondes. Personne ne pouvait le lire à distance.

La boîte noire s'ouvre maintenant depuis la console : processeur, températures, carte graphique, mémoire, mémoire virtuelle, et les processus les plus gourmands, sur la dernière heure ou au-delà. Les événements Windows marquants y sont signalés au passage.

**La fenêtre annonce elle-même sa limite**, en haut, avant qu'on lise la moindre ligne : un relevé toutes les dix secondes suffit largement pour une surchauffe, qui monte en minutes ; un pic d'une seconde peut passer entre deux relevés. Ce n'est pas un enregistrement continu, et le logiciel ne laisse pas croire le contraire.

## Ce que deux jours sur un parc réel ont corrigé

| Défaut | Ce qu'il faisait |
|---|---|
| Un réglage impossible à saisir | Le script sait demander le nom du serveur DHCP — mais seulement quand quelqu'un est devant l'écran. Piloté par la console, il se tait par construction : la question n'était **jamais** posée, et le réveil réseau ne pouvait donc aboutir pour **aucun** poste, quelle que soit sa configuration. Le réglage vit maintenant dans la console. |
| Un champ qui ne servait qu'à une colonne | L'annuaire d'adresses MAC saisi dans la console n'atteignait pas le script, qui cherchait le sien ailleurs. Deux notions du même fichier cohabitaient sans se parler. |
| « hors annuaire » | La recherche part de l'unité d'organisation saisie et descend. Elle ne voit rien de ce qui vit ailleurs dans le domaine. Écrire « hors annuaire » affirmait que le poste n'existe pas, alors que le logiciel avait seulement constaté qu'**il n'est pas ici**. Le libellé dit désormais « pas dans cette unité ». |
| Un échec rattrapé compté comme un échec | Un poste éteint ne répond pas : le script écrit `echec`, le réveille, puis écrit `ok`. Une gestion à distance muette : il écrit `echec`, démarre le service, puis réécrit `ok`. **C'est le déroulement normal d'un déploiement réussi.** La console s'arrêtait à la première ligne en échec, et déclarait en échec des postes installés et joignables. |
| Un poste à recopier à la main | Un poste mis en mode parc s'inscrit maintenant tout seul dans la supervision. Seuls ceux-là : un poste simplement installé n'écoute pas, et une ligne qui échoue toujours vaut moins que pas de ligne. |

Le quatrième avait trois conséquences pour un seul défaut : la colonne de vérification mentait, la reprise des échecs recochait des postes déjà traités, et l'inscription automatique ne trouvait plus rien à inscrire. **Un défaut de raisonnement se paie à tous les endroits qui s'appuient dessus** — d'où la règle placée dans le noyau, et non dans l'interface.

## Ce que le logiciel refuse de faire, et le dit

- **Le réveil réseau ne traverse pas les routeurs.** Un poste sur un autre sous-réseau que la console ne sera pas réveillé, quelle que soit la configuration de son BIOS. Le script l'écrit à chaque envoi plutôt que de laisser conclure à une panne.
- **Aucun secret n'est écrit sur le disque par le déploiement.** Le secret maître du parc est demandé à chaque exécution, sans rien afficher, et n'apparaît dans aucun journal.
- **Aucun jeton n'est inscrit** quand un poste rejoint la console : il se déduit du secret maître et du nom de machine à chaque interrogation.
- **Le mode parc n'écoute que les adresses privées.** La règle de pare-feu posée sur le poste est limitée à `127.0.0.1`, `10.0.0.0/8`, `172.16.0.0/12` et `192.168.0.0/16`, et le service refuse une source hors de ces plages — puis **écrit pourquoi**, ce qu'il ne faisait pas.
- **Vérifier ne modifie rien.** Ni réveil, ni copie, ni installation, ni mise en parc, ni question posée.

## Ce qui ne change pas

Aucun format de fichier n'évolue : les analyses enregistrées par les versions 1.6.x restent lisibles, et la comparaison entre deux scans fonctionne d'une version à l'autre. Le rapport de diagnostic, son moteur de règles et ses 32 conclusions sont ceux de la 1.6.2.

Cette version **ajoute** une capacité, elle n'en modifie aucune.

716 tests, aucun échec.
