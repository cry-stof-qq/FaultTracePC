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
| 1.7.0 | **jamais publiée** — construite et éprouvée sur un parc réel les 19, 20 et 21/09/2026. Cinq défauts en sont sortis, dont trois qu'aucune relecture de code n'aurait trouvés. Publier 1.7.0 puis 1.7.1 à trois jours d'intervalle n'aurait rien appris à personne |
| 1.7.1 | **publiée** le 21/09/2026 — thème **le parc entre dans le logiciel** — points 64 (déploiement, quatre lots) ✔, 43 (archivage des alertes) ✔ et 46 (boîte noire distante) ✔. C'est la 1.7.0 corrigée : voir « La matinée du 21/09/2026 », défauts A à E |
| 1.8.0 | **publiée le 10/10/2026** — release créée par le travail « Publication » sur le tag `v1.8.0` (commit `32e924d`) ; empreintes publiées : MSI `1eb0a293…5ade09`, ZIP `dbfd4caf…62d60b8`. Articles palisser.fr mis à jour le même jour : article 30 (français) et 31 (anglais) — section « Nouveau en 1.8.0 », version actuelle, commandes `msiexec` ; articles 32, 33 et 34 vérifiés, sans mention de version à changer. — publication décidée le 10/10/2026 — par le tag `v1.8.0` : c'est le travail « Publication » de GitHub qui construit le MSI et le ZIP à partir du commit tagué, et qui ajoute lui-même les sommes SHA-256 au corps de la release. Les fichiers essayés sur les deux postes ont été construits localement à partir des mêmes sources ; ce ne sont pas, octet pour octet, ceux qui sont publiés. (Erreur corrigée le même jour : une section SHA-256 calculée sur les fichiers locaux avait été ajoutée aux notes — elle aurait donné des empreintes fausses pour les fichiers publiés, et a été retirée avant le tag.) — **en préparation de publication depuis le 10/10/2026** (numéro de version passé à 1.8.0, notes de version écrites ; MSI construit et **installé par-dessus la 1.7.1 sur le poste de l'auteur le 10/10/2026** — version affichée 1.8.0, service redémarré, rapport complet avec toutes les nouvelles cartes et conclusions ; un défaut trouvé et corrigé à cette occasion, voir point 86. **Essai sur un poste en mode parc réussi le 10/10/2026** : poste hors domaine en 1.7.1, mis à jour en 1.8.0 par installation locale du MSI — réglage du mode parc présent avant et après, service redémarré, console : version 1.8.0, « surveillance active », boîte noire ouverte à distance. **Confirmation faite le 12/10/2026** depuis la console, sur les postes du domaine : les 8 postes joignables sur 10 affichent la version 1.8.0 et « surveillance active » ; les deux autres étaient éteints (« délai dépassé »), dont un poste hors de l'établissement. Constaté en préparant la construction : `build\publish.ps1` portait encore `1.6.2` comme version par défaut de l'archive portable — passé à 1.8.0) — en cours depuis le 09/10/2026 — thème **le rapport rapproche ce qu'il sait** — points 80 à 87, ce qu'un rapport sur une machine inconnue a montré. Décidé le 09/10/2026 : passe AVANT winget, parce qu'il touche à la promesse même du logiciel — trouver la cause d'une panne — et sert tous les utilisateurs, pas seulement un parc |
| 1.8.1 | **publiée le 12/10/2026** — release créée par le travail « Publication » sur le tag `v1.8.1` (commit `eacbd0d`). Point 90 vérifié le même jour après publication de l'essai : sur le poste du parc mis à jour, relevés à 16:54:57, :47, :37, :27… — 10 secondes exactement. Point 98 **vérifié le 17/10/2026** sur le poste de l'auteur : une erreur WHEA réelle (09:32:25) a été archivée par la console avec le niveau « warn » (« à surveiller ») et le numéro d'événement 17 (`va`), là où les précédentes, écrites par la 1.8.0, étaient toutes « crit ». Articles palisser.fr mis à jour le même jour : article 30 (français) et 31 (anglais) — section « Nouveau en 1.8.1 » / « New in 1.8.1 » au-dessus de celle de la 1.8.0, version actuelle 1.8.1, quatre commandes `msiexec` passées à 1.8.1 ; vérifié sur les pages publiques. — en cours depuis le 12/10/2026 — thème **un rapport de parc qui ne se trompe pas** — correctifs constatés sur le rapport du parc du 12/10/2026, dans cet ordre : 95 (conseil de divergence) → 98 (gravité des erreurs WHEA) → 97 (postes comparés nommés) → 92 (barre d'état coupée) → 90 (horloge fixe de 10 s). Ordre et répartition choisis le 12/10/2026, l'auteur ayant donné son accord sur tous les points et laissé l'ordre au choix : d'abord ce qui donne un conseil faux ou une alarme exagérée, puis ce qui manque au rapport, puis le confort. **Les cinq lots livrés le 12/10/2026** (95, 98, 97, 92, 90). **En préparation de publication depuis le 12/10/2026** : numéro de version passé à 1.8.1 (projets App, Cli et Monitor, `build\publish.ps1`, `installer\build-msi.ps1`), notes de version écrites (`docs/releases/RELEASE_NOTES_v1.8.1.md`, sans section SHA-256 : c'est le travail « Publication » qui l'ajoute sur ses propres fichiers). **Essai du 12/10/2026** : MSI 1.8.1 installé sur le poste de l'auteur (version 1.8.1 affichée, service en cours d'exécution) et déployé sur un poste du parc (celui des erreurs WHEA). Vérifiés sur pièce : point 92 (barre d'état lisible en entier), point 95 (le poste le plus à jour n'est plus désigné en retard ; ce sont les deux autres qui sont nommés), point 97 (« Comparaison faite sur 3 poste(s) sur 10 », sept postes nommés avec leur raison, dont trois « jamais analysé »). Non vérifiables sur le moment : point 98 (aucune nouvelle erreur WHEA depuis la mise à jour — la dernière alerte archivée date d'avant), point 90 (la boîte noire montrée était celle d'un poste resté en 1.8.0, où les relevés tombaient déjà toutes les 10 secondes ; l'écart de 12 secondes du 10/10/2026 avait été vu sur un autre poste). Relevé au passage : dans la Supervision, la colonne « Version » coupe « 1.8.0 — à mettre à jour » (point 100) |
| 1.9.0 | **publiée le 19/10/2026** (21:34, heure de Paris) — release créée par le travail « Publication » sur le tag `v1.9.0` (commit `277ee58`), vérifiée le même soir : deux fichiers, notes complètes dans l'ordre prévu, empreintes publiées : MSI `8b0cc2c214cc0e75b9e712c1a79bf99154b945cb73e41a945d8005720eba4583`, ZIP `c43056ed7adeb9765b5e174e97e515e6eef3ed08130b4acb15b80508f60c8473`. MSI publié téléchargé par l'auteur le 19/10/2026 et vérifié : empreinte identique à celle de la release. Articles palisser.fr mis à jour le 19/10/2026 : article 30 (français) et 31 (anglais) — section « Nouveau en 1.9.0 » / « New in 1.9.0 » au-dessus de celle de la 1.8.1, version actuelle 1.9.0, cinq commandes `msiexec` passées à 1.9.0, passage « raccourci du Bureau » réécrit pour la console de parc (une liste `ADDLOCAL` installe exactement ce qu'elle nomme ; trois formes : `Main,DesktopShortcutFeature`, `Main`, `Main,ParkFeature`), phrase sur la case « Mode parc » après « cochés par défaut », exigence d'un Windows 10 ou 11 à jour en tête de « À savoir avant d'installer » ; vérifié sur les deux pages publiques. Correction utile au passage : la page publiait `ADDLOCAL=Main` sans mise en garde, forme qui, en 1.9.0, retire la console d'un poste d'administration à la mise à jour. L'article 30 a été enregistré par Claude ; pour le 31, l'enregistrement a été refusé par l'outil de contrôle du navigateur, le texte a été rempli par Claude et l'auteur a cliqué « Enregistrer ». Parc déployé le 20/10/2026 avec le MSI PUBLIÉ : paquet copié sur le partage, chemin du paquet changé dans les réglages de la console (il désignait encore le MSI 1.8.1), postes mis à jour depuis la console — tous sauf un poste, dont le nom n'est pas écrit ici (dépôt public). Vérifié juste avant le tag, avec le MSI reconstruit : diagnostic 1.9.0 du poste de l'auteur, la carte des services dit « Les 1 autre(s) échec(s) sont trop peu nombreux pour désigner un coupable » et recommande de terminer la désinstallation — **en préparation de publication depuis le 19/10/2026** (go de l'auteur le même jour) — lot 1 : numéro de version passé à 1.9.0 (projets App, Cli et Monitor, `build\publish.ps1`, `installer\build-msi.ps1` et ses exemples) ; lot 2 : notes de version relues et complétées (en-tête « brouillon » retiré, résumé anglais étendu, section ajoutée pour les points 93, 94 et 100, correctifs regroupés sous « Le rapport d'un poste : des conseils alignés sur ce qui est mesuré ») ; lot 3 : MSI (63,4 Mo) et archive portable construits sur le poste de l'auteur pour les essais — empreintes SHA-256 de ces fichiers d'essai : MSI `E679AAE3…E1F18D`, ZIP `F16D41AC…0BD3DF` (les fichiers publiés seront reconstruits par le travail « Publication » à partir du tag, avec d'autres empreintes) ; lot 4 **réussi le 19/10/2026** dans la VM propre (Windows 11 26H2) : installation neuve du MSI 1.9.0 sans la case — boutons « Parc » et « Réseau » absents ; pilote de test créé par `sc.exe create`, puis diagnostic 1.9.0 : la carte des inscriptions orphelines cite « pilote « FaultTracePC test orphelin » (FTPCTestOrphelin), démarrage Auto » (point 103 vérifié en vrai) ; ajout de la case par « Modifier » — les deux boutons apparaissent ; rapport signé « v1.9.0 ». Relevé dans ce rapport, sans rapport avec la publication : la fiche « Cartes réseau » liste comme cartes distinctes les couches de filtrage d'une même carte (« …-WFP Native MAC Layer LightWeight Filter-0000 », « …-QoS Packet Scheduler-0000 »…), sans adresse — du bruit, à examiner après la publication ; devenu le point 111, corrigé le 20/10/2026 ; lot 5 **réussi le 19/10/2026 au soir** : console de l'auteur mise à jour par-dessus la 1.8.1, case « Mode parc » cochée d'office, tout visible ; deux postes du parc mis à jour en 1.9.0 par le clic droit « Mettre à jour ce poste… » (les autres s'éteignent à 21:00) ; diagnostic à distance en 1.9.0 sur l'un d'eux : verdict « MATÉRIELLE » avec la mention des plantages antérieurs (point 105), 34 arrêts de l'agent d'inventaire mis de côté à l'arrêt de Windows (point 109), service sans programme mis à part (point 104) ; rapport du parc : 8 cartes de divergence au lieu de 175 (point 96), carte WHEA sur deux postes (point 99). Trois incohérences relevées dans ce rapport et corrigées le même soir avant le tag : la carte WHEA disait « transmis que par les postes en 1.9.0 » pour un poste DÉJÀ en 1.9.0 dont le dernier diagnostic était antérieur — elle nomme désormais les postes sans modèle et dit de relancer leur diagnostic ; le résumé d'un poste comptait comme « problèmes critiques » les conclusions antérieures à la période — la carte « poste isolé » désignait un poste pour 4 problèmes dont 3 vieux de plusieurs mois ; `KslD.sys`, pilote de Defender, restait seul sur sa carte. Quatrième, relevée sur le diagnostic 1.9.0 du poste de l'auteur le même soir et corrigée aussi : après la mise à part d'un service sans programme (8 échecs), il restait UN échec d'un autre service, que la carte disait « à traiter en premier » — le reste doit désormais atteindre le seuil de la carte (5) pour désigner un coupable, sinon la carte dit qu'il est trop peu nombreux. Relevé, non corrigé (après la publication) : la section « Évolution depuis le dernier scan » range sous « Pilotes mis à jour » un fichier passé à une version plus ANCIENNE (10.0.26100.9549 → 10.0.26100.8875) — devenu le point 110, corrigé le 20/10/2026 ; à suivre : construction du MSI et de l'archive, essai en VM propre (installation neuve, case « Mode parc », pilote de test du point 103), essai de mise à jour sur un poste du parc, puis tag. Contenu terminé le 19/10/2026 : points 79, 93, 94, 96, 99, 100, et correctifs 101 à 109 — prévue — thème **deux publics, un seul paquet** — point 79 : le mode parc devient une case à cocher de l'installation, absente par défaut, **sans qu'un poste déjà en mode parc le perde**. Décidé le 10/10/2026 : juste après la 1.8.0 et **avant winget**, pour que les particuliers qui installeront par winget n'aient déjà plus le mode parc. **Avec les points 93 et 94** (console : actualiser seulement la sélection, mettre à jour un seul poste par clic droit), placés le 12/10/2026 : la 1.9.0 touche déjà au déploiement par la console (le paquet devra y demander le mode parc), le même chemin n'est donc éprouvé qu'une fois. **Avec aussi les points 96 et 99** (comparateur : regrouper les pilotes de Windows et de Defender, rapprocher une même alerte sur plusieurs postes), placés le 12/10/2026 : ce sont des ajouts au comparateur, pas des correctifs |
| 1.9.1 | **publiée le 21/10/2026** — release créée par le travail « Publication » sur le tag `v1.9.1` (commit `366a727`), vérifiée le même jour sur la page de la release : marquée « Latest », quatre fichiers (MSI, archive portable, deux archives de sources), notes complètes, empreintes publiées : MSI `ecb34e2ac6440a1c4cf86f24cb820cc7df2bf85f6c2bb255719b6d75831913cb`, ZIP `1bf1a3728f5caa18524e00a04bcfd824672e9a081462a3e120146429d9bcd7fe`. Les notes ne donnent pas de nombre de tests : le tag a été posé avant qu'il soit relevé. Articles palisser.fr mis à jour le 21/10/2026 par Claude, les deux enregistrés sans blocage : article 30 (français) et 31 (anglais) — section « Nouveau en 1.9.1 » / « New in 1.9.1 » au-dessus de celle de la 1.9.0 (cinq corrections en liste, et le rappel qu'il faut mettre à jour les postes), version actuelle 1.9.1, cinq commandes `msiexec` passées à 1.9.1 ; vérifié sur les deux pages publiques. Parc déployé le 21/10/2026 avec le MSI PUBLIÉ : paquet copié sur le partage, chemin changé dans la console, postes mis à jour par la sélection. Vérifié le même jour sur trois diagnostics 1.9.1 : sur un poste B760 (celui des 121 erreurs WHEA), la carte WHEA est en avertissement et le verdict ne dit plus « MATÉRIELLE » ; sur le portable, une seule carte pour les trois coupures, avec la phrase sur l'événement 6008 et le bouton maintenu (point 114 vérifié en vrai). Mais le verdict de ce poste B760 est devenu « LOGICIELLE (corruption système ou application) », à cause d'une alerte de saturation de la mémoire virtuelle : voir le point 115. — en préparation de publication depuis le 21/10/2026 (go de l'auteur le même jour) — thème **ce que neuf diagnostics réels ont montré** — correctifs seulement, points 110 à 114, aucune fonction nouvelle, installation identique à la 1.9.0. Décidé le 21/10/2026 de ne pas attendre d'autres correctifs, à cause du point 112 : le verdict était faux sur cinq postes sur neuf (« MATÉRIELLE » pour des erreurs WHEA toutes corrigées), et le défaut ne se serait éteint seul qu'à la mi-novembre. Lot 1 : numéro de version passé à 1.9.1 (projets App, Cli et Monitor, `build\publish.ps1`, `installer\build-msi.ps1` et ses exemples ; la mention historique « MODE PARC (1.9.0 et après) » est gardée). Lot 2 : notes de version écrites (`docs/releases/RELEASE_NOTES_v1.9.1.md`, sans section SHA-256 : c'est le travail « Publication » qui l'ajoute). Lot 3 **réussi le 21/10/2026** : MSI 1.9.1 construit et installé sur le poste de l'auteur par-dessus la 1.9.0, diagnostic de contrôle à 11:51 — rapport signé « v1.9.1 » ; carte « 6 erreur(s) matérielle(s) CORRIGÉE(S) — lien PCI Express » en **avertissement** (critique la veille, en 1.9.0, avec les mêmes données) ; verdict « Pas de panne critique, mais des points de vigilance » au lieu de « MATÉRIELLE » (point 112 vérifié en vrai) ; fiche « Cartes réseau » : 19 interfaces puis « et 64 couche(s) de filtrage posée(s) sur ces cartes… » (point 111 vérifié en vrai). Relevé en relisant ce rapport : 10 des 19 interfaces sont des interfaces cachées de Windows (WAN Miniport, RAS, débogueur du noyau…) que PowerShell 5.1 ne montre pas — elles ne sont pas des couches de filtrage et restent listées ; les replier aussi serait un changement de comportement, à proposer après la publication. Tag `v1.9.1` posé le 21/10/2026 sur `366a727`, CI verte (compilation et tests, construction de l'installateur, publication). Pas d'essai en VM : rien ne change à l'installation |
| 1.10.0 | prévue — thème **mettre à jour sans surprise** — point 15, le bloc winget : voir le plan arrêté le 19/09/2026. Décalé d'une version le 09/10/2026, puis d'une autre le 10/10/2026 (point 79 placé avant) |

**Fait en 1.3.0 :** réglage de langue de portée machine (`ProgramData\FaultTracePC\langue.txt`, propriété MSI `FTPCLANG`, `--set-machine-lang`) ; alertes préventives refabriquées à la lecture à partir de la règle et de la valeur.

**Reporté de la 1.3.0 en 1.4.0**, décidé le 17/08/2026 : donner un identifiant stable aux 32 conclusions du moteur de règles, pour que `Historique\*.json` cesse de stocker des titres en clair — la console de parc les réaffiche, et une console anglaise lit donc des titres français. C'est un contrat entre versions, pas un rangement.

**Reporté de la 1.3.0 en 1.4.0**, décidé le 17/08/2026 : persister le rapport complet (JSON à côté du HTML) pour pouvoir le régénérer dans une autre langue — ou avec un générateur plus récent. Écarté de la 1.3 parce que le déclencheur n'existe pas : `Lang.Apply` n'est appelé nulle part à l'exécution, la langue est fixée au démarrage et le sélecteur redémarre l'application. Une régénération « si la langue a changé » serait du code mort.

---

## Règle d'écriture — aucune donnée réelle dans ce dépôt

Ce dépôt est **public**. Rien de ce qui identifie l'établissement, son réseau ou ses machines ne doit y entrer — et la règle, posée le 20/09/2026, est qu'on **écrit directement anonymisé** plutôt que de nettoyer après coup : une donnée réelle poussée une fois reste dans l'historique, et l'effacer coûte une réécriture d'historique disproportionnée.

| Ce qui ne s'écrit jamais | Ce qu'on écrit à la place |
|---|---|
| un secret, un jeton, un mot de passe | des `*`, jamais la valeur ni un extrait |
| une adresse IP **publique** réelle | `203.0.113.x` (RFC 5737) — c'est la ligne à ne jamais franchir |
| une adresse IP privée réelle | `192.0.2.x` ou `198.51.100.x`, ou une valeur d'exemple sans rapport |
| un nom de poste réel | un pseudonyme stable : `POSTE-01`, `POSTE-ELEVE-01`, `ATELIER-07` |
| un nom de serveur réel | `SRV-FICHIERS`, `SRV-DHCP`, `SRV-AD` |
| le domaine Active Directory | `exemple.local`, ou `<domaine>` |
| un chemin de partage réel | `\\serveur\partage` |
| une adresse MAC réelle | `AA-BB-CC-DD-EE-FF` |

**Les adresses PUBLIQUES d'abord**, précision de l'auteur le 20/09/2026. Une adresse privée qui fuit ne dit presque rien : `10.x` et `192.168.x` sont les mêmes chez tout le monde, et rien ne permet d'y rattacher un établissement. Une adresse **publique**, elle, désigne un site, un abonnement, un pare-feu — c'est elle qui transforme un dépôt de code en carte d'identité. Les deux se masquent, mais on ne se trompe pas sur la hiérarchie : une adresse privée oubliée se corrige au prochain passage, une adresse publique poussée est une fuite.

**Pourquoi pas `127.0.0.1` pour remplacer une adresse**, alors que c'est le réflexe habituel : dans CE logiciel, `127.0.0.1` a un sens précis et fonctionnel — c'est la boucle locale, le seul cas que le premier verrou du service accepte sans contrôle de plage, et c'est une valeur réellement utilisée dans la console pour superviser la machine locale. L'employer comme bouche-trou ferait lire « la machine distante est la machine locale », ce qui est faux et trompeur. Les plages RFC 5737 existent exactement pour cet usage et ne peuvent être confondues avec rien.

**Deux pseudonymes sont posés une fois pour toutes** dans le code, les tests et ce document. Ils désignent deux machines réelles dont les rapports ont fourni la matière des points 48 à 71 :

- **`POSTE-TEMOIN`** — le poste à l'origine du projet, celui des rapports des 14 et 17/09/2026 ;
- **`POSTE-ELEVE-01`** — le poste élève du 14/09/2026, à l'origine des points 50 à 53.
- **`POSTE-SALLE-02`** — le premier poste installé à distance par le script de déploiement, le 06/09/2026, à l'origine des points 48 et 49.

Les garder stables importe autant que les anonymiser : c'est ce qui permet de relier entre eux, six mois plus tard, un commentaire de code, un test et une ligne de cette feuille de route.

**Ce qui n'est pas concerné** : les plages `10.0.0.0/8`, `172.16.0.0/12` et `192.168.0.0/16` écrites dans la règle de pare-feu et dans le contrôle du service, ainsi que `127.0.0.1` au même endroit. Ce ne sont pas des exemples, ce sont les **valeurs de fonctionnement** du logiciel ; les remplacer casserait le produit.

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
| 79 | **Le mode parc devient une fonctionnalité facultative du paquet** | décidé le 21/09/2026 ; **placé en 1.9.0 le 10/10/2026**, avant winget — voir « Deux publics, un seul paquet » plus bas |
| 80 | **Lancer WinDbg quand il vient du Microsoft Store** | constaté le 24/09/2026 — **✔ terminé le 09/10/2026** : lot A (le rapport dit pourquoi le pilote n'est pas nommé), lot B (la fenêtre après analyse), lot C (mesuré : le WinDbg du Store se lance sur un autre poste, le bouton d'installation reste tel quel) — voir « Ce qu'un rapport sur une machine inconnue a montré » |
| 81 | **Dire quel antivirus protège réellement la machine** | reformulé le 24/09/2026 après vérification sur la machine : la liste des processus ne suffit pas — **✔ terminé le 09/10/2026** : lecture, carte « Protection antivirus », trois conclusions, voir la section |
| 82 | **Confronter le verdict aux mesures qui le contredisent** | constaté le 24/09/2026, donnée déjà collectée — **✔ terminé le 10/10/2026** : stockage contre SMART, mémoire contre diagnostic mémoire |
| 83 | **Regrouper les plantages dans le temps et nommer les amas** | constaté le 24/09/2026, donnée déjà collectée — **en cours** : lot 1 (séries) livré le 10/10/2026 |
| 84 | **Exploiter les dates de pose des pilotes** | constaté le 24/09/2026, donnée déjà collectée — **lot 1 livré le 10/10/2026** (pilotes datés du début d'une série) |
| 85 | **Lister les logiciels installés dans le rapport** | constaté le 24/09/2026 — un antivirus désinstallé de la veille était invisible — **✔ terminé le 10/10/2026** : logiciels au début d'une série, liste repliée, services et pilotes sans programme, pilotes-filtres |
| 86 | **Charge processeur absente de la boîte noire sur une machine** | constaté le 24/09/2026, cause non établie — **lot 1 (instrumenter) livré le 10/10/2026** ; la suite attend une machine concernée |
| 87 | **Lire le réglage de vidage mémoire avant de recommander de le changer** | constaté le 24/09/2026 — le réglage était celui d'origine — **✔ terminé le 10/10/2026** |
| 88 | **Réveiller un poste par le réseau depuis la console, sans rien déployer** | demandé le 05/10/2026, choix arrêtés le même jour (bouton dans les deux onglets, confirmation), à faire plus tard ; redemandé le 19/10/2026 pour un poste **ou une sélection**, depuis le clic droit de la 1.9.0 — voir « Point 88 » plus bas |
| 89 | **Voir qu'un pilote a disparu, et lequel** | demandé le 09/10/2026 — **noté pour plus tard, rien n'est commencé** ; quatre angles morts vérifiés dans le code ; cas réel (carte graphique sans pilote) et mesure sur machine saine notés, voir « Point 89 » plus bas |
| 90 | **Le relevé « toutes les 10 secondes » a lieu toutes les 12 secondes environ** | constaté le 10/10/2026 sur la boîte noire distante d'un poste — **décidé** : caler le service sur une horloge fixe, après la publication de la 1.8.0 ; voir « Point 90 » plus bas |
| 91 | **Colonne « T° CPU » vide sur tous les postes de la console** | constaté le 12/10/2026 : vide sur les 8 postes joignables, alors que la colonne « T° GPU » est remplie sur 5 d'entre eux. **Cause non établie** : processeurs qui n'exposent pas leur sonde (fréquent sur les portables, voir point 86), ou lecture impossible par la bibliothèque de capteurs — aucune n'est vérifiée. À établir avant toute correction : la colonne était-elle remplie avant la 1.8.0 ? puis, comme au point 86, faire dire au service pourquoi la mesure manque |
| 92 | **Texte de la barre d'état de la console coupé** | constaté le 12/10/2026 sur la console en 1.8.0 : « Tous les postes joignables son… » — défaut d'affichage, à corriger avec le prochain lot |
| 93 | **Actualiser seulement les postes sélectionnés dans la Supervision** | demandé le 12/10/2026 — **placé en 1.9.0** ; voir « Points 93 et 94 » plus bas |
| 94 | **Mettre à jour un seul poste par un clic droit dans la Supervision** | demandé le 12/10/2026 — **placé en 1.9.0** ; le menu reprend aussi boîte noire, historique des alertes, diagnostic à distance et « Actualiser ce poste » (validé le même jour) ; voir « Points 93 et 94 » plus bas |
| 95 | **Divergence de version : le conseil peut dire de revenir en arrière** | constaté le 12/10/2026 — **défaut** : le poste le plus à jour est désigné « en retard » ; voir « Rapport du parc du 12/10/2026 » plus bas |
| 96 | **Divergence de version : 88 cartes, dont 80 pour un seul poste** | constaté le 12/10/2026 — bruit : pilotes de Windows et de Defender comptés un par un ; voir « Rapport du parc du 12/10/2026 » plus bas |
| 97 | **Le rapport du parc ne dit pas quels postes il a comparés** | constaté le 12/10/2026 — comparaison faite sur 3 postes sur 10 sans le nommer ; voir « Rapport du parc du 12/10/2026 » plus bas |
| 98 | **Toute erreur WHEA est une « alerte critique », corrigée ou fatale** | constaté le 12/10/2026 dans le code ; numéro d'événement non conservé ; voir « Rapport du parc du 12/10/2026 » plus bas |
| 99 | **Même alerte sur plusieurs postes : le comparateur ne la rapproche pas** | constaté le 12/10/2026 — trois postes d'une même série en alerte WHEA ; voir « Rapport du parc du 12/10/2026 » plus bas |
| 100 | **Colonne « Version » de la Supervision trop étroite** | constaté le 12/10/2026 sur la console 1.8.1 : « 1.8.0 — à mettre à jou… » — défaut d'affichage, sans conséquence sur la donnée ; à corriger avec la 1.9.0 (points 93 et 94 touchent déjà cette liste) |
| 101 | **Échecs répétés d'un service tiers : la boîte à outils renvoie vers sfc et DISM** | constaté le 19/10/2026 sur le rapport d'un poste du parc — **défaut, corrigé le même jour** ; voir « Déploiement du 19/10/2026 » plus bas |
| 102 | **Un même plantage compté deux fois (variante « _M » d'un code d'arrêt)** | constaté le 19/10/2026 — **défaut, corrigé le même jour** ; voir « Trois diagnostics du 19/10/2026 » plus bas |
| 103 | **Un pilote de Trend Micro en échec « fichier introuvable » n'est pas reconnu comme orphelin** | constaté le 19/10/2026 — **cause établie le même jour en VM** : WMI ne liste pas un pilote dont le fichier manque ; **corrigé le même jour** (lecture du registre) ; voir « Trois diagnostics du 19/10/2026 » |
| 104 | **Échecs dispersés : des services déjà expliqués comptent comme « le système »** | validé le 19/10/2026 — **livré le 19/10/2026** ; voir « Trois diagnostics du 19/10/2026 » |
| 105 | **Un plantage ancien, hors période, décide du verdict** | **validé et livré le 19/10/2026** — la carte reste, elle ne porte plus le verdict ; voir « Trois diagnostics du 19/10/2026 » |
| 106 | **Textes périmés « Phase 2 » / « Phase 3 » et code 0x23 absent du catalogue** | **validé et livré le 19/10/2026** ; voir « Trois diagnostics du 19/10/2026 » |
| 107 | **Plantages d'un logiciel tiers : la boîte à outils renvoie vers sfc et DISM** | constaté le 19/10/2026 — **défaut, corrigé le même jour** ; voir « Trois diagnostics du 19/10/2026 » |
| 108 | **« Le problème principal vient d'un pilote » sous un verdict matériel** | constaté le 19/10/2026 — **défaut, corrigé le même jour** ; voir « Trois diagnostics du 19/10/2026 » |
| 109 | **Un service arrêté pendant l'arrêt de Windows compté comme un échec (cas de GLPI Agent)** | relevé le 19/10/2026 — **confirmé le même jour par mesure, corrigé le même jour** ; voir « Trois diagnostics du 19/10/2026 » |
| 110 | **Un pilote revenu à une version plus ancienne rangé sous « Pilotes mis à jour »** | relevé le 19/10/2026 sur le diagnostic 1.9.0 du poste de l'auteur — **corrigé le 20/10/2026** ; voir « Point 110 » plus bas |
| 111 | **Des couches de filtrage listées comme des cartes réseau** | relevé le 19/10/2026 dans un rapport de la VM d'essai, mesuré le 20/10/2026 sur le poste de l'auteur — **corrigé le 20/10/2026** ; voir « Point 111 » plus bas |
| 112 | **Des erreurs WHEA toutes corrigées remontées en critique par d'anciennes alertes — verdict « MATÉRIELLE » sur cinq postes** | constaté le 21/10/2026 sur neuf diagnostics du parc — **corrigé le 21/10/2026** ; voir « Diagnostics du parc du 21/10/2026 » plus bas |
| 113 | **Un plantage d'application .NET compté deux fois, la seconde sous « (inconnue) » et comme « anciennement instable »** | constaté le 21/10/2026 — **corrigé le 21/10/2026** ; même section |
| 114 | **Une même coupure dans deux cartes : Kernel-Power 41 (critique) et événement 6008 (avertissement)** | constaté le 21/10/2026 — **corrigé le 21/10/2026** ; même section |
| 115 | **Une saturation de la mémoire virtuelle donne le verdict « LOGICIELLE (corruption système) » et fait proposer sfc et DISM** | constaté le 21/10/2026 — **à décider avec l'auteur** (la correction demande une catégorie de plus) ; voir « Point 115 » dans « Diagnostics du parc du 21/10/2026 » |

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

**48 — Une erreur WHEA de port PCIe est attribuée au processeur, et la recommandation envoie au mauvais endroit. FAIT le 18/09/2026, 1.6.0.** Constaté le 06/09/2026 sur `POSTE-SALLE-02`, premier poste installé à distance par le script de déploiement.

Le rapport écrit : « Le processeur a signalé 27 erreur(s) matérielle(s) », et recommande températures, alimentation, retrait de l'overclocking/XMP, mise à jour du BIOS. Les 27 événements sont pourtant tous identiques et disent autre chose : `WHEA-Logger` **ID 17**, *erreur matérielle **corrigée***, `Composant : PCI Express Root Port`, `Source de l'erreur : Advanced Error Reporting (PCI Express)`. Le triplet `0x0:0x1:0x0` désigne le port racine du processeur, et derrière lui la carte graphique.

Deux erreurs de lecture se cumulent :

- **la source est confondue avec le rapporteur.** Sur Intel, les ports racines PCIe sont bien dans le paquet du processeur, donc « le processeur a signalé » n'est pas faux au sens strict. Mais pour celui qui lit, c'est trompeur : le fautif est un lien PCIe et son périphérique, pas un cœur ;
- **la gravité est perdue.** L'ID 17 signale une erreur *corrigée* — le lien a récupéré, rien n'a été perdu. Le rapport la classe critique au même titre qu'une erreur fatale.

Résultat concret : la recommandation fait retirer l'XMP et suspecter l'alimentation, quand la piste utile est le lien PCIe — génération négociée, gestion d'énergie du lien, pilote, contact du connecteur. C'est exactement le défaut que ce logiciel existe pour combattre : « le pilote fautif est `nvlddmkm.sys` », exact et inutile.

**Correctif envisagé :** distinguer les sources WHEA (`Processor Core`, `Cache Hierarchy`, `Memory Controller`, `PCI Express Root Port`) et donner une recommandation par source, ainsi que distinguer corrigé (ID 17) de fatal. Le champ existe déjà dans le message de l'événement : il est lu, puis ignoré.

**49 — Le message WHEA est tronqué juste avant l'information décisive. FAIT le 18/09/2026, 1.6.0.** Même rapport, même jour.

Le tableau des événements affiche : `Bus principal :Appareil :Fonctio…`. Le triplet bus/appareil/fonction est coupé — et c'est la **seule** donnée qui permet d'agir, puisqu'elle nomme le lien fautif. Il a fallu retourner au journal d'événements de la machine pour l'obtenir, ce qui est précisément le travail que le logiciel prétend éviter.

**Correctif envisagé :** pour les événements WHEA, extraire le triplet et le porter dans la conclusion — voire le traduire en nom d'appareil, `Get-PnpDevice` donnant l'emplacement sous la forme « Bus PCI 0, périphérique 1, fonction 0 ». La troncature à 600 caractères reste bonne pour le reste ; ici, ce qui compte est en fin de message.

**50 — Les codes d'arrêt sont dans le journal, le logiciel ne les lit pas. FAIT le 18/09/2026, 1.6.0.** Constaté le 14/09/2026 sur `POSTE-ELEVE-01`, un poste élève.

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

**54 — « Ce logiciel n'est plus installé, problème sans objet » écrit à propos d'un composant de Windows. FAIT le 17/09/2026, 1.6.0.** Constaté le 14/09/2026 sur `POSTE-TEMOIN`, le poste qui est à l'origine de ce projet.

Le rapport écrit : *« Application anciennement instable : **dwm.exe** (9 crashs) — ce logiciel ne figure plus parmi les programmes installés — problème probablement sans objet. »*

`dwm.exe` est le gestionnaire de fenêtres de Windows. Il ne figure pas dans la liste des programmes installés parce qu'il n'est pas une application : c'est un composant du système. Il n'a jamais été désinstallé.

Et le classement est doublement faux, parce que ces neuf plantages — module fautif `dwmcore.dll` — sont **la meilleure corroboration de la conclusion principale du même rapport** : le compositeur graphique meurt quand le pilote d'affichage meurt. Le logiciel a écarté sa pièce à conviction la plus solide.

**Correctif :** une liste blanche de binaires système (`dwm.exe`, `explorer.exe`, `csrss.exe`, `svchost.exe`, `lsass.exe`, `winlogon.exe`, `services.exe`, `RuntimeBroker.exe`, `SearchHost.exe`…) pour lesquels l'absence dans les programmes installés ne signifie rien. Plus largement : **l'absence d'une entrée de désinstallation ne doit jamais, à elle seule, disqualifier un plantage constaté.** Un programme portable ou du Microsoft Store est dans le même cas.

**55 — La version du pilote fautif n'est pas comparée d'une analyse à l'autre. FAIT le 18/09/2026, 1.6.0.** Même machine, trois rapports : 24/08 07 h 38, 24/08 12 h 41, 14/09.

Le pilote mis en cause, `nvlddmkm.sys`, apparaît en `32.0.15.8216` dans le premier rapport et en `32.0.15.8278` dans le second, cinq heures plus tard. L'auteur avait mis le pilote à jour entre les deux. Quatre nouveaux écrans bleus portant **la même signature** sont survenus ensuite, après huit semaines de calme.

Le logiciel dit bien « le problème PERSISTE : un nouveau crash avec la même signature ». Il ne dit pas la seule chose qui conclut le dossier : **le pilote accusé a été remplacé entre les deux analyses, et les plantages ont continué — ce n'est donc pas le pilote.**

Les données sont déjà là : la version du pilote est enregistrée à chaque analyse, et la section « Pilotes mis à jour » les compare déjà. Il manque le rapprochement entre ce tableau et le pilote nommé par la conclusion.

**Ce que ça change concrètement :** sans ce rapprochement, la recommandation reste « réinstaller le pilote proprement avec DDU » — c'est-à-dire refaire ce qui vient d'échouer. Avec, elle devient « le logiciel est hors de cause, regardez le matériel ». C'est la différence entre une boucle et un diagnostic.

**56 — Les vidages « en direct » de Windows étaient listés, jamais exploités. FAIT le 17/09/2026.** Constaté sur `POSTE-TEMOIN`, rapport du 17/09 17 h 38.

Le rapport affichait **178 fichiers de `C:\Windows\LiveKernelReports`** — nom, date, taille, code d'arrêt — dont **155 portant le code `0x141`**, le plus ancien du 16/06/2022. Il ne les comptait pas, n'en tirait aucune conclusion, et les nommait `BUGCODE_0x141`, un repli fabriqué qui ressemblait à s'y méprendre à un identifiant Microsoft.

`0x141` est `VIDEO_ENGINE_TIMEOUT_DETECTED` : un moteur de la carte graphique n'a pas répondu, Windows l'a réinitialisé, aucun écran bleu n'est apparu. **C'est le journal de bord d'une carte qui se dégrade, et il ne passe jamais par le journal d'événements** — ces incidents n'existent que sous forme de fichier.

**Correctif livré :** `AnalyzeGpu` compte désormais les gels (`0x141`, `0x117`, `0x119`, `0x193`) ; le catalogue nomme `0x141` et `0x144` ; le repli pour un code inconnu affiche le code brut plutôt qu'un nom d'allure officielle.

**57 — « 0 réinitialisation » était écrit en présence de 155 réinitialisations. FAIT le 17/09/2026.** Même rapport, conséquence directe du 56.

La carte de verdict annonçait *« Instabilité du pilote graphique (0 réinitialisation, 7 BSOD) »*. Le compteur ne regardait que les événements `Display 4101` du journal Windows. **Le logiciel imprimait la preuve du contraire quatre cents lignes plus bas dans le même document.**

**Correctif livré :** le titre porte les trois comptages — gels du moteur, réinitialisations journalisées, écrans bleus — et la conclusion croise désormais les deux sources.

**58 — Un gel suivi d'un écran bleu est une réinitialisation qui a ÉCHOUÉ : ce rapprochement n'était pas fait. FAIT le 17/09/2026.**

Les deux séries existaient séparément. Les apparier donne la seule mesure qui distingue un pilote instable d'un matériel qui lâche : **la proportion de gels qui se terminent mal, et la date à laquelle elle bascule.** Sur `POSTE-TEMOIN` : environ 145 gels récupérés sans incident de 2022 à juin 2026, puis 10 gels sur 10 soldés par un écran bleu à partir du 1er juillet 2026.

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

  **Décision du 19/09/2026, et correction d'un défaut constaté le jour même :** les deux copies portaient encore **le même numéro de version**, « 1.1 », alors qu'elles avaient cessé d'être le même fichier — c'est très exactement la manière dont deux copies divergent en silence. La copie publiée devient la **1.2** (une seule correction : un exemple de nom de paquet resté en 1.5.1) ; celle du dépôt porte désormais **« 1.3 — NON PUBLIÉE »** et dit en en-tête ce qu'elle ajoute et ce qu'elle deviendra. **À terme il n'y en aura qu'une** : la copie du dépôt remplacera la 1.2 sur palisser.fr après le premier déploiement réel, ses trois paramètres supplémentaires étant optionnels et sans effet pour qui lance le script à la main.
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

**PREMIER DÉPLOIEMENT RÉEL — 19/09/2026, 16 h 35, SUR UN POSTE DE SALLE, ET PAR VPN.** Le point 64 cesse d'être « écrit mais pas éprouvé ». Une machine réelle du domaine, à distance, depuis un poste qui n'était pas sur le réseau de l'établissement, a reçu le logiciel et est passée en mode parc. Les sept lignes du journal JSON sont `ok`.

Le déroulé mesuré, bout en bout **75 secondes** :

| Étape | Durée | Ce qu'elle prouve |
|---|---|---|
| compte | — | l'annuaire répond à travers le VPN |
| réponse | 7 s | le poste répond (ping ou 445) |
| partage | 0,5 s | le partage administratif `C$` accessible à distance |
| winrm | 0,06 s | la gestion à distance répond sur 5985 |
| **copie** | **6,3 s** | **63 Mo à travers le tunnel** |
| installation | 18 s | msiexec silencieux, **code 0** |
| parc | 43 s | dont 35 s d'attente de relecture par le service, puis le port 58620 répond |

**Ce qui est prouvé** : les quatre contrôles en lecture ; la copie d'un paquet de 63 Mo par VPN ; l'installation silencieuse et son code de sortie ; l'effacement du paquet après coup ; la pose de la règle de pare-feu et la bascule en mode parc ; le secret maître demandé **une seule fois** et jamais affiché ; le journal JSON en ASCII pur sur des données réelles — l'apostrophe de « pas d'adresse distante » est bien sortie en `\u0027`.

**Ce qui n'est toujours pas prouvé**, et qu'il ne faut pas confondre avec le reste :

- **Le réveil réseau.** Le poste a été allumé autrement, depuis un serveur de l'établissement. Et il ne pouvait pas en être autrement : un paquet de réveil est une **diffusion**, et une diffusion ne franchit pas un routeur — le VPN en est un. L'essai de 16 h 29, poste éteint, l'a d'ailleurs montré proprement : `reponse` → `echec` (« ni ping, ni port 445 »), puis `reveil` → `echec` (« adresse MAC inconnue »). **Le chemin d'échec est donc éprouvé lui aussi**, et il a nommé la vraie cause. Le réveil reste à essayer **sur place**, avec l'adresse MAC connue.
- **Un lot de plusieurs postes.** Un seul poste ne met à l'épreuve ni le plafond de cinquante, ni la confirmation chiffrée sur un nombre supérieur à un, ni l'enchaînement.
- **La reprise des seuls postes en échec.** Il n'y a pas eu d'échec à reprendre — et quand un échec réel s'est produit le lendemain (copie du paquet refusée, « signature non valide », depuis un partage `NETLOGON` atteint par VPN), la relance du même déploiement est passée sans rien changer d'autre. Un partage de fichiers ordinaire remplacera `NETLOGON`, qui est répliqué sur tous les contrôleurs de domaine et n'est pas fait pour des fichiers de soixante mégaoctets.

**Coût d'un poste éteint : 31 secondes** avant le verdict — deux pings puis le contrôle du port 445. Sur un lot où la moitié des machines dorment, c'est le poste éteint qui fixe la durée, pas le poste installé.

**20/09/2026 — la mise à jour par-dessus une version installée est éprouvée.** Le numéro du code passe à **1.7.0** dans les trois projets et dans le script de construction : sans ça, le paquet d'essai se serait appelé `1.6.2` tout en contenant la 1.7.0, la colonne « Version » de la console aurait affiché `1.6.2` sur tous les postes — traités comme oubliés — et la barre d'état aurait annoncé « tous les postes joignables sont en 1.6.2 », une fausse bonne nouvelle au moment précis où l'on cherche à voir avancer un lot. Le MSI 1.7.0, construit localement et installé **par-dessus une 1.6.2 en place**, remplace bien les trois exécutables : `FaultTracePC.exe`, `FaultTracePC.Cli.exe` et `FaultTracePC.Monitor.exe` portent tous `1.7.0.0` sur le disque. *C'est exactement ce que feront les postes lundi ; ça ne se découvrira plus devant trente machines.*

**Numéroter n'est pas publier**, et la distinction est écrite ici parce qu'elle s'oublie : le numéro vit dans le code et suffit à fabriquer un paquet local ; la publication se déclenche en poussant l'étiquette `v1.7.0`, ce qui construit le paquet en intégration continue et crée la *release* GitHub. Deux conséquences pratiques : le workflow **s'arrête net** si `docs/releases/RELEASE_NOTES_v1.7.0.md` n'existe pas, et les exemples qui citent `1.6.2` dans `DEPLOIEMENT.md` et dans le script de déploiement sont **vrais aujourd'hui** — ils deviendront faux le jour du tag, c'est donc ce jour-là qu'ils doivent changer.

**Fausse alerte du 20/09, consignée parce qu'elle a failli faire modifier l'installeur.** Le journal `/l*v` d'une installation montrait zéro fichier copié, la fenêtre de maintenance de Windows et le même code produit déjà installé — de quoi conclure que la mise à jour ne remplaçait rien et que le code produit n'était pas régénéré, contrairement à ce qu'affirme le commentaire de `FaultTracePC.wxs`. **Tout s'expliquait par une information absente du journal : le paquet avait déjà été installé en double-clic juste avant.** Le journal ne racontait donc pas une mise à jour mais une réinstallation du même paquet par-dessus lui-même, dont le comportement observé est parfaitement normal — et au passage souhaitable, puisque c'est ce qui permet de relancer un déploiement sur un lot déjà à moitié traité sans rien casser.

**La règle qui en sort, et c'est la troisième erreur du même genre en deux jours** — le proxy, puis le secret maître, puis le paquet. Chaque fois un raisonnement solide posé sur une prémisse non mesurée ; chaque fois c'est une mesure, et non un meilleur raisonnement, qui a tranché. *Avant d'interpréter une trace, établir ce qui l'a produite.* Et devant une affirmation, une question suffit à séparer le bon grain de l'ivraie : **« comment le sais-tu ? »** — « je l'ai lu » et « je l'ai mesuré » se vérifient, « parce que sinon » est une déduction.

**Constaté le 20/09/2026, VPN coupé — « délai dépassé » et « injoignable » ne sont pas la même panne.** Dans la même liste, les postes enregistrés avec une ADRESSE affichaient « délai dépassé », ceux enregistrés avec leur NOM affichaient « injoignable ». C'est exact et c'est voulu : le premier message signifie *on a trouvé où frapper, personne n'a répondu dans les quatre secondes* ; le second, *on n'a même pas su où frapper* — sans VPN, le nom ne se résout pas. **Mais rien dans l'interface ne l'explique**, et les deux mots se lisent tous les deux comme « ça ne répond pas ». L'information de diagnostic existait donc et n'atteignait personne.

**Décision du 20/09/2026 : le survol l'explique, les deux mots ne changent pas.** La colonne fait 150 pixels, des libellés plus longs n'y tiendraient pas ; la bulle d'aide, elle, a la place de dire ce que chaque mot signifie ET quoi vérifier. **Les deux listes de vérifications sont volontairement disjointes**, parce que les deux pannes n'ont aucun recouvrement : chercher le pare-feu d'un poste dont on n'a pas su résoudre le nom, c'est chercher au mauvais endroit. Chacune commence d'ailleurs par dire ce qui est **hors de cause** — pour « délai dépassé », ni le nom ni le jeton ni le secret ; pour « injoignable », ni le jeton ni le secret ni le pare-feu du poste. *Éliminer explicitement les fausses pistes vaut autant que nommer la bonne : c'est la leçon des trois heures du 19/09.*

**La 1.7.0 peut donc se préparer.** Reste à faire avant de la publier : un lot de plusieurs postes, et un réveil réseau depuis le réseau de l'établissement. **Les deux demandent d'être sur place.**

### La soirée du 19/09/2026 — trois heures pour un refus, et ce qu'elles ont appris

Le déploiement réussi, la console refusait le poste avec « refusé (jeton, nom Windows ou horloge décalée ?) ». Depuis le réseau de l'établissement tout fonctionnait ; par VPN, jamais. **La cause est le premier verrou du service, qui n'accepte que la boucle locale et les plages privées : le pare-feu TRADUISAIT l'adresse source entre le réseau du VPN et celui des postes, et le poste voyait donc une adresse PUBLIQUE là où la console mesurait chez elle une adresse de tunnel privée.** Le verrou faisait exactement ce qu'on lui avait demandé.

**Ce que ça a coûté, et pourquoi.** Le service répondait `403` sans écrire nulle part pourquoi. Quatre causes possibles, aucune mesurable depuis la console — et l'adresse source, la seule qui comptait, n'était **connaissable que du poste**, puisque c'est précisément elle que le chemin altérait. Trois heures, deux hypothèses fausses successives (un proxy web, puis un secret maître différent), chacune plausible et chacune démentie par la mesure suivante.

**Le correctif, livré le soir même : le service écrit ses refus dans `erreurs.log` — le motif ET l'adresse qu'il a réellement vue.** La toute première ligne produite a donné la réponse en dix secondes. Une minute de silence entre deux refus identiques, pour qu'un scanner de ports ne noie pas le journal. La réponse réseau, elle, ne change pas : les deux verrous rendent toujours le même `403`, en dire plus à un appelant non authentifié serait le renseigner. **Ne rien écrire du tout ne protégeait personne — ça rendait seulement le diagnostic impossible.**

Quatre leçons, et la première vaut pour tout ce projet :

- **Un refus qui ne dit pas pourquoi coûte des heures, et le coût est invisible tant qu'on ne l'a pas payé.** Le raisonnement remplace alors la mesure, et le raisonnement se trompe. *Tout point du logiciel qui refuse quelque chose doit pouvoir dire lequel de ses contrôles a refusé.*
- **Une mesure prise à un bout ne vaut pas pour l'autre bout.** La console a mesuré son adresse source et l'a trouvée privée ; le poste en voyait une autre. *Quand une décision se prend sur une donnée, c'est là où elle se prend qu'il faut aller la lire.*
- **La boucle locale n'éprouve pas le contrôle d'adresse**, elle le court-circuite par une ligne explicite. Le seul poste qui répondait était la console elle-même en `127.0.0.1` : **aucun essai n'avait jamais franchi ce verrou**, et personne ne s'en était aperçu. *Un cas particulier qui passe toujours n'est pas un test.*
- **L'utilisateur avait raison dès la première phrase.** Il a dit « c'est peut-être ma sécurité qui bloque » ; deux démonstrations contraires ont suivi, l'une reposant sur une hypothèse non vérifiée (que la règle de pare-feu était seule à ouvrir le port), l'autre sur une déduction. *Une démonstration dont une prémisse n'est pas mesurée n'est pas une démonstration.*

**Ce qui reste ouvert, et ce n'est pas un défaut du logiciel :** la traduction d'adresse entre le réseau du VPN et celui des postes se règle sur le pare-feu, pas ici. Élargir les plages acceptées serait le mauvais correctif — l'adresse vue étant publique, l'autoriser reviendrait à ouvrir le service à tout ce qui se cache derrière cette traduction. Si un besoin de plages réglables apparaît un jour, il devra être posé comme une décision, poste par poste, et non comme un contournement.

**Deux outils de diagnostic entrent dans `scripts/` à cette occasion** — `Test-JetonDirect.ps1` (refait la requête de la console à la main) et `Test-SecretMaitre.ps1` (dit lequel de plusieurs secrets maîtres correspond à un poste, sans jamais afficher ni le jeton ni le secret). Écrits dans l'urgence dans un dossier ignoré par git, ils auraient disparu avant le prochain incident.

**Deux enseignements de mise en page, tirés de captures d'écran réelles :**

- **La colonne qu'on vient lire doit tenir dans la fenêtre.** La colonne « Vérification » était hors de l'écran : 1 150 pixels de colonnes pour une fenêtre de 960. Une fonctionnalité qu'il faut aller chercher en faisant défiler est une fonctionnalité à moitié absente.
- **Un message ne doit pas écraser ce qu'on regarde.** La barre d'état s'étalait sur cinq lignes. Deux lignes au maximum, et le texte complet au survol — le tableau est ce qu'on regarde.

---

### La matinée du 21/09/2026 — le premier lot réel, et un réveil réseau qui ne pouvait pas marcher

Premier déploiement en lot depuis le réseau de l'établissement, sur six postes
d'une unité d'organisation. Deux postes allumés installés et passés en mode parc
sans incident. Quatre postes éteints n'ont pas pu être réveillés. Trois défauts en sont sortis, tous constatés, aucun supposé.

#### Défaut A — « hors annuaire » affirmait ce que le logiciel ne sait pas

L'onglet Inventaire annonçait « hors annuaire » pour des postes bien présents
dans l'annuaire, mais rangés dans une autre unité d'organisation.

La recherche part de l'unité saisie et descend (`SearchScope.Subtree`,
`ParkDirectory.cs`). Elle ne voit donc rien de ce qui vit ailleurs dans le
domaine. Le fait constaté était juste — cette unité ne contient pas ce poste —
mais la phrase affichée disait autre chose.

**Corrigé le 21/09/2026** : le libellé devient « pas dans cette unité »
(`not in this OU`). Il dit ce qu'on a cherché, pas ce qui existe.

C'est le même défaut de forme que le 403 muet du 19/09 : un logiciel qui conclut
au-delà de ce qu'il a mesuré coûte du temps à celui qui le lit.

#### Défaut B — le réveil réseau ne pouvait aboutir, quelle que soit la machine

Message affiché sur les quatre postes éteints : « Adresse MAC inconnue : réveil
réseau impossible ». Or un outil tiers réveille sans difficulté deux de ces
postes — la carte et le BIOS ne sont donc pas en cause.

`Get-MacDuPoste` cherche l'adresse dans trois sources, dans cet ordre :

| # | Source | État constaté le 21/09/2026 |
|---|---|---|
| 1 | serveur DHCP | **jamais interrogé** — la variable est vide |
| 2 | `postes.csv` à côté du script | **fichier absent** du dossier `Deploiement` |
| 3 | cache ARP local | vide : un poste éteint n'a pas parlé récemment |

Les trois échouent, et la première pour une raison structurelle : la console
lance toujours le script avec `-SortieJson`, ce qui met `$muet` à vrai
(`Initialize-Parametres`) et supprime la question « serveur DHCP ? ». Comme
`parametres.json` n'existe pas à côté du script, la valeur ne peut jamais être
renseignée **par ce chemin-là**. Le réveil réseau lancé depuis la console ne
pouvait donc aboutir pour aucun poste.

La preuve est dans la sortie elle-même : aucune ligne « DHCP injoignable » ni
« ne connaît pas de bail » n'apparaît. Le bloc DHCP n'a pas échoué — il n'a pas
été exécuté.

#### Défaut C — le champ « Annuaire d'adresses MAC » ne sert qu'à l'affichage

L'onglet Inventaire propose un champ « Annuaire d'adresses MAC ». Il est lu
(`ParkInventory.CheminAdressesMac`) pour remplir la colonne « MAC connue », et
**il s'arrête là** : il ne figure pas dans les paramètres passés au script de
déploiement, qui cherche son `postes.csv` à côté de lui-même.

Le champ promet donc de configurer le réveil réseau alors qu'il ne configure
qu'une colonne. Deux notions d'« annuaire MAC » cohabitent sans se parler.

#### Défaut D — le poste déployé devait être recopié à la main

Après un déploiement réussi, la console lisait le journal et mettait à jour
l'état de la ligne, mais n'inscrivait pas le poste dans l'onglet Supervision. Le
script affichait « À saisir dans la console de parc » et l'administrateur
recopiait un nom, un hôte et un port que le logiciel venait d'établir lui-même.

Sur un poste c'est agaçant ; sur trente c'est une source de fautes de frappe. Et
une faute de frappe dans un nom de machine donne un jeton faux, donc un refus
403 — le piège du 19/09/2026, retrouvé par un autre chemin.

#### Ce qui a été corrigé le 21/09/2026

| Défaut | Correction |
|---|---|
| A | libellé « pas dans cette unité » au lieu de « hors annuaire » |
| B | champ **Serveur DHCP** dans l'onglet Inventaire (`ParametresParc.ServeurDhcp`), transmis au script ; et le script dit désormais laquelle des trois sources a manqué, au lieu d'un « MAC inconnue » sans motif |
| C | nouveau paramètre `-FichierMac` du script ; le champ « Annuaire d'adresses MAC » de la console y est transmis — **uniquement s'il est renseigné**, pour que le script gardé sur une clé USB continue de lire le `postes.csv` posé à côté de lui |
| D | les postes passés en **mode parc** sont inscrits automatiquement dans l'onglet Supervision. Seuls ceux-là : un poste simplement installé n'écoute pas, et une ligne qui échoue toujours vaut moins que pas de ligne. L'hôte inscrit est le **nom**, jamais l'adresse ; aucun jeton n'est écrit ; un poste déjà présent n'est pas touché |

Le réglage du serveur DHCP vit maintenant des deux côtés, et c'est voulu : le
script interrogé à la main demande le nom et le range dans son `parametres.json`,
la console le range dans le sien. Deux fichiers de même nom, deux schémas
différents, deux dossiers différents — à ne pas confondre.

**Ce que ce lot apprend, et qui vaut au-delà de lui :** un réglage qu'un outil
sait demander n'est pas un réglage disponible. Il ne l'est que par les chemins
qui peuvent effectivement poser la question. Ici la console fermait le seul
chemin qui l'aurait posée, et rien ne le disait.

#### Vérification sur le parc réel, 21/09/2026, quatre passes

Le serveur DHCP renseigné, le réveil réseau fonctionne. Constaté sur trois
postes éteints : adresse MAC obtenue du bail, paquet magique envoyé, poste
réveillé, installé et mis en parc dans la foulée. La première passe, faite sans
avoir renseigné le serveur, a servi de témoin : elle affiche les trois sources
et la raison de chacune, exactement comme prévu.

Un poste résiste au réveil (`POSTE-ELEVE-01`) : adresse MAC trouvée, paquet
envoyé, pas de réponse après 90 secondes. Le BIOS est le suspect, pas le
logiciel — le message le dit désormais sans ambiguïté.

**Une limite du réveil, visible dans la sortie et à retenir :** le paquet magique
part de la carte qui mène au poste, et ne traverse pas les routeurs. Un poste sur
un autre VLAN que celui de la console ne sera pas réveillé, quelle que soit la
configuration de son BIOS. Le script l'écrit à chaque envoi.

#### Défaut E — un échec rattrapé était compté comme un échec

L'inscription automatique du défaut D n'a rien inscrit. La cause n'est pas dans
le code écrit ce jour-là, mais dans une règle plus ancienne qu'il a rendue
visible.

`VerdictDuPoste` s'arrêtait à la PREMIÈRE ligne en échec du journal. Or le script
consigne ce qu'il observe **au moment où il l'observe, y compris quand il
s'apprête à le réparer** :

| Étape | Ce que le script écrit | Ce qui se passe ensuite |
|---|---|---|
| `reponse` | `echec` — le poste éteint ne répond pas | il est réveillé, puis `reveil ok` |
| `winrm` | `echec` — la gestion à distance est muette | le service est démarré, puis `winrm ok` |

Ces deux lignes sont le déroulement **normal** d'un déploiement réussi sur
machine éteinte. Un poste réveillé, installé, mis en parc et joignable ressortait
donc « échec : pas de réponse réseau ». Conséquences : la colonne Vérification
mentait, la reprise du lot D (`PostesEnEchec`) recochait des postes qui n'avaient
rien à reprendre, et le défaut D ne trouvait plus aucun poste à inscrire.

**Corrigé le 21/09/2026**, dans le noyau et non dans la console, parce que la
reprise du lot D applique la même règle — `LectureDuJournal.EchecsDecisifs` :

1. pour une même étape écrite plusieurs fois, seule la **dernière** ligne compte ;
2. un échec de `reponse` est annulé par un `reveil` réussi.

Ce qui n'est **pas** annulé : un réveil qui échoue, une copie qui échoue, une
installation refusée, une mise en parc refusée. Quatre tests fixent ces limites,
dont un qui vérifie qu'une installation refusée après un réveil réussi reste un
échec.

**Vérifié le 21/09/2026 à midi**, après correction : un poste éteint, jamais
installé et absent de la console, a été réveillé, installé, mis en parc **et
inscrit tout seul dans l'onglet Supervision**. La console passe de 7 à 9 postes
suivis, toutes versions 1.7.0, tous joignables. Le point 64 est complet de bout
en bout — de « ce poste existe dans l'annuaire » à « ce poste est supervisé » —
sans une seule saisie manuelle.

#### Les trois fonctions restantes, éprouvées le 21/09/2026 après-midi

- **« Reprendre les échecs »** — relecture d'un journal de vérification : un seul
  poste recoché, celui qui avait réellement échoué. Les postes réveillés avec
  succès le matin ne sont pas recochés. La règle du défaut E tient dans les deux
  sens : en mode *vérification seule* le script ne réveille pas, l'échec de
  `reponse` n'est donc rattrapé par rien et reste décisif — ce qui est juste.
- **Historique des alertes** (point 43) — relecture de l'archive locale sur un
  poste de salle : 18 alertes sur 90 jours, 18 critiques, toutes de la même règle
  (erreur matérielle signalée par le processeur, WHEA), du 14 au 21/09. C'est
  exactement le cas d'usage qui a motivé le point : la preuve écrite qu'une
  machine signale des erreurs matérielles depuis des semaines.
- **Boîte noire distante** (point 46) — 360 relevés sur la dernière heure, lus
  depuis la console sans se rendre sur le poste, avec les événements Windows
  marquants signalés au passage.

**La 1.7.0 est complète.** Elle sera publiée sous le numéro **1.7.1**, décision
du 21/09/2026 : les postes du parc portent déjà un paquet numéroté 1.7.0 qui
n'est plus le binaire d'aujourd'hui, et msiexec n'installe rien par-dessus une
version identique. Deux binaires différents sous un même numéro est une
affirmation fausse ; le numéro doit dire la vérité.

**La leçon, et c'est la troisième fois en trois jours :** ce logiciel se trompe
quand il conclut plus vite qu'il ne mesure. Le 403 muet concluait « refusé » sans
dire par quel verrou ; « hors annuaire » concluait « n'existe pas » à partir de
« pas ici » ; et ici « une ligne echec » concluait « poste en échec » alors que
la ligne suivante disait le contraire. Le même défaut de raisonnement, trois
habits différents.

#### Le ZIP de palisser.fr — ce qu'il contient vraiment

Vérifié le 21/09/2026 en ouvrant le fichier mis en ligne, plutôt qu'en le
supposant : l'article 34 publie la **1.1 du 5 septembre 2026**. La **1.2 n'a
jamais été publiée**, alors que l'entête du script du dépôt affirmait le
contraire depuis deux jours. Une affirmation sur l'état d'un site, écrite de
mémoire dans un commentaire de code, s'était périmée sans que rien ne le
signale. L'entête est corrigé.

Le contenu du ZIP est propre : aucune donnée réelle, `postes.csv` ne porte que
les adresses d'exemple `AA-BB-CC-DD-EE-01` et `-02`. L'article et le ZIP sont
cohérents entre eux — tous deux annoncent 1.1.

Écart réel entre la 1.1 en ligne et la copie embarquée : quatre paramètres, tous
**facultatifs** — journal JSON, mode « vérifier seulement », liste de postes lue
dans un fichier, annuaire d'adresses MAC désigné par un chemin. Qui lance le
script à la main retrouve exactement le comportement de la 1.1. La 1.1 sait déjà
interroger le DHCP, réveiller, installer et mettre en parc.

**Décision à prendre, pas urgente :** republier un ZIP à jour, ou cesser de
distribuer le ZIP maintenant que le script voyage dans le logiciel. Rien ne
presse : ce qui est en ligne est exact et fonctionne.

---

## Points 80 à 87 — ce qu'un rapport sur une machine inconnue a montré

24/09/2026. Premier rapport 1.7.1 produit sur une machine **qui n'est pas celle
de l'auteur** : un portable grand public sous Windows 10 22H2, 8 Go, confié pour
lenteur et plantages, dont on ne connaît ni l'historique ni l'usage. C'est une
situation nouvelle pour ce logiciel — jusqu'ici il était relu sur des machines
dont on savait tout.

**Le contexte réel, appris APRÈS la lecture du rapport :** l'antivirus
Bitdefender venait d'être désinstallé la veille — incomplètement, on le saura
après coup —, Avast avait été installé pour le remplacer, et l'utilisateur dit que la machine est devenue lente « avant »
sans savoir avant quoi. Rien de tout cela n'était déductible du rapport — et
c'est précisément ce que les points ci-dessous cherchent à corriger.

### Ce que le rapport a bien fait

- Les trois événements `volmgr 161` sont correctement interprétés : ce n'est pas
  le disque, c'est la destination du vidage mémoire. Deux écrans bleus sans
  fichier d'analyse, et le rapport dit que la cause restera indéterminée tant
  que le réglage n'est pas corrigé. C'est juste, utile, et actionnable.
- La section « Limitations » nomme les cinq échecs d'analyse CDB au lieu de les
  taire.
- Les niveaux de confiance sont donnés, et la corrélation Windows Update est
  bien étiquetée « confiance faible, corrélation temporelle uniquement ».

### Point 80 — WinDbg installé, et déclaré absent

Cinq fois dans les limitations : `Analyse CDB de […].dmp : […] cdb.exe […]
Accès refusé.` **WinDbg était installé**, en version Microsoft Store, sous
`C:\Program Files\WindowsApps\…` — un dossier dont les permissions
interdisent le lancement direct, et une application empaquetée ne se démarre
pas par son chemin.

Conséquence : les sept écrans bleus portent tous « non identifié (**installer
WinDbg** pour l'analyse symbolique) ». **Le logiciel demande d'installer ce qui
est déjà là.**

C'est le point le plus coûteux des six, parce que l'analyse symbolique est la
seule chose qui aurait tranché entre les deux hypothèses restées ouvertes.

Deux corrections, pas une : employer l'alias d'exécution
(`%LOCALAPPDATA%\Microsoft\WindowsApps\`) plutôt que le chemin `WindowsApps`,
**et** distinguer dans le message « WinDbg absent » de « WinDbg présent mais
inaccessible ». Les deux ne demandent pas le même geste à celui qui lit.

**La première correction était une fausse piste — constaté le 09/10/2026 en
lisant le code.** L'alias d'exécution était DÉJÀ cherché, et avant le chemin
protégé : s'il avait existé sur cette machine, c'est lui qui aurait été lancé.
Il n'existait pas. Rien de vérifiable ne dit que le WinDbg du Store publie un
alias pour `cdb.exe` ; l'affirmation figurait dans un commentaire du code, sans
source, et a été retirée. Une question du Microsoft Q&A de mai 2025 (réponse
d'un conseiller indépendant, pas de Microsoft) va dans le même sens : la version
du Store ne se lance pas par son exécutable, et les « Debugging Tools for
Windows » du Windows SDK sont la voie qui fonctionne.

#### Lot A — livré le 09/10/2026 : dire la vérité

- Nouvel état `EtatAnalyseProfonde` sur le rapport : **sans objet, non
  demandée, absente, inaccessible, faite**. Cinq raisons de ne pas nommer un
  pilote, qui n'appellent pas le même geste.
- Un débogueur qui ne se lance pas est dit **une fois**, dans les limitations,
  avec le chemin, le motif donné par Windows, la mention du Microsoft Store
  **seulement si le chemin le désigne**, et ce qui fonctionne à la place. Les
  dumps suivants ne sont plus tentés : la cause est la même pour tous.
- Dans le tableau des écrans bleus, la cellule « pilote suspect » dit
  désormais **laquelle** des raisons s'applique. « Installer WinDbg » ne s'écrit
  plus que si WinDbg est réellement absent.
- Un plantage **sans fichier d'incident** dit « aucun fichier d'incident pour ce
  plantage » — il ne demande plus jamais d'installer quoi que ce soit.
- Une machine qui n'a que des dumps d'applications ne reçoit plus « WinDbg
  introuvable » pour une analyse qui n'avait rien à faire.
- En passant : une phrase française hors `Lang.T` dans `CdbAnalyzer`
  (« CDB n'a pas produit de verdict »), fuite de traduction corrigée.
- Neuf cas de test (six tests, dont un décliné en trois), parmi lesquels deux
  qui provoquent le refus de lancement avec un fichier portant l'extension `.exe`
  sans en être un.

#### Lot B ✔ — la fenêtre qui suit l'analyse, livré le 09/10/2026

Après un scan, la fenêtre principale affichait « les outils de débogage de
Microsoft ne sont pas installés sur cette machine » et proposait d'ouvrir la boîte
à outils pour les installer. Sur la machine du 24/09/2026, c'était faux, et le
bouton aurait réinstallé le même paquet. Elle doit lire le nouvel état et dire
« installé mais impossible à lancer » le cas échéant.

**Ce qui a été fait :**

- la décision est sortie de la fenêtre vers `Core/Analysis/ConseilWinDbg.cs`,
  pour pouvoir être testée (le projet de tests ne charge pas l'interface) ;
- elle lit `DiagnosticReport.AnalyseProfonde`, le même état que le rapport —
  la fenêtre et le rapport ne peuvent plus se contredire ;
- **WinDbg absent** : message et bouton inchangés (« pas installés », ouvrir la
  boîte à outils ?) ;
- **WinDbg présent mais refusé par Windows** : nouveau message, sans bouton
  d'installation — « sont bien installés, mais Windows a refusé de les lancer »,
  renvoi vers les limitations du rapport, et rappel que le bouton de la boîte à
  outils réinstallerait la même version ;
- **analyse faite, non demandée, ou aucun dump noyau** : rien ne s'affiche.
  Second défaut corrigé au passage : l'ancienne règle proposait d'installer
  WinDbg dès qu'aucun dump n'avait été analysé, y compris quand WinDbg s'était
  bien lancé mais avait dépassé le délai sur chaque dump (symboles trop longs à
  télécharger, par exemple) — vérifié dans `CdbAnalyzer` : un dépassement de
  délai laisse le dump marqué « non analysé » ;
- six cas de test (`ConseilWinDbgTests`).

#### Lot C — à mesurer AVANT de décider : ce que le bouton installe

Le bouton « 🐞 Installer WinDbg » de la boîte à outils lance
`winget install Microsoft.WinDbg` — c'est-à-dire **la version du Microsoft Store,
celle-là même qui n'a pas pu être lancée**. Avant de le changer, il faut savoir
si c'est vrai partout ou seulement sur cette machine : vérifier sur le poste de
l'auteur quelle version de WinDbg est présente, si l'alias existe, et si
l'analyse profonde y a déjà fonctionné. On ne change pas un installateur sur la
foi d'une seule machine.

**Mesuré le 09/10/2026 sur le poste de l'auteur** : WinDbg du Microsoft Store
présent, **même version** que sur la machine du 24/09/2026 (1.2606.22001.0) ;
**pas** d'alias `cdb.exe` ; **pas** de Debugging Tools du SDK. Même
configuration, donc. Mais le rapport du jour ne contient aucun dump noyau —
seulement deux « Live Kernel Reports », que l'analyse profonde ne traite pas —
si bien que le logiciel n'a rien tenté et que le refus de lancement n'y est
**ni confirmé ni infirmé**. Reste un essai direct de lancement par le chemin,
en administrateur, avant de toucher au bouton.

**Essai direct fait le même jour, sur le même poste** : lancé par son chemin
complet, le cdb.exe du Microsoft Store **a démarré**. Le refus du 24/09/2026
n'est donc **pas** propre à la version du Store : la même version se lance sur
une machine et pas sur l'autre. La cause du refus n'est pas établie (une piste,
**non vérifiée** : le paquet du Store est inscrit pour un compte utilisateur
précis, et l'analyse aurait été lancée sous un autre compte). Deux conséquences :

- **le bouton de la boîte à outils n'est pas changé** — rien ne prouve que
  l'installateur est en cause ;
- **la phrase du lot A était trop générale** : elle affirmait que la version du
  Store « ne se lance pas par son chemin ». Corrigée en « sur certaines
  machines Windows refuse de le lancer directement », dans `CdbAnalyzer` (texte
  du rapport et commentaire d'en-tête). Le test qui vérifie que le Store n'est
  nommé que si le chemin le désigne reste valable tel quel.

**Fait le même jour** : l'avertissement xUnit1051 apparu à la compilation du
lot A (un appel sans jeton d'annulation dans les tests) est corrigé — les trois
appels de `AnalyseProfondeTests` passent `TestContext.Current.CancellationToken`.

### Point 81 — quel antivirus protège réellement la machine ?

**Une fausse piste d'abord, gardée ici parce qu'elle est instructive.** À la
lecture du rapport, les deux plus gros processus étaient `MsMpEng` (Windows
Defender, 266 Mo) et `AvastSvc` (148 Mo). La conclusion tirée en conversation
fut : « deux moteurs antivirus actifs en même temps, et le logiciel n'en dit
rien ». Avec, pour la proposition d'amélioration : « la liste des processus
suffit ».

**C'était faux, et vérifié faux le jour même** sur la machine :

| Commande | Résultat |
|---|---|
| `Get-MpComputerStatus` | `AMRunningMode` = **SxS Passive Mode**, `RealTimeProtectionEnabled` = **False** |
| `root/SecurityCenter2 → AntiVirusProduct` | Defender `393472`, Avast `266240` |

« SxS Passive Mode » est le mode où Defender tourne **à côté** d'un antivirus
tiers, en analyse périodique limitée, sans protection temps réel. Le processus
`MsMpEng` existe **dans ce mode aussi** — il continue de recevoir ses mises à
jour et de faire ses analyses planifiées. Sa présence, et même sa taille, ne
disent donc rien de son rôle. Les deux valeurs `productState` vont dans le même
sens selon leur décodage usuel (Defender inactif, Avast actif et à jour) — un
décodage que Microsoft ne documente pas officiellement, et qui ne vaut ici que
parce qu'il concorde avec `AMRunningMode`.

C'est **exactement** le défaut que cette section reproche au logiciel —
conclure plus loin que ce qui a été mesuré — commis cette fois par celui qui
relisait le rapport. Il vaut d'être écrit pour cette raison.

**Ce qu'il faut, réellement :** que le rapport **lise et affiche** l'état de la
protection, au lieu de le laisser deviner :

- le mode de Defender (`AMRunningMode`, `RealTimeProtectionEnabled`) ;
- la liste des antivirus enregistrés auprès du Centre de sécurité, et lequel
  est actif ;
- et, seulement si **deux** protections temps réel sont réellement actives, une
  conclusion à part entière.

Ce n'est plus « une donnée déjà collectée » : c'est une lecture nouvelle, peu
coûteuse, qui ne demande aucun droit particulier.

**Commencé le 09/10/2026, en deux lots.**

**Lot 1 ✔ — la lecture, livré le 09/10/2026.** Rien ne change encore dans le
rapport : la donnée est collectée, pas encore affichée.

- `Models.cs` : `EtatProtection` (Defender : mode brut `AMRunningMode`,
  protection temps réel ; Centre de sécurité : liste des antivirus inscrits) et
  `AntivirusInscrit` (nom, chemin, `productState` brut et son décodage) ;
  rangé dans `SystemSnapshot.Protection` ;
- `Collectors/ProtectionCollector.cs` : deux lectures indépendantes — si l'une
  échoue, l'autre est faite quand même, et l'échec est dit une fois dans les
  limitations (avec la mention que le Centre de sécurité n'existe pas sur les
  éditions Serveur) ;
- **la différence entre « rien trouvé » et « rien pu lire » est gardée** :
  `CentreSecuriteLu` et `DefenderLu` restent faux tant que la lecture n'a pas
  réussi, pour que le rapport ne dise jamais « aucun antivirus » faute d'avoir
  pu regarder ;
- le décodage de `productState` est marqué **non documenté** dans le code, et
  testé avec les deux valeurs réellement mesurées le 24/09/2026 ;
- quatre tests (`ProtectionTests`).

**Lot 2 — à faire : l'affichage**, et la conclusion « deux protections temps
réel actives » seulement quand elle est mesurée.

**Mesuré le 09/10/2026 sur le poste de l'auteur, avant d'écrire le lot 2 :**

| Source | Résultat |
|---|---|
| `Get-MpComputerStatus` | `AMRunningMode` = **Normal**, `RealTimeProtectionEnabled` = **True** |
| Centre de sécurité | `Windows Defender`, `397568` (0x061100), chemin `windowsdefender://` |
| Centre de sécurité | `Trend Micro Apex One Antivirus`, `266240` (0x041000), chemin `C:\Program Files (x86)\Trend Micro\Security Agent\Pccntmon.exe` |

Ce que cette mesure établit :

- **Defender se reconnaît par son chemin** `windowsdefender://` — ce n'est pas
  un fichier mais un nom réservé ; et son nom affiché est « Windows Defender ».
  C'est la mesure qui manquait pour ne pas le deviner ;
- **deuxième concordance du décodage** de `productState` : 0x061100 se décode
  « actif, à jour », et Defender dit lui-même « Normal », temps réel actif ;
- **et un cas qui met la future règle à l'épreuve** : sur ce poste, les deux
  sources réunies donnent **deux** antivirus actifs — Defender (mesuré chez
  lui) et Apex One (décodé). Si la règle « deux protections temps réel » était
  écrite telle quelle, elle se déclencherait ici. Avant de conclure, il faut
  savoir ce qu'est réellement Apex One sur ce poste : installé et actif, ou
  inscription restée derrière un logiciel retiré. ~~Non établi à ce jour~~ —
  **établi le même jour** (ci-dessous).

**Mesure complémentaire, même jour.** L'auteur confirme qu'Apex One a été mal
désinstallé de ce poste. Le programme déclaré au Centre de sécurité
(`Pccntmon.exe`) **n'existe plus** (`Test-Path` → False). Un seul service
reste : « Apex One NT WSC Service » (`TmWSCSvc`), **arrêté** — c'est celui qui
inscrit le produit auprès du Centre de sécurité. L'inscription est donc
**orpheline** : le Centre de sécurité déclare actif un antivirus qui n'existe
plus. Conséquence pour le logiciel : **un antivirus tiers ne compte comme
protection que si son programme existe encore**, et une inscription orpheline
est signalée pour ce qu'elle est.

**Lot 2 découpé en deux, pour rester petit :**

**Lot 2a ✔ — l'affichage, livré le 09/10/2026.** Carte « Protection antivirus »
dans les informations système du rapport :

- Defender décrit d'après **sa propre lecture** (temps réel actif ou non, mode ;
  « en retrait » quand le mode contient *Passive*), jamais d'après le code non
  documenté du Centre de sécurité ;
- autres antivirus : actif / inactif, signatures à jour ou périmées, avec la
  mention qu'il s'agit de ce qu'ils déclarent au Centre de sécurité ;
- **inscription orpheline** nommée comme telle, avec le chemin disparu, et la
  phrase « ce logiciel ne protège pas la machine » ;
- rien lu → « état non lu », « inconnus » — jamais « aucun antivirus » ;
- `ProtectionCollector.ProgrammePresent` vérifie que le programme déclaré
  existe ; `AntivirusInscrit.EstDefender` reconnaît Defender à son chemin
  `windowsdefender://` ;
- six tests de plus dans `ProtectionTests`, dont les deux configurations
  réellement mesurées (24/09 et 09/10).

**Vérifié en réel le 09/10/2026** sur le poste de l'auteur, avec la version
compilée : la carte affiche « Windows Defender : protection en temps réel
active (mode Normal) » puis « Trend Micro Apex One Antivirus : inscription
orpheline », avec le chemin disparu. Aucune ligne d'échec de lecture dans les
limitations : les deux sources ont été lues.

**Lot 2b ✔ — les conclusions, livré le 09/10/2026.** `RulesEngine.AnalyzeProtection` :

| Conclusion | Gravité | Quand |
|---|---|---|
| `protection.orpheline` — « Antivirus mal désinstallé : … » | information | un antivirus tiers est déclaré au Centre de sécurité mais son programme n'existe plus |
| `protection.deux` — « N antivirus surveillent la machine en même temps » | avertissement, confiance moyenne | Defender en temps réel **et** un tiers actif dont le programme est **vérifié présent** (ou deux tiers ainsi) |
| `protection.aucune` — « Aucune protection antivirus en temps réel » | avertissement, confiance moyenne | Defender sans temps réel **et** aucun tiers actif qui ne soit pas prouvé absent |

- **Deux seuils volontairement différents** : pour dire « deux antivirus », le
  programme du tiers doit être prouvé présent ; pour dire « aucune protection »,
  il suffit qu'il ne soit pas prouvé absent. Quand le chemin n'est pas
  vérifiable, rien n'est dit dans un sens comme dans l'autre ;
- **confiance moyenne** pour les deux avertissements : l'état des antivirus
  tiers vient du code non documenté du Centre de sécurité ;
- rien n'est conclu sur l'ensemble si une des deux lectures a échoué ;
- l'inscription orpheline est une **information** : elle ne change pas le
  verdict. Sur le poste de l'auteur, c'est la seule conclusion qui apparaît ;
- huit tests (`ConclusionsProtectionTests`, dont un ajouté après l'essai réel), dont les deux configurations
  réellement mesurées : la machine du 24/09/2026 ne déclenche **rien** — ce qui
  aurait été la bonne réponse à la fausse piste de ce jour-là.

**Vérifié en réel le 09/10/2026, et un défaut trouvé.** Sur le poste de
l'auteur, la conclusion « Antivirus mal désinstallé : Trend Micro Apex One
Antivirus » apparaît bien, seule, en information ; le verdict n'en est pas
changé. Mais elle était suivie de « 🧰 Dans FaultTracePC bouton Outils, puis :
sfc /scannow, puis DISM » — l'indication attachée à la catégorie « Logiciel ».
Ces outils réparent les fichiers de Windows, ils ne retirent pas un antivirus.
**Corrigé le même jour** : les trois conclusions passent en catégorie neutre
(« Général »), qui n'annonce aucun outil et n'ajoute rien au script de
réparation — sans quoi un avertissement « deux antivirus » y aurait ajouté les
sections prévues pour une corruption système. Un test de plus.
**Correction vérifiée en réel le même soir** : la conclusion porte le badge
« Général » et plus aucune indication de boîte à outils. (Premier essai faussé :
la fenêtre de l'essai précédent était restée ouverte, `dotnet build` n'avait pas
pu remplacer `FaultTracePC.Core.dll` — erreur MSB3027 — et c'est l'ancienne
version qui avait été relancée. Fermer FaultTracePC avant de recompiler.)

**Le point 81 est terminé.**

### Point 82 — un verdict que ses propres mesures contredisent

Verdict rendu : « Cause la plus probable : **STOCKAGE** (disque/SSD, câblage ou
firmware) », confiance **élevée**.

Deux sections plus bas, le même rapport mesure le disque : **0** secteur
réalloué, **0** erreur CRC, **0 %** d'usure, 250 heures de fonctionnement,
40 °C, et un volume rempli à **7 %**. Un SSD quasi neuf, sans un seul défaut.

Le verdict vient des seuls codes d'arrêt (`0x154`, `0x7A`), sans jamais être
confronté à la mesure. C'est la quatrième occurrence en une semaine du même pli
d'écriture : **conclure plus loin que ce qui a été mesuré.**

**Ce qu'il faut :** quand une famille de codes d'arrêt désigne un organe et que
la mesure de cet organe ne montre rien, la confiance baisse et le rapport le
dit — « les codes pointent vers le stockage, mais le disque ne montre aucun
défaut mesurable ; chercher au-dessus du disque ». Ne pas se taire, ne pas
affirmer : nommer la contradiction.

**Une correction d'abord, à la relecture du rapport du 24/09/2026 le
10/10/2026.** La phrase « le verdict vient des seuls codes d'arrêt » était
**trop forte**. Le même rapport contenait une autre conclusion critique de
stockage : « Erreurs signalées par le contrôleur de stockage (10) », dix
événements `storahci 129` — des réinitialisations du disque demandées par le
contrôleur. La piste du stockage n'était donc pas sans appui mesuré. Ce qui
manquait, c'est de dire que le **disque lui-même** — ses secteurs, son usure —
ne montrait rien, et que la question restait ouverte entre le disque, sa
liaison et son contrôleur. C'est ce que fait le lot 1.

**Lot 1 ✔ — livré le 10/10/2026.**

- les conclusions tirées des seuls codes d'arrêt portent désormais un code
  stable (`bsod.0x…`), un par code d'arrêt — pour les reconnaître sans lire
  leur titre, qui est traduit ;
- `RulesEngine.ConfronterStockageALaMesure` : pour un code d'arrêt « stockage »,
  si le disque système est **mesuré et sain** (au moins un compteur de défauts
  réellement lu ; aucun secteur défectueux, aucune erreur de transfert, aucune
  alerte du disque, aucune réserve épuisée, état Windows ni « avertissement »
  ni « défaillant ») :
  - **aucune autre alerte de stockage** → confiance « faible », et le rapport dit
    « la mesure ne le confirme pas… ne pas remplacer le disque sur la seule foi
    de ces codes » ;
  - **d'autres alertes de stockage** (le cas du 24/09) → la confiance est
    gardée, le rapport dit que le disque ne montre rien dans ses compteurs et
    renvoie vers ces alertes pour départager disque, liaison et contrôleur ;
- **le verdict** reçoit la même nuance quand il conclut « STOCKAGE » ;
- rien n'est dit si le disque système n'est pas identifié ou si ses compteurs
  n'ont pas été lus : une mesure absente n'est pas une mesure saine ;
- le disque système est celui qui porte la lettre de `SystemDrive` ;
- six tests (`StockageContreMesureTests`).

**Lot 2 ✔ — la mémoire contre le diagnostic mémoire Windows, livré le 10/10/2026.**

**Un défaut trouvé d'abord, en vérifiant où Windows range ce résultat.** Le
diagnostic mémoire (mdsched) écrit dans le journal Système, source
`Microsoft-Windows-MemoryDiagnostics-Results` : **1101 et 1201** = aucune
erreur, **1102 et 1202** = erreurs matérielles, chaque passage écrivant les
deux événements de sa paire. Le logiciel collectait bien ces événements, mais
ne comptait que **1202** comme erreur : un **1102 était lu « aucune erreur »**.
Sur un passage en échec, il voyait donc à la fois une erreur et un résultat
sain. Corrigé (`EventLogCollector.DiagnosticMemoireEnErreur`) ; un identifiant
inconnu n'est plus interprété du tout. Sources consultées — aucune page
Microsoft trouvée, deux sources concordantes : tenforums.com (tutoriel 91178)
et flamingo.run (« windows-memory-diagnostic-results »).

**Ce que fait le lot**, pour un code d'arrêt « mémoire » (`RulesEngine.ConfronterMemoireAuDiagnostic`) :

- **diagnostic sans erreur sur la période** → confiance « faible », et le
  rapport dit que rien ne confirme une mémoire défectueuse, en rappelant que ce
  test ne voit pas tout (MemTest86, plusieurs passes, est plus sensible) ;
- **aucun diagnostic sur la période** → le rapport dit que la mémoire n'a pas
  été testée, sans toucher à la confiance — rien n'a été mesuré ;
- **diagnostic en erreur** → rien à ajouter : la conclusion « RAM défectueuse
  confirmée » le dit déjà ;
- la conclusion existante « Suspicion de RAM défectueuse » passe aussi en
  confiance « faible » quand le diagnostic n'a rien trouvé (elle le disait déjà
  dans son texte, sans en tirer la conséquence) ;
- **le verdict « MÉMOIRE RAM »** reçoit la nuance correspondante : diagnostic
  sans erreur et sa date, ou « la mémoire n'a pas été testée sur la période ».
- onze cas de test (`MemoireContreDiagnosticTests`).

Sur la machine du 24/09/2026, aucun diagnostic mémoire n'avait été lancé : le
rapport y aurait ajouté « la mémoire n'a pas été testée ».

**Le point 82 est terminé le 10/10/2026** : stockage et mémoire, les deux
familles de codes rencontrées le 24/09/2026. D'autres familles (pilote
graphique, matériel) pourront suivre le même principe si un cas réel le
demande.

### Point 83 — sept plantages, deux amas, aucun regroupement

Les plantages n'étaient pas répartis dans le temps :

| Amas | Plantages | Écart |
|---|---|---|
| juillet | 3 (deux `0x1A`, un `0x154`) | — |
| — | — | **74 jours sans rien** |
| septembre | 4 (trois `0x154`, un `0x7A`) | — |

Et les **10 réinitialisations du contrôleur de stockage** tiennent toutes dans
la même fenêtre de 48 heures que le second amas. Pas une seule avant.

Le rapport liste les sept plantages dans un tableau chronologique et s'arrête
là. « Sept plantages en deux amas séparés de 74 jours » est une phrase qu'il a
de quoi écrire et qu'il n'écrit pas — et c'est la première question qu'un
technicien pose : **qu'est-ce qui a changé ce jour-là ?**

**Lot 1 ✔ — les séries, livré le 10/10/2026** (`RulesEngine.AnalyzeSeriesDePlantages`).

- les plantages sont triés par date et coupés en **séries** là où deux
  plantages successifs sont séparés de **14 jours ou plus** — un **choix**, pas
  une mesure, à revoir sur des cas réels ;
- à partir de **trois plantages et deux séries**, une conclusion en
  **information** (elle ne change pas le verdict) : « 7 plantages en 2 séries,
  jusqu'à 74 jours sans rien entre deux », le détail série par série (dates,
  codes d'arrêt et leur nombre, écart entre séries), et la question à poser
  avec sa date : « ils reprennent le 21/09/2026 — chercher ce qui a changé juste
  avant » ;
- cinq tests (`SeriesDePlantagesTests`), dont le profil du 24/09/2026.

**Erreur corrigée le même jour.** La première version de ce test disait les
jours de juillet « reconstitués, seul le 09/07 figurant dans le rapport ». C'était
faux : le tableau des écrans bleus du rapport donne les sept plantages, date,
heure et code — mal relu. La série de septembre y était aussi placée à partir du
21/09 au lieu du **22/09** ; l'exemple donné en conversation (« ils reprennent le
21/09/2026 ») était donc faux d'un jour. Le test reprend désormais les valeurs
réelles : 06/07 18:49 (`0x154`), 06/07 22:45 (`0x1A`), 09/07 22:07 (`0x1A`) ;
22/09 20:35 (`0x7A`), 22/09 20:57, 23/09 10:34, 24/09 07:49 (`0x154`). L'écart
de 74 jours, lui, était juste.

