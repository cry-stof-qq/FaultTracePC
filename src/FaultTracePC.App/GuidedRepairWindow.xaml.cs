using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Media;
using FaultTracePC.Core;
using FaultTracePC.Core.Repair;
using FaultTracePC.Core.Report;

namespace FaultTracePC.App;

/// <summary>
/// Mode « je ne sais pas ce que j'ai » : un seul bouton, pour quelqu'un qui ne
/// sait pas ce qu'est un pilote.
///
/// La ligne de partage, et c'est la seule règle qui compte ici :
///
///   · AUTOMATIQUE — ce qui ne peut rien casser et ne demande aucun arbitrage :
///     vérification des fichiers système, réparation de l'image Windows,
///     contrôle du disque EN LECTURE SEULE, vidage des fichiers temporaires.
///   · PROPOSÉ À LA FIN — tout ce qui redémarre la machine, installe, désinstalle,
///     ou modifie durablement quelque chose. Une par une, avec la raison.
///
/// Un utilisateur qui ouvre cette fenêtre n'a pas les moyens d'arbitrer une
/// question technique. Lui en poser une serait lui refiler une décision qu'il ne
/// peut pas prendre. On décide donc à sa place — mais uniquement là où se tromper
/// est sans conséquence, et jamais ailleurs.
/// </summary>
public partial class GuidedRepairWindow : Window
{
    private readonly ObservableCollection<StepVm> _steps = new();
    private readonly ObservableCollection<Proposal> _proposals = new();
    private CancellationTokenSource? _cts;
    private bool _running;
    private string? _reportPath;

    /// <summary>Faux si aucun point de restauration n'existe : on s'interdit alors
    /// toute action qui modifie des fichiers système.</summary>
    private bool _safetyNet = true;

    public GuidedRepairWindow()
    {
        InitializeComponent();
        IcSteps.ItemsSource = _steps;
        IcProposals.ItemsSource = _proposals;

        foreach (var label in new[]
                 {
                     Lang.T("Créer un point de restauration (tout reste annulable)", "Create a restore point (everything stays reversible)"),
                     Lang.T("Examiner l'ordinateur", "Examine the computer"),
                     Lang.T("Appliquer les réparations sans risque", "Apply the risk-free repairs"),
                     Lang.T("Vérifier si le problème a disparu", "Check whether the problem is gone"),
                 })
            _steps.Add(new StepVm(label));
    }

    // ==================================================================
    // Modèles d'affichage
    // ==================================================================

    public sealed class StepVm : INotifyPropertyChanged
    {
        public StepVm(string label) => Label = label;

        private string _icon = "○";
        private string _label = "";
        private Brush _brush = new SolidColorBrush(Color.FromRgb(0x6B, 0x7C, 0x91));

        public string Icon { get => _icon; set { _icon = value; Raise(); } }
        public string Label { get => _label; set { _label = value; Raise(); } }
        public Brush Brush { get => _brush; set { _brush = value; Raise(); } }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void Raise([CallerMemberName] string? n = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        public void Running() { Icon = "▶"; Brush = new SolidColorBrush(Color.FromRgb(0x24, 0x70, 0xB3)); }
        public void Done() { Icon = "✔"; Brush = new SolidColorBrush(Color.FromRgb(0x27, 0xAE, 0x60)); }
        public void Skipped(string why) { Icon = "–"; Label += $" — {why}"; Brush = new SolidColorBrush(Color.FromRgb(0x6B, 0x7C, 0x91)); }
        public void Failed(string why) { Icon = "✘"; Label += $" — {why}"; Brush = new SolidColorBrush(Color.FromRgb(0xC0, 0x39, 0x2B)); }
    }

    /// <summary>Action non anodine, proposée à la fin et jamais exécutée d'office.</summary>
    public sealed class Proposal
    {
        public required string Title { get; init; }
        public required string Why { get; init; }
        public required string ButtonText { get; init; }
        public required Action Run { get; init; }
    }

    // ==================================================================
    // Déroulement
    // ==================================================================

    private void BtnStart_Click(object sender, RoutedEventArgs e)
    {
        // Refus net, sans dialogue à choix multiples : cet assistant enchaîne sfc,
        // DISM et l'analyse disque. Les faire cohabiter avec une réparation déjà
        // lancée est exactement le conflit que le verrou existe pour empêcher, et
        // l'utilisateur visé ici n'a pas les moyens d'arbitrer le risque.
        if (RunningTools.BlockingLabel() is { } busy)
        {
            MessageBox.Show(this,
                Lang.T($"Une réparation est déjà en cours :\n\n    {busy}\n\n", $"A repair is already running:") +
                Lang.T("L'assistant enchaîne plusieurs réparations qui ne peuvent pas cohabiter avec celle-ci. ", "The assistant chains several repairs that cannot coexist with this one. ") +
                Lang.T("Attends qu'elle se termine, puis relance.", "Wait for it to finish, then start again."),
                "FaultTracePC", MessageBoxButton.OK, MessageBoxImage.Warning);
            RunningTools.FocusBlocking();
            return;
        }
        _ = RunAllAsync();
    }

