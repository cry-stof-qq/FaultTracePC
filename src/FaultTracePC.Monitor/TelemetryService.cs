using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using FaultTracePC.Core;
using FaultTracePC.Core.Report;
using Microsoft.Extensions.Hosting;

namespace FaultTracePC.Monitor;

/// <summary>
/// API de télémétrie du mode Client — double verrou :
///  1. l'adresse source doit être privée (RFC 1918) ou boucle locale ;
///  2. la requête doit porter une signature HMAC-SHA256 valide (en-têtes
///     X-FaultTrace-Ts / -Nonce / -Sig). Le token sert de clé et ne circule
///     JAMAIS ; l'horodatage et le nonce interdisent de rejouer une capture.
/// Toute requête non conforme reçoit un 403 laconique, sans détail exploitable.
/// Tout est en lecture seule, à l'exception de /api/scan qui déclenche un
/// diagnostic local (action prédéfinie, sans paramètre exécutable).
///
/// Endpoints :
///   GET /api/ping                     → identité de la machine
///   GET /api/status                   → dernier état (échantillon boîte noire + activité)
///   GET /api/flight?minutes=60        → journal récent (JSONL → tableau JSON)
///   GET /api/reports                  → liste des rapports partagés
///   GET /api/reports/download?name=…  → contenu HTML d'un rapport
/// </summary>
public sealed class TelemetryService : BackgroundService
{
    private static readonly Regex ReportNameRx = new(@"^Diagnostic_PC_[\w\-]+\.html$", RegexOptions.Compiled);

    /// <summary>Un seul diagnostic à la fois : un scan est coûteux, on refuse les rafales (429).</summary>
    private static readonly SemaphoreSlim ScanLock = new(1, 1);

    /// <summary>Nonces déjà servis (anti-rejeu), purgés au-delà de la tolérance d'horloge.</summary>
    private static readonly Dictionary<string, DateTime> SeenNonces = new();
    private static readonly object NonceLock = new();

