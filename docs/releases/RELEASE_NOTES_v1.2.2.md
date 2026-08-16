Correctifs. Trois défauts distincts, un même reproche : le rapport collectait des informations justes et n'en tirait pas les conséquences. Plus deux ajouts réclamés par l'usage sur un parc.

## Le verdict tient compte du matériel, pas seulement des plantages

Jusqu'ici, la conclusion affichée en tête du bloc « Évolution depuis le dernier scan » ne se calculait **qu'à partir des crashs**. Une machine n'ayant jamais planté mais dont le disque perdait des secteurs entre deux analyses recevait une carte verte titrée « Machine stable », l'avertissement étant relégué en petit dessous — c'est-à-dire précisément dans le cas où l'utilisateur n'a aucun autre signal pour se méfier.

La couleur de la carte retient désormais la pire des deux évaluations, et la dégradation la plus grave passe dans le titre. Trois signaux entrent dans la conclusion alors qu'ils en étaient absents : l'apparition de **secteurs défectueux**, l'aggravation de **l'état de santé** rapporté par le disque, et les nouvelles **erreurs matérielles WHEA** ou erreurs disque du journal Windows.

« Machine stable » n'est plus affirmé quand quelque chose le contredit, et **un problème critique qui persiste sans empirer n'est plus présenté comme une bonne nouvelle** : *« rien ne s'est aggravé depuis le dernier scan, mais rien n'est réglé non plus »*.

Les **erreurs CRC** ont leur propre message, qui met en cause la liaison — câble, connecteur, alimentation — et invite explicitement à changer le câble avant d'envisager de remplacer un disque qui peut être parfaitement sain. C'est une panne à cinq euros régulièrement traitée comme une panne à cent.

Une machine réellement saine reste verte : une amélioration de santé n'est pas une alerte, une variation de température reste une information, et un point d'usure SSD supplémentaire — le fonctionnement normal d'un SSD — ne déclenche rien.

## Les erreurs disque disent enfin de quoi elles parlent

« Erreurs disque répétées (28) » n'indiquait ni quel périphérique était en cause, ni quoi faire d'utile.

**Le périphérique est nommé.** Les événements Windows citent un chemin `\Device\…` qui n'est jamais traduit ; le rapport le reprend et dit s'il correspond à un disque inventorié, ou **pas** : *« `\Device\Harddisk1` ne correspond à AUCUN disque inventorié — support amovible, lecteur de cartes, ou disque absent au moment de l'analyse »*. C'était l'information décisive, elle était collectée, et elle n'apparaissait nulle part dans la conclusion. Sans elle, une réparation lancée sur le disque système ne pouvait rien corriger.

**Les identifiants cités sont ceux réellement observés.** Le texte annonçait « disk 153 / stornvme 129 » quels que soient les événements trouvés — une phrase toute faite qui pouvait nommer des identifiants absents, deux lignes sous le tableau qui affichait les vrais.

**Le conseil correspond au matériel présent.** Plus de « vérifier les câbles SATA » sur une machine dont le seul disque est un NVMe. Et quand les événements sont des **réinitialisations de contrôleur (ID 129)**, le rapport commence par la cause la plus fréquemment documentée — la gestion d'alimentation des liens PCI Express et AHCI — au lieu d'envoyer chercher un disque mourant qui n'existe pas.

## Le rapport renvoie vers la boîte à outils

Le mot « Outils » n'apparaissait **nulle part** dans le rapport. Il conseillait des actions sans jamais dire qu'un bouton les exécute : un lecteur ne connaissant pas déjà le logiciel n'avait aucun moyen de faire le lien.

Chaque conclusion indique désormais le bouton correspondant, avec son libellé exact. Les catégories qu'aucun outil ne traite — alimentation, matériel physique — n'affichent rien : promettre une réparation logicielle là où il faut ouvrir la machine serait le remède imaginaire que ce logiciel refuse de proposer.

## Bouton « ? » : la version installée

En haut à droite de la fenêtre. La version n'était visible nulle part dans l'interface — impossible de répondre à « tu es en quelle version ? » sans aller lire les propriétés du fichier. Elle est lue dans l'exécutable, jamais écrite à la main.

S'y ajoutent les informations qu'on demande toujours en premier quand quelque chose ne va pas : droits administrateur effectifs, état du service de surveillance, version de Windows, chemin de l'exécutable et dossier des rapports.

## Console de parc : quelle machine mettre à jour

Une colonne **Version** indique ce que chaque poste exécute, et la synthèse dit quoi faire — y compris quand c'est la console qui est en retard :

> ⚠ 2 poste(s) plus récent(s) que cette console (1.2.2) — c'est ELLE qu'il faut mettre à jour.

La comparaison est volontairement symétrique : une colonne qui ne saurait dire que « le poste est vieux » ferait mettre à jour la mauvaise machine.

Les postes en version antérieure à 1.2.2 n'annoncent pas leur version — ils apparaissent comme tels sans faire échouer l'interrogation. **L'ajout est purement additif** : aucune mise à jour de parc n'est nécessaire pour que la console fonctionne, seulement pour qu'un poste sache dire qui il est.

## Vérification

**135 tests automatisés**, dont 15 ajoutés pour ces correctifs. Le plus important reproduit le défaut d'origine — zéro plantage avant comme après, secteurs défectueux passant de 0 à 7 — et son symétrique, qui garantit qu'une machine saine reste annoncée comme telle. Quatre autres vérifient qu'on n'inquiète pas à tort.

Le cœur de la comparaison a été séparé de la lecture du dossier `Historique` : le verdict est ce que l'utilisateur lit en premier, c'était la dernière chose du projet à ne pas être vérifiable.

## Mise à jour

Le MSI remplace proprement la 1.2.0. Aucun changement de format des rapports archivés, du mode réseau ni du service.

| Fichier | Pour qui |
|---|---|
| `FaultTracePC-1.2.2.msi` | Installation classique, ou déploiement par GPO |
| `FaultTracePC-1.2.2-portable.zip` | Aucune installation : décompresser et lancer |

**L'interface et les rapports sont en français uniquement.**

## Sommes de contrôle (SHA-256)

Ces fichiers ne sont pas signés numériquement. Vérifier l'empreinte est le seul moyen de s'assurer que le fichier téléchargé est bien celui publié ici :

```powershell
Get-FileHash FaultTracePC-1.2.2.msi -Algorithm SHA256
```

```
97de10b1b8d095d5718063187e092e7a0adb8b60d8fdfd6577ce592395aafc81  FaultTracePC-1.2.2.msi
73eeee3ff584b4e83a0c29cc5066f8ac84fc5a7d47aa61084c347d21f7ed2f50  FaultTracePC-1.2.2-portable.zip
```
