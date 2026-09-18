## In English

**What a report claims, it must be able to back up.** Seven fixes, found the day after 1.6.0 shipped — by reading two reports the software had just produced on two **healthy** machines, one in French, one in English.

That turns out to be the harshest test there is. On a healthy machine, everything the software flags is a false positive. It flagged a network service that was doing exactly what Windows expects of it. It printed a driver "update" from version 10.0.26100.8875 to version 10.0.26100.8875. And it announced "Repeated disk errors (500)" where 500 was not a count at all, but the collection ceiling it had hit — the real total could have been 501 or 40,000.

That last one is the one that matters. A round number presented as a measurement distorts how serious a problem looks, in both directions, and no amount of re-reading the report could catch it: the number looked like a number. The ceiling now has a name, the software remembers when it hit it, and the report writes "at least 500" and says where the limit comes from.

Two additions came out of a single question — *"are these USB stick errors?"* — that the report made impossible to answer. Those 500 errors were three unrelated things filed under one heading: 479 bad blocks on a medium **unplugged since**, 20 resets of a SATA controller port, and one file-system error on a volume. They are now three cards, each with its own severity and its own advice. And a medium that is no longer plugged in is no longer just "a disk that was numbered 1": Windows remembers which hardware last mounted each drive letter, so the software can offer a name — clearly labelled a **lead**, never an identification, because nothing links a drive letter to the disk number it carried that day.

