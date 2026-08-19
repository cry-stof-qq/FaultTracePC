## In English

This release is about **what the reader sees**. The report now states how fresh its data is, shows the serious findings first and folds the rest, and no longer reports the same fact twice under two contradictory severities. Persisted files now carry a format number, so an old file is refused plainly instead of half-understood — nothing is deleted.

The notes below are in French. The full English description of the software is here: **[FaultTracePC — diagnose, monitor and repair a Windows PC](https://palisser.fr/spip.php?article31)**

| File | For whom |
|---|---|
| `FaultTracePC-1.4.0.msi` | Classic installation, or Group Policy deployment |
| `FaultTracePC-1.4.0-portable.zip` | No installation: unzip and run |

The `Source code` archives are generated automatically by GitHub: they contain the code, not the compiled software.

Set the language at install time: `msiexec /i FaultTracePC-1.4.0.msi FTPCLANG=en /qn`

**These files are not digitally signed.** Windows will show "Unknown publisher" — click *More info* then *Run anyway*. Checking the SHA-256 fingerprint at the bottom of this page is the only way to be sure the file you downloaded is the one published here.

---

Un seul thème : **ce que l'utilisateur lit**. Aucun nouveau diagnostic. Le logiciel regarde les mêmes choses qu'avant — il les raconte mieux, et il arrête d'en affirmer plus qu'il n'en sait.

## Le rapport dit de quand il date

Un rapport daté d'aujourd'hui laisse croire qu'il décrit aujourd'hui. Sur une machine éteinte depuis des mois, il décrit un passé lointain, et rien à la lecture ne permettait de s'en apercevoir. « Aucun problème détecté » sur trente jours dont vingt-huit machine éteinte n'est pas une bonne nouvelle : c'est une absence de mesure.

Une ligne sous l'en-tête donne désormais **l'âge du fait le plus récent**, toutes sources confondues — écrans bleus, journal d'événements, historique de fiabilité, boîte noire — et **la couverture réelle** de la période : « la surveillance temps réel a enregistré 7 jours sur les 30 analysés ».

Sans la surveillance, la durée d'allumage n'est pas annoncée : elle n'est **pas connue**. Elle seule écrit à intervalle régulier ; la déduire d'événements de démarrage que Windows n'écrit pas toujours reviendrait à avancer un chiffre inventé. Un test verrouille la distinction entre « journal absent » et « couverture de zéro jour » — le premier est une ignorance, le second serait une mesure.

## Un fait, une conclusion

Sur un rapport réel, la même erreur matérielle apparaissait **deux fois** : en avertissement depuis le journal de Windows, en critique depuis la surveillance temps réel. Même matériel, même dernière occurrence. Le lecteur voyait deux problèmes là où il n'y en a qu'un, avec deux gravités qui se contredisaient.

Les conclusions rapportant le même fait sont désormais fusionnées, après l'exécution de toutes les règles et avant le tri — l'ordre dans lequel les règles s'exécutent ne doit pas décider de ce qui survit. La conclusion conservée hérite de la gravité et de la confiance les plus fortes, et le doublon devient un argument : « ce fait a été signalé par deux chemins indépendants, ce qui le confirme ».

**Rien de ce qu'avait constaté l'autre chemin n'est perdu.** La première version de ce mécanisme effaçait la carte perdante ; vérification faite sur un rapport réel, le rapport y perdait le nombre d'occurrences relevées — trois événements, quand la carte conservée n'en annonçait que deux — et le nom du matériel en cause. Sous-estimer une magnitude est pire que répéter une date.

## L'essentiel d'abord

Un rapport peut porter huit conclusions, toutes visibles d'un coup. Un technicien lit une liste ; quelqu'un qui découvre son problème ne sait pas par où commencer.

**Toute conclusion critique reste visible, sans exception** — replier un problème grave serait exactement le défaut que ce logiciel corrige partout ailleurs. S'y ajoute le premier avertissement. Le reste se replie, et seulement à partir de deux éléments : replier une ligne unique fait cliquer pour découvrir une ligne.

Le bouton annonce le compte exact et sa répartition — « voir les 3 autres conclusions (3 informations) ». Masquer sans dire combien est précisément ce que ce logiciel reproche aux autres.

Et **tout ce qui est replié se rouvre à l'impression**. Un PDF transmis à un réparateur ne doit pas être amputé sans que son destinataire le sache. Deux protections volontairement redondantes, une règle d'impression et un gestionnaire de script, parce que le rendu d'un bloc replié dépend du navigateur. Cela corrige au passage deux blocs préexistants — les options avancées et les piles d'appels — absents des PDF exportés jusqu'ici.

## Les fichiers disent de quel format ils sont

Jusqu'ici, aucun fichier écrit par le logiciel n'indiquait de quel format il était. Chaque évolution obligeait à reconnaître les anciennes écritures **à leur allure** — un mot français ici, un champ absent là — et ce code de reconnaissance ne s'enlevait jamais.

Les résumés d'analyse portent désormais un numéro de format. Un fichier d'une version antérieure est **refusé franchement** au lieu d'être à demi compris, et un fichier écrit par une version plus récente l'est aussi : celle-ci ne saurait pas le lire correctement, et se taire vaut mieux que comparer sur des champs mal interprétés.

**Aucune donnée n'est supprimée.** Les fichiers restent dans le dossier Historique, et le rapport annonce leur nombre : « 3 analyses enregistrées par une version antérieure n'ont pas été relues — leur format a changé. Les fichiers restent, rien n'a été supprimé. » Refuser de relire est un choix technique ; effacer les données de quelqu'un en serait un autre, et celui-là ne se prend pas en silence.

La console de parc applique la même règle aux résumés reçus par le réseau : un poste resté en 1.3 est écarté du tableau et signalé, plutôt que produire une comparaison fausse.

Le retrait du code de compatibilité a été fait **pendant que le parc installé se compte en quelques machines** — cette fenêtre se referme au premier déploiement réel. Sur trois candidats recensés, un seul en était vraiment : la lecture des libellés français de la 1.2.x, devenue du code mort. Les deux autres — le renoncement du catalogue d'alertes quand l'extrait de Windows manque, et l'acceptation du format ancien par le protocole de parc — sont des gardes permanentes, et restent.

## Quatre défauts trouvés en chemin

Aucun ne figurait dans la feuille de route. Tous ont été trouvés en ouvrant un fichier ou un rapport réel.

**« 💡 Recommandation » s'affichait en français dans chaque conclusion du rapport anglais.** Le contrôle de traduction ne pouvait pas le voir : un mot isolé, sans accent, dont la forme anglaise ne diffère que d'une lettre.

**Le bouton « afficher les détails techniques » repassait en français au clic**, dans le rapport anglais. Le script qui réécrit son libellé est une constante — il ne peut contenir aucun appel de traduction. Et son exemption disait « aucun texte affiché », ce qui était vrai de presque tout le script et faux de deux lignes. Le libellé initial étant correct, la fuite ne se voyait qu'après un clic.

**Le rapport anglais affichait 147 tailles en français** — « Ko », « Mo », « Go ». Une unité de deux lettres n'a ni accent, ni mot outil, ni typographie française. Le séparateur décimal suit désormais la langue lui aussi : « 4,2 Go » dans un document anglais peut se lire comme un séparateur de milliers, soit dix fois la valeur réelle.

**Cinq lignes de la feuille de route** étaient données comme ouvertes alors qu'elles étaient faites depuis la 1.2.3, et une sixième depuis quatre versions.

Une liste nominative de faux amis a été ajoutée au contrôle de traduction, pour les mots que ses trois signaux ne peuvent pas voir par construction.

## Vérification

**Environ 315 tests.** Les plus utiles ne vérifient pas ce qui marche, mais ce qui pourrait faussement rassurer :

- aucune conclusion critique ne se retrouve jamais dans le bloc replié ;
- dans les échelles de gravité et de confiance, la valeur la plus basse est la plus forte : une comparaison à l'envers dégraderait silencieusement une conclusion critique, et un test l'interdit ;
- les conclusions sans identifiant ne sont jamais fusionnées — les regrouper aurait fondu tout le rapport en une seule carte ;
- un résumé écrit par une version plus récente est refusé comme un ancien ;
- l'annonce des fichiers écartés ne mentionne jamais une suppression, dans les deux langues ;
- le rapport anglais ne contient aucune unité française, avec son contrôle positif côté français.

## Mise à jour

Le MSI remplace proprement la 1.3.1. Le service, le protocole de parc et le format des rapports HTML sont inchangés.

**Un seul point d'attention :** les résumés d'analyse écrits par les versions antérieures ne sont plus relus. La première analyse après la mise à jour n'aura donc pas de comparaison avec la précédente, et le rapport le dira. Les fichiers ne sont pas supprimés.