**Lot 2 — à décider** : rapprocher des séries les autres événements de la même
fenêtre (les 10 réinitialisations du contrôleur, toutes dans les 48 heures du
second amas), et ce qui a changé juste avant — c'est le point 84 (dates de pose
des pilotes) et le point 85 (logiciels installés).

### Point 84 — les dates de pose des pilotes ne servent à rien

Le rapport donne la date de chaque pilote tiers et marque « ancien » au-delà de
quatre ans. C'est tout ce qu'il en fait.

Or **quatorze pilotes du même éditeur portaient la date du premier jour de
l'amas de septembre** — un jeu de pilotes posé, et la machine qui plante le soir
même. Un seul pilote du même éditeur gardait une date ancienne, ce qui écarte
une réécriture de masse par Windows.

**Ce qu'il faut :** rapprocher la date de pose des pilotes du début d'un amas de
plantages. À énoncer comme une **piste**, jamais comme une preuve : une date de
fichier est une date de pose, pas une causalité.

**Précision du 10/10/2026 sur cette dernière phrase.** « Une date de fichier est
une date de pose » est trop affirmatif : la date relevée est celle de la
dernière modification du fichier `.sys`. Elle correspond le plus souvent à
l'installation ou à la mise à jour, mais peut aussi être celle de sa
fabrication. Le rapport le dit.

