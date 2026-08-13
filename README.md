# FaultTracePC

Logiciel de diagnostic de pannes Windows 10/11 — léger et efficace.

Deux modes prévus :

1. **Scan post-mortem** (disponible — Phase 1) : après une panne, analyse croisée des
   dumps (`Minidump`, `MEMORY.DMP`, `LiveKernelReports`), du journal d'événements
   (BugCheck 1001, Kernel-Power 41, WHEA, erreurs disque, TDR, crashs applicatifs…),
   du Moniteur de fiabilité et de l'état matériel (WMI). Produit un rapport HTML en
   français ouvert automatiquement dans le navigateur par défaut.
2. **Surveillance temps réel** (Phase 3) : service Windows léger qui journalise en
   continu événements + températures dans un journal circulaire écrit sur disque,
   pour retrouver les secondes précédant un crash.

## Architecture

```
FaultTracePC.sln
└── src
    ├── FaultTracePC.Core     Bibliothèque : collecteurs, moteur de règles, rapport HTML
    │   ├── Collectors        WMI, journal d'événements, fiabilité, dumps (parsing natif)
    │   ├── Analysis          Catalogue BugCheck + moteur de corrélation
    │   └── Report            Générateur de rapport HTML autonome
    └── FaultTracePC.App      Application WPF (GUI légère), exige l'élévation admin
```

Points techniques notables :

- **Parsing natif des dumps noyau** : lecture du code STOP et des 4 paramètres
  directement dans l'en-tête `PAGE`/`DU64` (offsets 0x38 / 0x40), sans dépendance.
  L'analyse symbolique profonde (pilote exact via `!analyze -v`) arrive en Phase 2
  avec l'intégration optionnelle de WinDbg/CDB.
- **Requêtes d'événements ciblées** (XPath par fournisseur/ID) : rapide même sur
  un journal volumineux, plafonné à 500 événements par requête.
- Chaque collecteur est isolé : une source illisible n'empêche pas le diagnostic,
  elle est listée dans la section « Limitations » du rapport.

## Compiler et lancer (sur Windows, SDK .NET 10 requis)

```powershell
cd $env:USERPROFILE\Documents\FaultTracePC
dotnet build
dotnet run --project src/FaultTracePC.App
```

L'application demande l'élévation administrateur (nécessaire pour lire les dumps
et les journaux complets). Les rapports sont écrits dans `Documents\FaultTracePC\`.

## Publier un exécutable

```powershell
# Léger (~qq Mo), nécessite le runtime .NET 10 Desktop sur la machine cible :
dotnet publish src/FaultTracePC.App -c Release -r win-x64 --self-contained false

# 100 % autonome (~80 Mo), aucun prérequis sur la machine cible :
dotnet publish src/FaultTracePC.App -c Release -r win-x64 --self-contained true
```

L'exe se trouve ensuite dans `src/FaultTracePC.App/bin/Release/net10.0-windows/win-x64/publish/`.

## Feuille de route

- [x] Phase 1 — Scan post-mortem + rapport HTML
- [x] v0.2 — Processus en cours (RAM/CPU/disque), détection de saturation mémoire
      (Resource-Exhaustion-Detector), filtres cliquables dans le rapport, matériel
      nommé (marque/modèle) dans les conclusions, script PowerShell de réparation
      adapté aux problèmes trouvés (sûr par défaut, confirmations O/N)
- [x] Phase 2 (v0.3) — Analyse symbolique des dumps via CDB/WinDbg si présent
      (`winget install Microsoft.WinDbg`) : pilote fautif nommé (IMAGE_NAME),
      signature de crash (FAILURE_BUCKET_ID), pile d'appels dépliable dans le
      rapport, récurrence par pilote, interprétation des verdicts
      « memory_corruption »/« ntoskrnl » ; cache de symboles local
- [x] v0.4 — Suivi avant/après réparation (historique JSON des scans dans
      `Documents\FaultTracePC\Historique`, comparaison automatique : nouveaux crashs,
      récurrence de signature, pilotes mis à jour, évolution disques/mémoire, verdict
      d'efficacité) + base de signatures de ~30 pilotes connus (GPU, réseau, stockage,
      antivirus, anti-triche, RGB, virtualisation) avec correctifs ciblés
- [x] Phase 3 (v0.5) — Boîte noire temps réel : service Windows `FaultTracePC.Monitor`
      (échantillon toutes les 10 s : charge/température CPU-GPU via LibreHardwareMonitor,
      mémoire physique+virtuelle, top processus ; événements critiques en direct ;
      chaque ligne synchronisée physiquement sur disque ; rotation 14 jours dans
      `C:\ProgramData\FaultTracePC\Flight`). Installation/désinstallation en 1 clic
      depuis l'app (bouton 📡). Le scan lit le journal et affiche les dernières
      secondes avant chaque crash + règles surchauffe/saturation au moment du crash.
- [x] v0.6 — Mode réseau : « Client » (API HTTP en lecture seule dans le service —
      statut temps réel, journal boîte noire, rapports partagés — double verrou :
      adresses privées RFC 1918 uniquement ET token 256 bits, plus règle de pare-feu
      restreinte aux mêmes plages) et console « 🖥 Parc » (mode maître : état de
      toutes les machines clientes, ouverture des rapports distants). Rien n'est
      accessible depuis Internet ; aucune écriture ni exécution à distance.
- [x] v0.7 — Boîte à outils intégrée (🧰) : désinstallation d'une mise à jour Windows,
      réinitialisation des composants Windows Update, réparation sur place,
      sfc/DISM/chkdsk/mdsched/SMART à un clic dans une fenêtre PowerShell visible.
- [x] v0.8 — Alertes préventives : le service surveille des seuils (températures CPU/GPU,
      saturation de la mémoire virtuelle, erreurs WHEA, erreurs disque, santé SMART) et
      prévient AVANT la panne — notification Windows, journal `alerts.jsonl`, section
      dédiée dans le rapport, conclusions du diagnostic et endpoint `/api/alerts`.
      Anti-bruit : N échantillons consécutifs requis + délai anti-répétition.
- [ ] Phase 4 — Finitions GUI, installateur, mode CLI