    private async Task RunAllAsync()
    {
        if (_running) return;
        _running = true;
        _cts = new CancellationTokenSource();
        _safetyNet = true;
        _proposals.Clear();
        ConclusionBorder.Visibility = Visibility.Collapsed;
        TxtProposalsIntro.Visibility = Visibility.Collapsed;
        BtnStart.IsEnabled = false;
        BtnStart.Content = Lang.T("En cours…", "Running…");
        var ct = _cts.Token;

        try
        {
            // ---------- 1. Filet de sécurité ----------
            _steps[0].Running();
            Status(Lang.T("Création d'un point de restauration…", "Creating a restore point…"), 2);
            var rp = await CreateRestorePointAsync(ct);

            if (!rp.Ok)
            {
                // S'arrêter net serait la mauvaise réponse : la protection du système
                // désactivée est le cas COURANT sur un poste d'entreprise ou une
                // installation d'usine, pas une anomalie. On propose donc de l'activer,
                // et à défaut on continue — mais en ne faisant plus que ce qui n'a
                // strictement rien à annuler.
                var question = LooksLikeServiceDisabled(rp.Detail)
                    ? Lang.T("Aucun point de restauration n'a pu être créé : la protection du système est DÉSACTIVÉE sur cet ordinateur.\n\n", "No restore point could be created: system protection is DISABLED on this computer.")
                      + Lang.T("C'est fréquent — beaucoup de PC sortent d'usine ainsi, et certaines entreprises la désactivent.\n\n", "This is common — many PCs ship this way, and some companies disable it.")
                    : Lang.T("Aucun point de restauration n'a pu être créé.\n\nMotif : ", "No restore point could be created.\n\nReason: ") + rp.Detail + "\n\n";

                var choice = MessageBox.Show(this,
                    question +
                    Lang.T("OUI — activer la protection du système, créer le point de restauration, puis continuer normalement. ", "YES — turn on system protection, create the restore point, then carry on normally. ") +
                    Lang.T("C'est le choix recommandé : tout redevient annulable, au prix d'un peu d'espace disque.\n\n", "This is the recommended choice: everything becomes reversible again, at the cost of a little disk space.") +
                    Lang.T("NON — continuer sans filet, en mode réduit. L'assistant se limitera alors aux vérifications qui ", "NO — carry on without a safety net, in reduced mode. The assistant will then limit itself to the checks that ") +
                    Lang.T("ne modifient RIEN : examen, contrôle de l'image Windows en lecture seule, contrôle du disque en ", "change NOTHING: examination, read-only Windows image check, read-only disk ") +
                    Lang.T("lecture seule, fichiers temporaires. Il ne touchera pas aux fichiers système.\n\n", "check, temporary files. It will not touch system files.") +
                    Lang.T("ANNULER — ne rien faire.", "CANCEL — do nothing."),
                    Lang.T("FaultTracePC — pas de point de restauration", "FaultTracePC — no restore point"),
                    MessageBoxButton.YesNoCancel, MessageBoxImage.Warning, MessageBoxResult.Yes);

                if (choice == MessageBoxResult.Cancel)
                {
                    _steps[0].Failed(Lang.T("annulé", "cancelled"));
                    Status(Lang.T("Assistant annulé.", "Assistant cancelled."), 0);
                    return;
                }

                if (choice == MessageBoxResult.Yes && await EnableSystemProtectionAsync(ct))
                {
                    rp = await CreateRestorePointAsync(ct);
                }

                if (rp.Ok) _steps[0].Done();
                else
                {
                    _safetyNet = false;
                    _steps[0].Skipped(Lang.T("sans filet : mode réduit", "no safety net: reduced mode"));
                    Log(Lang.T("MODE RÉDUIT : sfc et la réparation de l'image Windows sont désactivés, ", "REDUCED MODE: sfc and the Windows image repair are disabled, ")
                      + Lang.T("car ils modifient des fichiers système sans possibilité de retour arrière.", "because they change system files with no way back."));
                    _proposals.Add(new Proposal
                    {
                        Title = Lang.T("Activer la protection du système", "Turn on system protection"),
                        Why = Lang.T("Sans elle, aucune réparation n'est réversible et l'assistant s'interdit de toucher aux fichiers système. ", "Without it no repair is reversible, and the assistant forbids itself from touching system files. ")
                            + Lang.T("L'activer prend quelques secondes et réserve un peu d'espace disque.", "Turning it on takes a few seconds and reserves a little disk space."),
                        ButtonText = Lang.T("Ouvrir les réglages", "Open the settings"),
                        Run = () => Open("SystemPropertiesProtection.exe"),
                    });
                }
            }
            else _steps[0].Done();

            // ---------- 2. Analyse initiale ----------
            _steps[1].Running();
            Status(Lang.T("Examen de l'ordinateur : dumps, journaux, matériel…", "Examining the computer: dumps, logs, hardware…"), 6);
            var before = await ScanAsync(2, 26, ct);
            _reportPath = HtmlReportGenerator.WriteToDisk(before);
            BtnReport.IsEnabled = true;
            _steps[1].Done();
            Log(Lang.T($"Analyse initiale : {before.Bsods.Count} crash(s), {before.Findings.Count} conclusion(s).", $"Initial analysis: {before.Bsods.Count} crash(es), {before.Findings.Count} conclusion(s)."));

            // ---------- 3. Réparations sûres ----------
            _steps[2].Running();
            await SafeRepairsAsync(ct);
            _steps[2].Done();

            // ---------- 4. Vérification ----------
            _steps[3].Running();
            Status(Lang.T("Nouvelle analyse pour vérifier…", "Running the analysis again to check…"), 82);
            var after = await ScanAsync(82, 96, ct);
            _reportPath = HtmlReportGenerator.WriteToDisk(after);
            _steps[3].Done();

            BuildProposals(after);
            Conclude(ToneOf(after), Sentence(before, after));
            ShowProposals();
            Status(Lang.T("Terminé.", "Done."), 100);
            OpenInBrowser(_reportPath);
        }
        catch (OperationCanceledException)
        {
            Status(Lang.T("Assistant interrompu.", "Assistant interrupted."), 0);
        }
        catch (Exception ex)
        {
            Log(Lang.T("ERREUR : ", "ERROR: ") + ex.Message);
            Conclude("crit", Lang.T("L'assistant s'est arrêté sur une erreur : ", "The assistant stopped on an error: ") + ex.Message);
        }
        finally
        {
            _running = false;
            BtnStart.IsEnabled = true;
            BtnStart.Content = Lang.T("Relancer", "Start again");
        }
    }

