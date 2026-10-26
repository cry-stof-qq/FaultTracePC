## In English

**This release adds one option: listing which installed programs have a newer version.** Tick "Software updates (winget)" before the analysis, and the report says which programs winget can update, with both versions — and, on the card of an application that keeps crashing, that an update exists for it. The box is unticked by default, because winget contacts the Internet, and FaultTracePC never does so without a ticked box. Nothing is installed: this release only reads.

The notes below are in French. The full English description of the software is here: **[FaultTracePC — diagnose, monitor and repair a Windows PC](https://palisser.fr/spip.php?article31)**

| File | For whom |
|---|---|
| `FaultTracePC-1.10.0.msi` | Classic installation, or Group Policy deployment |
| `FaultTracePC-1.10.0-portable.zip` | No installation: unzip and run |

The `Source code` archives are generated automatically by GitHub: they contain the code, not the compiled software.

Set the language at install time: `msiexec /i FaultTracePC-1.10.0.msi FTPCLANG=en /qn`

Fleet console at install time: `msiexec /i FaultTracePC-1.10.0.msi /qn ADDLOCAL=Main,ParkFeature` (or `ADDLOCAL=ALL`).

**These files are not digitally signed.** Windows will show "Unknown publisher" — click *More info* then *Run anyway*. Checking the SHA-256 fingerprint at the bottom of this page is the only way to be sure the file you downloaded is the one published here.

---

Une option nouvelle : savoir quels logiciels installés ont une version plus récente. L'installation est la même qu'en 1.9.x.

## Les mises à jour des logiciels, dans le rapport — point 15

Un logiciel resté ancien pendant que Windows avance finit parfois par poser problème. FaultTracePC peut maintenant le dire, en interrogeant **winget**, le gestionnaire de paquets de Windows.

**Une case, décochée par défaut** : « Mises à jour des logiciels (winget) », dans la fenêtre principale, à côté de « Analyse profonde (WinDbg) ». winget interroge sa source sur Internet ; or FaultTracePC promet de ne jamais contacter Internet sans case cochée. La case se décoche à chaque ouverture du logiciel. Elle ajoute jusqu'à deux minutes à l'analyse. **Elle n'installe rien** : c'est une lecture.

Case cochée, le rapport montre :

- **une conclusion « Information »** : « N logiciel(s) avec une mise à jour disponible », avec leurs noms et les deux versions (installée → disponible). Elle ne change ni le verdict ni le script de réparation ;
- **sur la carte d'une application qui plante**, quand c'est le cas : « Une mise à jour est disponible pour « … » selon winget : 8.8.5 → 8.9.8.1. » Le rapprochement entre le programme qui plante et le paquet de winget se fait sur le nom : c'est une ressemblance, pas une preuve, et la phrase demande de vérifier qu'il s'agit bien du même logiciel ;
- **une section dans les détails techniques**, « Mises à jour des logiciels (winget) » : nom, identifiant, version installée, version disponible, et pour qui le logiciel est installé — tout le poste, ce compte, ou les deux.

**Deux interrogations, pas une.** winget distingue les logiciels installés pour tout le poste et ceux installés pour le compte qui l'utilise. Mesuré sur un poste réel : la liste ordinaire de winget n'est pas la réunion des deux — un logiciel en retard, installé pour le compte, n'y figurait pas. FaultTracePC interroge donc les deux, et réunit les réponses par identifiant : un logiciel vu des deux côtés n'est compté qu'une fois.

**Jamais « tout est à jour » sans preuve.** Si winget est introuvable, ne répond pas en une minute, ou échoue, le rapport le dit, avec la raison et le code de winget, et précise que la liste est incomplète. Sur un poste sans Internet, ou dont l'accès est filtré, il explique : « winget n'a pas pu joindre sa source : pas d'accès à Internet, ou accès filtré. Réessayer avec une connexion qui l'autorise. » — deux cas mesurés, codes 0x8A15004B et 0x80072EE7. Sans Internet, winget a parfois répondu quand même, sans doute depuis une copie récente de son catalogue ; au-delà de son délai de vérification (15 minutes par défaut), il a échoué au lieu de présenter une liste ancienne — mesuré une fois, dans une machine virtuelle. Quand winget répond normalement sans rien signaler, le rapport écrit « winget ne signale aucune mise à jour », et cite winget.

**Case décochée, le rapport ne parle pas de winget**, ni pour dire « à jour », ni pour dire « non vérifié ».

## Limites, dites franchement

- **Pas pour le diagnostic à distance.** Il s'exécute sous le compte système de Windows, où Microsoft indique que winget en ligne de commande n'est pas pris en charge. Un diagnostic lancé depuis la console de parc ne dit donc rien des mises à jour de logiciels. Une autre voie, documentée par Microsoft pour le compte système, sera essayée sur un parc réel avant d'être proposée.
- **Seulement ce que winget connaît.** Un logiciel absent des sources de winget n'apparaît pas, même s'il est en retard.
- **winget doit être présent pour le compte qui lance FaultTracePC.** C'est le cas sur un Windows 10 ou 11 à jour ; si FaultTracePC est ouvert avec un autre compte administrateur, winget peut ne pas l'être, et le rapport le dit.
- **Rien ne s'installe encore.** Mettre à jour depuis FaultTracePC — un logiciel, ou tous — viendra dans une version suivante, avec ses propres garde-fous : une mise à jour agit sur la machine.

## Ce qu'il faut mettre à jour, et où

- **Sur un poste** que l'on diagnostique devant l'écran : c'est là que la case sert.
- **Sur un parc** : rien ne change pour les diagnostics à distance (voir les limites). Mettre les postes à jour n'est utile que pour s'en servir sur place.

## Ce qui ne change pas

- L'installation est la même qu'en 1.9.0. La case « Mode parc » garde le choix fait à l'installation précédente.
- Rappel : une liste `ADDLOCAL` installe exactement ce qu'elle nomme. `ADDLOCAL=Main` installe **sans** la console, même en mise à jour ; pour la garder, `ADDLOCAL=Main,ParkFeature`.
- FaultTracePC demande un Windows 10 ou 11 à jour.
- Les analyses enregistrées par les versions précédentes restent lisibles.
