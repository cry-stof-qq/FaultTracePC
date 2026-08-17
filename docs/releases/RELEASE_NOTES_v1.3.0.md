FaultTracePC parle anglais. Toute l'interface, tout le rapport, tous les messages, la ligne de commande et le script de réparation — pas une couche de traduction posée par-dessus, mais les deux langues écrites côte à côte dans le code, où l'oubli de l'une empêche la compilation.

Cette version ne change **aucun diagnostic**. Elle corrige en revanche quatre endroits où le logiciel décidait à partir de mots français, ce qui le rendait faux dès qu'il tournait ailleurs qu'en France.

## La langue se choisit toute seule, et se choisit à la main

Au premier lancement, FaultTracePC suit la **langue d'affichage de la session Windows** — pas le format des dates : un utilisateur français sur un Windows anglais a souvent gardé les dates françaises, et c'est bien la langue des menus qui dit dans quelle langue il lit.

Un sélecteur dans le bandeau, à côté du `?`, permet d'imposer le français, l'anglais, ou de revenir au comportement automatique. Le choix est retenu par utilisateur, dans `Documents\FaultTracePC\langue.txt` — pas à côté de l'exécutable : sur un poste partagé, le réglage d'un compte n'a pas à s'imposer aux autres.

Le libellé du bouton affiche la langue **active** ; la coche du menu marque la préférence **enregistrée**. Les deux se séparent tant qu'un changement n'a pas été suivi d'un redémarrage, et prétendre le contraire serait mentir : les libellés de l'interface sont construits à l'ouverture de la fenêtre, seule une relance les reconstruit. L'application le dit et propose de redémarrer — dans la langue que vous venez de choisir, pas dans celle que vous quittez. Si une analyse est en cours, elle le signale et propose **Non** par défaut.

En ligne de commande, `--lang fr|en|auto` impose la langue et prime sur tout le reste. L'option existait depuis la version bilingue interne ; elle est enfin documentée dans `--help`, dont l'aide complète était d'ailleurs restée en français.

## Un parc se déploie dans la langue qu'on veut

L'ordre complet est désormais : `--lang`, puis le choix de l'utilisateur, puis un **réglage de poste**, puis la session Windows.

Ce réglage de poste, `C:\ProgramData\FaultTracePC\langue.txt`, s'écrit à l'installation :

```
msiexec /i FaultTracePC-1.3.0.msi FTPCLANG=en /qn
```

ou à la main sur un poste, `FaultTracePC.Cli.exe --set-machine-lang en`. Il passe **avant** la session Windows — c'est précisément quand les sessions disent « français » et que l'administration veut l'anglais qu'il sert — mais **après** le choix de l'utilisateur : un réglage d'administrateur est un défaut, pas une contrainte. Le contraire ferait passer le sélecteur de l'application pour cassé, l'utilisateur cliquant « English » et retrouvant le français au lancement suivant. `FTPCLANG=auto` efface un réglage posé auparavant.

Il ne sert pas qu'au déploiement. Le service de surveillance tourne sous le compte SYSTEM, dont le dossier Documents ne contient jamais le fichier de l'utilisateur : sans ce réglage, il suivait toujours la langue par défaut du poste, et les alertes qu'il écrit en héritaient.

## Quatre décisions qui ne se prennent plus sur du texte traduit

C'est la partie de cette version qui a une vraie valeur de correction, indépendamment de la langue.

**L'état de santé d'un disque** était un mot français (`« Sain »`, `« Défaillant »`) comparé pour décider. Il est devenu une énumération. La différence n'est pas cosmétique : une valeur non rapportée par le matériel était rangée avec « sain », donc un disque muet passait pour un disque en bonne santé. Ce sont désormais deux états distincts. Les résumés écrits par la 1.2.x, qui contiennent les anciens mots, restent relus tels quels — une mise à jour remplace le logiciel, jamais les fichiers qu'il a déjà écrits.