**Lot 1 ✔ — livré le 10/10/2026** (`RulesEngine.AnalyzePilotesAuDebutDesSeries`).

- pour chaque série d'**au moins deux** plantages (point 83), les pilotes
  **tiers** dont le fichier est daté du jour du premier plantage ou des **deux
  jours précédents** — fenêtre choisie, pas mesurée ; on compare des dates, pas
  des heures, l'inventaire ne gardant que le jour ;
- regroupés par éditeur, avec la liste des fichiers (six au plus, puis « et N
  autre(s) ») et les pilotes du même éditeur qui gardent une date différente ;
- **avertissement en confiance « faible »** et **catégorie neutre** : c'est une
  piste, aucun outil de réparation n'est proposé sur cette base. La
  recommandation dit « piste à vérifier, pas une preuve » et donne le moyen de
  la confirmer : désinstaller ou revenir à la version précédente, et voir si
  les plantages cessent ;
- cinq tests (`PilotesDebutSerieTests`), avec les données réelles du 24/09/2026 :
  la conclusion y donne « Pilotes posés juste avant le début des plantages :
  Gen Digital Inc. (14) », série commencée le 22/09/2026 à 20:35, et signale
  `aswElam.sys` resté au 07/10/2025.

### Point 85 — les logiciels installés n'apparaissent pas

