Suite de la 1.2.2. La conclusion sur les erreurs disque nommait enfin un périphérique — mais sous une forme (`\Device\Harddisk1`) qui ne parle à personne. Cette version la rend lisible, ajoute l'outil qui manquait pour agir, et corrige trois endroits où le logiciel affirmait plus qu'il ne savait.

## Un disque se désigne comme l'utilisateur le voit

Le rapport identifie désormais chaque disque par les trois désignations qui permettent de le reconnaître : **son numéro dans le Gestionnaire de disques, ses lettres de lecteur, et son modèle**.

> Disque 0 (C:) — RPEYJ1T24MML1AWX

Les lettres sont obtenues en remontant la chaîne d'associations WMI du disque physique jusqu'aux volumes montés. Un disque sans volume monté — neuf, non partitionné, ou branché en lecture par un technicien — s'affiche simplement sans lettres, sans mention particulière : c'est un état normal, pas un défaut.

## Un numéro de disque n'est pas un identifiant

C'est le piège que cette version évite, et il n'est pas théorique.

Les numéros de disque sont attribués **à l'énumération**, au démarrage ou au branchement. Une clé USB branchée aujourd'hui est « Disque 1 » ; débranchée, ce numéro ne désigne plus rien ; rebranchée après un autre support, elle peut devenir « Disque 2 ».

Écrire platement « Disque 1 » pour un périphérique disparu enverrait donc l'utilisateur ouvrir le Gestionnaire de disques, n'y rien trouver, et conclure — à raison — que le rapport se trompe.

Le rapport distingue donc deux situations. **Le périphérique est là** : il est nommé complètement, vérifiable d'un coup d'œil. **Il n'y est plus** : aucun numéro n'est avancé comme s'il était consultable ; à la place, ce qui reste vrai et exploitable — **quand il a été vu** :

> un disque qui portait le numéro 1 au moment des faits, ABSENT aujourd'hui de la machine — le Gestionnaire de disques ne l'affichera donc pas. Vu entre le 21/07/2026 à 08:19 et le 10/08/2026 à 11:38. Le compteur de rattachement prend 2 valeurs différentes : c'est un support qui a été branché puis débranché à plusieurs reprises, pas un disque fixe.

La date est la seule information qui permette de se rappeler ce qui était branché ce jour-là. Le nombre de rattachements distincts (`\DR1`, `\DR2`…) distingue un support amovible d'un disque fixe : un disque interne garde la même instance.

## Ne plus alarmer un technicien pour le disque qu'il vient de réparer

Quand **toutes** les erreurs se rapportent à des disques qui ne sont plus connectés, la conclusion passe en simple information et le dit franchement :

> Aucun disque actuellement monté sur cette machine n'est mis en cause : ces erreurs concernent uniquement des supports qui ne sont plus connectés.

La recommandation devient une phrase au lieu d'une liste d'actions inutiles : *« Rien à réparer sur cette machine »*, avec l'invitation à examiner le support concerné une fois rebranché.

Un **port de contrôleur** (`\Device\RaidPortN`) ne bénéficie pas de cet allègement : celui-là appartient bien à la machine, et reste un avertissement.

## « Trop récent pour conclure »

Deux analyses espacées de dix minutes affichaient « Bon signe — à confirmer sur la durée ». C'est exact et vide de sens : la machine n'a rien eu le temps de faire entre les deux. Un utilisateur en a conclu, à tort, que quelque chose s'était amélioré.

La comparaison a désormais **trois paliers au lieu de deux**. En dessous de deux heures, elle refuse de conclure et le dit. Entre deux heures et une semaine, elle encourage sans affirmer. Au-delà, elle conclut.

## Deux boutons dans la boîte à outils

**🐞 Installer WinDbg.** Sans les outils de débogage de Microsoft, le code d'arrêt est lu mais le pilote fautif reste souvent anonyme — c'est la différence entre « la machine a planté » et « c'est ce pilote-là ». L'information était déjà dans le rapport, mais l'utilisateur devait recopier une commande à la main.

