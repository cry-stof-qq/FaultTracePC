using System.Net;
using System.Text;
using System.Text.Json;
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

    /// <summary>
    /// Intervalle de relecture de <c>remote.json</c>. Trente secondes : assez court
    /// pour qu'un déploiement par stratégie de groupe n'ait pas l'air en panne,
    /// assez long pour que la lecture d'un petit fichier local ne compte pas.
    /// </summary>
    private static readonly TimeSpan RelectureConfig = TimeSpan.FromSeconds(30);

    /// <summary>
    /// LE SERVICE NE RENONCE PLUS.
    ///
    /// Avant, la configuration était lue UNE fois au démarrage : mode Local, le
    /// service se terminait et ne relisait plus jamais rien. Or l'ordre d'un
    /// déploiement par stratégie de groupe est exactement celui qui déclenche ce
    /// cas : le MSI installe et démarre le service alors que <c>remote.json</c>
    /// n'existe pas encore, PUIS le script d'ouverture lance
    /// « --configure-remote ». La machine se retrouvait configurée, la commande
    /// rendait 0, et rien ne répondait — jusqu'au redémarrage suivant. C'est-à-dire
    /// : rien ne marche le jour où l'on déploie et où l'on teste, et tout marche
    /// le lendemain, quand on a déjà conclu que c'était cassé.
    ///
    /// Désormais le service boucle : il relit, écoute quand il doit écouter, et
    /// repart sur de nouvelles bases dès que le mode, le port ou le jeton change.
    /// Un port occupé n'est plus définitif non plus — il sera retenté.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var cfg = RemoteConfig.Load();

            // Mode Local, ou pas encore de jeton : rien n'est exposé, mais on
            // repassera. C'est toute la différence avec l'ancien « return ».
            if (!cfg.ModeClientActif)
            {
                await Patienter(stoppingToken);
                continue;
            }

            using var listener = new HttpListener();
            listener.Prefixes.Add($"http://+:{cfg.Port}/");
            try { listener.Start(); }
            catch
            {
                // Port occupé ou interdit : on n'expose rien plutôt que mal, et on
                // retentera — la cause est souvent temporaire (service en cours
                // d'arrêt, règle de pare-feu pas encore appliquée).
                await Patienter(stoppingToken);
                continue;
            }

            using var arret = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            await using var stopRegistration = arret.Token.Register(() => { try { listener.Stop(); } catch { } });

            var surveillance = SurveillerLaConfiguration(cfg, arret);

            while (!arret.IsCancellationRequested)
            {
                HttpListenerContext ctx;
                try { ctx = await listener.GetContextAsync(); }
                catch { break; } // listener arrêté : configuration changée, ou service en fin de vie

                _ = Task.Run(() => Handle(ctx, cfg), stoppingToken);
            }

            arret.Cancel();
            try { await surveillance; } catch { }
        }
    }

    /// <summary>
    /// Surveille la configuration pendant que l'API écoute et coupe l'écoute dès
    /// qu'elle change. Comparaison sur le SENS — mode, port, jeton — et non sur la
    /// date du fichier : réécrire le même contenu ne doit pas couper les
    /// connexions en cours.
    /// </summary>
    private static async Task SurveillerLaConfiguration(RemoteConfig actuelle, CancellationTokenSource arret)
    {
        while (!arret.IsCancellationRequested)
        {
            try { await Task.Delay(RelectureConfig, arret.Token); }
            catch (OperationCanceledException) { return; }

            if (!actuelle.MemeExpositionQue(RemoteConfig.Load()))
                arret.Cancel();
        }
    }

    /// <summary>Attend l'intervalle de relecture, sans lever à l'arrêt du service.</summary>
    private static async Task Patienter(CancellationToken stoppingToken)
    {
        try { await Task.Delay(RelectureConfig, stoppingToken); }
        catch (OperationCanceledException) { }
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
                    else Json(ctx, new { available = false, reason = Lang.T("Cette machine n'a jamais été analysée.", "This machine has never been analysed.") });
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
                        // « Diagnostic_* » et non « Diagnostic_PC_* » : depuis la
                        // 1.5.0 le nom porte celui de la machine. Le motif large
                        // continue de lister les rapports déposés avant.
                        : Directory.EnumerateFiles(dir, "Diagnostic_*.html")
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

                    // POURQUOI LA LANGUE VOYAGE AVEC LA DEMANDE (point 45)
                    // Ce service tourne sous le compte SYSTEM, dont la culture
                    // d'interface est celle de la MACHINE et jamais celle de la
                    // session de l'administrateur. Un diagnostic lancé depuis une
                    // console française revenait donc en anglais, sans que rien
                    // dans le rapport ne l'explique. La console dit maintenant
                    // dans quelle langue elle veut lire. Elle ne dit rien ? Le
                    // poste garde la sienne, et une console d'avant la 1.5.2
                    // continue de fonctionner exactement comme avant.
                    // Le changement est fait DANS le verrou : deux analyses ne
                    // peuvent pas se marcher dessus, et hors analyse la langue du
                    // service est intacte.
                    var langueAvant = Lang.Current;
                    if (Lang.FromCode(ctx.Request.QueryString["lang"]) is { } langueDemandee)
                        Lang.Apply(langueDemandee);
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
                        var name = HtmlReportGenerator.NomDuRapport(report);
                        File.WriteAllText(Path.Combine(RemoteConfig.SharedReportsDir, name),
                            HtmlReportGenerator.Generate(report), Encoding.UTF8);

                        // AJOUTS PUREMENT ADDITIFS (level, critical, warnings) : une
                        // console restée en 1.2.3 ignore ces champs et continue
                        // d'afficher « verdict ». Le code, lui, permet à une console à
                        // jour d'écrire la phrase dans SA langue — celle de
                        // l'administrateur, qui n'est pas forcément celle du poste.
                        var niveau = ScanLevelInfo.Of(report);
                        Json(ctx, new
                        {
                            ok = true,
                            report = name,
                            verdict = report.Verdict,
                            findings = report.Findings.Count(f => f.Severity != Severity.Info),
                            level = niveau.Code(),
                            critical = report.Findings.Count(f => f.Severity == Severity.Critical),
                            warnings = report.Findings.Count(f => f.Severity == Severity.Warning),
                        });
                    }
                    catch (Exception ex)
                    {
                        Json(ctx, new { ok = false, error = ex.Message });
                    }
                    finally
                    {
                        // Le poste retrouve SA langue : la demande valait pour ce
                        // rapport-là, pas pour le service.
                        Lang.Apply(langueAvant);
                        ScanLock.Release();
                    }
                    break;
                }

                case "/api/reports/download":
                {
                    var name = ctx.Request.QueryString["name"] ?? "";
                    var path = Path.Combine(RemoteConfig.SharedReportsDir, name);
                    // Anti-traversée de chemin : nom strictement conforme, fichier existant dans le dossier partagé.
                    if (!HtmlReportGenerator.EstUnNomDeRapport(name) || !File.Exists(path)) { NotFound(ctx); return; }
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
        string? topRecent = null;
        foreach (var l in recent)
        {
            try
            {
                var e = JsonSerializer.Deserialize<FlightSample>(l.Raw);
                if (e?.Kind != "s") continue;
                last = e;
                if (!string.IsNullOrEmpty(e.TopProcesses)) topRecent = e.TopProcesses;
            }
            catch { }
        }

        // DÉFAUT CONSTATÉ LE 31/08/2026 dans la console : la colonne « Top
        // processus » était vide deux fois sur trois, sans raison visible.
        // La boîte noire ne relève les processus qu'un échantillon sur trois —
        // toutes les 30 s, pour ne pas grossir le journal ni réveiller le disque
        // inutilement — et /api/status renvoie le DERNIER échantillon, qui n'en
        // porte donc généralement pas. On complète avec le relevé le plus récent
        // qui en contienne un : au pire 30 secondes d'âge, ce qui ne change rien
        // à la question posée (« qui charge cette machine ? ») et vaut infiniment
        // mieux qu'une colonne vide que personne ne sait interpréter.
        if (last is not null && string.IsNullOrEmpty(last.TopProcesses)) last.TopProcesses = topRecent;
        return new
        {
            machine = Environment.MachineName,
            time = DateTime.Now,
            uptimeHours = Math.Round(Environment.TickCount64 / 3_600_000.0, 1),
            active = last is not null && DateTime.Now - last.Time < TimeSpan.FromMinutes(2),
            lastSample = last,
            // Version du poste, pour que la console sache qui mettre à jour — elle
            // était déjà exposée par /api/ping, mais la console n'appelle que
            // /api/status : l'ajouter ici évite un aller-retour réseau par machine.
            // Ajout PUREMENT additif : un client antérieur ne renvoie simplement pas
            // ce champ, et la console l'affiche comme « version inconnue » sans que
            // l'interrogation échoue.
            version = typeof(TelemetryService).Assembly.GetName().Version?.ToString(3) ?? "",
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
                    // InvariantCulture explicite : le journal est écrit par le service
                    // et relu ici. Faire dépendre cette lecture des paramètres
                    // régionaux ferait disparaître la boîte noire sans un mot le jour
                    // où ils changent.
                    if (end < 0 || !DateTime.TryParse(
                        line.AsSpan(i + 5, end - i - 5),
                        System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.None, out var t)) continue;
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
