## In English

**The flight recorder now records two more readings: the processor's frequency and its power draw.** The background monitoring service writes them every 10 seconds, and the report shows them in "Last seconds before the incident". A dash means "not measured", never zero. This is the first step towards telling a processor slowed down by heat apart from one that is simply busy: this release measures, it does not conclude yet.

**These readings — and the processor temperature — need the PawnIO driver, which FaultTracePC does not install.** Measured on a real computer: without it, the sensors exist but stay empty; with it, they are read. On a computer without PawnIO, FaultTracePC has never read the processor temperature. The report now says so, and gives the command to install it on one computer.

**An external drive in trouble is handled with more care.** The repair script now scans only the system drive: an external drive that returns errors must be copied first, not put under load. And for a USB drive, the health that Windows reports is shown as "not measurable through USB": through a USB enclosure, Windows usually does not read the drive's own health.

**Rebuilt on .NET 10.0.15**, Microsoft's security update of 10 November 2026. FaultTracePC ships with its own copy of .NET, which Windows Update never patches: only a rebuild does.

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

Deux changements : la boîte noire enregistre deux mesures de plus — et dit enfin pourquoi les mesures du processeur manquent sur un poste sans le pilote PawnIO —, et un disque externe en difficulté est traité avec plus de précaution. L'installation est la même qu'en 1.10.0.

## La fréquence et la consommation du processeur dans la boîte noire — point 120

La **boîte noire**, c'est le service de surveillance qui tourne en arrière-plan sur chaque poste : toutes les 10 secondes, il note l'état de la machine (charge et température du processeur et de la carte graphique, mémoire, plus gros processus). Quand un incident survient, le rapport montre les dernières secondes qui l'ont précédé.

Elle note désormais **deux mesures de plus** :

- **la fréquence du processeur**, c'est-à-dire la vitesse à laquelle il tourne à cet instant — la plus haute de ses cœurs, affichée en GHz. La fréquence du « bus », une horloge de référence d'environ 100 MHz qui ne dit rien de l'effort du processeur, est écartée ;
- **sa consommation électrique**, en watts — celle de la puce entière. Les consommations cœur par cœur ne sont jamais additionnées : leur somme oublierait le reste de la puce.

Dans le rapport, le tableau « Dernières secondes avant l'incident » gagne deux colonnes : **« Fréquence CPU »** et **« Puissance CPU »**, à côté de la température du processeur.

**Pourquoi.** Un processeur trop chaud baisse lui-même sa fréquence pour se protéger : on dit qu'il est « bridé ». Le poste devient lent sans rien afficher. Jusqu'ici, FaultTracePC voyait un processeur chargé et chaud, mais ne pouvait pas distinguer un processeur bridé d'un processeur simplement occupé. **Cette version mesure, elle ne conclut pas encore.** La conclusion « processeur bridé par la chaleur » viendra dans une version suivante, avec des seuils fixés sur des mesures relevées sur de vrais postes plutôt que devinés — notamment pour ne pas confondre avec un portable qui baisse sa fréquence pour économiser sa batterie.

## Ces mesures demandent un pilote : PawnIO

**Mesuré sur un poste réel le 08/11/2026.** La bibliothèque qui lit les capteurs (LibreHardwareMonitor) passe par un **pilote**, PawnIO, pour lire la température, la fréquence et la consommation du processeur — et elle ne l'installe pas. Sans lui, les capteurs existent mais restent vides ; une fois PawnIO installé, les trois mesures sont lues dès le premier relevé.

**Cela vaut aussi pour la température du processeur, et pas seulement depuis cette version.** Sur un poste sans PawnIO, FaultTracePC ne l'a jamais lue, et les alertes « Température du processeur élevée » ne pouvaient donc pas se déclencher. La température et la charge de la carte graphique, la charge du processeur et la mémoire ne sont pas concernées : elles passent par d'autres chemins.

**FaultTracePC n'installe pas PawnIO.** Un pilote tourne au cœur de Windows, avec tous les droits : l'installer est une décision d'administrateur. Pour un poste, dans PowerShell en administrateur :

```
winget install --id namazso.PawnIO -e
Restart-Service FaultTracePCMonitor
```

La seconde commande redémarre la surveillance : elle ne voit le pilote qu'à son démarrage (redémarrer le poste fait la même chose).

**Le rapport dit désormais la vraie raison.** Le service note à son démarrage si PawnIO est présent. Quand une mesure du processeur manque — température, fréquence ou consommation —, la case affiche un tiret, et une note sous le tableau dit pourquoi : pilote absent (avec la commande ci-dessus), pilote installé mais arrêté, ou pilote présent mais mesure non fournie par ce processeur. Elle ajoute le nom des capteurs que le service a trouvés ; ces noms varient selon la marque du processeur et serviront à régler la future conclusion. Sous « Températures dans la durée », quand il n'y a pas de ligne pour le processeur faute de PawnIO, le rapport le dit aussi, et rappelle que les alertes de température du processeur ne peuvent pas se déclencher. Un tiret n'est pas un zéro. Une valeur invraisemblable (nulle, négative, au-delà de 7 000 MHz ou de 500 W) n'est pas retenue.