Question posée par celui qui tenait la machine : « ai-je bien fini de
désinstaller l'ancien antivirus ? » **Le rapport ne peut pas y répondre.** Il
n'a pas de section « logiciels installés ».

Ce qu'il permet de dire — aucun pilote chargé, aucun processus, aucun événement
au nom de cet éditeur — est déjà utile, et c'est la réponse qui a été donnée.
Ce qu'il ne permet pas de voir : des fichiers restés sur le disque, des services
orphelins, des pilotes-filtres désenregistrés mais présents.

**Vérifié après coup sur la machine** : trois dossiers de l'ancien antivirus
restaient sur le disque — dans `Program Files` et deux dans `ProgramData`, dont
celui de son agent de gestion. La désinstallation n'était donc pas terminée, et
rien dans le rapport ne permettait de le voir.

**Ce qui a réellement répondu à la question**, vérifié ensuite en administrateur :

| Contrôle | Résultat |
|---|---|
| `fltmc filters` — les pilotes qui s'intercalent entre Windows et le disque | quatorze filtres, **tous Microsoft ou du nouvel antivirus** ; aucun de l'ancien |
| services dont le nom affiché cite l'ancien éditeur | **aucun** |
| fichiers `.sys` dans les dossiers restants | aucun trouvé |
| contenu des trois dossiers restants | deux **vides**, un contenant **un seul élément**, lui-même un sous-dossier vide — mesuré au second essai : la première commande, qui masquait les erreurs, n'avait rien affiché du tout, ce qui ne distinguait pas « vide » de « accès refusé » |

Rien de l'ancien antivirus ne tournait plus : il ne restait que des dossiers sur
le disque. La désinstallation était **fonctionnellement** terminée, pas
proprement terminée.

**`fltmc filters` mérite une place dans le rapport.** C'est une lecture
unique, rapide, et le logiciel tourne déjà en administrateur. Un pilote-filtre
orphelin d'un antivirus mal désinstallé est une cause classique de lenteur et
d'écrans bleus liés au stockage — et c'est exactement ce qu'il fallait pouvoir
écarter ici. La pile des filtres, avec leur altitude et leur éditeur, répond à
la question « qu'est-ce qui se glisse entre Windows et le disque ? » mieux que
n'importe quelle liste de programmes.

La liste des programmes installés est **déjà collectée** — elle sert au contrôle
« ce logiciel est-il toujours installé ? » de la 1.6.0. Elle n'est simplement
pas affichée.

