## In English

**A report must name the fault it has in front of it.** Sixteen defects, found by using the software on four real machines in a school over two weeks — a workstation with a dying graphics card, a student laptop that had crashed fifteen times without the software noticing, a fleet of machines whose keyboards died until reboot, and the secretary's PC that started this whole project.

They all say the same thing: *the software was collecting far more than it was concluding.* The stop code was in the Windows log. The PCIe address was in the event. The 155 recorded graphics-engine hangs were printed in the report, under a made-up name, and never counted. None of it reached the verdict.

The rule that changed everything: **a reassuring verdict is irreversible — the reader closes the report.** So the software now counts abnormal shutdowns before it says anything reassuring, names the component rather than the chip that reports it, and writes its repair script from what it measured instead of from a family of faults.

The notes below are in French. The full English description of the software is here: **[FaultTracePC — diagnose, monitor and repair a Windows PC](https://palisser.fr/spip.php?article31)**

| File | For whom |
|---|---|
| `FaultTracePC-1.6.0.msi` | Classic installation, or Group Policy deployment |
| `FaultTracePC-1.6.0-portable.zip` | No installation: unzip and run |

The `Source code` archives are generated automatically by GitHub: they contain the code, not the compiled software.

Set the language at install time: `msiexec /i FaultTracePC-1.6.0.msi FTPCLANG=en /qn`

**These files are not digitally signed.** Windows will show "Unknown publisher" — click *More info* then *Run anyway*. Checking the SHA-256 fingerprint at the bottom of this page is the only way to be sure the file you downloaded is the one published here.

---

Seize défauts, tous constatés en se servant du logiciel sur des machines réelles, pas en relisant du code. Un thème unique : **un rapport doit nommer la panne qu'il a sous les yeux.**

## Le verdict ne peut plus rassurer à tort

C'était le pire mode de défaillance possible pour cet outil. Se tromper est réparable ; rassurer à tort ne l'est pas, parce que le lecteur referme le rapport.

Sur un poste élève qui plantait toutes les semaines depuis quatre mois, le rapport annonçait « Aucun BSOD détecté sur la période » et « Pas de panne critique ». Les quinze plantages étaient pourtant inscrits dans le journal de Windows : chaque événement `Kernel-Power 41` porte un champ `BugcheckCode`, renseigné à chaque fois. Le logiciel concluait à l'absence de plantage parce qu'il ne trouvait **aucun fichier de vidage** — or aucun n'avait pu être écrit. Il confondait « je n'ai pas de trace » et « il ne s'est rien passé ».

Ces plantages existent maintenant, avec leur nom. Et un `BugcheckCode` à **zéro** garde son sens propre : la machine a perdu son alimentation, ou quelqu'un a maintenu le bouton. Dans un établissement, c'est la distinction qui compte — elle sépare « un élève a forcé l'extinction » de « Windows s'est planté », et les deux ne se réparent pas pareil.

Les arrêts inattendus, eux, étaient collectés, affichés dans le tableau des événements, et n'alimentaient aucune conclusion. Ils en ont une. Et avant de prononcer quoi que ce soit de rassurant, le verdict compte désormais les arrêts anormaux : « Système sain » et « Pas de panne critique » ne peuvent plus être écrits sur une machine qui s'est arrêtée anormalement. À la place : *« La machine s'est arrêtée anormalement N fois, sans qu'une cause ait pu être établie. Ce n'est pas un système sain : il manque des traces, pas des pannes. »*

## Ce que Windows enregistre sans écran bleu est enfin lu

Sur le poste à l'origine du projet, le rapport affichait **178 fichiers** de `C:\Windows\LiveKernelReports` — nom, date, taille, code. Il ne les comptait pas, n'en tirait rien, et nommait le code `BUGCODE_0x141`, un repli fabriqué qui ressemblait à s'y méprendre à un identifiant Microsoft.

`0x141`, c'est `VIDEO_ENGINE_TIMEOUT_DETECTED` : un moteur de la carte graphique n'a pas répondu, Windows l'a réinitialisé, aucun écran bleu n'est apparu. **C'est le journal de bord d'une carte qui se dégrade**, et il ne passe jamais par le journal d'événements. Il y en avait 155, le plus ancien datant de juin 2022.

Pendant ce temps, la carte de verdict annonçait « Instabilité du pilote graphique (**0 réinitialisation**, 7 BSOD) ». Le logiciel imprimait la preuve du contraire quatre cents lignes plus bas dans le même document.

Ces gels sont comptés, et surtout **appariés aux écrans bleus survenus dans les dix minutes**. Un gel suivi d'un écran bleu est une réinitialisation qui a **échoué** — et la proportion d'échecs, avec la date à laquelle elle bascule, est la seule mesure qui distingue un pilote instable d'un matériel qui lâche. Sur cette machine : environ 145 gels récupérés sans incident de 2022 à juin 2026, puis dix sur dix soldés par un écran bleu à partir du 1er juillet.

Le repli pour un code inconnu affiche maintenant le code brut. Mieux vaut avouer qu'on ne sait pas que fabriquer un nom d'allure officielle.

## Le composant, et non celui qui le rapporte

Vingt-sept erreurs matérielles identiques sur un poste de salle informatique donnaient : « **Le processeur a signalé 27 erreurs matérielles** », classées critiques, suivies d'une recommandation qui faisait retirer l'XMP et suspecter l'alimentation.

Les vingt-sept disaient autre chose. Identifiant **17** : erreur **corrigée** — le matériel a récupéré, rien n'a été perdu. Composant : **port racine PCI Express**. Sur Intel, ces ports sont dans le paquet du processeur, donc « le processeur a signalé » n'est pas faux au sens strict ; pour qui lit, c'est trompeur. Le fautif est un lien et son périphérique, pas un cœur.

Deux corrections. La gravité : une erreur corrigée ne peut plus être critique, quel qu'en soit le nombre. La source : le titre et la conclusion nomment le composant, et la recommandation vise le lien — réenfoncer la carte, désactiver la gestion d'alimentation du lien, mettre à jour le pilote du périphérique, forcer une génération PCIe inférieure — en disant explicitement que ni l'overclocking ni la mémoire ni l'alimentation ne sont en cause.

Le triplet bus:appareil:fonction, seule donnée qui nomme le lien fautif, était coupé par la troncature du message : il se trouve en fin de texte, juste après la limite. Il est désormais extrait **avant** la coupe. Il avait fallu retourner au journal de la machine pour l'obtenir — exactement le travail que ce logiciel existe pour éviter.

## Le pilote est disculpé quand il doit l'être

Trois rapports de la même machine : `nvlddmkm.sys` en `32.0.15.8216` dans le premier, en `32.0.15.8278` dans le second, cinq heures plus tard. Le pilote avait été remplacé entre les deux. Quatre écrans bleus de **même signature** ont suivi.

Le logiciel disait « le problème PERSISTE, la réparation n'a pas suffi ». Il ne disait pas la seule chose qui clôt le dossier : **le pilote accusé a été remplacé, les plantages ont continué, ce n'est donc pas le pilote.** Sans ce rapprochement, la recommandation restait « réinstaller proprement le pilote » — c'est-à-dire refaire ce qui venait d'échouer.

Les deux données existaient déjà, à quatre cents lignes l'une de l'autre. Elles se rencontrent.

## Chaque chose est appelée par son nom

**Un composant de Windows n'est pas un logiciel désinstallé.** `dwm.exe` était déclaré « ne figure plus parmi les programmes installés — problème probablement sans objet ». Il n'a jamais été désinstallé : c'est le gestionnaire de fenêtres. Et ses onze plantages via `dwmcore.dll` étaient la meilleure corroboration de la conclusion principale du même rapport — le compositeur meurt quand l'affichage meurt. Une liste blanche de vingt-cinq binaires système empêche ce verdict, et un plantage reconnu graphique par son exécutable ou son module fautif bascule de la catégorie Logiciel vers Pilote graphique, en citant la conclusion qu'il corrobore.

**L'échec d'écriture d'un vidage n'est pas une erreur disque.** Les messages `volmgr` 45, 46, 49, 161 et 162 étaient comptés parmi les erreurs disque et menaient à un contrôle de disque. Ils disent que Windows n'a pas pu écrire le fichier qui aurait permis de diagnostiquer les plantages. Ils ont leur conclusion propre, qui nomme le fichier d'échange déclaré et compte les écrans bleus dépourvus d'analyse — c'est ce chiffre qui la rend urgente.

**Une clé USB n'est pas un disque système.** Douze erreurs réparties entre une clé USB, un volume et un port de contrôleur étaient réunies sous un chiffre unique, suivi d'une recommandation qui commençait par la gestion d'alimentation du lien PCI Express et les câbles SATA. Les supports amovibles sont séparés des disques fixes ; quand ils sont seuls en cause, la gravité retombe et les gestes machine disparaissent.

**Un pilote a deux dates, et ce ne sont pas les mêmes.** `32.0.15.8278 du 08/07/2026` dans une carte, `du 25/06/2026` dans une autre : l'une est la date du fichier `.sys`, l'autre celle du paquet. Le rapport dit maintenant laquelle il montre.

**Un incident, un tableau.** Un même plantage arrivait trois fois dans la boîte noire — en-tête du vidage, événement Kernel-Power, date du fichier — et produisait trois tableaux identiques sous des titres à une minute d'écart. Ils sont regroupés. Et le silence qui précède l'incident est nommé : un tableau qui s'arrête quatre-vingt-quatorze secondes avant l'heure annoncée n'est pas une fin de tableau, c'est l'échantillonneur qui a cessé de répondre avec la machine.

## Le réseau existe

C'était le seul domaine dont le logiciel ne disait **rien**, alors que « plus de réseau » est l'une des pannes les plus fréquentes d'un parc. Sur un poste élève dont le Wi-Fi avait disparu, le rapport a parlé de la batterie et des erreurs disque.

Une section réseau porte désormais les cartes avec leur état et leur pilote, le nombre de réseaux Wi-Fi enregistrés, l'état des services `WlanSvc`, `Dhcp`, `Dnscache` et `NlaSvc`, et l'appartenance au domaine. Aucun outil externe, aucun processus lancé.

Et une conclusion en sort. Carte présente, zéro réseau enregistré : *« Ce n'est pas une panne de la carte : Windows n'a simplement rien à proposer. Le poste appartient au domaine : ses profils Wi-Fi viennent des stratégies de groupe. Sans réseau, il ne les reçoit pas ; sans elles, il n'a pas de Wi-Fi. C'est une boucle, et seul un câble la casse. »* La recommandation est le geste : un câble, puis `gpupdate /force`.

Deux précautions valent d'être dites. Aucune adresse MAC complète ne sort du rapport — seuls les trois octets du constructeur sont conservés. Et quand le dossier des profils n'est pas lisible sans élévation, le logiciel ne conclut **rien** : annoncer une panne parce qu'on n'a pas pu regarder serait pire que se taire.

## Le script de réparation part des mesures

Il raisonnait par familles de panne : les mêmes gestes pour toutes les machines d'une même catégorie. Sur le poste dont la carte graphique agonisait, il présentait dix pilotes de 2021 comme « les premiers suspects » alors que l'analyse WinDbg nommait `nvlddmkm.sys` dans cinq vidages sur cinq, et demandait de surveiller une température que la boîte noire avait déjà mesurée pendant 94 heures.

Il commence maintenant par rappeler les conclusions de la machine avec leur recommandation. La température relevée est imprimée au lieu d'être redemandée. Le module désigné par l'analyse symbolique passe devant l'inventaire d'ancienneté, qui cesse de s'intituler « les premiers suspects ». Et `wsl --update` ne se lance plus que si la virtualisation tourne réellement — pas sur le poste d'une secrétaire.

## Mise à jour

Le MSI remplace proprement la 1.5.2. Aucun changement de format de fichier ni de protocole de parc. Un poste resté en 1.5.2 continue de fonctionner avec une console 1.6.0 : la section réseau sera simplement absente de ses rapports.

468 tests, aucun échec.
