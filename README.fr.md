<div align="center">

<img src="assets/FaultTracePC.png" alt="FaultTracePC" width="120">

# FaultTracePC

**Trouver la cause d'une panne Windows 10/11 — et savoir quoi faire ensuite.**

Analyse des écrans bleus, boîte noire temps réel, alertes avant la panne,
rapport lisible et aide à la réparation. Gratuit, en français, sans télémétrie.

[🇬🇧 English](README.md) · 🇫🇷 Français

[![Compilation et tests](https://github.com/cry-stof-qq/FaultTracePC/actions/workflows/ci.yml/badge.svg)](https://github.com/cry-stof-qq/FaultTracePC/actions/workflows/ci.yml)
[![Installeur](https://github.com/cry-stof-qq/FaultTracePC/actions/workflows/installeur.yml/badge.svg)](https://github.com/cry-stof-qq/FaultTracePC/actions/workflows/installeur.yml)

Chaque commit est compilé et testé sur une machine Windows vierge, et les paquets
publiés proviennent de cette même construction automatisée — pas d'un poste de
travail.

</div>

---

## Pourquoi ce logiciel

Quand un PC plante, Windows enregistre tout ce qu'il faut pour comprendre — et
le rend illisible. Les informations sont éclatées entre l'Observateur
d'événements, le Moniteur de fiabilité, les fichiers `.dmp`, les compteurs SMART
des disques et les capteurs matériels. Les outils existants savent lire *une* de
ces sources : l'un décode les dumps, l'autre affiche les températures, un
troisième liste les crashs. Aucun ne les croise, et aucun ne dit clairement quoi
faire.

FaultTracePC fait exactement cela : il rassemble toutes ces sources, les
recoupe, et produit **un verdict en français avec un niveau de confiance
honnête** — puis propose les réparations correspondantes.

## Ce qu'il fait

**Analyse après une panne.** Il lit les fichiers dump (`Minidump`, `MEMORY.DMP`,
`LiveKernelReports`), en extrait nativement le code STOP et ses paramètres, et —
si WinDbg est installé — lance l'analyse symbolique pour **nommer le pilote
fautif** avec sa pile d'appels. Il croise ensuite avec le journal d'événements
(BugCheck, Kernel-Power, WHEA, erreurs disque, réinitialisations du pilote
graphique, crashs d'applications, saturation mémoire), le Moniteur de fiabilité,
l'inventaire des pilotes, la santé SMART des disques et les processus en cours.

**Surveillance temps réel — la boîte noire.** Un service Windows léger
(< 1 % de CPU) enregistre en continu températures, mémoire et événements
critiques. Chaque ligne est écrite *physiquement* sur le disque : les dernières
secondes avant un crash survivent au crash. C'est ce qui permet de dire
« le processeur était à 97 °C juste avant l'arrêt » — ce qu'aucune analyse
après coup ne peut reconstituer.

**Alertes avant la panne.** Le service surveille des seuils (températures,
mémoire virtuelle, erreurs WHEA, santé des disques) et prévient par une
notification Windows *avant* que la machine tombe.

**État réel du matériel.** Les compteurs de santé des disques sont lus
directement auprès du matériel, par le chemin adapté à chaque technologie :

- **SATA/ATA** — attributs SMART bruts via WMI : secteurs réalloués, secteurs en
  attente, secteurs illisibles, erreurs CRC, heures, usure SSD. Le rapport dit en
  clair si des **clusters sont défectueux**, et distingue un disque qui meurt
  d'un simple **câble SATA défaillant**.
- **NVMe** — journal de santé (log page 0x02) lu par `DeviceIoControl`, comme le
  font les outils spécialisés. Windows n'expose pas ces compteurs par WMI : sans
  ce chemin, un SSD NVMe ne peut tout simplement pas être diagnostiqué. On y
  obtient la **réserve de blocs de remplacement** comparée au seuil du
  constructeur (le vrai signal de fin de vie d'un NVMe), les **erreurs
  d'intégrité des données**, et les alertes que le contrôleur lève lui-même.

Quand aucun compteur n'est lisible, le rapport **le dit** au lieu d'afficher un
tableau vide qui ferait passer une absence de mesure pour un bilan de santé.

Sur un portable, l'**état de la batterie** est donné en pourcentage d'usure, avec
un verdict lisible (*usée*, *très usée*, *hors d'usage*).

**Aide à la réparation.** Chaque diagnostic génère un script PowerShell adapté
aux problèmes trouvés — qui commence par créer un **point de restauration** et
ne lance rien sans confirmation. Une boîte à outils intégrée réunit les
réparations courantes : point de restauration, désinstaller une mise à jour
Windows fautive, réinitialiser les composants Windows Update, `sfc`, `DISM`,
`chkdsk`, diagnostic mémoire, SMART, nettoyage de l'espace disque, analyse
Microsoft Defender, réinitialisation réseau. Une fenêtre dédiée pilote
**Windows Update** et affiche ce que la page Paramètres masque — mises à jour
**optionnelles** et **pilotes** — avec sélection ligne par ligne et **jamais de
redémarrage automatique**.

**Le problème est-il encore là ?** Quand un logiciel est mis en cause, le
rapport vérifie s'il est toujours installé, s'il a été désinstallé, ou s'il a
été **réinstallé ou mis à jour après le dernier crash** — et le dit, au lieu
d'afficher éternellement un problème déjà réglé.

**Chaque pilote a un nom, un propriétaire et une action.** Une base de 59 pilotes
documentés relie un fichier `.sys` au logiciel ou au matériel qui l'installe, et
au correctif éprouvé — « nvlddmkm.sys » devient « pilote NVIDIA, réinstallation
propre avec DDU en mode sans échec ». Les pilotes absents de la base sont
rattachés à leur **famille de plateforme** (AMD, Intel, Realtek, VirtualBox,
Fortinet, OEM…) quand le nom **et** l'éditeur concordent. Le rapport distingue
les deux niveaux : une correspondance nominative donne le correctif précis, une
identification par famille un conseil générique mais juste.

**« Je ne sais pas ce que j'ai ».** Un bouton unique, pensé pour qui ne sait pas
ce qu'est un pilote : point de restauration, examen, réparations **sans risque**
appliquées seules, puis nouvelle analyse et une conclusion en une phrase. Tout ce
qui redémarre, installe ou désinstalle est **proposé à la fin, une action à la
fois, avec sa raison**. Sans point de restauration possible, l'assistant propose
d'activer la protection du système — et à défaut continue en **mode réduit**, en
s'interdisant alors de toucher aux fichiers système.

**Températures dans la durée.** Ce n'est pas la température d'un instant qui
annonce un plantage, mais le temps cumulé passé trop haut : *« 40 minutes
au-dessus de 90 °C cette semaine »*, avec les épisodes continus les plus longs.
Les périodes machine éteinte ne sont jamais comptées.

**Export PDF, à la demande.** Un bouton crée un PDF du rapport **complet**,
détails techniques inclus, pour le joindre à un ticket. Aucun PDF n'est généré
automatiquement.

**Suivi et parc.** Chaque scan est archivé : le suivant répond à la vraie
question — « est-ce que la réparation a marché ? ». En mode parc, une console
affiche l'état de plusieurs machines et permet de lancer un diagnostic à
distance sans se déplacer.

**Comparateur de parc.** Ce qu'aucun diagnostic individuel ne peut voir : un
pilote ancien identique sur six postes n'est plus un suspect, c'est une image de
déploiement à corriger — une fois, pour tout le parc. Le comparateur relève ce
qui est **commun** (pilote, code d'arrêt, modèle de disque qui se dégrade), ce
qui **diverge** (même pilote en plusieurs versions : les retardataires sont
nommés) et ce qui est **isolé** (un poste qui accumule seul, et relève d'un
traitement individuel).

**Suis-je à jour ?** Le bouton `🔄 Vérifier les mises à jour` compare la version
réellement embarquée dans l'exécutable à la dernière publiée sur
[la page des versions](https://github.com/cry-stof-qq/FaultTracePC/releases/latest).
S'il y a du neuf, il affiche les nouveautés et propose d'ouvrir la page de
téléchargement — **il ne télécharge rien et n'installe rien tout seul** : sur un
parc déployé par GPO, un exécutable qui se met à jour sans qu'on le lui demande
est un risque, pas un service. La vérification au démarrage est **désactivée par
défaut** : sans cocher la case `au démarrage`, FaultTracePC ne contacte jamais
Internet de lui-même. Sur un poste sans accès Internet, l'échec est annoncé
clairement et ne bloque rien.

## Installation

| Format | Pour qui | Comment |
|---|---|---|
| **MSI** | Installation durable, déploiement par GPO | `msiexec /i FaultTracePC-1.2.3.msi` (ou double-clic) |
| **Portable (.zip)** | Dépannage sur clé USB, rien à installer | Décompresser, lancer `FaultTracePC.exe` |

Les deux sont disponibles dans les [Releases](../../releases). Aucun prérequis :
le runtime .NET est inclus. **Windows 10 ou 11, 64 bits, droits administrateur**
(nécessaires pour lire les dumps et les journaux système).

Optionnel mais recommandé — l'analyse symbolique des dumps, qui nomme le pilote
fautif, nécessite WinDbg :

```powershell
winget install Microsoft.WinDbg
```

## Utilisation

1. Lancer FaultTracePC (il demande l'élévation administrateur).
2. **🔍 Analyser cette machine** — le rapport HTML s'ouvre dans le navigateur.
   Il démarre en mode simple ; un bouton révèle le détail technique.
3. **📡 Surveillance temps réel** — installe la boîte noire en un clic. Elle
   continue même application fermée, et redémarre avec le PC.
4. **🧰 Outils** — les réparations à un clic, dans une fenêtre PowerShell visible.

En ligne de commande, pour un parc ou une tâche planifiée :

```powershell
FaultTracePC.Cli.exe --quiet --json --days 90 --output \\serveur\Diagnostics$
# codes de sortie : 0 sain · 1 avertissements · 2 critique · 3 erreur
```

## Limites — dites honnêtement

- **Sans WinDbg**, le code STOP est lu mais le pilote fautif reste souvent non
  identifié : le diagnostic est alors moins précis (confiance abaissée en
  conséquence dans le rapport, jamais masquée).
- **Les températures CPU dépendent de la machine.** Le pilote historique
  utilisé par les bibliothèques de capteurs est bloqué par Windows 11 récent ;
  installer [PawnIO](https://pawnio.eu) rétablit la lecture. Les températures
  GPU fonctionnent sans lui.
- **Un diagnostic n'est pas une certitude.** Chaque conclusion affiche son
  niveau de confiance — « faible » signale une piste à vérifier, pas une preuve.
- **Rien n'est envoyé nulle part.** Aucune télémétrie, aucun compte. Les
  rapports restent dans `Documents\FaultTracePC`. Le mode réseau, facultatif,
  n'accepte que les adresses privées et des requêtes signées.

## Mode réseau (facultatif)

Une machine peut publier son état en **lecture seule** pour une console
d'administration. Double verrou : seules les adresses privées (RFC 1918) sont
acceptées, **et** chaque requête doit porter une signature HMAC-SHA256 — le
secret ne circule jamais sur le réseau, et le rejeu d'une requête capturée est
refusé. Une règle de pare-feu restreinte aux mêmes plages est posée en plus.
Rien n'est accessible depuis Internet.

## Compiler depuis les sources

Prérequis : [SDK .NET 10](https://dotnet.microsoft.com/download/dotnet/10.0).

```powershell
git clone https://github.com/cry-stof-qq/FaultTracePC.git
cd FaultTracePC
dotnet build
dotnet test                                    # 120 tests
dotnet run --project src\FaultTracePC.App
```

Produire les distribuables :

```powershell
powershell -ExecutionPolicy Bypass -File build\publish.ps1 -Zip
powershell -ExecutionPolicy Bypass -File installer\build-msi.ps1 -Version 1.2.3
```

## Architecture

```
src/
  FaultTracePC.Core      Collecte (WMI, événements, dumps, capteurs), moteur de
                         règles, catalogue des codes STOP, base de signatures de
                         pilotes, génération des rapports HTML
  FaultTracePC.App       Interface WPF : scan, visualiseur, console de parc,
                         boîte à outils, configuration réseau
  FaultTracePC.Monitor   Service Windows : boîte noire, alertes préventives,
                         API de télémétrie signée
  FaultTracePC.Cli       Diagnostic en ligne de commande (parc, GPO)
tests/                   Tests xUnit : sécurité, parsing des dumps, règles
```

## Contribuer

Issues et pull requests bienvenues, en français ou en anglais.

## Licence

[MIT](LICENSE) — utilisation, modification et redistribution libres.

Ce logiciel est fourni sans garantie. Il lit l'état du système et ne modifie
rien sans confirmation explicite de l'utilisateur.
