# FaultTracePC — feuille de route

État au 05/09/2026. Document de travail, pas un engagement.

## Où on en est

| Version | État |
|---|---|
| 1.2.2 | **publiée** — MSI + ZIP + sommes de contrôle |
| 1.2.3 | **publiée** — release GitHub, 4 fichiers, sommes de contrôle |
| 1.3.0 | **publiée** — release GitHub, MSI + ZIP, 250 tests verts |
| 1.3.1 | **publiée** — journal des pannes, stratégie d'exécution, 262 tests verts |
| 1.4.0 | **publiée** — ce que l'utilisateur lit : fraîcheur, déduplication, repli, tampon de format |
| 1.4.1 | **publiée** — apostrophe typographique dans le script de réparation (point 37) |
| 1.5.0 | **publiée** — thème unique **le parc** — secret maître et jeton dérivé (13, 14, 27), plus le point 36 et le refus des options inconnues |
| 1.5.1 | correctif de déploiement — le paquet sait se remplacer lui-même, pare-feu posé par la ligne de commande, procédure écrite |
| 1.5.2 | **publiée** — deux défauts constatés, sans nouvelle surface : la langue d'un rapport distant (point 45) et le lanceur `.bat` du script de réparation (point 36, moitié restante). 428 tests verts |
| 1.6.0 | **publiée** le 18/09/2026 — thème **un rapport doit nommer la panne qu'il a sous les yeux** — seize points, 48 à 63, livrés les 17 et 18/09/2026 |
| 1.6.1 | **publiée** le 18/09/2026 — correctifs constatés sur deux rapports 1.6.0 réels, plus deux ajouts demandés dans la foulée — points 65 à 71, 513 tests verts |
| 1.6.2 | **publiée** le 18/09/2026 — sept correctifs constatés sur un rapport 1.6.1 réel, dont **deux créés par la 1.6.1 elle-même** — points 72 à 78, 616 tests verts |
| 1.7.0 | prévue — thème **le parc entre dans le logiciel** — points 64 (déploiement, quatre lots) ✔ écrit mais non éprouvé, 43 (archivage des alertes) ✔ et 46 (boîte noire distante) ✔ |
| 1.8.0 | prévue — thème **mettre à jour sans surprise** — point 15, le bloc winget : voir le plan arrêté le 19/09/2026 |

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
| 7 | ~~Canal d'alerte inadapté au parc~~ | **fermé le 30/08/2026** — la justification était fausse (voir point 42) et l'auteur ne veut pas d'un canal de plus | — |
| 8 | ~~`Historique\` ne purge jamais~~ | **fait en 1.2.3** : purge au-delà de 90 jours **et** des 10 analyses les plus récentes — les deux conditions, pour qu'une machine analysée une fois par an ne perde rien (`ScanHistory.cs:85`) | — |
| 9 | ~~`DiskBrief.Health`~~ | **tranché en 1.3.0** : c'était bien une valeur française. Devenu une énumération ; les résumés écrits par la 1.2.x restent relus | — |
| **30** | **Fraîcheur des données non signalée** | Remonté par un test sur une machine éteinte depuis des mois (1.1.0). Trois manques distincts : l'âge du fait le plus récent n'est jamais indiqué ; la couverture réelle non plus (« 30 jours analysés, dont 2 jours machine allumée ») ; et la comparaison n'avait **aucun plancher de durée** — ce dernier volet est **fait en 1.2.3** : trois paliers, refus de conclure en dessous de deux heures. Les deux premiers restent ouverts. Les données existent déjà (dernier démarrage, durée d'allumage, horodatage des événements), elles ne sont pas exploitées | moyenne |

## 3. Tes propositions validées, non faites

| # | Sujet | Statut |
|---|---|---|
| 10 | **Triage RAW** — disque mourant / sain / jamais `chkdsk /f` | validé « ok à 100 % » |
| 11 | **Pente SMART sur N scans** plutôt que le seul écart avec le précédent | validé |
| 12 | **Localisation FR/EN**, détection par session utilisateur, sélecteur dans l'application | **fait en 1.3.0** |
| 13 | ~~`--configure-remote --generate-token` en ligne de commande~~ | **fait** — `--generate-master-secret` et `--configure-remote --master-secret <valeur\|->`, secret lisible sur l'entrée standard, fichier relu après écriture, ni secret ni jeton affichés (constaté dans `Cli/Program.cs` le 29/08/2026) |
| 14 | ~~**ACL sur `remote.json`**~~ | **fait** — `FileProtection` : héritage coupé, accès réduit à SYSTEM et Administrateurs par SID, échec journalisé dans `erreurs.log` ; 4 tests posent et relisent l'ACL réelle |
| 15 | **Bloc winget** : section du rapport + boutons « tout mettre à jour » / choix par logiciel | validé — **planifié 1.8.0**, plan arrêté le 19/09/2026, plus bas |
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

**27 — ~~`parc.json` vit dans `Documents`, donc par utilisateur, et contient les tokens de tout le parc~~. FAIT.** Deux conséquences : aucun export/import, donc un profil Windows reconstruit efface la configuration du parc ; et sur un réseau d'établissement, un dossier Documents redirigé met ces tokens sur un partage. Le **token dérivé** (secret maître + nom de machine) supprime le problème à la racine : plus de liste à sauvegarder, la console recalcule. *Difficulté : moyenne.*

**28 — Réconcilier les comptes, partout.** L'épisode winget a montré la valeur d'une ligne qui compare ce qu'on a analysé à ce que l'outil annonce. Le principe vaut au-delà : chaque fois que le logiciel résume une source, il devrait pouvoir dire « j'en ai lu 7 sur 7 ». C'est ce qui distingue une liste vérifiée d'une liste plausible. *Difficulté : faible, à faire au cas par cas.*

**32 — Espace disque : répondre à une autre question que WinDirStat.** Les fondations existent — un bouton listant WinSxS, Windows.old, les temporaires et le cache Windows Update, plus une conclusion sous 8 % d'espace libre. Ce qui manque est ce qui distingue un diagnostic d'une cartographie : dire pour chaque poste **s'il est récupérable et comment**. WinSxS ne se supprime pas, il se nettoie par DISM ; `pagefile.sys` et `hiberfil.sys` ne se touchent jamais à la main ; les clichés instantanés pèsent plusieurs gigaoctets et **n'apparaissent dans aucun scan de fichiers**. Et un point que seul ce logiciel peut traiter : un `MEMORY.DMP` de plusieurs dizaines de gigaoctets **dont il sait s'il l'a déjà analysé**, donc s'il est devenu inutile. Enfin, un disque système saturé empêche l'écriture des dumps — le prochain plantage ne laisserait aucune trace, ce qui en fait une conclusion de diagnostic et pas une remarque de ménage. *Difficulté : moyenne.*

**33 — `actions/checkout@v5` et `actions/upload-artifact@v6`.** Calendrier GitHub vérifié le 17/08/2026 : Node 20 en fin de vie en avril 2026, runners passés à Node 24 par défaut le 16 juin 2026, retrait de Node 20 à l'automne 2026. Les actions en v4 tournent donc **déjà** sur Node 24 et les workflows sont verts : ce n'est pas une panne annoncée, c'est de l'hygiène. La raison de le faire tôt est `publication.yml`, lancé une fois par version — une casse s'y découvrirait au pire moment. Trois `checkout@v4` et deux `upload-artifact@v4` à passer en v5 et v6 (pas plus haut : v6 de checkout et v7 d'upload-artifact changent des comportements inutiles ici). `setup-dotnet@v5` est déjà en Node 24. Vérifier ensuite par un essai à blanc de la publication. *Difficulté : triviale.*

**34 — La réparation ne démarrait pas sous stratégie de groupe. CAUSE TROUVÉE, CORRIGÉ EN 1.3.1.** Retour d'usager du 17/08/2026, Windows 11 23H2 : une console s'ouvrait et se refermait aussitôt, sans message. Ce n'était pas l'assistant guidé mais le bouton **« Lancer la réparation »** de la fenêtre principale — une capture d'écran l'a établi.

`BtnRepair_Click` lançait `powershell.exe -NoProfile -ExecutionPolicy Bypass -File <script.ps1>` avec `UseShellExecute = true`. Or `-ExecutionPolicy Bypass` ne fixe que la portée **Process**, la plus faible : une stratégie de groupe (`MachinePolicy`, `UserPolicy`) prime sur elle. Sur un poste où l'administration a fixé `Restricted` ou `AllSigned`, PowerShell refuse le fichier avant sa première ligne, la console se referme, et — `Process.Start` ayant réussi — aucun `catch` ne se déclenche. Preuve interne : le script généré se termine par « Appuyer sur Entrée pour fermer » ; s'il ne s'affiche pas, la première ligne n'a jamais été lue.

Corrigé en 1.3.1 : contrôle préalable de la stratégie (`PowerShellPolicy`, qui ne regarde que les deux portées de groupe — `LocalMachine=Restricted` est le cas par défaut d'une machine saine et ne doit rien bloquer), message nommant la portée et la valeur, `-NoExit` sur le lancement et dans le `.bat` généré. **La stratégie n'est pas contournée**, décision validée : un outil qui désobéit à la stratégie du parc perd le droit d'y être déployé.

Trois manques comblés au passage, qui valaient indépendamment de cette cause : gestionnaire d'exception global dans les trois exécutables, journal `%ProgramData%\FaultTracePC\erreurs.log`, et message d'échec qui laisse la fenêtre ouverte.

**34 bis — Ce qui reste.** Dans l'assistant guidé, `RunHiddenAsync` récupère `p.ExitCode` mais presque tous les appelants l'ignorent (`var (_, output) = …`) : une commande qui échoue passe inaperçue. À ajouter : journaliser le code de sortie non nul sans crier au loup (`sfc` et `DISM` en renvoient légitimement), et nommer le cas « sortie vide **et** code non nul », signature d'un interpréteur bloqué. Priorité retombée : l'assistant lance ses commandes en `-Command` en ligne, qui **n'est pas** soumis à la stratégie d'exécution — c'est pourquoi lui fonctionnait. *Difficulté : faible.*

**35 — Moitié anglaise écrasée dans `GuidedRepairWindow`. CORRIGÉ EN 1.3.1.** `Lang.T($"Une réparation est déjà en cours :\n\n    {busy}\n\n", $"A repair is already running:")` — la version anglaise perd le nom de l'outil bloquant et les sauts de ligne. Le test de ratio ne l'attrape pas : 24 caractères contre 40 passent son seuil. À corriger, et à faire suivre d'une réflexion sur le seuil. *Difficulté : triviale.*

**36 — ~~Les fenêtres PowerShell ne se ferment plus jamais toutes seules~~. FAIT EN ENTIER — les trois boutons en 1.5.0, le lanceur `.bat` en 1.5.2.**
*(05/09/2026)* Le `.bat` posé à côté du rapport gardait `-NoExit` et restait ouvert pour toujours, alors que le script se termine par sa propre invite : le même mensonge que le logiciel corrigeait ailleurs, à seize jours d'intervalle. Il passe au motif de `PowerShellLauncher` — `-Command` démarre même si la stratégie refuse le `.ps1`, le `catch` montre le refus, le `finally` ne retient la fenêtre que s'il y a quelque chose à lire. Le contenu est garanti ASCII par un test, dans les deux langues : le fichier s'écrit en ASCII et un seul accent y deviendrait un « ? », invite comprise. **Limite laissée en place et écrite dans le code :** le chemin arrive par `%~dp0`, connu seulement à l'exécution — un dossier utilisateur contenant une apostrophe couperait le littéral PowerShell. C'est le défaut de la 1.4.1, au seul endroit où on ne peut pas l'échapper d'avance. Remonté par l'auteur le 19/08/2026 en testant l'application, et c'est un effet de bord de la 1.3.1.

`-NoExit` a été ajouté pour qu'une fenêtre ne s'évapore plus quand une stratégie de groupe refuse le script avant sa première ligne. Effet non voulu : **la fenêtre ne se referme plus jamais d'elle-même**, alors que le script généré se termine par « Appuyer sur Entrée pour fermer ». Appuyer sur Entrée dépose l'utilisateur sur une invite PowerShell. **Le logiciel écrit une phrase qui n'est plus vraie** — exactement la classe de défaut qu'il corrige ailleurs.

**Correctif retenu : l'enrobage.** Remplacer `-NoExit -File <script.ps1>` par

    -Command "& { try { & '<script.ps1>' } catch { Write-Host $_ } finally { Read-Host '<pause>' } }"

Trois propriétés, et c'est leur combinaison qui règle le problème :

- `-Command` en ligne **n'est pas soumis à la stratégie d'exécution** : l'enrobage démarre donc toujours, même quand le `.ps1` est refusé ;
- le `finally` **garantit la pause dans tous les cas** — refus, plantage, erreur de syntaxe, interruption par un antivirus. C'est ce que `-NoExit` apportait, sans son défaut ;
- plus de `-NoExit`, donc **Entrée ferme vraiment**, et la phrase redevient vraie.

La stratégie n'est toujours pas contournée : elle refuse le `.ps1`, on affiche son refus au lieu de le laisser passer en un clin d'œil.

**À traiter en même temps :** la boîte à outils lance `-NoExit -Command` et a donc le même comportement. Les corriger séparément laisserait deux comportements différents à deux boutons qui se ressemblent.

**Fait le 29/08/2026 : `Core/PowerShellLauncher`,** appelé par les trois boutons — « Lancer la réparation », la boîte à outils et l'assistant guidé. Six tests vérifient la ligne d'arguments produite, dont un chemin contenant `O'Brien` et sa variante à apostrophe typographique : le lanceur retombait sinon exactement dans le défaut de la 1.4.1, dans l'autre sens.

