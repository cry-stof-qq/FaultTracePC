using System.Text;

namespace FaultTracePC.Core.Report;

/// <summary>
/// Génère un script PowerShell d'aide à la réparation ADAPTÉ aux problèmes trouvés :
/// seules les sections correspondant aux conclusions du diagnostic sont incluses.
///
/// Philosophie de sécurité :
///  - les tests en LECTURE SEULE s'exécutent automatiquement ;
///  - toute action qui MODIFIE le système (sfc, DISM RestoreHealth, chkdsk /f,
///    diagnostic mémoire avec redémarrage) demande une confirmation O/N ;
///  - les actions risquées (driver verifier) ne sont jamais exécutées : elles sont
///    documentées en commentaire avec un avertissement explicite.
/// Tout est journalisé (transcript) dans Documents\FaultTracePC.
/// </summary>
public static class RepairScriptGenerator
{
    /// <summary>Catégories pour lesquelles un script a du sens (au moins un test/réparation).</summary>
    public static bool IsRepairable(DiagnosticReport r) =>
        r.Findings.Any(f => f.Severity != Severity.Info);

    public static string Generate(DiagnosticReport r)
    {
        var cats = r.Findings
            .Where(f => f.Severity != Severity.Info)
            .Select(f => f.Category)
            .Distinct()
            .ToHashSet();

        bool hasBsod = r.Bsods.Count > 0;
        var sb = new StringBuilder(16 * 1024);

        // ------------------------------------------------------------- en-tête
        sb.AppendLine("#Requires -RunAsAdministrator");
        sb.AppendLine("<#");
        sb.AppendLine("  FaultTracePC — Script d'aide à la réparation");
        sb.AppendLine($"  Généré le {r.GeneratedAt:dd/MM/yyyy HH:mm} pour la machine {r.System.MachineName}");
        sb.AppendLine($"  Basé sur {r.Findings.Count(f => f.Severity != Severity.Info)} problème(s) détecté(s) : "
                      + string.Join(", ", cats.Select(CatLabel)));
        sb.AppendLine();
        sb.AppendLine("  RÈGLES DE CE SCRIPT :");
        sb.AppendLine("   - Les tests en lecture seule s'exécutent automatiquement.");
        sb.AppendLine("   - Toute action qui modifie le système demande une confirmation O/N.");
        sb.AppendLine("   - Rien d'irréversible n'est lancé sans votre accord.");
        sb.AppendLine();
        sb.AppendLine("  Lancement : clic droit > Exécuter avec PowerShell, ou :");
        sb.AppendLine("    powershell -ExecutionPolicy Bypass -File .\\" + "Reparation_PC.ps1");
        sb.AppendLine("#>");
        sb.AppendLine();
        sb.AppendLine("$ErrorActionPreference = 'Continue'");
        sb.AppendLine("$logDir = Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'FaultTracePC'");
        sb.AppendLine("New-Item -ItemType Directory -Force -Path $logDir | Out-Null");
        sb.AppendLine("Start-Transcript -Path (Join-Path $logDir (\"Reparation_log_{0:yyyy-MM-dd_HHmm}.txt\" -f (Get-Date))) | Out-Null");
        sb.AppendLine();
        sb.AppendLine("function Ask([string]$q) { (Read-Host \"$q (O/N)\") -match '^[oOyY]' }");
        sb.AppendLine("function Section([string]$t) { Write-Host \"`n=== $t ===\" -ForegroundColor Cyan }");
        sb.AppendLine();
        sb.AppendLine("Write-Host 'FaultTracePC — Aide à la réparation' -ForegroundColor Green");
        sb.AppendLine($"Write-Host 'Diagnostic du {r.GeneratedAt:dd/MM/yyyy HH:mm} — problèmes ciblés : {string.Join(", ", cats.Select(CatLabel))}'");
        sb.AppendLine();

        // ------------------------------------------- filet de sécurité : restauration
        // Rien ne doit être modifié avant d'avoir un retour en arrière possible.
        // Windows bride par défaut la création de points de restauration à un
        // toutes les 24 h : on lève temporairement la bride, puis on la remet.
        sb.AppendLine("Section 'Filet de sécurité : point de restauration'");
        sb.AppendLine("$srKey = 'HKLM:\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\SystemRestore'");
        sb.AppendLine("$srOld = (Get-ItemProperty -Path $srKey -Name SystemRestorePointCreationFrequency -ErrorAction SilentlyContinue).SystemRestorePointCreationFrequency");
        sb.AppendLine("$srDrive = $env:SystemDrive + '\\'");
        sb.AppendLine("try { New-ItemProperty -Path $srKey -Name SystemRestorePointCreationFrequency -Value 0 -PropertyType DWord -Force | Out-Null } catch {}");
        sb.AppendLine("Write-Host 'Création du point de restauration (peut prendre 1 à 2 minutes)…'");
        sb.AppendLine("$srErr = $null");
        sb.AppendLine("try { Checkpoint-Computer -Description 'FaultTracePC - avant reparation' -RestorePointType 'MODIFY_SETTINGS' -ErrorAction Stop }");
        sb.AppendLine("catch { $srErr = $_.Exception.Message }");
        sb.AppendLine("$rp = Get-ComputerRestorePoint -ErrorAction SilentlyContinue | Select-Object -Last 1");
        sb.AppendLine("if (-not $srErr -and $rp) {");
        sb.AppendLine("    Write-Host ('Point de restauration disponible : #{0} — {1} ({2})' -f $rp.SequenceNumber, $rp.Description, $rp.ConvertToDateTime($rp.CreationTime)) -ForegroundColor Green");
        sb.AppendLine("} else {");
        sb.AppendLine("    Write-Host 'AUCUN point de restauration n''a pu être créé : les étapes suivantes ne seront PAS annulables.' -ForegroundColor Yellow");
        sb.AppendLine("    if ($srErr) { Write-Host ('  Motif : ' + $srErr) -ForegroundColor Yellow }");
        sb.AppendLine("    Write-Host '  Cause la plus fréquente : la protection du système est désactivée sur ce PC.' -ForegroundColor Yellow");
        sb.AppendLine("    Write-Host ('  Pour l''activer : Enable-ComputerRestore -Drive ' + $srDrive + '   (ou Panneau de configuration > Système > Protection du système)')");
        sb.AppendLine("    if (-not (Ask 'Continuer SANS filet de sécurité ?')) { Stop-Transcript | Out-Null; exit }");
        sb.AppendLine("}");
        sb.AppendLine("# Remise en place du réglage d'origine de Windows (bride des 24 h).");
        sb.AppendLine("try { if ($null -ne $srOld) { Set-ItemProperty -Path $srKey -Name SystemRestorePointCreationFrequency -Value $srOld } else { Remove-ItemProperty -Path $srKey -Name SystemRestorePointCreationFrequency -ErrorAction SilentlyContinue } } catch {}");
        sb.AppendLine("Write-Host 'Pour revenir en arrière plus tard : rstrui.exe'");
        sb.AppendLine();

        // ------------------------------------------------------- état général
        sb.AppendLine("Section 'État général'");
        sb.AppendLine("Get-CimInstance Win32_OperatingSystem | Select-Object Caption, Version, LastBootUpTime | Format-List");
        sb.AppendLine("Write-Host ('Mémoire libre : {0:N1} Go' -f ((Get-CimInstance Win32_OperatingSystem).FreePhysicalMemory/1MB))");
        sb.AppendLine();

        // --------------------------------------- intégrité système (logiciel)
        if (cats.Contains(FaultCategory.Software) || cats.Contains(FaultCategory.WindowsUpdate) ||
            cats.Contains(FaultCategory.Driver) || hasBsod)
        {
            // Vérifications EXÉCUTÉES (pas seulement conseillées) : l'utilisateur voit
            // directement « déjà à jour » ou « mise à jour appliquée » — pas de conseil inutile.
            sb.AppendLine("Section 'Mises à jour des composants (vérification automatique)'");
            sb.AppendLine("if (Get-Command wsl -ErrorAction SilentlyContinue) {");
            sb.AppendLine("    Write-Host 'Vérification/mise à jour de WSL (virtualisation) :'");
            sb.AppendLine("    wsl --update");
            sb.AppendLine("}");
            sb.AppendLine("Write-Host 'Dernières mises à jour Windows installées :'");
            sb.AppendLine("Get-HotFix | Sort-Object InstalledOn -Descending | Select-Object -First 5 HotFixID, Description, InstalledOn | Format-Table -AutoSize");
            sb.AppendLine("if (Ask 'Ouvrir Windows Update pour rechercher les mises à jour en attente') {");
            sb.AppendLine("    Start-Process 'ms-settings:windowsupdate-action'");
            sb.AppendLine("}");
            sb.AppendLine();
            sb.AppendLine("Section 'Intégrité des fichiers système'");
            sb.AppendLine("if (Ask 'Lancer sfc /scannow (vérifie et répare les fichiers système, ~10 min)') {");
            sb.AppendLine("    sfc /scannow");
            sb.AppendLine("}");
            sb.AppendLine("if (Ask \"Lancer DISM /ScanHealth (vérifie l'image Windows, ~5 min)\") {");
            sb.AppendLine("    DISM /Online /Cleanup-Image /ScanHealth");
            sb.AppendLine("    if (Ask 'Des corruptions ont-elles été signalées ? Lancer la réparation DISM /RestoreHealth (~15 min, internet requis)') {");
            sb.AppendLine("        DISM /Online /Cleanup-Image /RestoreHealth");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            sb.AppendLine();
        }

        // ----------------------------------------------------------- stockage
        if (cats.Contains(FaultCategory.Storage))
        {
            sb.AppendLine("Section 'Santé du stockage (lecture seule)'");
            sb.AppendLine("Get-PhysicalDisk | Select-Object FriendlyName, MediaType, HealthStatus, OperationalStatus, @{n='Taille (Go)';e={[math]::Round($_.Size/1GB)}} | Format-Table -AutoSize");
            sb.AppendLine("Get-PhysicalDisk | Get-StorageReliabilityCounter | Select-Object DeviceId, Temperature, Wear, ReadErrorsTotal, WriteErrorsTotal, PowerOnHours | Format-Table -AutoSize");
            sb.AppendLine("Write-Host 'Vérification rapide des volumes (lecture seule) :'");
            sb.AppendLine("Get-Volume | Where-Object DriveLetter | ForEach-Object { Write-Host (\"  Volume {0}: \" -f $_.DriveLetter); Repair-Volume -DriveLetter $_.DriveLetter -Scan }");
            sb.AppendLine("if (Ask 'Planifier une réparation complète du disque système (chkdsk C: /f — s''exécutera au prochain redémarrage)') {");
            sb.AppendLine("    chkdsk C: /f");
            sb.AppendLine("}");
            sb.AppendLine("Write-Host 'Si un disque est en état Avertissement/Défaillant : SAUVEGARDER puis remplacer. Vérifier aussi le firmware SSD chez le fabricant.' -ForegroundColor Yellow");
            sb.AppendLine();
        }

        // ------------------------------------------------------------ mémoire
        if (cats.Contains(FaultCategory.Memory) || cats.Contains(FaultCategory.Software))
        {
            sb.AppendLine("Section 'Mémoire'");
            sb.AppendLine("Write-Host 'Configuration du fichier d''échange :'");
            sb.AppendLine("$auto = (Get-CimInstance Win32_ComputerSystem).AutomaticManagedPagefile");
            sb.AppendLine("Write-Host (\"  Géré automatiquement : {0}\" -f $auto)");
            sb.AppendLine("Get-CimInstance Win32_PageFileUsage | Select-Object Name, @{n='Alloué (Mo)';e={$_.AllocatedBaseSize}}, @{n='Pic (Mo)';e={$_.PeakUsage}} | Format-Table -AutoSize");
            sb.AppendLine("if (-not $auto) { Write-Host '  Conseil : repasser le fichier d''échange en « géré automatiquement » sauf besoin précis.' -ForegroundColor Yellow }");

            if (cats.Contains(FaultCategory.Software))
            {
                sb.AppendLine("# Cas « mémoire épuisée par un logiciel » (virtualisation WSL/Docker/Hyper-V) :");
                sb.AppendLine("$wslCfg = Join-Path $env:USERPROFILE '.wslconfig'");
                sb.AppendLine("if (Test-Path $wslCfg) { Write-Host 'Contenu de .wslconfig :'; Get-Content $wslCfg }");
                sb.AppendLine("elseif (Get-Process -Name vmmem, vmmemWSL -ErrorAction SilentlyContinue) {");
                sb.AppendLine("    Write-Host 'vmmem détecté SANS .wslconfig : WSL2/Docker peut consommer jusqu''à ~80 % de la RAM.' -ForegroundColor Yellow");
                sb.AppendLine("    Write-Host 'Créer %USERPROFILE%\\.wslconfig avec : [wsl2]  puis  memory=8GB  (adapter), puis « wsl --shutdown ».'");
                sb.AppendLine("}");
            }

            if (cats.Contains(FaultCategory.Memory))
            {
                sb.AppendLine("if (Ask 'Planifier le diagnostic mémoire Windows (mdsched — REDÉMARRE le PC immédiatement)') {");
                sb.AppendLine("    mdsched.exe");
                sb.AppendLine("}");
                sb.AppendLine("Write-Host 'Pour un test approfondi : MemTest86 sur clé USB, 4 passes minimum, XMP désactivé pendant le test.' -ForegroundColor Yellow");
            }
            sb.AppendLine();
        }

        // ---------------------------------------------------- pilote graphique
        if (cats.Contains(FaultCategory.GpuDriver))
        {
            sb.AppendLine("Section 'Pilote graphique'");
            sb.AppendLine("Get-CimInstance Win32_VideoController | Select-Object Name, DriverVersion, DriverDate | Format-Table -AutoSize");
            sb.AppendLine("Write-Host 'Procédure recommandée (manuelle) :' -ForegroundColor Yellow");
            sb.AppendLine("Write-Host '  1. Télécharger le dernier pilote (NVIDIA/AMD/Intel) ET l''outil DDU (Display Driver Uninstaller).'");
            sb.AppendLine("Write-Host '  2. Mode sans échec > DDU > « Nettoyer et redémarrer ».'");
            sb.AppendLine("Write-Host '  3. Installer le pilote téléchargé, SANS les logiciels annexes.'");
            sb.AppendLine("Write-Host '  4. Surveiller la température GPU en charge (HWiNFO) : au-delà de ~85 °C soutenu, dépoussiérer/ventiler.'");
            sb.AppendLine();
        }

        // ------------------------------------------------------------ pilotes
        if (cats.Contains(FaultCategory.Driver) || hasBsod)
        {
            sb.AppendLine("Section 'Pilotes tiers'");
            var old = r.System.Drivers
                .Where(d => !d.IsMicrosoft && d.FileDate is { } fd && fd < DateTime.Now.AddYears(-4) &&
                            d.State.Equals("Running", StringComparison.OrdinalIgnoreCase))
                .OrderBy(d => d.FileDate).Take(15).ToList();
            if (old.Count > 0)
            {
                sb.AppendLine("Write-Host 'Pilotes tiers ANCIENS détectés par FaultTracePC (les premiers suspects en cas de BSOD) :' -ForegroundColor Yellow");
                foreach (var d in old)
                    sb.AppendLine($"Write-Host '  - {PsEscape(Path.GetFileName(d.Path))} — {PsEscape(d.CompanyName)} — {d.FileDate:dd/MM/yyyy} ({PsEscape(d.DisplayName)})'");
                sb.AppendLine("Write-Host 'Pour chacun : mettre à jour depuis le site de l''éditeur, ou désinstaller le logiciel associé s''il ne sert plus.'");
            }
            sb.AppendLine("pnputil /enum-drivers | Out-File (Join-Path $logDir 'inventaire_pilotes.txt'); Write-Host ('Inventaire complet exporté : ' + (Join-Path $logDir 'inventaire_pilotes.txt'))");
            sb.AppendLine("<#");
            sb.AppendLine("  OUTIL AVANCÉ (volontairement NON exécuté par ce script) : le Vérificateur de pilotes");
            sb.AppendLine("  « verifier /standard /all » force le pilote fautif à se révéler par un BSOD nommé…");
            sb.AppendLine("  mais peut provoquer une BOUCLE de démarrage. Ne l'utiliser que si vous savez le");
            sb.AppendLine("  désactiver en mode sans échec avec « verifier /reset ». Réservé aux techniciens.");
            sb.AppendLine("#>");
            sb.AppendLine();
        }

        // -------------------------------------------------------- alimentation
        if (cats.Contains(FaultCategory.Power) || cats.Contains(FaultCategory.Hardware))
        {
            sb.AppendLine("Section 'Alimentation / matériel'");
            sb.AppendLine("powercfg /lastwake");
            sb.AppendLine("if (Ask 'Générer le rapport d''énergie Windows (powercfg /energy, observe le système 60 s)') {");
            sb.AppendLine("    powercfg /energy /output (Join-Path $logDir 'rapport-energie.html') /duration 60");
            sb.AppendLine("    Write-Host ('Rapport : ' + (Join-Path $logDir 'rapport-energie.html'))");
            sb.AppendLine("}");
            sb.AppendLine("Write-Host 'Rappel : coupures brutales sans BSOD et erreurs WHEA ne se réparent PAS par logiciel.' -ForegroundColor Yellow");
            sb.AppendLine("Write-Host 'Vérifier physiquement : températures en charge, poussière, câbles d''alimentation, et tester avec un autre bloc si récurrent.'");
            if (cats.Contains(FaultCategory.Hardware))
                sb.AppendLine($"Write-Host 'Matériel de cette machine : CPU {PsEscape(r.System.Cpu.Name)} | Carte mère {PsEscape(r.System.Bios.BaseboardManufacturer)} {PsEscape(r.System.Bios.BaseboardProduct)} | BIOS {PsEscape(r.System.Bios.Version)}'");
            sb.AppendLine();
        }

        // ------------------------------------------------------ Windows Update
        if (cats.Contains(FaultCategory.WindowsUpdate))
        {
            sb.AppendLine("Section 'Mises à jour récentes'");
            sb.AppendLine("Get-HotFix | Sort-Object InstalledOn -Descending | Select-Object -First 10 HotFixID, Description, InstalledOn | Format-Table -AutoSize");
            sb.AppendLine("Write-Host 'Si les crashs ont commencé juste après une mise à jour précise : Paramètres > Windows Update > Historique > Désinstaller.'");
            sb.AppendLine();
        }

        // -------------------------------------------------------------- fin
        sb.AppendLine("Section 'Terminé'");
        sb.AppendLine("Write-Host 'Tests terminés. Relancer un scan FaultTracePC après redémarrage pour comparer.' -ForegroundColor Green");
        sb.AppendLine("Stop-Transcript | Out-Null");
        sb.AppendLine("Read-Host 'Appuyer sur Entrée pour fermer'");

        return sb.ToString();
    }

