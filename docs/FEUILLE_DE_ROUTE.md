# FaultTracePC — feuille de route

État au 15/08/2026. Document de travail, pas un engagement.

## Où on en est

| Version | État |
|---|---|
| 1.2.2 | **publiée** — MSI + ZIP + sommes de contrôle |
| 1.2.3 | construite, 138 tests verts, poussée — **non publiée** |

---

## 1. Défauts connus, non corrigés

Trouvés en testant la 1.2.3 aujourd'hui.

| # | Défaut | Cause | Difficulté |
|---|---|---|---|
| 1 | « Disque 0 **(aucune lettre)** » alors que C: est dessus | Le chemin WMI est reconstruit à la main ; `Disk #0, Partition #0` contient une **virgule**, qui sépare les paires clé-valeur dans un chemin d'objet. Correctif : lire `__RELPATH`, comme le fait déjà `RelatedReliabilityCounters` dix lignes plus bas | faible |
| 2 | « (aucune lettre) » affiché comme un défaut | Un disque sans volume monté est normal. Sans lettres, ne rien écrire | triviale |
| 3 | « …ne l'affichera donc pas Vu entre le… » et « …disque fixe.. » | Ponctuation doublée : la méthode renvoie une phrase déjà ponctuée que l'appelant ponctue à nouveau | triviale |

## 2. Dette technique repérée en conversation

