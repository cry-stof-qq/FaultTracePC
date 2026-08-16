FaultTracePC trouve la cause d'une panne ou d'un écran bleu sous Windows 10 et 11, l'explique en français clair, et propose la réparation adaptée. Aucun prérequis à installer, aucune donnée envoyée nulle part.

Cette version répond à une question que la 1.1 laissait en suspens : **« et maintenant, je fais quoi ? »**

## Nouveautés de la 1.2

**Chaque pilote a un nom, un propriétaire et une action.** Dire « le pilote fautif est `nvlddmkm.sys` » ne sert à rien à quelqu'un qui ne sait pas ce qu'est un pilote. Une base de **59 pilotes documentés** relie désormais chaque fichier `.sys` au logiciel ou au matériel qui l'installe, et au correctif éprouvé. Les pilotes absents de la base sont rattachés à leur **famille de plateforme** — AMD, Intel, Realtek, Qualcomm, VirtualBox, Fortinet, constructeurs OEM — lorsque le nom **et** l'éditeur inscrit dans le fichier concordent. Le rapport distingue les deux niveaux : une correspondance nominative donne le correctif précis, une identification par famille un conseil générique mais juste.

Une catégorie compte plus que les autres : les **victimes du noyau**. `ntoskrnl.exe`, `fltmgr.sys`, `wdf01000.sys`, `dxgkrnl.sys` apparaissent en tête des analyses de dump, et un débutant en conclut naturellement qu'ils sont coupables. La base dit explicitement qu'ils constatent l'erreur au lieu de la provoquer, et vers où chercher. `tm.sys` y figure avec un avertissement en toutes lettres : son nom évoque Trend Micro, c'est le gestionnaire de transactions du noyau Windows, et le supprimer rend le système inutilisable.

**Le mode « je ne sais pas ce que j'ai ».** Un bouton unique, pensé pour qui n'a pas les moyens d'arbitrer une question technique. Il crée un point de restauration, examine la machine, applique **seul** les réparations qui ne peuvent rien casser — vérification des fichiers système, réparation de l'image Windows, contrôle du disque en lecture seule, fichiers temporaires — puis revérifie et conclut **en une phrase**.

Tout ce qui redémarre, installe ou désinstalle est **proposé à la fin, une action à la fois, avec la raison pour laquelle l'assistant ne l'a pas prise à votre place**.

La réparation de l'image Windows n'est lancée que si un contrôle en lecture seule a réellement détecté une corruption : sur un système sain, vingt minutes sont économisées et le journal le dit. Si aucun point de restauration ne peut être créé — protection du système désactivée, cas fréquent en entreprise et sur beaucoup de configurations d'usine — l'assistant propose de l'activer, et à défaut continue en **mode réduit** : il s'interdit alors toute action modifiant les fichiers système, plutôt que de pratiquer un irréversible discret.

**Températures dans la durée.** Une pointe à 95 °C pendant dix secondes est normale ; une heure cumulée au-dessus de 90 °C use le matériel et provoque des arrêts de protection que rien, dans les journaux Windows, ne relie spontanément à la chaleur. Le rapport indique désormais le **temps cumulé** passé au-dessus des seuils, par capteur, avec les épisodes continus les plus longs et leur pointe.

Deux précautions rendent ce chiffre crédible : un intervalle n'est compté que si **les deux relevés** qui l'encadrent dépassent le seuil — on sous-estime plutôt que de gonfler une valeur qui sert à alarmer — et les périodes machine éteinte ou service arrêté sont exclues. Les seuils restent ceux, configurables, du service d'alerte.

**Comparateur de parc.** Ce qu'aucun diagnostic individuel ne peut voir. Sur une machine isolée, un pilote de 2019 est « un suspect potentiel ». Quand six postes sur douze portent exactement le même, c'est une image de déploiement à corriger — une fois, pour tout le parc.

