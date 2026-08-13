<div align="center">

<img src="assets/FaultTracePC.png" alt="FaultTracePC" width="120">

# FaultTracePC

**Trouver la cause d'une panne Windows 10/11 — et savoir quoi faire ensuite.**

Analyse des écrans bleus, boîte noire temps réel, alertes avant la panne,
rapport lisible et aide à la réparation. Gratuit, en français, sans télémétrie.

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

**Aide à la réparation.** Chaque diagnostic génère un script PowerShell adapté
aux problèmes trouvés — et rien ne se lance sans confirmation. Une boîte à
outils intégrée réunit les réparations courantes : désinstaller une mise à jour
Windows fautive, réinitialiser les composants Windows Update, `sfc`, `DISM`,
`chkdsk`, diagnostic mémoire, SMART.

**Suivi et parc.** Chaque scan est archivé : le suivant répond à la vraie
question — « est-ce que la réparation a marché ? ». En mode parc, une console
affiche l'état de plusieurs machines et permet de lancer un diagnostic à
distance sans se déplacer.

## Installation

| Format | Pour qui | Comment |
|---|---|---|
| **MSI** | Installation durable, déploiement par GPO | `msiexec /i FaultTracePC-1.0.0.msi` (ou double-clic) |
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
dotnet test                                    # 59 tests
dotnet run --project src\FaultTracePC.App
```

Produire les distribuables :

```powershell
powershell -ExecutionPolicy Bypass -File build\publish.ps1 -Zip
powershell -ExecutionPolicy Bypass -File installer\build-msi.ps1 -Version 1.0.0
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

## Licence

[MIT](LICENSE) — utilisation, modification et redistribution libres.

Ce logiciel est fourni sans garantie. Il lit l'état du système et ne modifie
rien sans confirmation explicite de l'utilisateur.
