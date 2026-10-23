## In English

**This release adds one option and fixes one verdict.** A machine whose virtual memory had filled up was given the verdict "SOFTWARE (system corruption or an application)" and was offered sfc and DISM, which repair Windows files, not a program that uses up all the memory. It now gets its own verdict, "MEMORY EXHAUSTION". And the toolbox can now export the raw Windows logs (.evtx) for the person of your choice — an option that does nothing until you click it, with plain warnings about what these files contain.

The notes below are in French. The full English description of the software is here: **[FaultTracePC — diagnose, monitor and repair a Windows PC](https://palisser.fr/spip.php?article31)**

| File | For whom |
|---|---|
| `FaultTracePC-1.9.2.msi` | Classic installation, or Group Policy deployment |
| `FaultTracePC-1.9.2-portable.zip` | No installation: unzip and run |

The `Source code` archives are generated automatically by GitHub: they contain the code, not the compiled software.

Set the language at install time: `msiexec /i FaultTracePC-1.9.2.msi FTPCLANG=en /qn`

Fleet console at install time: `msiexec /i FaultTracePC-1.9.2.msi /qn ADDLOCAL=Main,ParkFeature` (or `ADDLOCAL=ALL`).

**These files are not digitally signed.** Windows will show "Unknown publisher" — click *More info* then *Run anyway*. Checking the SHA-256 fingerprint at the bottom of this page is the only way to be sure the file you downloaded is the one published here.

---

Une correction de verdict, trouvée sur un diagnostic réel en 1.9.1, et une option nouvelle dans la boîte à outils. L'installation est la même qu'en 1.9.0 et 1.9.1.

## Une mémoire saturée n'est pas une corruption de Windows — point 115

Sur un poste dont la mémoire virtuelle avait été saturée à 99 %, le verdict disait « Cause la plus probable : LOGICIELLE (corruption système ou application) », et la boîte à outils proposait sfc /scannow puis DISM. Ces deux outils réparent des fichiers de Windows : ils ne peuvent rien contre un programme qui consomme toute la mémoire.

Ces conclusions ont désormais leur propre famille, **« Ressources »** :

- le verdict dit **« SATURATION DE LA MÉMOIRE (un programme qui consomme trop, ou un fichier d'échange trop petit — ni une panne de Windows, ni une barrette de RAM) »** ;
- sont concernées : les alertes de mémoire virtuelle saturée, l'épuisement de la mémoire détecté par Windows, la pression mémoire au moment de l'analyse, la mémoire réservée par la virtualisation, et la saturation relevée juste avant un plantage ;
- la boîte à outils ne propose plus sfc ni DISM pour ces cartes, et le script de réparation affiche la configuration du fichier d'échange au lieu de réparer les fichiers de Windows.

## Exporter les journaux de Windows (.evtx), en option — point 116

Dans la boîte à outils, groupe « Consoles Windows utiles » : **« 📤 Exporter les journaux Windows (.evtx) »**. Si personne ne clique, rien ne se passe. Le clic ouvre une fenêtre qui explique avant tout choix :

- **ce n'est pas un diagnostic** : ces fichiers contiennent les événements bruts, sans les écrans bleus, la santé des disques, les températures ni la boîte noire — le rapport de FaultTracePC reste ce qu'il faut envoyer en premier ;
- **à qui les confier** : ce qu'ils contiennent est nommé concrètement — le nom du PC, des noms de comptes et des chemins d'utilisateurs, les logiciels installés et désinstallés, les clés USB branchées, des noms de réseaux, les heures d'utilisation — avec le conseil de ne jamais les déposer sur un forum ou un site public.

Puis on choisit :

- **les journaux** : Système et Application (par défaut), ou le journal **Sécurité**. Celui-ci contient les ouvertures de session de chaque personne, et sur un PC partagé celles des autres : il ne s'exporte qu'après avoir coché une case qui le dit ;
- **la période** : 30 jours (par défaut, comme l'analyse), 90 jours, ou le journal complet — la taille réelle des journaux est alors affichée avant l'export, avec l'avertissement qu'un gros fichier est difficile à envoyer et que des données anciennes servent rarement.

**Rien n'est envoyé.** Les fichiers sont écrits dans un dossier daté de Documents\FaultTracePC, ouvert dans l'Explorateur à la fin. Ils sont accompagnés des textes des messages, pour rester lisibles sur un autre PC : essai fait en ouvrant un export dans l'Observateur d'événements d'une autre machine. « Annuler » est le bouton par défaut.

## Au passage

Un bouton désactivé à fond coloré devenait presque illisible (texte blanc sur fond clair). Corrigé pour le bouton « Exporter » de cette fenêtre et pour le bouton rouge de la confirmation par saisie du nombre de postes, dans la console de parc.

## Ce qu'il faut mettre à jour, et où

- Le point 115 porte sur le **rapport d'un poste** : sur un parc, ce sont les postes qu'il faut mettre à jour, puisqu'un diagnostic à distance est produit par le poste lui-même.
- Le point 116 est une option de la boîte à outils, sur le poste où on l'utilise.

## Ce qui ne change pas

- L'installation est la même qu'en 1.9.0. La case « Mode parc » garde le choix fait à l'installation précédente.
- Rappel : une liste `ADDLOCAL` installe exactement ce qu'elle nomme. `ADDLOCAL=Main` installe **sans** la console, même en mise à jour ; pour la garder, `ADDLOCAL=Main,ParkFeature`.
- FaultTracePC demande un Windows 10 ou 11 à jour.
- Les analyses enregistrées par les versions précédentes restent lisibles.
