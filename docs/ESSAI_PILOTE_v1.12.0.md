# Essai pilote de la 1.12.0

Préparé le 12/11/2026, à la demande de l'auteur : **l'essai se fera plus tard**, ce document dit tout ce qu'il faut pour le faire. Le principe : installer la 1.12.0, construite sur le poste de l'auteur, sur **les six postes d'une même salle déjà suivis dans la console** — et sur le poste de l'auteur — puis vérifier en vrai, point par point, ce qui n'a pu l'être que par les tests automatiques. La publication (tag `v1.12.0`) ne vient qu'après, si l'essai est bon.

Noms de postes, serveur et partage ci-dessous : valeurs d'exemple.

## ⚠ Une date à respecter pour le point 125

Le diagnostic à distance lit **30 jours** de journal. La période « éteint 21 jours » du poste A commence le **21/10/2026 à 21:00** : pour que sa conclusion apparaisse, le diagnostic du poste A doit être lancé **au plus tard le 19/11/2026**. Après, l'arrêt du 21/10 sort de la période lue, et la conclusion ne peut plus être vérifiée sur ce cas réel.

## 1. Construire le MSI (poste de l'auteur, PowerShell)

```
cd C:\Users\<compte>\Documents\FaultTracePC
powershell -ExecutionPolicy Bypass -File installer\build-msi.ps1
Get-FileHash dist\FaultTracePC-1.12.0.msi -Algorithm SHA256
```

Noter l'empreinte. Une construction locale embarque le .NET du SDK installé sur le poste ; c'est sans importance pour l'essai (la version publiée sera construite par GitHub).

## 2. Le déposer sur le partage de déploiement

```
Copy-Item dist\FaultTracePC-1.12.0.msi \\SERVEUR\partage$\
Get-FileHash \\SERVEUR\partage$\FaultTracePC-1.12.0.msi -Algorithm SHA256
```

Les deux empreintes doivent être identiques.

## 3. Déployer sur les six postes seulement

Console de parc → onglet **Inventaire** :
1. champ du paquet : `\\SERVEUR\partage$\FaultTracePC-1.12.0.msi` ;
2. cocher **uniquement** les six postes de la salle (et, si on le souhaite, le poste de l'auteur) ;
3. lancer la mise à jour et confirmer.

Onglet **Supervision** → « Actualiser tout » : la colonne Version des postes déployés doit afficher 1.12.0.

## 4. Diagnostiquer

Pour chacun des six postes : « Lancer un diagnostic à distance » (avant le 19/11 pour le poste A, voir plus haut). Puis « Rapport du parc ».

## 5. Ce qu'il faut vérifier

| Point | Où | Résultat attendu |
|---|---|---|
| 121 — tri | Console, onglet Supervision | Clic sur « Machine » : ▲, ordre naturel ; second clic : ▼. « CPU % », « T° GPU », « RAM % » : ordre des nombres. « Dernier relevé » : ordre des dates. « Alertes 90 j » : sur le total. Cases vides toujours en bas. Le tri tient après « Actualiser tout ». |
| 121 — tri | Console, onglet Inventaire | Même comportement ; « Dernière session » triée comme une date ; la colonne des cases à cocher ne trie pas. |
| 122 — langue | Rapport du parc | Colonne « Dernière alerte / erreur » en français (plus de « Processor temperature high »). |
| 123 — programme introuvable | Rapport des postes de la salle | Carte « … dont le programme est introuvable » pour le service Synaptics ; texte nuancé (« C'est souvent… Mais un logiciel toujours installé… »). |
| 124 — événement d'information | Rapport du poste B | La carte WHEA redevient « N erreur(s) matérielle(s) CORRIGÉE(S) — lien PCI Express », niveau Avertissement ; le verdict ne dit plus « MATÉRIELLE ». |
| 125 — arrêts et démarrages | Rapport de chaque poste | Section « Derniers arrêts et démarrages » ; les arrêts programmés de 20:30 (redémarrage) et 21:00 (arrêt) apparaissent comme « arrêt propre, demandé par shutdown.exe ». Poste A (avant le 19/11) : conclusion « Poste resté éteint 21 j 11 h (du 21/10 à 21:00 au 12/11 à 08:02) », « ni erreur matérielle, ni plantage… », « Redémarrage : démarrage à froid ». |
| 126 — postes restés éteints | Rapport du parc | Une carte qui nomme le poste A (et le poste B si sa période dépasse 48 heures) ; « pendant ce temps, N autre(s) poste(s) du même modèle ont démarré » ; puis « Ce qui les distingue… » ou « Rien ne les distingue… ». Il faut que les six postes aient la 1.12.0 ET un diagnostic récent. |
| 120 (a) — sondes de température | Poste de l'auteur (PawnIO installé) | Ligne de début de session du journal de la boîte noire : `|temp=…` avant `|pawnio=Running` ; relevés à 80 °C ou plus : champ `ctn` avec le nom d'une sonde. |
| 120 (C) — bridage | Rapport du poste de l'auteur | Carte « Processeur à sa limite de température sous charge, sans ralentir » (Information) au lieu de « Surchauffe ». La carte d'alerte en temps réel, elle, reste — limite connue. |

Commandes utiles sur le poste de l'auteur (point 120 a) :

```
Select-String -Path "C:\ProgramData\FaultTracePC\Flight\flight_$(Get-Date -Format yyyyMMdd).jsonl" -Pattern '"diag"' | Select-Object -Last 1
Select-String -Path "C:\ProgramData\FaultTracePC\Flight\flight_$(Get-Date -Format yyyyMMdd).jsonl" -Pattern '"ctn"' | Select-Object -Last 3
```

## 6. Après l'essai

Noter chaque résultat dans la feuille de route (points 120 à 126). Si tout est bon : publication de la 1.12.0 (tag `v1.12.0`), puis — quand l'auteur la juge assez fiable — installation sur les 26 autres postes de la salle, qui rendra la carte du point 126 vraiment solide.