## Un disque externe en difficulté — point 119

Constaté le 03/11/2026 sur un vrai disque externe devenu illisible : FaultTracePC l'a bien signalé en temps réel, et la carte du rapport conseillait à juste titre de le recopier d'abord. Mais trois choses contredisaient ce conseil.

- **Le script de réparation n'examine plus que le disque système.** Il lançait un examen (`Repair-Volume -Scan`) sur chaque disque ayant une lettre, le disque externe en panne compris. Or un disque qui lâche doit être recopié avant toute autre lecture. Le script le dit désormais : « Les autres disques ne sont pas examinés : un disque externe qui renvoie des erreurs doit d'abord être recopié, pas sollicité. » La boîte à outils et la réparation guidée, elles, n'examinaient déjà que le disque système.
- **« Sain » à travers l'USB n'est plus présenté comme une garantie.** La fiche « Disques physiques » affichait « santé : Sain » pour ce disque, au moment même où Windows enregistrait des centaines de blocs défectueux. À travers un boîtier USB, l'état de santé que Windows rapporte n'est le plus souvent pas lu sur le disque. Le rapport écrit maintenant : « non mesurable à travers l'USB (Windows indique « Sain ») ». Un état mauvais reste affiché tel quel ; les disques internes ne changent pas.
- **Le conseil des alertes « Erreur disque signalée par Windows »** recommandait à tout disque de mettre à jour le firmware du SSD. Il dit maintenant : « Sauvegarder les données importantes sans attendre, et vérifier la santé SMART du disque et ses câbles. Disque externe : le recopier avant tout, sans lancer de réparation dessus. SSD interne : vérifier aussi son firmware chez le fabricant. » Les alertes déjà archivées affichent le nouveau texte.

## Construite sur .NET 10.0.15 — mise à jour de sécurité du 10/11/2026

FaultTracePC embarque **sa propre copie de .NET**, le moteur qui le fait tourner, et Windows Update ne la met jamais à jour : seule une reconstruction le fait (voir les notes de la 1.9.3). Microsoft a publié le 10/11/2026 la mise à jour de sécurité mensuelle de .NET 10, la **10.0.15**. Cette version est construite dessus, et ses bibliothèques Microsoft (`System.Management`, `System.Diagnostics.EventLog`, `System.DirectoryServices`, `Microsoft.Extensions.Hosting.WindowsServices`) passent de 10.0.14 à 10.0.15.

Parmi les failles corrigées ce jour-là, quatre touchent .NET lui-même (deux élévations de privilèges, deux dénis de service) ; les autres concernent ASP.NET Core et Visual Studio, que FaultTracePC n'utilise pas. L'annonce de Microsoft n'en donne que le titre : elle ne permet pas de savoir si FaultTracePC y était exposé. C'est la raison de ne pas attendre.

## Limites, dites franchement

- **Rien pour le passé.** La fréquence et la consommation ne sont enregistrées qu'à partir de l'installation de la 1.11.0. Un incident plus ancien n'en aura jamais : le rapport le dit.
- **Sans PawnIO, rien de nouveau** : ni température, ni fréquence, ni consommation du processeur. Le rapport le dit, poste par poste.
- **Sur un parc, FaultTracePC ne déploie pas PawnIO**, et la commande ci-dessus ne convient pas à un déploiement par stratégie de groupe : winget n'est pas utilisable sous le compte système de Windows (mesuré le 02/11/2026). La manière de l'installer sur un parc n'est pas encore documentée ; une fois la 1.11.0 déployée, les rapports des postes diront où il manque.
- **Même avec PawnIO**, un processeur peut ne pas fournir l'une de ces mesures à la bibliothèque : le rapport le dit alors sous cette raison.
- **Pas encore de conclusion** sur un processeur bridé : voir plus haut.

## Ce qu'il faut mettre à jour, et où

Tous les postes où FaultTracePC est installé : c'est le service de surveillance de chaque poste qui enregistre les nouvelles mesures. Sur un parc, la console le fait par la sélection. Pour que les mesures du processeur soient lues, il faut **en plus** PawnIO sur le poste (voir plus haut).

## Ce qui ne change pas

- L'installation est la même qu'en 1.10.0. La case « Mode parc » garde le choix fait à l'installation précédente.
- Rappel : une liste `ADDLOCAL` installe exactement ce qu'elle nomme. `ADDLOCAL=Main` installe **sans** la console, même en mise à jour ; pour la garder, `ADDLOCAL=Main,ParkFeature`.
- La case « Mises à jour des logiciels (winget) » de la 1.10.0 fonctionne comme avant.
- FaultTracePC demande un Windows 10 ou 11 à jour.
- Les analyses enregistrées par les versions précédentes restent lisibles.