    /// <summary>
    /// Écrit le script .ps1 ET son lanceur .bat à côté du rapport, renseigne les chemins
    /// dans le rapport, et retourne le chemin du .ps1 (null si rien à réparer).
    ///
    /// Le .bat résout les deux blocages de Windows sur les .ps1 double-cliqués :
    ///  - ExecutionPolicy : contournée pour CETTE exécution seulement (-ExecutionPolicy Bypass,
    ///    aucun réglage système n'est modifié) ;
    ///  - droits admin : auto-élévation via Start-Process -Verb RunAs (invite UAC classique).
    /// </summary>
    public static string? WriteToDisk(DiagnosticReport r)
    {
        if (!IsRepairable(r)) return null;
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "FaultTracePC");
        Directory.CreateDirectory(dir);

        var ps1Name = $"Reparation_PC_{r.GeneratedAt:yyyy-MM-dd_HHmm}.ps1";
        var ps1Path = Path.Combine(dir, ps1Name);
        // BOM UTF-8 indispensable pour que PowerShell 5.1 affiche correctement les accents.
        File.WriteAllText(ps1Path, Generate(r), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        r.RepairScriptPath = ps1Path;

        var batPath = Path.Combine(dir, $"Reparation_PC_{r.GeneratedAt:yyyy-MM-dd_HHmm}.bat");
        var bat = "@echo off\r\n"
                + "rem FaultTracePC - lanceur du script de reparation (double-clic)\r\n"
                + "rem Demande l'elevation administrateur (UAC) puis execute le .ps1 associe.\r\n"
                + "powershell -NoProfile -Command \"Start-Process powershell -Verb RunAs -ArgumentList '-NoProfile','-ExecutionPolicy','Bypass','-File','\"\"%~dp0" + ps1Name + "\"\"'\"\r\n";
        // Encodage OEM/ASCII : les .bat n'aiment pas l'UTF-8 avec BOM (d'où l'absence d'accents ci-dessus).
        File.WriteAllText(batPath, bat, Encoding.ASCII);
        r.RepairLauncherPath = batPath;

        return ps1Path;
    }

    private static string CatLabel(FaultCategory c) => c switch
    {
        FaultCategory.Hardware => "matériel",
        FaultCategory.Memory => "mémoire RAM",
        FaultCategory.Storage => "stockage",
        FaultCategory.GpuDriver => "pilote graphique",
        FaultCategory.Driver => "pilotes",
        FaultCategory.Software => "logiciel",
        FaultCategory.Power => "alimentation",
        FaultCategory.WindowsUpdate => "Windows Update",
        _ => "général",
    };

    /// <summary>Échappe les apostrophes pour les chaînes PowerShell entre quotes simples.</summary>
    private static string PsEscape(string s) => s.Replace("'", "''");
}
