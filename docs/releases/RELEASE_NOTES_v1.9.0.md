<!-- BROUILLON commencé le 17/10/2026 — à compléter avec les autres points de la
     1.9.0 (93, 94, 96, 99, 100) et à relire avant de pousser le tag v1.9.0.
     Le travail « Publication » lit ce fichier tel quel : ce commentaire HTML ne
     s'affiche pas dans la release, mais tout le reste, si. -->
## In English

**This release separates the two audiences of FaultTracePC.** Someone diagnosing their own PC no longer sees the fleet console; an administrator gets it with one tick box at installation — and no machine that already had it loses it.

The notes below are in French. The full English description of the software is here: **[FaultTracePC — diagnose, monitor and repair a Windows PC](https://palisser.fr/spip.php?article31)**

| File | For whom |
|---|---|
| `FaultTracePC-1.9.0.msi` | Classic installation, or Group Policy deployment |
| `FaultTracePC-1.9.0-portable.zip` | No installation: unzip and run |

The `Source code` archives are generated automatically by GitHub: they contain the code, not the compiled software.

Set the language at install time: `msiexec /i FaultTracePC-1.9.0.msi FTPCLANG=en /qn`

Fleet console at install time: `msiexec /i FaultTracePC-1.9.0.msi /qn ADDLOCAL=Main,ParkFeature` (or `ADDLOCAL=ALL`).

**These files are not digitally signed.** Windows will show "Unknown publisher" — click *More info* then *Run anyway*. Checking the SHA-256 fingerprint at the bottom of this page is the only way to be sure the file you downloaded is the one published here.

---

## Deux publics, un seul paquet — point 79

Un particulier veut diagnostiquer sa machine ; un administrateur veut piloter un parc. Jusqu'ici, tous deux recevaient la console de parc. Elle devient une **case à cocher de l'installation**, « Mode parc (administration d'un parc) », **non cochée par défaut**.

- **Ce qu'elle change** : l'affichage des boutons **« Parc »** (la console) et **« Réseau »**. Rien d'autre.
- **Ce qu'elle ne change pas** : la surveillance d'un poste. Un poste déjà configuré pour la console reste joignable, que la case soit cochée ou non — sa configuration ne dépend pas de l'installateur.
- **Mise à jour depuis la 1.8.x ou avant** : la case est **cochée d'office**. Personne ne perd la console qu'il avait.
- **Mises à jour suivantes** : le choix fait est conservé, case cochée ou non.
- **Un poste exposé sur le réseau** montre toujours « Réseau », même sans la case : qui s'assoit devant doit pouvoir le voir.
- **La version portable** affiche toujours la console.

**En ligne de commande**, la case s'obtient avec `ADDLOCAL=Main,ParkFeature` ou `ADDLOCAL=ALL`. Attention : une liste `ADDLOCAL` installe **exactement** ce qu'elle nomme — `ADDLOCAL=Main`, forme publiée jusqu'ici pour se passer du raccourci du Bureau, installe donc **sans** la console, même en mise à jour. Le poste reste supervisé ; seuls les deux boutons disparaissent.

**Éprouvé avant publication** sur une machine virtuelle Windows 11 à jour : installation neuve (interactive et silencieuse), ajout de la case, retrait de la case, mise à jour depuis la 1.8.1 (case cochée d'office), mise à jour d'un poste sans la case (elle reste décochée), mise à jour silencieuse avec `ADDLOCAL=Main` d'un poste supervisé (toujours joignable), désinstallation (configuration du poste conservée), version portable.

## Corrigé au passage

- Dans le rapport français, la carte antivirus affichait le mode de Windows Defender en anglais (« mode Not running »). Il est désormais traduit (« arrêté », « passif », « normal »…) ; une valeur inconnue est rendue telle quelle.
- Quand des échecs répétés de services viennent d'un ou deux services seulement — souvent un logiciel tiers —, la carte ne propose plus sfc /scannow et DISM, qui réparent Windows et non ce logiciel. Ils restent proposés quand les échecs se dispersent sur de nombreux services, ce qui désigne plutôt le système.
- Un même écran bleu pouvait être compté deux fois quand le minidump et MEMORY.DMP l'enregistraient sous deux formes du même code (par exemple 0x1000007E et 0x7E) : deux conclusions critiques pour un seul plantage. Les deux formes sont désormais reconnues comme un seul arrêt.
- Quand des services échouent de façon répétée, ceux dont le programme a disparu (restes d'une désinstallation) sont mis à part avant de conclure : ils ne font plus croire à un problème de Windows quand les autres échecs se concentrent sur un ou deux services.
- Les plantages répétés d'un logiciel tiers ne proposent plus sfc /scannow et DISM ; ils restent proposés quand c'est un composant de Windows qui plante.
- Sous un verdict matériel, le rapport ne dit plus « le problème principal vient d'un pilote ».
- Un écran bleu plus ancien que la période analysée ne décide plus du verdict : il reste affiché, avec la mention qu'il est antérieur, et le verdict le signale. Constaté sur un parc : « PILOTE défectueux » pour un unique écran bleu vieux de dix mois, sur une machine saine.
- Un service coupé pendant l'arrêt ou le redémarrage de Windows n'est plus compté comme un échec. Constaté sur un parc : l'agent d'inventaire « s'arrêtait de manière inattendue » chaque soir, toujours dans la même seconde que l'arrêt de Windows programmé — il était coupé, pas en panne.

## Windows à jour

Constaté pendant les essais : sur un Windows 10 de 2019 jamais mis à jour, le service de surveillance s'arrête aussitôt (« Your Windows doesn't fully support CET »). La cause est la protection CET, que les programmes construits avec .NET utilisent depuis .NET 9, et qu'un Windows aussi ancien ne gère pas entièrement. Installer les mises à jour de Windows règle le problème. FaultTracePC demande donc un **Windows 10 ou 11 à jour**.
