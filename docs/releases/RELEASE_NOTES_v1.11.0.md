## In English

**The flight recorder now records two more readings: the processor's frequency and its power draw.** The background monitoring service writes them every 10 seconds, when the processor exposes those sensors, and the report shows them in "Last seconds before the incident". A dash means "not measured", never zero. This is the first step towards telling a processor slowed down by heat apart from one that is simply busy: this release measures, it does not conclude yet.

**An external drive in trouble is handled with more care.** The repair script now scans only the system drive: an external drive that returns errors must be copied first, not put under load. And for a USB drive, the health that Windows reports is shown as "not measurable through USB": through a USB enclosure, Windows usually does not read the drive's own health.

The notes below are in French. The full English description of the software is here: **[FaultTracePC — diagnose, monitor and repair a Windows PC](https://palisser.fr/spip.php?article31)**

| File | For whom |
|---|---|
| `FaultTracePC-1.11.0.msi` | Classic installation, or Group Policy deployment |
| `FaultTracePC-1.11.0-portable.zip` | No installation: unzip and run |

The `Source code` archives are generated automatically by GitHub: they contain the code, not the compiled software.

Set the language at install time: `msiexec /i FaultTracePC-1.11.0.msi FTPCLANG=en /qn`

Fleet console at install time: `msiexec /i FaultTracePC-1.11.0.msi /qn ADDLOCAL=Main,ParkFeature` (or `ADDLOCAL=ALL`).

**These files are not digitally signed.** Windows will show "Unknown publisher" — click *More info* then *Run anyway*. Checking the SHA-256 fingerprint at the bottom of this page is the only way to be sure the file you downloaded is the one published here.

---

Deux changements : la boîte noire enregistre deux mesures de plus, et un disque externe en difficulté est traité avec plus de précaution. L'installation est la même qu'en 1.10.0.

## La fréquence et la consommation du processeur dans la boîte noire — point 120

La **boîte noire**, c'est le service de surveillance qui tourne en arrière-plan sur chaque poste : toutes les 10 secondes, il note l'état de la machine (charge et température du processeur et de la carte graphique, mémoire, plus gros processus). Quand un incident survient, le rapport montre les dernières secondes qui l'ont précédé.

Elle note désormais **deux mesures de plus** :

- **la fréquence du processeur**, c'est-à-dire la vitesse à laquelle il tourne à cet instant — la plus haute de ses cœurs, affichée en GHz. La fréquence du « bus », une horloge de référence d'environ 100 MHz qui ne dit rien de l'effort du processeur, est écartée ;
- **sa consommation électrique**, en watts — celle de la puce entière. Les consommations cœur par cœur ne sont jamais additionnées : leur somme oublierait le reste de la puce.

Dans le rapport, le tableau « Dernières secondes avant l'incident » gagne deux colonnes : **« Fréquence CPU »** et **« Puissance CPU »**, à côté de la température du processeur.

**Pourquoi.** Un processeur trop chaud baisse lui-même sa fréquence pour se protéger : on dit qu'il est « bridé ». Le poste devient lent sans rien afficher. Jusqu'ici, FaultTracePC voyait un processeur chargé et chaud, mais ne pouvait pas distinguer un processeur bridé d'un processeur simplement occupé. **Cette version mesure, elle ne conclut pas encore.** La conclusion « processeur bridé par la chaleur » viendra dans une version suivante, avec des seuils fixés sur des mesures relevées sur de vrais postes plutôt que devinés — notamment pour ne pas confondre avec un portable qui baisse sa fréquence pour économiser sa batterie.

**Un tiret n'est pas un zéro.** Quand une mesure manque, la case affiche un tiret. Quand elle manque sur tout le tableau, une note l'explique : « Fréquence et consommation du processeur non mesurées : un tiret n'est pas un zéro. » Elle ajoute, quand le service les a notés à son démarrage, le nom des capteurs qu'il a trouvés — ou « aucun ». Ces noms varient selon la marque du processeur : ils serviront à régler la future conclusion. Une valeur invraisemblable (nulle, négative, au-delà de 7 000 MHz ou de 500 W) n'est pas retenue.

## Un disque externe en difficulté — point 119

Constaté le 03/11/2026 sur un vrai disque externe devenu illisible : FaultTracePC l'a bien signalé en temps réel, et la carte du rapport conseillait à juste titre de le recopier d'abord. Mais trois choses contredisaient ce conseil.

- **Le script de réparation n'examine plus que le disque système.** Il lançait un examen (`Repair-Volume -Scan`) sur chaque disque ayant une lettre, le disque externe en panne compris. Or un disque qui lâche doit être recopié avant toute autre lecture. Le script le dit désormais : « Les autres disques ne sont pas examinés : un disque externe qui renvoie des erreurs doit d'abord être recopié, pas sollicité. » La boîte à outils et la réparation guidée, elles, n'examinaient déjà que le disque système.
- **« Sain » à travers l'USB n'est plus présenté comme une garantie.** La fiche « Disques physiques » affichait « santé : Sain » pour ce disque, au moment même où Windows enregistrait des centaines de blocs défectueux. À travers un boîtier USB, l'état de santé que Windows rapporte n'est le plus souvent pas lu sur le disque. Le rapport écrit maintenant : « non mesurable à travers l'USB (Windows indique « Sain ») ». Un état mauvais reste affiché tel quel ; les disques internes ne changent pas.
- **Le conseil des alertes « Erreur disque signalée par Windows »** recommandait à tout disque de mettre à jour le firmware du SSD. Il dit maintenant : « Sauvegarder les données importantes sans attendre, et vérifier la santé SMART du disque et ses câbles. Disque externe : le recopier avant tout, sans lancer de réparation dessus. SSD interne : vérifier aussi son firmware chez le fabricant. » Les alertes déjà archivées affichent le nouveau texte.

## Limites, dites franchement

- **Rien pour le passé.** La fréquence et la consommation ne sont enregistrées qu'à partir de l'installation de la 1.11.0. Un incident plus ancien n'en aura jamais : le rapport le dit.
- **Tous les processeurs n'exposent pas ces capteurs.** Quand ils manquent, le rapport le dit et nomme ce que le service a trouvé.
- **Pas encore de conclusion** sur un processeur bridé : voir plus haut.

## Ce qu'il faut mettre à jour, et où

Tous les postes où FaultTracePC est installé : c'est le service de surveillance de chaque poste qui enregistre les nouvelles mesures. Sur un parc, la console le fait par la sélection.

## Ce qui ne change pas

- L'installation est la même qu'en 1.10.0. La case « Mode parc » garde le choix fait à l'installation précédente.
- Rappel : une liste `ADDLOCAL` installe exactement ce qu'elle nomme. `ADDLOCAL=Main` installe **sans** la console, même en mise à jour ; pour la garder, `ADDLOCAL=Main,ParkFeature`.
- La case « Mises à jour des logiciels (winget) » de la 1.10.0 fonctionne comme avant.
- FaultTracePC demande un Windows 10 ou 11 à jour.
- Les analyses enregistrées par les versions précédentes restent lisibles.
