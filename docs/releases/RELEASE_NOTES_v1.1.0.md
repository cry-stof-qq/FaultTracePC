FaultTracePC trouve la cause d'une panne ou d'un écran bleu sous Windows 10 et 11, l'explique en français clair, et propose la réparation adaptée. Aucun prérequis à installer, aucune donnée envoyée nulle part.

## Nouveautés de la 1.1

**Santé réelle des disques.** Les compteurs sont lus directement auprès du matériel, par le chemin adapté à chaque technologie. En SATA/ATA, les attributs SMART bruts : secteurs réalloués, secteurs en attente, secteurs illisibles, erreurs CRC. En NVMe, le journal de santé du contrôleur via `DeviceIoControl` — réserve de blocs de remplacement comparée au seuil du constructeur, erreurs d'intégrité des données, alertes levées par le disque lui-même. Windows n'expose pas ces compteurs NVMe par WMI : sans ce chemin, un SSD moderne ne peut tout simplement pas être diagnostiqué.

Le rapport distingue un disque qui meurt d'un simple câble SATA défaillant. Et quand aucun compteur n'est lisible, **il le dit** au lieu d'afficher un tableau vide qui ferait passer une absence de mesure pour un bilan de santé.

**État de la batterie.** Sur un portable : pourcentage d'usure, capacité restante face à la capacité d'origine, nombre de cycles, et un verdict lisible — *bon état*, *usée*, *très usée*, *hors d'usage*.

**Le problème est-il encore là ?** Quand un logiciel est mis en cause, le rapport vérifie s'il est toujours installé, s'il a été désinstallé, ou s'il a été réinstallé ou mis à jour après le dernier crash. Fini les problèmes déjà réglés qui restent affichés indéfiniment.

**Boîte à outils élargie.** Point de restauration avant toute intervention, libération d'espace disque, programmes lancés au démarrage, analyses Microsoft Defender, réinitialisation de la pile réseau, rapport de batterie détaillé.

**Fenêtre Windows Update dédiée.** Elle affiche ce que la page Paramètres masque — mises à jour **optionnelles** et **pilotes** — avec sélection ligne par ligne, taille, numéro KB et besoin de redémarrage. L'ordinateur n'est **jamais** redémarré automatiquement. Chaque opération est consignée dans `Documents\FaultTracePC\MajWindows_AAAA-MM-JJ.txt`.

**Vérification de version.** Un bouton compare la version installée à la dernière publiée ici. Il ne télécharge rien et n'installe rien tout seul : il informe, vous décidez. La vérification au démarrage est désactivée par défaut — sans l'activer, le logiciel ne contacte jamais Internet de lui-même.

**78 tests automatisés** couvrent l'analyse des dumps, les règles de diagnostic, la génération du rapport et la sécurité du mode réseau.

## Que télécharger

| Fichier | Pour qui |
|---|---|
| `FaultTracePC-1.1.0.msi` | Installation classique, ou déploiement par GPO sur un parc |
| `FaultTracePC-1.1.0-portable.zip` | Aucune installation : décompresser et lancer `FaultTracePC.exe`, y compris depuis une clé USB |

Les deux embarquent le runtime .NET : **rien à installer au préalable**, même sur une machine qui vient d'être réinstallée. Le logiciel demande les droits administrateur, indispensables pour lire les fichiers dump et les journaux système complets.

## À savoir avant d'installer

**Ces fichiers ne sont pas signés numériquement.** Au premier lancement, Windows SmartScreen affichera « Éditeur inconnu » : c'est normal pour un logiciel gratuit sans certificat de signature de code, qui coûte plusieurs centaines d'euros par an. Cliquez sur « Informations complémentaires » puis « Exécuter quand même ».

Certains antivirus peuvent aussi réagir, parce que FaultTracePC fait précisément ce que fait un outil de diagnostic : lire les dumps mémoire, interroger le matériel à bas niveau, et — si vous activez le mode parc — écouter sur un port réseau. **Ne désactivez pas votre antivirus pour autant** : si vous avez le moindre doute, le code source est intégralement lisible dans ce dépôt, et vous pouvez recompiler vous-même avec `dotnet build`.

## Limites, dites honnêtement

L'analyse symbolique des dumps qui **nomme le pilote fautif** demande WinDbg (`winget install Microsoft.WinDbg`). Sans lui, FaultTracePC lit quand même le code STOP et ses paramètres, mais reste plus vague sur le coupable.

Les températures processeur ne sont pas exposées par toutes les machines. Les compteurs SMART détaillés dépendent du contrôleur : beaucoup de contrôleurs RAID n'en transmettent aucun.

Et surtout : une coupure brutale sans écran bleu, ou des erreurs matérielles WHEA répétées, **ne se réparent pas par logiciel**. Le rapport le dit plutôt que de proposer un remède imaginaire.

## Licence

MIT — utilisation libre, y compris en entreprise et en établissement scolaire, sans aucune garantie.
