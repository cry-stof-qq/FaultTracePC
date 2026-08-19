# FaultTracePC — feuille de route

État au 17/08/2026. Document de travail, pas un engagement.

## Où on en est

| Version | État |
|---|---|
| 1.2.2 | **publiée** — MSI + ZIP + sommes de contrôle |
| 1.2.3 | **publiée** — release GitHub, 4 fichiers, sommes de contrôle |
| 1.3.0 | **publiée** — release GitHub, MSI + ZIP, 250 tests verts |
| 1.3.1 | correctifs — journal des pannes, stratégie d'exécution, 262 tests verts |

**Fait en 1.3.0 :** réglage de langue de portée machine (`ProgramData\FaultTracePC\langue.txt`, propriété MSI `FTPCLANG`, `--set-machine-lang`) ; alertes préventives refabriquées à la lecture à partir de la règle et de la valeur.

**Reporté de la 1.3.0 en 1.4.0**, décidé le 17/08/2026 : donner un identifiant stable aux 32 conclusions du moteur de règles, pour que `Historique\*.json` cesse de stocker des titres en clair — la console de parc les réaffiche, et une console anglaise lit donc des titres français. C'est un contrat entre versions, pas un rangement.

**Reporté de la 1.3.0 en 1.4.0**, décidé le 17/08/2026 : persister le rapport complet (JSON à côté du HTML) pour pouvoir le régénérer dans une autre langue — ou avec un générateur plus récent. Écarté de la 1.3 parce que le déclencheur n'existe pas : `Lang.Apply` n'est appelé nulle part à l'exécution, la langue est fixée au démarrage et le sélecteur redémarre l'application. Une régénération « si la langue a changé » serait du code mort.

---

## 1. Défauts connus, non corrigés

Trouvés en testant la 1.2.3 aujourd'hui.

| # | Défaut | Cause | Difficulté |
|---|---|---|---|
| 1 | ~~« Disque 0 (aucune lettre) » alors que C: est dessus~~ | **fait en 1.2.3** : le code lit `__RELPATH` au lieu de reconstruire le chemin WMI à la main (`SystemInfoCollector.cs:292`) | — |
| 2 | ~~« (aucune lettre) » affiché comme un défaut~~ | **fait en 1.2.3** : sans lettre on n'écrit rien (`HtmlReportGenerator.cs:570`) | — |
| 3 | ~~Ponctuation doublée dans les conclusions sur les périphériques disparus~~ | **fait en 1.2.3** | — |

## 2. Dette technique repérée en conversation

