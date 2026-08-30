## In English

A deployment fix. Reinstalling the **same version** over itself was refused by Windows Installer with error 1638 — "another version of this product is already installed" — and with `/qn`, that failure was completely silent: nothing installed, nothing said. The package can now replace itself. Nothing else changed; if you are not deploying across several machines, 1.5.0 serves you just as well.

The notes below are in French. The full English description of the software is here: **[FaultTracePC — diagnose, monitor and repair a Windows PC](https://palisser.fr/spip.php?article31)**

| File | For whom |
|---|---|
| `FaultTracePC-1.5.1.msi` | Classic installation, or Group Policy deployment |
| `FaultTracePC-1.5.1-portable.zip` | No installation: unzip and run |

The `Source code` archives are generated automatically by GitHub: they contain the code, not the compiled software.

Set the language at install time: `msiexec /i FaultTracePC-1.5.1.msi FTPCLANG=en /qn`

**These files are not digitally signed.** Windows will show "Unknown publisher" — click *More info* then *Run anyway*. Checking the SHA-256 fingerprint at the bottom of this page is the only way to be sure the file you downloaded is the one published here.

---

Une correction de déploiement, trouvée la veille d'une rentrée scolaire en répétant l'installation sur un second poste.

## Le paquet ne savait pas se remplacer lui-même

Réinstaller **le même numéro de version** par-dessus lui-même échouait, avec le message de Windows Installer : « Une autre version de ce produit est déjà installée. Impossible de poursuivre l'installation de cette version. »

La cause est un défaut par défaut de WiX : une mise à jour n'est reconnue que pour une version **strictement inférieure**. Le `ProductCode` étant régénéré à chaque compilation, un paquet portant le même numéro que celui déjà installé n'est ni une mise à jour, ni le même paquet. `AllowSameVersionUpgrades` corrige exactement ce cas.

Ce n'est pas un cas d'école. Un déploiement n'est jamais linéaire : un poste qu'on reprend, un script d'ouverture de session qui repasse le lendemain, une machine réparée qu'on remet en service. Chacune de ces situations tombait sur ce refus.

## Ce qui rendait le défaut coûteux

**En mode silencieux, l'échec ne dit rien.** `msiexec /qn` supprime toute interface, y compris les messages d'erreur : la commande rend le code 1638 et, si personne ne le lit, l'installation paraît s'être bien passée. Sur un parc, cela donne des postes où rien n'est installé et que rien ne signale.

C'est le vrai enseignement de cette version, et il est maintenant écrit dans la procédure de déploiement : une installation silencieuse **se vérifie par son code de sortie**, jamais par l'absence de message.

```powershell
$p = Start-Process msiexec -ArgumentList '/i','FaultTracePC-1.5.1.msi','/qn','/l*v','C:\Windows\Temp\ftpc.log' -Wait -PassThru
if ($p.ExitCode -notin 0,3010) { throw "installation echouee ($($p.ExitCode))" }
```

`0` installé · `3010` installé, redémarrage demandé · `1638` une autre version est déjà là · `1603` échec général, le journal en dit plus.

## Aussi dans cette version

**La règle de pare-feu est posée par la ligne de commande.** `--configure-remote` crée désormais la règle entrante limitée aux adresses privées, et dit ce qu'il a fait. Elle n'était créée ni par le MSI, ni par la ligne de commande : un parc déployé par stratégie de groupe obtenait des postes qui écoutent correctement et que rien ne peut joindre. L'option `--no-firewall` laisse la stratégie de groupe s'en charger, ce qui reste la pratique recommandée en domaine — une règle locale pouvant y être ignorée.

**Une procédure de déploiement écrite.** `docs/DEPLOIEMENT.md` : le secret maître, l'installation par stratégie de groupe, la configuration sans interface, le pare-feu, les vérifications à faire le jour même, les messages d'erreur et ce qu'ils veulent dire — et une section « limites connues » qui ne cache rien.

## Mise à jour

Le MSI remplace proprement la 1.5.0, et désormais aussi lui-même. Aucun changement de format de fichier ni de protocole de parc.