**Le compte rendu de `sfc`** était reconnu par des motifs français. Sur une session anglaise, aucun ne correspondait, et le mode guidé concluait « rien trouvé » : un faux négatif silencieux sur une machine peut-être abîmée. La lecture reconnaît maintenant les deux langues, et surtout sait dire qu'elle **n'a pas su lire** — auquel cas elle ne conclut rien et propose de relancer la commande en visible.

Un piège s'est révélé au passage, en relevant les phrases dans le fichier de ressources de Windows lui-même : `sfc.exe.mui` mélange l'apostrophe typographique `’` et l'apostrophe droite `'` **dans le même fichier**. Une comparaison naïve aurait déclaré illisible le compte rendu d'une machine française parfaitement saine. Les apostrophes et les espaces insécables sont normalisés avant comparaison.

**`DISM`** est désormais appelé avec `/English` partout où c'est le **programme** qui lit sa sortie — c'est le seul moyen de la rendre déterministe quelle que soit la session. Là où c'est un **humain** qui lit, dans la boîte à outils qui ouvre une console visible, l'option n'est surtout pas ajoutée. La documentation Microsoft prévient que certaines ressources ne peuvent pas être affichées en anglais : la lecture reconnaît donc encore le français, et le refus de l'option est détecté pour réessayer sans elle.

**Le nom du pilote fautif** dans la conclusion était reconstitué en découpant un titre. Le découpage tombait à côté dès que le titre changeait de langue. Chaque conclusion porte maintenant un **code** stable et un **sujet** — le nom de fichier, le modèle de disque — qui ne sont jamais traduits.

## Le parc mélange les langues sans configuration

Le protocole de comparaison de parc transportait des phrases. Il transporte maintenant des **codes** : niveau d'analyse, compteurs, modèles. Une console française interrogeant un poste anglais lit des faits, pas de la prose, et les met en forme dans sa propre langue. Les deux formats sont acceptés en réception, et une réponse illisible ne produit **jamais** un verdict — elle produit un échec.

## Une alerte enregistrée se relit dans la langue du moment

Le service écrivait la phrase française dans `alerts.json` au moment de l'émission. Le lendemain, l'application en anglais relisait ce fichier et affichait la phrase française — elle n'avait rien d'autre sous la main.

Or le fait, lui, était déjà là : l'identifiant de règle (`cpu_temp`) et la valeur mesurée (92). « La règle température processeur s'est déclenchée à 92 » suffit à refabriquer la phrase dans la langue du moment. Une table unique, `AlertCatalog`, porte désormais le texte des neuf règles ; le service l'appelle avant d'écrire, le lecteur après avoir lu. `AlertEngine` ne contient plus une seule chaîne traduite.

Rien n'a été ajouté au fichier là où le fait pouvait se déduire : le modèle du disque est dans l'identifiant de règle, et son état se lit dans le niveau — `crit` a été émis pour un disque défaillant, `warn` pour un disque à surveiller. Un seul champ est apparu, pour l'extrait du message de Windows que citent deux règles : celui-là ne se déduit de rien.

La table **renonce** dans trois cas plutôt que d'écrire une phrase fausse : règle inconnue — une alerte venue d'une version plus récente —, règle à seuil sans valeur, et extrait manquant, ce qui est le cas des alertes écrites avant cette version. Le texte d'origine est alors laissé intact : une phrase dans la mauvaise langue vaut mieux qu'une phrase amputée du fait qu'elle rapporte.

## Ce qui empêche la version anglaise de se dégrader

Une traduction se défait silencieusement : une phrase coupée en deux dont seule la première moitié est traduite, et le français réapparaît au milieu d'une page anglaise. C'est arrivé, ici, une quinzaine de fois.

Un test lit donc le **code source** à chaque exécution de la suite et exige que tout littéral d'allure française se trouve dans un appel de traduction. Deux soupapes, toutes deux traçables : quatre fichiers exemptés en bloc — les tables qui stockent les deux langues côte à côte, dont la complétude est vérifiée par leur propre test — et un commentaire `// pas-de-traduction :` posé à huit endroits, chacun avec sa raison : une clé interne, un fragment de la sortie de Windows qu'on reconnaît, de la CSS.