    // ==================================================================
    // Étape 1 — point de restauration
    // ==================================================================

    private async Task<(bool Ok, string Detail)> CreateRestorePointAsync(CancellationToken ct)
    {
        // La bride Windows limite à un point toutes les 24 h : on la lève le temps
        // de l'opération, puis on remet le réglage d'origine.
        const string cmd =
            "$k='HKLM:\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\SystemRestore';" +
            "$old=(Get-ItemProperty -Path $k -Name SystemRestorePointCreationFrequency -ErrorAction SilentlyContinue).SystemRestorePointCreationFrequency;" +
            "try{New-ItemProperty -Path $k -Name SystemRestorePointCreationFrequency -Value 0 -PropertyType DWord -Force|Out-Null}catch{};" +
            "$err=$null;" +
            "try{Checkpoint-Computer -Description 'FaultTracePC - assistant guide' -RestorePointType 'MODIFY_SETTINGS' -ErrorAction Stop}catch{$err=$_.Exception.Message};" +
            "try{if($null -ne $old){Set-ItemProperty -Path $k -Name SystemRestorePointCreationFrequency -Value $old}else{Remove-ItemProperty -Path $k -Name SystemRestorePointCreationFrequency -ErrorAction SilentlyContinue}}catch{};" +
            "if($err){Write-Output ('ECHEC: '+$err)}else{Write-Output 'OK'}";

        var (code, output) = await RunPsAsync(cmd, ct, TimeSpan.FromMinutes(5));

        bool ok = code == 0 && output.Contains("OK", StringComparison.Ordinal)
                            && !output.Contains("ECHEC", StringComparison.Ordinal);
        var detail = ok ? "" : Shorten(output);
        Log(ok ? Lang.T("Point de restauration créé.", "Restore point created.") : Lang.T("Point de restauration IMPOSSIBLE. Motif : ", "Restore point IMPOSSIBLE. Reason: ") + detail);
        return (ok, detail);
    }

