# Vocabulaire anglais de FaultTracePC

Ce fichier existe pour une raison précise : plusieurs textes **se renvoient les
uns aux autres**. Une conclusion écrit « voir la section *Processus en cours* du
rapport », un conseil renvoie à un bouton de la boîte à outils. Si le titre de
section et le renvoi sont traduits dans deux lots différents sans référence
commune, la phrase anglaise pointe vers une section qui ne porte plus ce nom —
et l'utilisateur cherche quelque chose qui n'existe pas.

Chaque terme fixé ici engage **tous** les lots suivants.

## Sections du rapport HTML

| Français | Anglais |
|---|---|
| Processus en cours | Running processes |
| Boîte noire | Flight recorder |
| Entretien effectué | Maintenance performed |
| Limitations | Limitations |
| Aide à la réparation | Repair assistance |

## Interface

| Français | Anglais |
|---|---|
| Analyse profonde (case à cocher) | Deep analysis |
| Boîte à outils | Toolbox |
| Surveillance temps réel | Real-time monitoring |
| Console de parc | Fleet console |
| Analyser ce PC | Analyse this PC |

## Boutons de la boîte à outils

Ces libellés sont **cités** dans le rapport HTML (« bouton 🧰 Tools, puis : … »).
Le rapport a été traduit avant la fenêtre Outils : les intitulés ci-dessous sont
donc des ENGAGEMENTS. Un libellé de bouton qui s'en écarterait ferait chercher
un bouton qui n'existe pas — pire que de ne rien indiquer.

| Français | Anglais |
|---|---|
| 🧰 Outils | 🧰 Tools |
| 💽 Vérifier le disque système (lecture seule) | 💽 Check the system drive (read-only) |
| 🌡 Santé des disques (SMART) | 🌡 Drive health (SMART) |
| 🔌 Alimentation des liens | 🔌 Link power management |
| 💾 Gestion des disques | 💾 Disk Management |
| 🧠 Diagnostic mémoire Windows (redémarre !) | 🧠 Windows Memory Diagnostic (reboots!) |
| ⬇ Rechercher et installer les mises à jour (optionnelles et pilotes inclus) | ⬇ Find and install updates (optional and driver updates included) |
| 🗑 Désinstaller la mise à jour sélectionnée | 🗑 Uninstall the selected update |
| ♻ Réinitialiser les composants Windows Update | ♻ Reset the Windows Update components |
| 🧪 sfc /scannow (fichiers système) | 🧪 sfc /scannow (system files) |
| 🔍 DISM — vérifier l'image Windows | 🔍 DISM — check the Windows image |
| 📡 Surveillance temps réel | 📡 Real-time monitoring |
| Je ne sais pas ce que j'ai | I don't know what's wrong with it |

## Termes techniques récurrents

| Français | Anglais | Remarque |
|---|---|---|
| écran bleu | blue screen / BSOD | |
| pilote | driver | |
| pilote fautif | faulting driver | jamais « guilty » |
| secteurs défectueux | bad sectors | |
| secteurs en attente | pending sectors | |
| réserve de blocs | available spare | terme NVMe officiel |
| usure | wear | |
| boîte noire | flight recorder | |
| conclusion (du diagnostic) | finding | |
| verdict | verdict | |
| parc | fleet | jamais « park » |
| relevé | reading | |

## Ce qui ne se traduit JAMAIS

- Les noms de codes STOP (`DPC_WATCHDOG_VIOLATION`) : identifiants Microsoft,
  cherchés tels quels dans la documentation et sur les forums.
- Les noms de fichiers pilotes (`nvlddmkm.sys`).
- Les noms de commandes et d'outils (`sfc /scannow`, `DISM`, `chkdsk`, WinDbg,
  MemTest86, DDU).
- Les noms de compteurs SMART et de valeurs d'API (`NoErrorsFound`, `NeedsScan`).
- Les identifiants de règles d'alerte (`cpu_temp`, `disk_health_…`).

## Typographie

| | Français | Anglais |
|---|---|---|
| Deux-points | espace avant : `Titre : texte` | pas d'espace : `Title: text` |
| Pourcentage | espace : `4 %` | pas d'espace : `4%` |
| Guillemets | « … » | “ … ” |
| Dates | `jj/mm/aaaa` | `aaaa-mm-jj` (ISO, non ambigu) |
| Décimales | virgule (culture fr-FR) | point — via `Lang.Culture`, jamais la culture du poste |
