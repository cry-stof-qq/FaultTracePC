using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using FaultTracePC.Core;

namespace FaultTracePC.App;

/// <summary>
/// Console « maître » : interroge les machines en mode Client (état temps réel,
/// dernier rapport) via leur API en lecture seule. La liste des machines est
/// enregistrée dans Documents\FaultTracePC\parc.json.
/// </summary>
public partial class ParkWindow : Window
{
    private static string ParkFile => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "FaultTracePC", "parc.json");

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(4) };

    /// <summary>Client dédié aux diagnostics distants : un scan complet peut durer plusieurs minutes.</summary>
    private static readonly HttpClient ScanHttp = new() { Timeout = TimeSpan.FromMinutes(15) };

    private List<ParkMachine> _machines = new();

    public ParkWindow()
    {
        InitializeComponent();
        LoadMachines();
        if (_machines.Count > 0) _ = RefreshAllAsync();
    }

    // ------------------------------------------------------------------
    // Persistance de la liste
    // ------------------------------------------------------------------

    public sealed class ParkMachine
    {
        public string Name { get; set; } = "";
        public string Host { get; set; } = "";
        public int Port { get; set; } = 58620;
        public string Token { get; set; } = "";
    }

    private void LoadMachines()
    {
        try
        {
            if (File.Exists(ParkFile))
                _machines = JsonSerializer.Deserialize<List<ParkMachine>>(File.ReadAllText(ParkFile)) ?? new();
        }
        catch { _machines = new(); }
        RenderRows(null);
    }

    private void SaveMachines()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ParkFile)!);
            File.WriteAllText(ParkFile, JsonSerializer.Serialize(_machines, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, Lang.T("Impossible d'enregistrer la liste : ", "Could not save the list: ") + ex.Message, "FaultTracePC",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    // ------------------------------------------------------------------
    // Actions
    // ------------------------------------------------------------------

    private void BtnAdd_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TxtHost.Text) || string.IsNullOrWhiteSpace(TxtToken.Text))
        {
            MessageBox.Show(this, Lang.T("Hôte et token sont obligatoires (le token s'obtient sur la machine cliente, fenêtre 🌐 Mode réseau).", "Host and token are required (the token is obtained on the client machine, 🌐 Network mode window)."),
                "FaultTracePC", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        _machines.Add(new ParkMachine
        {
            Name = string.IsNullOrWhiteSpace(TxtName.Text) ? TxtHost.Text : TxtName.Text.Trim(),
            Host = TxtHost.Text.Trim(),
            Port = int.TryParse(TxtPort.Text, out var p) && p is >= 1024 and <= 65535 ? p : 58620,
            Token = TxtToken.Text.Trim(),
        });
        SaveMachines();
        TxtName.Clear(); TxtHost.Clear(); TxtToken.Clear();
        _ = RefreshAllAsync();
    }

    private void BtnRemove_Click(object sender, RoutedEventArgs e)
    {
        if (LvMachines.SelectedItem is not Row row)
        {
            MessageBox.Show(this,
                Lang.T("Sélectionne d'abord une machine dans la liste, puis clique sur « Retirer la sélection ».", "Select a machine in the list first, then click “Remove the selection”."),
                "FaultTracePC", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (MessageBox.Show(this,
                Lang.T($"Retirer « {row.Name} » ({row.Host}) de la console de parc ?\n\n", $"Remove “{row.Name}” ({row.Host}) from the fleet console?\n\n") +
                Lang.T("Cela retire seulement la machine de TA liste de supervision :\n", "This only removes the machine from YOUR monitoring list:\n") +
                Lang.T("• rien n'est désinstallé sur le poste distant ;\n", "• nothing is uninstalled on the remote machine;\n") +
                Lang.T("• son historique de scans local n'est pas touché ;\n", "• its local scan history is untouched;\n") +
                Lang.T("• tu pourras la rajouter plus tard avec son nom, son adresse et son jeton.", "• you can add it back later with its name, address and token."),
                Lang.T("FaultTracePC — retirer une machine", "FaultTracePC — remove a machine"),
                MessageBoxButton.OKCancel, MessageBoxImage.Question, MessageBoxResult.Cancel) != MessageBoxResult.OK)
            return;

        // Attention : la colonne « Host » affichée vaut « hôte:port ».
        // Comparer m.Host à row.Host ne correspond JAMAIS — la machine n'était
        // alors pas réellement retirée (bogue corrigé en 1.1).
        int removed = _machines.RemoveAll(m => m.Name == row.Name && $"{m.Host}:{m.Port}" == row.Host);
        SaveMachines();

        // On repart des derniers résultats connus : les autres machines gardent
        // leur état affiché au lieu de repasser en « inconnu ».
        foreach (var k in _lastResults.Keys.Where(k => k.Name == row.Name && $"{k.Host}:{k.Port}" == row.Host).ToList())
            _lastResults.Remove(k);
        RenderRows(_lastResults);

        TxtStatus.Text = removed > 0
            ? Lang.T($"« {row.Name} » retirée de la liste. {_machines.Count} machine(s) supervisée(s).", $"“{row.Name}” removed from the list. {_machines.Count} machine(s) monitored.")
            : Lang.T($"« {row.Name} » n'était plus dans la liste.", $"“{row.Name}” was no longer in the list.");
    }

    private void BtnRefresh_Click(object sender, RoutedEventArgs e) => _ = RefreshAllAsync();

    /// <summary>Dernier état connu, réutilisé pour le rapport de parc.</summary>
    private Dictionary<ParkMachine, QueryResult> _lastResults = new();

    private async Task RefreshAllAsync()
    {
        TxtStatus.Text = Lang.T($"Interrogation de {_machines.Count} machine(s)…", $"Querying {_machines.Count} machine(s)…");
        var results = await Task.WhenAll(_machines.Select(QueryAsync));
        _lastResults = results.ToDictionary(r => r.Machine, r => r);
        RenderRows(_lastResults);

        // Synthèse des versions : la question posée devant un parc n'est pas
        // « quelle version tourne où » mais « qu'est-ce que je dois mettre à jour,
        // et est-ce que c'est ma console ou les postes ».
        var joignables = results.Where(r => r.Ok).ToList();
        var enRetard = joignables.Count(r => string.IsNullOrEmpty(r.Version)
                                          || (Version.TryParse(r.Version, out var v)
                                              && Version.TryParse(ConsoleVersion, out var loc) && v < loc));
        var enAvance = joignables.Count(r => Version.TryParse(r.Version, out var v)
                                          && Version.TryParse(ConsoleVersion, out var loc) && v > loc);

        var versions = enAvance > 0
            ? Lang.T($" ⚠ {enAvance} poste(s) plus récent(s) que cette console ({ConsoleVersion}) — c'est ELLE qu'il faut mettre à jour.", $" ⚠ {enAvance} machine(s) newer than this console ({ConsoleVersion}) — it is THE CONSOLE that needs updating.")
            : enRetard > 0
                ? Lang.T($" ⬆ {enRetard} poste(s) à mettre à jour vers la {ConsoleVersion}.", $" ⬆ {enRetard} machine(s) to update to {ConsoleVersion}.")
                : joignables.Count > 0 ? Lang.T($" Tous les postes joignables sont en {ConsoleVersion}.", $" All reachable machines are on {ConsoleVersion}.") : "";

        TxtStatus.Text = Lang.T($"Actualisé à {DateTime.Now:HH:mm:ss} — {joignables.Count}/{_machines.Count} machine(s) joignable(s).", $"Refreshed at {DateTime.Now:HH:mm:ss} — {joignables.Count}/{_machines.Count} machine(s) reachable.") + versions;
    }

    /// <summary>Génère et ouvre le rapport HTML consolidé du parc.</summary>
    private async void BtnParkReport_Click(object sender, RoutedEventArgs e)
    {
        if (_machines.Count == 0)
        {
            MessageBox.Show(this, Lang.T("Aucune machine enregistrée. Ajoute d'abord tes postes clients.", "No machine registered. Add your client machines first."),
                "FaultTracePC", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        try
        {
            // On repart d'un état frais : un rapport doit refléter l'instant présent.
            TxtStatus.Text = Lang.T("Interrogation des machines pour le rapport…", "Querying the machines for the report…");
            await RefreshAllAsync();

            var lines = _machines.Select(m =>
            {
                _lastResults.TryGetValue(m, out var r);
                var alerts = r?.Alerts ?? new List<PreventiveAlert>();
                var latest = alerts.OrderByDescending(a => a.Time).FirstOrDefault();
                return new ParkReportGenerator.MachineLine
                {
                    Name = m.Name,
                    Host = $"{m.Host}:{m.Port}",
                    Reachable = r?.Ok ?? false,
                    MonitoringActive = r?.Active ?? false,
                    Error = r?.Error ?? Lang.T("non interrogée", "not queried"),
                    LastSample = r?.Last?.Time,
                    CpuLoad = r?.Last?.CpuLoad,
                    CpuTemp = r?.Last?.CpuTemp,
                    GpuTemp = r?.Last?.GpuTemp,
                    MemPct = r?.Last?.MemPct,
                    TopProcesses = r?.Last?.TopProcesses ?? "",
                    CriticalAlerts = alerts.Count(a => a.Level == "crit"),
                    WarningAlerts = alerts.Count(a => a.Level != "crit"),
                    LastAlert = latest is null ? "" : $"{Lang.ShortDateMinute(latest.Time)} — {latest.Title}",
                };
            }).ToList();

            // Comparateur de parc : on rapatrie le résumé du dernier scan de chaque
            // poste joignable. C'est la seule information qui permette de corréler —
            // les relevés temps réel disent l'état, pas l'inventaire.
            TxtStatus.Text = Lang.T("Récupération des résumés d'analyse pour la comparaison…", "Fetching the analysis summaries for the comparison…");
            var summaries = new List<Core.Analysis.ParkComparator.MachineSummary>();
            foreach (var m in _machines)
            {
                try
                {
                    using var req = SignedRequest(m, HttpMethod.Get, "/api/summary");
                    using var resp = await Http.SendAsync(req);
                    if (!resp.IsSuccessStatusCode) continue;
                    var json = await resp.Content.ReadAsStringAsync();
                    if (JsonSerializer.Deserialize<Core.Report.ScanHistory.ScanSummary>(json) is { } sum)
                        summaries.Add(new Core.Analysis.ParkComparator.MachineSummary(m.Name, sum));
                }
                catch { /* poste injoignable ou jamais analysé : il sort simplement de la comparaison */ }
            }

            var comparison = Core.Analysis.ParkComparator.Analyze(summaries);
            var path = ParkReportGenerator.WriteToDisk(lines, comparison);
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            TxtStatus.Text = Lang.T($"Rapport du parc généré : {path}", $"Fleet report generated: {path}");
        }
        catch (Exception ex)
        {
            TxtStatus.Text = Lang.T("Échec de la génération du rapport : ", "Report generation failed: ") + ex.Message;
        }
    }

    private sealed record QueryResult(ParkMachine Machine, bool Ok, bool Active, FlightSample? Last, string Error)
    {
        /// <summary>Alertes préventives récupérées lors de la même interrogation.</summary>
        public List<PreventiveAlert> Alerts { get; init; } = new();

        /// <summary>
        /// Version du poste, telle qu'il l'annonce. Vide si le client est antérieur
        /// à la 1.2.2 : le champ n'existait pas, ce n'est pas une erreur.
        /// </summary>
        public string Version { get; init; } = "";
    }

    /// <summary>
    /// Construit une requête signée : le token sert de clé HMAC et ne quitte
    /// jamais cette machine — seule la signature circule.
    /// </summary>
    private static HttpRequestMessage SignedRequest(ParkMachine m, HttpMethod method, string path, string query = "")
    {
        var host = m.Host.Trim().Trim('/');
        var url = $"http://{host}:{m.Port}{path}" + (query.Length > 0 ? "?" + query : "");
        var req = new HttpRequestMessage(method, url);
        foreach (var (name, value) in RemoteConfig.BuildAuthHeaders(m.Token, method.Method, path, query))
            req.Headers.Add(name, value);
        return req;
    }

    private static async Task<QueryResult> QueryAsync(ParkMachine m)
    {
        try
        {
            using var req = SignedRequest(m, HttpMethod.Get, "/api/status");
            using var resp = await Http.SendAsync(req);
            if (!resp.IsSuccessStatusCode)
                return new(m, false, false, null, resp.StatusCode == System.Net.HttpStatusCode.Forbidden
                    ? Lang.T("refusé (token ou horloge décalée ?)", "refused (token or clock skew?)") : $"HTTP {(int)resp.StatusCode}");

            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            bool active = doc.RootElement.TryGetProperty("active", out var a) && a.GetBoolean();
            FlightSample? last = null;
            if (doc.RootElement.TryGetProperty("lastSample", out var ls) && ls.ValueKind == JsonValueKind.Object)
                last = ls.Deserialize<FlightSample>();

            // Champ apparu en 1.2.2 : absent chez les clients antérieurs, ce qui est
            // une information en soi (« ce poste est en retard ») et non une panne.
            var version = doc.RootElement.TryGetProperty("version", out var v) && v.ValueKind == JsonValueKind.String
                ? v.GetString() ?? "" : "";

            // Alertes préventives des 7 derniers jours (best effort : une machine
            // avec un service ancien n'expose pas encore cet endpoint).
            var alerts = new List<PreventiveAlert>();
            try
            {
                using var alertReq = SignedRequest(m, HttpMethod.Get, "/api/alerts", "days=7");
                using var alertResp = await Http.SendAsync(alertReq);
                if (alertResp.IsSuccessStatusCode)
                    alerts = JsonSerializer.Deserialize<List<PreventiveAlert>>(
                        await alertResp.Content.ReadAsStringAsync()) ?? new();
            }
            catch { }

            return new(m, true, active, last, "") { Alerts = alerts, Version = version };
        }
        catch (Exception ex)
        {
            return new(m, false, false, null, ex is TaskCanceledException ? Lang.T("délai dépassé", "timed out") : Lang.T("injoignable", "unreachable"));
        }
    }

    // ------------------------------------------------------------------
    // Affichage
    // ------------------------------------------------------------------

    public sealed class Row
    {
        public string Name { get; set; } = "";
        public string Host { get; set; } = "";
        public string Etat { get; set; } = "";
        public string DernierReleve { get; set; } = "";
        public string Cpu { get; set; } = "";
        public string TempCpu { get; set; } = "";
        public string TempGpu { get; set; } = "";
        public string Ram { get; set; } = "";
        public string Top { get; set; } = "";

        /// <summary>Version du poste, comparée à celle de cette console.</summary>
        public string Version { get; set; } = "";
    }

    /// <summary>Version de cette console — la référence à laquelle les postes sont comparés.</summary>
    private static string ConsoleVersion => UpdateChecker.CurrentVersion.ToString(3);

    /// <summary>
    /// Compare la version d'un poste à celle de la console.
    ///
    /// Volontairement symétrique : c'est parfois la CONSOLE qui est en retard, et
    /// une colonne qui ne saurait dire que « le poste est vieux » ferait mettre à
    /// jour la mauvaise machine — exactement la question qu'on se pose devant un parc.
    /// </summary>
    private static string DescribeVersion(QueryResult? r)
    {
        if (r is null || !r.Ok) return "";
        if (string.IsNullOrEmpty(r.Version)) return Lang.T("⬆ antérieure à 1.2.2", "⬆ older than 1.2.2");

        if (!Version.TryParse(r.Version, out var remote) || !Version.TryParse(ConsoleVersion, out var local))
            return r.Version;

        var cmp = remote.CompareTo(local);
        return cmp < 0 ? Lang.T($"⬆ {r.Version} — à mettre à jour", $"⬆ {r.Version} — to be updated")
             : cmp > 0 ? Lang.T($"⚠ {r.Version} — console en retard", $"⚠ {r.Version} — console is behind")
             : r.Version;
    }

    private void RenderRows(Dictionary<ParkMachine, QueryResult>? results)
    {
        LvMachines.ItemsSource = _machines.Select(m =>
        {
            var r = results is not null && results.TryGetValue(m, out var q) ? q : null;
            return new Row
            {
                Name = m.Name,
                Host = $"{m.Host}:{m.Port}",
                Etat = r is null ? "—"
                     : !r.Ok ? $"🔴 {r.Error}"
                     : r.Active ? Lang.T("🟢 surveillance active", "🟢 monitoring active")
                     : Lang.T("🟠 joignable, surveillance arrêtée", "🟠 reachable, monitoring stopped"),
                DernierReleve = r?.Last?.Time is { } rt ? Lang.ShortDateSecond(rt) : "",
                Cpu = r?.Last?.CpuLoad?.ToString("0.#") ?? "",
                TempCpu = r?.Last?.CpuTemp is { } ct ? $"{ct:0.#} °C" : "",
                TempGpu = r?.Last?.GpuTemp is { } gt ? $"{gt:0.#} °C" : "",
                Ram = r?.Last?.MemPct?.ToString("0.#") ?? "",
                Top = r?.Last?.TopProcesses ?? "",
                Version = DescribeVersion(r),
            };
        }).ToList();
    }

    // ------------------------------------------------------------------
    // Rapport distant
    // ------------------------------------------------------------------

    /// <summary>
    /// Déclenche un scan complet sur la machine sélectionnée puis rapatrie et ouvre
    /// son rapport HTML — le « diagnostic sans se déplacer ».
    /// </summary>
    private async void BtnRemoteScan_Click(object sender, RoutedEventArgs e)
    {
        if (LvMachines.SelectedItem is not Row row)
        {
            MessageBox.Show(this, Lang.T("Sélectionne d'abord une machine dans la liste.", "Select a machine in the list first."), "FaultTracePC",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var machine = _machines.FirstOrDefault(m => m.Name == row.Name && $"{m.Host}:{m.Port}" == row.Host);
        if (machine is null) return;

        if (MessageBox.Show(this,
                Lang.T($"Lancer un diagnostic complet sur {machine.Name} ?\n\n", $"Run a full diagnosis on {machine.Name}?\n\n") +
                Lang.T("Le scan s'exécute sur la machine distante (période 30 jours, analyse des dumps comprise) ", "The scan runs on the remote machine (30-day period, dump analysis included) ") +
                Lang.T("et peut prendre plusieurs minutes ; son rapport s'ouvrira automatiquement ici.", "and can take several minutes; its report will open here automatically."),
                "FaultTracePC", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        BtnRemoteScan.IsEnabled = false;
        try
        {
            TxtStatus.Text = Lang.T($"🩺 Diagnostic en cours sur {machine.Name}… (plusieurs minutes possibles, ne pas fermer cette fenêtre)", $"🩺 Diagnosis running on {machine.Name}… (may take several minutes, do not close this window)");
            using var req = SignedRequest(machine, HttpMethod.Post, "/api/scan", "days=30");
            using var resp = await ScanHttp.SendAsync(req);

            if ((int)resp.StatusCode == 429)
            {
                TxtStatus.Text = Lang.T($"{machine.Name} : un diagnostic est déjà en cours — réessaie dans quelques minutes.", $"{machine.Name}: a diagnosis is already running — try again in a few minutes.");
                return;
            }
            resp.EnsureSuccessStatusCode();
            var res = ParkProtocol.ReadScanResponse(await resp.Content.ReadAsStringAsync());
            if (!res.Ok)
            {
                // Le motif vient du poste : il est donc écrit dans la langue du
                // poste, et on le présente comme tel plutôt que de le faire passer
                // pour une phrase de la console.
                TxtStatus.Text = Lang.T($"{machine.Name} : échec du scan distant — ", $"{machine.Name}: remote scan failed — ") +
                    (string.IsNullOrWhiteSpace(res.Error) ? Lang.T("réponse inexploitable", "unusable response") : res.Error);
                return;
            }

            await DownloadAndOpenReportAsync(machine, res.ReportName);
            TxtStatus.Text = Lang.T($"✅ Diagnostic de {machine.Name} terminé — ", $"✅ Diagnosis of {machine.Name} complete — ") + (res.Level is { } niveau
                // Poste à jour : la phrase est écrite ICI, dans la langue de la console.
                ? niveau.Sentence(res.Critical, res.Warnings)
                // Poste antérieur à la 1.3.0 : il n'envoie pas de code, on affiche sa
                // phrase telle quelle. La retraduire serait inventer.
                : string.IsNullOrWhiteSpace(res.RemoteSentence) ? Lang.T("voir le rapport", "see the report") : res.RemoteSentence);
        }
        catch (Exception ex)
        {
            TxtStatus.Text = Lang.T($"Échec du diagnostic distant : {(ex is TaskCanceledException ? "délai dépassé" : ex.Message)}",
                                     $"Remote diagnosis failed: {(ex is TaskCanceledException ? "timed out" : ex.Message)}");
        }
        finally
        {
            BtnRemoteScan.IsEnabled = true;
        }
    }

    private async Task DownloadAndOpenReportAsync(ParkMachine machine, string name)
    {
        var query = $"name={Uri.EscapeDataString(name)}";
        using var dlReq = SignedRequest(machine, HttpMethod.Get, "/api/reports/download", query);
        // Client à long délai : un rapport volumineux sur une liaison lente
        // dépasserait les 4 secondes du client d'interrogation d'état.
        using var dlResp = await ScanHttp.SendAsync(dlReq);
        dlResp.EnsureSuccessStatusCode();
        var safeName = string.Concat($"{machine.Name}_{name}"
            .Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        var tmp = Path.Combine(Path.GetTempPath(), safeName);
        await File.WriteAllTextAsync(tmp, await dlResp.Content.ReadAsStringAsync());
        Process.Start(new ProcessStartInfo(tmp) { UseShellExecute = true });
    }

    private async void BtnOpenReport_Click(object sender, RoutedEventArgs e)
    {
        if (LvMachines.SelectedItem is not Row row)
        {
            MessageBox.Show(this, Lang.T("Sélectionne d'abord une machine dans la liste.", "Select a machine in the list first."), "FaultTracePC",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var machine = _machines.FirstOrDefault(m => m.Name == row.Name && $"{m.Host}:{m.Port}" == row.Host);
        if (machine is null) return;

        try
        {
            TxtStatus.Text = Lang.T($"Récupération du dernier rapport de {machine.Name}…", $"Fetching the last report from {machine.Name}…");
            using var listReq = SignedRequest(machine, HttpMethod.Get, "/api/reports");
            using var listResp = await Http.SendAsync(listReq);
            listResp.EnsureSuccessStatusCode();
            using var doc = JsonDocument.Parse(await listResp.Content.ReadAsStringAsync());
            var first = doc.RootElement.EnumerateArray().FirstOrDefault();
            if (first.ValueKind != JsonValueKind.Object)
            {
                TxtStatus.Text = Lang.T($"{machine.Name} : aucun rapport partagé (lancer un scan sur cette machine).", $"{machine.Name}: no shared report (run a scan on that machine).");
                return;
            }
            var name = first.GetProperty("name").GetString()!;

            await DownloadAndOpenReportAsync(machine, name);
            TxtStatus.Text = Lang.T($"Rapport {name} de {machine.Name} ouvert.", $"Report {name} from {machine.Name} opened.");
        }
        catch (Exception ex)
        {
            TxtStatus.Text = Lang.T($"Échec : {ex.Message}", $"Failed: {ex.Message}");
        }
    }
}
