## In English

Fleet management, made deployable. A single **master secret** now replaces the list of per-machine tokens the console used to keep in a plain file: each machine derives its own token from that secret and its Windows name, and the console recomputes it — nothing to copy, nothing to back up. `remote.json`, which holds a machine's token, is no longer readable by every user of the PC. And the PowerShell windows opened by the three repair buttons **close again when you press Enter**, which the software had been promising, wrongly, since 1.3.1.

The notes below are in French. The full English description of the software is here: **[FaultTracePC — diagnose, monitor and repair a Windows PC](https://palisser.fr/spip.php?article31)**

| File | For whom |
|---|---|
| `FaultTracePC-1.5.0.msi` | Classic installation, or Group Policy deployment |
| `FaultTracePC-1.5.0-portable.zip` | No installation: unzip and run |

The `Source code` archives are generated automatically by GitHub: they contain the code, not the compiled software.

Set the language at install time: `msiexec /i FaultTracePC-1.5.0.msi FTPCLANG=en /qn`

**These files are not digitally signed.** Windows will show "Unknown publisher" — click *More info* then *Run anyway*. Checking the SHA-256 fingerprint at the bottom of this page is the only way to be sure the file you downloaded is the one published here.

---

Une version à thème unique : **le parc**. Plus une correction qui, elle, concerne tout le monde.

## Un seul secret au lieu d'une liste de jetons

Jusqu'ici, chaque poste en mode Client tirait un jeton au hasard, qu'il fallait recopier à la main dans la console. La console conservait la liste complète — un jeton par machine, en clair — dans `Documents\FaultTracePC\parc.json`. Trois conséquences, dont aucune n'était acceptable pour un déploiement d'établissement :

- aucun export possible, donc un profil Windows reconstruit effaçait la configuration de tout le parc ;
- sur un poste dont le dossier Documents est redirigé — cas courant en établissement — **les jetons de toutes les machines atterrissaient sur un partage réseau** ;
- et chaque nouveau poste demandait un copier-coller de 64 caractères.

Désormais, un **secret maître** unique est produit une fois :

```
FaultTracePC.Cli.exe --generate-master-secret
```

Chaque poste en déduit **son** jeton, à partir de ce secret et de son nom Windows (HMAC-SHA256). La console fait le même calcul quand elle interroge une machine. Il n'y a donc plus de liste à conserver, à sauvegarder, ni à protéger.

**Le poste ne connaît jamais le secret maître.** Il le reçoit le temps d'une commande, en déduit son jeton, et n'écrit que celui-là. Un secret laissé sur chaque poste aurait offert le parc entier à qui ouvre un seul poste.

Configuration sans interface, donc déployable par stratégie de groupe :

```
FaultTracePC.Cli.exe --configure-remote --master-secret - --port 58620
```

La valeur `-` lit le secret sur l'entrée standard : passé en argument, il serait visible dans la liste des processus le temps de l'exécution — acceptable pour une commande tapée à la main, à éviter dans un script partagé.

**Sur la console**, le secret est enregistré chiffré par Windows (DPAPI) pour ton seul compte, dans `%LOCALAPPDATA%` — que les profils itinérants ne déplacent pas. Il est illisible depuis une autre session et sur une autre machine, même copié. Un bouton **Oublier** le retire ; les postes, eux, ne sont pas touchés.

**Les postes déjà configurés continuent de fonctionner.** Le champ « jeton » de la console reste disponible : un jeton inscrit l'emporte sur la dérivation. La migration se fait poste par poste, quand tu repasses dessus.

**Un point de vigilance, et il est réel :** le jeton se calcule à partir du **nom Windows** du poste — celui que renvoie `hostname`. Le champ « Nom » de la console servait jusqu'ici de libellé libre ; un libellé de fantaisie produit maintenant un refus. D'où trois garde-fous : le nom devient obligatoire quand le jeton est déduit, la fenêtre « Mode réseau » affiche le nom exact à recopier, et le message de refus nomme les trois causes possibles — jeton, nom Windows, horloge décalée.

## `remote.json` n'est plus lisible par tout le monde

Ce fichier porte le jeton de la machine. Il vit dans `C:\ProgramData\FaultTracePC`, dont les permissions par défaut laissent le groupe Utilisateurs lire ce qui s'y trouve : sur un poste partagé, n'importe quelle session pouvait l'ouvrir.

L'héritage est désormais coupé et l'accès réduit à **SYSTEM et aux administrateurs**. Rien ne casse : le service de surveillance tourne en LocalSystem, et l'application comme la ligne de commande s'exécutent déjà en administrateur. La fenêtre « Mode réseau » affiche l'état réel du fichier, relu sur le disque — pas une promesse.

## Les fenêtres PowerShell se ferment de nouveau

Depuis la 1.3.1, une fenêtre de réparation ne se refermait **plus jamais** d'elle-même. Le script se terminait pourtant par « Appuyer sur Entrée pour fermer » : appuyer sur Entrée déposait l'utilisateur sur une invite PowerShell. **Le logiciel écrivait une phrase fausse** — exactement la classe de défaut qu'il corrige ailleurs.

L'option `-NoExit` avait été ajoutée pour une bonne raison : sans elle, une console refusée par une stratégie de groupe s'évaporait avant qu'on ait pu lire le refus. Elle est remplacée par un enrobage qui garde les deux qualités et perd le défaut : la pause est garantie par un `finally` **quand le script ne va pas au bout**, et la fenêtre se ferme vraiment quand il y va. Les trois boutons concernés — « Lancer la réparation », la boîte à outils et l'assistant guidé — se comportent maintenant de la même façon.

La stratégie d'exécution n'est toujours pas contournée : elle refuse le fichier de script, et son refus s'affiche au lieu de disparaître en un clin d'œil.

*Reste à faire : le lanceur `.bat` engendré à côté du script porte encore `-NoExit`.*

## Une option inconnue n'est plus ignorée en silence

Constaté en préparant cette version : `FaultTracePC.Cli.exe --une-option-qui-n-existe-pas` ne refusait rien. L'option était ignorée, le programme lançait une analyse complète de trente jours, et rendait **0** — donc annonçait un succès.

Sur une machine, c'est trois minutes perdues. Dans un script déployé par stratégie de groupe, une faute de frappe dans `--configure-remote` aurait analysé **tout un parc** au lieu de le configurer, sans que rien ne le signale. Toute option inconnue est maintenant nommée et refusée avec le code 3.

## Mise à jour

Le MSI remplace proprement la 1.4.1. Aucun changement de format de fichier ni de protocole de parc : une console en 1.5.0 interroge un poste resté en 1.4 sans difficulté, à condition que ce poste garde son jeton inscrit dans la console.