| # | Sujet | Pourquoi ça compte | Difficulté |
|---|---|---|---|
| 4 | ~~`DateTime.TryParse` sans culture explicite~~ | **fait en 1.2.3** : `InvariantCulture` explicite dans `ParkComparator.cs:247` et `TelemetryService.cs:291`. Reste un cas sans conséquence, `RepairToolboxWindow.xaml.cs:54`, qui ne sert qu'à trier un affichage | — |
| 5 | ~~Analyse de la sortie de `sfc`~~ | **fait en 1.3.0** : les deux langues sont reconnues, et l'échec d'analyse est distingué du « rien trouvé » | — |
| 6 | ~~`DISM /English`~~ | **fait en 1.3.0**, uniquement là où c'est le programme qui lit — pas dans la console visible de la boîte à outils | — |
| 7 | Canal d'alerte inadapté au parc | La bulle de notification vit dans une session utilisateur. Machine réveillée en WoL sans session : l'alerte n'a aucun destinataire. Journal d'événements Windows + point d'accès de télémétrie | moyenne |
| 8 | ~~`Historique\` ne purge jamais~~ | **fait en 1.2.3** : purge au-delà de 90 jours **et** des 10 analyses les plus récentes — les deux conditions, pour qu'une machine analysée une fois par an ne perde rien (`ScanHistory.cs:85`) | — |
| 9 | ~~`DiskBrief.Health`~~ | **tranché en 1.3.0** : c'était bien une valeur française. Devenu une énumération ; les résumés écrits par la 1.2.x restent relus | — |
| **30** | **Fraîcheur des données non signalée** | Remonté par un test sur une machine éteinte depuis des mois (1.1.0). Trois manques distincts : l'âge du fait le plus récent n'est jamais indiqué ; la couverture réelle non plus (« 30 jours analysés, dont 2 jours machine allumée ») ; et la comparaison n'avait **aucun plancher de durée** — ce dernier volet est **fait en 1.2.3** : trois paliers, refus de conclure en dessous de deux heures. Les deux premiers restent ouverts. Les données existent déjà (dernier démarrage, durée d'allumage, horodatage des événements), elles ne sont pas exploitées | moyenne |

## 3. Tes propositions validées, non faites

| # | Sujet | Statut |
|---|---|---|
| 10 | **Triage RAW** — disque mourant / sain / jamais `chkdsk /f` | validé « ok à 100 % » |
| 11 | **Pente SMART sur N scans** plutôt que le seul écart avec le précédent | validé |
| 12 | **Localisation FR/EN**, détection par session utilisateur, sélecteur dans l'application | **fait en 1.3.0** |
| 13 | `--configure-remote --generate-token` en ligne de commande | ta proposition |
| 14 | **ACL sur `remote.json`** (fichier, pas dossier — le dossier abrite aussi les rapports et le journal d'alertes) | validé |
| 15 | **Bloc winget** : section du rapport + boutons « tout mettre à jour » / choix par logiciel | validé sur le principe |
| 16 | **Hiérarchie du rapport pour un débutant** | ton observation, pas encore un plan |

## 4. Repris — et une dépendance découverte

| # | Sujet | Statut au 15/08 |
|---|---|---|
| 17 | Choix de langue à l'installation | **remplacé**, pas reporté : détection automatique par session + sélecteur dans l'application — **fait en 1.3.0**. L'installateur lui-même reste français |
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

**26 — ~~Un bouton « installer WinDbg » dans la boîte à outils~~. FAIT EN 1.2.3**, constaté le 19/08/2026. Le bouton existe (`L.ToolWinDbg`, `RepairToolboxWindow.xaml:132`), son action aussi (`case "windbg"`, installation par `winget install --id Microsoft.WinDbg`), et `MainWindow.ProposerWinDbg` le propose après une analyse ayant trouvé des fichiers d'incident sans pouvoir les exploiter. La ligne est restée ouverte trois versions, et je l'ai recommandée deux fois comme « le meilleur rapport valeur/effort de la liste » alors qu'elle était faite.

**27 — `parc.json` vit dans `Documents`, donc par utilisateur, et contient les tokens de tout le parc.** Deux conséquences : aucun export/import, donc un profil Windows reconstruit efface la configuration du parc ; et sur un réseau d'établissement, un dossier Documents redirigé met ces tokens sur un partage. Le **token dérivé** (secret maître + nom de machine) supprime le problème à la racine : plus de liste à sauvegarder, la console recalcule. *Difficulté : moyenne.*

**28 — Réconcilier les comptes, partout.** L'épisode winget a montré la valeur d'une ligne qui compare ce qu'on a analysé à ce que l'outil annonce. Le principe vaut au-delà : chaque fois que le logiciel résume une source, il devrait pouvoir dire « j'en ai lu 7 sur 7 ». C'est ce qui distingue une liste vérifiée d'une liste plausible. *Difficulté : faible, à faire au cas par cas.*

**32 — Espace disque : répondre à une autre question que WinDirStat.** Les fondations existent — un bouton listant WinSxS, Windows.old, les temporaires et le cache Windows Update, plus une conclusion sous 8 % d'espace libre. Ce qui manque est ce qui distingue un diagnostic d'une cartographie : dire pour chaque poste **s'il est récupérable et comment**. WinSxS ne se supprime pas, il se nettoie par DISM ; `pagefile.sys` et `hiberfil.sys` ne se touchent jamais à la main ; les clichés instantanés pèsent plusieurs gigaoctets et **n'apparaissent dans aucun scan de fichiers**. Et un point que seul ce logiciel peut traiter : un `MEMORY.DMP` de plusieurs dizaines de gigaoctets **dont il sait s'il l'a déjà analysé**, donc s'il est devenu inutile. Enfin, un disque système saturé empêche l'écriture des dumps — le prochain plantage ne laisserait aucune trace, ce qui en fait une conclusion de diagnostic et pas une remarque de ménage. *Difficulté : moyenne.*

**33 — `actions/checkout@v5` et `actions/upload-artifact@v6`.** Calendrier GitHub vérifié le 17/08/2026 : Node 20 en fin de vie en avril 2026, runners passés à Node 24 par défaut le 16 juin 2026, retrait de Node 20 à l'automne 2026. Les actions en v4 tournent donc **déjà** sur Node 24 et les workflows sont verts : ce n'est pas une panne annoncée, c'est de l'hygiène. La raison de le faire tôt est `publication.yml`, lancé une fois par version — une casse s'y découvrirait au pire moment. Trois `checkout@v4` et deux `upload-artifact@v4` à passer en v5 et v6 (pas plus haut : v6 de checkout et v7 d'upload-artifact changent des comportements inutiles ici). `setup-dotnet@v5` est déjà en Node 24. Vérifier ensuite par un essai à blanc de la publication. *Difficulté : triviale.*

**34 — La réparation ne démarrait pas sous stratégie de groupe. CAUSE TROUVÉE, CORRIGÉ EN 1.3.1.** Retour d'usager du 17/08/2026, Windows 11 23H2 : une console s'ouvrait et se refermait aussitôt, sans message. Ce n'était pas l'assistant guidé mais le bouton **« Lancer la réparation »** de la fenêtre principale — une capture d'écran l'a établi.

`BtnRepair_Click` lançait `powershell.exe -NoProfile -ExecutionPolicy Bypass -File <script.ps1>` avec `UseShellExecute = true`. Or `-ExecutionPolicy Bypass` ne fixe que la portée **Process**, la plus faible : une stratégie de groupe (`MachinePolicy`, `UserPolicy`) prime sur elle. Sur un poste où l'administration a fixé `Restricted` ou `AllSigned`, PowerShell refuse le fichier avant sa première ligne, la console se referme, et — `Process.Start` ayant réussi — aucun `catch` ne se déclenche. Preuve interne : le script généré se termine par « Appuyer sur Entrée pour fermer » ; s'il ne s'affiche pas, la première ligne n'a jamais été lue.

Corrigé en 1.3.1 : contrôle préalable de la stratégie (`PowerShellPolicy`, qui ne regarde que les deux portées de groupe — `LocalMachine=Restricted` est le cas par défaut d'une machine saine et ne doit rien bloquer), message nommant la portée et la valeur, `-NoExit` sur le lancement et dans le `.bat` généré. **La stratégie n'est pas contournée**, décision validée : un outil qui désobéit à la stratégie du parc perd le droit d'y être déployé.

Trois manques comblés au passage, qui valaient indépendamment de cette cause : gestionnaire d'exception global dans les trois exécutables, journal `%ProgramData%\FaultTracePC\erreurs.log`, et message d'échec qui laisse la fenêtre ouverte.

**34 bis — Ce qui reste.** Dans l'assistant guidé, `RunHiddenAsync` récupère `p.ExitCode` mais presque tous les appelants l'ignorent (`var (_, output) = …`) : une commande qui échoue passe inaperçue. À ajouter : journaliser le code de sortie non nul sans crier au loup (`sfc` et `DISM` en renvoient légitimement), et nommer le cas « sortie vide **et** code non nul », signature d'un interpréteur bloqué. Priorité retombée : l'assistant lance ses commandes en `-Command` en ligne, qui **n'est pas** soumis à la stratégie d'exécution — c'est pourquoi lui fonctionnait. *Difficulté : faible.*

**35 — Moitié anglaise écrasée dans `GuidedRepairWindow`. CORRIGÉ EN 1.3.1.** `Lang.T($"Une réparation est déjà en cours :\n\n    {busy}\n\n", $"A repair is already running:")` — la version anglaise perd le nom de l'outil bloquant et les sauts de ligne. Le test de ratio ne l'attrape pas : 24 caractères contre 40 passent son seuil. À corriger, et à faire suivre d'une réflexion sur le seuil. *Difficulté : triviale.*

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

### 1.4.0 — décidée le 18/08/2026

**Thème unique : ce que l'utilisateur lit.** Points **16, 29, 30, 26**.

Le critère qui a tranché n'est pas la valeur des points mais **l'interruptibilité**. Un déploiement dans l'établissement est possible à la rentrée et la date n'appartient pas à l'auteur : le thème devait pouvoir s'arrêter net sans rien laisser à moitié construit. Ces quatre points sont indépendants les uns des autres — on s'arrête où on en est.

Ordre proposé, du moins cher au plus délicat :

1. ~~**26 — bouton « installer WinDbg »**~~ — **déjà fait en 1.2.3**, constaté le 19/08/2026 en ouvrant le fichier. Retiré de la 1.4.
2. **30 — fraîcheur des données.** L'âge du fait le plus récent, et la couverture réelle (« 30 jours analysés, dont 2 jours machine allumée »). Même famille de défaut que la console qui se refermait en silence : *le logiciel affirme plus qu'il ne sait*. Le plancher de comparaison, troisième volet de ce point, est déjà fait en 1.2.3.
3. **29 — limiter ce que le mode simple affiche.** Critiques et premier avertissement ; le reste replié. La décision de ce qu'on masque est plus délicate que le code.
4. **16 — hiérarchie du rapport pour un débutant.** Le plus vaste, et le seul sans plan écrit. À prendre en dernier, avec ce que les trois précédents auront appris.

**Ce qu'on s'interdit d'y ajouter :** de nouveaux diagnostics. Le triage RAW (10), la pente SMART (11), le bloc winget (15) et l'espace disque (32) restent pour plus tard. Un logiciel qui vient de découvrir qu'il ne savait pas signaler ses propres pannes n'a pas besoin de surface supplémentaire.

### Lot 0 de la 1.4 — la rupture de format, pendant qu'elle est gratuite

**Décidé le 18/08/2026, sur proposition de l'auteur.** Le parc installé se compte aujourd'hui en quelques machines, toutes connues. C'est le seul moment où une rupture de compatibilité ne coûte rien, et cette fenêtre se referme le jour du premier déploiement réel.

Code de compatibilité à retirer, recensé — puis **révisé à l'examen du code le 19/08/2026** : sur les trois candidats, **un seul en était vraiment**.

**Retiré : `DiskHealth.Parse`.** La lecture tolérante des mots français de la 1.2.x (`sain`, `dégradé`, `défaillant`) est devenue du code mort dès que les résumés ont porté un tampon de format — le fichier qui les contient est refusé en amont. Les garder aurait été pire qu'inutile : deux mécanismes pour le même problème, et le plus faible (la reconnaissance à l'allure, qui laisse passer en silence) masquant le plus sûr (le tampon, qui refuse franchement).

**Gardé : le renoncement d'`AlertCatalog`.** Ce n'était pas du code de compatibilité, contrairement à ce que son commentaire laissait croire. Il refuse de refabriquer la phrase d'une alerte quand l'extrait du message de Windows manque — or cet extrait peut manquer **aujourd'hui encore**, si Windows n'a rien écrit d'exploitable. Le retirer ferait écrire une phrase amputée du fait qu'elle rapporte. C'est une garde permanente, pas une dette.

**Gardé : le double format de `ParkProtocol`.** L'argument écrit dans son propre commentaire tient toujours, et il tient même davantage maintenant : « un parc ne se met pas à jour en un jour, et une console qui refuse de parler aux postes d'hier est inutilisable le jour du déploiement ». Un poste sans code envoie sa phrase, la console l'affiche telle quelle — c'est une dégradation gracieuse qui coûte un test de nullité, pas une compatibilité coûteuse.

*Leçon à retenir de cet écart : « supprimer ce qui ne sert plus » se décide fichier par fichier, dans le code, pas depuis une liste écrite de mémoire.*

**Ce que ce lot ne fait PAS, et c'est une limite ferme : il n'efface aucune donnée de l'utilisateur.** Refuser de relire un fichier est un choix technique ; le supprimer en silence contredirait un logiciel qui a ajouté une section « Entretien effectué » en 1.2.3 précisément pour ne jamais effacer sans le dire. Les fichiers restent sur le disque, et le rapport annonce ce qu'il n'a pas relu, avec le nombre — « 3 analyses antérieures à la 1.4 ne sont pas relues, format différent ; elles restent dans le dossier Historique ».

**Et l'ajout qui donne son sens au lot : estampiller les formats.** Si la compatibilité coûte cher aujourd'hui, c'est que les fichiers persistés — résumés d'historique, `alerts.json`, réponses du protocole de parc — **ne portent aucun numéro de version de format**. Chaque fichier écrit par la 1.4 en portera un, et toute lecture commencera par le vérifier. Sans cela, on refera ce débat à la 1.6, avec un parc déployé et plus aucune fenêtre pour le trancher.

### Bloc parc — armé, à déclencher sur un mot

Points **13, 14, 27, 7**. Non planifié, mais entièrement décidé : le jour où un déploiement d'établissement obtient une date, ce bloc passe devant la 1.4 sans rien réétudier.

1. **27 — token dérivé** (secret maître + nom de machine). Supprime le problème à la racine : plus de liste de jetons à sauvegarder, la console recalcule. Règle du même coup le fait que `parc.json` vit dans `Documents`, donc par utilisateur, et se perd avec un profil reconstruit.
2. **14 — ACL sur `remote.json`** (le fichier, pas le dossier, qui abrite aussi les rapports).
3. **13 — `--configure-remote --generate-token`** en ligne de commande, sans quoi rien de tout cela ne se déploie par GPO.
4. **7 — canal d'alerte adapté au parc.** La bulle de notification vit dans une session utilisateur : une machine réveillée en WoL sans session ouverte n'a aucun destinataire. Journal d'événements Windows plus point d'accès de télémétrie.

---

## La question qui devrait décider de l'ordre

Ce découpage suppose que la localisation vient en premier. Ce n'est vrai que si rien d'autre n'a de date.

**Or la 1.3 attend un signal qui n'est pas venu** — le README anglais est en ligne depuis ce matin. Pendant ce temps, un déploiement de parc à la rentrée, lui, aurait une date.

Si tu comptes déployer sur ton établissement en septembre, alors le bloc **token + ACL + script GPO** (13, 14, 27) devient prioritaire sur la localisation, et devrait passer devant — la 1.3 deviendrait le déploiement, la 1.4 le texte.

C'est le calendrier qui doit trancher, pas les numéros de version.