Le comparateur relève ce qui est **commun** (pilote ancien partagé, même code d'arrêt, même modèle de disque qui se dégrade), ce qui **diverge** (le même pilote en plusieurs versions : les postes en retard sont nommés) et ce qui est **isolé** (un poste qui concentre bien plus de problèmes que la moyenne, et relève d'un traitement individuel). En dessous de deux postes, il ne produit rien et le dit.

**Export PDF, à la demande.** Un bouton crée un PDF du rapport, en s'appuyant sur le navigateur déjà présent sur la machine — aucune dépendance ajoutée. Le PDF contient le rapport **complet**, détails techniques inclus, là où l'écran masque ces sections par défaut : un document transmis à un réparateur ou joint à un ticket ne doit pas être amputé sans que son destinataire le sache. Aucun PDF n'est généré automatiquement.

**Divers.** Un bandeau en tête de rapport indique par où commencer quand il y a quelque chose à traiter — et ne s'affiche pas quand la machine est saine. Les réparations qui modifient le système ne peuvent plus se lancer en parallèle : sfc et DISM se disputent le magasin de composants, et une seule action modifiante tourne désormais à la fois, avec proposition de basculer vers la fenêtre en cours. L'installeur propose enfin le **raccourci sur le Bureau** et le **lancement immédiat**, cochés par défaut.

**120 tests automatisés** couvrent l'analyse des dumps, les règles de diagnostic, la base de pilotes, le décodage SMART NVMe, le calcul thermique, le comparateur de parc et la sécurité du mode réseau.

## Mise à jour depuis la 1.1

Le MSI remplace proprement la version précédente. Sur un parc en mode réseau, **les postes clients doivent être mis à jour** pour que le comparateur les voie : il s'appuie sur un nouveau point d'accès, et les clients en 1.1 sortent simplement de la comparaison sans faire échouer le rapport. Chaque poste doit également avoir été analysé au moins une fois pour avoir un résumé à transmettre.

## Que télécharger

| Fichier | Pour qui |
|---|---|
| `FaultTracePC-1.2.0.msi` | Installation classique, ou déploiement par GPO sur un parc |
| `FaultTracePC-1.2.0-portable.zip` | Aucune installation : décompresser et lancer `FaultTracePC.exe`, y compris depuis une clé USB |

Les deux embarquent le runtime .NET : **rien à installer au préalable**. Le logiciel demande les droits administrateur, indispensables pour lire les fichiers dump et les journaux système complets.

## À savoir avant d'installer

**Ces fichiers ne sont pas signés numériquement.** Au premier lancement, Windows SmartScreen affichera « Éditeur inconnu » : c'est normal pour un logiciel gratuit sans certificat de signature de code. Cliquez sur « Informations complémentaires » puis « Exécuter quand même ».

Certains antivirus peuvent réagir, parce que FaultTracePC fait précisément ce que fait un outil de diagnostic : lire les dumps mémoire, interroger le matériel à bas niveau, et — si vous activez le mode parc — écouter sur un port du réseau local. **Ne désactivez pas votre antivirus pour autant** : le code source est intégralement lisible ici, et vous pouvez recompiler vous-même avec `dotnet build`.

## Limites, dites honnêtement

L'analyse symbolique qui **nomme le pilote fautif** demande WinDbg (`winget install Microsoft.WinDbg`). Sans lui, le code STOP est lu mais le coupable reste plus vague.

Les températures processeur ne sont pas exposées par toutes les machines. Les compteurs SMART détaillés dépendent du contrôleur : beaucoup de contrôleurs RAID n'en transmettent aucun. Dans ce cas, le rapport le dit clairement plutôt que d'afficher un tableau vide.

Et surtout : une coupure brutale sans écran bleu, ou des erreurs matérielles répétées, **ne se réparent pas par logiciel**. Le rapport l'écrit noir sur blanc et oriente vers les vérifications physiques plutôt que de proposer un remède imaginaire.

**L'interface et les rapports sont en français uniquement.**

## Licence

MIT — utilisation libre, y compris en entreprise et en établissement scolaire, sans aucune garantie.
