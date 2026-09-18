## In English

**Two of these seven fixes repair damage that 1.6.1 did itself.** That is the price of a release that changes the *structure* of its own conclusions, and it is exactly why the report produced by 1.6.1 was read line by line on the day it shipped — on a **healthy** machine again, where everything the software flags is a false positive and there is nowhere to hide.

The worst one was not subtle. The report announced that Microsoft Edge *"no longer appears among the installed programs"* — while six `msedge` processes were listed in the same report, a few sections above. Two faults stacked on top of each other: the name match broke on a single space between "Microsoft Edge" and "MicrosoftEdgeUpdate", and an absence from the installed-programs list was being presented as proof, when Windows components, Store apps and portable software never appear in that list at all. A report that contradicts itself on the page is worse than a report that says nothing.

The others follow the same line. 29 service failures were counted but never named, although each event carried the service name in its own data fields. Two services accounting for 28 of those 29 failures were declared *"scattered"*, and the report recommended `sfc` and `DISM` — Windows repair tools — for two third-party agents. A service that **fails to start** and a service that **dies unexpectedly** were being told apart by the wording of their message, when the only reliable marker is the event ID, which Windows never translates.

And a lesson worth more than any of the seven, written down in the roadmap: *changing the meaning of a shared identifier moves the problem instead of solving it, as long as you have not looked for who else relies on it.* 1.6.1 changed what the disk-error identifier designated, and the real-time monitoring alert quietly landed on the wrong card.