    private static bool IsNonceFresh(string nonce)
    {
        lock (NonceLock)
        {
            var now = DateTime.UtcNow;
            if (SeenNonces.Count > 512)
            {
                var stale = SeenNonces.Where(kv => (now - kv.Value).TotalSeconds > RemoteConfig.ClockToleranceSeconds)
                                      .Select(kv => kv.Key).ToList();
                foreach (var k in stale) SeenNonces.Remove(k);
            }
            if (SeenNonces.ContainsKey(nonce)) return false;
            SeenNonces[nonce] = now;
            return true;
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var cfg = RemoteConfig.Load();
        if (!string.Equals(cfg.Mode, "Client", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrEmpty(cfg.Token))
            return; // mode Local : rien d'exposé, le service de télémétrie s'endort.

        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://+:{cfg.Port}/");
        try { listener.Start(); }
        catch { return; } // port occupé ou interdit : on n'expose rien plutôt que mal.

        await using var stopRegistration = stoppingToken.Register(() => { try { listener.Stop(); } catch { } });

        while (!stoppingToken.IsCancellationRequested)
        {
            HttpListenerContext ctx;
            try { ctx = await listener.GetContextAsync(); }
            catch { break; } // listener arrêté

            _ = Task.Run(() => Handle(ctx, cfg), stoppingToken);
        }
    }

    private static void Handle(HttpListenerContext ctx, RemoteConfig cfg)
    {
        try
        {
            // Verrou 1 : adresse source privée ou locale uniquement.
            if (!RemoteConfig.IsPrivateOrLoopback(ctx.Request.RemoteEndPoint?.Address))
            {
                Deny(ctx); return;
            }
            // Verrou 2 : signature HMAC de la requête. Le secret ne circule jamais,
            // et l'horodatage + le nonce interdisent de rejouer une requête capturée.
            var url = ctx.Request.Url;
            var ok = RemoteConfig.VerifySignature(
                cfg.Token,
                ctx.Request.HttpMethod,
                url?.AbsolutePath ?? "",
                url?.Query.TrimStart('?') ?? "",
                ctx.Request.Headers[RemoteConfig.HeaderTimestamp],
                ctx.Request.Headers[RemoteConfig.HeaderNonce],
                ctx.Request.Headers[RemoteConfig.HeaderSignature],
                IsNonceFresh);
            if (!ok) { Deny(ctx); return; }

            switch (ctx.Request.Url?.AbsolutePath.ToLowerInvariant())
            {
                case "/api/ping":
                    Json(ctx, new
                    {
                        machine = Environment.MachineName,
                        product = "FaultTracePC.Monitor",
                        version = typeof(TelemetryService).Assembly.GetName().Version?.ToString(3) ?? "?",
                        time = DateTime.Now,
                    });
                    break;

                case "/api/status":
                    Json(ctx, BuildStatus());
                    break;

                case "/api/flight":
                {
                    int minutes = int.TryParse(ctx.Request.QueryString["minutes"], out var m) ? Math.Clamp(m, 1, 1440) : 60;
                    var cutoff = DateTime.Now.AddMinutes(-minutes);
                    var lines = ReadFlightLines(2)
                        .Where(l => l.Time >= cutoff)
                        .Select(l => l.Raw);
                    Raw(ctx, "application/json", "[" + string.Join(",", lines) + "]");
                    break;
                }

                case "/api/summary":
                    // Résumé du dernier scan : versions de pilotes, crashs, disques,
                    // conclusions critiques. C'est ce que le mode parc corrèle entre
                    // postes. Rien de nominatif n'y figure — ni chemins utilisateur,
                    // ni processus, ni contenu de fichiers.
                    // Une machine jamais analysée n'a pas de résumé : on répond
                    // explicitement « rien à comparer » plutôt que null, pour que le
                    // maître distingue « poste sans scan » de « poste injoignable ».
                    if (Core.Report.ScanHistory.LoadLatest() is { } latest) Json(ctx, latest);
                    else Json(ctx, new { available = false, reason = "Cette machine n'a jamais été analysée." });
                    break;

                case "/api/alerts":
                {
                    int days = int.TryParse(ctx.Request.QueryString["days"], out var ad) ? Math.Clamp(ad, 1, 30) : 7;
                    Json(ctx, Core.Collectors.AlertLogReader.Read(days));
                    break;
                }

                case "/api/reports":
                {
                    var dir = RemoteConfig.SharedReportsDir;
                    var list = !Directory.Exists(dir)
                        ? new List<object>()
                        : Directory.EnumerateFiles(dir, "Diagnostic_PC_*.html")
                            .OrderByDescending(File.GetLastWriteTime)
                            .Take(30)
                            .Select(f => (object)new { name = Path.GetFileName(f), sizeKb = new FileInfo(f).Length / 1024, date = File.GetLastWriteTime(f) })
                            .ToList();
                    Json(ctx, list);
                    break;
                }

                case "/api/scan":
                {
                    // Lance un diagnostic COMPLET sur cette machine et publie le rapport
                    // dans le dossier partagé. Ce n'est pas de l'exécution arbitraire :
                    // une seule action prédéfinie, en lecture seule sur le système.
                    if (!ScanLock.Wait(0)) { ctx.Response.StatusCode = 429; ctx.Response.Close(); break; }
                    try
                    {
                        int days = int.TryParse(ctx.Request.QueryString["days"], out var d) ? Math.Clamp(d, 1, 90) : 30;
                        bool deep = ctx.Request.QueryString["deep"] != "0";

                        var report = new ScanOrchestrator()
                            .RunAsync(new ScanOptions { Days = days, IncludeDrivers = true, DeepDumpAnalysis = deep })
                            .GetAwaiter().GetResult();

                        Directory.CreateDirectory(RemoteConfig.SharedReportsDir);
                        // Script de réparation d'abord : le rapport y fait référence.
                        try { RepairScriptGenerator.WriteToDisk(report); } catch { }
                        var name = $"Diagnostic_PC_{report.GeneratedAt:yyyy-MM-dd_HHmm}.html";
                        File.WriteAllText(Path.Combine(RemoteConfig.SharedReportsDir, name),
                            HtmlReportGenerator.Generate(report), Encoding.UTF8);

                        Json(ctx, new
                        {
                            ok = true,
                            report = name,
                            verdict = report.Verdict,
                            findings = report.Findings.Count(f => f.Severity != Severity.Info),
                        });
                    }
                    catch (Exception ex)
                    {
                        Json(ctx, new { ok = false, error = ex.Message });
                    }
                    finally { ScanLock.Release(); }
                    break;
                }

                case "/api/reports/download":
                {
                    var name = ctx.Request.QueryString["name"] ?? "";
                    var path = Path.Combine(RemoteConfig.SharedReportsDir, name);
                    // Anti-traversée de chemin : nom strictement conforme, fichier existant dans le dossier partagé.
                    if (!ReportNameRx.IsMatch(name) || !File.Exists(path)) { NotFound(ctx); return; }
                    Raw(ctx, "text/html; charset=utf-8", File.ReadAllText(path));
                    break;
                }

                default:
                    NotFound(ctx);
                    break;
            }
        }
        catch
        {
            try { ctx.Response.Abort(); } catch { }
        }
    }

    // ------------------------------------------------------------------

    private static object BuildStatus()
    {
        var recent = ReadFlightLines(2).ToList();
        FlightSample? last = null;
        foreach (var l in recent)
        {
            try
            {
                var e = JsonSerializer.Deserialize<FlightSample>(l.Raw);
                if (e?.Kind == "s") last = e;
            }
            catch { }
        }
        return new
        {
            machine = Environment.MachineName,
            time = DateTime.Now,
            uptimeHours = Math.Round(Environment.TickCount64 / 3_600_000.0, 1),
            active = last is not null && DateTime.Now - last.Time < TimeSpan.FromMinutes(2),
            lastSample = last,
        };
    }

    /// <summary>Lignes du journal des N derniers jours, avec leur horodatage (parse minimal).</summary>
    private static IEnumerable<(DateTime Time, string Raw)> ReadFlightLines(int days)
    {
        var dir = FlightRecorderService.FlightDir;
        if (!Directory.Exists(dir)) yield break;
        var files = Directory.EnumerateFiles(dir, "flight_*.jsonl")
            .Where(f => File.GetLastWriteTime(f) >= DateTime.Now.AddDays(-days))
            .OrderBy(f => f);
        foreach (var file in files)
        {
            List<(DateTime, string)> lines = new();
            try
            {
                using var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new StreamReader(fs);
                while (reader.ReadLine() is { } line)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    // Extraction rapide de "t":"..." sans désérialisation complète.
                    var i = line.IndexOf("\"t\":\"", StringComparison.Ordinal);
                    if (i < 0) continue;
                    var end = line.IndexOf('"', i + 5);
                    if (end < 0 || !DateTime.TryParse(line.AsSpan(i + 5, end - i - 5), out var t)) continue;
                    lines.Add((t, line));
                }
            }
            catch { }
            foreach (var l in lines) yield return l;
        }
    }

    // ------------------------------------------------------------------

    private static void Json(HttpListenerContext ctx, object payload) =>
        Raw(ctx, "application/json", JsonSerializer.Serialize(payload));

    private static void Raw(HttpListenerContext ctx, string contentType, string body)
    {
        var bytes = Encoding.UTF8.GetBytes(body);
        ctx.Response.StatusCode = 200;
        ctx.Response.ContentType = contentType;
        ctx.Response.ContentLength64 = bytes.Length;
        ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
        ctx.Response.Close();
    }

    private static void Deny(HttpListenerContext ctx)
    {
        ctx.Response.StatusCode = 403;
        ctx.Response.Close();
    }

    private static void NotFound(HttpListenerContext ctx)
    {
        ctx.Response.StatusCode = 404;
        ctx.Response.Close();
    }
}
