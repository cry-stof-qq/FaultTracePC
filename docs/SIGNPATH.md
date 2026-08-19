# Candidature à la Fondation SignPath

Dossier préparé le 19/08/2026. Objectif : obtenir la signature de code gratuite
pour les binaires publiés, afin que Windows cesse d'afficher « Éditeur inconnu ».

Source des conditions : <https://signpath.org/terms.html>

---

## 1. Les conditions, et où nous en sommes

| Condition | État | Preuve |
|---|---|---|
| Licence approuvée OSI, sans double licence commerciale | ✅ | `LICENSE` — MIT |
| Aucun composant propriétaire ou non open source | ✅ | Dépendances : .NET 10, WiX Toolset, xUnit — toutes open source |
| Pas de logiciel malveillant ni indésirable | ✅ | Outil de diagnostic, aucune collecte, aucun envoi réseau vers Internet |
| Projet activement maintenu | ✅ | 1.1.0 → 1.4.0 publiées, historique de commits public |
| Déjà publié sous la forme à signer | ✅ | MSI et ZIP portable publiés à chaque version |
| Fonctionnalité décrite sur la page de téléchargement | ✅ | Notes de version détaillées + <https://palisser.fr/spip.php?article30> (FR) et <https://palisser.fr/spip.php?article31> (EN) |
| **Binaires construits depuis les sources de façon vérifiable** | ✅ | `.github/workflows/publication.yml` — construction par GitHub Actions à partir du commit désigné par le tag, aucune intervention manuelle |
| Approbation manuelle de chaque signature | ✅ | Chaque publication part d'un tag posé à la main |
| Authentification à deux facteurs sur le dépôt et sur SignPath | ✅ GitHub (application d'authentification, confirmé le 19/08/2026) — SignPath à activer après création du compte | Voir §3 |
| Séparation des rôles auteur / relecteur / approbateur | ⚠ **point faible** | Voir §2 |

## 2. Le point à aborder franchement : le mainteneur unique

SignPath demande une séparation des rôles entre celui qui écrit, celui qui relit
et celui qui approuve. FaultTracePC a **un seul mainteneur**.

Ne pas le cacher. Ce qui peut être avancé, et qui est vrai :

- **La construction n'a jamais lieu sur un poste de développement.** Les binaires
  proviennent exclusivement de GitHub Actions, à partir du commit exact désigné
  par le tag. C'est précisément la garantie que la séparation des rôles cherche à
  obtenir — qu'un binaire ne puisse pas contenir autre chose que le code publié.
- **Le déclenchement est manuel et tracé** : poser un tag est un acte délibéré,
  horodaté et public.
- **La chaîne refuse de publier ce qui ne passe pas les tests** : l'étape des
  tests précède la publication et l'interrompt en cas d'échec.
- **Un essai à blanc existe** (`workflow_dispatch` sans publication) et sert avant
  chaque version : tout est construit, rien n'est publié.

## 3. Ce qu'il reste à vérifier avant d'envoyer

~~**L'authentification à deux facteurs du compte GitHub.**~~ **Vérifiée le
19/08/2026** : *Two-factor authentication → Authenticator app → Configured*.

**L'authentification à deux facteurs du compte SignPath** se règle après la
création du compte, au même endroit que le profil.

## 4. Ce qu'ils vont regarder en premier

D'après leurs conditions, dans cet ordre :

1. **La licence** — MIT, sans ambiguïté.
2. **La reproductibilité de la construction** — c'est le cœur de leur exigence, et
   c'est le point le plus solide du dossier.
3. **La page de téléchargement** — elle doit décrire ce que fait le logiciel. Les
   notes de version et les deux articles du site y répondent.
4. **La réputation vérifiable**, pour un programme exécutable. Aucun seuil chiffré
   n'est publié ; leurs termes reconnaissent que ce point relève d'un jugement au
   cas par cas. C'est l'inconnue du dossier.

## 5. Si la candidature est refusée

Le paysage a changé en 2024, et deux idées reçues sont à écarter.

- **Azure Artifact Signing** (ex-Trusted Signing), ~10 $/mois : les **particuliers
  ne sont éligibles qu'aux États-Unis et au Canada**. Fermé à un particulier
  français ; ouvert à une organisation de l'UE.
- **Certificat OV**, 150–300 $/an, particuliers du monde entier — clé privée
  obligatoirement sur token matériel ou HSM depuis juin 2023.
- **Certificat EV**, 400 $/an et plus : **à écarter**. Depuis 2024 il ne
  court-circuite plus SmartScreen au premier téléchargement et suit la même montée
  en réputation qu'un OV. Son seul avantage historique a disparu.

Source : [Code signing options for Windows app developers — Microsoft Learn](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/code-signing-options)

## 6. En attendant

Les sommes de contrôle SHA-256 publiées à chaque version restent le seul moyen de
vérifier qu'un fichier téléchargé est bien celui publié. Elles sont générées par
la chaîne de construction, jamais écrites à la main.

---

# Le formulaire, champ par champ

Relevé sur <https://signpath.org/apply> le 19/08/2026. Les champs marqués \* sont obligatoires.

| Champ | Quoi mettre |
|---|---|
| **Project Name** \* | `FaultTracePC` — voir l'avertissement ci-dessous |
| **Repository URL** \* | `https://github.com/cry-stof-qq/FaultTracePC` |
| **Homepage URL** \* | `https://palisser.fr/spip.php?article31` — la page **anglaise**. Le dépôt est déjà donné au champ précédent ; pointer une page dédiée montre un projet, pas seulement un dépôt. |
| **Download URL** | `https://github.com/cry-stof-qq/FaultTracePC/releases/latest` — **attention, voir l'obligation ci-dessous** |
| **Privacy Policy URL** | `https://palisser.fr/spip.php?article31` — la section « Personal data ». Leur consigne dit « requis si le logiciel collecte des données » ; le logiciel n'envoie rien, mais le rapport contient des données personnelles localement. Donner le lien est la réponse honnête, et cette section existe déjà. |
| **Wikipedia URL** | laisser vide |
| **Tagline** \* | voir texte ci-dessous |
| **Description** \* | voir texte ci-dessous |
| **Reputation** \* | voir texte ci-dessous — c'est le champ décisif |
| **Maintainer Type** | liste déroulante — « Individual » |
| **Build System** | liste déroulante — « GitHub Actions » |
| **First / Last Name** \* | ton prénom et ton nom — ce sera le compte SignPath |
| **Email** \* | l'adresse du compte |
| **Company Name** | laisser vide |
| **Primary Discovery Channel** \* | liste déroulante, réponds sincèrement |

## Une obligation à ne pas manquer

Le champ *Download URL* précise : **« This page must mention that the project uses the SignPath Foundation for code signing. »**

La page de téléchargement devra donc créditer SignPath. Comme le corps des releases est composé par `publication.yml`, la mention se mettra à cet endroit — **une fois la candidature acceptée**, pas avant : annoncer une signature qu'on n'a pas serait faux.

## Les trois textes

**Tagline**

> FaultTracePC finds the cause of a Windows crash, explains it in plain language, and carries the repair through.

**Description**

> FaultTracePC is a free diagnostic tool for Windows 10 and 11. It reads what Windows already records — crash dumps, event logs, the reliability history and the hardware sensors — cross-checks those sources, and writes a report that names the software or the driver involved and says what to do about it. A guided mode applies the risk-free repairs on its own and asks before anything that reboots, installs or uninstalls. Everything runs locally: no account, no telemetry, nothing sent anywhere. The interface and the reports exist in English and in French.

**Reputation** — le champ décisif, et celui où il ne faut rien enjoliver

> The project is young and I will not overstate it. It is developed and maintained by one person, released publicly since June 2026, with five versions published so far. Every binary is built by GitHub Actions from the exact commit a tag designates — never on a workstation — and each release carries SHA-256 checksums generated by that same chain. The source is MIT and complete: there is no proprietary component and nothing to take on trust.
>
> Its documentation is public in two languages, at https://palisser.fr/spip.php?article30 (French) and https://palisser.fr/spip.php?article31 (English), including a step-by-step guide and an explicit section on what personal data a diagnostic report contains.
>
> What I cannot show yet is a large user base. The software is used by a handful of people I know and is starting to be evaluated for deployment in an organisation. I am applying now because the requirement I consider hardest — verifiable builds from source — is the one I have already met, and because an unsigned installer showing "Unknown publisher" is precisely what stops that evaluation from going further.

## Un point à vérifier avant d'envoyer, et il n'est pas favorable

Le champ *Project Name* précise : **« A Google search for this name should clearly identify your project. »**

**Recherche effectuée le 19/08/2026 : elle ne le fait pas.** « FaultTracePC » renvoie des articles de géologie et des pages Wikipédia sur les failles sismiques. Ni le dépôt GitHub ni le site n'apparaissent.

Ce n'est pas rédhibitoire — leur formulaire dit « should », pas « must » — mais combiné au champ *Reputation*, cela oriente vers un verdict « trop tôt ». Deux signaux mesurables à attendre plutôt qu'à espérer :

1. une recherche sur « FaultTracePC » fait remonter le dépôt ou le site dans les premiers résultats ;
2. les releases affichent des compteurs de téléchargement non nuls.

Trois actions gratuites qui accélèrent le premier point : renseigner la **description et les sujets** du dépôt GitHub (`windows`, `bsod`, `crash-analysis`, `diagnostics`, `dotnet`), soumettre le site aux moteurs de recherche, et laisser passer quelques semaines — les articles ne sont en ligne que depuis le 18/08/2026.