    /// <summary>
    /// Le service est-il simplement désactivé ? C'est le cas de loin le plus
    /// fréquent — configuration d'usine de certains constructeurs, ou stratégie
    /// d'entreprise — et il se corrige en quelques secondes.
    /// </summary>
    private static bool LooksLikeServiceDisabled(string detail) =>
        // pas-de-traduction : fragment de la sortie de Windows, pas de la nôtre.
        detail.Contains("désactivé", StringComparison.OrdinalIgnoreCase)
        || detail.Contains("disabled", StringComparison.OrdinalIgnoreCase)
        || detail.Contains("0x80070422", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Réactive la protection du système : les services requis, puis la protection
    /// du volume système. Ce n'est PAS lancé d'office — l'utilisateur le demande
    /// explicitement, car cela modifie durablement un réglage de la machine et
    /// réserve de l'espace disque.
    /// </summary>
    private async Task<bool> EnableSystemProtectionAsync(CancellationToken ct)
    {
        Status(Lang.T("Activation de la protection du système…", "Turning on system protection…"), 3);
        const string cmd =
            "$ok=$true;" +
            "foreach($s in 'VSS','swprv','SDRSVC'){try{Set-Service -Name $s -StartupType Manual -ErrorAction Stop}catch{$ok=$false}};" +
            "try{Start-Service -Name VSS -ErrorAction Stop}catch{$ok=$false};" +
            "try{Enable-ComputerRestore -Drive \"$env:SystemDrive\\\" -ErrorAction Stop}catch{$ok=$false;Write-Output ('ECHEC: '+$_.Exception.Message)};" +
            "if($ok){Write-Output 'OK'}";
        var (_, outp) = await RunPsAsync(cmd, ct, TimeSpan.FromMinutes(3));
        bool ok = outp.Contains("OK", StringComparison.Ordinal) && !outp.Contains("ECHEC", StringComparison.Ordinal);
        Log(ok ? Lang.T("Protection du système activée.", "System protection turned on.") : Lang.T("Activation refusée : ", "Activation refused: ") + Shorten(outp));
        return ok;
    }

    // ==================================================================
    // Étape 3 — réparations sans risque
    // ==================================================================

    private async Task SafeRepairsAsync(CancellationToken ct)
    {
        // --- Fichiers système ---
        // sfc REMPLACE des fichiers système. C'est une réparation, pas une
        // destruction — mais sans point de restauration, elle n'est pas annulable.
        // On s'en abstient donc plutôt que de pratiquer un irréversible discret.
        if (!_safetyNet)
        {
            Log(Lang.T("sfc ignoré (mode réduit) : il modifie des fichiers système et rien ne serait annulable.", "sfc skipped (reduced mode): it changes system files and nothing would be reversible."));
            Status(Lang.T("Mode réduit : les fichiers système ne sont pas touchés.", "Reduced mode: system files are left alone."), 30);
        }
        else
        {
            Status(Lang.T("Vérification des fichiers système (sfc)… 5 à 15 minutes.", "Checking the system files (sfc)… 5 to 15 minutes."), 30);
            // sfc écrit sa sortie en UTF-16 quand elle est redirigée : sans cet
            // encodage explicite, on ne lit qu'un texte truffé de caractères nuls.
            var (_, sfc) = await RunHiddenAsync("sfc.exe", "/scannow", ct, TimeSpan.FromMinutes(30), Encoding.Unicode);
            switch (RepairOutput.ReadSfc(sfc))
            {
                case SfcResult.Repaired:
                    Log(Lang.T("sfc : des fichiers système endommagés ont été trouvés et réparés.", "sfc: damaged system files were found and repaired."));
                    break;

                case SfcResult.NoViolations:
                    Log(Lang.T("sfc : aucun fichier système endommagé.", "sfc: no damaged system file."));
                    break;

                case SfcResult.RepairIncomplete:
                    // Le cas que la version précédente confondait avec un succès.
                    Log(Lang.T("sfc : des fichiers endommagés n'ont PAS pu être réparés.", "sfc: damaged files could NOT be repaired."));
                    _proposals.Add(new Proposal
                    {
                        Title = Lang.T("Des fichiers système restent endommagés", "Some system files are still damaged"),
                        Why = Lang.T("sfc a trouvé des fichiers système endommagés et n'a pas pu tous les réparer. ", "sfc found damaged system files and could not repair them all. ")
                            + Lang.T("C'est presque toujours que l'image de référence dans laquelle il puise ses fichiers sains ", "Almost always this means the reference image it draws its healthy files from ")
                            + Lang.T("est elle-même abîmée : il faut donc réparer l'image (DISM) PUIS relancer sfc, dans cet ordre. ", "is itself damaged: the image must therefore be repaired (DISM) THEN sfc run again, in that order. ")
                            + Lang.T("Le détail des fichiers concernés est écrit dans %windir%\\Logs\\CBS\\CBS.log.", "The list of affected files is written to %windir%\\Logs\\CBS\\CBS.log."),
                        ButtonText = Lang.T("Relancer sfc en visible", "Run sfc in a visible window"),
                        Run = () => Launch("sfc /scannow"),
                    });
                    break;

                case SfcResult.CouldNotRun:
                    Log(Lang.T("sfc n'a pas pu s'exécuter — son compte rendu figure dans le journal ci-dessus.", "sfc could not run — its output appears in the log above."));
                    break;

                default:
                    // On ne conclut RIEN. Annoncer « terminé » sur un compte rendu
                    // qu'on n'a pas su lire, c'est présenter une machine peut-être
                    // abîmée comme saine.
                    Log(Lang.T("sfc a terminé, mais son compte rendu n'a pas pu être lu : aucune conclusion sur les fichiers système.", "sfc finished, but its output could not be read: no conclusion about the system files."));
                    _proposals.Add(new Proposal
                    {
                        Title = Lang.T("Vérifier les fichiers système soi-même", "Check the system files yourself"),
                        Why = Lang.T("La vérification a bien été exécutée, mais l'assistant n'a pas su lire sa conclusion — ", "The check did run, but the assistant could not read its conclusion — ")
                            + Lang.T("Windows l'écrit dans sa langue d'affichage, et celle-ci ne fait pas partie de celles qu'il sait analyser. ", "Windows writes it in its display language, and that language is not one it can parse. ")
                            + Lang.T("Plutôt que d'affirmer que tout va bien sans l'avoir lu, il ne conclut pas.", "Rather than claiming all is well without having read it, it draws no conclusion."),
                        ButtonText = Lang.T("Relancer sfc en visible", "Run sfc in a visible window"),
                        Run = () => Launch("sfc /scannow"),
                    });
                    break;
            }
        }

        // --- Image Windows : on MESURE avant de réparer ---
        Status(Lang.T("Contrôle de l'image Windows (lecture seule)… 5 minutes.", "Checking the Windows image (read-only)… 5 minutes."), 45);
        var scan = await RunDismAsync("/Online /Cleanup-Image /ScanHealth", ct, TimeSpan.FromMinutes(30));
        var image = RepairOutput.ReadImageScan(scan);
        bool corrupt = image is ImageHealth.Repairable or ImageHealth.NotRepairable;

        if (image == ImageHealth.Unreadable)
        {
            Log(Lang.T("DISM : compte rendu illisible — aucune conclusion sur l'image Windows.", "DISM: unreadable output — no conclusion about the Windows image."));
            _proposals.Add(new Proposal
            {
                Title = Lang.T("Vérifier l'image Windows soi-même", "Check the Windows image yourself"),
                Why = Lang.T("Le contrôle a bien été exécuté, mais l'assistant n'a pas su lire sa conclusion. ", "The check did run, but the assistant could not read its conclusion. ")
                    + Lang.T("Il ne lancera donc aucune réparation : la déclencher sans avoir lu le diagnostic serait ", "It will therefore start no repair: triggering one without having read the diagnosis would be ")
                    + Lang.T("aussi faux que d'annoncer une image saine. La commande ci-dessous ne modifie rien, ", "as wrong as announcing a healthy image. The command below changes nothing, ")
                    + Lang.T("elle affiche le résultat à lire directement.", "it shows the result to read directly."),
                ButtonText = Lang.T("Lancer le contrôle en visible", "Run the check in a visible window"),
                Run = () => Launch("DISM /Online /Cleanup-Image /ScanHealth"),
            });
        }
        else if (image == ImageHealth.NotRepairable)
        {
            // Windows le dit lui-même : inutile de faire tourner /RestoreHealth
            // vingt minutes pour se voir opposer le même refus.
            Log(Lang.T("DISM : l'image Windows est endommagée et se déclare NON réparable par ce moyen.", "DISM: the Windows image is damaged and reports itself NOT repairable this way."));
            _proposals.Add(SourceLocaleProposal());
        }
        else if (corrupt && !_safetyNet)
        {
            Log(Lang.T("Corruption détectée, mais réparation ignorée (mode réduit) : elle modifie l'image Windows.", "Corruption found, but the repair was skipped (reduced mode): it changes the Windows image."));
            _proposals.Add(new Proposal
            {
                Title = Lang.T("Réparer l'image Windows", "Repair the Windows image"),
                Why = Lang.T("Le contrôle en lecture seule a trouvé une corruption de l'image Windows. La réparation la corrigerait, ", "The read-only check found corruption in the Windows image. The repair would fix it, ")
                    + Lang.T("mais l'assistant ne l'a pas lancée : sans point de restauration, elle ne serait pas annulable. ", "but the assistant did not start it: without a restore point it would not be reversible. ")
                    + Lang.T("Active la protection du système, puis relance l'assistant — ou lance la réparation en connaissance de cause.", "Turn on system protection, then run the assistant again — or start the repair knowing what it means."),
                ButtonText = Lang.T("Réparer maintenant", "Repair now"),
                Run = () => Launch("DISM /Online /Cleanup-Image /RestoreHealth"),
            });
        }
        else if (corrupt)
        {
            // Réparation lancée d'office : à ce stade c'est le correctif correct, et
            // la question « faut-il réparer le magasin de composants ? » n'a pas de
            // sens pour la personne à qui cet assistant s'adresse.
            Status(Lang.T("Corruption détectée — réparation de l'image Windows… 15 à 20 minutes.", "Corruption found — repairing the Windows image… 15 to 20 minutes."), 55);
            var restore = await RunDismAsync("/Online /Cleanup-Image /RestoreHealth", ct, TimeSpan.FromMinutes(45));
            var issue = RepairOutput.ReadImageRepair(restore);
            Log(issue switch
            {
                ImageRepair.Completed => Lang.T("DISM : image Windows réparée.", "DISM: Windows image repaired."),
                ImageRepair.Failed => Lang.T("DISM : la réparation n'a pas abouti. ", "DISM: the repair did not succeed. ") + Shorten(restore),
                _ => Lang.T("DISM : compte rendu de réparation illisible — je ne peux pas affirmer qu'elle a abouti. ", "DISM: unreadable repair output — I cannot state that it succeeded. ") + Shorten(restore),
            });
            // Illisible est traité comme un échec : proposer la marche à suivre ne
            // coûte rien, laisser croire à une réparation réussie coûte cher.
            if (issue != ImageRepair.Completed)
                _proposals.Add(SourceLocaleProposal());
        }
        else
        {
            Log(Lang.T("DISM : aucune corruption détectée — réparation inutile, elle est sautée (20 minutes économisées).", "DISM: no corruption found — the repair is pointless and is skipped (20 minutes saved)."));
            Status(Lang.T("Image Windows saine — réparation inutile.", "Windows image healthy — repair unnecessary."), 55);
        }

        // --- Disque, en LECTURE SEULE ---
        Status(Lang.T("Contrôle du disque système (lecture seule)…", "Checking the system disk (read-only)…"), 66);
        var (_, vol) = await RunPsAsync("Repair-Volume -DriveLetter C -Scan | Out-String", ct, TimeSpan.FromMinutes(20));
        var volume = RepairOutput.ReadVolumeScan(vol);
        bool diskNeedsFix = volume == VolumeScan.NeedsRepair;
        Log(volume switch
        {
            VolumeScan.NeedsRepair => Lang.T("Disque : des corrections sont nécessaires.", "Disk: corrections are needed."),
            VolumeScan.NoErrors => Lang.T("Disque : aucune anomalie signalée.", "Disk: no anomaly reported."),
            _ => Lang.T("Disque : compte rendu illisible — aucune conclusion sur le système de fichiers.", "Disk: unreadable output — no conclusion about the file system."),
        });
        if (diskNeedsFix)
            _proposals.Add(new Proposal
            {
                Title = Lang.T("Corriger le disque système", "Fix the system disk"),
                Why = Lang.T("Le contrôle en lecture seule a trouvé des anomalies sur le disque. La correction ne peut se faire ", "The read-only check found anomalies on the disk. The fix can only run ")
                    + Lang.T("qu'au démarrage, avant le chargement de Windows : elle exige donc un redémarrage, et peut durer longtemps. ", "at boot, before Windows loads: it therefore requires a restart, and can take a long time. ")
                    + Lang.T("C'est pour cette raison que l'assistant ne la lance pas de lui-même.", "That is why the assistant does not start it by itself."),
                ButtonText = Lang.T("Planifier au redémarrage", "Schedule at restart"),
                Run = () => Launch("chkdsk C: /f"),
            });

        // --- Fichiers temporaires ---
        Status(Lang.T("Vidage des fichiers temporaires…", "Emptying the temporary files…"), 74);
        var (_, tmp) = await RunPsAsync(
            "$b=(Get-ChildItem $env:TEMP -Recurse -Force -ErrorAction SilentlyContinue|Measure-Object -Property Length -Sum).Sum;" +
            "Get-ChildItem $env:TEMP -Recurse -Force -ErrorAction SilentlyContinue|Remove-Item -Recurse -Force -ErrorAction SilentlyContinue;" +
            "$a=(Get-ChildItem $env:TEMP -Recurse -Force -ErrorAction SilentlyContinue|Measure-Object -Property Length -Sum).Sum;" +
            "Write-Output ('LIBERE:'+[math]::Round((($b-$a)/1MB),1))", ct, TimeSpan.FromMinutes(15));
        var freed = System.Text.RegularExpressions.Regex.Match(tmp, @"LIBERE:([\d.,]+)");
        Log(freed.Success ? Lang.T($"Fichiers temporaires : {freed.Groups[1].Value} Mo libérés.", $"Temporary files: {freed.Groups[1].Value} MB freed.") : Lang.T("Fichiers temporaires : vidés.", "Temporary files: emptied."));
    }

    // ==================================================================
    // Étape 4 — conclusion et propositions
    // ==================================================================

    private void BuildProposals(DiagnosticReport after)
    {
        // Pilote nommé par l'analyse : l'action la plus utile qui existe.
        var driverFinding = after.Findings.FirstOrDefault(f =>
            f.Category == FaultCategory.Driver && f.Severity == Severity.Critical);
        if (driverFinding is not null)
            _proposals.Add(new Proposal
            {
                Title = Lang.T("Traiter le pilote mis en cause", "Deal with the driver named"),
                Why = driverFinding.Recommendation.Length > 0 ? driverFinding.Recommendation : driverFinding.Details,
                ButtonText = Lang.T("Ouvrir le rapport", "Open the report"),
                Run = () => OpenInBrowser(_reportPath),
            });

        if (after.Findings.Any(f => f.Category == FaultCategory.Memory && f.Severity == Severity.Critical))
            _proposals.Add(new Proposal
            {
                Title = Lang.T("Tester la mémoire (RAM)", "Test the memory (RAM)"),
                Why = Lang.T("L'analyse pointe vers la mémoire. Le test redémarre immédiatement l'ordinateur et l'occupe ", "The analysis points to the memory. The test restarts the computer immediately and keeps it busy ")
                    + Lang.T("plusieurs dizaines de minutes : impossible de le lancer sans ton accord. Enregistre ton travail avant.", "for several tens of minutes: it cannot be started without your agreement. Save your work first."),
                ButtonText = Lang.T("Lancer le test", "Start the test"),
                Run = () => Launch("mdsched.exe"),
            });

        if (after.System.Disks.Any(d => d.Smart is { } s && (s.BadSectors > 0 || s.SpareExhausted || s.PredictedFailure == true)))
            _proposals.Add(new Proposal
            {
                Title = Lang.T("Sauvegarder tes fichiers sans attendre", "Back up your files right away"),
                Why = Lang.T("Le disque signale une dégradation. Aucune réparation logicielle ne corrige cela : ce qui compte ", "The disk is reporting degradation. No software repair fixes that: what matters ")
                    + Lang.T("maintenant est de mettre les fichiers importants à l'abri avant d'envisager un remplacement.", "now is getting the important files to safety before considering a replacement."),
                ButtonText = Lang.T("Ouvrir la sauvegarde", "Open Backup"),
                Run = () => Open("control.exe", "/name Microsoft.BackupAndRestoreCenter"),
            });

        _proposals.Add(new Proposal
        {
            Title = Lang.T("Installer les mises à jour Windows en attente", "Install the pending Windows updates"),
            Why = Lang.T("Les mises à jour, notamment celles de pilotes, corrigent une grande part des plantages. ", "Updates, driver updates above all, fix a large share of crashes. ")
                + Lang.T("L'assistant ne les installe pas seul : certaines demandent un redémarrage, et c'est à toi de choisir quand.", "The assistant does not install them on its own: some require a restart, and when is your call."),
            ButtonText = Lang.T("Voir les mises à jour", "See the updates"),
            Run = () => new WindowsUpdateWindow { Owner = this }.Show(),
        });
    }

    private static string ToneOf(DiagnosticReport r) =>
        r.Findings.Any(f => f.Severity == Severity.Critical) ? "crit"
        : r.Findings.Any(f => f.Severity == Severity.Warning) ? "warn" : "ok";

    /// <summary>La phrase unique : ce qu'on a trouvé, ce qu'on a fait, ce qu'il reste.</summary>
    private string Sentence(DiagnosticReport before, DiagnosticReport after)
    {
        var crit = after.Findings.Where(f => f.Severity == Severity.Critical).ToList();

        if (crit.Count == 0 && after.Bsods.Count == 0)
            return Lang.T("Aucun problème sérieux détecté. L'ordinateur a été vérifié et nettoyé ; si des ralentissements ou des blocages ", "No serious problem found. The computer was checked and cleaned; if slowdowns or freezes ")
                 + Lang.T("persistent, active la surveillance en temps réel : elle enregistrera ce qui se passe juste avant le prochain incident.", "persist, turn on real-time monitoring: it will record what happens just before the next incident.");

        // Le nom du pilote se lit dans Subject, posé par le moteur de règles.
        // Le reconstituer en découpant Title reviendrait à décider sur du texte
        // traduit : le découpage tomberait à côté dès que la langue change.
        var driver = crit.FirstOrDefault(f => f.Code == "driver.identified")
                  ?? crit.FirstOrDefault(f => f.Category == FaultCategory.Driver);
        if (driver is not null)
        {
            var name = driver.Subject.Length > 0 ? driver.Subject : driver.Title;
            return Lang.T($"La cause la plus probable est un pilote : {name}. ", $"The most likely cause is a driver: {name}. ")
                 + Lang.T("Les réparations sans risque ont été appliquées ; le traitement de ce pilote demande ton accord, il est proposé ci-dessous.",
                          "The risk-free repairs have been applied; dealing with this driver needs your agreement, and is offered below.");
        }

        if (crit.Any(f => f.Category == FaultCategory.Storage))
            return Lang.T("Le disque montre des signes de faiblesse. Aucune réparation logicielle ne corrige cela : sauvegarde tes fichiers ", "The disk is showing signs of weakness. No software repair fixes that: back up your files ")
                 + Lang.T("sans attendre, puis fais remplacer le disque.", "right away, then have the disk replaced.");

        if (crit.Any(f => f.Category == FaultCategory.Hardware || f.Category == FaultCategory.Memory))
            return Lang.T("Les symptômes pointent vers le matériel, pas vers un logiciel : les réparations appliquées n'y changeront rien. ", "The symptoms point to the hardware, not to software: the repairs applied will change nothing there. ")
                 + Lang.T("Les vérifications à faire sont proposées ci-dessous.", "The checks worth doing are offered below.");

        return Lang.T($"{crit.Count} problème(s) sérieux subsistent après les réparations automatiques. ", $"{crit.Count} serious problem(s) remain after the automatic repairs. ")
             + Lang.T("Le rapport complet les détaille, et les actions qui demandent ton accord sont proposées ci-dessous.", "The full report details them, and the actions needing your agreement are offered below.");
    }

    // ==================================================================
    // Exécution de commandes, sans fenêtre visible
    // ==================================================================

    /// <summary>
    /// Exécute une commande PowerShell sans fenêtre, en forçant la sortie en UTF-8.
    ///
    /// Sans le préambule [Console]::OutputEncoding, PowerShell écrit dans la page de
    /// codes OEM de la console (850 en français) : redirigée puis lue en UTF-8, la
    /// sortie devient « le service est d‚sactiv‚ ». Un message d'erreur illisible au
    /// moment précis où l'utilisateur en a besoin, c'est pire que pas de message.
    /// </summary>
    /// <summary>
    /// Marche à suivre quand la réparation par Windows Update est hors de portée :
    /// fournir soi-même une image d'installation. Le cas est fréquent en
    /// établissement, où les postes n'atteignent pas Windows Update directement.
    /// </summary>
    private Proposal SourceLocaleProposal() => new()
    {
        Title = Lang.T("Réparer l'image Windows depuis une source locale", "Repair the Windows image from a local source"),
        Why = Lang.T("La réparation automatique n'a pas abouti — le plus souvent parce que la machine n'a pas accès à Windows Update, ", "The automatic repair did not succeed — most often because the machine cannot reach Windows Update, ")
            + Lang.T("ou parce qu'un serveur de mises à jour d'entreprise (WSUS) filtre les téléchargements. ", "or because a corporate update server (WSUS) is filtering the downloads. ")
            + Lang.T("La solution est de fournir à DISM une image d'installation Windows locale avec l'option /Source.", "The answer is to give DISM a local Windows installation image through the /Source option."),
        ButtonText = Lang.T("Voir la marche à suivre", "See how to do it"),
        Run = () => MessageBox.Show(this,
            Lang.T("1) Télécharger l'ISO de la MÊME version de Windows que celle installée.\n", "1) Download the ISO of the SAME Windows version as the one installed.\n") +
            Lang.T("2) Faire un double-clic dessus pour la monter (elle apparaît comme un lecteur, par exemple D:).\n", "2) Double-click it to mount it (it shows up as a drive, D: for example).\n") +
            Lang.T("3) Dans un terminal administrateur :\n\n", "3) In an administrator terminal:\n\n") +
            "    DISM /Online /Cleanup-Image /RestoreHealth /Source:WIM:D:\\sources\\install.wim:1 /LimitAccess\n\n" +
            Lang.T("En remplaçant D: par la lettre du lecteur monté.", "Replacing D: with the letter of the mounted drive."),
            Lang.T("Réparer depuis une source locale", "Repair from a local source"), MessageBoxButton.OK, MessageBoxImage.Information),
    };

    /// <summary>
    /// Exécute DISM en forçant sa sortie en anglais (/English, option globale
    /// documentée). Ce n'est pas un choix d'affichage : c'est le seul moyen de
    /// rendre le compte rendu lisible PAR LE PROGRAMME quelle que soit la langue
    /// du poste. Là où c'est un HUMAIN qui lit — la boîte à outils, qui ouvre une
    /// console visible — l'option n'est surtout pas ajoutée : la sortie doit alors
    /// être dans la langue de la personne.
    ///
    /// La documentation Microsoft prévient que « certaines ressources ne peuvent
    /// pas être affichées en anglais » : la lecture reconnaît donc encore le
    /// français, et sait dire qu'elle n'a pas su lire.
    /// </summary>
    private async Task<string> RunDismAsync(string args, CancellationToken ct, TimeSpan timeout)
    {
        var (_, output) = await RunPsAsync($"DISM {args} /English | Out-String", ct, timeout);
        if (RepairOutput.RejectedEnglishOption(output))
        {
            Log(Lang.T("DISM a refusé l'option /English : nouvelle tentative sans elle.", "DISM refused the /English option: trying again without it."));
            (_, output) = await RunPsAsync($"DISM {args} | Out-String", ct, timeout);
        }
        return output;
    }

    private Task<(int Code, string Output)> RunPsAsync(string command, CancellationToken ct, TimeSpan timeout) =>
        RunHiddenAsync("powershell.exe",
            "-NoProfile -ExecutionPolicy Bypass -Command \"[Console]::OutputEncoding=[System.Text.Encoding]::UTF8; "
            + command.Replace("\"", "\\\"") + "\"",
            ct, timeout, Encoding.UTF8);

    private async Task<(int Code, string Output)> RunHiddenAsync(
        string file, string args, CancellationToken ct, TimeSpan timeout, Encoding? encoding = null)
    {
        Log($"> {file} {args}");
        var psi = new ProcessStartInfo(file, args)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        if (encoding is not null) { psi.StandardOutputEncoding = encoding; psi.StandardErrorEncoding = encoding; }

        using var p = Process.Start(psi) ?? throw new InvalidOperationException(Lang.T($"Impossible de lancer {file}.", $"Cannot start {file}."));
        var sb = new StringBuilder();
        var outTask = p.StandardOutput.ReadToEndAsync(ct);
        var errTask = p.StandardError.ReadToEndAsync(ct);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(timeout);
        try { await p.WaitForExitAsync(timeoutCts.Token); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            try { p.Kill(entireProcessTree: true); } catch { }
            Log(Lang.T($"  (délai dépassé après {timeout.TotalMinutes:0} min — étape abandonnée)", $"  (timed out after {timeout.TotalMinutes:0} min — step abandoned)"));
            return (-1, "");
        }

        sb.Append(await outTask).Append(await errTask);
        var text = sb.ToString();
        foreach (var line in text.Split('\n').Select(l => l.Trim('\r', ' ')).Where(l => l.Length > 0).TakeLast(4))
            Log("  " + line);
        return (p.ExitCode, text);
    }

    private async Task<DiagnosticReport> ScanAsync(int from, int to, CancellationToken ct)
    {
        var progress = new Progress<ScanProgress>(sp =>
        {
            TxtStatus.Text = sp.Step;
            PbMain.Value = from + (to - from) * sp.Percent / 100.0;
        });
        return await new ScanOrchestrator().RunAsync(
            new ScanOptions { Days = 30, IncludeDrivers = true, DeepDumpAnalysis = true }, progress, ct);
    }

    // ==================================================================
    // Utilitaires d'interface
    // ==================================================================

    private void Status(string text, double percent)
    {
        TxtStatus.Text = text;
        PbMain.Value = percent;
        Log("— " + text);
    }

    private void Log(string line)
    {
        TxtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {line}{Environment.NewLine}");
        TxtLog.ScrollToEnd();
    }

    private void Conclude(string tone, string sentence)
    {
        var (bg, border) = tone switch
        {
            "crit" => (Color.FromRgb(0xFD, 0xED, 0xEC), Color.FromRgb(0xC0, 0x39, 0x2B)),
            "warn" => (Color.FromRgb(0xFE, 0xF5, 0xE7), Color.FromRgb(0xE6, 0x7E, 0x22)),
            _ => (Color.FromRgb(0xEA, 0xF7, 0xF0), Color.FromRgb(0x27, 0xAE, 0x60)),
        };
        ConclusionBorder.Background = new SolidColorBrush(bg);
        ConclusionBorder.BorderBrush = new SolidColorBrush(border);
        ConclusionBorder.Visibility = Visibility.Visible;
        TxtConclusion.Text = sentence;
    }

    private void ShowProposals() =>
        TxtProposalsIntro.Visibility = _proposals.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

    private void BtnProposal_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is Proposal p) p.Run();
    }