Reconnaître le français demande **trois signaux, pas un**. Un accent ou un chevron ; à défaut trois mots outils français distincts ; à défaut **l'espace avant `:` `;` `!` `?`**, une marque typographique que l'anglais n'a pas. Les deux premiers laissaient passer « Surveillance : ACTIVE » et « Connu de FaultTracePC ? » — ni accent, ni trois mots. Aucun mot commun aux deux langues ne figure dans la liste : « analyse », « machine », « cause » n'apportent aucun signal et feraient prendre une phrase anglaise pour du français.

Ce test a trouvé, avant d'être écrit, des fuites dans la comparaison de parc, les erreurs d'intégrité SMART, les coupures d'alimentation, la carte des disques du rapport, les deux messages d'échec de l'export PDF, deux commentaires du script PowerShell généré, l'état de batterie, et l'aide complète du CLI. Puis, une fois la règle typographique ajoutée, une trentaine d'autres : les libellés `ERREUR :`, `Top :`, `Historique des scans :`, cinq lignes du relevé d'espace disque, les séparateurs de liste `" ; "`, les catégories du tableau des mises à jour, et un en-tête de tableau resté entier dans le rapport.

Deux autres classes de défauts ont leur propre test. **Les formats de date écrits en dur** : `dd/MM` était présent à dix-neuf endroits, ce qui donnait des dates françaises dans presque tous les tableaux du rapport anglais. Le format se décide désormais dans `Lang` — ISO en anglais, et non le `jj/mm/aaaa` britannique qu'un lecteur américain lit à l'envers sans s'en apercevoir. **Le XAML écrit entre balises** : un libellé peut être un attribut `Text="…"` ou le contenu d'un élément, et la fenêtre Réseau portait la seconde forme, invisible au premier contrôle.

Un second test produit le rapport **rendu** en anglais et le relit : c'est le seul moyen de vérifier les tables exemptées du contrôle de source, dont les descriptions de codes d'arrêt et la base de 59 pilotes. Un contrôle positif l'accompagne — le même rapport en français doit bien contenir du français — sans quoi un générateur renvoyant une page vide passerait les deux sans que personne s'en aperçoive.

Enfin, la suite ne tourne plus en parallèle. La langue est un état global ; des tests qui la basculent et des tests qui affirment du texte français, exécutés en même temps, produisent des échecs intermittents sans rapport avec ce qu'ils vérifient. La suite entière tourne en moins de deux secondes : la sérialisation ne coûte rien.

La suite compte **250 tests**.

## Détails de mise en forme

Les dates suivent la langue : `jj/mm/aaaa` en français, la forme ISO `aaaa-mm-jj` en anglais. Les décimales, l'espace avant le `%` et les guillemets — `« »` contre `“ ”` — suivent la même règle. Les codes d'arrêt, les noms de fichiers `.sys`, les valeurs brutes rendues par Windows et les modèles de matériel ne sont jamais traduits : ce sont des identifiants, pas du texte.

## Ce que cette version ne fait pas

**L'installateur reste en français.** C'est un choix, pas un oubli : il est bâti avec WiX et sa localisation est un chantier distinct de celui de l'application.

**Un rapport garde la langue dans laquelle il a été produit.** Changer de langue redémarre l'application ; les fichiers HTML déjà écrits ne sont pas retraduits, et ne peuvent pas l'être : seul le résumé d'un scan est conservé, pas les données complètes. Les régénérer suppose de persister le rapport entier — utile aussi pour rejouer un vieux scan avec un générateur plus récent, donc traité comme une fonctionnalité à part.

**Les conclusions critiques du résumé de scan restent stockées en clair.** La console de parc les réaffiche telles quelles : une console anglaise interrogeant des postes français y lira des titres français. Le correctif suppose de donner un identifiant stable aux 32 conclusions du moteur de règles — un vocabulaire écrit dans des fichiers relus par d'autres versions, donc un contrat, pas un rangement. Reporté en 1.4.

**Aucune nouvelle fonction de diagnostic.** Le triage RAW, la pente SMART, le bloc winget et la surveillance de l'espace disque restent prévus pour la 1.4.
