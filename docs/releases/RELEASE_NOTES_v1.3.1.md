## In English

A corrective release. The **Run the repair** button could open a console that closed again immediately, without a word and without doing anything, on machines where a Group Policy forbids running scripts — a common setting in companies and schools. The software now checks that policy first, names it, and keeps the window open instead of vanishing. It also writes its own failures to a log file for the first time.

The notes below are in French. The full English description of the software is here: **[FaultTracePC — diagnose, monitor and repair a Windows PC](https://palisser.fr/spip.php?article31)**

| File | For whom |
|---|---|
| `FaultTracePC-1.3.1.msi` | Classic installation, or Group Policy deployment |
| `FaultTracePC-1.3.1-portable.zip` | No installation: unzip and run |

The `Source code` archives are generated automatically by GitHub: they contain the code, not the compiled software.

Set the language at install time: `msiexec /i FaultTracePC-1.3.1.msi FTPCLANG=en /qn`

**These files are not digitally signed.** Windows will show "Unknown publisher" — click *More info* then *Run anyway*. Checking the SHA-256 fingerprint at the bottom of this page is the only way to be sure the file you downloaded is the one published here.

---

Une version corrective, née d'un retour d'utilisateur. Aucune fonction nouvelle : elle répare un défaut qui rendait la réparation impossible sur tout un type de postes, et elle donne enfin au logiciel le moyen de dire ce qui lui arrive.

## Le bouton qui ne faisait rien

Sur une machine Windows 11 23H2, un clic sur **« Lancer la réparation »** ouvrait une console qui se refermait aussitôt. Pas de message, pas de trace, pas de travail effectué.

La cause n'était pas celle qu'on croit. Le bouton démarrait PowerShell avec `-ExecutionPolicy Bypass -File <script.ps1>`, ce qui semble suffisant. Ce ne l'est pas : **cette option ne fixe que la portée `Process`, la plus faible de toutes.** Une stratégie de groupe — portées `MachinePolicy` et `UserPolicy` — prime sur elle. Sur un poste où l'administration a fixé `Restricted` ou `AllSigned`, ce qui est la configuration courante en entreprise et en établissement scolaire, PowerShell refuse le fichier **avant d'en lire la première ligne**, écrit son refus, et se referme.

Le lancement, lui, avait réussi : le programme n'avait donc aucune erreur à rattraper, et ne disait rien.

La preuve était dans le logiciel lui-même. Le script généré se termine par « Appuyer sur Entrée pour fermer ». Si cette invite ne s'affiche pas, c'est que le script n'a pas échoué — il n'a jamais commencé.

## Ce qui change

**La stratégie est vérifiée avant de lancer.** Le logiciel interroge `Get-ExecutionPolicy -List` et ne regarde que les deux portées qu'une stratégie de groupe impose. C'est une distinction qui compte : `LocalMachine` vaut `Restricted` par défaut sur presque toutes les machines Windows saines, et le `Bypass` de portée `Process` la remplace parfaitement. Confondre les deux ferait refuser la réparation sur la majorité des postes en bon état.

**Le refus est expliqué.** Quand une stratégie bloque, le message nomme la portée et la valeur exactes, indique que le réglage vient de l'administration du parc, et donne deux issues : demander l'autorisation des scripts locaux (`RemoteSigned`), ou lancer les réparations une par une depuis la boîte à outils — qui n'utilise aucun fichier de script et n'est donc pas concernée.

**La console ne disparaît plus.** L'option `-NoExit` est ajoutée au lancement et au fichier `.bat` généré. Le `Read-Host` final du script ne sert à rien s'il n'est jamais atteint : seul l'hôte PowerShell, qui traite cette option avant même d'ouvrir le fichier, peut garder la fenêtre ouverte pour montrer un refus.

## Ce que le logiciel refuse de faire

Contourner la stratégie. C'est techniquement possible — un script peut être passé par l'entrée standard, ce qui échappe au contrôle — et ce serait exactement ce qu'un administrateur a interdit.

Un outil de diagnostic destiné à être déployé sur des parcs ne peut pas désobéir à la configuration de ces parcs. Il constate, nomme la cause, et laisse la décision à qui de droit.

## Le logiciel sait enfin dire ce qui lui arrive

Ce retour d'utilisateur a révélé un manque plus large que le défaut lui-même : **aucune exception n'était rattrapée nulle part, et rien n'était jamais écrit.** Ni l'utilisateur ni l'auteur ne pouvaient conclure quoi que ce soit. Un logiciel dont toute la démarche consiste à dire « je n'ai pas su lire » ne peut pas disparaître sans un mot.

Les trois exécutables posent désormais des gardes globales, avant même la résolution de la langue, et écrivent dans **`%ProgramData%\FaultTracePC\erreurs.log`** : horodatage, exécutable, version, version de Windows, langue active, droits administrateur, puis l'exception avec sa pile et toutes ses causes internes.

Ce dossier plutôt que `Documents`, pour une raison précise : le service de surveillance tourne sous le compte SYSTEM, qui n'a aucun dossier Documents d'utilisateur — un journal rangé là ne recueillerait jamais **ses** pannes à lui, alors que c'est le composant le plus difficile à observer. L'utilisateur, lui, n'a rien à deviner : le message affiché donne le chemin complet.

Trois partis pris : ce journal **ne lève jamais d'exception** — un journal qui plante en écrivant une erreur remplacerait la panne d'origine par la sienne ; il **tourne à 512 Ko**, un journal sans limite finissant par occuper l'espace disque dont le logiciel diagnostique le manque ; et **son contenu n'est pas traduit**, parce qu'un journal venu d'une machine anglaise doit se comparer ligne à ligne avec un autre.

En ligne de commande, le filet couvre maintenant aussi ce qui précède l'analyse — résolution de la langue, lecture des arguments, `--set-machine-lang` — qui pouvait jusqu'ici refermer la console sans un mot. Le message part sur la sortie d'erreur **même sous `--quiet`** : un plantage n'est pas une information qu'on a le droit de taire à un script de parc.

## Correction de traduction

Le message « une réparation est déjà en cours » perdait, dans sa version anglaise, le nom de l'outil bloquant et sa mise en forme. Le test de ratio ne l'avait pas vu : 24 caractères contre 40 passent son seuil. La même phrase existait ailleurs dans le logiciel, complète — c'était une copie qui avait perdu sa fin.

## Vérification

**262 tests**, dont douze nouveaux. Les plus utiles ne vérifient pas ce qui marche mais ce qui pourrait faussement rassurer :

- `LocalMachine=Restricted` **ne doit pas** bloquer la réparation — c'est le cas par défaut d'une machine saine ;
- une sortie de PowerShell incompréhensible ne bloque rien non plus : mieux vaut tenter la réparation et montrer l'échec que la refuser sur une lecture ratée ;
- le compte rendu d'erreur remonte **toutes** les exceptions internes, sans quoi on lirait « une erreur est survenue » sans jamais voir la cause enfouie deux niveaux plus bas ;
- il est identique en français et en anglais ;
- le journal ne s'écrit pas dans `Documents`.

## Mise à jour

Le MSI remplace proprement la 1.3.0. Aucun changement de protocole de parc, de format de rapport ni de service : rien à refaire côté déploiement.
