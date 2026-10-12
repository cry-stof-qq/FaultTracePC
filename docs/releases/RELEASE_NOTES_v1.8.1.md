## In English

**This release makes the fleet report stop misleading.** A fleet report produced by 1.8.0 on a real school network was re-read card by card. It advised the most up-to-date machine to go back to an older Windows driver, compared 3 machines out of 10 without saying so, and showed three machines in red for hardware errors that the hardware had corrected by itself. Every change below comes from that report.

The notes below are in French. The full English description of the software is here: **[FaultTracePC — diagnose, monitor and repair a Windows PC](https://palisser.fr/spip.php?article31)**

| File | For whom |
|---|---|
| `FaultTracePC-1.8.1.msi` | Classic installation, or Group Policy deployment |
| `FaultTracePC-1.8.1-portable.zip` | No installation: unzip and run |

The `Source code` archives are generated automatically by GitHub: they contain the code, not the compiled software.

Set the language at install time: `msiexec /i FaultTracePC-1.8.1.msi FTPCLANG=en /qn`

**These files are not digitally signed.** Windows will show "Unknown publisher" — click *More info* then *Run anyway*. Checking the SHA-256 fingerprint at the bottom of this page is the only way to be sure the file you downloaded is the one published here.

---

Un rapport du parc produit par la 1.8.0, sur un réseau d'établissement réel, a été relu carte par carte. Il conseillait au poste le plus à jour de revenir à un pilote plus ancien, comparait 3 postes sur 10 sans le dire, et affichait en rouge trois postes pour des erreurs que leur matériel avait corrigées lui-même. Cette version corrige ce que ce rapport a montré.

## Un conseil de mise à jour qui ne fait plus revenir en arrière — point 95

Quand un même pilote existe en plusieurs versions sur le parc, le rapport désignait comme « en retard » tous les postes qui n'avaient pas la version **la plus fréquente**. Le poste qui avait un mois de mises à jour Windows d'avance était ainsi dit en retard, et le conseil revenait à lui faire installer une version plus ancienne.

Désormais, les numéros de version sont **comparés** : les postes en retard sont ceux qui n'ont pas la version **la plus récente**, et le conseil est de les mettre à jour vers elle. La comparaison est numérique (6.0.10016.1 est plus récente que 6.0.9496.1). Si un numéro ne se lit pas, personne n'est désigné : le rapport donne la répartition et conseille de vérifier chez l'éditeur.

## Une erreur corrigée par le matériel n'est plus une alerte critique — point 98

La surveillance classait **toute** erreur matérielle WHEA en « alerte critique », que le matériel l'ait corrigée ou non, et ne gardait pas le numéro de l'événement. Mesuré sur un poste concerné : 50 événements, tous de numéro 17 et de niveau « Avertissement » — des erreurs corrigées, aucune fatale.

- La gravité suit désormais le niveau que **Windows lui-même** donne à l'événement : avertissement → « à surveiller » ; erreur ou critique → « critique ».
- L'alerte garde le numéro de l'événement : « Erreur matérielle corrigée (WHEA, événement 17) ». Elle ne parle plus de « défaillance » ni de « processeur », et conseille de lancer un diagnostic du poste, qui nomme le lien en cause.
- Une erreur corrigée ne fait plus taire, pendant le délai anti-répétition, une erreur grave qui la suivrait.

Les alertes **déjà archivées** avant la 1.8.1 restent « critiques » : elles ne portent ni numéro ni niveau, rien ne permet de les reclasser. Elles sortent du compte des 90 jours en vieillissant.

## Le rapport du parc dit sur quels postes il compare — point 97

La comparaison ne porte que sur les postes qui ont déjà eu un diagnostic complet. Le rapport ne le disait pas : « 2 poste(s) sur 3 » se lisait « deux tiers du parc ». Il affiche désormais, sous le verdict de la comparaison, les postes comparés, et chacun des autres avec sa raison et ce qu'il faut faire — par exemple « jamais analysé — lancer un diagnostic à distance ».

Au passage, un défaut corrigé : un poste **jamais analysé** était compté parmi ceux dont le résumé « vient d'une version antérieure ».

## Une boîte noire réellement à 10 secondes — point 90

La fenêtre de la boîte noire annonce un relevé toutes les 10 secondes ; ils tombaient toutes les 12 secondes environ, parce que le service attendait 10 secondes **après** chaque relevé. Il suit désormais une horloge fixe.

## La barre d'état de la console lisible en entier — point 92

En bas de l'onglet Supervision, le message d'état partageait la ligne avec cinq boutons et était coupé (« Tous les postes joignables son… »). Il a maintenant sa propre ligne, au-dessus des boutons.

## Ce qu'il faut mettre à jour, et où

- Les points 95, 97 et 92 concernent la **console** : il suffit de mettre à jour le poste d'où l'on administre le parc.
- Les points 98 et 90 concernent le **service de surveillance** : ils ne s'appliquent qu'aux postes mis à jour en 1.8.1. Un poste resté en 1.8.0 continue de classer ses erreurs WHEA en critique.

## Ce qui ne change pas

- **Le mode parc reste inclus**, comme en 1.8.0, et une mise à jour le conserve. Le rendre facultatif à l'installation reste prévu pour la 1.9.0.
- Les analyses enregistrées par les versions précédentes restent lisibles.

842 tests, aucun échec.
