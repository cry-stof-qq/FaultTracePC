using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Media;
using FaultTracePC.Core;
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
                     "Créer un point de restauration (tout reste annulable)",
                     "Examiner l'ordinateur",
                     "Appliquer les réparations sans risque",
                     "Vérifier si le problème a disparu",
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
                $"Une réparation est déjà en cours :\n\n    {busy}\n\n" +
                "L'assistant enchaîne plusieurs réparations qui ne peuvent pas cohabiter avec celle-ci. " +
                "Attends qu'elle se termine, puis relance.",
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
        BtnStart.Content = "En cours…";
        var ct = _cts.Token;

        try
        {
            // ---------- 1. Filet de sécurité ----------
            _steps[0].Running();
            Status("Création d'un point de restauration…", 2);
            var rp = await CreateRestorePointAsync(ct);

            if (!rp.Ok)
            {
                // S'arrêter net serait la mauvaise réponse : la protection du système
                // désactivée est le cas COURANT sur un poste d'entreprise ou une
                // installation d'usine, pas une anomalie. On propose donc de l'activer,
                // et à défaut on continue — mais en ne faisant plus que ce qui n'a
                // strictement rien à annuler.
                var question = LooksLikeServiceDisabled(rp.Detail)
                    ? "Aucun point de restauration n'a pu être créé : la protection du système est DÉSACTIVÉE sur cet ordinateur.\n\n"
                      + "C'est fréquent — beaucoup de PC sortent d'usine ainsi, et certaines entreprises la désactivent.\n\n"
                    : "Aucun point de restauration n'a pu être créé.\n\nMotif : " + rp.Detail + "\n\n";

                var choice = MessageBox.Show(this,
                    question +
                    "OUI — activer la protection du système, créer le point de restauration, puis continuer normalement. " +
                    "C'est le choix recommandé : tout redevient annulable, au prix d'un peu d'espace disque.\n\n" +
                    "NON — continuer sans filet, en mode réduit. L'assistant se limitera alors aux vérifications qui " +
                    "ne modifient RIEN : examen, contrôle de l'image Windows en lecture seule, contrôle du disque en " +
                    "lecture seule, fichiers temporaires. Il ne touchera pas aux fichiers système.\n\n" +
                    "ANNULER — ne rien faire.",
                    "FaultTracePC — pas de point de restauration",
                    MessageBoxButton.YesNoCancel, MessageBoxImage.Warning, MessageBoxResult.Yes);

                if (choice == MessageBoxResult.Cancel)
                {
                    _steps[0].Failed("annulé");
                    Status("Assistant annulé.", 0);
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
                    _steps[0].Skipped("sans filet : mode réduit");
                    Log("MODE RÉDUIT : sfc et la réparation de l'image Windows sont désactivés, "
                      + "car ils modifient des fichiers système sans possibilité de retour arrière.");
                    _proposals.Add(new Proposal
                    {
                        Title = "Activer la protection du système",
                        Why = "Sans elle, aucune réparation n'est réversible et l'assistant s'interdit de toucher aux fichiers système. "
                            + "L'activer prend quelques secondes et réserve un peu d'espace disque.",
                        ButtonText = "Ouvrir les réglages",
                        Run = () => Open("SystemPropertiesProtection.exe"),
                    });
                }
            }
            else _steps[0].Done();

            // ---------- 2. Analyse initiale ----------
            _steps[1].Running();
            Status("Examen de l'ordinateur : dumps, journaux, matériel…", 6);
            var before = await ScanAsync(2, 26, ct);
            _reportPath = HtmlReportGenerator.WriteToDisk(before);
            BtnReport.IsEnabled = true;
            _steps[1].Done();
            Log($"Analyse initiale : {before.Bsods.Count} crash(s), {before.Findings.Count} conclusion(s).");

            // ---------- 3. Réparations sûres ----------
            _steps[2].Running();
            await SafeRepairsAsync(ct);
            _steps[2].Done();

            // ---------- 4. Vérification ----------
            _steps[3].Running();
            Status("Nouvelle analyse pour vérifier…", 82);
            var after = await ScanAsync(82, 96, ct);
            _reportPath = HtmlReportGenerator.WriteToDisk(after);
            _steps[3].Done();

            BuildProposals(after);
            Conclude(ToneOf(after), Sentence(before, after));
            ShowProposals();
            Status("Terminé.", 100);
            OpenInBrowser(_reportPath);
        }
        catch (OperationCanceledException)
        {
            Status("Assistant interrompu.", 0);
        }
        catch (Exception ex)
        {
            Log("ERREUR : " + ex.Message);
            Conclude("crit", "L'assistant s'est arrêté sur une erreur : " + ex.Message);
        }
        finally
        {
            _running = false;
            BtnStart.IsEnabled = true;
            BtnStart.Content = "Relancer";
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
        Log(ok ? "Point de restauration créé." : "Point de restauration IMPOSSIBLE. Motif : " + detail);
        return (ok, detail);
    }

    /// <summary>
    /// Le service est-il simplement désactivé ? C'est le cas de loin le plus
    /// fréquent — configuration d'usine de certains constructeurs, ou stratégie
    /// d'entreprise — et il se corrige en quelques secondes.
    /// </summary>
    private static bool LooksLikeServiceDisabled(string detail) =>
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
        Status("Activation de la protection du système…", 3);
        const string cmd =
            "$ok=$true;" +
            "foreach($s in 'VSS','swprv','SDRSVC'){try{Set-Service -Name $s -StartupType Manual -ErrorAction Stop}catch{$ok=$false}};" +
            "try{Start-Service -Name VSS -ErrorAction Stop}catch{$ok=$false};" +
            "try{Enable-ComputerRestore -Drive \"$env:SystemDrive\\\" -ErrorAction Stop}catch{$ok=$false;Write-Output ('ECHEC: '+$_.Exception.Message)};" +
            "if($ok){Write-Output 'OK'}";
        var (_, outp) = await RunPsAsync(cmd, ct, TimeSpan.FromMinutes(3));
        bool ok = outp.Contains("OK", StringComparison.Ordinal) && !outp.Contains("ECHEC", StringComparison.Ordinal);
        Log(ok ? "Protection du système activée." : "Activation refusée : " + Shorten(outp));
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
            Log("sfc ignoré (mode réduit) : il modifie des fichiers système et rien ne serait annulable.");
            Status("Mode réduit : les fichiers système ne sont pas touchés.", 30);
        }
        else
        {
            Status("Vérification des fichiers système (sfc)… 5 à 15 minutes.", 30);
            // sfc écrit sa sortie en UTF-16 quand elle est redirigée : sans cet
            // encodage explicite, on ne lit qu'un texte truffé de caractères nuls.
            var (_, sfc) = await RunHiddenAsync("sfc.exe", "/scannow", ct, TimeSpan.FromMinutes(30), Encoding.Unicode);
            bool sfcRepaired = sfc.Contains("réparé", StringComparison.OrdinalIgnoreCase)
                            || sfc.Contains("repaired", StringComparison.OrdinalIgnoreCase);
            Log(sfcRepaired ? "sfc : des fichiers ont été réparés." : "sfc : terminé.");
        }

        // --- Image Windows : on MESURE avant de réparer ---
        Status("Contrôle de l'image Windows (lecture seule)… 5 minutes.", 45);
        var (_, scan) = await RunPsAsync("DISM /Online /Cleanup-Image /ScanHealth | Out-String", ct, TimeSpan.FromMinutes(30));
        bool corrupt = scan.Contains("réparable", StringComparison.OrdinalIgnoreCase)
                    || scan.Contains("repairable", StringComparison.OrdinalIgnoreCase)
                    || scan.Contains("est endommagé", StringComparison.OrdinalIgnoreCase);

        if (corrupt && !_safetyNet)
        {
            Log("Corruption détectée, mais réparation ignorée (mode réduit) : elle modifie l'image Windows.");
            _proposals.Add(new Proposal
            {
                Title = "Réparer l'image Windows",
                Why = "Le contrôle en lecture seule a trouvé une corruption de l'image Windows. La réparation la corrigerait, "
                    + "mais l'assistant ne l'a pas lancée : sans point de restauration, elle ne serait pas annulable. "
                    + "Active la protection du système, puis relance l'assistant — ou lance la réparation en connaissance de cause.",
                ButtonText = "Réparer maintenant",
                Run = () => Launch("DISM /Online /Cleanup-Image /RestoreHealth"),
            });
        }
        else if (corrupt)
        {
            // Réparation lancée d'office : à ce stade c'est le correctif correct, et
            // la question « faut-il réparer le magasin de composants ? » n'a pas de
            // sens pour la personne à qui cet assistant s'adresse.
            Status("Corruption détectée — réparation de l'image Windows… 15 à 20 minutes.", 55);
            var (_, restore) = await RunPsAsync("DISM /Online /Cleanup-Image /RestoreHealth | Out-String", ct, TimeSpan.FromMinutes(45));
            bool ok = restore.Contains("terminée", StringComparison.OrdinalIgnoreCase)
                   || restore.Contains("completed successfully", StringComparison.OrdinalIgnoreCase);
            Log(ok ? "DISM : image Windows réparée." : "DISM : la réparation n'a pas abouti. " + Shorten(restore));
            if (!ok)
                _proposals.Add(new Proposal
                {
                    Title = "Réparer l'image Windows depuis une source locale",
                    Why = "La réparation automatique n'a pas abouti — le plus souvent parce que la machine n'a pas accès à Windows Update, "
                        + "ou parce qu'un serveur de mises à jour d'entreprise (WSUS) filtre les téléchargements. "
                        + "La solution est de fournir à DISM une image d'installation Windows locale avec l'option /Source.",
                    ButtonText = "Voir la marche à suivre",
                    Run = () => MessageBox.Show(this,
                        "1) Télécharger l'ISO de la MÊME version de Windows que celle installée.\n" +
                        "2) Faire un double-clic dessus pour la monter (elle apparaît comme un lecteur, par exemple D:).\n" +
                        "3) Dans un terminal administrateur :\n\n" +
                        "    DISM /Online /Cleanup-Image /RestoreHealth /Source:WIM:D:\\sources\\install.wim:1 /LimitAccess\n\n" +
                        "En remplaçant D: par la lettre du lecteur monté.",
                        "Réparer depuis une source locale", MessageBoxButton.OK, MessageBoxImage.Information),
                });
        }
        else
        {
            Log("DISM : aucune corruption détectée — réparation inutile, elle est sautée (20 minutes économisées).");
            Status("Image Windows saine — réparation inutile.", 55);
        }

        // --- Disque, en LECTURE SEULE ---
        Status("Contrôle du disque système (lecture seule)…", 66);
        var (_, vol) = await RunPsAsync("Repair-Volume -DriveLetter C -Scan | Out-String", ct, TimeSpan.FromMinutes(20));
        bool diskNeedsFix = vol.Contains("NeedsScan", StringComparison.OrdinalIgnoreCase)
                         || vol.Contains("SpotFixNeeded", StringComparison.OrdinalIgnoreCase)
                         || vol.Contains("FullRepairNeeded", StringComparison.OrdinalIgnoreCase);
        Log(diskNeedsFix ? "Disque : des corrections sont nécessaires." : "Disque : aucune anomalie signalée.");
        if (diskNeedsFix)
            _proposals.Add(new Proposal
            {
                Title = "Corriger le disque système",
                Why = "Le contrôle en lecture seule a trouvé des anomalies sur le disque. La correction ne peut se faire "
                    + "qu'au démarrage, avant le chargement de Windows : elle exige donc un redémarrage, et peut durer longtemps. "
                    + "C'est pour cette raison que l'assistant ne la lance pas de lui-même.",
                ButtonText = "Planifier au redémarrage",
                Run = () => Launch("chkdsk C: /f"),
            });

        // --- Fichiers temporaires ---
        Status("Vidage des fichiers temporaires…", 74);
        var (_, tmp) = await RunPsAsync(
            "$b=(Get-ChildItem $env:TEMP -Recurse -Force -ErrorAction SilentlyContinue|Measure-Object -Property Length -Sum).Sum;" +
            "Get-ChildItem $env:TEMP -Recurse -Force -ErrorAction SilentlyContinue|Remove-Item -Recurse -Force -ErrorAction SilentlyContinue;" +
            "$a=(Get-ChildItem $env:TEMP -Recurse -Force -ErrorAction SilentlyContinue|Measure-Object -Property Length -Sum).Sum;" +
            "Write-Output ('LIBERE:'+[math]::Round((($b-$a)/1MB),1))", ct, TimeSpan.FromMinutes(15));
        var freed = System.Text.RegularExpressions.Regex.Match(tmp, @"LIBERE:([\d.,]+)");
        Log(freed.Success ? $"Fichiers temporaires : {freed.Groups[1].Value} Mo libérés." : "Fichiers temporaires : vidés.");
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
                Title = "Traiter le pilote mis en cause",
                Why = driverFinding.Recommendation.Length > 0 ? driverFinding.Recommendation : driverFinding.Details,
                ButtonText = "Ouvrir le rapport",
                Run = () => OpenInBrowser(_reportPath),
            });

        if (after.Findings.Any(f => f.Category == FaultCategory.Memory && f.Severity == Severity.Critical))
            _proposals.Add(new Proposal
            {
                Title = "Tester la mémoire (RAM)",
                Why = "L'analyse pointe vers la mémoire. Le test redémarre immédiatement l'ordinateur et l'occupe "
                    + "plusieurs dizaines de minutes : impossible de le lancer sans ton accord. Enregistre ton travail avant.",
                ButtonText = "Lancer le test",
                Run = () => Launch("mdsched.exe"),
            });

        if (after.System.Disks.Any(d => d.Smart is { } s && (s.BadSectors > 0 || s.SpareExhausted || s.PredictedFailure == true)))
            _proposals.Add(new Proposal
            {
                Title = "Sauvegarder tes fichiers sans attendre",
                Why = "Le disque signale une dégradation. Aucune réparation logicielle ne corrige cela : ce qui compte "
                    + "maintenant est de mettre les fichiers importants à l'abri avant d'envisager un remplacement.",
                ButtonText = "Ouvrir la sauvegarde",
                Run = () => Open("control.exe", "/name Microsoft.BackupAndRestoreCenter"),
            });

        _proposals.Add(new Proposal
        {
            Title = "Installer les mises à jour Windows en attente",
            Why = "Les mises à jour, notamment celles de pilotes, corrigent une grande part des plantages. "
                + "L'assistant ne les installe pas seul : certaines demandent un redémarrage, et c'est à toi de choisir quand.",
            ButtonText = "Voir les mises à jour",
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
            return "Aucun problème sérieux détecté. L'ordinateur a été vérifié et nettoyé ; si des ralentissements ou des blocages "
                 + "persistent, active la surveillance en temps réel : elle enregistrera ce qui se passe juste avant le prochain incident.";

        var driver = crit.FirstOrDefault(f => f.Category == FaultCategory.Driver);
        if (driver is not null)
            return $"La cause la plus probable est un pilote : {driver.Title.Replace("Pilote fautif identifié", "").Trim(' ', ':', '(', ')')}. "
                 + "Les réparations sans risque ont été appliquées ; le traitement de ce pilote demande ton accord, il est proposé ci-dessous.";

        if (crit.Any(f => f.Category == FaultCategory.Storage))
            return "Le disque montre des signes de faiblesse. Aucune réparation logicielle ne corrige cela : sauvegarde tes fichiers "
                 + "sans attendre, puis fais remplacer le disque.";

        if (crit.Any(f => f.Category == FaultCategory.Hardware || f.Category == FaultCategory.Memory))
            return "Les symptômes pointent vers le matériel, pas vers un logiciel : les réparations appliquées n'y changeront rien. "
                 + "Les vérifications à faire sont proposées ci-dessous.";

        return $"{crit.Count} problème(s) sérieux subsistent après les réparations automatiques. "
             + "Le rapport complet les détaille, et les actions qui demandent ton accord sont proposées ci-dessous.";
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

        using var p = Process.Start(psi) ?? throw new InvalidOperationException($"Impossible de lancer {file}.");
        var sb = new StringBuilder();
        var outTask = p.StandardOutput.ReadToEndAsync(ct);
        var errTask = p.StandardError.ReadToEndAsync(ct);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(timeout);
        try { await p.WaitForExitAsync(timeoutCts.Token); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            try { p.Kill(entireProcessTree: true); } catch { }
            Log($"  (délai dépassé après {timeout.TotalMinutes:0} min — étape abandonnée)");
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
            "L'assistant est en cours. Interrompre maintenant peut laisser une réparation à moitié faite.\n\n" +
            "Fermer quand même ?",
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
        catch (Exception ex) { Log("Impossible de lancer la commande : " + ex.Message); }
    }

    private void Open(string file, string args = "")
    {
        try { Process.Start(new ProcessStartInfo(file, args) { UseShellExecute = true }); }
        catch (Exception ex) { Log($"Impossible d'ouvrir {file} : {ex.Message}"); }
    }

    private void OpenInBrowser(string? path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
        try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
        catch (Exception ex) { Log("Impossible d'ouvrir le rapport : " + ex.Message); }
    }

    private static string Shorten(string s)
    {
        var t = string.Join(' ', s.Split('\n').Select(x => x.Trim()).Where(x => x.Length > 0));
        return t.Length > 220 ? t[..220] + "…" : t;
    }
}
