# Déployer FaultTracePC sur un parc

État au 05/09/2026, valable à partir de la **1.5.1**. Ce document décrit ce que
le logiciel fait réellement — pas ce qu'il devrait faire. Les limites connues
sont à la fin, et elles sont nommées.

---

## Ce que ce document couvre

Installer FaultTracePC sur plusieurs postes Windows 10/11 par stratégie de
groupe, les rendre visibles depuis une console d'administration, et vérifier que
ça marche **le jour du déploiement** plutôt que le lendemain.

Il ne couvre pas l'usage du logiciel sur un poste isolé : pour ça, le
[pas à pas](https://palisser.fr/spip.php?article32) suffit.

---

## Le principe, en trois phrases

Un **secret maître** est produit une seule fois pour tout le parc et rangé dans
un gestionnaire de mots de passe. Chaque poste en déduit **son** jeton, à partir
du secret et de son nom Windows ; il ne garde que ce jeton et n'apprend jamais le
secret. La console refait le même calcul quand elle interroge un poste, ce qui
supprime toute liste de jetons à conserver.

---

## 1. Ce qui se prépare une seule fois

### Le secret maître

Sur n'importe quelle machine où le logiciel est installé :

```
FaultTracePC.Cli.exe --generate-master-secret
```

Le secret sort sur la **sortie standard**, l'avertissement sur la sortie
d'erreur : `... --generate-master-secret > secret.txt` ne capture donc que le
secret. Ranger la valeur dans un gestionnaire de mots de passe, puis effacer le
fichier.

**Ce secret ne peut pas être retrouvé.** Le perdre oblige à reconfigurer tous les
postes. Le divulguer donne accès à tout le parc.

### Le paquet

`FaultTracePC-1.5.1.msi`, à déposer sur un partage lisible par les ordinateurs du
domaine (droit *Lecture* pour `Ordinateurs du domaine`, pas seulement pour les
utilisateurs — l'installation se fait sous le compte machine).

---

## 2. Sur chaque poste

### Installer

Le paquet s'installe **par machine**, dans `%ProgramFiles%\FaultTracePC`. Il se
déploie donc en **« Attribué à l'ordinateur »**.

```
msiexec /i FaultTracePC-1.5.1.msi /qn
```

Variantes utiles :

| Besoin | Commande |
|---|---|
| Sans raccourci sur le Bureau | `msiexec /i FaultTracePC-1.5.1.msi /qn ADDLOCAL=Main` |
| Avec raccourci | `msiexec /i FaultTracePC-1.5.1.msi /qn ADDLOCAL=Main,DesktopShortcutFeature` |
| Imposer l'anglais au parc | `msiexec /i FaultTracePC-1.5.1.msi FTPCLANG=en /qn` |

`FTPCLANG` accepte `fr`, `en` ou `auto`, et écrit
`C:\ProgramData\FaultTracePC\langue.txt`. C'est un **défaut**, pas une
contrainte : l'utilisateur qui choisit une autre langue dans l'application garde
son choix. Sur une machine déjà installée, `FaultTracePC.Cli.exe
--set-machine-lang en` produit le même résultat.

En mode silencieux, rien n'est lancé à la fin de l'installation.

**Ne jamais faire confiance à `/qn` sans lire son code de sortie.** L'option
supprime toute interface, **y compris les messages d'erreur** : une installation
qui échoue ne dit rien du tout. Constaté le 30/08/2026 sur un poste où le paquet
n'a rien installé sans que rien ne le signale.

```powershell
$p = Start-Process msiexec -ArgumentList '/i','FaultTracePC-1.5.1.msi','/qn','/l*v','C:\Windows\Temp\ftpc-install.log' -Wait -PassThru
if ($p.ExitCode -notin 0,3010) { throw "FaultTracePC : installation echouee ($($p.ExitCode))" }
```

| Code | Sens | Quoi faire |
|---|---|---|
| `0` | installé | — |
| `3010` | installé, redémarrage demandé | rien d'urgent, le service démarrera au redémarrage |
| `1638` | **une autre version est déjà installée** | voir ci-dessous |
| `1603` | échec général | lire le journal `/l*v` |

**Le cas `1638`.** Le `ProductCode` est régénéré à chaque compilation, et une
mise à jour n'est reconnue que pour une version **strictement inférieure** :
réinstaller le MÊME numéro de version par-dessus lui-même est donc refusé. Il
faut désinstaller d'abord :

```powershell
Get-ItemProperty HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*, HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\* |
  Where-Object DisplayName -like 'FaultTracePC*' | Select-Object DisplayName, DisplayVersion, PSChildName
msiexec /x "{PSChildName affiché}" /qn
```

Ne pas employer `Get-CimInstance Win32_Product` pour cette recherche : cette
classe déclenche une reconfiguration de chaque produit installé sur la machine.

Une version **supérieure** remplace proprement l'ancienne, sans désinstallation
préalable : mettre le parc à jour consiste bien à remplacer le paquet dans la
stratégie.

L'installation enregistre et démarre le service **`FaultTracePCMonitor`**, sous le
compte système, en démarrage automatique. La désinstallation l'arrête et le
supprime.

### Configurer le mode parc

```
FaultTracePC.Cli.exe --configure-remote --master-secret - --port 58620
```

La valeur `-` lit le secret sur l'**entrée standard**. Passé en argument, il
serait visible dans la liste des processus le temps de l'exécution : acceptable
pour une commande tapée à la main, à éviter dans un script partagé.

Exemple de script d'ordinateur au démarrage :

```powershell
$secret = Get-Content '\\serveur\Deploiement$\secret.txt' -Raw
$secret | & "$env:ProgramFiles\FaultTracePC\FaultTracePC.Cli.exe" --configure-remote --master-secret - --port 58620
if ($LASTEXITCODE -ne 0) { Write-EventLog -LogName Application -Source 'Application' -EventId 1000 -Message "FaultTracePC : configuration echouee ($LASTEXITCODE)" }
```

Le fichier du secret doit être lisible par les **ordinateurs** du domaine et par
personne d'autre. C'est le point le plus sensible de la procédure.

**L'ordre n'a pas d'importance.** Depuis la 1.5.1, le service relit sa
configuration toutes les 30 secondes : installer puis configurer, ou l'inverse,
aboutit au même résultat en moins d'une minute, sans redémarrage. *(Avant la
1.5.1, un poste configuré après l'installation restait injoignable jusqu'au
redémarrage suivant, sans qu'aucun message ne le signale.)*

### Ouvrir le pare-feu

Depuis la 1.5.1, `--configure-remote` **pose la règle lui-même** et dit ce qu'il a
fait :

```
Règle de pare-feu posée sur le port 58620, limitée aux adresses privées.
```

S'il n'y parvient pas, il l'écrit sur la sortie d'erreur sans faire échouer la
configuration — un poste dont le pare-feu est géré ailleurs fonctionne très bien
— mais il le dit : *« le poste écoutera sans être joignable tant que le port ne
sera pas ouvert »*.

**En domaine, cette règle locale peut être ignorée** selon la configuration du
profil de domaine (« appliquer les règles de pare-feu locales »). La règle doit
alors être poussée par stratégie de groupe, et l'option `--no-firewall` évite
d'en poser une inutile. C'est le cas d'un établissement ; la création locale est
un **secours** pour les parcs hors domaine.

La règle à créer, en entrée :

| Champ | Valeur |
|---|---|
| Protocole | TCP |
| Port local | 58620 (ou celui choisi) |
| Adresses distantes | 127.0.0.1, 10.0.0.0/8, 172.16.0.0/12, 192.168.0.0/16 |
| Action | Autoriser |

La même chose à la main, pour un essai immédiat :

```
netsh advfirewall firewall add rule name="FaultTracePC" dir=in action=allow protocol=TCP localport=58620 remoteip=127.0.0.1,10.0.0.0/8,172.16.0.0/12,192.168.0.0/16
```

Restreindre les adresses distantes n'est pas décoratif : c'est le premier des
deux verrous du mode réseau. Le second est la signature de chaque requête.

---

## 3. Sur la console

La console est une installation ordinaire de FaultTracePC, sur le poste de
l'administrateur. Aucun mode particulier à activer.

1. bouton **🖥 Parc** ;
2. coller le secret maître dans « 🔑 Secret maître », **Enregistrer**. Il est
   chiffré par Windows pour ce compte seulement, dans `%LOCALAPPDATA%` — que les
   profils itinérants ne déplacent pas ;
3. ajouter chaque poste : **Nom** = son *nom Windows* (celui que renvoie
   `hostname`), **Hôte** = son nom réseau ou son IP, **Port** = 58620, **jeton
   laissé vide**.

**Le nom Windows entre dans le calcul du jeton.** Un libellé de fantaisie —
« Salle 3 poste 1 » — produit un jeton faux et un refus sans autre explication.
C'est l'erreur la plus facile à commettre.

Les postes configurés avant la 1.5.1, qui portent un jeton tiré au hasard,
continuent de fonctionner : un jeton inscrit à la main l'emporte sur le calcul.
La migration se fait poste par poste.

---

## 4. Vérifier — le jour même

### Sur le poste

| Quoi | Comment | Attendu |
|---|---|---|
| Le service tourne | `Get-Service FaultTracePCMonitor` | `Running` |
| La configuration est écrite | `Get-Content C:\ProgramData\FaultTracePC\remote.json` | `"Mode": "Client"`, le port choisi, un jeton |
| Le fichier est protégé | `Get-Acl C:\ProgramData\FaultTracePC\remote.json \| Format-List` | SYSTEM et Administrateurs uniquement, héritage désactivé |
| Le port écoute | `netsh http show servicestate \| Select-String 58620` | le préfixe `http://+:58620/` |

**`Get-NetTCPConnection` n'est pas le bon outil ici** : le service passe par
`HttpListener`, donc la réservation appartient à HTTP.SYS au niveau du noyau et
non à un socket ordinaire du processus. Et il faut laisser passer les trente
secondes de relecture avant de conclure quoi que ce soit. **La console qui
répond reste la seule preuve qui compte.**

### Depuis la console

Le poste doit apparaître **joignable** avec son état. Sinon, le message dit
laquelle des trois causes il faut regarder :

| Message | Cause la plus fréquente |
|---|---|
| `refusé (jeton, nom Windows ou horloge décalée ?)` | le nom saisi n'est pas le nom Windows du poste ; ou les horloges diffèrent de plus de 5 minutes |
| `injoignable` | pare-feu, ou le poste est éteint |
| `délai dépassé` | réseau lent, ou le service est occupé par une analyse |
| `secret maître absent` | la console n'a pas de secret enregistré et le poste n'a pas de jeton inscrit |

Les horloges comptent : chaque requête est horodatée, et un écart de plus de
**5 minutes** la fait refuser. Sur un domaine, `w32tm /resync` sur le poste
suffit.

---

## 5. Ce que la console peut faire ensuite

- **Actualiser tout** : état temps réel de chaque poste (charge, températures,
  mémoire, dernier relevé, version installée) ;
- **Rapport du parc** : synthèse consolidée, plus la comparaison entre postes —
  même pilote ancien sur six machines, même code d'arrêt, même modèle de disque
  qui se dégrade. En dessous de deux postes analysés, elle ne produit rien
  plutôt qu'un verdict bâti sur un échantillon ;
- **Diagnostic à distance** : lance une analyse complète sur un poste et rapatrie
  son rapport. Une seule analyse à la fois par poste ;
- **Ouvrir le dernier rapport** d'un poste.

L'API exposée est en **lecture seule**, sauf `/api/scan` qui déclenche une
analyse — une action prédéfinie, sans effet sur le système.

---

## 6. Analyse programmée, sans console

La ligne de commande produit un rapport sans ouvrir de fenêtre. Convient à une
tâche planifiée :

```
FaultTracePC.Cli.exe --quiet --json --days 90 --output \\serveur\Diagnostics$
```

Le dossier accepte un chemin UNC. Les noms de fichier portent le nom de la
machine — `Diagnostic_<MACHINE>_<date>.html` — ce qui permet à tout un parc
d'écrire au même endroit sans collision.

Codes de sortie : **0** machine saine, **1** avertissements, **2** critique,
**3** erreur d'exécution. Une option inconnue rend **3** et refuse de continuer.

---

## 6 bis. Perdre ou changer le secret maître

C'est le seul scénario catastrophique de cette architecture. Il mérite d'être lu
**avant** d'en avoir besoin.

### Où il se trouve

| Quoi | Où | Survit à la désinstallation |
|---|---|---|
| Le secret maître (console) | `%LOCALAPPDATA%\FaultTracePC\parc.secret`, chiffré par Windows pour ce compte | oui — c'est une donnée utilisateur |
| Le jeton d'un poste | `C:\ProgramData\FaultTracePC\remote.json` | oui — c'est une donnée machine |

Désinstaller puis réinstaller ne remet donc **rien** à zéro : un installateur
n'efface pas les données de l'utilisateur.

### Le récupérer, s'il est encore sur la console

Le fichier est chiffré pour **ce compte, sur cette machine** : son propriétaire
peut donc le relire. Dans **Windows PowerShell 5.1** (`powershell.exe`) :

```powershell
Add-Type -AssemblyName System.Security
$blob  = [IO.File]::ReadAllBytes("$env:LOCALAPPDATA\FaultTracePC\parc.secret")
$ent   = [Text.Encoding]::UTF8.GetBytes('FaultTracePC.parc.v1')
$clair = [Security.Cryptography.ProtectedData]::Unprotect($blob, $ent, 'CurrentUser')
[Text.Encoding]::UTF8.GetString($clair)
```

Le ranger aussitôt dans un gestionnaire de mots de passe, puis fermer la console
— il reste sinon dans son historique. Aucun poste n'est à reconfigurer.

### Le changer, dans l'ordre

1. **Console** : fenêtre 🖥 Parc, bouton **Oublier**.
2. `FaultTracePC.Cli.exe --generate-master-secret`, rangé cette fois.
3. **Chaque poste déjà configuré** : `--configure-remote --master-secret -` avec
   le nouveau secret. Un poste oublié devient injoignable et la console dira
   `refusé` — rien n'est cassé sur lui, il attend un jeton qu'on ne calcule plus.
4. **Console** : coller le nouveau secret, Enregistrer.

### Ce qu'on ne peut pas faire aujourd'hui

**Changer le jeton d'un seul poste.** La dérivation est déterministe : recalculer
redonne le même jeton. Pour invalider celui d'une machine — jeton divulgué,
poste sorti du parc — il faut soit changer le secret de tout le parc, soit
inscrire à la main un jeton aléatoire pour cette machine dans la console. Une
révocation par poste reste à concevoir.

---

## 7. Retirer un poste, désinstaller

- **Retirer de la supervision** : bouton *Retirer la sélection* dans la console.
  Rien n'est désinstallé sur le poste, son historique n'est pas touché.
- **Repasser un poste en local** : fenêtre 🌐 *Mode réseau*, cocher **Local**,
  Appliquer. Le service cesse d'écouter dans les 30 secondes.
- **Désinstaller** : `msiexec /x FaultTracePC-1.5.1.msi /qn`. Le service est
  arrêté et supprimé. Les rapports et l'historique restent dans les Documents de
  l'utilisateur — le logiciel n'efface aucune donnée qu'il n'a pas créée pour
  lui-même.

---

## 8. Limites connues, au 05/09/2026

Elles sont écrites ici parce qu'un déploiement se prépare avec la vérité.

- **La règle de pare-feu locale peut être ignorée en domaine** (§ 2). Sur un
  parc d'établissement, c'est la stratégie de groupe qui doit ouvrir le port ;
  la règle posée par `--configure-remote` est un secours pour les parcs hors
  domaine. C'est l'oubli qui fait qu'un parc entier paraît injoignable.
- **Les alertes préventives n'ont pas de destinataire sur une machine sans
  session ouverte.** La notification est une bulle Windows, qui vit dans une
  session utilisateur. Une machine réveillée à distance, ou en attente devant
  l'écran de connexion, produit ses alertes sans que personne les voie. Le
  journal d'événements Windows est la réponse ; il n'est pas encore écrit.
- **Le logiciel n'est pas signé numériquement.** En déploiement par GPO ce n'est
  pas un obstacle — l'installation se fait sous le compte système — mais une
  installation manuelle affichera « Éditeur inconnu ». Une demande est en cours
  auprès de la SignPath Foundation.
- **Certains antivirus réagissent** à un outil qui lit les fichiers d'incident et
  interroge le matériel à bas niveau. Prévoir l'exclusion du dossier
  d'installation plutôt que la désactivation de l'antivirus.
- ~~**Le lanceur `.bat`** du script de réparation garde une fenêtre ouverte
  indéfiniment.~~ **Corrigé en 1.5.2** : il emploie le même enrobage que les
  trois boutons de l'application, et ne retient la fenêtre que lorsque le
  script n'est pas allé au bout.

---

## 9. Si quelque chose se passe mal

Le logiciel écrit ses propres pannes dans `C:\ProgramData\FaultTracePC\erreurs.log`
— version du logiciel, version de Windows, trace complète. Ce fichier est le
premier endroit à regarder, et il est fait pour être transmis tel quel.

Les autres fichiers utiles, tous sous `C:\ProgramData\FaultTracePC\` :

| Fichier | Contenu |
|---|---|
| `remote.json` | mode réseau, port, jeton de la machine |
| `langue.txt` | langue imposée au poste (`fr`, `en`, `auto`) |
| `alerts.json` | seuils de déclenchement des alertes |
| `Flight\alerts.jsonl` | journal des alertes émises |
| `Reports\` | rapports partagés, servis à la console |