**Une chose que le plan écrit ci-dessus n'avait pas vue :** le script engendré porte DÉJÀ sa propre invite « Appuyer sur Entrée pour fermer ». Un `finally` qui met en pause sans condition aurait donc obligé à appuyer **deux fois** sur Entrée. La pause du lanceur est conditionnée à un drapeau `$fini` posé seulement si l'exécution va au bout : la fenêtre ne retient l'utilisateur que lorsqu'il y a quelque chose à lire. Les commandes de la boîte à outils, elles, n'ont pas d'invite à elles et gardent une pause inconditionnelle.

**Ce qui reste : le lanceur `.bat`** engendré à côté du `.ps1` porte encore `-NoExit`, à l'intérieur d'un `Start-Process -Verb RunAs -ArgumentList`. Le convertir demande trois niveaux de citation imbriqués (cmd, puis PowerShell, puis la liste d'arguments) que je ne peux pas éprouver depuis une machine sans Windows : à faire avec un double-clic réel pour vérifier.

**Minuteur de fermeture automatique : écarté, et pas par paresse.** Il résout un problème douteux — une fenêtre terminée et laissée ouverte ne coûte rien — et en crée un vrai : fermer pendant que quelqu'un lit le résultat, ou pendant qu'une réparation tourne encore. Un `chkdsk` sur un disque abîmé dépasse largement l'heure. Si l'idée revenait, la seule forme acceptable serait un décompte visible déclenché **après** la fin du script et annulable à la moindre touche — ce que l'enrobage rend inutile. *Difficulté : faible.*

**37 — L'apostrophe typographique cassait le script de réparation. CORRIGÉ EN 1.4.1.** Constaté le 19/08/2026 sur le PC d'un ami, à partir des trois fichiers produits par la 1.4.0. Le script généré contenait la ligne `Write-Host '  - iaLPSS2_I2C.sys — Pilote v2 I2C d'E/S série Intel(R)'` : le `’` du nom de pilote, recopié tel quel depuis la base des pilotes, **fermait la chaîne** au milieu de la phrase et tout ce qui suivait devenait de la syntaxe invalide.

Ce n'est pas une régression de la 1.4.0 : `PsEscape` n'a jamais traité que l'apostrophe droite `'`, depuis sa première ligne. Ce que la 1.4.0 a changé, c'est la **visibilité** — `-NoExit` a laissé la fenêtre ouverte assez longtemps pour que l'erreur se lise au lieu de disparaître.

Pourquoi rien ne l'a vu : le C# est parfaitement valide, le compilateur n'a rien à dire, les tests de traduction ne regardent pas les chaînes produites à l'exécution, et l'apostrophe typographique est **correcte** dans un nom de pilote français. Le défaut n'existe que pour le second interprète, celui qui relit le texte. Documentation Microsoft, vérifiée : « PowerShell treats smart quotation marks, also called typographic or curly quotes, as normal quotation marks for strings. »

Corrigé en 1.4.1 : `PsEscape` ramène U+2018, U+2019, U+201B et U+2032 sur l'apostrophe droite **avant** de la doubler. Deux tests verrouillent le résultat — un jeu de cas sur les quatre caractères, et un test qui relit chaque ligne d'un script réellement engendré comme le ferait PowerShell — délimiteur ouvrant, doublage, accent grave, commentaires — pour qu'une ligne laissant une chaîne ouverte échoue même si le caractère fautif est un autre. La boîte à outils n'était pas touchée : elle lance ses commandes en `-Command` en ligne, sans passer par le script.

**La leçon, la même que le point 36 :** un texte que le logiciel écrit pour qu'un autre programme le relise n'est pas du texte, c'est du code — et il faut le tester en le relisant, pas en relisant celui qui l'écrit.

**38 — Deux conventions de nom de rapport. CORRIGÉ.** Trouvé le 30/08/2026 en relisant un rapport réel — pas le contenu du rapport, son **nom de fichier** : `Diagnostic_PC_2026-08-30_0907.html` sur une machine appelée `POSTE-01`.

La ligne de commande écrivait `Diagnostic_<MACHINE>_<date>.html`, avec ce commentaire : « nom incluant la machine, indispensable quand tout un parc écrit dans le même partage réseau ». L'application et le service, eux, étaient restés à `Diagnostic_PC_<date>.html`, reliquat d'avant que le nom de machine existe. Deux familles de noms dans le même dossier — et, sur un partage où plusieurs postes déposent leurs rapports, deux machines analysant à la même minute s'écrasent l'une l'autre.

Corrigé en alignant les trois écrivains sur une formule unique, `HtmlReportGenerator.NomDuRapport`. Le contrôle de nom du service — un garde-fou anti-traversée de répertoire — a été élargi **en même temps** et accepte les deux formes : rendre invisibles les rapports déjà déposés aurait effacé l'historique d'un parc du jour au lendemain. L'invariant est testé : *ce que le logiciel produit doit passer le contrôle qu'il applique lui-même*, y compris pour une machine nommée `SALLE-3/POSTE-1`.

**39 — Une carte critique en anglais dans un rapport français. FAUSSE ALERTE, mais instructive.** Le rapport du 29/08 affichait sa conclusion la plus grave — l'alerte WHEA — intégralement en anglais, titre, détail et recommandation, dans un rapport par ailleurs français. Cause supposée : le service tourne sous SYSTEM, dont la langue n'est pas celle de l'utilisateur, et il avait écrit l'alerte en anglais dans `alerts.json`.

Deux tests écrits pour reproduire — l'un sur `AlertCatalog.Localize`, l'autre sur la carte complète fabriquée par le moteur de règles — sont **verts du premier coup** : le code actuel refabrique bien le texte. Un rapport régénéré avec la 1.5.0 sur la même machine, avec les mêmes alertes, est intégralement français.

**Conclusion corrigée le 30/08/2026.** J'avais attribué l'anglais à un exécutable antérieur (`dist\` figé au 17/08). Le point 45 donne une explication plus vraisemblable, et qui ne dépend d'aucune supposition : le service tourne sous SYSTEM, dont la langue est celle du système et non celle de l'utilisateur — c'est **lui** qui avait écrit cette alerte en anglais dans `alerts.jsonl`. Les deux observations restent cohérentes : le fichier contenait de l'anglais, la relecture par une session française l'a refabriqué. La leçon d'origine tient toujours, mais elle s'accompagne d'une seconde : *une explication qui ferme un dossier n'est pas forcément la bonne — celle-ci a tenu vingt-quatre heures.*

*Ce que ça laisse : deux tests de non-régression qui n'existaient pas, et une leçon — avant de chercher un défaut dans le code, vérifier avec quel binaire le fichier a été produit.*

**40 — Le service renonçait au démarrage, ce qui condamnait le déploiement par GPO. CORRIGÉ.** Trouvé le 30/08/2026 en déroulant la séquence réelle d'un déploiement, pas en lisant une liste.

`TelemetryService.ExecuteAsync` lisait `remote.json` **une seule fois** et faisait `return` si le mode n'était pas « Client » : la tâche se terminait et ne relisait plus jamais rien. Or l'ordre d'un déploiement par stratégie de groupe est précisément celui qui déclenche ce cas :

1. le MSI installe et démarre le service — `remote.json` n'existe pas encore, donc mode Local, donc la télémétrie s'arrête définitivement ;
2. le script d'ouverture lance `--configure-remote --master-secret -`, qui écrit la configuration ;
3. rien ne redémarre le service.

Le poste était configuré, la commande rendait 0, **et rien ne répondait** — jusqu'au redémarrage suivant. Autrement dit : rien ne marche le jour où l'on déploie et où l'on teste, tout marche le lendemain, quand on a déjà conclu que c'était cassé. La fenêtre « Mode réseau » ne connaissait pas ce défaut parce qu'elle redéploie le service juste après avoir enregistré ; `MonitorServiceManager` vit dans le projet WPF, et la ligne de commande — celle qui est faite pour la GPO — n'y a pas accès.

Corrigé en faisant **boucler** le service : il relit la configuration toutes les 30 secondes, écoute quand il doit écouter, et repart sur de nouvelles bases dès que le mode, le port ou le jeton change. Un port occupé n'est plus définitif non plus : il est retenté. La comparaison porte sur le SENS — mode effectif, port, jeton — et non sur la date du fichier, pour qu'un script qui réécrit le même contenu ne coupe pas les connexions en cours. La ligne de commande annonce désormais le délai à l'administrateur, au lieu de le laisser deviner.

**Ce que ça change pour la rentrée :** l'ordre des opérations n'a plus d'importance. Installer puis configurer, ou l'inverse, aboutit au même résultat en moins d'une minute.

**41 — Perdre le secret maître : aucune procédure, et aucune révocation par poste.** Rencontré le 30/08/2026 par l'auteur, la veille d'une rentrée, avec deux machines — le meilleur moment possible.

Deux manques distincts, et seul le premier est comblé.

**La procédure de perte** n'existait nulle part : ni message du logiciel disant où le secret est rangé, ni commande de réinitialisation, ni mention dans la documentation. Le secret était pourtant récupérable — chiffré par DPAPI pour le compte de l'administrateur, sur sa machine, donc déchiffrable par lui. Écrit depuis dans `DEPLOIEMENT.md` § 6 bis : où il vit, comment le relire, comment le changer dans le bon ordre.

**La révocation par poste, elle, n'existe pas.** La dérivation est déterministe : recalculer le jeton d'une machine redonne exactement le même. Invalider le jeton d'un seul poste — divulgué, ou machine sortie du parc — oblige donc à changer le secret de TOUT le parc, ou à inscrire un jeton aléatoire à la main pour cette machine. C'est le prix de « plus de liste à conserver », et il n'avait pas été nommé au moment de la décision. Pistes : un compteur de version par machine entrant dans la dérivation (`HMAC(secret, NOM|génération)`), ou une liste de révocation côté console. *Difficulté : moyenne. À traiter avant qu'un vrai parc soit déployé, pas après.*

**42 — Le point 7 était fondé sur un fait faux. FERMÉ le 30/08/2026, après discussion.** Sa justification disait : « une machine réveillée sans session ouverte n'a aucun destinataire pour ses alertes ». C'est inexact, et l'inexactitude a survécu trois versions parce que personne n'était allé lire le code.

Ce qui se passe réellement : le service **écrit** chaque alerte dans `Flight\alerts.jsonl`, avec écriture forcée sur le disque, comme la boîte noire. Elle est donc durable et survit au redémarrage. Trois chemins la ramènent ensuite à un humain — la bulle de la barre des tâches (si l'application est ouverte), la console de parc à chaque actualisation (`/api/alerts?days=7`), et le rapport de parc. **Rien n'est perdu. Rien n'est poussé.** Ce qui manquait n'était pas la conservation mais le fait de venir chercher l'administrateur.

Or les alertes concernées — température soutenue, disque qui se dégrade, WHEA répétées, coupures brutales — ont une constante de temps de plusieurs jours ou semaines. Le scénario qui fait mal n'est pas « l'alerte est arrivée trop tard », c'est « personne n'a ouvert la console pendant trois semaines ». C'est une question d'habitude, pas de canal.

**Décision de l'auteur, le 30/08/2026 :** « rien de plus, j'ouvre la console. » Le point est donc fermé plutôt que reporté — un point ouvert qu'on ne veut pas faire pollue une liste et se fait re-proposer indéfiniment.

**Écarté au passage :** écrire dans le journal d'événements Windows n'aurait rien changé. L'établissement dispose de GLPI, qui est un outil d'inventaire et de tickets — **son agent ne collecte pas les journaux d'événements**. Les alertes y seraient tombées dans un journal que rien ne ramasse.

**43 — Les alertes disparaissent avec le poste. FAIT le 19/09/2026.** Trouvé en fermant le point 7, et plus sérieux que lui.

Les alertes vivent **sur la machine**, dans `ProgramData\FaultTracePC\Flight\alerts.jsonl`. Un poste réimagé — opération routinière en établissement, souvent pendant les vacances — repart avec un journal vide. La console les collecte à chaque actualisation mais **ne les conserve pas** : elle les affiche et les oublie.

Conséquence concrète : on peut perdre la preuve qu'une machine chauffait depuis six mois, précisément au moment où elle servirait à justifier son remplacement. C'est aussi ce qui empêche toute vue dans la durée — « ce poste alerte trois fois plus que les autres » est une phrase que la console ne peut pas dire aujourd'hui.

Piste : la console archive ce qu'elle collecte, dans son dossier à elle, par machine et par règle. Rien à changer sur les postes. *Difficulté : faible à moyenne.*

**Livré le 19/09/2026, en deux gestes.** La console archive ce qu'elle collecte, dans `Documents\FaultTracePC\Alertes`, **un fichier par poste**. Rien n'a changé sur les postes.

- **Rien n'est jamais effacé, et c'est une décision.** Une alerte pèse quelques centaines d'octets ; même un poste bavard produira moins d'un mégaoctet par an. Effacer au bout de N jours, ce serait risquer de supprimer exactement la preuve qu'on cherchait — et la supprimer sans que personne l'ait demandé. Les périodes proposées (7, 30, 90, 365 jours, tout) filtrent **l'affichage**, jamais le fichier. Un bouton ouvre le dossier : c'est là qu'on supprime à la main l'historique d'un poste sorti du parc.
- **Le dédoublonnage est la règle, pas l'exception.** La console relit sept jours d'alertes à CHAQUE actualisation : sans lui, une alerte du lundi serait archivée une fois par actualisation jusqu'au lundi suivant. Deux alertes sont la même quand elles portent le même **instant** et la même **règle** — les ticks plutôt que le texte de la date, pour qu'un aller-retour de sérialisation ne fabrique pas un faux nouveau.
- **L'archivage est branché sur l'actualisation, et nulle part ailleurs.** C'est le seul moment où la console a les alertes sous la main. Archiver à la fermeture ou une fois par jour reviendrait à parier que la console est ouverte au bon moment. L'écriture se fait hors du fil d'affichage ; un échec d'archivage n'interrompt pas la supervision mais **est dit** dans la barre d'état — une archive qui cesse de s'écrire en silence est pire que pas d'archive du tout, puisqu'on croirait avoir la preuve.
- **Le nom du poste est contrôlé AVANT de devenir un chemin.** Lettres, chiffres et traits d'union, comme pour la liste de déploiement. Neuf cas de test, dont `../../ailleurs` et `C:\Windows\System32`.
- **Une colonne « Alertes 90 j » dans l'onglet Supervision**, avec le nombre de critiques entre parenthèses. Quatre-vingt-dix jours, c'est la durée qui sépare deux vacances scolaires : sept jours ne diraient rien d'un poste qui chauffe une fois par mois, un an mélangerait la machine d'aujourd'hui avec celle d'avant sa réparation. Zéro s'écrit « — » et non « 0 » : une colonne pleine de zéros attire l'œil sur ce qui ne s'est pas produit.
- **Le texte des alertes est refabriqué, pas recopié.** Une archive de six mois a été écrite dans la langue de l'époque ; le fichier conserve le **fait** — la règle et la valeur mesurée — et `AlertCatalog` réécrit la phrase dans la langue d'aujourd'hui. Sans cela, l'historique serait un mélange de deux langues, et aucun autre test ne l'aurait vu.
- **Une archive vide n'est pas une preuve de bonne santé.** Un poste jamais interrogé par cette console a exactement le même fichier vide qu'un poste sain. La fenêtre le dit en toutes lettres plutôt que de laisser lire « ce poste va bien ».
- **Quatrième dossier écrit par le logiciel à côté du code** — après `parametres.json`, `Journal/` et `Deploiement/`. Cette fois la ligne du `.gitignore` a été écrite **dans le même geste** que le code qui écrit le dossier, comme la leçon du 19/09 le demandait.

**44 — Publier l'état du parc dans GLPI. Choix de stratégie, pas correction.** L'établissement utilise déjà GLPI pour son inventaire et ses tickets. Y déposer l'état de santé de chaque poste — ou ouvrir un ticket quand un disque commence à lâcher — mettrait l'information là où l'administrateur regarde déjà, au lieu de lui demander d'ouvrir un outil de plus.

**La décision de conception à ne pas rater** : l'intégration doit vivre **sur la console, pas sur les postes**. Vingt machines portant chacune un jeton d'API GLPI, ce sont vingt secrets à protéger, à renouveler et à corriger le jour où l'API change. La console collecte déjà l'état de tout le parc, elle est unique, et elle sait déjà chiffrer un secret pour son seul propriétaire.

GLPI expose une API REST : la v1 historique (`apirest.php`) et, depuis GLPI 11, une API « haut niveau » v2. **À vérifier sur la version réellement installée avant d'écrire quoi que ce soit** — c'est le genre de détail qui change d'une version à l'autre. *Difficulté : moyenne. Aucune urgence.*

**45 — ~~Un rapport distant sort dans la langue de la machine cible, pas dans celle de l'administrateur~~. FAIT EN 1.5.2.**
*(05/09/2026)* Corrigé exactement comme prévu ci-dessous, et le correctif tient en trois endroits : `Lang.FromCode` (« fr »/« en » → la langue ; `auto`, vide ou inconnu → **null**, c'est-à-dire « rien demandé » et non « français » — sans quoi une console ancienne imposerait le français à un parc anglophone), `ParkProtocol.ScanQuery` qui met la chaîne de requête en un seul endroit testable, et le service qui applique la langue **dans `ScanLock`** puis la rend au poste dans le `finally`. Deux vérifications faites avant d'écrire : la signature HMAC couvre la requête, donc ajouter `lang=` des deux côtés reste cohérent — l'oublier aurait fait refuser tous les scans distants ; et `Lang` n'est employé qu'à un seul autre endroit du service, donc le basculement temporaire n'a presque aucun rayon d'action. Constaté par l'auteur le 30/08/2026 : diagnostic lancé depuis la console vers sa propre machine, dont la session est française — rapport en anglais.

`/api/scan` ne rapatrie pas des données : il déclenche l'analyse sur la machine cible, et **c'est le service qui écrit le HTML**. Or ce service tourne sous SYSTEM, et la langue se résout ainsi : option de ligne de commande, puis préférence utilisateur (`Documents\...\langue.txt`, que SYSTEM n'a pas), puis réglage machine (`ProgramData\FaultTracePC\langue.txt`, absent par défaut), puis langue d'affichage de la session — celle du système, pas celle de l'administrateur. Un Windows installé en anglais auquel un utilisateur a ajouté le français **pour son compte** produit donc des rapports anglais.

**C'est une incohérence avec un principe que le projet applique déjà ailleurs :** dans `ParkProtocol.Sentence`, la phrase est construite « chez celui qui lit, donc dans SA langue ». Le rapport distant, lui, est écrit dans la langue de celui qui l'exécute.

**Correctif retenu :** la console joint sa langue à la requête (`/api/scan?days=30&lang=fr`) et le service génère dans cette langue. Purement additif — un poste resté en 1.5.1 ignore le paramètre et se comporte comme aujourd'hui.

**Le piège à ne pas manquer en l'écrivant :** `Lang.Apply` est global au processus et le service traite ses requêtes en parallèle. Deux consoles de langues différentes se marcheraient dessus. Ce n'est sûr que parce que `/api/scan` est **déjà sérialisé par `ScanLock`** : la langue doit être appliquée **à l'intérieur** de ce verrou et restaurée ensuite. Écrit naïvement, c'est une bombe à retardement qu'aucun test existant ne verrait.

**Contournement en attendant**, à faire sur chaque poste : `FaultTracePC.Cli.exe --set-machine-lang fr` **puis `Restart-Service FaultTracePCMonitor`** — contrairement à `remote.json`, la langue n'est lue qu'au démarrage du service. Au déploiement, `FTPCLANG=fr` fait la même chose d'emblée.

**46 — « Voir en temps réel pourquoi ça coupe » : le besoin est bon, la réponse n'est pas l'actualisation automatique. FAIT le 19/09/2026 — la vue boîte noire distante. L'actualisation automatique reste à faire, comme confort.** Demandé par l'auteur le 31/08/2026 en découvrant que la console n'actualise que sur clic.

**Pourquoi le direct ne répondrait pas à la question.** Au moment de la coupure, la machine s'arrête et le réseau avec elle : la console ne verrait rien de plus. Une actualisation toutes les deux secondes afficherait une valeur vieille de deux secondes, puis « injoignable ». Elle donnerait l'**illusion** d'une réponse — exactement le genre de fonctionnalité qui rassure sans informer.

**Ce qui répond déjà à la question**, et c'est la raison d'être de la boîte noire : le service relève un échantillon toutes les 10 s et l'écrit **avec écriture forcée sur le disque**. Les dernières secondes avant la coupure survivent donc à la coupure. Le moteur de règles les exploite (`AnalyzeFlightRecorder`) et le rapport écrit la phrase — « le processeur était à 97 °C juste avant l'arrêt ». La réponse existe : elle arrive après, parce que c'est le seul moment où elle peut arriver.

**Ce qui manque vraiment, et qui est à moitié construit :** le point d'accès **`/api/flight?minutes=60` existe dans le service et n'est appelé par personne**. La console ne sait donc pas montrer la boîte noire d'une machine distante. C'est ça, la fonctionnalité à écrire : après une coupure, sélectionner le poste et voir ce qu'il a enregistré dans ses dernières minutes — températures, charge, mémoire — sans avoir à s'y rendre ni à relancer une analyse complète.

**Limite à énoncer dans l'interface** : 10 secondes entre deux relevés. Pour une surchauffe, qui monte en minutes, c'est largement suffisant. Pour un pic de charge instantané, on peut passer à côté. Le dire vaut mieux que laisser croire à un enregistrement continu.

**L'actualisation automatique reste souhaitable** — mais comme confort, pas comme réponse : une case « actualiser toutes les N secondes », désactivée par défaut, en sachant qu'interroger vingt postes en boucle a un coût réseau. *Difficulté : faible pour l'actualisation, moyenne pour la vue boîte noire distante.*

**Livré le 19/09/2026.** Un bouton « Boîte noire du poste » dans l'onglet Supervision ouvre les relevés du poste sélectionné : 15 minutes, 1 heure, 4 heures ou 24 heures.

- **Le plus récent en haut.** Ce qu'on vient chercher après une coupure, ce sont les DERNIÈRES lignes ; les faire défiler depuis le début cacherait la réponse au bas du tableau.
- **Trois sortes de lignes sautent aux yeux** : les événements Windows, les démarrages et les arrêts de la surveillance. Et parmi elles, la plus précieuse — *« Démarrage de la surveillance — LA SESSION PRÉCÉDENTE S'EST TERMINÉE BRUTALEMENT »* : c'est la preuve, écrite par la machine elle-même, qu'elle n'a pas été éteinte proprement. Sans elle, « le poste a redémarré » et « le poste a planté » se ressemblent.
- **La limite de dix secondes est écrite en grand**, pas en note de bas de page. Laisser croire à un enregistrement continu ferait chercher une trace qui n'a jamais pu être écrite.
- **Une période sans relevé se dit comme telle** — poste éteint, ou surveillance arrêtée — et pas comme une panne.
- **Un délai d'attente propre à cette lecture.** Le client de la console abandonne au bout de quatre secondes, ce qui est voulu pour interroger vingt postes d'affilée ; vingt-quatre heures de relevés font près de neuf mille lignes, et le même délai aurait coupé la lecture en plein milieu, avec une erreur qui aurait ressemblé à un poste injoignable.

**Ce que ce lot a coûté, et pourquoi si peu :** le point d'accès `/api/flight` existait depuis la 1.5.0 et n'était appelé par personne ; la fonction qui signe les requêtes acceptait déjà une chaîne de requête. *Une fonctionnalité à moitié construite et jamais branchée ne se voit pas dans le code : elle se voit dans la feuille de route, et c'est à ça qu'elle sert.*

**Et une inconsistance de vocabulaire corrigée dans la foulée :** la colonne de mémoire virtuelle s'appelait « Engagée » alors que le logiciel dit « Mém. virt. » dans la console et « Mémoire virtuelle » dans ses conclusions. Deux mots pour la même chose obligent le lecteur à deviner que c'est la même chose.

**47 — Colonne « Top processus » vide deux fois sur trois. CORRIGÉ.** Constaté dans la console le 31/08/2026. La boîte noire ne relève les processus qu'un échantillon sur trois — toutes les 30 s, pour ne pas grossir le journal — et `/api/status` renvoie le **dernier** échantillon, qui n'en porte donc généralement pas. La colonne se remplissait au hasard, ce qui est pire qu'une colonne toujours vide : on ne sait pas si l'information manque ou si la machine n'a rien à signaler. `BuildStatus` complète désormais avec le relevé le plus récent qui en contienne un — au pire 30 secondes d'âge, sans conséquence pour la question posée.

**48 — Une erreur WHEA de port PCIe est attribuée au processeur, et la recommandation envoie au mauvais endroit. FAIT le 18/09/2026, 1.6.0.** Constaté le 06/09/2026 sur `S2-00-32-2025`, premier poste installé à distance par le script de déploiement.

Le rapport écrit : « Le processeur a signalé 27 erreur(s) matérielle(s) », et recommande températures, alimentation, retrait de l'overclocking/XMP, mise à jour du BIOS. Les 27 événements sont pourtant tous identiques et disent autre chose : `WHEA-Logger` **ID 17**, *erreur matérielle **corrigée***, `Composant : PCI Express Root Port`, `Source de l'erreur : Advanced Error Reporting (PCI Express)`. Le triplet `0x0:0x1:0x0` désigne le port racine du processeur, et derrière lui la carte graphique.

Deux erreurs de lecture se cumulent :

- **la source est confondue avec le rapporteur.** Sur Intel, les ports racines PCIe sont bien dans le paquet du processeur, donc « le processeur a signalé » n'est pas faux au sens strict. Mais pour celui qui lit, c'est trompeur : le fautif est un lien PCIe et son périphérique, pas un cœur ;
- **la gravité est perdue.** L'ID 17 signale une erreur *corrigée* — le lien a récupéré, rien n'a été perdu. Le rapport la classe critique au même titre qu'une erreur fatale.

Résultat concret : la recommandation fait retirer l'XMP et suspecter l'alimentation, quand la piste utile est le lien PCIe — génération négociée, gestion d'énergie du lien, pilote, contact du connecteur. C'est exactement le défaut que ce logiciel existe pour combattre : « le pilote fautif est `nvlddmkm.sys` », exact et inutile.

**Correctif envisagé :** distinguer les sources WHEA (`Processor Core`, `Cache Hierarchy`, `Memory Controller`, `PCI Express Root Port`) et donner une recommandation par source, ainsi que distinguer corrigé (ID 17) de fatal. Le champ existe déjà dans le message de l'événement : il est lu, puis ignoré.

**49 — Le message WHEA est tronqué juste avant l'information décisive. FAIT le 18/09/2026, 1.6.0.** Même rapport, même jour.

Le tableau des événements affiche : `Bus principal :Appareil :Fonctio…`. Le triplet bus/appareil/fonction est coupé — et c'est la **seule** donnée qui permet d'agir, puisqu'elle nomme le lien fautif. Il a fallu retourner au journal d'événements de la machine pour l'obtenir, ce qui est précisément le travail que le logiciel prétend éviter.

**Correctif envisagé :** pour les événements WHEA, extraire le triplet et le porter dans la conclusion — voire le traduire en nom d'appareil, `Get-PnpDevice` donnant l'emplacement sous la forme « Bus PCI 0, périphérique 1, fonction 0 ». La troncature à 600 caractères reste bonne pour le reste ; ici, ce qui compte est en fin de message.

**50 — Les codes d'arrêt sont dans le journal, le logiciel ne les lit pas. FAIT le 18/09/2026, 1.6.0.** Constaté le 14/09/2026 sur `MLEAR-031-2024`, un poste élève.

Le rapport annonce « Aucun BSOD détecté sur la période » et « Pas de panne critique ». La machine avait planté **quinze fois**. Les événements `Kernel-Power 41` portent un champ `BugcheckCode` renseigné à chaque fois : 0xEF, 0xC000021A, 0x1E, 0x7E, 0x7A. Le logiciel conclut à l'absence de plantage parce qu'il ne trouve **aucun fichier de vidage** — or aucun vidage n'avait pu être écrit. Il confond « je n'ai pas de trace » avec « il ne s'est rien passé ».

Le `BugCheckCatalog` existe déjà et sait traduire ces codes. Il n'est alimenté que par les vidages ; il suffit de le nourrir aussi depuis l'événement 41.

**Ce que ça apporte, et qui n'est pas qu'une correction d'affichage :** un `BugcheckCode` à **0** signifie qu'il n'y a PAS eu de plantage — la machine a perdu son alimentation, ou quelqu'un a tenu le bouton. Sur ce poste, cinq des vingt événements étaient de ce type. Dans un établissement, c'est la distinction qui compte : elle sépare « un élève a forcé l'extinction » de « Windows s'est planté », et les deux ne se réparent pas pareil. Aucun autre signal ne fait cette séparation. Mieux : l'ordre des dates la tranche dans l'autre sens. Sur cette machine, les coupures arrivent toujours APRÈS les plantages du même jour — les élèves subissaient la panne, ils ne la causaient pas.

**51 — `volmgr` 161 et 46 ne sont pas des erreurs disque. FAIT le 18/09/2026, 1.6.0.** Même machine, même jour.

Ces événements disent « la création du fichier de vidage a échoué ». Le logiciel les compte en « Erreurs disque répétées » et en fait le point le plus notable du rapport. C'est la **conséquence** du plantage prise pour sa cause — et sur ce poste, la vraie explication était ailleurs : un fichier d'échange de 2,4 Go pour 15,8 Go de mémoire, trop petit pour un vidage noyau.

C'est une panne en soi, et c'est celle qui empêche de diagnostiquer toutes les autres. Elle mérite sa conclusion propre, avec les quatre vérifications qui la règlent : type de vidage, taille du fichier d'échange, espace libre, pilotes filtres.

**52 — Un verdict ne peut pas dire « pas de panne critique » après avoir compté cinq arrêts inattendus. FAIT le 18/09/2026, 1.6.0.** Même rapport.

Les catégories « Arrêt inattendu » et « Coupure (Kernel-Power 41) » existent dans le tableau des événements. Elles n'alimentent aucune conclusion et ne pèsent pas sur le verdict. Le rapport a donc rassuré sur une machine qui plantait toutes les semaines depuis quatre mois. **C'est le pire mode de défaillance possible pour cet outil** : se tromper est réparable, rassurer à tort ne l'est pas.

**53 — Aucune section réseau. FAIT le 18/09/2026, 1.6.0.** Constaté le même jour, sur la même machine, pour une panne dont le rapport ne dit pas un mot : plus aucun réseau Wi-Fi visible.

Trois faits, tous lisibles sans le moindre outil externe, donnaient le diagnostic en une ligne :

- carte Wi-Fi présente, radio active, pilote chargé — mais **zéro profil enregistré**, ni par stratégie de groupe ni par l'utilisateur ;
- Ethernet jamais connecté depuis la réinstallation du 13/05 : aucune stratégie de groupe reçue, donc aucun profil, donc pas de Wi-Fi — une boucle que seul un câble casse ;
- consentement de localisation refusé pour la session (`HKCU\...\ConsentStore\location` à `Deny`). Depuis l'automne 2024, Windows exige ce consentement pour toute API qui énumère les réseaux ; sans lui, la liste est vide et `netsh` rend « accès refusé ».

Le rapport, lui, a parlé de la batterie et de sept erreurs disque. Le tableau des pilotes signalait bien le pilote Wi-Fi comme « ancien (4 ans) » — sans que ça devienne jamais une conclusion.

Une section réseau devrait porter : cartes présentes et leur état, pilote et son âge, profils Wi-Fi, dernière connexion au domaine, services `WlanSvc`/`Dhcp`/`Dnscache`, filtres NDIS tiers liés aux cartes. Dans un parc, « plus de réseau » est l'une des pannes les plus fréquentes, et c'est aujourd'hui le seul domaine dont le logiciel ne dit **rien**.

**54 — « Ce logiciel n'est plus installé, problème sans objet » écrit à propos d'un composant de Windows. FAIT le 17/09/2026, 1.6.0.** Constaté le 14/09/2026 sur `PC-W10-11`, le poste qui est à l'origine de ce projet.

Le rapport écrit : *« Application anciennement instable : **dwm.exe** (9 crashs) — ce logiciel ne figure plus parmi les programmes installés — problème probablement sans objet. »*

`dwm.exe` est le gestionnaire de fenêtres de Windows. Il ne figure pas dans la liste des programmes installés parce qu'il n'est pas une application : c'est un composant du système. Il n'a jamais été désinstallé.

Et le classement est doublement faux, parce que ces neuf plantages — module fautif `dwmcore.dll` — sont **la meilleure corroboration de la conclusion principale du même rapport** : le compositeur graphique meurt quand le pilote d'affichage meurt. Le logiciel a écarté sa pièce à conviction la plus solide.

**Correctif :** une liste blanche de binaires système (`dwm.exe`, `explorer.exe`, `csrss.exe`, `svchost.exe`, `lsass.exe`, `winlogon.exe`, `services.exe`, `RuntimeBroker.exe`, `SearchHost.exe`…) pour lesquels l'absence dans les programmes installés ne signifie rien. Plus largement : **l'absence d'une entrée de désinstallation ne doit jamais, à elle seule, disqualifier un plantage constaté.** Un programme portable ou du Microsoft Store est dans le même cas.

**55 — La version du pilote fautif n'est pas comparée d'une analyse à l'autre. FAIT le 18/09/2026, 1.6.0.** Même machine, trois rapports : 24/08 07 h 38, 24/08 12 h 41, 14/09.

Le pilote mis en cause, `nvlddmkm.sys`, apparaît en `32.0.15.8216` dans le premier rapport et en `32.0.15.8278` dans le second, cinq heures plus tard. L'auteur avait mis le pilote à jour entre les deux. Quatre nouveaux écrans bleus portant **la même signature** sont survenus ensuite, après huit semaines de calme.

Le logiciel dit bien « le problème PERSISTE : un nouveau crash avec la même signature ». Il ne dit pas la seule chose qui conclut le dossier : **le pilote accusé a été remplacé entre les deux analyses, et les plantages ont continué — ce n'est donc pas le pilote.**

Les données sont déjà là : la version du pilote est enregistrée à chaque analyse, et la section « Pilotes mis à jour » les compare déjà. Il manque le rapprochement entre ce tableau et le pilote nommé par la conclusion.

**Ce que ça change concrètement :** sans ce rapprochement, la recommandation reste « réinstaller le pilote proprement avec DDU » — c'est-à-dire refaire ce qui vient d'échouer. Avec, elle devient « le logiciel est hors de cause, regardez le matériel ». C'est la différence entre une boucle et un diagnostic.

**56 — Les vidages « en direct » de Windows étaient listés, jamais exploités. FAIT le 17/09/2026.** Constaté sur `PC-W10-11`, rapport du 17/09 17 h 38.

Le rapport affichait **178 fichiers de `C:\Windows\LiveKernelReports`** — nom, date, taille, code d'arrêt — dont **155 portant le code `0x141`**, le plus ancien du 16/06/2022. Il ne les comptait pas, n'en tirait aucune conclusion, et les nommait `BUGCODE_0x141`, un repli fabriqué qui ressemblait à s'y méprendre à un identifiant Microsoft.

`0x141` est `VIDEO_ENGINE_TIMEOUT_DETECTED` : un moteur de la carte graphique n'a pas répondu, Windows l'a réinitialisé, aucun écran bleu n'est apparu. **C'est le journal de bord d'une carte qui se dégrade, et il ne passe jamais par le journal d'événements** — ces incidents n'existent que sous forme de fichier.

**Correctif livré :** `AnalyzeGpu` compte désormais les gels (`0x141`, `0x117`, `0x119`, `0x193`) ; le catalogue nomme `0x141` et `0x144` ; le repli pour un code inconnu affiche le code brut plutôt qu'un nom d'allure officielle.

**57 — « 0 réinitialisation » était écrit en présence de 155 réinitialisations. FAIT le 17/09/2026.** Même rapport, conséquence directe du 56.

La carte de verdict annonçait *« Instabilité du pilote graphique (0 réinitialisation, 7 BSOD) »*. Le compteur ne regardait que les événements `Display 4101` du journal Windows. **Le logiciel imprimait la preuve du contraire quatre cents lignes plus bas dans le même document.**

**Correctif livré :** le titre porte les trois comptages — gels du moteur, réinitialisations journalisées, écrans bleus — et la conclusion croise désormais les deux sources.

**58 — Un gel suivi d'un écran bleu est une réinitialisation qui a ÉCHOUÉ : ce rapprochement n'était pas fait. FAIT le 17/09/2026.**

Les deux séries existaient séparément. Les apparier donne la seule mesure qui distingue un pilote instable d'un matériel qui lâche : **la proportion de gels qui se terminent mal, et la date à laquelle elle bascule.** Sur `PC-W10-11` : environ 145 gels récupérés sans incident de 2022 à juin 2026, puis 10 gels sur 10 soldés par un écran bleu à partir du 1er juillet 2026.

**Correctif livré :** appariement dans une fenêtre de dix minutes, datation de la bascule, disculpation du pilote quand le premier gel lui est antérieur, et mise hors de cause de la surchauffe **par la mesure de la boîte noire** au lieu de demander à l'utilisateur d'aller la prendre lui-même. La recommandation devient le test décisif et gratuit : retirer la carte, brancher l'écran sur la sortie de la carte mère, ou permuter avec un autre poste.

**59 — Les plantages de `dwm.exe` sont une corroboration de la panne graphique, pas un problème logiciel séparé. FAIT le 17/09/2026, 1.6.0.** Même rapport.

Onze plantages de `dwm.exe` via `dwmcore.dll`, classés « Application anciennement instable » et écartés au titre du point 54. Or le gestionnaire de fenêtres de Windows meurt **parce que** l'affichage meurt : c'est la meilleure confirmation de la panne, rangée dans la mauvaise colonne. Le point 54 corrige le « sans objet » ; celui-ci demande davantage — **rattacher le plantage d'un composant graphique de Windows à la conclusion graphique** au lieu d'en faire une ligne isolée.

**60 — Une clé USB et un port de contrôleur comptés ensemble sous « erreurs disque ». FAIT le 18/09/2026, 1.6.0.** Même rapport.

Douze événements agrégés en un seul chiffre : `volmgr` 162, `Ntfs` 55, `disk` 51, `storahci` 129, répartis entre une clé USB (`Generic Flash Disk`, E:), un volume F: et `\Device\RaidPort0`. La recommandation commence par la gestion d'alimentation des liens PCI Express et les câbles SATA — **deux conseils sans aucun sens pour une clé USB.** Il faut séparer par périphérique avant de conseiller quoi que ce soit.

**61 — Deux dates différentes pour le même pilote, dans le même rapport. FAIT le 17/09/2026, 1.6.0.** Même rapport.

`32.0.15.8278 du 08/07/2026` dans une carte, `32.0.15.8278 du 25/06/2026` dans une autre. L'une est la date du fichier `.sys`, l'autre celle du paquet INF ; les deux sont annoncées comme « le pilote du… ». Il faut nommer laquelle on affiche, ou n'en afficher qu'une.

**62 — La boîte noire répète le même incident et masque le gel. FAIT le 17/09/2026, 1.6.0.** Même rapport.

Trois tableaux identiques pour l'incident du 15/09 09 h 53, deux pour celui du 17/09 12 h 28. Et le tableau intitulé « dernières secondes avant l'incident de 12 h 29 » s'arrête à **12 h 27 min 26 s** — quatre-vingt-quatorze secondes avant. Ce trou n'est pas un défaut d'affichage : **c'est le gel lui-même**, l'échantillonneur ayant cessé de répondre en même temps que la machine. Il faut dédoublonner les incidents proches et **nommer le trou au lieu de le laisser passer pour une fin de tableau.**

**63 — Le script de réparation raisonne par familles de problèmes, pas par mesures. FAIT le 18/09/2026, 1.6.0.** Script `Reparation_PC_2026-09-17_1738.ps1`.

Le script est bien construit — point de restauration, confirmation O/N, rien d'irréversible sans accord. Mais il ignore ce que le rapport a mesuré : il désigne comme premiers suspects dix pilotes Trend Micro et AMD de 2021 alors que l'analyse WinDbg nomme `nvlddmkm.sys` dans cinq dumps sur cinq ; il demande de surveiller la température GPU au seuil de 85 °C alors que la boîte noire l'a mesurée pendant 94 h 32 à 67,7 °C maximum ; il propose `chkdsk C: /f` quand les erreurs portent sur une clé USB ; il lance `wsl --update` sur le poste d'une secrétaire.

**Le principe à corriger tient en une phrase : le script doit être écrit à partir des mesures du rapport, pas à partir des catégories de panne détectées.**

**64 — Déploiement de parc intégré au logiciel. À FAIRE, 1.7.0.** Demandé le 17/09/2026.

Le déploiement se fait aujourd'hui par un script PowerShell distribué à part, publié sur palisser.fr. Il fonctionne, il a été éprouvé sur un parc réel, mais il vit en dehors du logiciel alors que celui-ci porte déjà `ParkWindow`, `ParkSecret`, `ParkProtocol`, `PowerShellPolicy` et `parc.json`.

**Décisions arrêtées le 17/09/2026 :**

- **Le logiciel pilote le script, il ne le remplace pas.** Le `.ps1` gagne un commutateur `-SortieJson` : sans lui, comportement inchangé pour qui le lance à la main ; avec lui, une ligne JSON par poste et par étape, écrite **dans un fichier en UTF-8** — pas sur la sortie standard, que PowerShell 5.1 décode avec la page de code de la console et transforme en charabia.
- **La liste des postes se construit en C#**, en fusionnant trois sources dédoublonnées par nom de machine : `postes.csv`, une OU de l'Active Directory (via `DirectorySearcher` natif, pour ne pas exiger RSAT), et `parc.json`.
- **Périmètre complet** : réveil réseau, installation, bascule en mode parc.
- **Aucun mot de passe nulle part.** L'exécution se fait avec le ticket Kerberos du compte qui a lancé le logiciel.
- **Garde-fous** : mode « vérifier seulement » qui ne modifie rien, aucune case cochée par défaut, confirmation par saisie du nombre de postes, plafond par exécution, journal horodaté, secret ni affiché ni journalisé, et **aucune action de désinstallation dans cet onglet**.

**Découpage en quatre lots**, le risque à la fin sur une plomberie déjà éprouvée : **A** la liste seule, en lecture pure — **B** le mode « vérifier seulement », qui construit et prouve le canal JSON sur des opérations inoffensives — **C** le déploiement réel avec ses garde-fous — **D** le journal et la reprise des seuls postes en échec.

**Avancement au 18/09/2026 — le lot A est découpé en trois, et deux sont livrés :**

- **A-1 ✔** la fusion des sources (`ParkInventory`). Décision prise ce jour-là, contre celle du 17/09 : **`postes.csv` ne crée aucun poste.** Son en-tête le disait déjà en majuscules — c'est un annuaire d'adresses MAC, qui contient les téléphones et les tablettes rendus par le DHCP. Il ne fait qu'ajouter une adresse MAC à un poste déjà listé par l'Active Directory ou par `parc.json`. Dédoublonnage par nom Windows normalisé, les trois écritures de l'annuaire et la forme longue du DHCP se rejoignant sur la même clé.
- **A-2 ✔** l'interrogation de l'annuaire (`ParkDirectory`) et les réglages (`ParametresParc`). `PageSize = 1000` est la ligne qui compte : sans elle un contrôleur de domaine s'arrête à mille résultats **en silence**. Le contrôle de la saisie nomme la faute et sa correction — une virgule non échappée ne produit pas « virgule non échappée » côté LDAP, mais « syntaxe non valide », voire une liste vide sans erreur. Et `ListerUnites` rend les noms distinctifs tels que l'annuaire les stocke, donc déjà échappés : choisir plutôt que saisir supprime le problème à la source.
- **A-3 ✔** l'onglet qui affiche tout ça, livré le 19/09/2026. La console passe à deux onglets ; le contenu de la supervision est déplacé tel quel et `ParkWindow.xaml.cs` n'est pas modifié — la logique du nouvel onglet tient dans une classe partielle à part, branchée par l'événement `Loaded`. Quatre décisions validées ce jour-là : **deux champs plutôt qu'une liste modifiable** (c'est le nom distinctif qui part dans le réglage, pas le libellé lisible) ; **rien ne part au démarrage**, le réglage est relu mais l'annuaire n'est pas interrogé ; **l'adresse MAC ne s'affiche pas**, la colonne dit seulement si elle est connue ; **« hors annuaire » plutôt qu'une valeur par défaut**, un poste saisi à la main n'ayant pas de compte d'ordinateur connu du logiciel.

**Ce que le premier contact avec un annuaire réel a révélé, le 19/09/2026 :**

- **`LDAP://` tout court n'est pas un chemin valide** — ADSI le refuse avec `0x80005000`, `E_ADS_BAD_PATHNAME`. C'était pourtant ce que `CheminLdap` rendait pour une saisie vide, c'est-à-dire **pour le cas par défaut** : le seul où l'utilisateur n'a rien à saisir était le seul qui ne pouvait pas marcher. La racine se demande désormais à l'annuaire lui-même, par `LDAP://RootDSE` et son attribut `defaultNamingContext`. **Et le test était vert** : il affirmait `CheminLdap("") == "LDAP://"`, la mauvaise valeur, gravée. Deuxième leçon de la même famille que le point 77 — *un test qui grave l'erreur attendue ne protège de rien, il empêche seulement de s'en apercevoir.*
- **Deux `postes.csv` qui ne se voyaient pas.** Le script de déploiement écrit le sien à côté de lui — il est fait pour tourner depuis une clé USB ; la console cherchait le sien dans `Documents\FaultTracePC`. La colonne des adresses MAC restait vide sans que rien ne l'explique. Le chemin est devenu réglable, un dossier est accepté autant qu'un fichier, les guillemets d'un « Copier en tant que chemin d'accès » sont retirés, et le message distingue désormais **fichier absent** de **fichier lu, zéro adresse**.
- **Une liste vide après une erreur ne se dit plus comme une liste vide après une lecture réussie.** La barre affichait « aucune unité d'organisation, ce qui convient dans la plupart des cas » pendant que l'annuaire refusait le chemin : rassurant, et faux.

**Lot B ✔ — le mode « vérifier seulement », livré le 19/09/2026.** Le logiciel pilote le script, il ne le remplace pas — la décision du 17/09 tient jusqu'au bout.

- **Le script entre dans le dépôt et voyage avec le logiciel.** Distribué à part, il existait en deux exemplaires qui ont fini par diverger. Surtout, le contrat JSON lie le script à la console : deux fichiers livrés séparément se désynchronisent sans que rien ne le signale, et le symptôme serait un journal illisible. Embarqué en ressource au nom logique fixé, extrait par **copie des octets bruts** — la marque d'ordre des octets n'est pas décorative, PowerShell 5.1 lit sinon le fichier en ANSI et abîme tous ses accents.
- **Le canal : une ligne JSON par poste et par étape, dans un fichier, en ASCII pur.** Pas sur la sortie standard, que PowerShell 5.1 décode avec la page de code de la console. Et tout accent part en `\uXXXX` : le premier essai réel a affiché « vÃ©rifier » à la relecture parce que `Get-Content` sans `-Encoding` lit en ANSI. Le fichier était correct, sa lecture ne l'était pas — mais **un journal qu'on ne peut lire qu'en connaissant son encodage est un journal à moitié utile.** Les codes `etape` et `etat` sont invariants, la console fabrique la phrase.
- **Une ligne illisible est comptée, jamais effacée**, et un JSON *incomplet* en dernière position ne se dit pas comme un JSON *complet mais incohérent* : le premier peut être une écriture en cours, le second est un défaut où qu'il se trouve. La première version confondait les deux, et le test l'a montré avant tout usage réel.
- **Le réveil réseau n'est pas une étape inoffensive.** Il n'écrit rien sur la machine, mais il l'allume : trente postes qui démarrent parce qu'on a cliqué sur « vérifier » est un effet que personne n'a demandé. Un test tombe si `copie`, `installation` ou `parc` rejoignaient la liste des étapes autorisées.
- **La liste des postes passe par un fichier, pas par la ligne de commande** — une ligne de commande Windows a une longueur maximale que quelques centaines de noms dépassent, et l'échec serait arrivé le jour où le parc a grossi, pas le jour où on l'aurait compris. Un nom de poste ne contient que lettres, chiffres et traits d'union, et **ce qui est écarté est nommé** : une liste silencieusement raccourcie ferait croire à un déploiement complet.
- **Le bouton et ses garde-fous** : aucune case cochée au départ, plafond de cent postes par vérification, contrôle de la stratégie d'exécution avant de lancer quoi que ce soit, et une question qui dit aussi ce qui ne sera **pas** fait. Un journal horodaté par exécution, jamais effacé. « Aucune trace » ne se dit pas comme « prêt ».

**Trois leçons du 19/09/2026, toutes trouvées par l'essai et non par la relecture :**

- **Des accolades orphelines ne sont pas un bloc de code.** En retirant le `if` devant `{ Read-Host … }` sans retirer les accolades, on obtient `finally { { Read-Host } }` : PowerShell y crée un OBJET représentant du code, le jette, et la fenêtre se referme. Aucune erreur n'est levée, rien ne se voit à la relecture. *Assembler une commande par morceaux fabrique ce genre de faute ; écrire chaque forme en entier ne le fabrique pas.*
- **Une adresse qui ne peut pas servir ne doit pas être proposée** — lien-local, index de zone, boucle locale, et surtout la machine locale elle-même, qui répond par l'une de ses propres cartes, virtuelle aussi bien que physique. Mieux vaut rien qu'une valeur fausse : sans adresse, on saisit le nom, ce que le script recommande de toute façon.
- **Troisième dossier en deux jours écrit par le logiciel à côté du code sans être ignoré** — après `parametres.json` et `Journal/`, c'est `Deploiement/`. Le dossier de données du logiciel et le dossier de développement sont le même. *Tout nouveau chemin d'écriture ajouté au logiciel doit être ajouté au `.gitignore` dans le même geste.*

**Lot C ✔ — le déploiement réel, livré le 19/09/2026.** C'est la première fois que ce logiciel modifie autre chose que le poste sur lequel il tourne.

- **Le bouton et sa confirmation chiffrée.** Il faut RECOPIER le nombre de postes pour que « Lancer » s'active. Un « Oui / Non » se clique par réflexe — c'est même le geste le plus rapide quand on est pressé, donc exactement au mauvais moment. Recopier un nombre oblige à le lire, et lire « 47 » quand on croyait en avoir coché trois est la seule chose qui arrête la main à temps. La touche Entrée **annule**.
- **La fenêtre dit ce qui va se passer, avant.** Copie puis installation silencieuse, réveil réseau des machines éteintes, démarrage **durable** du service de gestion à distance, remplacement d'une version plus ancienne — et, selon la case, ce que le poste deviendra ou ne deviendra pas. *Une confirmation qui ne dit pas ce qu'elle confirme ne protège de rien.*
- **Plafond de cinquante postes**, contre cent en vérification : ici on modifie des machines, et cinquante est encore une liste qu'on peut relire avant de valider.
- **Le paquet est contrôlé dans la console, pas sur le poste distant.** Un chemin fautif découvert au milieu d'un lot laisserait la moitié du parc installée et l'autre moitié non. « Introuvable » et « illisible » ne se disent pas pareil : un partage injoignable lève au lieu de rendre faux, et le traduire en « introuvable » enverrait chercher au mauvais endroit. La version affichée est lue **dans le nom du fichier**, donc annoncée comme une indication et jamais comme un fait.
- **Le déploiement démarre WinRM quand il le trouve muet.** `sc.exe \\poste` pilote les services par le canal des partages Windows (445), pas par WinRM : c'est ce qui permet de démarrer le service quand le service ne répond pas. Jamais en vérification — la vérification ne modifie rien, c'est sa définition.
- **Les décomptes se font sur des codes, jamais sur les phrases affichées.** Compter des textes traduits ferait dépendre un bilan de la langue de l'interface.
- **« Aucune trace » ne se dit pas comme « en échec ».** Un poste que le script n'a pas atteint n'est ni installé ni en panne : on ne sait rien de lui, et c'est écrit tel quel.

**Deux décisions de fond, prises après discussion le 19/09/2026 :**

- **La vérification préalable prévient, elle n'interdit pas.** Exiger l'état « prêt » aurait bloqué les postes ÉTEINTS — précisément ceux que le déploiement sait réveiller. L'aperçu (« 9 prêts · 1 en échec · 2 non vérifiés ») sert à savoir avant de lancer, pas à empêcher. Et *« non vérifié » ne veut pas dire « en panne » : cela veut dire qu'on ne sait rien de ce poste.*
- **Le secret maître ne passe pas par la console.** Le script sait le lire dans un fichier ; ce serait l'écrire en clair sur le disque, même brièvement, et la règle « aucun mot de passe nulle part » tient depuis le début. Il est donc demandé UNE FOIS dans la fenêtre PowerShell, sans rien afficher, pour tout le lot. Corollaire : le script se sait « piloté » dès qu'on lui donne un journal JSON et ne pose plus aucune question de confort — **sauf celle-là**, qui n'en est pas une.

**Lot D ✔ — la reprise des seuls postes en échec, livrée le 19/09/2026.**

- **Le bouton PRÉPARE une sélection, il ne lance rien.** Reprendre un déploiement reste une décision, et elle se prend en cliquant sur « Déployer », avec sa confirmation chiffrée, comme la première fois.
- **Le tri des journaux se fait sur le NOM du fichier, pas sur sa date.** Le nom porte `aaaa-mm-jj_hhmmss`, des chiffres à largeur fixe : trié comme du texte, il est trié comme du temps. La date du fichier, elle, est réécrite par une copie, une restauration ou une sauvegarde — le « dernier » journal ne serait alors plus le dernier. Un test écrit l'ancien journal APRÈS le récent pour vérifier que c'est bien le bon qui gagne.
- **Le résultat n'est remis que sur les postes que le journal connaît.** L'appliquer aux autres les marquerait « aucune trace », ce qui serait faux : ils n'étaient simplement pas du lot.
- **Les échecs absents de la liste affichée sont comptés ET nommés**, avec la raison probable au survol. Sans ça, on croirait tout reprendre.
- **La vraie cause passe avant le symptôme.** Sans inventaire chargé, le bouton le dit au lieu d'annoncer « douze postes absents de la liste », ce qui aurait fait chercher du côté de l'unité d'organisation alors qu'il suffisait d'actualiser. Constaté au premier essai, le jour même.

**Le point 64 est écrit. IL N'EST PAS ÉPROUVÉ.** À la date du 19/09/2026, **aucune machine distante n'a jamais été touchée** : tout a été vérifié à blanc, plus une vérification réelle sur le poste de l'auteur, qui est aussi la console. Le premier déploiement réel — un seul poste, sur place, mode parc compris — est ce qui transformera « ça devrait marcher » en « ça marche ». **La 1.7.0 ne se publie pas avant.**

**Deux enseignements de mise en page, tirés de captures d'écran réelles :**

- **La colonne qu'on vient lire doit tenir dans la fenêtre.** La colonne « Vérification » était hors de l'écran : 1 150 pixels de colonnes pour une fenêtre de 960. Une fonctionnalité qu'il faut aller chercher en faisant défiler est une fonctionnalité à moitié absente.
- **Un message ne doit pas écraser ce qu'on regarde.** La barre d'état s'étalait sur cinq lignes. Deux lignes au maximum, et le texte complet au survol — le tableau est ce qu'on regarde.

---

## Points 65 à 71 — ce que deux rapports 1.6.0 ont montré

Le 18/09/2026, l'auteur envoie deux rapports produits par la 1.6.0 tout juste publiée, l'un en français sur `POSTE-01`, l'autre en anglais sur `POSTE-01-EN`, pour vérifier le texte des deux langues. Les deux machines sont saines. **C'est justement ce qui rend ces rapports utiles : sur une machine saine, tout ce que le logiciel signale est un faux positif.**

**65 — Un service à démarrage « à la demande » et arrêté est signalé comme devant tourner. FAIT le 18/09/2026, 1.6.1.**

`NlaSvc` — le service qui détermine si le réseau est un domaine, un réseau privé ou public — apparaît `Stopped` avec un mode de démarrage `Manual` sur **les deux** machines. Le rapport écrit « Il devrait tourner » et propose `sc.exe start NlaSvc`. C'est l'état normal d'un service à la demande : Windows le démarre quand un composant en a besoin et l'arrête ensuite.

La règle ne comparait qu'à une liste de services attendus, sans regarder leur mode de démarrage. Corrigé par `ServiceStateInfo.DoitTourner`, vrai seulement pour `Auto`/`Automatic`. Un service **automatique** arrêté reste signalé — c'est, lui, une anomalie.

**66 — Une carte Wi-Fi Direct virtuelle compte comme une carte sans fil. FAIT le 18/09/2026, 1.6.1.**

`Microsoft Wi-Fi Direct Virtual Adapter` positionne `IsWireless`. Sur un poste fixe sans radio, la conclusion « aucun réseau Wi-Fi enregistré » se serait déclenchée sur une carte qui n'existe pas physiquement. Défaut **latent** : il n'apparaît sur aucun des deux rapports, tous deux sur des machines qui ont une vraie carte sans fil.

Corrigé par deux conditions cumulées : `Win32_NetworkAdapter WHERE PhysicalAdapter = TRUE`, **et** des marqueurs de description (Wi-Fi Direct, VPN, hyperviseurs). Une seule des deux ne suffit pas — certaines cartes virtuelles se déclarent physiques.

**67 — Les états de service ne sont pas traduits. FAIT le 18/09/2026, 1.6.1.**

Le rapport français affichait `Running · Auto`. Deux libellés de plus, `StateLabel` et `StartModeLabel`, tous deux dans `Lang.T`.

**68 — Une date de fichier réécrite passe pour une mise à jour de pilote. FAIT le 18/09/2026, 1.6.1.**

Le rapport annonce « Pilotes mis à jour : … `afd.sys` : 10.0.26100.8875 → 10.0.26100.8875 … ». Une flèche entre deux fois le même numéro. `ScanHistory` stocke `« version|date de fichier »` et comparait la valeur **entière** ; Windows réécrit ces fichiers sans changer le pilote (réparation de l'image, restauration de composants).

**Ce n'est pas qu'un défaut d'affichage.** Cette liste alimente le point 55, qui conclut « le pilote accusé a été remplacé et les plantages ont continué, ce n'est donc pas lui ». Sur une fausse mise à jour, cette phrase **disculpe un pilote qui n'a jamais été remplacé** — l'inverse exact de ce que le point 55 existe pour faire.

**69 — Un plafond de collecte présenté comme un comptage. FAIT le 18/09/2026, 1.6.1.**

Le rapport titre « Erreurs disque répétées (500) ». 500 n'est pas un décompte : c'est la limite en dur d'`EventLogCollector`, atteinte. Le vrai total pouvait être 501 comme 40 000.

Un chiffre rond présenté comme une mesure fausse l'appréciation de la gravité, et **dans les deux sens** — c'est exactement ce que ce logiciel existe pour éviter. La limite porte désormais un nom (`MaxEvenementsParRequete`), le collecteur retient les catégories où la coupe a eu lieu, et le rapport écrit « au moins 500 » en nommant la limite. Appliqué aussi aux comparaisons entre deux scans, disque et WHEA.

**70 — Une seule carte pour des périphériques de natures différentes. FAIT le 18/09/2026, 1.6.1.** Demandé par l'auteur le 18/09/2026, en réponse à sa question « c'est des erreurs de clé USB ? ».

Les 500 erreurs de `POSTE-01` étaient trois choses sans rapport : 479 blocs défectueux sur un support **débranché depuis**, 20 réinitialisations d'un **port de contrôleur SATA**, une erreur de **système de fichiers** sur un volume. Un seul titre, une seule gravité, une seule recommandation — qui commençait par la gestion d'alimentation PCI Express et le firmware du SSD, inutile pour les trois.

Le point 60 avait séparé les *conseils* dans un texte commun. Il fallait séparer les *cartes*. Six natures de périphérique, lues sur le chemin `\Device\…` que Windows ne traduit jamais : disque monté, port de contrôleur, volume nommé, aucun périphérique nommé, support amovible, support absent. Une nature = une carte, sa gravité, son conseil.

Deux pièges rencontrés en chemin, tous deux verrouillés par un test :

- `HarddiskVolume3` commence par `Harddisk` : le motif historique `Harddisk(\d+)`, non ancré, l'aurait lu comme le disque numéro 3 — absent de l'inventaire, donc classé « support débranché ». Un volume monté aurait disparu du rapport.
- `FusionnerLesDoublons` recolle tout ce qui partage un identifiant de fait. Deux cartes sous `disk_event` auraient été refusionnées en une, défaisant la séparation. La première nature de l'ordre de priorité garde `disk_event`, les autres reçoivent le leur.

**71 — Un support débranché n'a pas de nom. FAIT le 18/09/2026, 1.6.1.** Demandé par l'auteur le 18/09/2026, même échange.

Le journal Windows n'écrit qu'un numéro de disque, et ce numéro est réattribué à chaque branchement : sur un support débranché depuis, le rapport ne pouvait dire que « un disque qui portait le numéro 1 ». Windows retient pourtant, dans `SYSTEM\MountedDevices`, quelle lettre a été montée par quel matériel, et dans `Enum\USBSTOR` le nom lisible des supports USB déjà vus.

Nouveau collecteur `StorageHistoryCollector`, en lecture seule, qui recoupe les deux. Trois décisions qui comptent plus que le code :

- **Le numéro de série n'est pas retenu.** Identifiant matériel unique, sans valeur diagnostique — même traitement que l'adresse MAC, déjà masquée.
- **Le rapport écrit « PISTE », jamais « identification ».** Rien ne relie une lettre de lecteur au numéro de disque qu'elle portait ce jour-là. Annoncer autrement ferait accuser un support au hasard.
- **« Pas pu regarder » ne se lit pas « rien trouvé ».** Lire `MountedDevices` demande des droits d'administrateur ; sans eux, la phrase dit pourquoi. Même principe que le `-1` des profils Wi-Fi.

---

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
2. ~~**30 — fraîcheur des données**~~ — **fait**. `Analysis/DataFreshness` relève l'âge du fait le plus récent, toutes sources confondues, et la couverture réelle de la période. Sans surveillance temps réel, la durée d'allumage est déclarée inconnue plutôt que déduite. Le plancher de comparaison, troisième volet, était déjà fait en 1.2.3.
3. ~~**29 — limiter ce que le mode simple affiche**~~ — **fait**. `Analysis/FindingDisplay` : tout le critique visible sans exception, plus le premier avertissement ; le reste replié à partir de deux éléments, avec son compte annoncé. Les blocs repliés se rouvrent à l'impression, ce qui corrige au passage deux `<details>` préexistants absents des PDF.
4. ~~**16 — hiérarchie du rapport pour un débutant**~~ — **fait, après recadrage**. Deux rapports réels du 19/08/2026 ont montré que le problème n'était pas la hiérarchie mais **la duplication** : la même erreur WHEA apparaissait deux fois, en avertissement depuis le journal de Windows et en critique depuis la surveillance — même matériel, même dernière occurrence, deux gravités contradictoires. `RulesEngine.FusionnerLesDoublons` tourne après toutes les règles et avant le tri ; trois identifiants de fait sont partagés entre les deux chemins (`whea`, `disk_event`, `exhaustion`). Le doublon devient un argument : le fait est confirmé par deux sources indépendantes.

**Ce que la 1.4 a aussi corrigé, trouvé en chemin et jamais dans cette liste :**

- **« 💡 Recommandation »** écrit en dur dans chaque conclusion du rapport anglais ;
- **le libellé du bouton de bascule** réécrit en français par le script au clic, l'exemption `pas-de-traduction` du littéral l'ayant rendu invisible ;
- **147 tailles en français** dans le rapport anglais (`Ko`, `Mo`, `Go`), plus le séparateur décimal — « 4,2 Go » se lit comme un séparateur de milliers pour un lecteur américain ;
- **cinq lignes de cette feuille de route** données comme ouvertes alors qu'elles étaient faites en 1.2.3, plus le point 26 fait depuis quatre versions.

Aucun de ces défauts n'a été trouvé en lisant une liste. Tous l'ont été en ouvrant un fichier ou un rapport réel.

**Fuites de traduction trouvées pendant la 1.4**, hors périmètre mais corrigées : « 💡 Recommandation » écrit en dur dans chaque conclusion, et le libellé du bouton de bascule réécrit en français par le script au clic. Les deux échappaient aux trois signaux — mot isolé sans accent pour la première, littéral exempté en bloc pour la seconde. Une liste nominative de faux amis a été ajoutée au test de rendu anglais.

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

### Bloc parc — TERMINÉ le 30/08/2026

Points **13, 14, 27, 7**. Armé en août « à déclencher sur un mot », déclenché le 29, livré en 1.5.0 et 1.5.1, et **éprouvé sur deux vraies machines la veille de la rentrée** : paquet réinstallable, service qui prend sa configuration sans redémarrage, règle de pare-feu posée par la ligne de commande, console qui trouve un poste par son seul nom Windows.

Trois des quatre points ont été faits ; le quatrième a été **fermé** parce qu'il reposait sur un fait faux (point 42). Ce qui reste du sujet « parc » n'est pas dans ce bloc : c'est l'archivage des alertes (43) et la question GLPI (44).

1. ~~**27 — token dérivé**~~ — **fait**. `RemoteConfig.TokenFor` : jeton inscrit prioritaire (dérogation pour les postes déployés avant), sinon dérivé, null quand aucun calcul n'est possible — et la console le DIT au lieu de signer avec une chaîne vide. Le secret maître vit chiffré par DPAPI dans `%LOCALAPPDATA%`, que les profils itinérants ne déplacent pas. La fenêtre « Mode réseau » du poste client dérive elle aussi, et n'affiche plus de jeton à recopier.

    **Ce qui reste à surveiller :** le jeton se calcule à partir du **nom Windows**. Le champ « Nom » de la console servait de libellé libre — un libellé de fantaisie produit un `403` muet. D'où : nom obligatoire quand le jeton est déduit, infobulle renvoyant à `hostname`, et message de refus nommant les trois causes (jeton, nom, horloge).
2. ~~**14 — ACL sur `remote.json`**~~ — **fait**. Aucune contrepartie à peser : les trois lecteurs du fichier sont le service (LocalSystem) et les deux exécutables, dont le manifeste porte `requireAdministrator`.
3. ~~**13 — `--configure-remote --generate-token`** en ligne de commande~~ — **fait**, et sous une meilleure forme : le poste dérive son jeton du secret maître au lieu d'en tirer un au sort.
4. ~~**7 — canal d'alerte adapté au parc**~~ — **fermé**, voir point 42. Le bloc parc est donc terminé.

---

### 1.6.0 — points 48 à 63, TERMINÉE le 18/09/2026

Thème : **un rapport doit nommer la panne qu'il a sous les yeux.**

Seize points, trois machines, deux semaines. Ils disent tous la même chose : *le logiciel collecte beaucoup plus qu'il ne conclut.* Le code d'arrêt était dans le journal de Windows. Le triplet PCIe était dans la boîte noire. L'âge du pilote Wi-Fi était dans le tableau des pilotes. Rien de tout cela n'est remonté jusqu'au verdict.

| # | Ce qui manque | Constaté sur |
|---|---|---|
| 48 ✔ | une erreur WHEA de port PCIe est attribuée au processeur, et « corrigée » est classé critique | S2-00-32-2025, 06/09 |
| 49 ✔ | le triplet bus/appareil/fonction est tronqué à l'écriture comme à l'affichage | S2-00-32-2025, 06/09 |
| 50 ✔ | le `BugcheckCode` de l'événement 41 n'est pas lu — quinze plantages invisibles | MLEAR-031-2024, 14/09 |
| 51 ✔ | l'échec d'écriture du vidage est compté comme une erreur disque | MLEAR-031-2024, 14/09 |
| 52 ✔ | les arrêts inattendus ne pèsent pas sur le verdict | MLEAR-031-2024, 14/09 |
| 53 ✔ | aucune section réseau | MLEAR-031-2024, 14/09 |
| 54 ✔ | un composant de Windows déclaré « désinstallé, sans objet » | PC-W10-11, 14/09 |
| 55 ✔ | la version du pilote fautif n'est pas comparée entre deux analyses | PC-W10-11, 14/09 |
| 56 ✔ | 178 vidages « en direct » listés, jamais exploités — dont 155 gels du moteur graphique | PC-W10-11, 17/09 |
| 57 ✔ | « 0 réinitialisation » écrit en présence de 155 réinitialisations | PC-W10-11, 17/09 |
| 58 ✔ | un gel suivi d'un écran bleu est une récupération ratée : rapprochement jamais fait | PC-W10-11, 17/09 |
| 59 ✔ | les plantages de `dwm.exe` écartés au lieu d'être rattachés à la panne graphique | PC-W10-11, 17/09 |
| 60 ✔ | une clé USB et un port de contrôleur comptés ensemble sous « erreurs disque » | PC-W10-11, 17/09 |
| 61 ✔ | deux dates différentes pour le même pilote, dans le même rapport | PC-W10-11, 17/09 |
| 62 ✔ | la boîte noire répète le même incident et laisse le gel passer pour une fin de tableau | PC-W10-11, 17/09 |
| 63 ✔ | le script de réparation raisonne par familles de panne, pas par mesures | PC-W10-11, 17/09 |

**Les seize points sont livrés**, en dix lots des 17 et 18/09/2026, chacun compilé et testé avant le suivant. Le projet est passé de 428 à 468 tests.

Les points 48 à 52 et 54 à 63 étaient des **corrections**, sur des données déjà collectées : rien à instrumenter, tout à relier. Le 53 était le seul ajout — et il en a demandé deux lots, un pour collecter les faits réseau, un pour en tirer la conclusion.

Ce que la version change, en une phrase : **le logiciel ne rend plus de verdict rassurant sur une machine qui s'arrête anormalement**, il nomme le composant au lieu de son rapporteur, et il écrit son script de réparation à partir de ce qu'il a mesuré.

Tranché le 18/09/2026 : les points **43** et **46** ne rejoignent PAS cette version. Ils vivent sur la console, pas sur le rapport d'une machine — les greffer ici aurait brouillé un thème qui tient tout seul. Ils passent en 1.7.0, dont c'est précisément le sujet.

---

## Point 15 — le bloc winget, plan arrêté le 19/09/2026

Validé sur le principe depuis le début, repoussé deux fois — par les notes de la 1.3.0, puis par le périmètre de la 1.4 (*« un logiciel qui vient de découvrir qu'il ne savait pas signaler ses propres pannes n'a pas besoin de surface supplémentaire »*). Les deux reports étaient justes. Le terrain est maintenant dégagé.

**Pourquoi 1.8.0 et pas 1.7.0.** La 1.7.0 est une version « parc », winget est une fonction « poste » : mélanger les deux donne une version dont on ne peut pas résumer ce qu'elle apporte. Surtout, **tout ce que fait le logiciel aujourd'hui est de la lecture.** Mettre à jour des logiciels, c'est agir sur la machine — redémarrer une application en cours d'usage, changer une interface sous les doigts d'un enseignant, saturer la bande passante un lundi matin. Ça mérite son propre thème et ses propres garde-fous, pas une section ajoutée en fin de version.

**Ce qui est établi, vérifié sur `winget --version` = v1.29.290 le 19/09/2026 :**

- **Aucune sortie exploitable par programme pour `upgrade`** — ni `--output`, ni `--format`, ni JSON dans la liste des options. Le point 24 reste donc obligatoire : découper **aux positions de l'en-tête**, jamais sur « deux espaces ou plus ».
- **Les deux boutons du point 15 correspondent à deux commandes réelles** : `winget upgrade --id <Identifiant> --exact` pour le choix par logiciel, `--all` (alias `-r`, `--recurse`) pour tout.
- **Quatre options à ne jamais passer par défaut** : `--allow-reboot` (sans elle winget ne redémarre pas la machine — c'est la seule protection contre un poste qui redémarre sous les doigts de quelqu'un), `--uninstall-previous` (si la nouvelle version échoue, le logiciel a disparu), `--force` (continuer malgré ce qui devrait arrêter), et `--ignore-security-hash`, qui ne doit même pas être proposée dans l'interface.
- **Trois options à passer, sinon la commande se bloque en silence** : `--disable-interactivity`, `--accept-package-agreements`, `--accept-source-agreements`. Plus `--silent` quand le paquet le supporte.
- **`--include-pinned` : jamais forcé.** Une épingle a été posée exprès. Le paquet s'affiche « épinglé » plutôt que de laisser croire qu'il est à jour.
- **`--proxy` existe**, ce qui compte dans un établissement filtré : les sources winget bloquées par stratégie sont un cas fréquent, déjà rencontré en 1.2.3.

**La question ouverte, à trancher par l'essai avant d'écrire quoi que ce soit dans le rapport :** `winget upgrade` liste les paquets de la machine **plus ceux du profil du compte qui l'exécute**. Or le logiciel demande les droits d'administrateur. Élever son propre compte ne change rien ; ouvrir en tant qu'**autre** administrateur ferait disparaître les logiciels installés « pour l'utilisateur courant ». Le rapport dirait « rien à mettre à jour » sur un poste qui en a dix. **Non vérifié à ce jour.**

---

## Points 72 à 78 — ce que la 1.6.1 a montré à son tour

Même méthode, même jour : un rapport produit par la version qu'on vient de publier, relu ligne à ligne. Sept points de plus, dont **deux créés par la 1.6.1 elle-même** — c'est le prix d'une version qui change la structure des conclusions, et il vaut mieux le payer tout de suite.

**72 — La phrase de comptage était en double, et la première était fausse. FAIT le 18/09/2026.**

La carte des 20 réinitialisations de contrôleur portait « Le journal Windows en contient davantage », qui se lit « il y en a plus de 20 ». **Rien ne l'établit.** Ce qui a buté sur le plafond, c'est la CATÉGORIE ; rien ne dit que la coupe a touché ces 20-là plutôt que les 479 autres. La phrase suivante disait déjà la chose correcte. Les deux fusionnent en une, qui ne parle que du total de la catégorie.

**73 — Une désinstallation affirmée sur une absence de preuve. FAIT le 18/09/2026.**

« MicrosoftEdgeUpdate.exe (27 crashs) — ce logiciel ne figure plus parmi les programmes installés », écrit pendant que **six processus `msedge` tournaient, listés dans le même rapport**. Deux fautes cumulées : la correspondance échouait sur un espace entre « Microsoft Edge » et « MicrosoftEdgeUpdate » ; et l'absence de la liste des programmes installés était présentée comme une preuve, alors que les composants de Windows, les applications du Microsoft Store et les logiciels portables n'y figurent pas. La comparaison se fait désormais sur les lettres et les chiffres seulement, et le texte dit ce qu'il constate sans conclure au-delà.

**74 — Les services en échec étaient comptés, pas nommés. FAIT le 18/09/2026.**

« Échecs de services Windows répétés (29) », suivi de « Consulter le détail dans la section Événements ». Le logiciel avait les 29 événements sous la main, chacun portant le service dans ses **données** — lues là, jamais dans la phrase du message, qui est traduite. C'est le reproche exact du thème de la 1.6.0, revenu sur une autre règle.

**75 — Les deux moitiés de la réponse n'étaient pas collées. FAIT le 18/09/2026.**

Un événement `Ntfs 55` nommait « le volume D: » ; la lecture du registre proposait, deux cartes plus loin, « D: (Kingston DataTraveler 3.0 USB Device) ». Le rapprochement n'est écrit que si la lettre est citée par les événements de cette carte, que le support est amovible et qu'il n'est pas monté aujourd'hui — et il est annoncé comme une piste plus serrée, jamais comme une preuve.

**76 — Deux services couvrant 28 échecs sur 29 étaient déclarés « dispersés ». FAIT le 18/09/2026.**

Le seuil demandait qu'**un seul** service pèse la moitié. `GLPI Agent` 14 fois, `TmWSCSvc` 14 fois, un troisième 1 fois : aucun n'atteignait la moitié, et le rapport concluait « aucun ne domine, ce qui désigne plutôt le système » en renvoyant vers `sfc` et `DISM` — **pour deux agents tiers**. La règle compte désormais combien de services il faut réunir pour couvrir les quatre cinquièmes : un ou deux, on les nomme ; trois ou plus, la dispersion est réelle.

**77 — Le point 65 avait ramené le mélange par une autre porte. FAIT le 18/09/2026.**

L'alerte de la surveillance temps réel citait « le volume D: » et se retrouvait collée à la carte du **port de contrôleur SATA**. La faute n'était pas dans la fusion des doublons : le point 65 a changé le sens de l'identifiant `disk_event`, qui désignait LA carte des erreurs disque et désigne désormais **la première nature présente**. Une leçon à retenir pour la suite : *changer le sens d'un identifiant partagé déplace le problème au lieu de le résoudre, tant qu'on n'a pas cherché qui d'autre s'y appuie.*

**78 — Un service qui ne démarre pas et un service qui meurt étaient dits pareil. FAIT le 18/09/2026.**

`7000`/`7001` — « n'a pas pu démarrer » — et `7031`/`7034` — « s'est terminé de manière inattendue » — ne se regardent pas au même endroit : le premier renvoie à son inscription et à son fichier, le second à son propre journal. La distinction se lit sur l'**identifiant** de l'événement, jamais sur sa phrase.

Et une règle posée par l'auteur ce jour-là, qui vaut au-delà de ce point : **« mettre en avant l'attention que l'utilisateur doit apporter, pas obligatoirement la solution quand il n'y en a pas »** — le logiciel doit valoir pour toutes les machines, pas décrire celle qui a servi à trouver le défaut.

### 1.6.1 — points 65 à 71, le lendemain de la 1.6.0

Thème : **ce qu'un rapport affirme, il doit pouvoir le soutenir.**

Sept points, tous nés du même geste : l'auteur envoie deux rapports de la 1.6.0 tout juste publiée, un dans chaque langue, sur deux machines **saines**. Sur une machine saine, tout ce que le logiciel signale est un faux positif — et c'est ce qui rend ces deux rapports plus utiles qu'une machine en panne.

| # | Ce qui n'allait pas | Constaté sur |
|---|---|---|
| 65 ✔ | un service à la demande arrêté signalé comme devant tourner (`NlaSvc`) | les deux machines, 18/09 |
| 66 ✔ | une carte Wi-Fi Direct virtuelle comptée comme carte sans fil — défaut latent | relecture du code, 18/09 |
| 67 ✔ | états et modes de démarrage des services affichés en anglais dans le rapport français | poste de l'auteur, 18/09 |
| 68 ✔ | `afd.sys : 10.0.26100.8875 → 10.0.26100.8875` — une date de fichier prise pour une mise à jour | poste de l'auteur, 18/09 |
| 69 ✔ | « Erreurs disque répétées (500) » — 500 était le plafond de collecte, pas un décompte | poste de l'auteur, 18/09 |
| 70 ✔ | une seule carte pour un support débranché, un port de contrôleur et un volume | poste de l'auteur, 18/09 |
| 71 ✔ | un support débranché ne pouvait être désigné que par un numéro qui ne vaut plus rien | poste de l'auteur, 18/09 |

Les points **65 à 69** sont des corrections, livrées sans qu'il soit besoin de les demander. Les points **70 et 71** sont deux ajouts, décidés le 18/09/2026 après la question « c'est des erreurs de clé USB ? » — question à laquelle le rapport ne permettait pas de répondre.

Deux d'entre eux valaient plus que leur apparence :

- Le **68** n'était pas un défaut d'affichage. La liste des pilotes changés alimente le point 55 : une fausse mise à jour y disculpait un pilote qui n'avait jamais été remplacé.
- Le **69** touche à ce que ce logiciel promet. Un plafond présenté comme une mesure fausse l'appréciation de la gravité dans les deux sens, et aucune relecture du rapport ne pouvait le détecter — le chiffre avait l'air d'un chiffre.

Les sept points sont livrés, en quatre lots du 18/09/2026, chacun compilé et testé avant le suivant. Le projet passe de 468 à **513 tests**.

Ce que la version change, en une phrase : **le logiciel ne signale plus d'anomalie sur une machine saine, et ne présente plus une limite technique comme un fait mesuré.**

### 1.7.0 — le parc entre dans le logiciel

Thème : **ce que l'administrateur fait à la main sur trente postes, l'outil doit savoir le faire et s'en souvenir.**

| # | Ce qui manque | Pourquoi maintenant |
|---|---|---|
| 64 | le déploiement se fait par un script distribué à part | quatre lots : la liste des postes, la vérification sans modification, le déploiement réel, le journal et la reprise |
| 43 | la console affiche les alertes et les oublie | un poste réimagé repart vierge ; on perd la preuve qu'il chauffait depuis six mois, au moment où elle servirait à le faire remplacer |
| 46 | `/api/flight` existe dans le service et personne ne l'appelle | après une coupure, voir ce que le poste a enregistré dans ses dernières minutes sans s'y rendre |

Les trois sont livrables séparément, et les trois vivent **sur la console, pas sur les postes** — c'est ce qui en fait une version cohérente. Le script publié sur palisser.fr n'est pas abandonné : il devient le moteur du point 64 et reste utilisable seul.

Cette version est volontairement tenue à l'écart de la 1.6.0 : son thème est l'action sur un parc, pas la qualité d'un rapport. Les mélanger aurait fait une version qui ne raconte rien.

## La question qui devrait décider de l'ordre

Ce découpage suppose que la localisation vient en premier. Ce n'est vrai que si rien d'autre n'a de date.

**Or la 1.3 attend un signal qui n'est pas venu** — le README anglais est en ligne depuis ce matin. Pendant ce temps, un déploiement de parc à la rentrée, lui, aurait une date.

Si tu comptes déployer sur ton établissement en septembre, alors le bloc **token + ACL + script GPO** (13, 14, 27) devient prioritaire sur la localisation, et devrait passer devant — la 1.3 deviendrait le déploiement, la 1.4 le texte.

C'est le calendrier qui doit trancher, pas les numéros de version.
