## In English

Two observed defects, no new surface. A diagnosis launched from the fleet console came back written in the **target machine's** language instead of the administrator's — the service runs as SYSTEM, whose interface culture is the machine's, never the administrator's session. And the `.bat` launcher sitting next to a report kept its window open forever, while the script it runs ends with "Press Enter to close": the software was writing a sentence that was not true.

The notes below are in French. The full English description of the software is here: **[FaultTracePC — diagnose, monitor and repair a Windows PC](https://palisser.fr/spip.php?article31)**

| File | For whom |
|---|---|
| `FaultTracePC-1.5.2.msi` | Classic installation, or Group Policy deployment |
| `FaultTracePC-1.5.2-portable.zip` | No installation: unzip and run |

The `Source code` archives are generated automatically by GitHub: they contain the code, not the compiled software.

Set the language at install time: `msiexec /i FaultTracePC-1.5.2.msi FTPCLANG=en /qn`

**These files are not digitally signed.** Windows will show "Unknown publisher" — click *More info* then *Run anyway*. Checking the SHA-256 fingerprint at the bottom of this page is the only way to be sure the file you downloaded is the one published here.

---

Deux défauts constatés en utilisant le logiciel, corrigés sans rien ajouter d'autre.

## Un rapport distant sortait dans la langue du poste, pas dans la vôtre

Diagnostic lancé depuis la console de parc vers une machine dont la session est française : rapport en anglais.

`/api/scan` ne rapatrie pas des données — il déclenche l'analyse sur la machine visée, et **c'est le service de cette machine qui écrit le HTML**. Or ce service tourne sous le compte SYSTEM, dont la culture d'interface est celle de la machine et jamais celle de la session de l'administrateur. La langue se résolvait donc sans lui.

La console joint maintenant sa langue à la requête. Le poste l'applique le temps de l'analyse, puis reprend la sienne : la demande vaut pour ce rapport-là, pas pour le service.

C'était une incohérence avec un principe que le logiciel appliquait déjà ailleurs — le poste transmet un code, la console écrit la phrase, dans la langue de celui qui lit. Le rapport, lui, était encore écrit dans la langue de celui qui l'exécute.

**Un parc mixte continue de fonctionner.** Un poste resté en 1.5.1 ignore simplement le paramètre. Une console plus ancienne ne demande rien — et « rien demandé » veut dire « le poste garde sa langue », non « français » : sans cette nuance, une console non mise à jour aurait imposé le français à un parc anglophone.

## La fenêtre de réparation se ferme enfin, partout

La 1.3.1 avait ajouté `-NoExit` pour qu'une console ne s'évapore plus quand une stratégie de groupe refuse le script avant sa première ligne. Effet non voulu : la fenêtre ne se refermait **plus jamais** d'elle-même, alors que le script se termine par « Appuyer sur Entrée pour fermer ».

Les trois boutons de l'application avaient été corrigés en 1.5.0. Le lanceur `.bat` posé à côté du rapport, lui, était resté en arrière — c'est celui qu'ouvre la personne qui a reçu un rapport et son script, et qui double-clique.

Il emploie désormais le même enrobage : `-Command` n'étant pas soumis à la stratégie d'exécution, il démarre toujours, affiche le refus s'il y en a un, et ne retient la fenêtre **que** si le script n'est pas allé au bout — sans quoi il faudrait appuyer deux fois sur Entrée. La stratégie n'est pas contournée : son refus est montré au lieu de passer en un clin d'œil.

Ses lignes de commentaire suivent maintenant la langue du rapport ; elles restaient en français sur un poste anglais.

**Une limite reste, et elle est écrite dans le code :** le chemin du script arrive par `%~dp0`, qui n'est connu qu'à l'exécution. Un dossier utilisateur contenant une apostrophe couperait le littéral PowerShell — le défaut corrigé en 1.4.1, au seul endroit où on ne peut pas l'échapper d'avance.

## Mise à jour

Le MSI remplace proprement la 1.5.1, et lui-même. Aucun changement de format de fichier ni de protocole de parc : le paramètre de langue est purement additif.
