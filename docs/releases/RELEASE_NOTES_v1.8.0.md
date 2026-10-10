## In English

**This release makes the report connect what it already knows.** FaultTracePC collected the right data but too often listed it without drawing the link a technician would draw: a "storage" verdict next to a drive measured perfectly healthy, seven crashes in two clear waves presented as a flat list, fourteen drivers installed the very day the crashes resumed, an antivirus "running" that was in fact standing aside. Every change below comes from one real report on an unknown machine, re-read line by line.

The guiding rule of this version: **say only what was measured, and say when something could not be measured.** Several of the corrections below are cases where the software — or the person re-reading its report — concluded further than the data allowed.

The notes below are in French. The full English description of the software is here: **[FaultTracePC — diagnose, monitor and repair a Windows PC](https://palisser.fr/spip.php?article31)**

| File | For whom |
|---|---|
| `FaultTracePC-1.8.0.msi` | Classic installation, or Group Policy deployment |
| `FaultTracePC-1.8.0-portable.zip` | No installation: unzip and run |

The `Source code` archives are generated automatically by GitHub: they contain the code, not the compiled software.

Set the language at install time: `msiexec /i FaultTracePC-1.8.0.msi FTPCLANG=en /qn`

**These files are not digitally signed.** Windows will show "Unknown publisher" — click *More info* then *Run anyway*. Checking the SHA-256 fingerprint at the bottom of this page is the only way to be sure the file you downloaded is the one published here.

---

Un rapport de diagnostic sur une machine inconnue a été relu ligne à ligne. Il contenait presque tout ce qu'il fallait pour comprendre la panne — et ne faisait presque aucun des rapprochements qu'un technicien aurait faits. Cette version apprend au rapport à **rapprocher ce qu'il sait**, et à dire ce qu'il n'a pas pu mesurer.

## Un verdict confronté à ses propres mesures — point 82

Un écran bleu « stockage » ne dit pas que le **disque** est usé. Quand les codes d'arrêt désignent le stockage, le rapport regarde désormais les compteurs de santé (SMART) du disque système :

- **rien d'autre ne pointe vers le stockage** et le disque est sain → la confiance tombe, et le rapport dit : « la mesure ne le confirme pas — ne pas remplacer le disque sur la seule foi de ces codes » ;
- **d'autres alertes de stockage existent** (réinitialisations du contrôleur, par exemple) → la piste tient, mais le rapport précise que le disque lui-même ne montre rien, et renvoie vers ces alertes pour départager disque, liaison et contrôleur.

Même principe pour la **mémoire** : un écran bleu « mémoire » est confronté au diagnostic mémoire de Windows, s'il a été lancé. Sans erreur, la confiance tombe ; jamais lancé, le rapport dit « la mémoire n'a pas été testée ». Le verdict reçoit la même nuance.

Au passage, un défaut de lecture corrigé : sur un diagnostic mémoire en échec, l'un des deux événements que Windows écrit était lu comme un succès.

## Les plantages regroupés dans le temps — points 83 et 84

Sept écrans bleus, trois en juillet puis quatre en septembre, séparés de 74 jours sans rien : le rapport les listait. Il les regroupe désormais en **séries**, donne l'écart entre elles, et pose la question qui compte : **qu'est-ce qui a changé juste avant que les plantages reprennent ?**

Il y répond en partie lui-même : les **pilotes** et les **logiciels** dont la date tombe le jour du premier plantage d'une série, ou dans les deux jours qui précèdent, sont nommés. Sur la machine étudiée, quatorze pilotes du même éditeur portaient la date exacte du premier plantage de septembre. Le rapport le présente comme une **piste à vérifier, jamais comme une preuve** — une date de fichier n'est pas une cause — et donne le moyen de la confirmer.

## Quel antivirus protège réellement la machine — point 81

À la seule liste des processus, deux antivirus semblaient tourner ensemble. C'était faux : l'un des deux était en retrait, sans protection en temps réel. Le rapport **lit** désormais l'état de Windows Defender et les antivirus déclarés au Centre de sécurité de Windows, dans une carte « Protection antivirus », et en tire trois conclusions — chacune seulement si les deux lectures ont réussi :

- **deux antivirus surveillent en même temps** ;
- **aucune protection en temps réel** ;
- **antivirus mal désinstallé** : déclaré à Windows, mais son programme n'existe plus. Cas réel : un antivirus déclaré « actif » alors qu'il avait été retiré — il aurait fait compter deux protections là où il n'y en avait qu'une.

## Ce qui reste après une désinstallation — point 85

- **Services et pilotes inscrits dont le programme n'existe plus** : Windows les connaît encore, leur fichier a disparu. Le rapport les nomme, avec une mise en garde : ne pas effacer une inscription à la main sans savoir à quoi elle sert.
- **Pilotes-filtres** — ce qui s'intercale entre Windows et les fichiers (antivirus, sauvegarde, chiffrement) : la pile complète, avec l'éditeur de chacun, ceux qui ne sont pas de Microsoft en gras. C'est ce qui permet d'écarter, ou de désigner, un reste d'ancien antivirus.
- **Logiciels installés** : la liste complète, repliée en bas du rapport. Dans les conclusions, seuls apparaissent ceux qui servent au diagnostic.

Ce qui est volontairement laissé de côté : les **dossiers** restés sur le disque. Les rattacher à un logiciel disparu demanderait de deviner d'après leur nom.

## Un conseil seulement sur un réglage lu — point 87

Le rapport recommandait de « corriger » le réglage des fichiers de plantage sur une machine où il était resté celui d'origine de Windows. Il **lit** désormais ce réglage et la gestion du fichier d'échange, les affiche dans une carte « Fichiers de plantage », et ne recommande un changement que si l'un des deux est réellement en cause. Réglages d'origine : « ce n'est pas un réglage à corriger ».

## Pourquoi le pilote fautif n'est pas nommé — point 80

Quand WinDbg est installé mais que Windows refuse de le lancer, le rapport ne dit plus « installer WinDbg » sur chaque écran bleu : il dit une fois ce qui s'est passé, et la fenêtre qui suit l'analyse ne propose plus de réinstaller la même version. Constaté sur une machine ; sur une autre, la même version de WinDbg se lance normalement — le rapport ne généralise pas.

## La charge processeur absente de la boîte noire — point 86

Sur une machine, la colonne « CPU % » de la boîte noire était vide de bout en bout. La cause n'est **pas établie**. Plutôt que de corriger au hasard, le service de surveillance enregistre désormais, à chaque démarrage, ce que ses capteurs voient ; et le rapport écrit « charge processeur non mesurée — un tiret n'est pas un zéro », avec cette raison. La prochaine machine concernée dira la cause.

## Les indications de la boîte à outils

Des cartes affichaient « sfc /scannow, puis DISM » — qui réparent les fichiers de Windows — sous des constats sans rapport : un antivirus mal désinstallé, un fichier de plantage non écrit. Elles n'annoncent plus d'outil qu'aucun bouton ne traite.

## Ce qui ne change pas

- **Le mode parc reste inclus**, comme en 1.7.1, et une mise à jour depuis la 1.7.1 le conserve : vérifié avant publication sur un poste en mode parc, qui a gardé son réglage, est resté joignable depuis la console et a rouvert sa boîte noire à distance. Le rendre facultatif à l'installation est prévu pour la 1.9.0, avec l'exigence qu'aucun poste déjà en mode parc ne le perde.
- Les analyses enregistrées par les versions précédentes restent lisibles : les nouvelles informations s'ajoutent, aucune n'est retirée.

815 tests, aucun échec.
