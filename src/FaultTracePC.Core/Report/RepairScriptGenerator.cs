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
        sb.AppendLine(Lang.T("  FaultTracePC — Script d'aide à la réparation", "  FaultTracePC — Repair assistance script"));
        sb.AppendLine(Lang.T($"  Généré le {r.GeneratedAt:dd/MM/yyyy HH:mm} pour la machine {r.System.MachineName}", $"  Generated on {r.GeneratedAt:yyyy-MM-dd HH:mm} for machine {r.System.MachineName}"));
        sb.AppendLine(Lang.T($"  Basé sur {r.Findings.Count(f => f.Severity != Severity.Info)} problème(s) détecté(s) : ", $"  Based on {r.Findings.Count(f => f.Severity != Severity.Info)} problem(s) detected: ")
                      + string.Join(", ", cats.Select(CatLabel)));
        sb.AppendLine();
        sb.AppendLine(Lang.T("  RÈGLES DE CE SCRIPT :", "  RULES OF THIS SCRIPT:"));
        sb.AppendLine(Lang.T("   - Les tests en lecture seule s'exécutent automatiquement.", "   - Read-only checks run automatically."));
        sb.AppendLine(Lang.T("   - Toute action qui modifie le système demande une confirmation O/N.", "   - Every action that changes the system asks for a Y/N confirmation."));
        sb.AppendLine(Lang.T("   - Rien d'irréversible n'est lancé sans votre accord.", "   - Nothing irreversible runs without your agreement."));
        sb.AppendLine();
        sb.AppendLine(Lang.T("  Lancement : clic droit > Exécuter avec PowerShell, ou :", "  To run: right-click > Run with PowerShell, or:"));
        sb.AppendLine("    powershell -ExecutionPolicy Bypass -File .\\" + "Reparation_PC.ps1");
        sb.AppendLine("#>");
        sb.AppendLine();
        sb.AppendLine("$ErrorActionPreference = 'Continue'");
        sb.AppendLine("$logDir = Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'FaultTracePC'");
        sb.AppendLine("New-Item -ItemType Directory -Force -Path $logDir | Out-Null");
        sb.AppendLine("Start-Transcript -Path (Join-Path $logDir (\"Reparation_log_{0:yyyy-MM-dd_HHmm}.txt\" -f (Get-Date))) | Out-Null");
        sb.AppendLine();
        sb.AppendLine(Lang.T("function Ask([string]$q) { (Read-Host \"$q (O/N)\") -match '^[oOyY]' }", "function Ask([string]$q) { (Read-Host \"$q (Y/N)\") -match '^[oOyY]' }"));
        sb.AppendLine("function Section([string]$t) { Write-Host \"`n=== $t ===\" -ForegroundColor Cyan }");
        sb.AppendLine();
        sb.AppendLine(Lang.T("Write-Host 'FaultTracePC — Aide à la réparation' -ForegroundColor Green", "Write-Host 'FaultTracePC — Repair assistance' -ForegroundColor Green"));
        sb.AppendLine(Lang.T($"Write-Host 'Diagnostic du {r.GeneratedAt:dd/MM/yyyy HH:mm} — problèmes ciblés : {string.Join(", ", cats.Select(CatLabel))}'", $"Write-Host 'Diagnosis of {r.GeneratedAt:yyyy-MM-dd HH:mm} — problems targeted: {string.Join(", ", cats.Select(CatLabel))}'"));
        sb.AppendLine();

        // ------------------------------------------- filet de sécurité : restauration
        // Rien ne doit être modifié avant d'avoir un retour en arrière possible.
        // Windows bride par défaut la création de points de restauration à un
        // toutes les 24 h : on lève temporairement la bride, puis on la remet.
        sb.AppendLine(Lang.T("Section 'Filet de sécurité : point de restauration'", "Section 'Safety net: restore point'"));
        sb.AppendLine("$srKey = 'HKLM:\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\SystemRestore'");
        sb.AppendLine("$srOld = (Get-ItemProperty -Path $srKey -Name SystemRestorePointCreationFrequency -ErrorAction SilentlyContinue).SystemRestorePointCreationFrequency");
        sb.AppendLine("$srDrive = $env:SystemDrive + '\\'");
        sb.AppendLine("try { New-ItemProperty -Path $srKey -Name SystemRestorePointCreationFrequency -Value 0 -PropertyType DWord -Force | Out-Null } catch {}");
        sb.AppendLine(Lang.T("Write-Host 'Création du point de restauration (peut prendre 1 à 2 minutes)…'", "Write-Host 'Creating the restore point (may take 1 to 2 minutes)…'"));
        sb.AppendLine("$srErr = $null");
        sb.AppendLine("try { Checkpoint-Computer -Description 'FaultTracePC - avant reparation' -RestorePointType 'MODIFY_SETTINGS' -ErrorAction Stop }");
        sb.AppendLine("catch { $srErr = $_.Exception.Message }");
        sb.AppendLine("$rp = Get-ComputerRestorePoint -ErrorAction SilentlyContinue | Select-Object -Last 1");
        sb.AppendLine("if (-not $srErr -and $rp) {");
        sb.AppendLine(Lang.T("    Write-Host ('Point de restauration disponible : #{0} — {1} ({2})' -f $rp.SequenceNumber, $rp.Description, $rp.ConvertToDateTime($rp.CreationTime)) -ForegroundColor Green", "    Write-Host ('Restore point available: #{0} — {1} ({2})' -f $rp.SequenceNumber, $rp.Description, $rp.ConvertToDateTime($rp.CreationTime)) -ForegroundColor Green"));
        sb.AppendLine("} else {");
        sb.AppendLine(Lang.T("    Write-Host 'AUCUN point de restauration n''a pu être créé : les étapes suivantes ne seront PAS annulables.' -ForegroundColor Yellow", "    Write-Host 'NO restore point could be created: the following steps will NOT be reversible.' -ForegroundColor Yellow"));
        sb.AppendLine(Lang.T("    if ($srErr) { Write-Host ('  Motif : ' + $srErr) -ForegroundColor Yellow }", "    if ($srErr) { Write-Host ('  Reason: ' + $srErr) -ForegroundColor Yellow }"));
        sb.AppendLine(Lang.T("    Write-Host '  Cause la plus fréquente : la protection du système est désactivée sur ce PC.' -ForegroundColor Yellow", "    Write-Host '  Most common cause: System Protection is turned off on this PC.' -ForegroundColor Yellow"));
        sb.AppendLine(Lang.T("    Write-Host ('  Pour l''activer : Enable-ComputerRestore -Drive ' + $srDrive + '   (ou Panneau de configuration > Système > Protection du système)')", "    Write-Host ('  To turn it on: Enable-ComputerRestore -Drive ' + $srDrive + '   (or Control Panel > System > System Protection)')"));
        sb.AppendLine(Lang.T("    if (-not (Ask 'Continuer SANS filet de sécurité ?')) { Stop-Transcript | Out-Null; exit }", "    if (-not (Ask 'Continue WITHOUT a safety net?')) { Stop-Transcript | Out-Null; exit }"));
        sb.AppendLine("}");
        sb.AppendLine(Lang.T("# Remise en place du réglage d'origine de Windows (bride des 24 h).", "# Restoring the original Windows setting (the 24 h throttle)."));
        sb.AppendLine("try { if ($null -ne $srOld) { Set-ItemProperty -Path $srKey -Name SystemRestorePointCreationFrequency -Value $srOld } else { Remove-ItemProperty -Path $srKey -Name SystemRestorePointCreationFrequency -ErrorAction SilentlyContinue } } catch {}");
        sb.AppendLine(Lang.T("Write-Host 'Pour revenir en arrière plus tard : rstrui.exe'", "Write-Host 'To roll back later: rstrui.exe'"));
        sb.AppendLine();

        // ------------------------------------------------------- état général
        sb.AppendLine(Lang.T("Section 'État général'", "Section 'General state'"));
        sb.AppendLine("Get-CimInstance Win32_OperatingSystem | Select-Object Caption, Version, LastBootUpTime | Format-List");
        sb.AppendLine(Lang.T("Write-Host ('Mémoire libre : {0:N1} Go' -f ((Get-CimInstance Win32_OperatingSystem).FreePhysicalMemory/1MB))", "Write-Host ('Free memory: {0:N1} GB' -f ((Get-CimInstance Win32_OperatingSystem).FreePhysicalMemory/1MB))"));
        sb.AppendLine();

        // --------------------------------------- intégrité système (logiciel)
        if (cats.Contains(FaultCategory.Software) || cats.Contains(FaultCategory.WindowsUpdate) ||
            cats.Contains(FaultCategory.Driver) || hasBsod)
        {
            // Vérifications EXÉCUTÉES (pas seulement conseillées) : l'utilisateur voit
            // directement « déjà à jour » ou « mise à jour appliquée » — pas de conseil inutile.
            sb.AppendLine(Lang.T("Section 'Mises à jour des composants (vérification automatique)'", "Section 'Component updates (automatic check)'"));
            sb.AppendLine("if (Get-Command wsl -ErrorAction SilentlyContinue) {");
            sb.AppendLine(Lang.T("    Write-Host 'Vérification/mise à jour de WSL (virtualisation) :'", "    Write-Host 'Checking/updating WSL (virtualisation):'"));
            sb.AppendLine("    wsl --update");
            sb.AppendLine("}");
            sb.AppendLine(Lang.T("Write-Host 'Dernières mises à jour Windows installées :'", "Write-Host 'Latest Windows updates installed:'"));
            sb.AppendLine("Get-HotFix | Sort-Object InstalledOn -Descending | Select-Object -First 5 HotFixID, Description, InstalledOn | Format-Table -AutoSize");
            sb.AppendLine(Lang.T("if (Ask 'Ouvrir Windows Update pour rechercher les mises à jour en attente') {", "if (Ask 'Open Windows Update to look for pending updates') {"));
            sb.AppendLine("    Start-Process 'ms-settings:windowsupdate-action'");
            sb.AppendLine("}");
            sb.AppendLine();
            sb.AppendLine(Lang.T("Section 'Intégrité des fichiers système'", "Section 'System file integrity'"));
            sb.AppendLine(Lang.T("if (Ask 'Lancer sfc /scannow (vérifie et répare les fichiers système, ~10 min)') {", "if (Ask 'Run sfc /scannow (checks and repairs the system files, ~10 min)') {"));
            sb.AppendLine("    sfc /scannow");
            sb.AppendLine("}");
            sb.AppendLine(Lang.T("if (Ask \"Lancer DISM /ScanHealth (vérifie l'image Windows, ~5 min)\") {", "if (Ask \"Run DISM /ScanHealth (checks the Windows image, ~5 min)\") {"));
            sb.AppendLine("    DISM /Online /Cleanup-Image /ScanHealth");
            sb.AppendLine(Lang.T("    if (Ask 'Des corruptions ont-elles été signalées ? Lancer la réparation DISM /RestoreHealth (~15 min, internet requis)') {", "    if (Ask 'Was any corruption reported? Run the DISM /RestoreHealth repair (~15 min, internet required)') {"));
            sb.AppendLine("        DISM /Online /Cleanup-Image /RestoreHealth");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            sb.AppendLine();
        }

        // ----------------------------------------------------------- stockage
        if (cats.Contains(FaultCategory.Storage))
        {
            sb.AppendLine(Lang.T("Section 'Santé du stockage (lecture seule)'", "Section 'Storage health (read-only)'"));
            sb.AppendLine("Get-PhysicalDisk | Select-Object FriendlyName, MediaType, HealthStatus, OperationalStatus, @{n='Taille (Go)';e={[math]::Round($_.Size/1GB)}} | Format-Table -AutoSize");
            sb.AppendLine("Get-PhysicalDisk | Get-StorageReliabilityCounter | Select-Object DeviceId, Temperature, Wear, ReadErrorsTotal, WriteErrorsTotal, PowerOnHours | Format-Table -AutoSize");
            sb.AppendLine(Lang.T("Write-Host 'Vérification rapide des volumes (lecture seule) :'", "Write-Host 'Quick volume check (read-only):'"));
            sb.AppendLine("Get-Volume | Where-Object DriveLetter | ForEach-Object { Write-Host (\"  Volume {0}: \" -f $_.DriveLetter); Repair-Volume -DriveLetter $_.DriveLetter -Scan }");
            sb.AppendLine(Lang.T("if (Ask 'Planifier une réparation complète du disque système (chkdsk C: /f — s''exécutera au prochain redémarrage)') {", "if (Ask 'Schedule a full repair of the system drive (chkdsk C: /f — will run at the next restart)') {"));
            sb.AppendLine("    chkdsk C: /f");
            sb.AppendLine("}");
            sb.AppendLine(Lang.T("Write-Host 'Si un disque est en état Avertissement/Défaillant : SAUVEGARDER puis remplacer. Vérifier aussi le firmware SSD chez le fabricant.' -ForegroundColor Yellow", "Write-Host 'If a drive shows Warning/Unhealthy: BACK UP then replace it. Also check the SSD firmware with the manufacturer.' -ForegroundColor Yellow"));
            sb.AppendLine();
        }

        // ------------------------------------------------------------ mémoire
        if (cats.Contains(FaultCategory.Memory) || cats.Contains(FaultCategory.Software))
        {
            sb.AppendLine(Lang.T("Section 'Mémoire'", "Section 'Memory'"));
            sb.AppendLine(Lang.T("Write-Host 'Configuration du fichier d''échange :'", "Write-Host 'Page file configuration:'"));
            sb.AppendLine("$auto = (Get-CimInstance Win32_ComputerSystem).AutomaticManagedPagefile");
            sb.AppendLine(Lang.T("Write-Host (\"  Géré automatiquement : {0}\" -f $auto)", "Write-Host (\"  Managed automatically: {0}\" -f $auto)"));
            sb.AppendLine(Lang.T("Get-CimInstance Win32_PageFileUsage | Select-Object Name, @{n='Alloué (Mo)';e={$_.AllocatedBaseSize}}, @{n='Pic (Mo)';e={$_.PeakUsage}} | Format-Table -AutoSize", "Get-CimInstance Win32_PageFileUsage | Select-Object Name, @{n='Allocated (MB)';e={$_.AllocatedBaseSize}}, @{n='Peak (MB)';e={$_.PeakUsage}} | Format-Table -AutoSize"));
            sb.AppendLine(Lang.T("if (-not $auto) { Write-Host '  Conseil : repasser le fichier d''échange en « géré automatiquement » sauf besoin précis.' -ForegroundColor Yellow }", "if (-not $auto) { Write-Host '  Advice: set the page file back to “managed automatically” unless you have a specific need.' -ForegroundColor Yellow }"));

            if (cats.Contains(FaultCategory.Software))
            {
                sb.AppendLine(Lang.T("# Cas « mémoire épuisée par un logiciel » (virtualisation WSL/Docker/Hyper-V) :", "# Case “memory exhausted by software” (WSL/Docker/Hyper-V virtualisation):"));
                sb.AppendLine("$wslCfg = Join-Path $env:USERPROFILE '.wslconfig'");
                sb.AppendLine(Lang.T("if (Test-Path $wslCfg) { Write-Host 'Contenu de .wslconfig :'; Get-Content $wslCfg }", "if (Test-Path $wslCfg) { Write-Host 'Contents of .wslconfig:'; Get-Content $wslCfg }"));
                sb.AppendLine("elseif (Get-Process -Name vmmem, vmmemWSL -ErrorAction SilentlyContinue) {");
                sb.AppendLine(Lang.T("    Write-Host 'vmmem détecté SANS .wslconfig : WSL2/Docker peut consommer jusqu''à ~80 % de la RAM.' -ForegroundColor Yellow", "    Write-Host 'vmmem detected WITHOUT .wslconfig: WSL2/Docker can use up to ~80% of the RAM.' -ForegroundColor Yellow"));
                sb.AppendLine(Lang.T("    Write-Host 'Créer %USERPROFILE%\\.wslconfig avec : [wsl2]  puis  memory=8GB  (adapter), puis « wsl --shutdown ».'", "    Write-Host 'Create %USERPROFILE%\\.wslconfig with: [wsl2]  then  memory=8GB  (adjust), then “wsl --shutdown”.'"));
                sb.AppendLine("}");
            }

            if (cats.Contains(FaultCategory.Memory))
            {
                sb.AppendLine(Lang.T("if (Ask 'Planifier le diagnostic mémoire Windows (mdsched — REDÉMARRE le PC immédiatement)') {", "if (Ask 'Schedule the Windows memory diagnostic (mdsched — RESTARTS the PC immediately)') {"));
                sb.AppendLine("    mdsched.exe");
                sb.AppendLine("}");
                sb.AppendLine(Lang.T("Write-Host 'Pour un test approfondi : MemTest86 sur clé USB, 4 passes minimum, XMP désactivé pendant le test.' -ForegroundColor Yellow", "Write-Host 'For a thorough test: MemTest86 on a USB stick, 4 passes minimum, XMP disabled during the test.' -ForegroundColor Yellow"));
            }
            sb.AppendLine();
        }

        // ---------------------------------------------------- pilote graphique
        if (cats.Contains(FaultCategory.GpuDriver))
        {
            sb.AppendLine(Lang.T("Section 'Pilote graphique'", "Section 'Display driver'"));
            sb.AppendLine("Get-CimInstance Win32_VideoController | Select-Object Name, DriverVersion, DriverDate | Format-Table -AutoSize");
            sb.AppendLine(Lang.T("Write-Host 'Procédure recommandée (manuelle) :' -ForegroundColor Yellow", "Write-Host 'Recommended procedure (manual):' -ForegroundColor Yellow"));
            sb.AppendLine(Lang.T("Write-Host '  1. Télécharger le dernier pilote (NVIDIA/AMD/Intel) ET l''outil DDU (Display Driver Uninstaller).'", "Write-Host '  1. Download the latest driver (NVIDIA/AMD/Intel) AND the DDU tool (Display Driver Uninstaller).'"));
            sb.AppendLine(Lang.T("Write-Host '  2. Mode sans échec > DDU > « Nettoyer et redémarrer ».'", "Write-Host '  2. Safe mode > DDU > “Clean and restart”.'"));
            sb.AppendLine(Lang.T("Write-Host '  3. Installer le pilote téléchargé, SANS les logiciels annexes.'", "Write-Host '  3. Install the downloaded driver, WITHOUT the extra software.'"));
            sb.AppendLine(Lang.T("Write-Host '  4. Surveiller la température GPU en charge (HWiNFO) : au-delà de ~85 °C soutenu, dépoussiérer/ventiler.'", "Write-Host '  4. Watch the GPU temperature under load (HWiNFO): above ~85 °C sustained, clear the dust and improve airflow.'"));
            sb.AppendLine();
        }

        // ------------------------------------------------------------ pilotes
        if (cats.Contains(FaultCategory.Driver) || hasBsod)
        {
            sb.AppendLine(Lang.T("Section 'Pilotes tiers'", "Section 'Third-party drivers'"));
            var old = r.System.Drivers
                .Where(d => !d.IsMicrosoft && d.FileDate is { } fd && fd < DateTime.Now.AddYears(-4) &&
                            d.State.Equals("Running", StringComparison.OrdinalIgnoreCase))
                .OrderBy(d => d.FileDate).Take(15).ToList();
            if (old.Count > 0)
            {
                sb.AppendLine(Lang.T("Write-Host 'Pilotes tiers ANCIENS détectés par FaultTracePC (les premiers suspects en cas de BSOD) :' -ForegroundColor Yellow", "Write-Host 'OLD third-party drivers found by FaultTracePC (the first suspects after a BSOD):' -ForegroundColor Yellow"));
                foreach (var d in old)
                {
                    // Lang.T sorti de l'interpolation : à l'intérieur d'un trou, il
                    // est invisible au contrôle de traduction qui lit les sources.
                    var quand = d.FileDate is { } dateFichier
                        ? Lang.Date(dateFichier)
                        : Lang.T("date inconnue", "date unknown");
                    sb.AppendLine($"Write-Host '  - {PsEscape(Path.GetFileName(d.Path))} — {PsEscape(d.CompanyName)} — {quand} ({PsEscape(d.DisplayName)})'");
                }
                sb.AppendLine(Lang.T("Write-Host 'Pour chacun : mettre à jour depuis le site de l''éditeur, ou désinstaller le logiciel associé s''il ne sert plus.'", "Write-Host 'For each: update from the vendor website, or uninstall the associated software if it is no longer used.'"));
            }
            sb.AppendLine(Lang.T("pnputil /enum-drivers | Out-File (Join-Path $logDir 'inventaire_pilotes.txt'); Write-Host ('Inventaire complet exporté : ' + (Join-Path $logDir 'inventaire_pilotes.txt'))", "Write-Host ('Full inventory exported: ' + (Join-Path $logDir 'inventaire_pilotes.txt'))"));
            sb.AppendLine("<#");
            sb.AppendLine(Lang.T("  OUTIL AVANCÉ (volontairement NON exécuté par ce script) : le Vérificateur de pilotes", "  ADVANCED TOOL (deliberately NOT run by this script): Driver Verifier"));
            sb.AppendLine(Lang.T("  « verifier /standard /all » force le pilote fautif à se révéler par un BSOD nommé…", "  “verifier /standard /all” forces the faulty driver to reveal itself through a named BSOD…"));
            sb.AppendLine(Lang.T("  mais peut provoquer une BOUCLE de démarrage. Ne l'utiliser que si vous savez le", "  but it can cause a boot LOOP. Use it only if you know how to disable it"));
            sb.AppendLine(Lang.T("  désactiver en mode sans échec avec « verifier /reset ». Réservé aux techniciens.", "  from safe mode with “verifier /reset”. For technicians only."));
            sb.AppendLine("#>");
            sb.AppendLine();
        }

        // -------------------------------------------------------- alimentation
        if (cats.Contains(FaultCategory.Power) || cats.Contains(FaultCategory.Hardware))
        {
            sb.AppendLine(Lang.T("Section 'Alimentation / matériel'", "Section 'Power / hardware'"));
            sb.AppendLine("powercfg /lastwake");
            sb.AppendLine(Lang.T("if (Ask 'Générer le rapport d''énergie Windows (powercfg /energy, observe le système 60 s)') {", "if (Ask 'Generate the Windows energy report (powercfg /energy, observes the system for 60 s)') {"));
            sb.AppendLine("    powercfg /energy /output (Join-Path $logDir 'rapport-energie.html') /duration 60");
            sb.AppendLine(Lang.T("    Write-Host ('Rapport : ' + (Join-Path $logDir 'rapport-energie.html'))", "    Write-Host ('Report: ' + (Join-Path $logDir 'rapport-energie.html'))"));
            sb.AppendLine("}");
            sb.AppendLine(Lang.T("Write-Host 'Rappel : coupures brutales sans BSOD et erreurs WHEA ne se réparent PAS par logiciel.' -ForegroundColor Yellow", "Write-Host 'Reminder: abrupt power losses without a BSOD and WHEA errors are NOT fixed by software.' -ForegroundColor Yellow"));
            sb.AppendLine(Lang.T("Write-Host 'Vérifier physiquement : températures en charge, poussière, câbles d''alimentation, et tester avec un autre bloc si récurrent.'", "Write-Host 'Check physically: temperatures under load, dust, power cables, and test with another PSU if it recurs.'"));
            if (cats.Contains(FaultCategory.Hardware))
                sb.AppendLine(Lang.T($"Write-Host 'Matériel de cette machine : CPU {PsEscape(r.System.Cpu.Name)} | Carte mère {PsEscape(r.System.Bios.BaseboardManufacturer)} {PsEscape(r.System.Bios.BaseboardProduct)} | BIOS {PsEscape(r.System.Bios.Version)}'", $"Write-Host 'Hardware of this machine: CPU {PsEscape(r.System.Cpu.Name)} | Motherboard {PsEscape(r.System.Bios.BaseboardManufacturer)} {PsEscape(r.System.Bios.BaseboardProduct)} | BIOS {PsEscape(r.System.Bios.Version)}'"));
            sb.AppendLine();
        }

        // ------------------------------------------------------ Windows Update
        if (cats.Contains(FaultCategory.WindowsUpdate))
        {
            sb.AppendLine(Lang.T("Section 'Mises à jour récentes'", "Section 'Recent updates'"));
            sb.AppendLine("Get-HotFix | Sort-Object InstalledOn -Descending | Select-Object -First 10 HotFixID, Description, InstalledOn | Format-Table -AutoSize");
            sb.AppendLine(Lang.T("Write-Host 'Si les crashs ont commencé juste après une mise à jour précise : Paramètres > Windows Update > Historique > Désinstaller.'", "Write-Host 'If the crashes started right after one particular update: Settings > Windows Update > Update history > Uninstall.'"));
            sb.AppendLine();
        }

        // -------------------------------------------------------------- fin
        sb.AppendLine(Lang.T("Section 'Terminé'", "Section 'Done'"));
        sb.AppendLine(Lang.T("Write-Host 'Tests terminés. Relancer un scan FaultTracePC après redémarrage pour comparer.' -ForegroundColor Green", "Write-Host 'Checks complete. Run a FaultTracePC scan again after restarting to compare.' -ForegroundColor Green"));
        sb.AppendLine("Stop-Transcript | Out-Null");
        sb.AppendLine(Lang.T("Read-Host 'Appuyer sur Entrée pour fermer'", "Read-Host 'Press Enter to close'"));

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
                // -NoExit : une strategie de groupe peut refuser le .ps1 avant sa
                // premiere ligne. Sans cette option, la console se refermerait sur
                // le refus sans que personne puisse le lire.
                + "powershell -NoProfile -Command \"Start-Process powershell -Verb RunAs -ArgumentList '-NoProfile','-ExecutionPolicy','Bypass','-NoExit','-File','\"\"%~dp0" + ps1Name + "\"\"'\"\r\n";
        // Encodage OEM/ASCII : les .bat n'aiment pas l'UTF-8 avec BOM (d'où l'absence d'accents ci-dessus).
        File.WriteAllText(batPath, bat, Encoding.ASCII);
        r.RepairLauncherPath = batPath;

        return ps1Path;
    }

    private static string CatLabel(FaultCategory c) => c switch
    {
        FaultCategory.Hardware => Lang.T("matériel", "hardware"),
        FaultCategory.Memory => Lang.T("mémoire RAM", "RAM"),
        FaultCategory.Storage => Lang.T("stockage", "storage"),
        FaultCategory.GpuDriver => Lang.T("pilote graphique", "display driver"),
        FaultCategory.Driver => Lang.T("pilotes", "drivers"),
        FaultCategory.Software => Lang.T("logiciel", "software"),
        FaultCategory.Power => Lang.T("alimentation", "power"),
        FaultCategory.WindowsUpdate => "Windows Update",
        _ => Lang.T("général", "general"),
    };

    /// <summary>
    /// Rend un texte sûr à l'intérieur d'une chaîne PowerShell entre quotes simples.
    ///
    /// L'APOSTROPHE DROITE NE SUFFIT PAS, ET ÇA A CASSÉ UN SCRIPT EN VRAI.
    /// Le 19/08/2026, sur le PC d'un tiers, le script généré n'a pas démarré du
    /// tout : erreur d'analyse, aucune réparation exécutée. En cause, la
    /// description d'un pilote Intel — « Pilote v2 I2C d’E/S série Intel(R) ».
    ///
    /// Ce « ’ » est U+2019, pas l'apostrophe droite. Or la documentation de
    /// PowerShell est explicite : « PowerShell treats smart quotation marks, also
    /// called typographic or curly quotes, AS NORMAL QUOTATION MARKS for strings ».
    /// La chaîne se terminait donc à « d’ », et la fin de la ligne était lue comme
    /// du code — puis l'apostrophe finale ouvrait une nouvelle chaîne qui avalait
    /// la ligne suivante.
    ///
    /// Aucun contrôle ne pouvait le voir : le générateur produisait un texte
    /// parfaitement valide en C#, et le défaut n'existait qu'aux yeux de
    /// l'interpréteur qui le relit. Le déclencheur — une description de pilote
    /// française contenant une apostrophe — est très fréquent.
    ///
    /// On ramène donc toute la famille des guillemets simples typographiques à
    /// l'apostrophe droite AVANT de doubler. Le texte affiché y perd sa
    /// typographie ; dans une console PowerShell, la différence est invisible, et
    /// un script qui démarre vaut mieux qu'une apostrophe élégante.
    /// </summary>
    internal static string PsEscape(string s) =>
        (s ?? "")
            .Replace('\u2018', '\'')   // ‘ guillemet-apostrophe culbuté
            .Replace('\u2019', '\'')   // ’ guillemet-apostrophe — le coupable
            .Replace('\u201B', '\'')   // ‛ guillemet-apostrophe culbuté réfléchi
            .Replace('\u2032', '\'')   // ′ prime, que PowerShell traite aussi comme une apostrophe
            .Replace("'", "''");
}
