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
            MessageBox.Show(this, "Impossible d'enregistrer la liste : " + ex.Message, "FaultTracePC",
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
            MessageBox.Show(this, "Hôte et token sont obligatoires (le token s'obtient sur la machine cliente, fenêtre 🌐 Mode réseau).",
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
        if (LvMachines.SelectedItem is Row row)
        {
            _machines.RemoveAll(m => m.Name == row.Name && m.Host == row.Host);
            SaveMachines();
            RenderRows(null);
        }
    }

    private void BtnRefresh_Click(object sender, RoutedEventArgs e) => _ = RefreshAllAsync();

    private async Task RefreshAllAsync()
    {
        TxtStatus.Text = $"Interrogation de {_machines.Count} machine(s)…";
        var results = await Task.WhenAll(_machines.Select(QueryAsync));
        RenderRows(results.ToDictionary(r => r.Machine, r => r));
        TxtStatus.Text = $"Actualisé à {DateTime.Now:HH:mm:ss} — {results.Count(r => r.Ok)}/{_machines.Count} machine(s) joignable(s).";
    }

    private sealed record QueryResult(ParkMachine Machine, bool Ok, bool Active, FlightSample? Last, string Error);

    private static async Task<QueryResult> QueryAsync(ParkMachine m)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, $"http://{m.Host}:{m.Port}/api/status");
            req.Headers.Add("X-FaultTrace-Token", m.Token);
            using var resp = await Http.SendAsync(req);
            if (!resp.IsSuccessStatusCode)
                return new(m, false, false, null, resp.StatusCode == System.Net.HttpStatusCode.Forbidden
                    ? "refusé (token ?)" : $"HTTP {(int)resp.StatusCode}");

            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            bool active = doc.RootElement.TryGetProperty("active", out var a) && a.GetBoolean();
            FlightSample? last = null;
            if (doc.RootElement.TryGetProperty("lastSample", out var ls) && ls.ValueKind == JsonValueKind.Object)
                last = ls.Deserialize<FlightSample>();
            return new(m, true, active, last, "");
        }
        catch (Exception ex)
        {
            return new(m, false, false, null, ex is TaskCanceledException ? "délai dépassé" : "injoignable");
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
                     : r.Active ? "🟢 surveillance active"
                     : "🟠 joignable, surveillance arrêtée",
                DernierReleve = r?.Last?.Time.ToString("dd/MM HH:mm:ss") ?? "",
                Cpu = r?.Last?.CpuLoad?.ToString("0.#") ?? "",
                TempCpu = r?.Last?.CpuTemp is { } ct ? $"{ct:0.#} °C" : "",
                TempGpu = r?.Last?.GpuTemp is { } gt ? $"{gt:0.#} °C" : "",
                Ram = r?.Last?.MemPct?.ToString("0.#") ?? "",
                Top = r?.Last?.TopProcesses ?? "",
            };
        }).ToList();
    }

    // ------------------------------------------------------------------
    // Rapport distant
    // ------------------------------------------------------------------

    private async void BtnOpenReport_Click(object sender, RoutedEventArgs e)
    {
        if (LvMachines.SelectedItem is not Row row)
        {
            MessageBox.Show(this, "Sélectionne d'abord une machine dans la liste.", "FaultTracePC",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var machine = _machines.FirstOrDefault(m => m.Name == row.Name && $"{m.Host}:{m.Port}" == row.Host);
        if (machine is null) return;

        try
        {
            TxtStatus.Text = $"Récupération du dernier rapport de {machine.Name}…";
            using var listReq = new HttpRequestMessage(HttpMethod.Get, $"http://{machine.Host}:{machine.Port}/api/reports");
            listReq.Headers.Add("X-FaultTrace-Token", machine.Token);
            using var listResp = await Http.SendAsync(listReq);
            listResp.EnsureSuccessStatusCode();
            using var doc = JsonDocument.Parse(await listResp.Content.ReadAsStringAsync());
            var first = doc.RootElement.EnumerateArray().FirstOrDefault();
            if (first.ValueKind != JsonValueKind.Object)
            {
                TxtStatus.Text = $"{machine.Name} : aucun rapport partagé (lancer un scan sur cette machine).";
                return;
            }
            var name = first.GetProperty("name").GetString()!;

            using var dlReq = new HttpRequestMessage(HttpMethod.Get,
                $"http://{machine.Host}:{machine.Port}/api/reports/download?name={Uri.EscapeDataString(name)}");
            dlReq.Headers.Add("X-FaultTrace-Token", machine.Token);
            using var dlResp = await Http.SendAsync(dlReq);
            dlResp.EnsureSuccessStatusCode();

            var tmp = Path.Combine(Path.GetTempPath(), $"{machine.Name}_{name}");
            await File.WriteAllTextAsync(tmp, await dlResp.Content.ReadAsStringAsync());
            Process.Start(new ProcessStartInfo(tmp) { UseShellExecute = true });
            TxtStatus.Text = $"Rapport {name} de {machine.Name} ouvert.";
        }
        catch (Exception ex)
        {
            TxtStatus.Text = $"Échec : {ex.Message}";
        }
    }
}
