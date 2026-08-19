## In English

A correction. On a machine whose Intel driver description contained a typographic apostrophe, the generated repair script **did not start at all** — a parse error, no repair performed. PowerShell treats curly quotes as real quotation marks, so the string ended in the middle of a driver name and the rest of the line was read as code. The escaping now neutralises the whole family of typographic apostrophes.

The notes below are in French. The full English description of the software is here: **[FaultTracePC — diagnose, monitor and repair a Windows PC](https://palisser.fr/spip.php?article31)**

| File | For whom |
|---|---|
| `FaultTracePC-1.4.1.msi` | Classic installation, or Group Policy deployment |
| `FaultTracePC-1.4.1-portable.zip` | No installation: unzip and run |

The `Source code` archives are generated automatically by GitHub: they contain the code, not the compiled software.

Set the language at install time: `msiexec /i FaultTracePC-1.4.1.msi FTPCLANG=en /qn`

**These files are not digitally signed.** Windows will show "Unknown publisher" — click *More info* then *Run anyway*. Checking the SHA-256 fingerprint at the bottom of this page is the only way to be sure the file you downloaded is the one published here.

---

Une seule correction, mais elle rendait le bouton principal inopérant sur une bonne partie des machines françaises.

## Le script de réparation ne démarrait pas

Sur le PC d'un utilisateur, un clic sur **« Lancer la réparation »** produisait une fenêtre pleine d'erreurs d'analyse et **aucune réparation exécutée**.

La cause tient dans un caractère. La description d'un pilote Intel s'appelle « Pilote v2 I2C **d'E/S** série Intel(R) », et cette apostrophe-là n'est pas l'apostrophe droite : c'est l'apostrophe typographique, U+2019.

Or la documentation de PowerShell est explicite : *« PowerShell treats smart quotation marks, also called typographic or curly quotes, as normal quotation marks for strings. »* La chaîne se terminait donc au milieu du nom du pilote, la fin de la ligne était lue comme du code, et l'apostrophe finale ouvrait une nouvelle chaîne qui avalait la ligne suivante. D'où la cascade d'erreurs.

L'échappement ne traitait que l'apostrophe droite. Il ramène désormais **toute la famille** — `'`, `‘`, `’`, `‛`, `′` — à l'apostrophe droite avant de la doubler. Le texte affiché y perd sa typographie ; dans une console PowerShell la différence est invisible, et un script qui démarre vaut mieux qu'une apostrophe élégante.

## Pourquoi rien ne l'avait vu

C'est la partie instructive. Le générateur produisait un texte **parfaitement valide en C#** : aucun test de compilation, aucun contrôle de traduction, aucune relecture du code source ne pouvait s'en apercevoir. Le défaut n'existait qu'aux yeux de l'interpréteur qui relit le résultat, sur une machine où un pilote porte le bon caractère.

Deux tests le verrouillent désormais, et ils vérifient le **script produit**, pas le code qui le produit :

- aucun caractère d'apostrophe typographique ne subsiste dans le script généré à partir du cas réel ;
- **chaque ligne referme ses chaînes** — une ligne qui laisse une chaîne ouverte avale la suivante, et c'est exactement la cascade observée. Le test relit le script comme le ferait PowerShell : il suit quel délimiteur a ouvert la chaîne, tient compte du doublage (`''`, `""`), de l'accent grave qui échappe entre guillemets doubles, et du `#` qui ouvre un commentaire. Compter simplement les apostrophes ne suffisait pas — `Write-Host "vérifie l'image Windows"` est parfaitement valide, et cette ligne existe dans le script.

## Ce que ce défaut a confirmé au passage

Le `-NoExit` ajouté en 1.4.0 pour que les fenêtres ne s'évaporent plus a servi ici pour la première fois : sans lui, cette fenêtre aurait clignoté et disparu, et le rapport aurait été « le bouton ne fait rien », sans trace. C'est parce qu'elle est restée ouverte que les dix-neuf lignes d'erreur ont pu être lues.

Le rapport HTML, lui, n'était pas concerné : il affichait le nom du pilote correctement.

## Contournement pour qui reste en 1.4.0

La **boîte à outils** n'est pas touchée. Elle exécute des commandes en ligne, jamais un fichier de script, et le nom des pilotes n'y transite pas. Les mêmes réparations s'y lancent une par une.

## Mise à jour

Le MSI remplace proprement la 1.4.0. Aucun changement de format, de protocole ni de service.
