## In English

**Security maintenance release: no new feature, no behaviour change.** FaultTracePC ships with its own copy of the .NET runtime (self-contained build), so Windows Update never patches it: only a rebuild does. This release is rebuilt on .NET 10.0.14, which includes Microsoft's out-of-band security fixes of 25 October 2026, and its Microsoft libraries are brought up to the same version.

The notes below are in French. The full English description of the software is here: **[FaultTracePC — diagnose, monitor and repair a Windows PC](https://palisser.fr/spip.php?article31)**

| File | For whom |
|---|---|
| `FaultTracePC-1.9.3.msi` | Classic installation, or Group Policy deployment |
| `FaultTracePC-1.9.3-portable.zip` | No installation: unzip and run |

The `Source code` archives are generated automatically by GitHub: they contain the code, not the compiled software.

Set the language at install time: `msiexec /i FaultTracePC-1.9.3.msi FTPCLANG=en /qn`

Fleet console at install time: `msiexec /i FaultTracePC-1.9.3.msi /qn ADDLOCAL=Main,ParkFeature` (or `ADDLOCAL=ALL`).

**These files are not digitally signed.** Windows will show "Unknown publisher" — click *More info* then *Run anyway*. Checking the SHA-256 fingerprint at the bottom of this page is the only way to be sure the file you downloaded is the one published here.

---

Version de maintenance de sécurité : aucune fonction nouvelle, aucun changement de comportement.

## Pourquoi cette version

FaultTracePC embarque **sa propre copie de .NET**, le moteur qui le fait tourner : c'est ce qui permet de l'installer sans rien d'autre, même sur une machine fraîchement réinstallée. La contrepartie est simple : **Windows Update ne met jamais à jour cette copie**. Microsoft le rappelle pour toute application construite ainsi : il faut la reconstruire et la republier avec le .NET corrigé.

Microsoft a publié deux mises à jour de sécurité de .NET 10 en octobre 2026 : le 13 (10.0.13), puis, hors calendrier, le 25 (10.0.14). Celle du 25 corrige une faille de la décompression Brotli (CVE-2026-111497) et republie deux correctifs du 13 qui étaient incomplets, dont une corruption de mémoire dans les sockets réseau (CVE-2026-70197). La 1.9.2, construite le 23 octobre, n'avait pas ceux du 25.

## Ce qui change

- Construite sur **.NET 10.0.14**.
- Bibliothèques Microsoft alignées sur 10.0.14 : `System.Management`, `System.Diagnostics.EventLog`, `System.DirectoryServices`, `Microsoft.Extensions.Hosting.WindowsServices` (elles étaient en 10.0.10).

## Ce qui a été vérifié, et ce qui ne l'est pas

- FaultTracePC n'utilise ni la décompression Brotli ni la décompression automatique des réponses HTTP : d'après la description de Microsoft, la faille Brotli ne le concerne pas.
- Pour la faille des sockets réseau, la description publiée ne permet pas de conclure : la console de parc et la vérification des mises à jour passent par les sockets de .NET. C'est la raison de ne pas attendre.
- LibreHardwareMonitorLib (lecture des températures) est déjà à sa dernière version, 0.9.6, qui passe par PawnIO et non par l'ancien pilote WinRing0. WiX, qui fabrique le MSI, n'est concerné par aucune faille connue.

## Désormais

FaultTracePC sera reconstruit après chaque mise à jour de sécurité mensuelle de .NET (le deuxième mardi du mois), puisque rien d'autre ne mettra à jour le .NET qu'il embarque.

## Ce qu'il faut mettre à jour, et où

Tous les postes où FaultTracePC est installé : chacun embarque sa copie de .NET. Sur un parc, la console le fait par la sélection.

## Ce qui ne change pas

- L'installation est la même qu'en 1.9.0. La case « Mode parc » garde le choix fait à l'installation précédente.
- Rappel : une liste `ADDLOCAL` installe exactement ce qu'elle nomme. `ADDLOCAL=Main` installe **sans** la console, même en mise à jour ; pour la garder, `ADDLOCAL=Main,ParkFeature`.
- FaultTracePC demande un Windows 10 ou 11 à jour.
- Les analyses enregistrées par les versions précédentes restent lisibles.
