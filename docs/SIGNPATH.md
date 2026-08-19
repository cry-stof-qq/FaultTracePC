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