The notes below are in French. The full English description of the software is here: **[FaultTracePC — diagnose, monitor and repair a Windows PC](https://palisser.fr/spip.php?article31)**

| File | For whom |
|---|---|
| `FaultTracePC-1.6.2.msi` | Classic installation, or Group Policy deployment |
| `FaultTracePC-1.6.2-portable.zip` | No installation: unzip and run |

The `Source code` archives are generated automatically by GitHub: they contain the code, not the compiled software.

Set the language at install time: `msiexec /i FaultTracePC-1.6.2.msi FTPCLANG=en /qn`

**These files are not digitally signed.** Windows will show "Unknown publisher" — click *More info* then *Run anyway*. Checking the SHA-256 fingerprint at the bottom of this page is the only way to be sure the file you downloaded is the one published here.

---

Sept points, trouvés le jour même de la 1.6.1, en relisant un rapport que la 1.6.1 venait de produire sur une machine **saine**.

C'est la même méthode que la veille, et elle donne le même résultat : sur une machine en panne, on cherche si le logiciel trouve ; sur une machine saine, **tout ce qu'il signale est un faux positif**.

Avec, cette fois, une différence qui compte : **deux des sept défauts avaient été créés par la 1.6.1 elle-même.** C'est le prix d'une version qui change la structure de ses propres conclusions, et il vaut mieux le payer tout de suite.

## Un logiciel déclaré désinstallé pendant qu'il tournait

Le rapport écrivait : « `MicrosoftEdgeUpdate.exe` (27 crashs) — ce logiciel ne figure plus parmi les programmes installés ».

**Six processus `msedge` étaient listés dans le même rapport**, quelques sections plus haut.

Deux fautes cumulées. La première est technique : le rapprochement entre le nom d'un exécutable et celui d'un programme installé butait sur l'espace entre « Microsoft Edge » et « MicrosoftEdgeUpdate ». La comparaison ne retient plus que les lettres et les chiffres.

La seconde est plus grave, parce qu'elle n'aurait pas été réparée par une meilleure comparaison : **l'absence de la liste des programmes installés était présentée comme une preuve**. Cette liste ne contient ni les composants de Windows, ni les applications du Microsoft Store, ni les logiciels portables. Ne pas y figurer ne veut rien dire. Le rapport constate désormais qu'il n'a pas trouvé le programme, et s'arrête là.

Un rapport qui se contredit sur la même page est pire qu'un rapport qui ne dit rien : il apprend à ne plus le lire.

## 29 échecs de services comptés, aucun nommé

« Échecs de services Windows répétés (29) », suivi de « Consulter le détail dans la section Événements ».

Le logiciel avait les 29 événements sous la main, et chacun porte le nom du service **dans ses données** — pas dans la phrase du message, qui est traduite. Il les lit maintenant là où ils sont, et il nomme.

C'est exactement le reproche qui avait donné son thème à la 1.6.0 — *nommer la panne qu'on a sous les yeux* — revenu sur une autre règle. Un thème ne se traite pas une fois : il se repasse sur chaque règle.

## Deux services couvrant 28 échecs sur 29, déclarés « dispersés »

`GLPI Agent` 14 fois, `TmWSCSvc` 14 fois, un troisième service 1 fois.

Le seuil demandait qu'**un seul** service pèse la moitié des échecs pour qu'on puisse le désigner. Aucun des deux n'y arrivait. Le rapport concluait donc « aucun ne domine, ce qui désigne plutôt le système », et renvoyait vers `sfc` et `DISM` — **pour deux agents tiers**, dont aucun n'appartient à Windows.

La règle compte désormais combien de services il faut réunir pour couvrir les quatre cinquièmes des échecs : un ou deux, on les nomme ; trois ou plus, la dispersion est réelle et le conseil système reprend son sens.

## Un service qui ne démarre pas et un service qui meurt ne se regardent pas au même endroit

Les identifiants `7000` et `7001` disent « n'a pas pu démarrer ». Les identifiants `7031` et `7034` disent « s'est terminé de manière inattendue ». Ce sont deux pannes différentes : la première renvoie à l'inscription du service et à son fichier, la seconde à son propre journal.

La distinction se lit sur l'**identifiant** de l'événement. Jamais sur sa phrase, qui change avec la langue du système.

## Une lettre de lecteur et un nom de support, enfin rapprochés

Un événement `Ntfs 55` nommait « le volume D: ». La lecture du registre, introduite la veille, proposait deux cartes plus loin « D: (Kingston DataTraveler 3.0 USB Device) ».

Les deux moitiés de la réponse étaient dans le même rapport, sans être collées.

Elles le sont maintenant, sous trois conditions strictes : la lettre doit être citée par les événements de cette carte, le support doit être amovible, et il ne doit pas être monté au moment de l'analyse. Et le rapprochement reste annoncé comme **une piste plus serrée**, jamais comme une preuve — rien ne relie une lettre de lecteur au matériel qui la portait ce jour-là.

## Les deux défauts que la 1.6.1 avait créés

**Une phrase de comptage en double, dont la première était fausse.** La carte des 20 réinitialisations de contrôleur portait « Le journal Windows en contient davantage », qui se lit « il y en a plus de 20 ». **Rien ne l'établit.** Ce qui avait buté sur le plafond de collecte, c'était la *catégorie* entière ; rien ne dit que la coupe a touché ces 20-là plutôt que les 479 autres événements. La phrase suivante disait déjà la chose correcte. Les deux n'en font plus qu'une, qui ne parle que du total de la catégorie.

**Une alerte collée à la mauvaise carte.** L'alerte de la surveillance temps réel citait « le volume D: » et se retrouvait sur la carte du **port de contrôleur SATA**. La cause n'était pas la fusion des doublons : la 1.6.1 avait changé le *sens* de l'identifiant partagé par ces cartes — il désignait la carte des erreurs disque, il désigne désormais la première nature présente — sans que personne cherche qui d'autre s'appuyait dessus.

C'est la leçon à retenir, et elle est inscrite dans la feuille de route : **changer le sens d'un identifiant partagé déplace le problème au lieu de le résoudre, tant qu'on n'a pas cherché qui d'autre s'y appuie.**

## Ce qui ne change pas, et une chose qui s'ajoute

Aucun format de fichier n'évolue : les analyses enregistrées par la 1.6.0 et la 1.6.1 restent lisibles, et la comparaison entre deux scans fonctionne d'une version à l'autre. Aucune permission nouvelle n'est demandée, aucune source de données nouvelle n'est lue.

Un fichier s'ajoute au paquet : `System.DirectoryServices.dll`. C'est la première pierre de la console de parc prévue pour la 1.7.0. **Cette version ne l'appelle jamais** — aucune interface n'y mène, aucun code de l'application ne s'y rend. Elle n'ouvre aucune connexion, n'interroge aucun annuaire et ne demande aucun droit. Elle est mentionnée ici parce qu'elle est visible dans le paquet, et qu'un fichier qui apparaît sans explication est une question de plus à se poser.

616 tests, aucun échec.