The notes below are in French. The full English description of the software is here: **[FaultTracePC — diagnose, monitor and repair a Windows PC](https://palisser.fr/spip.php?article31)**

| File | For whom |
|---|---|
| `FaultTracePC-1.6.1.msi` | Classic installation, or Group Policy deployment |
| `FaultTracePC-1.6.1-portable.zip` | No installation: unzip and run |

The `Source code` archives are generated automatically by GitHub: they contain the code, not the compiled software.

Set the language at install time: `msiexec /i FaultTracePC-1.6.1.msi FTPCLANG=en /qn`

**These files are not digitally signed.** Windows will show "Unknown publisher" — click *More info* then *Run anyway*. Checking the SHA-256 fingerprint at the bottom of this page is the only way to be sure the file you downloaded is the one published here.

---

Sept points, trouvés le lendemain de la 1.6.0 en relisant deux rapports que le logiciel venait de produire — l'un en français, l'autre en anglais, sur deux machines **saines**.

C'est le contrôle le plus sévère qui soit. Sur une machine en panne, on cherche si le logiciel trouve. Sur une machine saine, **tout ce qu'il signale est un faux positif**, et il n'y a nulle part où se cacher.

## Il signalait une anomalie là où Windows fonctionnait normalement

`NlaSvc` — le service qui décide si le réseau est un domaine, un réseau privé ou un réseau public — apparaissait `Stopped`, mode de démarrage `Manual`, sur **les deux** machines. Le rapport écrivait « Il devrait tourner » et proposait `sc.exe start NlaSvc`.

C'est l'état normal d'un service à la demande : Windows le démarre quand un composant en a besoin, et l'arrête ensuite. La règle comparait à une liste de services attendus sans jamais regarder leur mode de démarrage. Elle le regarde maintenant. Un service **automatique** arrêté, lui, reste signalé — celui-là est une vraie anomalie.

Dans la même famille, un défaut qui n'était pas encore sorti : `Microsoft Wi-Fi Direct Virtual Adapter` comptait comme une carte sans fil. Sur un poste fixe sans radio, la conclusion « aucun réseau Wi-Fi enregistré » se serait déclenchée sur une carte qui n'existe pas physiquement. Il faut désormais deux conditions pour être une vraie carte sans fil — être déclarée physique par Windows, **et** ne pas porter un marqueur de virtualisation dans sa description. Une seule ne suffit pas : certaines cartes virtuelles se déclarent physiques.

Et le rapport français affichait `Running · Auto`. Il affiche « en cours » et « automatique ».

## Un chiffre rond qui n'était pas une mesure

Le rapport titrait **« Erreurs disque répétées (500) »**.

500 n'était pas un décompte. C'était la limite de collecte du journal d'événements, en dur dans le code depuis toujours, atteinte ce jour-là. Le vrai total pouvait être 501 comme 40 000.

C'est le genre de défaut contre lequel la relecture ne peut rien : le chiffre avait l'air d'un chiffre. Et il fausse l'appréciation de la gravité **dans les deux sens** — on peut aussi bien s'alarmer pour 500 erreurs quand il y en a 40 000 que traiter 500 comme un plafond alors qu'il n'en manquait qu'une.

La limite porte maintenant un nom, le collecteur retient les catégories où la coupe a eu lieu, et le rapport écrit « au moins 500 » en disant d'où vient la limite. La même prudence s'applique aux comparaisons d'une analyse à l'autre, pour les erreurs disque comme pour les erreurs matérielles WHEA.

## Une flèche entre deux fois le même numéro

Dans « Pilotes mis à jour depuis le scan précédent » : `afd.sys : 10.0.26100.8875 → 10.0.26100.8875`.

Le logiciel mémorise chaque pilote sous la forme *version | date de fichier*, et comparait la valeur entière. Windows réécrit régulièrement ces fichiers — réparation de l'image, restauration de composants — sans changer le pilote d'un iota. La date bougeait, la comparaison voyait une mise à jour.

**Ce n'était pas qu'une question d'affichage.** Cette liste alimente la conclusion introduite en 1.6.0 : *« le pilote accusé a été remplacé entre les deux analyses, et les plantages ont continué — ce n'est donc pas lui. »* Sur une fausse mise à jour, cette phrase **disculpait un pilote qui n'avait jamais été remplacé**. L'inverse exact de ce à quoi elle sert. La comparaison ne porte plus que sur la version.

## « C'est des erreurs de clé USB ? »

La question posée devant le rapport, à laquelle le rapport ne permettait pas de répondre.

Les 500 erreurs étaient trois choses sans rapport entre elles :

- **479** blocs défectueux sur `\Device\Harddisk1`, un support débranché depuis ;
- **20** réinitialisations de `\Device\RaidPort1`, un port du contrôleur SATA — qui, lui, appartient bien à la machine ;
- **1** erreur de structure de système de fichiers sur un volume.

Un seul titre, une seule gravité, une seule recommandation — qui commençait par la gestion d'alimentation PCI Express et le firmware du SSD. Inutile pour les trois.

La 1.6.0 avait déjà séparé les *conseils* dans un texte commun. Il fallait séparer les *cartes*. Les événements sont maintenant répartis selon la nature du périphérique qu'ils citent — disque monté, port de contrôleur, volume, support amovible, support absent, aucun périphérique nommé — et chaque nature reçoit sa carte, sa gravité et son conseil. Un support débranché ne pèse plus sur la gravité de la machine ; un port de contrôleur reste un avertissement.

Le classement se lit sur le chemin `\Device\…`, que Windows ne traduit jamais : c'est le seul identifiant exploitable quelle que soit la langue du système.

## Mettre un nom sur un support qui n'est plus là

« Un disque qui portait le numéro 1 » était tout ce que le logiciel savait dire — et c'est exact : les numéros de disque sont attribués au branchement, celui-là ne désigne plus rien.

Windows, lui, retient quelle lettre de lecteur a été montée par quel matériel, et le nom lisible des supports USB déjà vus. Le logiciel recoupe désormais les deux, en lecture seule, et propose les supports amovibles qu'il connaît et qui ne sont pas montés au moment de l'analyse : *« D: (SanDisk Cruzer Blade USB Device) »*.

Trois décisions comptent plus que le code lui-même :

- **Le numéro de série du support n'est pas retenu.** C'est un identifiant matériel unique, sans aucune valeur diagnostique — même traitement que l'adresse MAC, que ce logiciel masque déjà.
- **Le rapport écrit « PISTE », jamais « identification ».** Rien ne relie une lettre de lecteur au numéro de disque qu'elle portait ce jour-là. L'annoncer autrement ferait accuser un support au hasard, ce qui est précisément le genre d'erreur que cet outil existe pour éviter.
- **« Pas pu regarder » ne se lit pas « rien trouvé ».** Cette lecture demande des droits d'administrateur ; sans eux, le rapport dit pourquoi au lieu de laisser croire que Windows ne connaît aucun support.

## Ce qui ne change pas

Aucun format de fichier n'évolue : les analyses enregistrées par la 1.6.0 restent lisibles, et la comparaison entre deux scans fonctionne d'une version à l'autre. Aucune nouvelle permission n'est demandée. La seule source de données nouvelle est une lecture de la base de registre, sans écriture.