    private void BtnReport_Click(object sender, RoutedEventArgs e) => OpenInBrowser(_reportPath);

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        if (!_running) return;
        var r = MessageBox.Show(this,
            Lang.T("L'assistant est en cours. Interrompre maintenant peut laisser une réparation à moitié faite.\n\n", "The assistant is running. Stopping now can leave a repair half done.\n\n") +
            Lang.T("Fermer quand même ?", "Close anyway?"),
            "FaultTracePC", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
        if (r != MessageBoxResult.Yes) { e.Cancel = true; return; }
        _cts?.Cancel();
    }

    private void Launch(string command)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -NoExit -Command \"{command}\"",
                UseShellExecute = true,
            });
        }
        catch (Exception ex) { Log(Lang.T("Impossible de lancer la commande : ", "Cannot start the command: ") + ex.Message); }
    }

    private void Open(string file, string args = "")
    {
        try { Process.Start(new ProcessStartInfo(file, args) { UseShellExecute = true }); }
        catch (Exception ex) { Log(Lang.T($"Impossible d'ouvrir {file} : {ex.Message}", $"Cannot open {file}: {ex.Message}")); }
    }

    private void OpenInBrowser(string? path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
        try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
        catch (Exception ex) { Log(Lang.T("Impossible d'ouvrir le rapport : ", "Cannot open the report: ") + ex.Message); }
    }

    private static string Shorten(string s)
    {
        var t = string.Join(' ', s.Split('\n').Select(x => x.Trim()).Where(x => x.Length > 0));
        return t.Length > 220 ? t[..220] + "…" : t;
    }
}