| # | Sujet | Pourquoi ça compte | Difficulté |
|---|---|---|---|
| 4 | `DateTime.TryParse` sans culture explicite (`ParkComparator.cs:241`, `TelemetryService.cs:269`) | Fonctionne aujourd'hui car les formats sont ISO. Casse le jour où une machine écrit et une autre relit | triviale |
| 5 | Analyse de la sortie de `sfc` | Sur une session non francophone, aucun motif ne correspond et le mode guidé conclut « rien trouvé ». **Faux négatif silencieux.** Détecter l'échec d'analyse, pas la langue | moyenne |
| 6 | `DISM /English` | Rend la lecture déterministe quelle que soit la session | faible |
| 7 | Canal d'alerte inadapté au parc | La bulle de notification vit dans une session utilisateur. Machine réveillée en WoL sans session : l'alerte n'a aucun destinataire. Journal d'événements Windows + point d'accès de télémétrie | moyenne |
| 8 | `Historique\` ne purge jamais | La boîte noire purge à 14 jours ; l'historique des scans, non. ~800 fichiers/an/machine, et chaque lecture énumère tout le dossier. **Attention** : la rétention devra être longue (90 jours et plus), la pente SMART en dépend | faible |
| 9 | `DiskBrief.Health` — valeur française ou neutre WMI ? | Non vérifié. Détermine si le comparateur de parc survit à un poste anglais | à vérifier |
| **30** | **Fraîcheur des données non signalée** | Remonté par un test sur une machine éteinte depuis des mois (1.1.0). Trois manques distincts : l'âge du fait le plus récent n'est jamais indiqué ; la couverture réelle non plus (« 30 jours analysés, dont 2 jours machine allumée ») ; et la comparaison n'a **aucun plancher de durée** — deux scans espacés de dix minutes produisent « Bon signe ». Les données existent déjà (dernier démarrage, durée d'allumage, horodatage des événements), elles ne sont pas exploitées | moyenne |

## 3. Tes propositions validées, non faites

| # | Sujet | Statut |
|---|---|---|
| 10 | **Triage RAW** — disque mourant / sain / jamais `chkdsk /f` | validé « ok à 100 % » |
| 11 | **Pente SMART sur N scans** plutôt que le seul écart avec le précédent | validé |
| 12 | **Localisation FR/EN**, détection par session utilisateur, sélecteur dans l'application | validé |
| 13 | `--configure-remote --generate-token` en ligne de commande | ta proposition |
| 14 | **ACL sur `remote.json`** (fichier, pas dossier — le dossier abrite aussi les rapports et le journal d'alertes) | validé |
| 15 | **Bloc winget** : section du rapport + boutons « tout mettre à jour » / choix par logiciel | validé sur le principe |
| 16 | **Hiérarchie du rapport pour un débutant** | ton observation, pas encore un plan |

## 4. Repris — et une dépendance découverte

| # | Sujet | Statut au 15/08 |
|---|---|---|
| 17 | Choix de langue à l'installation | **remplacé**, pas reporté : détection automatique par session + sélecteur dans l'application |
| 18 | **Signature de code** | **validé pour les futures versions.** Voie retenue : SignPath Foundation (gratuit, open source) |
| 19 | **Construction par GitHub Actions** | **passe de « confort » à prérequis** — voir ci-dessous |
| 20 | Segoe Fluent Icons | **à exclure** — voir §5, point 31 |

**La dépendance :** les conditions de la SignPath Foundation exigent que *« les binaires soient issus de constructions automatisées et vérifiables à partir du code source »*, en plus d'une licence approuvée OSI (MIT ✓), d'un projet activement maintenu (✓), de l'authentification multifacteur pour tous les intervenants, et d'une **politique de signature publiée** décrivant les rôles Auteurs / Relecteurs / Approbateurs.

Autrement dit : **la signature de code que tu viens de valider impose la construction automatisée.** Les points 18 et 19 ne sont pas deux sujets, c'en est un seul, dans cet ordre.

Une réserve honnête : les conditions mentionnent aussi qu'un programme exécutable doit présenter *« une certaine réputation vérifiable »*. Avec zéro téléchargement, ce critère peut encore bloquer. Les prérequis techniques se préparent dès maintenant — ils sont utiles indépendamment ; la candidature attend d'avoir quelque chose à montrer.

**Ce que la construction automatisée apporte, signature ou pas :** les quatre erreurs WiX consécutives seraient apparues sans que tu recompiles à chaque tentative ; les deux tests périmés d'aujourd'hui auraient échoué avant que tu récupères les fichiers ; et les binaires publiés seraient rattachés à un commit précis. Les dépôts publics disposent de minutes gratuites sur les exécuteurs standard — à confirmer sur ton compte, mais c'est la règle générale.

## 5. À exclure — et pourquoi

| # | Idée | Raison du refus |
|---|---|---|
| 21 | ACL refusant l'exécution aux non-administrateurs | Le manifeste `requireAdministrator` fait déjà le travail, avec un message d'erreur compréhensible. Une ACL le remplacerait par « accès refusé ». Vérifié : l'application, la CLI **et** la version portable embarquent le même manifeste — pas de porte dérobée |
| 22 | Bloquer l'installation selon la langue détectée | `SystemLanguageID` mesure la machine, `UserLanguageID` mesure l'installateur ; ni l'un ni l'autre ne mesure les utilisateurs à venir. Faux positifs **et** faux négatifs sur ton parc, et échec silencieux en GPO |
| 23 | `winget export` comme source de données | Exporte les logiciels **installés**, jamais les versions **disponibles**. Et omet ceux qu'il ne sait pas rattacher à une source |
| 24 | Découper la sortie winget sur « 2 espaces ou plus » | **Prouvé faux sur tes données** : quand une valeur remplit sa colonne, il ne reste qu'un espace. 3 paquets sur 7. Découper aux positions de l'en-tête : 7 sur 7 |
| 25 | `--include-unknown` sur la commande de **mise à jour** | Ne le passer qu'au **listage**. Le défaut de winget exclut déjà les versions inconnues — il suffit de ne pas le contredire |
| **31** | **Segoe Fluent Icons à la place des émojis** | **Cette police n'existe pas sur Windows 10** — elle n'est livrée qu'avec Windows 11 et doit être téléchargée ailleurs. Et ses icônes vivent dans la zone à usage privé d'Unicode : quand la police manque, on n'obtient pas une icône approchante mais des **carrés vides**. Les émojis, eux, s'affichent partout, avec au pire un rendu différent. Passer aux icônes vectorielles casserait donc l'affichage sur la moitié des machines visées, pour un gain esthétique. Si un jour le besoin se confirme, la seule police présente sur Windows 10 **et** 11 est `Segoe MDL2 Assets` — pas Fluent |

## 6. Idées non encore proposées

Mon avis, à discuter.

**26 — Un bouton « installer WinDbg » dans la boîte à outils.** WinDbg conditionne la précision de **toute** analyse de dump : sans lui, le code STOP est lu mais le pilote fautif reste souvent anonyme. Aujourd'hui, l'information vit dans une **infobulle**, et l'utilisateur doit taper `winget install Microsoft.WinDbg` lui-même. C'est le meilleur rapport valeur/effort de toute cette liste : une ligne de code pour améliorer chaque diagnostic futur. *Difficulté : triviale.*

**27 — `parc.json` vit dans `Documents`, donc par utilisateur, et contient les tokens de tout le parc.** Deux conséquences : aucun export/import, donc un profil Windows reconstruit efface la configuration du parc ; et sur un réseau d'établissement, un dossier Documents redirigé met ces tokens sur un partage. Le **token dérivé** (secret maître + nom de machine) supprime le problème à la racine : plus de liste à sauvegarder, la console recalcule. *Difficulté : moyenne.*

**28 — Réconcilier les comptes, partout.** L'épisode winget a montré la valeur d'une ligne qui compare ce qu'on a analysé à ce que l'outil annonce. Le principe vaut au-delà : chaque fois que le logiciel résume une source, il devrait pouvoir dire « j'en ai lu 7 sur 7 ». C'est ce qui distingue une liste vérifiée d'une liste plausible. *Difficulté : faible, à faire au cas par cas.*

**32 — Espace disque : répondre à une autre question que WinDirStat.** Les fondations existent — un bouton listant WinSxS, Windows.old, les temporaires et le cache Windows Update, plus une conclusion sous 8 % d'espace libre. Ce qui manque est ce qui distingue un diagnostic d'une cartographie : dire pour chaque poste **s'il est récupérable et comment**. WinSxS ne se supprime pas, il se nettoie par DISM ; `pagefile.sys` et `hiberfil.sys` ne se touchent jamais à la main ; les clichés instantanés pèsent plusieurs gigaoctets et **n'apparaissent dans aucun scan de fichiers**. Et un point que seul ce logiciel peut traiter : un `MEMORY.DMP` de plusieurs dizaines de gigaoctets **dont il sait s'il l'a déjà analysé**, donc s'il est devenu inutile. Enfin, un disque système saturé empêche l'écriture des dumps — le prochain plantage ne laisserait aucune trace, ce qui en fait une conclusion de diagnostic et pas une remarque de ménage. *Difficulté : moyenne.*

**33 — `actions/checkout@v5`.** Le journal d'exécution signale que la v4 tourne sur une version de Node bientôt dépréciée. Sans effet aujourd'hui. *Difficulté : triviale.*

**29 — Limiter ce que le mode simple affiche.** Ton rapport porte 8 conclusions, toutes visibles d'emblée. Un technicien lit une liste ; un débutant ne sait pas par où commencer. Piste : n'afficher que les critiques et le premier avertissement, le reste replié derrière « voir les 6 autres ». *Difficulté : faible ; la décision de ce qu'on masque est plus délicate que le code.*

---

## Découpage proposé

### Hors versions — la chaîne de construction

Point **19** (GitHub Actions), prérequis du point **18** (signature). À faire quand tu veux : ça ne modifie pas le logiciel, seulement la façon de le fabriquer. Plus tôt c'est en place, plus tôt les erreurs d'installeur et les tests périmés se signalent tout seuls.

### 1.2.4 — correctifs (petite, rapide)

Points **1, 2, 3, 4**. Éventuellement **26** (bouton WinDbg) et **8** (purge de l'historique, rétention 90 jours minimum — la pente SMART en dépend).

*Pourquoi séparé :* ce sont des corrections de ce qui est déjà publié ou prêt à l'être, sans aucune surface nouvelle. Les fondre dans la 1.3 les rendrait indisponibles pendant des semaines pour rien.

### 1.3.0 — un seul thème : le texte

Points **12** et tout ce que la localisation **impose** : codes plutôt que phrases dans le protocole de parc, données persistées en codes, rendu HTML à la demande, stockage de la langue par utilisateur, propriété MSI pour la GPO. Plus **5, 6, 9** — la lecture des sorties d'outils est le même sujet.

*Pourquoi rien d'autre :* c'est la seule version du projet qui touche **chaque chaîne** du logiciel, et elle casse la compatibilité du protocole de parc. Si une régression apparaît, il faut pouvoir l'attribuer. Y ajouter de nouveaux diagnostics, c'est renoncer à savoir ce qui a cassé.

### 1.4.0 — les fonctionnalités, écrites bilingues d'emblée

Points **10, 11, 15, 13, 14, 7, 27, 29, 30**.

*Pourquoi après :* chacune ajoute de la prose. Écrite avant la 1.3, il faut la traduire ensuite ; écrite après, elle naît dans les deux langues du premier coup.

---

## La question qui devrait décider de l'ordre

Ce découpage suppose que la localisation vient en premier. Ce n'est vrai que si rien d'autre n'a de date.

**Or la 1.3 attend un signal qui n'est pas venu** — le README anglais est en ligne depuis ce matin. Pendant ce temps, un déploiement de parc à la rentrée, lui, aurait une date.

Si tu comptes déployer sur ton établissement en septembre, alors le bloc **token + ACL + script GPO** (13, 14, 27) devient prioritaire sur la localisation, et devrait passer devant — la 1.3 deviendrait le déploiement, la 1.4 le texte.

C'est le calendrier qui doit trancher, pas les numéros de version.
