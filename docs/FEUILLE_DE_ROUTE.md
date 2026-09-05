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
| 1.5.2 | deux défauts constatés, sans nouvelle surface — la langue d'un rapport distant (point 45) et le lanceur `.bat` du script de réparation (point 36, moitié restante). 428 tests verts |

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

**38 — Deux conventions de nom de rapport. CORRIGÉ.** Trouvé le 30/08/2026 en relisant un rapport réel — pas le contenu du rapport, son **nom de fichier** : `Diagnostic_PC_2026-08-30_0907.html` sur une machine appelée `TECH-INFO-2025`.

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

**43 — Les alertes disparaissent avec le poste. À traiter avant un vrai déploiement.** Trouvé en fermant le point 7, et plus sérieux que lui.

Les alertes vivent **sur la machine**, dans `ProgramData\FaultTracePC\Flight\alerts.jsonl`. Un poste réimagé — opération routinière en établissement, souvent pendant les vacances — repart avec un journal vide. La console les collecte à chaque actualisation mais **ne les conserve pas** : elle les affiche et les oublie.

Conséquence concrète : on peut perdre la preuve qu'une machine chauffait depuis six mois, précisément au moment où elle servirait à justifier son remplacement. C'est aussi ce qui empêche toute vue dans la durée — « ce poste alerte trois fois plus que les autres » est une phrase que la console ne peut pas dire aujourd'hui.

Piste : la console archive ce qu'elle collecte, dans son dossier à elle, par machine et par règle. Rien à changer sur les postes. *Difficulté : faible à moyenne.*

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

**46 — « Voir en temps réel pourquoi ça coupe » : le besoin est bon, la réponse n'est pas l'actualisation automatique.** Demandé par l'auteur le 31/08/2026 en découvrant que la console n'actualise que sur clic.

**Pourquoi le direct ne répondrait pas à la question.** Au moment de la coupure, la machine s'arrête et le réseau avec elle : la console ne verrait rien de plus. Une actualisation toutes les deux secondes afficherait une valeur vieille de deux secondes, puis « injoignable ». Elle donnerait l'**illusion** d'une réponse — exactement le genre de fonctionnalité qui rassure sans informer.

**Ce qui répond déjà à la question**, et c'est la raison d'être de la boîte noire : le service relève un échantillon toutes les 10 s et l'écrit **avec écriture forcée sur le disque**. Les dernières secondes avant la coupure survivent donc à la coupure. Le moteur de règles les exploite (`AnalyzeFlightRecorder`) et le rapport écrit la phrase — « le processeur était à 97 °C juste avant l'arrêt ». La réponse existe : elle arrive après, parce que c'est le seul moment où elle peut arriver.

**Ce qui manque vraiment, et qui est à moitié construit :** le point d'accès **`/api/flight?minutes=60` existe dans le service et n'est appelé par personne**. La console ne sait donc pas montrer la boîte noire d'une machine distante. C'est ça, la fonctionnalité à écrire : après une coupure, sélectionner le poste et voir ce qu'il a enregistré dans ses dernières minutes — températures, charge, mémoire — sans avoir à s'y rendre ni à relancer une analyse complète.

**Limite à énoncer dans l'interface** : 10 secondes entre deux relevés. Pour une surchauffe, qui monte en minutes, c'est largement suffisant. Pour un pic de charge instantané, on peut passer à côté. Le dire vaut mieux que laisser croire à un enregistrement continu.

**L'actualisation automatique reste souhaitable** — mais comme confort, pas comme réponse : une case « actualiser toutes les N secondes », désactivée par défaut, en sachant qu'interroger vingt postes en boucle a un coût réseau. *Difficulté : faible pour l'actualisation, moyenne pour la vue boîte noire distante.*

**47 — Colonne « Top processus » vide deux fois sur trois. CORRIGÉ.** Constaté dans la console le 31/08/2026. La boîte noire ne relève les processus qu'un échantillon sur trois — toutes les 30 s, pour ne pas grossir le journal — et `/api/status` renvoie le **dernier** échantillon, qui n'en porte donc généralement pas. La colonne se remplissait au hasard, ce qui est pire qu'une colonne toujours vide : on ne sait pas si l'information manque ou si la machine n'a rien à signaler. `BuildStatus` complète désormais avec le relevé le plus récent qui en contienne un — au pire 30 secondes d'âge, sans conséquence pour la question posée.

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

## La question qui devrait décider de l'ordre

Ce découpage suppose que la localisation vient en premier. Ce n'est vrai que si rien d'autre n'a de date.

**Or la 1.3 attend un signal qui n'est pas venu** — le README anglais est en ligne depuis ce matin. Pendant ce temps, un déploiement de parc à la rentrée, lui, aurait une date.

Si tu comptes déployer sur ton établissement en septembre, alors le bloc **token + ACL + script GPO** (13, 14, 27) devient prioritaire sur la localisation, et devrait passer devant — la 1.3 deviendrait le déploiement, la 1.4 le texte.

C'est le calendrier qui doit trancher, pas les numéros de version.