**Décidé le 10/10/2026** (choix de l'auteur entre deux options) : le rapport
n'affiche **dans ses conclusions que ce qui sert au diagnostic**, et la **liste
complète, repliée**, en bas du rapport pour qui veut la consulter. L'option
écartée — toute la liste dans les détails techniques — noyait une centaine de
lignes sans rapport avec la panne.

**Lot 1 ✔ — livré le 10/10/2026.**

- `RulesEngine.AnalyzeLogicielsAuDebutDesSeries` : les logiciels dont la date
  d'installation déclarée tombe le jour du premier plantage d'une série ou dans
  les deux jours précédents — même principe et même fenêtre que les pilotes
  (point 84). Avertissement en confiance faible, catégorie neutre, « piste à
  vérifier, pas une preuve » ;
- la date est celle que le logiciel déclare dans le registre (`InstallDate`) :
  certains n'en déclarent aucune, et une mise à jour ne la change pas toujours —
  le rapport le dit ;
- section « Logiciels installés », **repliée**, plus récents d'abord
  (`HtmlReportGenerator.SectionLogicielsInstalles`) ;
- quatre tests (`LogicielsInstallesTests`) ; les logiciels y sont des données de
  test, la liste de la machine du 24/09/2026 n'apparaissant pas dans son rapport.

**Vérifié en réel le 10/10/2026** sur le poste de l'auteur : section repliée
« Afficher les 71 logiciels installés », plus récents d'abord. Aucun plantage
sur ce poste, donc aucune conclusion « installé juste avant » : attendu.

**Lot 2 — les restes d'un logiciel désinstallé, limité le 10/10/2026 à ce qui se
mesure.** Décidé avec l'auteur : les **dossiers restés sur le disque** sont
écartés — les rattacher à un logiciel disparu demanderait de deviner d'après
leur nom, avec trop de fausses alertes possibles. Restent deux mesures, livrées
en deux petits lots.

**Lot 2a ✔ — services et pilotes inscrits sans programme, livré le 10/10/2026.**

- `ServiceCollector` lit tous les services (`Win32_Service`), isole le chemin du
  programme dans leur ligne de commande (entre guillemets, sinon jusqu'à
  `.exe` / `.sys` — un chemin sans guillemets peut contenir des espaces) et
  vérifie que le fichier existe ; un chemin illisible n'est pas conclu ;
- `DriverCollector` note désormais si le fichier `.sys` inscrit existe
  (`DriverInfo.FichierPresent`) — c'est l'**angle mort A du point 89** : un
  pilote dont le fichier a disparu était collecté puis jeté sans un mot ;
- conclusion en **information**, catégorie neutre : « N service(s) ou pilote(s)
  inscrit(s) dont le programme n'existe plus », avec nom, mode de démarrage,
  état et chemin ; recommandation de passer par l'outil de l'éditeur, et
  **garde-fou** : ne pas effacer une inscription à la main sans savoir à quoi
  elle sert — retirer un pilote de démarrage peut empêcher Windows de démarrer ;
- huit cas de test (`InscriptionsOrphelinesTests`).

Mesure de référence déjà faite le 09/10/2026 sur le poste de l'auteur (point 89) :
**zéro** pilote inscrit dont le fichier manque. Pour les services, pas encore de
référence : le premier rapport le dira — on y attend au moins le service
d'Apex One si son programme a disparu.

**Vérifié en réel le 10/10/2026** sur le poste de l'auteur : **une seule**
inscription signalée, « Apex One NT WSC Service » (TmWSCSvc), démarrage
**automatique**, arrêté, programme `C:\Program Files (x86)\Trend Micro\Security
Agent\TmWSCSvc.exe` disparu. Aucun autre service ni pilote signalé : pas de fausse
alerte sur ce poste.

**Et une incohérence trouvée dans le même rapport, corrigée le même jour.** La
carte « Échecs de services Windows répétés » citait TmWSCSvc (7 échecs de
démarrage) et concluait : « le point à vérifier est donc l'existence du
fichier » — alors que la nouvelle carte, trois lignes plus haut, venait de
l'établir. Elle dit désormais ce qui a été **mesuré** : « Vérifié : le programme
de TmWSCSvc n'existe plus », ou, si le programme est là, « ce n'est pas une
inscription restée derrière une désinstallation » ; l'ancienne phrase ne reste
que si les services n'ont pas pu être lus. Deux tests de plus.

**Lot 2b ✔ — la pile des pilotes-filtres, livré le 10/10/2026.**

- `FiltreCollector` lance `fltmc filters` (droits administrateur, déjà acquis)
  et lit les lignes qui suivent le **trait de tirets** — jamais un mot d'en-tête,
  ces en-têtes étant traduits selon la langue de Windows. Sans trait (droits
  insuffisants, message d'erreur), la pile est « non lue », jamais « vide » ;
- carte « Pilotes-filtres (entre Windows et les fichiers) » dans les
  informations système : nom, altitude, éditeur (pris dans l'inventaire des
  pilotes). **En gras** : les filtres d'un autre éditeur que Microsoft, ou dont
  l'éditeur n'a pas pu être lu — on ne présume pas sain ce qu'on n'a pas lu ;
- quatre tests (`FiltresDisqueTests`).

**Vérifié en réel le 10/10/2026** sur le poste de l'auteur : sa sortie de
`fltmc filters` (Windows en français) et la carte de son rapport donnent la
**même liste** — quatorze filtres, mêmes altitudes, tous Microsoft, aucun en
gras. Cette sortie réelle remplace dans les tests celle, reconstituée, de la
première version. Deux détails qu'elle a appris : l'en-tête français
« Nom. d’instn. » porte une apostrophe typographique, et une altitude peut avoir
une décimale (`385250.5`) — tous deux sans effet sur la lecture.

**Le point 85 est terminé** (lot 1, lots 2a et 2b).

### Point 86 — la boîte noire sans charge processeur

Sur cette machine, **tous** les relevés de la boîte noire portent « — » dans la
colonne CPU %, alors que la température du GPU, la mémoire et la mémoire
virtuelle sont bien enregistrées. La température du processeur manque aussi —
ce qui est fréquent et documenté, beaucoup de processeurs portables ne
l'exposent pas. **La charge processeur, elle, ne devrait jamais manquer.**

Or le motif de la visite était « le PC est très lent ». C'est précisément la
mesure qui aurait servi, et c'est la seule absente.

**Cause non établie.** Ce n'est pas la langue du système : sur les postes du
parc, en français aussi, la colonne est remplie. À instrumenter avant toute
hypothèse : journaliser la raison pour laquelle la mesure échoue, plutôt que
d'afficher un tiret qui ressemble à « rien à signaler ».

**Ce qui est lu, vérifié dans le code le 10/10/2026.** Le service prend la
charge processeur dans la bibliothèque de capteurs LibreHardwareMonitor, sur
la sonde de charge dont le nom contient « Total ». Si la bibliothèque ne
s'ouvre pas, ne voit pas le processeur, ou si le processeur n'a pas cette
sonde, la valeur reste vide — sans que rien ne dise laquelle de ces trois
situations s'est produite. Sur la machine du 24/09/2026, la température du GPU
était lue : la bibliothèque s'était donc ouverte. Les deux autres causes
restent possibles ; aucune n'est établie.

**Lot 1 ✔ — instrumenter, livré le 10/10/2026.** Aucune hypothèse n'est
corrigée « au cas où » :

- `SensorReader` (service) note à l'ouverture ce que la bibliothèque a vu —
  erreur d'ouverture, ou chaque processeur avec ses sondes de charge, ou, si
  aucun processeur n'est vu, les autres matériels — et le service l'écrit dans
  la ligne de **début de session** du journal (`diag`) ; les capteurs sont donc
  ouverts juste avant cette ligne, et non plus juste après ;
- le texte est construit par `Analysis.DiagnosticCapteurs` (dans Core, pour être
  testé) ;
- `FlightJournalCollector` garde le diagnostic du dernier démarrage ;
- le rapport, sous chaque tableau « Dernières secondes avant l'incident » où
  **aucun** relevé n'a de charge : « Charge processeur non mesurée : un tiret
  n'est pas un zéro », suivi de ce que le service a vu — ou, pour un journal
  écrit avant la 1.8.0, « la raison n'a pas été enregistrée » ;
- sept tests (`DiagnosticCapteursTests`).

**CI rouge à la première livraison, corrigé le même jour.** Un test sur 812 en
échec : le garde-fou de traduction (`Aucun_texte_francais_hors_de_Lang_T`). Le
séparateur `" ; "` — espace avant le point-virgule, typographie française —
était écrit en dur dans `DiagnosticCapteurs`, hors de `Lang.T`. Il passe par
`Lang.T(" ; ", "; ")`. Cause déduite du code (le journal de la CI ne nommait
pas le test) : la règle du garde-fou désigne exactement ce littéral, et c'est
le seul de la livraison qui y tombe.

**Première installation réelle, 10/10/2026** (MSI 1.8.0 par-dessus la 1.7.1, poste
de l'auteur) : le service a redémarré et a bien écrit la ligne de début de
session avec son diagnostic — processeur AMD Ryzen 7 7735U, seize sondes « CPU
Core #n », **« CPU Total »** et « CPU Core Max ». Sur ce poste la sonde lue existe :
c'est cohérent avec une charge mesurée. **Mais le diagnostic était écrit en
anglais**, sur un Windows en français : le service tourne sous le compte SYSTEM,
dont la langue n'est pas celle de l'utilisateur (même cause qu'au point sur la
langue du service, voir plus haut). Un rapport en français l'aurait cité tel quel.
**Corrigé le même jour** : le service écrit des faits bruts, sans langue
(`v1|cpu=…|charge=…`, `DiagnosticCapteurs.Encoder`), et le rapport les met en
phrases dans sa propre langue (`DiagnosticCapteurs.Lire`) ; un texte qui n'a pas
cette forme est rendu tel quel. Trois tests de plus. Le MSI est à reconstruire.

**Vérifié après reconstruction et réinstallation, même jour** : la ligne de début
de session porte désormais `"diag":"v1|cpu=AMD Ryzen 7 7735U with Radeon
Graphics|charge=CPU Core #1,…,CPU Core #16,CPU Total,CPU Core Max"` — des faits
bruts, plus aucune phrase anglaise.

**Lot 2 — à décider sur pièce** : la prochaine machine concernée, avec un
service 1.8.0, dira laquelle des trois situations est en cause. Une piste
indépendante de la bibliothèque existe — Windows calcule lui-même la charge
processeur — mais elle ne sera retenue qu'une fois la cause connue.

### Point 87 — une recommandation sur un réglage jamais lu

La carte `volmgr 161` du rapport recommandait : « activer les petits vidages
mémoire […] et laisser le fichier d'échange géré automatiquement par Windows ».
La recommandation a été reprise telle quelle en conversation — « corriger les
réglages de vidage ».

**Vérifié sur la machine** : le réglage était **« Vidage mémoire automatique »**,
fichier `%SystemRoot%\MEMORY.DMP`, remplacement des fichiers existants coché.
C'est **la configuration d'origine de Windows** depuis Windows 8, et personne
n'y avait touché. Ce mode écrit l'équivalent d'un vidage noyau **et** des
petits vidages : le rapport en avait d'ailleurs trouvé les deux sortes sur le
disque.

Le logiciel a donc déduit un réglage défaillant d'un **événement**, sans jamais
**lire le réglage**. Et « activer les petits vidages » laissait entendre qu'ils
ne l'étaient pas, alors qu'ils l'étaient.

Ce que disaient réellement les faits : cinq plantages sur sept ont laissé un
fichier ; les deux qui n'en ont pas laissé sont tous deux dans l'amas de
septembre, pendant les réinitialisations du contrôleur de stockage. Une
explication plausible — non démontrée — est que l'écriture du vidage a échoué
parce que le chemin vers le disque était lui-même perturbé au moment du
plantage. Ce n'est pas un réglage à corriger.

**Ce qu'il faut :**

- lire `HKLM\SYSTEM\CurrentControlSet\Control\CrashControl` (`CrashDumpEnabled`)
  et l'état « géré par le système » du fichier d'échange, et les **afficher** ;
- ne recommander un changement que si le réglage lu s'écarte de celui d'origine ;
- quand des vidages manquent alors que le réglage est bon, le dire comme tel :
  « le réglage est correct ; les plantages sans fichier sont à rapprocher de ce
  qui se passait sur le stockage à ce moment-là ».

Cinquième occurrence en une semaine du même pli : **conclure plus loin que ce
qui a été mesuré** — et la troisième fois qu'il est commis aussi par celui qui
relit, en reprenant la conclusion du logiciel sans la vérifier.

**Livré le 10/10/2026.**

- **lecture** (`SystemInfoCollector`) : `CrashDumpEnabled` et `FilterPages` dans
  `HKLM\SYSTEM\CurrentControlSet\Control\CrashControl`, et
  `AutomaticManagedPagefile` (`Win32_ComputerSystem`). Valeurs selon la
  documentation Microsoft « Overview of memory dump file options for Windows » :
  0 aucun, 1 complet (actif avec `FilterPages` = 1), 2 noyau, 3 petit, 7
  automatique. Non lu = null, jamais confondu avec « absent » ;
- **affichage** : carte « Fichiers de plantage » dans les informations système
  (réglage lu, et gestion du fichier d'échange) — `ReglageVidage.Nom` ;
- **la carte `volmgr` ne recommande plus rien sans avoir lu** :
  - réglage sur « aucun » → c'est la cause, remettre « Vidage mémoire automatique » ;
  - fichier d'échange réglé à la main → le laisser gérer par Windows ;
  - **réglages d'origine** (le cas du 24/09) → « ce n'est pas un réglage à
    corriger », l'échec est à rapprocher de ce qui se passait au moment du
    plantage, en particulier sur le stockage ; recommandation « ne pas modifier
    ces réglages » ;
  - réglages non lus → rien n'est affirmé, on dit de vérifier ;
  - la phrase « la destination du vidage manque ou est trop petite », affirmée
    sans mesure, est retirée ;
- **catégorie neutre** : en « Logiciel », la carte recevait l'indication
  « sfc /scannow, puis DISM » et ajoutait au script de réparation des sections
  sans rapport — le même défaut qu'au point 81 ;
- onze cas de test (`ReglageVidageTests`), dont la configuration du 24/09/2026.

**Vérifié en réel le 10/10/2026** sur le poste de l'auteur : la carte affiche
« Réglage : vidage mémoire automatique (réglage d'origine de Windows) » et
« Fichier d'échange : géré par Windows ». Pas d'événement `volmgr` sur ce poste :
la carte d'échec d'écriture n'y apparaît pas, ce qui est attendu.

### L'issue, pour la machine

Le jour même, après la fin de la désinstallation de l'ancien antivirus et sans
autre intervention, **la machine est redevenue sensiblement plus rapide** —
constat de celui qui la tenait. Cela conforte l'hypothèse que les plantages de
septembre et la lenteur venaient de la cohabitation des deux antivirus pendant
deux jours ; cela ne la prouve pas. La confirmation sera l'absence de nouvel
écran bleu dans les jours qui suivent.

L'amas de juillet, lui, **reste inexpliqué** : il est antérieur à l'arrivée du
nouvel antivirus, et hors de la fenêtre de 30 jours qu'avait lue le rapport.

À retenir pour le logiciel : **le rapport a désigné le stockage, la cause était
au-dessus du disque**, et rien de ce qu'il avait collecté ne l'en aurait empêché
s'il avait rapproché ses propres sections. C'est tout l'objet des points 80 à 87.

### Ce que ce rapport apprend au projet

**Quatre des huit points ne demandent aucune collecte nouvelle** (82 à 85). Tout
est déjà lu, rangé, disponible. Le 80 est une correction de lancement, le 81 et
le 87 des lectures nouvelles mais légères, et le 86 demande d'abord de comprendre
pourquoi une mesure échoue en silence. Ce qui manque est du **raisonnement**, pas de la donnée —
rapprocher deux sections qui ne se parlent pas. C'est une bonne nouvelle : le
coût est faible et le gain immédiat.

**Et une leçon de méthode.** Jusqu'ici les rapports étaient relus sur des
machines dont l'auteur savait tout : il complétait mentalement ce que le rapport
ne disait pas, sans s'en apercevoir. Sur une machine inconnue, confiée par
quelqu'un qui ne sait pas raconter ce qui s'est passé, **le rapport est tout ce
qu'on a** — et c'est là que ses silences se voient.

C'est le meilleur banc d'essai que ce logiciel ait rencontré. Il faudrait en
chercher d'autres.

---

## Rapport du parc du 12/10/2026 — ce qu'il a montré (points 95 à 99)

Rapport du parc généré par la console 1.8.0, 10 postes, 9 joignables. Relu à
la demande de l'auteur pour voir ce qui pourrait être amélioré en mode parc.
Noms de postes remplacés ici par leur rôle.

**Point 95 — défaut : le conseil peut dire de revenir en arrière.** Vérifié
dans `ParkComparator.Analyze` : la version « majoritaire » est simplement la
plus fréquente, et tous les autres postes sont dits « en retard ». Sur ce
rapport, le poste de l'auteur porte les pilotes de Windows les **plus récents**
(`10.0.26100.9549` contre `10.0.26100.9278` sur les deux autres) : il est
pourtant désigné « en retard », et le conseil revient à lui faire installer une
version plus ancienne. Quand chaque version n'existe que sur un poste (trois
versions, trois postes), la « majoritaire » est tirée au hasard de l'ordre, et
la phrase « une version majoritaire et quelques retardataires » est fausse.
À corriger : comparer les numéros de version, désigner les postes en retard
**par rapport à la plus récente**, et ne pas parler de majorité quand il n'y
en a pas.

**Point 96 — bruit : 88 cartes « divergence de version », dont 80 pour un seul
poste.** Ce sont des pilotes livrés avec Windows (signature `WinBuild`) : ils
changent tous ensemble à chaque mise à jour cumulative. Un poste qui a un mois
de mises à jour d'avance produit donc une carte par fichier. Même chose pour
les pilotes de Microsoft Defender (`WdFilter`, `WdBoot`, `WdNisDrv`, `KslD`),
mis à jour avec sa plateforme. Le fait utile tient en une carte : « tel poste
n'a pas le même niveau de mise à jour de Windows que les autres ». Proposé :
regrouper les pilotes de Windows en une carte « niveau de mise à jour », ceux
de Defender en une carte « plateforme Defender », et ne garder une carte par
fichier que pour les pilotes de constructeurs.

**Point 97 — le rapport ne dit pas quels postes il a comparés.** La
comparaison ne porte que sur les postes qui ont déjà un diagnostic complet
(résumé `/api/summary`) : ici **3 postes sur 10**. Les 6 autres postes
joignables, jamais analysés, sortent de la comparaison sans être nommés ; les
postes écartés parce que leur résumé vient d'une version plus ancienne ne sont
signalés que dans la barre d'état de la console, pas dans le rapport. « 2
poste(s) sur 3 » se lit alors comme « 2 postes sur 3 du parc ». À ajouter dans
le rapport : la liste des postes comparés, celle des postes non comparés avec
la raison, et le conseil de lancer un diagnostic à distance sur ces derniers.

**Point 98 — toute erreur WHEA est une « alerte critique ».** Vérifié dans
`AlertEngine.EvaluateEvent` : tout événement du fournisseur `WHEA-Logger`
donne une alerte `crit`, que l'erreur ait été **corrigée** par le matériel ou
qu'elle soit **fatale**. Le texte de l'alerte le reconnaît lui-même (« une
erreur corrigée ou fatale »). Et l'alerte archivée ne garde **ni le numéro
d'événement ni son niveau** (`ex` vide) : on ne peut plus savoir après coup ce
qui s'est passé. Sur ce rapport, trois postes sont en « alerte critique » pour
cette seule raison (35, 18 et 1 alertes archivées). Proposé : garder le numéro
et le niveau de l'événement dans l'alerte, et graduer par le **niveau** que
Windows donne lui-même à l'événement (avertissement → « à surveiller »,
erreur ou critique → « critique ») plutôt que par une table de numéros.
**À vérifier d'abord sur un des postes concernés** : quels numéros et quels
niveaux d'événements WHEA il porte réellement.

**Point 96 — livré le 19/10/2026** (go de l'auteur le même jour), à partir du
rapport du parc du 19/10/2026 10:15 : 175 cartes « divergence », dont 145 de
pilotes livrés avec Windows, 4 de Defender, 3 de composants de sécurité Microsoft
aux versions mêlées, 23 de constructeurs. `ParkComparator.Famille` classe chaque
fichier d'après ce que ses versions disent : mention `(WinBuild.` → Windows ; nom
en `Wd` et versions toutes en `4.18.` → Defender ; le reste → constructeur. Les
familles Windows et Defender donnent UNE carte chacune (à partir de deux fichiers ;
un fichier seul garde sa carte), qui nomme les postes en retard avec, pour chacun,
le nombre de fichiers en retard, et cite cinq fichiers en exemple. Les pilotes de
constructeurs gardent une carte par fichier. Effet attendu sur le cas réel : de
175 cartes à environ 25. Tests : `ParcRegroupementTests` (7 cas).

**Point 99 — plan validé le 19/10/2026 (« go pour 99a et 99b »).** 99a : une
carte de parc à partir du nombre d'événements WHEA déjà présent dans le résumé
(vérifié dans le code de la 1.8.1 : `WheaEvents` y est), donc valable tout de
suite pour tout le parc. 99b : ajouter au résumé le modèle du poste et le
composant WHEA désigné (lien PCI Express et adresse), champs AJOUTÉS sans changer
le numéro de format — une console plus ancienne les ignore, un poste plus ancien
ne les envoie pas ; visibles une fois les postes en 1.9.0.

**Lot 99a — livré le 19/10/2026.** `ParkComparator.Analyze`, section « 5 bis » :
dès que deux postes comparés ont au moins un événement WHEA sur leur période,
une carte « Erreurs matérielles (WHEA) sur N postes sur M » (« à surveiller »),
qui les nomme du plus touché au moins touché avec leur nombre, et conseille de
comparer modèle et version du BIOS, puis de tester une mise à jour sur UN poste en
mesurant les erreurs par jour avant et après. Limite connue : le résumé compte
tous les événements du fournisseur WHEA-Logger, sans distinguer corrigé et fatal
— d'où la gravité « à surveiller » ; 99b permettra de la préciser. Tests :
`ParcMemeAlerteTests` (2).

**Lot 99b — livré le 19/10/2026.** Quatre champs AJOUTÉS au résumé
(`ScanSummary`), `FormatActuel` inchangé (2) : `Modele` (fabricant et modèle
déclarés par le BIOS), `VersionBios`, `WheaFatales` (événements WHEA-Logger 18),
`WheaComposants` (composant et adresse des événements WHEA, cinq au plus, du
plus fréquent au moins fréquent). La carte « Erreurs matérielles (WHEA) » devient
critique dès qu'une erreur est fatale, indique « dont N fatale(s) » par poste, et
dit ce que les postes ont en commun (`PointsCommunsWhea`) : même modèle ou non,
même version de BIOS ou combien, et le composant mis en cause sur TOUS. Elle ne
conclut que sur les postes qui transmettent ces informations, et le dit quand une
partie du parc ne les envoie pas encore (postes antérieurs à la 1.9.0). Tests :
`ParcMemeAlerteTests` (4 de plus, 6 au total).

**Point 99 — même alerte sur plusieurs postes : le comparateur ne la rapproche
pas.** Le comparateur croise les pilotes et les codes d'arrêt, pas les alertes
de la surveillance. Les trois postes en alerte WHEA appartiennent à la même
série de machines (même convention de nom, même année) : c'est exactement le
genre de fait qu'un diagnostic individuel ne voit pas. Le rapport du parc
n'affiche pas non plus le modèle des machines, ce qui empêche de dire si elles
sont identiques. Proposé : une carte « même alerte sur N postes », avec le
modèle de chaque poste quand il est connu.

**Également relevé, déjà noté** : colonne « T° CPU » vide partout (point 91).

**Mesure sur un des trois postes, 12/10/2026** (commande lancée par l'auteur,
50 derniers événements WHEA du journal Système) : **50 événements, tous
d'identifiant 17, tous de niveau « Avertissement »** — des erreurs que le
matériel a corrigées lui-même, aucune fatale. L'« alerte critique » de ce poste
était donc exagérée. C'est aussi une **incohérence interne** : les points 48 et
49 (1.6.0) ont déjà appris au **rapport** de diagnostic à ne pas classer en
critique des erreurs WHEA corrigées ; la **surveillance temps réel** du service,
elle, n'en a pas tenu compte. Point 98 placé en 1.8.1 pour cette raison.

**Répartition décidée le 12/10/2026** (accord de l'auteur sur tous les points,
ordre laissé au choix) :

- **1.8.1** — 95, puis 98, puis 97, puis 92 et 90. Ce sont des correctifs : un
  conseil faux, une alarme exagérée, une information qui manque.
- **1.9.0** — 96 et 99, avec 79, 93 et 94. Ce sont des ajouts au comparateur.

**Lot 1 — point 95, livré le 12/10/2026.** `ParkComparator.Divergence` remplace
le calcul en ligne : si tous les numéros de version se comparent
(`ParkComparator.NumeroDeVersion`, qui lit le numéro en tête de chaîne, de deux
à quatre parties), les postes nommés sont ceux qui n'ont pas la **plus
récente**, et le conseil est de les mettre à jour vers elle ; la comparaison est
numérique (`6.0.10016.1` est plus récente que `6.0.9496.1`). Si un numéro ne se
lit pas, personne n'est désigné en retard : la carte donne la répartition et
conseille de vérifier chez l'éditeur. La phrase « une version majoritaire et
quelques retardataires » disparaît. Dix tests (`ParcDivergenceTests`, dont le
cas réel du 12/10/2026).

**Lot 2 — point 98, livré le 12/10/2026.**

- `AlertCatalog.NiveauWhea` (Core, pour être testé) : la gravité d'une alerte
  WHEA suit le niveau que Windows a donné à l'événement — avertissement ou
  information → « à surveiller » ; erreur, critique ou niveau inconnu →
  « critique », comme avant.
- `AlertEngine.EvaluateEvent` (service) reçoit le numéro et le niveau de
  l'événement (`FlightRecorderService` les lui passe) ; le numéro est gardé
  dans la valeur de l'alerte (`va`). L'anti-répétition est tenue **par niveau** :
  une erreur corrigée ne fait plus taire une erreur grave qui la suivrait dans
  le délai.
- `AlertCatalog.Localize` : une erreur corrigée s'intitule « Erreur matérielle
  corrigée (WHEA, événement 17) », sans « défaillance » ni « par le processeur »,
  et conseille de lancer un diagnostic du poste, qui nomme le lien en cause
  (points 48 et 49). Une erreur grave s'intitule « Erreur matérielle grave
  signalée (WHEA, événement N) » et garde l'ancien conseil. Une alerte écrite
  avant la 1.8.1 (sans numéro) garde son texte d'origine.
- `RulesEngine` : la carte « Alerte préventive répétée » prend son texte dans
  l'alerte la plus grave, et non plus dans la plus récente — sans quoi une
  carte critique pourrait s'intituler « erreur corrigée ».
- Neuf tests (`AlerteWheaTests`).

**Limite connue** : les alertes déjà archivées par la console avant la 1.8.1
restent « critiques » — elles ne portent ni numéro ni niveau, rien ne permet de
les reclasser après coup. Elles sortent du compte des 90 jours en vieillissant.

**Lot 3 — point 97, livré le 12/10/2026.**

**Défaut trouvé en préparant ce lot, vérifié dans le code.** Un poste jamais
analysé répond à `/api/summary` par `{"available":false,…}`. La console lisait
cette réponse comme un résumé : format 0, donc refusé, donc compté parmi les
postes « écartés : résumé produit par une version antérieure ». Sur le parc du
12/10/2026, c'est probablement le cas des six postes joignables non comparés —
la barre d'état leur aurait alors attribué une raison fausse.

- `ParkComparator.LireReponse` (Core) classe la réponse : résumé lisible, poste
  **jamais analysé**, format incompatible, ou réponse illisible.
- `ParkComparator.MotifAbsence` : jamais analysé, format incompatible,
  illisible, demande refusée, injoignable, secret maître absent ;
  `TexteMotif` le dit avec ce qu'il faut faire (« jamais analysé — lancer un
  diagnostic à distance »).
- `ParkAnalysis.Compares` (rempli par `Analyze`) et `ParkAnalysis.NonCompares`
  (rempli par la console, `ParkWindow.BtnParkReport_Click`, qui note la raison
  de chaque poste resté dehors au lieu de l'ignorer).
- `ParkComparator.Perimetre` : la phrase affichée dans le rapport du parc, juste
  sous le verdict de la comparaison — « Comparaison faite sur 3 poste(s) sur
  10 : … Non comparés (7) : … », et le rappel que les chiffres « N poste(s) sur
  M » ne comptent que les postes comparés.
- Le compteur de la barre d'état « version antérieure » ne compte plus que les
  vrais formats incompatibles ; sa phrase n'a pas été retouchée.
- Huit tests (`PerimetreComparaisonTests`).

**Lot 4 — point 92, livré le 12/10/2026.** Vérifié dans `ParkWindow.xaml` : la
barre du bas de la Supervision partageait une ligne en deux colonnes, le texte
d'état à gauche, les cinq boutons à droite. Ce choix datait de l'arrivée d'un
cinquième bouton, pour qu'un texte long ne passe jamais sous les boutons. Mais à
la taille d'ouverture de la fenêtre, il ne restait au texte qu'environ 250
pixels : le message courant (« … Tous les postes joignables sont en 1.8.0. »)
était coupé. Le texte passe sur **sa propre ligne, au-dessus des boutons**, sur
toute la largeur ; deux lignes au plus, la bulle d'aide garde le texte entier
au survol. Il ne peut toujours pas chevaucher les boutons. Aucun test : c'est
de la mise en page, à vérifier à l'œil dans la console.

## Points 93 et 94 — agir sur un seul poste depuis la Supervision

**Demande de l'auteur, 12/10/2026**, après la mise à jour du parc en 1.8.0 :
pouvoir actualiser seulement la sélection, et pas tout le parc ; pouvoir faire
un clic droit sur un poste pour mettre à jour ce poste-là seulement. Version
laissée au choix : **placée en 1.9.0**. **Précisé par l'auteur le même jour :
la demande porte sur l'onglet Supervision** ; l'onglet Inventaire n'est pas
concerné et garde ses cases à cocher et son bouton « Déployer ».

**Pourquoi la 1.9.0.** Le point 79 oblige déjà à reprendre le déploiement par
la console : le paquet devra y demander explicitement le mode parc. Mettre à
jour un poste par clic droit emprunte ce même chemin. Les faire ensemble évite
d'éprouver le déploiement deux fois, sur deux versions. La 1.10.0 (winget) a un
autre thème.

**Ce qui existe déjà, vérifié dans le code le 12/10/2026.**

- La liste de la Supervision (`LvMachines`) n'accepte qu'**un** poste
  sélectionné à la fois (`SelectedItem`) ; « Actualiser tout »
  (`BtnRefresh_Click` → `RefreshAllAsync`) interroge tout le parc.
- Mettre à jour un seul poste est **déjà possible**, mais depuis l'onglet
  Inventaire : cocher ce seul poste, puis « Déployer » (`BtnInvDeployer_Click`,
  50 postes au plus par déploiement, paquet vérifié, confirmation).

**Ce qui est à faire.**

- Point 93 : sélection multiple dans la Supervision (Ctrl / Maj) et un bouton
  « Actualiser la sélection » à côté de « Actualiser tout ».
  **Garde-fou** : « Retirer la sélection » agit aujourd'hui sur un seul poste ;
  avec la sélection multiple, retirer plusieurs postes demandera une
  confirmation qui annonce leur nombre.
- Point 94 : menu du clic droit sur un poste, avec « Mettre à jour ce poste ».
  Même chemin et mêmes contrôles que le déploiement de l'Inventaire (paquet
  vérifié, confirmation qui annonce la version actuelle du poste et celle du
  paquet), pas un second mécanisme.
  **À vérifier avant d'écrire** : le déploiement vise le nom d'ordinateur du
  domaine ; la Supervision connaît le poste par son adresse et par le nom qu'il
  annonce. Il faut s'assurer que c'est le bon poste qui est visé.
- **Validé par l'auteur le 12/10/2026** : le même menu reprend aussi les
  actions déjà présentes en bas de la fenêtre — boîte noire du poste,
  historique des alertes, diagnostic à distance — ainsi qu'« Actualiser ce
  poste ». Les boutons du bas restent en place : le menu est un raccourci de
  plus, pas un remplacement (confirmé par l'auteur le même jour).

**Décisions de l'auteur du 17/10/2026 sur la 1.9.0** (à la fin du point 79) :
la section « Windows à jour » des notes est validée — `CETCompat` reste actif ;
les articles de palisser.fr seront mis à jour à la publication.

**Point 94, lot 1 — livré le 17/10/2026.** Menu du clic droit sur un poste de
la Supervision (`ParkWindow.xaml`, `ListView.ContextMenu`) : « Actualiser ce
poste », puis « Boîte noire du poste », « Historique des alertes », « Lancer un
diagnostic à distance » — ces trois-là appellent les **mêmes gestionnaires**
que les boutons du bas, qui restent. Le menu ne s'ouvre que sur une ligne
(`LvMachines_ContextMenuOpening` remonte l'arbre visuel jusqu'à un
`ListViewItem`) : un clic droit dans une zone vide, un autre poste restant
sélectionné, aurait sinon agi sur ce poste sans qu'on le vise. « Actualiser ce
poste » (`RefreshOneAsync`) interroge ce seul poste, archive ses alertes comme
l'actualisation complète (point 43) et ne touche pas à l'état connu des autres.
« Mettre à jour ce poste » : lot 2. Aucun test automatique — interface WPF, à
vérifier à l'écran. **Vérifié à l'écran le 17/10/2026** par l'auteur, dans la console compilée : menu présent sur un poste, entrées fonctionnelles.

**Point 94, lot 2 — livré le 17/10/2026. Choix de l'auteur : option A.**
Deux façons étaient possibles : (A) « Mettre à jour ce poste » ouvre l'onglet
Inventaire avec ce seul poste coché, et le déploiement reste celui du bouton
« Déployer la sélection » ; (B) déployer directement depuis le clic droit, avec
sa propre confirmation. **B écartée** : elle aurait dupliqué la vérification du
paquet, le choix du mode parc, la liste des conséquences et la confirmation par
saisie du nombre — deux chemins pour une opération qui installe un logiciel sur
une machine distante.

- Menu : « ⬆ Mettre à jour ce poste… » en tête (les points de suspension disent
  qu'une étape suit). `MnuMettreAJourPoste_Click` **ne déploie rien** : il
  bascule sur l'onglet Inventaire, décoche tout, coche la ou les lignes du même
  poste, fait défiler jusqu'à elle et dit quoi faire ensuite.
- Correspondance des noms : `ListeDePostes.MemePoste` (Core, testée) compare les
  noms normalisés (`ParkInventory.NomWindows` : casse, forme longue, « $ ») ;
  **une adresse IP n'est jamais un nom** (« 10.0.0.5 » aurait donné « 10 »), et
  un nom invalide ne correspond jamais. Le nom affiché est essayé d'abord, puis
  l'hôte.
- Inventaire pas encore affiché, ou poste absent : **rien n'est coché**, et la
  barre d'état dit pourquoi et quoi faire.
- Onze tests (`MemePosteTests`).

**Essai à l'écran par l'auteur, 17/10/2026** : sans inventaire affiché, la barre
d'état demandait de l'actualiser d'abord ; après actualisation (et la connexion
VPN, sans laquelle l'annuaire n'est pas lu — seule la liste de la console
l'est), « Mettre à jour ce poste » coche bien **ce seul poste**, aussi bien pour
un poste du domaine que pour un poste hors domaine.

**Demande de l'auteur, même jour, traitée aussitôt** : actualiser l'inventaire
d'abord n'était pas intuitif. Le corps du bouton « Actualiser l'inventaire »
devient `ActualiserInventaireAsync()` ; « Mettre à jour ce poste » l'appelle lui-
même **si l'inventaire n'est pas encore affiché** (lecture sans risque : aucune
machine n'est contactée). S'il est déjà affiché, il n'est **pas** relu — une
relecture effacerait les vérifications faites entre-temps. Si la lecture échoue,
rien n'est coché et le message de la lecture est gardé à la suite du sien.

**Point 94 terminé le 17/10/2026** — validé par l'auteur à l'écran, CI verte.

**Point 93 — livré le 17/10/2026 (un seul lot).**

- `LvMachines` en `SelectionMode="Extended"` : Ctrl+clic, Maj+clic.
- Bouton **« 🔄 Actualiser la sélection »** à côté de « Actualiser tout »
  (`BtnRefreshSelection_Click`) : n'interroge que les postes choisis, archive
  leurs alertes (point 43), puis **resélectionne** les mêmes postes après le
  réaffichage de la liste (`Reselectionner`) — sans quoi la sélection
  disparaîtrait à chaque actualisation. « Actualiser ce poste » (point 94)
  garde aussi la ligne sélectionnée.
- **« Retirer la sélection » avec plusieurs postes** : confirmation chiffrée
  (`ConfirmationChiffreeWindow`, saisie du nombre), avec la liste des noms et ce
  que le retrait fait et ne fait pas — garde-fou prévu au plan du 12/10/2026.
  Avec un seul poste, la confirmation d'origine est inchangée.
- **Les actions qui visent UN poste** (boîte noire, historique des alertes,
  diagnostic à distance, dernier rapport, menu du clic droit) passent par
  `UnSeulPoste()` : avec plusieurs postes sélectionnés, elles le disent au lieu
  d'agir sur le premier de la sélection — que `SelectedItem` aurait rendu sans
  prévenir.
- Aucun test automatique (interface WPF). **Vérifié à l'écran par l'auteur le
  17/10/2026** (actualisation de la sélection, message « un seul poste »,
  confirmation chiffrée du retrait multiple), CI verte. **Point 93 terminé.**

**Point 100 — livré le 17/10/2026.** La colonne « Version » coupait
« ⬆ 1.8.0 — à mettre à jour ». Plutôt que d'élargir la colonne (le total des
largeurs est déjà calé sous la largeur utile de la fenêtre, voir le commentaire
de `ParkWindow.xaml`), le texte est raccourci et dit **plus** : « ⬆ 1.8.0 →
1.9.0 » (le poste, et la version vers laquelle le mettre à jour) ;
« ⚠ 1.9.1, console 1.9.0 » quand c'est la console qui est en retard. Le sens
des flèches est expliqué dans une bulle d'aide sur l'en-tête de la colonne.
La logique sort de `ParkWindow` vers `Core/VersionDuPoste.cs`, pour être testée :
six tests (`VersionDuPosteTests`), dont un sur la longueur.

## Déploiement du 19/10/2026 — et ce qu'un rapport de poste a montré (point 101)

**Déploiement de la 1.8.1 par la console, 19/10/2026.** Sept postes cochés dans
l'onglet Inventaire, tous « installé et visible de la console », service en cours
d'exécution sur chacun. Supervision : 8 postes joignables sur 10, tous en 1.8.1.
Un portable non retrouvé n'a pas été mis à jour — choix de l'auteur, pas un échec.

**Point 98 vu sur le parc.** Les alertes apparues depuis la 1.8.1 sur les postes
en alerte WHEA sont en majorité non critiques, là où celles de la 1.8.0 étaient
toutes « crit » : même constat que sur le poste de l'auteur le 17/10/2026.

**Rapport d'un poste du parc (diagnostic à distance, 19/10/2026).** Verdict
« système sain ». Une carte « Échecs de services Windows répétés (27) » portait
sur un seul service, GLPI Agent — un service **tiers**. Le texte disait bien
« c'est lui qu'il faut traiter », mais la ligne « Dans FaultTracePC », tirée de la
catégorie Logiciel, renvoyait vers sfc /scannow et DISM : la réparation de Windows
pour la panne d'un autre éditeur. C'est le défaut corrigé en 1.8.0 sur d'autres
cartes (catégorie « aucune »).

**Point 101 — corrigé le 19/10/2026.** Dans `AnalyzeServiceFailures`, la
catégorie ne vaut plus Logiciel que lorsque les échecs sont **dispersés** (trois
services ou plus pour couvrir les quatre cinquièmes) : c'est le seul cas où la
recommandation désigne elle-même les fichiers système. Un ou deux services en
cause, ou aucun nom lisible : catégorie « aucune », donc ni sfc ni DISM dans la
boîte à outils ni dans le script de réparation. Quatre tests ajoutés dans
`ServicesEnEchecTests`.

**Relevé, non conclu.** Deux événements disque 51 (« erreur lors d'une opération
de pagination ») sur un deuxième disque le 01/10, sans carte de conclusion qui les
reprenne — à vérifier avant d'en faire un point : la nature de ce disque n'est pas
établie. Un agent antivirus tiers figure dans la liste des processus alors que la
carte Protection dit « aucun autre antivirus inscrit » — ce n'est pas forcément
faux (un agent peut ne pas s'inscrire auprès du Centre de sécurité), à ne pas
corriger sans preuve.

Précision du même jour : le disque cité (`Harddisk1`) n'apparaît pas dans
l'inventaire du poste au moment du scan — un seul disque, le SSD système. C'est
compatible avec un support amovible (l'auteur signale un usage courant des clés
USB par les enseignants), sans le prouver. L'absence de carte est voulue : la
conclusion disque ne se déclenche qu'à partir de 3 événements, ou avec un écran
bleu lié au stockage — ici, 2 événements et aucun écran bleu.

**Rapport du parc du 19/10/2026, 08:59** (avant le diagnostic d'un poste encore
jamais analysé). Comparaison faite sur 4 postes sur 10. Ce qu'il confirme :

- **Point 96** : 113 cartes « divergence de version », presque toutes des pilotes
  livrés avec Windows ou avec Defender (`tpm.sys`, `stornvme.sys`, `WdFilter.sys`,
  `WdNisDrv.sys`…) — pire que les 88 du 12/10/2026.
- **Point 99** : quatre postes ont une alerte WHEA (événement 17) dans la
  Supervision, et le rapport ne les rapproche pas. Trois d'entre eux n'ont jamais
  été analysés, donc ne sont pas comparés : il faut leur diagnostic pour que le
  comparateur les voie.
- **Relevé, puis expliqué le même jour — pas un défaut** : sur un poste, le texte
  de la dernière alerte est en anglais (« Corrected hardware error (WHEA, event
  17) ») dans un rapport français. L'auteur avait passé ce poste en anglais pour
  des essais. Le texte d'une alerte est écrit par le service du poste, dans la
  langue du poste, au moment de l'alerte ; la console le rend tel quel — le
  retraduire serait inventer.

**Fichier publié par erreur, constaté le 19/10/2026.** Une copie de sauvegarde de
`parc.json`, nommée `parc.json.avant-inscription-auto` (21/09/2026), était suivie
par git depuis le commit `50260ee` et donc publiée : noms de postes et adresses IP
réels du parc. **Aucun secret** : les jetons y sont vides (vérifié sur le fichier,
champ `Token` de longueur 0 pour chaque poste). Cause : le motif `parc.json` du
`.gitignore` ne vaut que pour ce nom exact. Corrigé : motifs `parc.json.*` et
`parc*.json` ajoutés, ainsi que `*_Diagnostic_*.html` (rapports récupérés par la
console, dont le nom commence par celui du poste) ; le fichier est retiré du suivi
par `git rm --cached` (il reste sur le disque). Vérifié le même jour : plus aucun
fichier du dossier n'est à la fois non suivi et non ignoré. L'historique, lui,
garde le fichier : le réécrire (`git filter-repo`, puis envoi forcé) est une
décision de l'auteur.

## Trois diagnostics du 19/10/2026 — points 102 à 106

Diagnostics à distance de trois postes du parc encore jamais analysés, faits par
des services en 1.8.1 (le correctif du point 101 n'y est donc pas encore : la
ligne sfc/DISM sur les échecs d'un service tiers y figure toujours, c'est attendu).

**Point 102 — corrigé le 19/10/2026.** Sur un poste, le même plantage du
10/03/2026 14:52 figurait deux fois : le minidump portait `0x1000007E`,
`MEMORY.DMP` portait `0x7E` — même heure, mêmes quatre paramètres. Le
dédoublonnage comparait les codes bruts. Conséquences : deux cartes critiques,
« 3 plantages en 2 séries » et « 3 crash(s) antérieurs » au lieu de 2, et la
carte du `0x1000007E` sans description (code absent du catalogue). Microsoft
documente `0x1000007E` comme la variante « _M » de `0x7E`
(learn.microsoft.com, « Bug Check 0x1000007E SYSTEM_THREAD_EXCEPTION_NOT_HANDLED_M »).
Correctif : `BugCheckCatalog.Canonique` ramène la plage `0x10000001`–`0x10000FFF`
à son code de base, appliqué aux trois sources d'incident (vidages, Kernel-Power
41, BugCheck 1001). Volontairement limité à cette plage : `0xDEADDEAD` (plantage
provoqué à la main) a aussi ce bit levé et ne doit pas changer. Le tableau des
fichiers de vidage montre toujours le code tel qu'il est écrit dans le fichier.
Tests : `VarianteMTests` (11 cas).

**Point 103 — constaté, cause non établie.** Sur le même poste, `TmPreFilter`
(pilote de filtrage de Trend Micro) échoue à chaque démarrage avec « Le fichier
spécifié est introuvable », mais la carte « service(s) ou pilote(s) inscrit(s)
dont le programme n'existe plus » ne le cite pas.

**Premier diagnostic FAUX, à ne pas reprendre** : il disait que seuls les services
étaient vérifiés. C'est inexact — `DriverCollector` lit `Win32_SystemDriver` et
note `FichierPresent` pour chaque pilote, et `AnalyzeInscriptionsOrphelines` cite
les pilotes dont le fichier manque (test `InscriptionsOrphelinesTests`). L'erreur
venait d'une lecture du seul `ServiceCollector`. Mesure faite par l'auteur le
19/10/2026 sur SON poste avec une commande reprenant la même logique : aucune
ligne — donc pas de fausse alerte des pilotes de Windows, mais rien non plus sur
le cas en cause, qui est sur un autre poste. Hypothèses, non vérifiées :
inscription sans chemin (`ImagePath` absent — Windows prend alors un chemin par
défaut que `Win32_SystemDriver` ne rend pas), ou pilote absent de
`Win32_SystemDriver`. Mesure en lecture seule demandée sur le poste concerné.

**Mesure du 19/10/2026** (poste non encore confirmé : l'invite ne le dit pas).
Registre : `ImagePath = \??\C:\Program Files (x86)\Trend Micro\Security Agent\TmPreFlt.sys`,
`Type = 1` (pilote noyau), `Start = 2` (automatique), groupe `FSFilter Anti-Virus`.
`Win32_SystemDriver` filtré sur ce nom : **aucun résultat**. Donc l'inscription
existe dans le registre, mais WMI ne la rend pas — et `DriverCollector`, qui lit
WMI, ne peut pas la voir. Piste de correction, à décider après confirmation du
poste et de l'existence du fichier : lire les clés `Services` du registre
(`Type` 1 ou 2) en plus de WMI.

**Seconde mesure du 19/10/2026, sur les DEUX postes concernés** (nom du poste
affiché cette fois) : fichier absent, `sc.exe qc TmPreFilter` → erreur 1060
(« le service spécifié n'existe pas »), et plus aucune clé de registre. Le pilote
a donc disparu entièrement entre les deux mesures — l'auteur avait prévu de
traiter les restes de Trend Micro ce jour-là. Le cas n'est plus mesurable, et le
poste de la première mesure reste incertain. **Point 103 laissé ouvert, sans
correction** : la piste « lire le registre en plus de WMI » ne repose que sur une
mesure dont le poste n'est pas sûr. À reprendre si un autre cas apparaît. Leçon
retenue : toujours faire afficher `hostname` dans une commande de mesure.

**Pour reprendre le point 103 (proposé le 19/10/2026).** L'auteur a encore des
postes avec Trend Micro. Le cas à mesurer n'est pas un poste où Trend est installé,
mais l'état intermédiaire : Trend désinstallé, restes PAS encore nettoyés. Sur un
tel poste, une commande en lecture seule liste les inscriptions de Trend (nom en
`Tm*` ou chemin contenant « Trend Micro ») avec, pour chacune : fichier présent ou
non, et vue ou non par WMI (`Win32_SystemDriver` / `Win32_Service`). Si une
inscription dont le fichier manque n'est pas vue par WMI, la correction est
établie : lire les clés `Services` du registre en plus de WMI.

**Mesure du 19/10/2026 sur deux postes encore équipés de l'agent Trend
(Endpoint Basecamp), nom du poste affiché.** Résultat identique sur les deux :
une clé `tmWfp` **sans chemin de programme** (`ImagePath` absent), ni fichier, ni
vue par WMI ; les deux services de Basecamp, présents et vus par WMI. Ce n'est PAS
le cas du point 103 (TmPreFilter avait un chemin, un démarrage automatique et des
échecs « fichier introuvable » à chaque démarrage). Une clé sans chemin n'est
vraisemblablement pas chargée comme service par Windows — d'où son absence dans
WMI —, mais ce n'est pas vérifié : reste à savoir si elle produit des échecs de
démarrage dans le journal. Mesure complémentaire demandée (contenu de la clé et
événements du Gestionnaire de services qui la citent). Point 103 toujours ouvert.

**Mesure complémentaire du 19/10/2026, sur les deux postes** : la clé `tmWfp` ne
contient **aucune valeur** (ni chemin, ni type, ni mode de démarrage) et une seule
sous-clé ; aucun événement du journal Système (5 000 derniers) ne la cite. C'est
une coquille vide que Windows ignore : elle ne produit aucun échec, et
FaultTracePC a raison de ne pas en parler. **Conclusion : ce n'est pas le cas du
point 103**, qui reste ouvert faute d'un poste dans l'état de TmPreFilter (chemin
présent, fichier absent, échec à chaque démarrage). Relevé au passage par
l'auteur : Trend n'apparaît plus dans « Ajout/suppression de programmes » sur ces
deux postes, alors que l'agent Endpoint Basecamp y est toujours installé comme
service.

**Comment fermer le point 103 (proposé le 19/10/2026 au soir).** Il faut un
poste où un pilote est inscrit AVEC un chemin, en démarrage automatique, alors
que son fichier n'existe pas — l'état de TmPreFilter. Deux voies :
1. **Reproduire, en machine virtuelle** (préférée : déterministe, sans risque
   avec un point de contrôle). Créer une inscription de test qui pointe vers un
   fichier absent (`sc.exe create` d'un pilote noyau en démarrage automatique,
   même groupe que TmPreFilter), redémarrer, puis vérifier trois choses : le
   journal Système porte bien l'échec 7000 « fichier introuvable » ; WMI
   (`Win32_SystemDriver`) liste ou non l'inscription ; la carte des inscriptions
   orphelines de FaultTracePC la cite ou non. Retour au point de contrôle ensuite.
2. **Attendre un cas réel** : un poste qui a encore l'agent Apex One complet
   (pilotes `Tm*` présents), désinstallé sans le script de nettoyage des restes.
Lecture du résultat : si WMI ne liste pas l'inscription, la correction (lire le
registre en plus de WMI) est justifiée ; si WMI la liste et que FaultTracePC la
cite, le logiciel est juste et le cas du 19/10/2026 avait une autre cause — le
point se ferme comme non reproduit.

**REPRODUIT le 19/10/2026 au soir, dans la machine virtuelle de test.** Un pilote
noyau de test, en démarrage automatique, groupe « FSFilter Anti-Virus », pointant
vers un fichier absent, créé par `sc.exe create`. Après redémarrage : le journal
Système porte bien l'échec 7000 « Le fichier spécifié est introuvable », et
`Win32_SystemDriver` filtré sur son nom ne renvoie **RIEN**. **Cause établie** :
WMI ne liste pas un pilote dont le fichier manque ; `DriverCollector` lit WMI,
donc `FichierPresent == false` n'arrive pratiquement jamais pour ce cas, et la
carte des inscriptions orphelines ne peut pas le citer. Correction proposée,
**en attente de l'accord de l'auteur** (changement de comportement) : lire aussi
les clés `Services` du registre — pilotes (`Type` 1 ou 2) en démarrage au boot,
système ou automatique (`Start` 0 à 2), chemin absent → inscription orpheline.
Avant d'écrire le code : mesure du bruit possible (pilotes de Windows inscrits
sans fichier) avec la même règle en PowerShell, sur la VM (doit trouver le pilote
de test), sur le poste de l'auteur et sur un poste du parc.

**Mesure du bruit faite le 19/10/2026 au soir** (règle : `Type` 1 ou 2,
`Start` 0 à 2, fichier du chemin absent — chemin par défaut
`\SystemRoot\System32\drivers\<nom>.sys` quand `ImagePath` manque) : la VM
de test affiche le pilote de test, et **seulement lui** ; le poste de l'auteur et
un poste du parc n'affichent **aucune ligne**. Pas de fausse alerte des pilotes de
Windows sur ces trois machines. Il n'est donc plus utile d'allumer les portables
qui ont encore Trend pour ce point. **Reste : l'accord de l'auteur pour écrire la
correction.**

**Correction validée le 19/10/2026 (« écrire la correction validé point 103 »),
livrée le même jour.** `ServiceCollector.PilotesOrphelinsDuRegistre` lit
`HKLM\SYSTEM\CurrentControlSet\Services` (vue 64 bits) et retient les pilotes
`Type` 1 ou 2, `Start` 0 à 2, dont le fichier n'existe pas (chemin par défaut
quand `ImagePath` manque). Ils rejoignent `ServicesOrphelins` avec
`EstPilote = true`, après les collectes WMI et sans doublon. La carte des
inscriptions orphelines les cite comme « pilote », sans état ; le point 104 les
reconnaît comme cause établie. Tests : `PilotesOrphelinsTests` (14 cas).

**CI rouge sur ce lot (commit `066e185`), corrigé le même jour.** Erreur de
compilation dans un commentaire : la séquence `\n` de « drivers\nom.sys », écrite
par un script d'édition, avait été transformée en vrai retour à la ligne, coupant
le commentaire en deux. Leçon : relire tout texte contenant une barre oblique
inverse après une modification faite par script.

**Point 104 — livré le 19/10/2026.** `AnalyzeServiceFailures` met à part, avant
de compter la dispersion, les services et pilotes dont le programme est MESURÉ
comme disparu (`ProgrammeDisparu` : liste des services orphelins, ou pilote avec
`FichierPresent == false`, comparé au nom court et au nom d'affichage). La carte
le dit (« N de ces échecs viennent d'une inscription dont le programme n'existe
plus… ils sont mis à part ») et compte le reste (« des 62 autres échecs »). Si
tout est expliqué : « rien ne désigne le système », recommandation de terminer
les désinstallations. Sur le cas réel, la carte aurait conclu « deux services
concentrent 61 des 62 autres échecs » au lieu de « aucun ne domine ». Trois tests
dans `ServicesEnEchecTests`.

**Point 104 — dépend du 103.** Sur ce poste, 93 échecs se répartissent sur
quatre services (31, 31, 30, 1) : la règle des quatre cinquièmes conclut
« aucun ne domine, ce qui désigne plutôt le système » et propose sfc et DISM.
Or deux des trois premiers sont des inscriptions sans fichier (un service déjà
établi comme tel, et `TmPreFilter`), le troisième est GLPI Agent. Aucun ne
désigne Windows. Piste : retirer du calcul de dispersion les services dont la
cause est déjà établie (programme disparu), avant de compter.

**Point 105 — proposition.** Deux postes reçoivent le verdict « PILOTE
défectueux » à partir de plantages tous antérieurs à la période analysée : un
seul plantage vieux de dix mois sur l'un, des plantages de février et mars 2026
sur l'autre — dont le problème actuel, mesuré, est une série d'erreurs WHEA
corrigées sur un lien PCI Express. Le rapport le dit lui-même (« crash(s)
antérieurs à la période analysée », « le diagnostic de ces crashs est
incomplet »), mais la carte reste « Critique » et décide du verdict. À discuter :
garder la carte, mais ne plus la laisser porter le verdict quand le dernier
plantage est hors période.

**Point 105 — livré le 19/10/2026.** Nouvelle propriété `Finding.DerniereTrace`
(trace la plus récente d'une conclusion), renseignée par la carte d'écran bleu et
par la carte « Instabilité graphique ». `ComputeVerdict` écarte du calcul les
conclusions critiques dont la dernière trace précède la période
(`HorsPeriode`), et ajoute au verdict une phrase qui les signale avec la date du
dernier plantage. Les cartes restent affichées, en « Critique », avec la mention
« Antérieur à la période analysée : rappelé ici, il ne décide pas du verdict ».
Le compte des « arrêts anormaux sur la période » ne prend plus que les écrans
bleus de la période. Effet sur le cas réel : verdict sain au lieu de « PILOTE
défectueux », et plus de phrase « le problème principal vient d'un pilote ».
Tests : `PlantagesAnciensTests` (4).

**Point 106 — proposition (textes existants).** La recommandation de repli
d'un code d'arrêt inconnu dit encore « automatisé en Phase 2 », et la section
des processus « le futur mode surveillance (Phase 3) » : l'analyse WinDbg et la
surveillance existent, ce sont des mots de la feuille de route, sans sens pour
le lecteur. Par ailleurs `0x23` (FAT_FILE_SYSTEM, un plantage d'un poste du
parc) n'est pas au catalogue.

**Point 106 — livré le 19/10/2026.** Repli d'un code d'arrêt inconnu : « ce code
n'est pas encore décrit par FaultTracePC ; pour nommer le module fautif, installer
s'ils manquent les Debugging Tools for Windows ou WinDbg : FaultTracePC analysera
lui-même le fichier de vidage au prochain scan » (c'est ce que fait `CdbAnalyzer`).
Section des processus : « la boîte noire (surveillance temps réel) » au lieu du
« futur mode surveillance (Phase 3) ». Catalogue : `0x23 FAT_FILE_SYSTEM`,
catégorie Stockage, texte tiré de la page Microsoft (learn.microsoft.com, « Bug
Check 0x23 FAT_FILE_SYSTEM ») ; le conseil sur la clé USB est une déduction (FAT
est le format des clés et cartes en FAT32), écrite comme telle. Confirmation de
l'auteur le même jour : le nettoyage des restes de Trend Micro avait bien été fait
entre les deux mesures du point 103. Tests : `TextesPerimesTests` (2).

**Point 107 — corrigé le 19/10/2026.** Sur le quatrième poste diagnostiqué ce
jour, 10 plantages d'un logiciel tiers de prise en main de classe (module fautif :
une DLL de ce logiciel) : la carte « Application instable » était de catégorie
Logiciel, donc la boîte à outils proposait sfc et DISM. Même défaut que le point
101, sur une autre carte. Désormais : catégorie Logiciel seulement si le programme
qui plante est un composant de Windows (liste `BinairesSysteme`), Pilote graphique
s'il touche l'affichage (inchangé), aucune sinon.

**Point 108 — corrigé le 19/10/2026.** Même poste : verdict « MATÉRIELLE »
(erreurs WHEA sur un lien PCI Express) et, deux lignes plus bas, « Dans ton cas,
le problème principal vient d'un pilote » — parce qu'une carte critique Pilote
existait (un écran bleu de juin 2026). La phrase dépend désormais du verdict
(`HtmlReportGenerator.PiloteEnCause`). Tests : `ConseilSelonLeVerdictTests`
(4 tests, avec le point 107).

**Rapport du parc du 19/10/2026, 10:15** — 8 postes comparés sur 10 (les deux
injoignables sont le portable laissé de côté et le poste de l'auteur à son
domicile). Confirme à nouveau : point 96 (175 cartes « divergence de version »)
et point 99 (quatre postes d'une même série avec des erreurs WHEA corrigées sur
un lien PCI Express — 12, 3, 34 erreurs et l'alerte du quatrième —, sans carte qui
les rapproche). Un point commun utile est en revanche bien trouvé : un même pilote
réseau ancien sur cinq postes.

**Point 109 — relevé le 19/10/2026, cause à confirmer.** Les échecs « GLPI Agent
s'est terminé de manière inattendue » (7031) figurent sur TOUS les postes
analysés, quelle que soit la version de l'agent (trois versions différentes sur
le parc). Sur deux postes, ils tombent presque chaque jour à 20:31 et 21:00 ; sur
les autres, à des heures de cours dispersées, plus 20:31 et 21:00 certains jours
communs à tout le parc. Hypothèse : l'agent est arrêté brutalement pendant un
redémarrage ou un arrêt de Windows (tâches planifiées du soir, extinctions par
les utilisateurs) — ce qui ne serait pas une panne. **Non vérifiée** : mesure
demandée (événements 7031 rapprochés des 1074/6006 d'un même poste). Si elle se
confirme, proposition : FaultTracePC reconnaît un 7031 survenu dans la minute qui
précède un arrêt de Windows et ne le compte plus comme un échec.

**Hypothèse CONFIRMÉE le 19/10/2026** sur un poste du parc (journal Système, 7
jours) : les 9 événements 7031 de GLPI Agent tombent tous **dans la même seconde
qu'un 6006** (« le service d'Enregistrement d'événement a été arrêté », c'est-à-dire
l'arrêt de Windows). Déclencheurs relevés : les deux tâches planifiées de l'auteur
(redémarrage vers 20:30, extinction à 21:00, toutes deux par `shutdown.exe`), et un
redémarrage de mise à jour Windows (`TrustedInstaller.exe`). L'auteur confirme que
ces tâches sont les siennes, mises en place pour les postes que les utilisateurs
oublient d'éteindre. Ce n'est donc pas une panne de l'agent : FaultTracePC compte à
tort comme « échec de service » un service coupé pendant l'arrêt de Windows, et en
fait une conclusion sur chaque poste. La signature mesurable à retenir : 7031 à
quelques secondes d'un 6006 du même journal. Précision de l'auteur : 19:30 et
20:00 sont les horaires de l'ancien master (anciens postes), 20:30 et 21:00 ceux
du nouveau, repoussés à la demande — d'où les deux séries d'heures relevées.

**Point 109 — corrigé le 19/10/2026.** `EventLogCollector.ArretsDeWindows` relève
les heures des 6006 de la période (`DiagnosticReport.ArretsDeWindows`, simple
repère, non affiché). `AnalyzeServiceFailures` met de côté les arrêts inattendus
(7031, 7034) survenus entre 10 secondes avant et 1 seconde après un 6006
(`CoupePendantUnArret`) ; les échecs de DÉMARRAGE (7000, 7001) restent comptés.
La carte dit combien d'arrêts ont été mis de côté et pourquoi. Si les échecs
restants passent sous le seuil de 5, pas de carte. Effet attendu sur le parc :
plus de conclusion « GLPI Agent » sur les postes où il n'est coupé que par les
arrêts du soir. Quatre tests dans `ServicesEnEchecTests`. Ne prend effet sur un
poste qu'une fois la 1.9.0 installée (c'est le service du poste qui fait
l'analyse à distance).

**CI rouge du 19/10/2026 (commit `16479fc`), corrigé le même jour.** Le lot
104/107/108 ne compilait pas : la variable `reste` ajoutée au point 104 existait
déjà dans la même méthode (texte « et N autre(s) service(s) »). Renommée
`aCompterTotal`. Erreur de livraison : le code n'avait pas été compilé avant
d'être remis — rappel qu'un lot non compilé doit le dire, et rester petit.

**Au passage, pour le retrait de Trend Micro** (non lié au code) : l'agent Trend
(`EndpointBasecamp`) tourne sur les quatre postes analysés ce jour ; le service
de l'antivirus de remplacement, qui pèse une centaine de Mo ailleurs, n'apparaît
dans la liste des processus que sur un seul d'entre eux.

## Diagnostics du parc du 21/10/2026 — points 112 à 114

Neuf diagnostics faits le 21/10/2026 avec la 1.9.0 : sept postes du parc, le poste de l'auteur et un portable. Relus carte par carte. Trois défauts en sont sortis ; les noms des postes ne sont pas écrits ici (dépôt public).

### Point 112 — d'anciennes alertes rendaient critiques des erreurs toutes corrigées

**Constat.** Cinq verdicts sur neuf disaient « MATÉRIELLE (CPU/carte mère/alimentation ou surchauffe) ». Chaque fois, la seule carte matérielle critique était « N erreur(s) matérielle(s) CORRIGÉE(S) — lien PCI Express » (3, 6, 15, 34 et 121 erreurs, toutes d'identifiant 17), avec deux phrases qui se contredisaient dans la même carte : « Le matériel vient de signaler une erreur corrigée ou fatale. Répétées, ces erreurs annoncent une défaillance CPU, mémoire, carte mère ou alimentation », puis « Ce sont des erreurs de LIEN, pas de processeur ».

**Cause, lue dans le code.** La règle du journal (`AnalyzeWhea`) classait bien ces erreurs « à surveiller » — c'est le travail des points 48 et 49. Mais `FusionnerLesDoublons` réunit cette carte avec celle des alertes de la surveillance et garde la gravité **la plus forte des deux**. Or, sur les cinq postes, la dernière alerte WHEA de la surveillance datait d'avant le déploiement de la 1.8.1 (du 28/09 au 12/10) : des alertes sans numéro d'événement, donc « crit » par défaut — le point 98 n'a changé que les alertes écrites **après** lui. Elles restent dans la fenêtre de 30 jours jusqu'à mi-novembre : le défaut se serait éteint seul, mais en attendant le verdict, première ligne lue, était faux sur la moitié du parc.

**Correction.** `RulesEngine.RelireWheaAncienne` : quand le journal Système de la période ne contient **que** des erreurs corrigées (17), une alerte WHEA ancienne de cette période ne peut venir que de l'une d'elles — la surveillance se déclenche sur ces mêmes événements. Elle est relue comme une erreur corrigée, événement 17, avec le texte d'une erreur corrigée. Rien ne change si le journal contient une seule erreur fatale (18) ou un identifiant inconnu, ni si l'alerte est plus ancienne que le plus ancien événement WHEA lu (le journal ne peut plus répondre pour elle). C'est une copie : la boîte noire garde ses alertes telles qu'elles ont été écrites. Quatre tests de plus dans `AlerteWheaTests` (le cas réel, une fatale dans le journal, une alerte plus ancienne que le journal, une alerte récente qui garde son niveau).

**Ce que ces cinq postes ont réellement en commun** (une observation, pas une conclusion) : quatre d'entre eux ont la même carte mère (ASUS PRIME B760M-A WIFI) et la même carte graphique (RTX 5060 Ti), et leurs erreurs viennent du même port racine `0x0:0x1:0x0`. La carte du parc du point 99 doit les rapprocher. Le poste qui en compte le plus (121) est aussi le seul à porter un pilote graphique plus récent ; cela ne prouve rien à lui seul.

### Point 113 — un plantage .NET compté deux fois

**Constat.** Sur un poste : « Application instable : MicrosoftSearchInBing.exe (33 crashs) », puis « Application anciennement instable : (inconnue) (33 crashs) », dernier plantage le jour même. Le tableau des événements montre, pour chaque plantage, deux événements à la même minute : `Application Error 1000` (qui nomme l'application) et `.NET Runtime 1026`.

**Cause, lue dans le code.** `EventLogCollector` collecte les 1026 dans la catégorie « plantage d'application » mais n'en extrait pas le nom de l'application ; `AnalyzeAppCrashes` les range donc sous « (inconnue) ». Et comme aucun programme installé ne s'appelle « (inconnue) », la carte se dit « anciennement instable » pour un plantage du matin. **Corrigé le 21/10/2026.**

- `EventLogCollector.NomApplicationDotNet` lit le nom dans la première ligne du 1026 (« Application: X.exe », ou « Application : X.exe » quand le message est rendu en français).
- `RulesEngine.SansDoublonsDotNet` écarte un 1026 quand un 1000 de la même application — ou, si son nom n'a pas pu être lu, un 1000 quelconque — est enregistré dans les deux minutes. Un 1026 **seul** reste compté : c'est alors la seule trace du plantage.
- Le même tri sert au nombre de plantages d'applications gardé dans l'historique (`ScanSummary.AppCrashEvents`), pour que les deux chiffres ne divergent pas.
- Relevé en corrigeant : « (inconnue) » était écrit en dur et passait tel quel dans le rapport anglais. Il passe par la traduction (« (unknown) »).
- Tests : `PlantageDotNetTests` (lecture du nom, trois textes sans nom, un 1026 qui accompagne un 1000, un 1026 seul, deux applications différentes, un 1026 sans nom, et le cas réel — 33 paires, une seule carte).

### Point 114 — une même coupure dans deux cartes

**Constat.** Sur deux postes : « 3 coupure(s) brutale(s) sans écran bleu » (Kernel-Power 41, critique) et « 3 arrêt(s) inattendu(s) sans écran bleu associé » (événement 6008, avertissement), avec les mêmes dates (« Dernière occurrence : 12/10/2026 11:20 » dans les deux).

**Cause, lue dans le code.** Le commentaire d'`ArretsInexpliques` annonce des arrêts « restés SANS explication : ni écran bleu, ni coupure franche déjà signalée », mais le code n'écarte que les arrêts proches d'un écran bleu. **Corrigé le 21/10/2026.**

- `CoupuresFranches` : la définition d'une coupure franche (Kernel-Power 41 de code 0, sans écran bleu à moins de 5 minutes) est désormais partagée par les deux règles, au lieu d'être écrite dans l'une seulement.
- `ArretsInexpliques` écarte un 6008 journalisé à 15 minutes ou moins d'une coupure franche — la même fenêtre que pour l'événement 41 et l'écran bleu : les deux événements sont écrits au même redémarrage. Un 6008 sans coupure garde sa carte.
- **Rien de ce que la carte 6008 disait n'est perdu.** Quand des 6008 accompagnent les coupures, la carte des coupures le dit (« c'est la même coupure vue par deux journaux, comptée une seule fois ») et reprend la piste qu'elle seule portait : quelqu'un a maintenu le bouton d'alimentation — demander d'abord aux utilisateurs d'un poste partagé.
- Le verdict ne change pas de nature : ces arrêts restent comptés, par la carte des coupures, et un poste qui en a ne peut toujours pas être dit sain.
- Tests : deux de plus dans `PlantagesSansVidageTests` (le cas réel — trois paires, une seule carte, la phrase sur le bouton présente — et un 6008 sans coupure, qui garde sa carte).

### Point 115 — une mémoire saturée n'est pas une corruption du système

**Constat**, sur le diagnostic 1.9.1 d'un poste B760, le 21/10/2026 : une fois la carte WHEA ramenée à « avertissement » (point 112), le verdict est devenu « Cause la plus probable : LOGICIELLE (corruption système ou application) ». La seule carte critique est « Alerte préventive répétée (2×) : Mémoire virtuelle presque saturée : 99 % » (08/10 et 13/10), et la boîte à outils y propose « sfc /scannow », puis « DISM ». Même chose le 21/10 sur un autre poste, en 1.9.0 : verdict « LOGICIELLE » tiré de « Mémoire saturée : Windows a détecté l'épuisement de la mémoire virtuelle (2×) ».

**Cause, lue dans le code.** Les alertes `commit` et `exhaustion`, et la conclusion tirée de l'événement 2004, sont rangées dans la catégorie `Software`. Or cette catégorie porte deux choses fausses ici : le verdict « corruption système ou application », et l'indication de la boîte à outils « sfc /scannow puis DISM » — qui réparent des fichiers de Windows, pas un programme qui consomme toute la mémoire. C'est le même défaut que les points 101 et 107.

**Pourquoi ce n'est pas corrigé d'office.** La catégorie `Memory` existe, mais elle veut dire « la RAM physique » : elle ferait conseiller un diagnostic mémoire qui redémarre la machine, tout aussi faux. `None` retirerait le conseil faux, mais le verdict deviendrait « Pannes détectées — voir le détail ». La correction propre est une **catégorie de plus**, par exemple « Ressources » : verdict « SATURATION DE LA MÉMOIRE (un programme ou le fichier d'échange) », aucune réparation de Windows proposée, et la recommandation de la carte, qui est juste (fermer ce qui consomme, nommer les processus), mise en avant. Une catégorie touche aussi les libellés du rapport, le script de réparation et les résumés envoyés à la console : c'est un changement de comportement, à décider avec l'auteur.

### Relevé, sans être un défaut

- **Deux cartes batterie sur un portable** : « usée — 79 % » (25 110 sur 31 920 mWh, 105 cycles) et « en bon état — 100 % » (72 150 sur 72 150 mWh, 60 cycles). **Réponse de l'auteur, 21/10/2026 : ce portable a bien deux batteries.** Ce n'est pas un défaut.
- **Le service de surveillance de FaultTracePC s'est arrêté une fois de manière inattendue** sur un poste (30/09/2026, 23:48). Une seule fois ; à regarder si cela se répète.
- **Plantages de Veyon** (`veyon-server.exe`, `veyon-worker.exe`) sur trois postes : un logiciel tiers, rien à corriger ici.
- **Restes de Trend Micro** sur un poste : `TmPreFilter` n'a pas pu démarrer 31 fois. Cohérent avec le retrait d'Apex One en cours.

---

## Point 111 — des couches de filtrage listées comme des cartes réseau

Relevé le 19/10/2026 dans le rapport de la VM d'essai (lot 4 de la 1.9.0) : la fiche « Cartes réseau » listait, comme des cartes à part, des lignes du type « …-WFP Native MAC Layer LightWeight Filter-0000 » et « …-QoS Packet Scheduler-0000 ». Elles ne faussaient aucune conclusion — elles n'étaient pas comptées comme physiques — mais elles noyaient les vraies cartes.

### Ce que les mesures du 20/10/2026 ont montré

- **Dans PowerShell 5.1, la liste est propre.** Sur le poste de l'auteur, `[System.Net.NetworkInformation.NetworkInterface]::GetAllNetworkInterfaces()` rend 9 cartes et la boucle locale, aucune couche de filtrage. Dans la VM, deux interfaces seulement, avec ou sans droits d'administrateur.
- **Fausse piste écartée : les droits d'administrateur.** Même résultat dans la VM avec et sans élévation.
- **Mesure impossible à refaire dans la VM** : elle avait été restaurée à son état « propre » juste après les mises à jour de Windows. PowerShell 7 n'y est pas installé.
- **La mesure qui a tranché : le rapport FaultTracePC du poste de l'auteur (20/10/2026, 14:35).** La fiche « Cartes réseau » y compte **84 lignes** : 19 interfaces — les 9 cartes que montre PowerShell 5.1, plus 10 interfaces cachées (cinq « WAN Miniport », « RAS Async Adapter », l'adaptateur du débogueur du noyau, « vSwitch (Default Switch) », un « Remote NDIS », une carte Realtek USB débranchée) — **puis 65 couches de filtrage** : WFP (deux sortes), QoS, Npcap, filtres de VirtualBox et de Fortinet, extension de commutateur Hyper-V. Le même appel, sur le même poste, rend donc sous **.NET 10** (FaultTracePC) les interfaces cachées et les couches de filtrage, et sous **.NET Framework** (PowerShell 5.1) ni les unes ni les autres. *(Correction du 21/10/2026 : ce paragraphe disait d'abord « les 9 mêmes cartes, puis une trentaine de couches ». C'était une lecture faite par Claude sur un extrait tronqué de la fiche ; le décompte complet est celui-ci, vérifié sur le même rapport.)* La cause précise, dans le code de .NET, n'a pas été cherchée.

### La correction

- **La forme, pas une liste de noms.** Windows nomme une couche « [nom de la carte]-[nom du filtre]-0000 » et la décrit « [nom du pilote]-[nom du filtre]-0000 ». Le nom et la description finissent donc par **le même morceau** « -[filtre]-0000 ». Une vraie carte n'a jamais cette fin commune. La règle (`NetworkCollector.EstCoucheDeFiltrage`) cherche cette fin commune ; elle ne dépend d'aucun nom de filtre, donc d'aucune liste à tenir à jour.
- **La carte de base n'a pas besoin d'être listée.** La règle ne regarde que la couche elle-même. *(Correction du 21/10/2026 : ce point disait d'abord que « Connexion au réseau local* 8 » et « vSwitch (Default Switch) » n'étaient pas rendues par .NET, et qu'une règle fondée sur le nom de la carte de base aurait donc échoué. C'est faux : ces deux interfaces figurent dans la fiche, plus bas que l'extrait lu ce jour-là. La règle retenue reste la bonne — elle ne dépend pas de l'ordre ni de la présence de la carte de base — mais pas pour la raison donnée.)*
- **Comptées, pas jetées.** `NetworkInfo.FilterLayerCount` garde leur nombre, et la fiche finit par « et N couche(s) de filtrage posée(s) sur ces cartes (pare-feu de Windows, QoS, capture réseau…) : ce ne sont pas des cartes, elles ne sont pas listées ».

**Fichiers touchés** : `Models.cs` (`NetworkInfo.FilterLayerCount`), `Collectors/NetworkCollector.cs` (`EstCoucheDeFiltrage`, appelée avant de créer une carte), `Report/HtmlReportGenerator.cs` (fiche « Cartes réseau »), `ReseauFauxPositifsTests.cs` — sept couches et neuf des vraies interfaces relevées dans le rapport réel, plus trois cas limites (fin numérotée d'un seul côté, fin numérotée sans nom de filtre, chaînes vides).

---

## Point 110 — un retour en arrière présenté comme une mise à jour

Relevé le 19/10/2026 dans le diagnostic 1.9.0 du poste de l'auteur, section « Évolution depuis le dernier scan » : « Pilotes mis à jour » listait un fichier passé de `10.0.26100.9549` à `10.0.26100.8875`. La version a **baissé** : ce n'est pas une mise à jour.

**Pourquoi c'est un défaut et pas un détail.** La comparaison ne regardait que si la version avait *changé*, jamais dans quel sens. Or un retour en arrière dit autre chose qu'une mise à jour : une mise à jour désinstallée, une restauration du système, ou un pilote remplacé par un plus ancien. Le ranger parmi les mises à jour cache cette information à qui lit le rapport.

**Corrigé le 20/10/2026.**

- Les deux numéros sont lus comme des **versions**, pas comme du texte, avec la fonction déjà utilisée par le comparateur de parc (`ParkComparator.NumeroDeVersion`). En texte, « 10.0.26100.10001 » passerait avant « 10.0.26100.9549 » ; en version, il passe après. Un suffixe comme « (WinBuild.160101.0800) » est ignoré.
- Une version qui baisse va dans une liste à part, `DriverDowngrades`, affichée sous « ↩️ Pilotes revenus à une version plus ancienne » (« Drivers back to an older version »), plafonnée à 12 lignes comme l'autre.
- Quand l'un des deux numéros ne se lit pas, le sens est inconnu : la ligne reste parmi les mises à jour, comme avant. Rien n'est conclu sans mesure.
- **Le point 55 ne change pas.** Un retour en arrière reste un *remplacement* : si deux versions différentes du pilote accusé ont planté avec la même signature, la version n'est pas en cause. Seul l'affichage sépare les deux listes.

**Fichiers touchés** : `Models.cs` (`ScanComparison.DriverDowngrades`), `Report/ScanHistory.cs` (`Compare`), `Report/HtmlReportGenerator.cs` (une ligne de plus dans « Évolution depuis le dernier scan »), `PlafondEtVersionsTests.cs` (quatre tests : retour en arrière, comparaison numérique et non textuelle, suffixe de construction, version illisible).

**Non traité, relevé en écrivant ce point** : deux chaînes différentes portant le même numéro (même version, suffixe différent) restent comptées comme une mise à jour. Jamais constaté ; à ne corriger que si un rapport le montre.

**La cause du retour en arrière sur le poste de l'auteur n'est pas établie.** Le correctif ne la cherche pas : il dit ce qui a été mesuré, dans le bon sens.

---

## Point 90 — un relevé annoncé toutes les 10 secondes, fait toutes les 12

Constaté le 10/10/2026 en ouvrant à distance la boîte noire d'un poste mis à
jour en 1.8.0 : les relevés tombent à 12:24:40, 12:24:51, 12:25:04, 12:25:16,
12:25:28… — un écart de **12 secondes environ**, alors que la fenêtre annonce en
tête « UN RELEVÉ TOUTES LES 10 SECONDES », et que les notes de la 1.7.1 le
disaient aussi.

**Cause, lue dans le code** : le service attend 10 secondes **après** chaque
relevé (`Task.Delay(SampleInterval)` en fin de boucle), et un relevé lui-même
prend du temps — lecture des capteurs, mémoire, et toutes les quelques fois la
liste des processus. L'intervalle réel est donc 10 secondes **plus** la durée du
relevé. Ce n'est pas une panne ; c'est une affirmation inexacte.

**Deux corrections possibles** : caler la boucle sur une horloge à 10 secondes
fixes (le texte redevient vrai), ou corriger le texte (« un relevé toutes les
10 à 15 secondes environ »).

**Décidé le 10/10/2026 par l'auteur : la première** — caler le service sur une
horloge fixe. **Après** la publication de la 1.8.0, pour ne pas toucher au
service d'un MSI déjà éprouvé. **Version fixée le 12/10/2026 : 1.8.1**, dernier lot.

**Lot 5 de la 1.8.1 — livré le 12/10/2026.** Dans `FlightRecorderService`, la
boucle `while … await Task.Delay(SampleInterval)` devient une boucle
`do … while (await horloge.WaitForNextTickAsync(…))` sur un `PeriodicTimer` de
10 secondes : le premier relevé part tout de suite, les suivants tombent sur
une horloge fixe, quelle que soit la durée du relevé. Si un relevé dépasse
10 secondes, `PeriodicTimer` ne rattrape qu'un seul battement, jamais une
rafale. L'arrêt du service passe toujours par l'annulation du jeton (même
`OperationCanceledException`, déjà interceptée). **Aucun test automatique** :
le service n'est pas référencé par le projet de tests. **À vérifier sur pièce**
après installation : dans la boîte noire, les heures des relevés doivent
avancer de 10 secondes (à une seconde près), et non plus de 12.

---

## Point 89 — voir qu'un pilote a disparu, et lequel

Question posée le 09/10/2026 : « sur un PC le pilote avait disparu — est-ce que
la correction permet de voir qu'un pilote est manquant et lequel ? »

**Demande notée le 09/10/2026, pour plus tard — rien n'est commencé** :
pouvoir **voir le matériel** de la machine, afin de savoir **si un pilote
manque** et **quel pilote est attendu**, c'est-à-dire au minimum **la marque**
du matériel concerné (NVIDIA, Intel, AMD…). C'est l'angle mort C ci-dessous,
complété par la lecture du code fabricant décrite dans « Le cas réel ».

**Réponse : non.** Le lot A du point 80 dit pourquoi le pilote fautif d'un écran
bleu n'est pas nommé ; il ne cherche pas de pilote manquant. Et en vérifiant le
code, le logiciel ne sait aujourd'hui voir un pilote disparu **par aucun
chemin**. Quatre angles morts, chacun constaté dans le code le 09/10/2026 :

| # | Angle mort | Où | Donnée déjà là ? |
|---|---|---|---|
| A | **Un pilote inscrit dont le fichier `.sys` a disparu est collecté… puis jeté sans un mot.** Le collecteur lit tous les pilotes inscrits (`Win32_SystemDriver`, quel que soit leur état), constate que le fichier n'existe pas (`File.Exists` faux), n'en lit donc pas la version — et le résumé d'historique écarte ensuite toute ligne sans version. | `DriverCollector`, `ScanHistory.Summarize` | **oui** |
| B | **La comparaison entre deux analyses ignore les pilotes apparus ou disparus.** Elle ne parcourt que les pilotes présents aujourd'hui, et saute ceux qu'elle ne retrouve pas hier (`continue`). Un pilote présent hier et absent aujourd'hui n'est jamais parcouru. | `ScanHistory`, comparaison des versions | **oui** |
| C | **Les périphériques sans pilote ou en erreur** — le point d'exclamation jaune du Gestionnaire de périphériques — ne sont pas lus du tout. | aucun collecteur | non |
| D | **L'événement 7026** (« les pilotes de démarrage suivants n'ont pas pu être chargés ») n'est pas collecté ; seuls 7000, 7001, 7031 et 7034 le sont. | `EventLogCollector` | non |

**A et B relèvent exactement du thème de la 1.8.0** : la donnée est collectée,
il manque de la dire. C et D demandent une lecture nouvelle.

**Angle mort A traité le 10/10/2026** par le lot 2a du point 85 : un pilote inscrit
dont le fichier manque est désormais signalé. B, C et D restent à faire.

**Le risque à ne pas courir : les faux positifs.** Sur une machine saine, des
pilotes inscrits sans fichier peuvent exister — restes d'un logiciel désinstallé,
composants optionnels jamais posés. Leur nombre sur un Windows sain n'est **pas
connu** à ce jour. Les signaler tous ferait crier le rapport sur une machine en
bonne santé, ce que la 1.6.1 a appris à ne plus faire. Avant d'écrire la règle,
il faut **mesurer** sur au moins une machine saine combien il y en a, et avec
quel mode de démarrage. L'intuition à vérifier : un pilote en démarrage
`Boot`, `System` ou `Auto` sans fichier est grave — Windows essaie de le
charger à chaque démarrage — tandis qu'un pilote `Manual` ou `Disabled` sans
fichier n'est qu'une inscription orpheline.

### Le cas réel qui motive ce point — angle mort C

Raconté par l'auteur le 09/10/2026, sur un poste qu'il connaissait : affichage
bloqué en basse résolution, impossible de la changer. Dans le Gestionnaire de
périphériques : un **matériel sans pilote** et **aucune carte graphique**. À
première vue on aurait pu croire à du matériel Intel ; l'auteur savait que
c'était du NVIDIA, a réinstallé le pilote depuis sa sauvegarde réseau, et tout
est revenu. FaultTracePC n'était pas installé sur ce poste.

**Ce que FaultTracePC aurait dit aujourd'hui sur ce poste — vérifié dans le
code, pas sur la machine** : **aucune conclusion**. Les cartes graphiques sont
lues (`Win32_VideoController` : nom, version et date du pilote, état) et
affichées telles quelles dans la carte « Cartes graphiques » des détails
techniques. Sur ce poste, on y aurait sans doute lu le nom de l'adaptateur
d'affichage de base de Microsoft — mais aucune règle ne le relève, et rien ne
dit de quel fabricant est la vraie carte.

**Ce qu'il faudrait pour répondre « il manque un pilote, et c'est du NVIDIA »** :

1. lire les périphériques en erreur avec leur **code de problème** (le code 28
   est « pilote non installé ») ;
2. lire l'**identifiant matériel** de chacun : il contient le code du
   fabricant, que Windows connaît même sans pilote — `VEN_10DE` NVIDIA,
   `VEN_8086` Intel, `VEN_1002` AMD. C'est ce qui aurait levé le doute
   Intel / NVIDIA ;
3. relever comme anomalie une carte graphique servie par l'adaptateur
   d'affichage de base de Microsoft.

**Mesure de référence sur une machine saine** (poste de l'auteur, 09/10/2026) :

- angle mort A : **zéro** pilote inscrit dont le fichier manque ;
- angle mort C : **un** périphérique en erreur, l'adaptateur réseau virtuel
  d'un client VPN, dont l'identifiant commence par `ROOT\` — un périphérique
  créé par un logiciel, pas une pièce de la machine. C'est exactement le faux
  positif à écarter : la règle ne devra retenir que les vrais bus matériels
  (`PCI\`, `USB\`, `HDAUDIO\`…), ou au moins classer à part les `ROOT\`.

Une seule machine saine ne suffit pas à fixer une règle ; elle suffit à montrer
que le filtre `ROOT\` est nécessaire.

**Une retombée qui vaut d'être notée.** L'angle mort A est celui qui aurait
répondu, le 24/09/2026, à la question « ai-je bien fini de désinstaller l'ancien
antivirus ? » si une inscription de pilote était restée derrière lui. Il
rejoint le point 85.

---

## Point 88 — réveiller un poste depuis la console

Demandé le 05/10/2026. **Noté pour plus tard, rien n'est commencé.** Les choix d'interface sont arrêtés et le code de l'autre outil a été comparé le même jour : le jour où ce point sera ouvert, il n'y aura plus qu'à écrire.

### Ce qui est demandé

Pouvoir démarrer un poste éteint par le réseau (Wake-on-LAN) depuis la console,
**sans passer par un déploiement**. L'adresse MAC vient :

- du **serveur DHCP du domaine**, interrogé pour le poste visé ;
- ou d'un **fichier CSV**, comme le fait déjà l'autre outil d'administration de
  l'auteur.

### Ce qui existe déjà — et c'est presque tout

Le réveil n'est pas à écrire : il est **dans le script de déploiement depuis sa
première version**, et il a été éprouvé sur le parc réel le 21/09/2026.

| Brique | État |
|---|---|
| Envoi du paquet de réveil, choix de la carte réseau, attente de la réponse | **fait** — `Send-Reveil` |
| Recherche de l'adresse MAC : DHCP, puis CSV, puis cache ARP | **fait** — `Get-MacDuPoste` |
| Mode « ne fait QUE réveiller », sans copie ni installation | **fait** — paramètre `-ReveilSeulement` du script (choix 4 du menu quand on le lance à la main) |
| Réglage du serveur DHCP dans la console | **fait** — point B du 21/09/2026 |
| Réglage de l'annuaire d'adresses MAC, transmis au script | **fait** — point C du 21/09/2026 |
| **Un bouton dans la console pour s'en servir** | **manque** |

La console sait « Vérifier » et « Déployer ». Elle ne sait pas « Réveiller » :
aujourd'hui, pour allumer un poste sans rien y installer, il faut lancer le
script à la main. **Ce point est donc un travail d'interface, pas de moteur.**

### Décidé le 05/10/2026

| Question | Réponse |
|---|---|
| Où mettre le bouton | **dans les deux onglets** : Inventaire, sur les postes cochés, à côté de « Vérifier » et « Déployer » ; Supervision, sur le poste sélectionné |
| Confirmation avant d'allumer plusieurs postes | **oui** — même principe que le déploiement : on annonce le nombre avant, pas après |
| De l'autre outil, que reprendre | **uniquement le réveil**, rien d'autre |

### Redemandé le 19/10/2026 — un poste ou une sélection

L'auteur a redemandé ce point le soir de la publication de la 1.9.0 : « réveiller un pc ou une sélection de pc en mode WoL ». La décision du 05/10/2026 disait « Supervision, sur le poste sélectionné » : à cette date, la Supervision ne savait sélectionner qu'un poste. Depuis la 1.9.0 (points 93 et 94), elle sait sélectionner plusieurs postes (Ctrl+clic, Maj+clic) et elle a un menu au clic droit. La demande se lit donc ainsi, à confirmer le jour où le point est ouvert :

- une entrée « Réveiller » dans le menu du clic droit de la Supervision, qui agit sur **tous les postes sélectionnés** ;
- dans l'Inventaire, sur les postes cochés, comme décidé le 05/10/2026 ;
- au-delà d'un poste, une confirmation qui annonce le nombre avant d'allumer, comme le déploiement ;
- le compte rendu dit, poste par poste, d'où vient l'adresse MAC ou laquelle des trois sources a manqué ;
- le défaut de `Update-LigneCsv` décrit plus bas est corrigé **avant**, puisqu'un réveil de plusieurs postes réécrit le fichier plusieurs fois.

Placement proposé le 19/10/2026, non décidé : après le bloc winget (1.10.0), qui a déjà été décalé deux fois. Raison : le réveil n'est pas bloquant aujourd'hui — l'autre outil d'administration de l'auteur réveille déjà les postes, et le script de déploiement sait le faire seul (`-ReveilSeulement`).

### Ce que la lecture de l'autre outil a appris

Son code de réveil a été lu le 05/10/2026 pour le comparer à celui-ci. Les deux
sont cousins : la recherche dans le DHCP de l'autre outil est **reprise de ce
script-ci**, son commentaire le dit.

| | Ce logiciel | L'autre outil |
|---|---|---|
| Recherche de l'adresse MAC | DHCP, puis CSV, puis cache ARP | DHCP, puis son CSV, puis un `postes.csv` trouvé dans ses dossiers, puis **saisie à la main** |
| Colonnes du CSV | `Nom;MAC` | `Nom;MAC;IP;Date` |
| Séparateur | point-virgule | point-virgule |
| D'où vient le CSV | écrit après une réponse du DHCP | écrit **aussi** chaque fois qu'un poste ALLUMÉ est examiné |
| Envoi du paquet | par la carte qui mène au poste, à défaut toutes | par toutes les cartes |
| Cibles et ports | diffusion du sous-réseau puis `255.255.255.255`, ports 9 et 7 | les mêmes |

**En lecture, les deux fichiers sont compatibles tels quels.** Même séparateur,
mêmes noms de colonnes `Nom` et `MAC` ; les colonnes en plus sont ignorées, la
casse du nom ne compte pas, et l'adresse est acceptée sous n'importe quelle
écriture puisque seuls ses chiffres hexadécimaux sont gardés. Il n'y a donc
**aucun format à apprendre** : le champ « Annuaire d'adresses MAC » peut déjà
désigner le fichier de l'autre outil.

**En écriture, non — et c'est un défaut à corriger AVANT de les brancher
ensemble.** `Update-LigneCsv` réécrit le fichier entier avec une ligne neuve qui
n'a que deux colonnes. Or PowerShell 5.1 décide des colonnes d'un CSV d'après
**la première ligne qu'il écrit** : si le poste réveillé est le premier dans
l'ordre alphabétique, le fichier ressort avec `Nom;MAC` seulement, et les
colonnes `IP` et `Date` de **toutes** les autres lignes sont perdues. Si un
autre poste est premier, rien n'est perdu. Un défaut qui ne se produit qu'une
fois sur plusieurs est le pire à retrouver après coup.

À faire en même temps que le bouton : `Update-LigneCsv` doit **conserver les
colonnes qu'il ne connaît pas** — relire l'entête existant, et écrire la ligne
neuve avec les mêmes colonnes, vides là où il n'a rien à mettre.

**Deux idées de l'autre outil qui valent d'être reprises**, à décider le moment
venu :

- **Apprendre l'adresse d'un poste quand il est allumé.** C'est ce qui rend
  l'autre outil indépendant du DHCP. Ici ce serait encore plus simple : la
  console interroge déjà chaque poste supervisé à chaque relève, et le service
  qui lui répond connaît sa propre carte réseau. Un poste supervisé une fois
  serait réveillable ensuite **sans serveur DHCP du tout** — ce qui compte pour
  qui n'a pas de DHCP Windows, puisque la recherche actuelle ne sait interroger
  que celui-là. C'est un ajout au protocole entre le poste et la console : une
  décision à part, pas un détail du bouton. À concilier avec le choix du
  19/09/2026 de ne jamais **afficher** l'adresse MAC.
- **La saisie à la main en dernier recours**, avec contrôle de la forme. Quand
  les trois sources n'ont rien, la console pourrait demander l'adresse au lieu
  de renvoyer vers un fichier à modifier.

### Les limites connues, à ne pas redécouvrir

- **Le paquet de réveil ne traverse pas les routeurs.** Un poste sur un autre
  sous-réseau que la console ne sera pas réveillé. Constaté et écrit le
  21/09/2026 ; le script le rappelle à chaque envoi.
- **Un portable en Wi-Fi ne se réveille pas** par ce moyen.
- **Le BIOS et la carte réseau doivent l'autoriser** : un poste a résisté le
  21/09/2026 alors que son adresse était trouvée et le paquet envoyé.
- **Interroger le DHCP demande d'y avoir accès** : gestion à distance ouverte
  sur le serveur, et droit de lire les baux. Sans cela, le CSV est la seule
  source — d'où l'intérêt de garder les deux.

### Sa place dans le découpage

C'est une fonction de **parc**. Elle relève donc du point 79 : le jour où le
mode parc deviendra une fonctionnalité facultative du paquet, ce bouton en fera
partie et n'apparaîtra pas chez un particulier.

---

## Deux publics, un seul paquet — décision du 21/09/2026

**Le constat qui ouvre la question.** Depuis la 1.7.1, le logiciel ne s'adresse
plus à un seul public. Un particulier veut diagnostiquer sa machine ; un
administrateur veut piloter un parc. Ce sont deux besoins, deux niveaux de
risque et deux craintes différentes — et aujourd'hui ils reçoivent le même
paquet, avec la console de parc, ses ports, son secret maître et
`System.DirectoryServices.dll` que le particulier n'emploiera jamais.

### Ce qui a été décidé

**Le mode parc devient une fonctionnalité facultative du paquet**, posée à
l'installation par le même mécanisme que le raccourci du Bureau — celui qui
existe déjà (`ADDLOCAL=Main,DesktopShortcutFeature`).

- un particulier installe et ne voit **jamais** l'onglet de parc ;
- un administrateur ajoute la fonctionnalité par une option de ligne de commande,
  ce qui convient aussi à un déploiement par stratégie de groupe ;
- **un seul paquet, un seul numéro de version, une seule construction, une seule
  série de tests.**

Le reste du problème — des notes de version qui parlent de parc à quelqu'un qui
n'en a pas — se règle dans le texte et non dans le paquet : deux sections
distinctes dans les notes et dans l'article, l'une pour la machine seule, l'autre
pour le parc.

**Quand :** pas tout de suite. La 1.7.1 a trois jours d'usage réel. Cette
décision attend une version ultérieure, sans date.

**Question de l'auteur le 10/10/2026** : « la 1.8.0 demandera-t-elle à
l'installation d'activer le mode parc ? ». **Réponse : non, pas en l'état.** Le
contenu de la 1.8.0 est arrêté aux points 80 à 87 ; ce point (79) reste « version
ultérieure, sans date ». La 1.8.0 garde donc le mode parc **inclus comme en
1.7.1**. Précision au passage : la décision ne prévoit pas une **question** posée
pendant l'installation, mais une **fonctionnalité absente par défaut**, ajoutée
par une option de ligne de commande (déploiement, stratégie de groupe). Décision
de l'auteur attendue : l'ajouter à la 1.8.0, ou la laisser à plus tard.

**Réponse de l'auteur, même jour** : d'accord sur le fond — un particulier n'a
pas besoin du mode parc ; la version n'avait simplement pas été fixée. Deux
exigences : **le mode parc déjà en place sur les postes ne doit pas être
perdu** ; et il trouverait pratique de pouvoir **l'activer à l'installation**.

**Ce que dit l'installateur actuel, vérifié dans `installer/FaultTracePC.wxs`** :
l'installation interactive affiche **déjà** la liste des fonctionnalités avec
des cases à cocher (`WixUI_FeatureTree` — c'est là qu'apparaît le raccourci du
Bureau). Une fonctionnalité « Mode parc » y apparaîtrait donc comme **une case
à cocher pendant l'installation**, non cochée par défaut — ce que l'auteur
souhaite — **et** resterait pilotable en silencieux par `ADDLOCAL` pour un
déploiement. Les deux formes ne s'opposent pas : c'est le même mécanisme.

**Le piège à traiter le jour venu, noté pour ne pas l'oublier.** À une mise à
jour, Windows Installer reporte l'état des fonctionnalités qui existaient dans
l'ancienne version. Or en 1.7.1 le parc fait partie de « Main » : une nouvelle
fonctionnalité « Mode parc », absente par défaut, **ne serait pas posée** sur un
poste mis à jour — il sortirait de la supervision sans prévenir. Il faudra que
l'installateur détecte un poste déjà en mode parc et coche la fonctionnalité
d'office, puis l'éprouver sur un vrai poste du parc avant toute publication.
Comportement exact de la migration à **vérifier** dans la documentation de WiX
et à l'essai, pas à supposer.

**Version décidée le 10/10/2026 par l'auteur : la 1.9.0**, juste après la 1.8.0
et avant winget (qui passe en 1.10.0). Motif : winget ouvre le logiciel au
grand public ; il vaut mieux que le mode parc y soit déjà facultatif.

### Plan du point 79 — lot 0 (lecture seule), 17/10/2026

Rien n'est codé à ce stade. Ce qui suit vient de la lecture du code et de
l'installateur actuels ; les décisions marquées « à valider » attendent l'auteur.

**Ce qu'est le « mode parc » aujourd'hui, vérifié dans le code.** Ce ne sont pas
une, mais deux choses distinctes, qui ne vivent pas au même endroit :

1. **Le côté poste client** — le service de surveillance ouvre son API en
   lecture seule **si et seulement si** `C:\ProgramData\FaultTracePC\remote.json`
   dit `"Mode": "Client"` (`RemoteConfig`, `TelemetryService`). Ce fichier est
   écrit par `FaultTracePC.Cli.exe --configure-remote` (étape « mise en mode
   parc » du script de déploiement, qui pose aussi la règle de pare-feu) ou par
   la fenêtre « Réseau ». **L'installateur ne le crée ni ne le supprime** : il
   survit déjà aux mises à jour.
2. **Le côté administrateur** — les boutons « Parc » (console) et « Réseau »
   (`RemoteConfigWindow`) de la fenêtre principale, toujours visibles.

L'installateur (`installer/FaultTracePC.wxs`) n'a que deux fonctionnalités :
`Main` (obligatoire : fichiers, service, raccourcis) et `DesktopShortcutFeature`.
L'interface `WixUI_FeatureTree` affiche déjà l'arbre avec des cases. Le script de
déploiement installe par `msiexec /i … /qn FTPCLANG=…`, **sans `ADDLOCAL`**.
`MajorUpgrade` est en place, avec `AllowSameVersionUpgrades="yes"`.

**Proposition (à valider) — D1 : la fonctionnalité ne gouverne que l'interface
d'administration.** Une fonctionnalité « Mode parc » (absente par défaut) pose
une valeur de registre sous `HKLM\SOFTWARE\FaultTracePC` ; l'application lit
cette valeur pour afficher ou non les boutons « Parc » et « Réseau ». **Le
service, lui, continue de n'obéir qu'à `remote.json`.** Conséquence voulue : un
poste client déjà en mode parc **ne peut pas** sortir de la supervision à cause
de cette fonctionnalité, quelle que soit la façon dont il est mis à jour — le
piège noté plus haut disparaît par construction au lieu d'être rattrapé par
une migration. Option écartée : faire dépendre l'API du service de la
fonctionnalité — c'est exactement ce qui ferait perdre la supervision à un
poste mis à jour sans la case.

**D2 — mise à jour depuis une 1.8.x ou plus ancienne (à valider).** Proposé :
toute mise à jour depuis une version qui ne connaissait pas la fonctionnalité
la **coche d'office** (condition de niveau sur la détection d'une mise à
jour). Rien ne disparaît chez personne ; seule une **installation neuve** l'a
absente par défaut. Variante plus fine, non retenue d'emblée : ne la cocher que
si `remote.json` existe — mais un administrateur qui n'utilise que la console
n'a pas forcément ce fichier, et le perdrait.

**D3 — affichage de « Réseau » sur un poste client sans la fonctionnalité (à
valider).** Proposé : si `remote.json` dit « Client », le bouton « Réseau » reste
visible même sans la fonctionnalité — un poste exposé sur le réseau doit pouvoir
le montrer à qui s'assoit devant.

**D4 — la version portable (ZIP), qui n'a ni installateur ni registre (à
valider).** Proposé : elle garde la console visible, comme aujourd'hui — c'est
l'outil de l'administrateur sur clé USB. Il faut donc que `Main` pose aussi une
valeur « installé par le MSI », pour distinguer « portable » de « installé sans
la case ».

**D5 — hors de ce point, noté pour plus tard.** `System.DirectoryServices.dll`
reste dans `Main` : déplacer des fichiers d'une fonctionnalité à l'autre ajoute
un risque de mise à jour pour un gain de quelques centaines de kilo-octets.

**À vérifier dans la documentation et à l'essai, pas à supposer.**

- `MigrateFeatureStates` reporte l'état des fonctionnalités **de même nom** que
  dans la version installée, et **ne s'exécute pas si la propriété `Preselected`
  est posée** (documentation Microsoft, page « MigrateFeatureStates Action »).
  Ce que Windows Installer fait d'une fonctionnalité **nouvelle** lors d'une
  mise à jour n'y est pas écrit : c'est la condition de niveau (D2) qui doit en
  décider, et c'est à éprouver.
- Si une ligne de commande passe `ADDLOCAL=Main` (forme publiée sur palisser.fr
  pour se passer du raccourci du Bureau), la fonctionnalité « Mode parc »
  **ne sera pas posée**. Avec D1, le poste reste supervisé ; seuls les boutons
  d'administration disparaissent. La documentation devra donner la forme
  `ADDLOCAL=Main,ParkFeature`.

**Essais à faire avant toute publication de la 1.9.0.**

| Cas | Attendu |
|---|---|
| Installation neuve interactive, case laissée vide | ni « Parc » ni « Réseau » |
| Installation neuve interactive, case cochée | « Parc » et « Réseau » présents |
| Installation neuve silencieuse `/qn` | sans mode parc |
| Installation neuve silencieuse `ADDLOCAL=ALL` | avec mode parc |
| Mise à jour 1.8.1 → 1.9.0 interactive, poste de l'auteur | console toujours là |
| Mise à jour 1.8.1 → 1.9.0 par le déploiement de la console, poste client | toujours « surveillance active », boîte noire distante ouverte, `remote.json` inchangé |
| Mise à jour 1.8.1 → 1.9.0 silencieuse `ADDLOCAL=Main`, poste client | toujours supervisé (D1) ; boutons d'administration absents |
| Mise à jour 1.9.0 → version suivante | l'état choisi (avec ou sans) est conservé |
| Désinstallation | `remote.json` laissé en place, comme aujourd'hui |
| Version portable | console visible (D4) |

**Décisions de l'auteur, 17/10/2026** : D1, D2, D3 et D4 **validées** ; la
forme `ADDLOCAL=Main,ParkFeature` à publier partout où `ADDLOCAL=Main` l'est
aujourd'hui **validée**. D5 n'appelait pas de décision : c'est un report
(`System.DirectoryServices.dll` reste dans `Main`), noté pour qu'il ne soit pas
pris pour un oubli.

**Découpage proposé une fois les décisions prises** — lot 1 : la lecture de
l'état (Core, testée) et l'affichage conditionnel des boutons ; lot 2 :
l'installateur (fonctionnalité, valeurs de registre, condition de mise à jour) ;
lot 3 : notes, article, et la forme `ADDLOCAL` corrigée partout où elle est
publiée ; puis les essais du tableau sur de vrais postes.

**Lot 1 — livré le 17/10/2026.**

- **Trouvé en préparant ce lot, vérifié dans `installer/FaultTracePC.wxs`** :
  la fonctionnalité `Main` pose **déjà** une valeur
  `HKLM\Software\Cry-stof\FaultTracePC\Installed = 1` (composant des
  raccourcis). Le plan du lot 0 prévoyait une valeur « installé par le MSI »
  pour distinguer la version portable (D4) : la réutiliser aurait **caché la
  console à tous les postes en 1.8.x** dès l'arrivée de ce code, puisqu'ils ont
  `Installed` sans avoir la case. D'où deux valeurs **nouvelles**, sous la même
  clé : `ParkFeatureManaged` (posée par `Main` d'un installateur qui connaît la
  case — lot 2) et `ParkFeature` (posée par la fonctionnalité « Mode parc » —
  lot 2).
- `Core/ModeParc.cs` : la décision, testable — console visible si la case n'est
  pas gérée (portable, ou installateur antérieur) ou si elle est cochée ;
  « Réseau » visible avec la console ou si le poste est en mode Client (D3) ;
  lecture du registre dans la vue 64 bits, registre illisible traité comme une
  version portable.
- `MainWindow` : `AppliquerModeParc()` au démarrage et après la fenêtre
  « Réseau ».
- **Aucun changement visible tant que le lot 2 n'est pas fait** : aucun
  installateur ne pose encore `ParkFeatureManaged`, donc tout reste affiché.
- Quatorze tests (`ModeParcTests`).

**Lot 2 — livré le 17/10/2026 (installateur, `installer/FaultTracePC.wxs`).**

- Nouvelle fonctionnalité `ParkFeature`, « Mode parc (administration d'un
  parc) », `Level="1000"` : non cochée en installation interactive, non
  installée en silencieux ; installée par la case, `ADDLOCAL=Main,ParkFeature`
  ou `ADDLOCAL=ALL`. Elle pose `ParkFeature = 1`.
- `Main` pose désormais `ParkFeatureManaged = 1` (composant
  `ParkFeatureManagedRegistry`).
- **D2** : `<Level Value="1" Condition="WIX_UPGRADE_DETECTED AND NOT
  PARKFEATUREMANAGED" />`. `PARKFEATUREMANAGED` est lue dans le registre par
  `AppSearch`, **avant** le retrait de l'ancienne version : absente sur une
  1.8.x ou plus ancienne, la case est cochée d'office ; présente (mise à jour
  depuis une 1.9.0 ou plus récente), la condition ne joue pas et c'est le report
  d'état de Windows Installer qui décide. **Pourquoi ne pas s'en tenir à
  `WIX_UPGRADE_DETECTED`** : une mise à jour 1.9.0 → 1.9.x aurait recoché la
  case chez qui l'avait décochée, si le report d'état ne s'appliquait pas.
- Le script de déploiement de la console n'est **pas** modifié : un poste de
  salle installé neuf en 1.9.0 n'a pas besoin des boutons d'administration (D1),
  et un poste mis à jour depuis la 1.8.x les garde (D2).
- Cinq tests (`InstallateurModeParcTests`) lisent le `.wxs` et vérifient qu'il
  pose exactement les valeurs que lit `ModeParc`, que la fonctionnalité est
  absente par défaut et que la condition de mise à jour est en place.
- **Rien de tout cela n'est encore éprouvé** : la construction du MSI et le
  tableau d'essais restent à faire.

**Premier essai, 17/10/2026, bac à sable Windows** (MSI construit à partir du lot
2, numéro encore 1.8.1) : installation neuve interactive — l'arbre des
fonctionnalités montre « Mode parc (administration d'un parc) » **non cochée** ;
après installation, la fenêtre principale n'a **ni « Parc » ni « Réseau »**,
le reste est intact (analyse, surveillance active, boîte noire, outils, « Je ne
sais pas ce que j'ai »). Premier cas du tableau : **conforme**. L'auteur a
trouvé long l'affichage du premier écran de l'assistant d'installation ; cause
non établie — la recherche de registre ajoutée est une lecture unique, peu
probable comme cause. À départager en lançant, dans le bac à sable, le MSI
1.8.1 publié : s'il est aussi lent, ce n'est pas ce lot. **Précision de l'auteur, même jour** : sur une vraie machine, l'installation n'est pas lente — la lenteur tient au bac à sable. Confirmé le même jour : le MSI **1.8.1 publié**, lancé dans le bac à sable, y est tout aussi lent — et le bac à sable s'est ensuite **arrêté de lui-même** (« L'environnement Bac à sable Windows s'est arrêté de manière inattendue », code `0x80370106`). Ce MSI s'installe sans problème sur les postes réels : ni la lenteur ni l'arrêt ne sont imputables à FaultTracePC. Piste non vérifiée : un fil du forum ElevenForum relie des arrêts identiques du bac à sable, après installation de logiciels, à la mise à jour Windows KB5124010 ; un seul témoignage, sans confirmation de Microsoft. **Écartée pour le poste de l'auteur le même jour** : `Get-HotFix -Id KB5124010` ne trouve pas cette mise à jour ; la cause de l'arrêt reste inconnue. **Conséquence pour les essais du point 79** : le bac à sable n'est pas fiable pour une séance longue (installer la 1.8.1 puis la mettre à jour) ; une machine virtuelle Hyper-V avec points de contrôle (retour à l'état d'avant en un clic) convient mieux pour les mises à jour. **Mise en place le même jour par l'auteur** : machine virtuelle Hyper-V sous Windows 10 (environnement d'évaluation proposé par Microsoft), avec points de contrôle.

**Premier essai dans cette machine virtuelle, 17/10/2026 — échec, cause établie le même jour (voir le paragraphe suivant).** Installation de la **1.8.1 publiée** (téléchargée depuis GitHub dans la machine) : message de Windows Installer « Échec du démarrage du service FaultTracePC — Surveillance temps réel (FaultTracePCMonitor) ». La machine est un **Windows 10 Entreprise d'évaluation, build 19041 d'origine (version 2004, décembre 2019), sans mises à jour**. Fait vérifié, sans être encore la cause démontrée : la liste officielle des systèmes pris en charge par .NET 10 (`dotnet/core`, `release-notes/10.0/supported-os.md`) ne cite, pour Windows 10, que les éditions **21H2 (E), 21H2 (IoT), 1809 (E) et 1607 (E)** — pas la 2004, et pas non plus la 22H2 grand public. **Question ouverte pour plus tard** : palisser.fr annonce « Windows 10 et 11 » ; officiellement, .NET 10 ne couvre Windows 10 qu'en éditions longue durée. À décider : le dire sur la page, ou vérifier par l'essai qu'un Windows 10 22H2 à jour fait tourner le logiciel. Les avertissements au téléchargement et au lancement viennent de l'absence de signature numérique (déjà connue, écrite dans les notes de version), pas de la machine.

**Cause établie, 17/10/2026.** Le service lancé à la main dans la machine
virtuelle s'arrête aussitôt sur : « Fatal error. Your Windows doesn't fully
support CET. Please install all available Windows updates. » Le journal
Application montre le même arrêt toutes les six secondes environ (redémarrages
du service), `.NET Runtime`, code `0x80131506`, `KERNELBASE.dll` 10.0.19041.1202.
**CET** (Control-flow Enforcement Technology) est une protection du processeur
contre le détournement de l'exécution ; depuis .NET 9, les exécutables produits
par .NET sont déclarés compatibles CET (documentation Microsoft, « CET supported
by default »), et un Windows 10 2004 sans mises à jour ne la prend pas
entièrement en charge. **Ce n'est pas un défaut de FaultTracePC, et la 1.8.1
n'est pas en cause** : c'est la combinaison .NET 10 + Windows très ancien non mis
à jour. Le message de Windows Installer (« vérifiez que vous disposez de
privilèges suffisants ») est trompeur : il le dit pour tout service qui ne
démarre pas.

**Option notée, NON retenue** : `<CETCompat>false</CETCompat>` dans les projets
ferait tourner le logiciel sur ces Windows-là, au prix d'une protection de
sécurité retirée sur toutes les machines. Un poste à ce point en retard de
mises à jour a un problème plus urgent que FaultTracePC. Préférable : dire
« Windows 10 ou 11 à jour » sur palisser.fr. À trancher par l'auteur.

**Suite décidée par l'auteur** : nouvelle machine virtuelle sur le dernier
Windows 11, pour reprendre les essais du point 79.

**Essais sur la machine virtuelle Windows 11 (dernière version, à jour), 17/10/2026.**
L'auteur confirme que l'installation y est bien plus fluide que dans le bac à
sable. Points de contrôle « Windows propre » et « 1.8.1 ».

| Cas | Résultat |
|---|---|
| 1.8.1 publiée installée | « Parc » et « Réseau » présents — conforme |
| Mise à jour 1.8.1 → MSI du lot 2 (interactive) | rien ne change à l'écran : console et « Réseau » toujours là — **D2 conforme** |
| Installation de maintenance « Modifier », fonctionnalité « Mode parc » retirée | l'arbre la montrait installée ; une fois retirée, « Parc » et « Réseau » disparaissent — **conforme** |
| Nouvelle construction du MSI (nouvel identifiant de produit), installée par-dessus le poste sans mode parc | traitée comme une mise à jour, pas comme une réparation ; « Mode parc » **non coché** dans l'arbre, ni « Parc » ni « Réseau » après installation ; registre : `Installed = 1`, `ParkFeatureManaged = 1`, **pas** de `ParkFeature` — **conforme** (la case décochée survit à la mise à jour) |
| Poste 1.8.1 passé en mode Client (`--configure-remote`), puis mis à jour en silencieux avec `ADDLOCAL=Main` (forme publiée aujourd'hui) | `remote.json` intact (« Client », même jeton) ; port 58620 en écoute après la mise à jour ; registre sans `ParkFeature` — **D1 conforme** : le poste reste supervisé sans la case. Comme prévu au plan, `ADDLOCAL=Main` **ne pose pas** la case même en venant d'une 1.8.x : la condition de D2 ne joue que sans liste explicite. **D3 conforme** (capture du 17/10/2026) : « Réseau » visible, « Parc » absent |

Note sur cet essai : juste après `--configure-remote`, le port 58620 n'était
**pas encore** en écoute. Ce n'est pas un défaut : la commande l'annonce
elle-même (« le service prend cette configuration en compte dans les
30 secondes »), et la vérification a été faite aussitôt.

| Désinstallation silencieuse (`/x … /qn`) du poste en mode Client | `remote.json` **conservé** ; service `FaultTracePCMonitor` supprimé ; clé `HKLM\Software\Cry-stof\FaultTracePC` **supprimée** — conforme |
| Installation neuve silencieuse (`/qn`) | registre : `Installed = 1`, `ParkFeatureManaged = 1`, pas de `ParkFeature` — conforme (la commande elle-même n'a pas été recopiée, mais la clé, absente juste avant, est réapparue sans la case) |
| Ajout silencieux par `ADDLOCAL=ALL` | `ParkFeature = 1` — conforme |

| Version portable construite à partir du code actuel (`build\publish.ps1 -Zip`), dézippée et lancée sur un Windows propre, sans MSI | « Parc » et « Réseau » présents — **D4 conforme**. (La version portable du 10/10/2026, antérieure au lot 1, n'aurait rien prouvé : elle affiche tout dans tous les cas.) |

**Bilan du tableau d'essais, 17/10/2026** : **tous les cas sont conformes** sur
Windows 11, D1 à D4 compris.

**Lot 3 — documentation, livré le 17/10/2026.**

- `docs/DEPLOIEMENT.md` : deux lignes au tableau des variantes
  (`ADDLOCAL=Main,ParkFeature`, `ADDLOCAL=ALL`) et un paragraphe « Le mode parc,
  à partir de la 1.9.0 » — ce que la case gouverne, les règles de mise à jour,
  le piège d'`ADDLOCAL=Main`, le comportement du déploiement par la console.
- `installer/FaultTracePC.wxs` : les exemples `ADDLOCAL` du commentaire de
  l'interface d'installation complétés.
- `installer/build-msi.ps1` : un bloc « MODE PARC » dans l'en-tête.
- `docs/releases/RELEASE_NOTES_v1.9.0.md` : **brouillon** commencé, avec le
  point 79, le correctif du mode de Defender et la mention « Windows à jour ».
  Un commentaire en tête rappelle qu'il reste à compléter avec les autres
  points de la 1.9.0 avant le tag.
- **Pas encore fait, volontairement** : les articles 30 et 31 de palisser.fr
  (au moment de la publication) ; la décision sur `CETCompat` reste à l'auteur
  — le brouillon des notes suit la recommandation « Windows à jour », à
  retirer si l'auteur en décide autrement. Cas limite noté : une version portable lancée sur une machine où
le MSI est installé **sans** la case lira le registre de cette machine et cachera
la console — c'est le choix fait sur cette machine, il paraît cohérent de le
respecter.

**Défaut constaté au même essai, corrigé directement** : la carte « Aucune
protection antivirus en temps réel » du rapport **français** affichait
« (mode Not running) » — la valeur brute `AMRunningMode` de Windows, en
anglais. `Analysis/ModeDefender.cs` traduit les valeurs connues (Normal,
Passive, SxS Passive Mode, EDR Block Mode, Not running) et rend une valeur
inconnue telle quelle ; employé par la conclusion (`RulesEngine`) et par la
carte « Protection antivirus » (`HtmlReportGenerator`). Neuf tests
(`ModeDefenderTests`). La conclusion elle-même est juste : dans le bac à
sable, Defender ne tourne pas.

**Bac à sable Windows, proposé par l'auteur le 17/10/2026 pour les essais** :
Windows Sandbox donne un Windows propre à chaque ouverture — idéal pour les
installations neuves du tableau, et pour une mise à jour 1.8.1 → 1.9.0 faite
dans la même séance (installer la 1.8.1, la passer en mode Client, puis
installer la 1.9.0). Il ne remplace pas l'essai sur un vrai poste du parc,
déployé par la console.

### Les deux options écartées, et pourquoi

Elles sont écrites ici parce qu'une piste abandonnée sans motif se represente.

**Option A — deux paquets distincts, « perso » et « parc ».** Écartée. Le coût
n'est pas dans le code, il est autour : deux constructions, deux jeux de notes de
version, quatre articles qui en deviennent huit, deux chemins de mise à jour à
éprouver, et une question de plus à laquelle répondre — « lequel je prends ? ».
Ce coût se paie à chaque version, pour toujours, et le projet tient sur une seule
personne.

S'y ajoute un piège d'installateur : deux MSI installant dans le même dossier
avec des `ProductCode` différents se marchent dessus. Qui installerait les deux
obtiendrait deux entrées dans « Programmes et fonctionnalités », ou un dossier à
moitié écrasé. Cela se gère — `UpgradeCode`, détection mutuelle — mais
l'installateur est justement la partie la moins éprouvée et celle qui casse le
plus silencieusement.

Enfin, deux binaires différents porteraient le même numéro. C'est exactement ce
qui a été refusé la veille en passant de 1.7.0 à 1.7.1.

**Option B — faire repartir le paquet « perso » de la 1.6.2.** Écartée, et plus
fermement. Cela veut dire maintenir deux lignées : toute correction du moteur de
diagnostic — là où vivent la grande majorité des corrections — devrait être
portée deux fois, éprouvée deux fois, décrite deux fois. C'est l'option la plus
coûteuse de toutes celles examinées.

### La découpe qui, elle, resterait défendable — plus tard

Si un découpage en paquets devait avoir lieu un jour, ce n'est pas
**perso / parc** mais **console / agent**. Il y a trois rôles et non deux :

| Rôle | Ce dont il a besoin |
|---|---|
| poste personnel | l'application, le service de surveillance, les réparations |
| poste supervisé | le service de surveillance et `FaultTracePC.Cli.exe` — **pas la console** |
| poste d'administration | tout, plus la console de parc |

Un poste de salle de classe reçoit aujourd'hui 63 Mo dont il n'emploiera jamais
l'onglet Inventaire. Ce découpage-là a un bénéfice **chiffrable** — taille copiée,
durée de copie, surface installée sur les machines les plus exposées — là où
perso / parc n'aurait été qu'un habillage.

**À ne pas trancher avant d'avoir mesuré.** La question se posera avec des
chiffres quand le parc aura grossi ; à neuf postes elle n'a pas d'objet.

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
| 48 ✔ | une erreur WHEA de port PCIe est attribuée au processeur, et « corrigée » est classé critique | POSTE-SALLE-02, 06/09 |
| 49 ✔ | le triplet bus/appareil/fonction est tronqué à l'écriture comme à l'affichage | POSTE-SALLE-02, 06/09 |
| 50 ✔ | le `BugcheckCode` de l'événement 41 n'est pas lu — quinze plantages invisibles | POSTE-ELEVE-01, 14/09 |
| 51 ✔ | l'échec d'écriture du vidage est compté comme une erreur disque | POSTE-ELEVE-01, 14/09 |
| 52 ✔ | les arrêts inattendus ne pèsent pas sur le verdict | POSTE-ELEVE-01, 14/09 |
| 53 ✔ | aucune section réseau | POSTE-ELEVE-01, 14/09 |
| 54 ✔ | un composant de Windows déclaré « désinstallé, sans objet » | POSTE-TEMOIN, 14/09 |
| 55 ✔ | la version du pilote fautif n'est pas comparée entre deux analyses | POSTE-TEMOIN, 14/09 |
| 56 ✔ | 178 vidages « en direct » listés, jamais exploités — dont 155 gels du moteur graphique | POSTE-TEMOIN, 17/09 |
| 57 ✔ | « 0 réinitialisation » écrit en présence de 155 réinitialisations | POSTE-TEMOIN, 17/09 |
| 58 ✔ | un gel suivi d'un écran bleu est une récupération ratée : rapprochement jamais fait | POSTE-TEMOIN, 17/09 |
| 59 ✔ | les plantages de `dwm.exe` écartés au lieu d'être rattachés à la panne graphique | POSTE-TEMOIN, 17/09 |
| 60 ✔ | une clé USB et un port de contrôleur comptés ensemble sous « erreurs disque » | POSTE-TEMOIN, 17/09 |
| 61 ✔ | deux dates différentes pour le même pilote, dans le même rapport | POSTE-TEMOIN, 17/09 |
| 62 ✔ | la boîte noire répète le même incident et laisse le gel passer pour une fin de tableau | POSTE-TEMOIN, 17/09 |
| 63 ✔ | le script de réparation raisonne par familles de panne, pas par mesures | POSTE-TEMOIN, 17/09 |

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