La proposition apparaît maintenant **là où le manque se constate** : après une analyse ayant trouvé des fichiers d'incident sans pouvoir les exploiter, l'application demande si elle doit ouvrir la boîte à outils. Et la conclusion du rapport renvoie vers le même bouton.

Il n'installe rien sans clic, et annonce clairement l'échec quand winget est absent ou que ses sources sont bloquées par stratégie — cas fréquent en établissement — en indiquant alors le repli par le SDK Windows, qui installe pour toute la machine.

**🔌 Alimentation des liens.** Affiche les réglages d'alimentation PCI Express et disque du schéma actif, puis ouvre le panneau pour les modifier. C'est la cause la plus fréquemment documentée des événements « réinitialisation au périphérique » (`storahci 129`). Il **n'écrit aucun réglage** : Microsoft documente la syntaxe de `powercfg` mais précise que les alias de sous-groupes varient d'un système à l'autre — appliquer un changement à partir d'un alias non garanti ne produirait aucune erreur visible, seulement un réglage inchangé, et l'utilisateur croirait son problème corrigé.

## Entretien de l'historique

Les résumés de scan s'accumulaient sans limite. Ils sont désormais purgés — mais seulement quand **les deux** conditions sont réunies : plus de 90 jours **et** au-delà des 10 plus récents.

Une seule des deux ne suffit jamais. Une machine analysée une fois par an garderait sinon zéro résumé, et perdrait avec eux la réponse à « est-ce que c'est réglé ? », qui est la raison d'être de cet historique.

Et la suppression **s'annonce**, dans une section « Entretien effectué » distincte des limitations de l'analyse : effacer des données de l'utilisateur en silence détonnerait avec un logiciel qui ne fait rien d'irréversible sans le dire.

## Corrections

**Les lettres de lecteur s'affichent enfin.** Le chemin d'objet WMI était reconstruit à la main ; or `Win32_DiskPartition.DeviceID` vaut « Disk #0, Partition #0 » — avec une **virgule**, qui sépare les paires clé-valeur dans un chemin WMI. La requête échouait donc en silence. Le code lit maintenant `__RELPATH`, le chemin que WMI produit lui-même correctement échappé.

**Les dates persistées se relisent en culture invariante.** Deux lectures — le comparateur de parc et le journal de la boîte noire — dépendaient des paramètres régionaux. Elles fonctionnaient tant que la machine qui écrit et celle qui lit partagent les mêmes ; le jour où ce n'est plus vrai, un poste disparaissait du comparateur sans un mot.

**Ponctuation** des conclusions sur les périphériques disparus.

## Vérification

**143 tests**, dont cinq nouveaux : deux pour le plancher de comparaison — celui qui refuse de conclure, et son symétrique qui garantit qu'au-delà rien ne change — et trois pour la règle de purge, dont celui qui vérifie qu'une machine analysée une fois par an ne perd rien.

La règle de purge a été séparée de l'accès disque pour être vérifiable, comme l'avait été le calcul du verdict en 1.2.2.

## Mise à jour

Le MSI remplace proprement les versions précédentes. Aucun changement de protocole, de format d'archive ni de service : **rien à faire côté parc**.

| Fichier | Pour qui |
|---|---|
| `FaultTracePC-1.2.3.msi` | Installation classique, ou déploiement par GPO |
| `FaultTracePC-1.2.3-portable.zip` | Aucune installation : décompresser et lancer |

**L'interface et les rapports sont en français uniquement.**

## Sommes de contrôle (SHA-256)

```
dcc0a7759af9bafb341b5fa3f356d07cae314baed1e99b64f85bf3caf0d17526  FaultTracePC-1.2.3.msi
b38443bf623b7f84e28a1109f68beccd146a0da71bd3825f5ce608948d89fa22  FaultTracePC-1.2.3-portable.zip
```
