using System.Text;
using FaultTracePC.Core.Analysis;

namespace FaultTracePC.Core.Report;

/// <summary>
/// Génère le rapport de diagnostic HTML autonome (aucune ressource externe),
/// en français, lisible par un non-spécialiste mais complet pour un technicien.
/// </summary>
public static class HtmlReportGenerator
{
    public static string Generate(DiagnosticReport r)
    {
        var sb = new StringBuilder(64 * 1024);
        sb.Append("<!DOCTYPE html><html lang=\"fr\"><head><meta charset=\"utf-8\">");
        sb.Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        sb.Append($"<title>Diagnostic {H(r.System.MachineName)} — {r.GeneratedAt:dd/MM/yyyy}</title>");
        sb.Append("<style>").Append(Css).Append("</style></head><body>");

        Header(sb, r);
        VerdictBanner(sb, r);
        Findings(sb, r);
        RepairSection(sb, r);
        BsodSection(sb, r);
        DumpSection(sb, r);
        ProcessesSection(sb, r);
        EventsSection(sb, r);
        SystemSection(sb, r);
        DriversSection(sb, r);
        ReliabilitySection(sb, r);
        ErrorsSection(sb, r);

        sb.Append($"<footer>Généré par <strong>FaultTracePC</strong> v0.2 le {r.GeneratedAt:dd/MM/yyyy à HH:mm} — période analysée : {r.ScanPeriodDays} jours. ");
        sb.Append("Les niveaux de confiance sont indiqués honnêtement : une confiance « faible » signale une piste, pas une preuve.</footer>");
        sb.Append("<script>").Append(FilterJs).Append("</script>");
        sb.Append("</body></html>");
        return sb.ToString();
    }

    /// <summary>
    /// Écrit le rapport (et le script de réparation s'il y a des problèmes)
    /// dans Documents\FaultTracePC et retourne le chemin du rapport HTML.
    /// </summary>
    public static string WriteToDisk(DiagnosticReport r)
    {
        // Le script d'abord : le rapport HTML référence son chemin et embarque son contenu.
        RepairScriptGenerator.WriteToDisk(r); // renseigne RepairScriptPath + RepairLauncherPath

        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "FaultTracePC");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"Diagnostic_PC_{r.GeneratedAt:yyyy-MM-dd_HHmm}.html");
        File.WriteAllText(path, Generate(r), Encoding.UTF8);
        return path;
    }

    // ------------------------------------------------------------------

    private static void Header(StringBuilder sb, DiagnosticReport r)
    {
        sb.Append("<header><h1>🩺 FaultTracePC — Rapport de diagnostic</h1>");
        sb.Append($"<p class=\"sub\">Machine <strong>{H(r.System.MachineName)}</strong> · {H(r.System.Os.Caption)} {H(r.System.Os.DisplayVersion)} (build {H(r.System.Os.BuildNumber)}) · généré le {r.GeneratedAt:dd/MM/yyyy à HH:mm} · période : {r.ScanPeriodDays} jours</p></header>");
    }

    private static void VerdictBanner(StringBuilder sb, DiagnosticReport r)
    {
        var cls = r.Findings.Any(f => f.Severity == Severity.Critical) ? "crit"
                : r.Findings.Any(f => f.Severity == Severity.Warning) ? "warn" : "ok";
        sb.Append($"<div class=\"verdict {cls}\"><div class=\"verdict-label\">Verdict</div><div class=\"verdict-text\">{H(r.Verdict)}</div></div>");
    }

    private static void Findings(StringBuilder sb, DiagnosticReport r)
    {
        sb.Append("<section><h2>Conclusions du diagnostic</h2>");
        foreach (var f in r.Findings)
        {
            var (cls, label) = f.Severity switch
            {
                Severity.Critical => ("crit", "Critique"),
                Severity.Warning => ("warn", "Avertissement"),
                _ => ("info", "Information"),
            };
            var conf = f.Confidence switch
            {
                Confidence.High => "confiance élevée",
                Confidence.Medium => "confiance moyenne",
                _ => "confiance faible",
            };
            sb.Append($"<div class=\"card {cls}\"><div class=\"card-head\"><span class=\"badge {cls}\">{label}</span>");
            sb.Append($"<span class=\"conf\">{CategoryLabel(f.Category)} · {conf}</span></div>");
            sb.Append($"<h3>{H(f.Title)}</h3><p>{H(f.Details)}</p>");
            if (!string.IsNullOrEmpty(f.Recommendation))
                sb.Append($"<p class=\"reco\"><strong>Recommandation :</strong> {H(f.Recommendation)}</p>");
            sb.Append("</div>");
        }
        sb.Append("</section>");
    }

    /// <summary>Carte « Aide à la réparation » : chemin du script + bouton de téléchargement (Blob).</summary>
    private static void RepairSection(StringBuilder sb, DiagnosticReport r)
    {
        if (r.RepairScriptPath is null) return;

        sb.Append("<section><h2>🛠️ Aide à la réparation</h2><div class=\"repair\">");
        sb.Append("<p>Un script PowerShell de tests et réparations <strong>adapté aux problèmes détectés ci-dessus</strong> a été généré. ");
        sb.Append("Les tests en lecture seule s'exécutent automatiquement ; toute action qui modifie le système demande confirmation (O/N). ");
        sb.Append("Un journal complet est conservé dans <code>Documents\\FaultTracePC</code>.</p>");
        if (r.RepairLauncherPath is not null)
        {
            sb.Append($"<p><strong>Le plus simple — double-clique sur :</strong> <code>{H(r.RepairLauncherPath)}</code><br>");
            sb.Append("<span class=\"small\">Ce lanceur demande l'élévation administrateur (UAC) et exécute le script automatiquement — aucun réglage Windows n'est modifié. ");
            sb.Append("Tu peux aussi cliquer sur « Lancer la réparation » directement dans FaultTracePC après un scan.</span></p>");
        }
        sb.Append($"<p><strong>Script détaillé :</strong> <code>{H(r.RepairScriptPath)}</code> — exécutable aussi depuis un terminal administrateur :</p>");
        sb.Append($"<pre>powershell -ExecutionPolicy Bypass -File \"{H(r.RepairScriptPath)}\"</pre>");
        sb.Append("<button class=\"btn\" onclick=\"downloadRepair()\">💾 Retélécharger le script (.ps1)</button>");
        sb.Append("</div>");
        // Contenu du script embarqué pour le bouton de téléchargement (échappé en JSON).
        sb.Append("<script>const REPAIR_PS1 = ")
          .Append(System.Text.Json.JsonSerializer.Serialize(RepairScriptGenerator.Generate(r)))
          .Append(";\nfunction downloadRepair(){const b=new Blob([\"\\uFEFF\"+REPAIR_PS1],{type:'text/plain;charset=utf-8'});const a=document.createElement('a');a.href=URL.createObjectURL(b);a.download='")
          .Append(H(Path.GetFileName(r.RepairScriptPath)))
          .Append("';a.click();URL.revokeObjectURL(a.href);}</script>");
        sb.Append("</section>");
    }

    /// <summary>Processus en cours au moment du scan : RAM, CPU, débit disque.</summary>
    private static void ProcessesSection(StringBuilder sb, DiagnosticReport r)
    {
        sb.Append("<section><h2>Processus en cours au moment du scan</h2>");
        if (r.Processes.Count == 0) { sb.Append("<p class=\"empty\">Relevé des processus indisponible.</p></section>"); return; }

        var os = r.System.Os;
        if (os.TotalVisibleMemoryKB > 0)
        {
            var physUsed = 100.0 * (os.TotalVisibleMemoryKB - os.FreePhysicalMemoryKB) / os.TotalVisibleMemoryKB;
            var commitUsed = os.TotalVirtualMemoryKB > 0
                ? 100.0 * (os.TotalVirtualMemoryKB - os.FreeVirtualMemoryKB) / os.TotalVirtualMemoryKB : 0;
            sb.Append($"<p class=\"sub2\">Mémoire physique utilisée : <strong>{physUsed:0} %</strong> ({RulesEngine.FormatBytes((os.TotalVisibleMemoryKB - os.FreePhysicalMemoryKB) * 1024)} / {RulesEngine.FormatBytes(os.TotalVisibleMemoryKB * 1024)})");
            if (commitUsed > 0)
                sb.Append($" · Mémoire virtuelle engagée (RAM + fichier d'échange) : <strong>{commitUsed:0} %</strong>");
            sb.Append(". CPU et débit disque mesurés sur ~1 s. Ce relevé montre l'état <em>au moment du scan</em> — pour l'état au moment d'un crash passé, voir les événements « Saturation mémoire » ci-dessous ou le futur mode surveillance (Phase 3).</p>");
        }

        sb.Append("<table><thead><tr><th>Processus</th><th>PID</th><th>RAM privée</th><th>Working set</th><th>CPU %</th><th>Disque (o/s)</th></tr></thead><tbody>");
        var top = r.Processes.Take(25).ToList();
        // On ajoute aussi les gros consommateurs CPU absents du top RAM.
        top.AddRange(r.Processes.OrderByDescending(p => p.CpuPercent).Take(8).Where(p => !top.Contains(p)));
        foreach (var p in top)
        {
            var hot = p.PrivateBytes > 2L * 1024 * 1024 * 1024 || p.CpuPercent >= 25;
            sb.Append($"<tr{(hot ? " class=\"oldrow\"" : "")}><td>{H(p.Name)}</td><td class=\"mono\">{p.Pid}</td>");
            sb.Append($"<td>{RulesEngine.FormatBytes((ulong)Math.Max(0, p.PrivateBytes))}</td><td>{RulesEngine.FormatBytes((ulong)Math.Max(0, p.WorkingSetBytes))}</td>");
            sb.Append($"<td>{p.CpuPercent:0.#}</td><td>{(p.IoBytesPerSec > 0 ? RulesEngine.FormatBytes((ulong)p.IoBytesPerSec) + "/s" : "—")}</td></tr>");
        }
        sb.Append("</tbody></table>");
        sb.Append($"<p class=\"empty\">{r.Processes.Count} processus au total — affichés : les 25 plus gros en RAM + les plus actifs en CPU. Lignes surlignées : &gt; 2 Go de RAM privée ou ≥ 25 % CPU.</p>");
        sb.Append("</section>");
    }

    private static void BsodSection(StringBuilder sb, DiagnosticReport r)
    {
        sb.Append("<section><h2>Écrans bleus (BSOD)</h2>");
        if (r.Bsods.Count == 0) { sb.Append("<p class=\"empty\">Aucun BSOD détecté sur la période.</p></section>"); return; }

        sb.Append("<table><thead><tr><th>Date</th><th>Bug Check</th><th>Nom</th><th>Paramètres</th><th>Pilote suspect</th><th>Dump</th><th>Sources</th></tr></thead><tbody>");
        foreach (var b in r.Bsods)
        {
            var code = b.BugCheckCode is null ? "—" : $"0x{b.BugCheckCode:X8}";
            var pars = b.Parameters is null ? "—" : string.Join("<br>", b.Parameters.Select(p => $"0x{p:X16}"));
            sb.Append($"<tr><td>{b.TimeLocal:dd/MM/yyyy HH:mm}</td><td class=\"mono\">{code}</td><td>{H(b.BugCheckName)}</td>");
            sb.Append($"<td class=\"mono small\">{pars}</td><td>{H(b.SuspectDriver ?? "à déterminer (Phase 2 : WinDbg)")}</td>");
            sb.Append($"<td class=\"small\">{H(b.DumpPath is null ? "—" : Path.GetFileName(b.DumpPath))}</td><td class=\"small\">{H(string.Join(", ", b.Sources))}</td></tr>");
        }
        sb.Append("</tbody></table></section>");
    }

    private static void DumpSection(StringBuilder sb, DiagnosticReport r)
    {
        sb.Append("<section><h2>Fichiers dump présents</h2>");
        if (r.Dumps.Count == 0) { sb.Append("<p class=\"empty\">Aucun fichier dump trouvé (Minidump, MEMORY.DMP, LiveKernelReports).</p></section>"); return; }

        sb.Append("<table><thead><tr><th>Fichier</th><th>Type</th><th>Date</th><th>Taille</th><th>Code STOP (en-tête)</th></tr></thead><tbody>");
        foreach (var d in r.Dumps)
        {
            var kind = d.Kind switch
            {
                DumpKind.KernelMinidump => "Minidump noyau",
                DumpKind.FullMemoryDump => "Dump mémoire complet",
                DumpKind.LiveKernelReport => "Live Kernel Report",
                DumpKind.UserModeMinidump => "Dump applicatif (WER)",
                _ => "Inconnu",
            };
            var code = d.BugCheckCode is null
                ? (d.ParseError is null ? "—" : $"illisible ({H(d.ParseError)})")
                : $"0x{d.BugCheckCode:X8} {H(BugCheckCatalog.NameOf(d.BugCheckCode.Value))}";
            sb.Append($"<tr><td class=\"small\">{H(d.Path)}</td><td>{kind}</td><td>{(d.CrashTimeFromHeader ?? d.LastWriteTime):dd/MM/yyyy HH:mm}</td>");
            sb.Append($"<td>{RulesEngine.FormatBytes((ulong)d.SizeBytes)}</td><td class=\"mono small\">{code}</td></tr>");
        }
        sb.Append("</tbody></table></section>");
    }

    private static void EventsSection(StringBuilder sb, DiagnosticReport r)
    {
        sb.Append("<section><h2>Historique des événements significatifs</h2>");
        if (r.Events.Count == 0) { sb.Append("<p class=\"empty\">Aucun événement significatif sur la période.</p></section>"); return; }

        // Résumé par catégorie : chaque puce est un BOUTON de filtre.
        sb.Append("<p class=\"sub2\">Clique sur une catégorie pour filtrer le tableau ; « Tous » réaffiche tout.</p>");
        sb.Append("<div class=\"chips\" id=\"evt-chips\">");
        sb.Append($"<button type=\"button\" class=\"chip active\" data-cat=\"all\">Tous : <strong>{r.Events.Count}</strong></button>");
        foreach (var g in r.Events.GroupBy(e => e.Category).OrderByDescending(g => g.Count()))
            sb.Append($"<button type=\"button\" class=\"chip\" data-cat=\"{g.Key}\">{CategoryEventLabel(g.Key)} : <strong>{g.Count()}</strong></button>");
        sb.Append("</div>");

        const int maxRows = 400;
        sb.Append("<table id=\"evt-table\"><thead><tr><th>Date</th><th>Catégorie</th><th>Source</th><th>ID</th><th>Détail</th></tr></thead><tbody>");
        foreach (var e in r.Events.Take(maxRows))
        {
            var extra = e.Extracted.Count > 0
                ? " <span class=\"extract\">" + H(string.Join(" · ", e.Extracted.Where(kv => kv.Key != "HasErrors").Select(kv => $"{kv.Key}: {kv.Value}"))) + "</span>"
                : "";
            sb.Append($"<tr data-cat=\"{e.Category}\"><td>{e.TimeLocal:dd/MM HH:mm}</td><td>{CategoryEventLabel(e.Category)}</td><td class=\"small\">{H(e.Provider)}</td>");
            sb.Append($"<td>{e.EventId}</td><td class=\"small\">{H(Shorten(e.Message, 180))}{extra}</td></tr>");
        }
        sb.Append("</tbody></table>");
        sb.Append("<p class=\"empty\" id=\"evt-none\" style=\"display:none\">Aucun événement de cette catégorie parmi les lignes affichées.</p>");
        if (r.Events.Count > maxRows)
            sb.Append($"<p class=\"empty\">… {r.Events.Count - maxRows} événements supplémentaires non affichés (les plus récents sont listés).</p>");
        sb.Append("</section>");
    }

    private static void SystemSection(StringBuilder sb, DiagnosticReport r)
    {
        var s = r.System;
        sb.Append("<section><h2>Configuration système</h2><div class=\"grid\">");

        Card(sb, "Windows", $"{H(s.Os.Caption)} {H(s.Os.DisplayVersion)}<br>Build {H(s.Os.BuildNumber)} · {H(s.Os.Architecture)}"
            + $"<br>Installé le {(s.Os.InstallDate?.ToString("dd/MM/yyyy") ?? "?")}"
            + $"<br>Dernier démarrage : {(s.Os.LastBootUpTime?.ToString("dd/MM/yyyy HH:mm") ?? "?")}"
            + (s.Os.Uptime is { } up ? $" (allumé depuis {(int)up.TotalHours} h {up.Minutes:00})" : ""));

        Card(sb, "Machine / BIOS", $"{H(s.Bios.SystemManufacturer)} {H(s.Bios.SystemModel)}"
            + $"<br>Carte mère : {H(s.Bios.BaseboardManufacturer)} {H(s.Bios.BaseboardProduct)}"
            + $"<br>BIOS {H(s.Bios.Version)} ({s.Bios.ReleaseDate?.ToString("dd/MM/yyyy") ?? "?"})");

        Card(sb, "Processeur", $"{H(s.Cpu.Name)}<br>{s.Cpu.Cores} cœurs / {s.Cpu.LogicalProcessors} threads · {s.Cpu.MaxClockSpeedMHz} MHz");

        var ramTotal = s.RamModules.Aggregate(0UL, (acc, m) => acc + m.CapacityBytes);
        Card(sb, $"Mémoire ({RulesEngine.FormatBytes(ramTotal)})",
            s.RamModules.Count == 0 ? "détail indisponible"
            : string.Join("<br>", s.RamModules.Select(m =>
                $"{H(m.DeviceLocator)} : {RulesEngine.FormatBytes(m.CapacityBytes)} {H(m.Manufacturer)} {H(m.PartNumber)} @ {(m.ConfiguredSpeedMTs > 0 ? m.ConfiguredSpeedMTs : m.SpeedMTs)} MT/s")));

        Card(sb, "Cartes graphiques",
            s.Gpus.Count == 0 ? "aucune détectée"
            : string.Join("<br>", s.Gpus.Select(g => $"{H(g.Name)} — pilote {H(g.DriverVersion)} du {(g.DriverDate?.ToString("dd/MM/yyyy") ?? "?")}")));

        Card(sb, "Disques physiques",
            s.Disks.Count == 0 ? "aucun détecté"
            : string.Join("<br>", s.Disks.Select(d =>
                $"{H(d.Model)} ({RulesEngine.FormatBytes(d.SizeBytes)}, {H(string.IsNullOrEmpty(d.MediaType) ? d.InterfaceType : d.MediaType)}) — santé : {H(FirstNonEmpty(d.HealthStatus, d.WmiStatus, "inconnue"))}"
                + (d.TemperatureC is { } t ? $" · {t} °C" : "")
                + (d.WearPercent is { } w and > 0 ? $" · usure {w} %" : ""))));

        Card(sb, "Volumes",
            s.Volumes.Count == 0 ? "aucun"
            : string.Join("<br>", s.Volumes.Select(v =>
                $"{H(v.Letter)} {H(v.Label)} ({H(v.FileSystem)}) : {RulesEngine.FormatBytes(v.FreeBytes)} libres / {RulesEngine.FormatBytes(v.SizeBytes)} ({v.PercentFree} %)")));

        Card(sb, "Fichier d'échange", H(s.Os.PageFileInfo));

        sb.Append("</div></section>");
    }

    private static void DriversSection(StringBuilder sb, DiagnosticReport r)
    {
        var thirdParty = r.System.Drivers
            .Where(d => d.State.Equals("Running", StringComparison.OrdinalIgnoreCase) && !d.IsMicrosoft && !string.IsNullOrEmpty(d.CompanyName))
            .OrderBy(d => d.CompanyName).ThenBy(d => d.Name).ToList();

        sb.Append("<section><h2>Pilotes tiers actifs</h2>");
        if (r.System.Drivers.Count == 0) { sb.Append("<p class=\"empty\">Inventaire des pilotes non collecté.</p></section>"); return; }
        if (thirdParty.Count == 0) { sb.Append("<p class=\"empty\">Aucun pilote tiers en cours d'exécution (tous Microsoft).</p></section>"); return; }

        sb.Append($"<p class=\"sub2\">{thirdParty.Count} pilotes non-Microsoft en cours d'exécution — en cas de BSOD, ce sont les premiers suspects, surtout les plus anciens.</p>");
        sb.Append("<div class=\"legend\"><strong>Pourquoi certains pilotes sont marqués « ancien » :</strong> un fichier pilote de plus de 4 ans "
                + "n'est <em>pas</em> un problème en soi — s'il n'apparaît dans aucun crash, c'est simplement un suspect potentiel à connaître. "
                + "Les pilotes bas niveau anciens (lecteurs virtuels, antivirus, outils disque) sont en revanche des causes classiques de BSOD "
                + "lorsqu'ils sont impliqués : si un BSOD cite l'un d'eux, mettre à jour l'application associée ou la désinstaller si elle ne sert plus.</div>");
        sb.Append("<table><thead><tr><th>Pilote</th><th>Éditeur</th><th>Version</th><th>Date du fichier</th><th>Âge</th><th>Fichier</th></tr></thead><tbody>");
        foreach (var d in thirdParty)
        {
            bool isOld = d.FileDate is { } fd && fd < DateTime.Now.AddYears(-4);
            var ageBadge = d.FileDate is null ? "—"
                : isOld ? $"<span class=\"agebadge\">ancien ({(int)((DateTime.Now - d.FileDate.Value).TotalDays / 365)} ans)</span>"
                : "récent";
            sb.Append($"<tr{(isOld ? " class=\"oldrow\"" : "")}><td>{H(FirstNonEmpty(d.DisplayName, d.Name))}</td><td>{H(d.CompanyName)}</td><td class=\"small\">{H(d.FileVersion)}</td>");
            sb.Append($"<td>{(d.FileDate?.ToString("dd/MM/yyyy") ?? "?")}</td><td>{ageBadge}</td><td class=\"small\">{H(Path.GetFileName(d.Path))}</td></tr>");
        }
        sb.Append("</tbody></table></section>");
    }

    private static void ReliabilitySection(StringBuilder sb, DiagnosticReport r)
    {
        sb.Append("<section><h2>Moniteur de fiabilité</h2>");
        if (r.ReliabilityRecords.Count == 0) { sb.Append("<p class=\"empty\">Aucune donnée de fiabilité disponible sur la période.</p></section>"); return; }
        sb.Append("<table><thead><tr><th>Date</th><th>Source</th><th>Produit</th><th>Message</th></tr></thead><tbody>");
        foreach (var rec in r.ReliabilityRecords.Take(60))
            sb.Append($"<tr><td>{rec.TimeLocal:dd/MM HH:mm}</td><td class=\"small\">{H(rec.SourceName)}</td><td class=\"small\">{H(rec.ProductName)}</td><td class=\"small\">{H(Shorten(rec.Message, 160))}</td></tr>");
        sb.Append("</tbody></table></section>");
    }

    private static void ErrorsSection(StringBuilder sb, DiagnosticReport r)
    {
        if (r.CollectorErrors.Count == 0) return;
        sb.Append("<section><h2>Limitations de cette analyse</h2><ul>");
        foreach (var e in r.CollectorErrors)
            sb.Append($"<li>{H(e)}</li>");
        sb.Append("</ul><p class=\"empty\">Ces sources n'ont pas pu être lues ; le diagnostic reste valable sur les données collectées.</p></section>");
    }

    // ------------------------------------------------------------------

    private static void Card(StringBuilder sb, string title, string html) =>
        sb.Append($"<div class=\"syscard\"><h4>{H(title)}</h4><p>{html}</p></div>");

    private static string CategoryLabel(FaultCategory c) => c switch
    {
        FaultCategory.Hardware => "Matériel",
        FaultCategory.Memory => "Mémoire RAM",
        FaultCategory.Storage => "Stockage",
        FaultCategory.GpuDriver => "Pilote graphique",
        FaultCategory.Driver => "Pilote",
        FaultCategory.Software => "Logiciel",
        FaultCategory.Power => "Alimentation",
        FaultCategory.WindowsUpdate => "Windows Update",
        _ => "Général",
    };

    private static string CategoryEventLabel(EventCategory c) => c switch
    {
        EventCategory.Bsod => "BSOD",
        EventCategory.PowerLoss => "Coupure (Kernel-Power 41)",
        EventCategory.UnexpectedShutdown => "Arrêt inattendu",
        EventCategory.Whea => "Erreur matérielle (WHEA)",
        EventCategory.DiskError => "Erreur disque",
        EventCategory.Tdr => "Réinit. pilote graphique",
        EventCategory.AppCrash => "Crash application",
        EventCategory.AppHang => "Application bloquée",
        EventCategory.ServiceFailure => "Échec de service",
        EventCategory.MemoryDiag => "Diagnostic mémoire",
        EventCategory.WindowsUpdate => "Windows Update",
        EventCategory.ResourceExhaustion => "Saturation mémoire",
        _ => "Autre",
    };

    /// <summary>JS du filtrage des événements par catégorie (puces cliquables).</summary>
    private const string FilterJs = """
        (function(){
          const chips = document.querySelectorAll('#evt-chips .chip');
          const rows = document.querySelectorAll('#evt-table tbody tr');
          const none = document.getElementById('evt-none');
          if (!chips.length) return;
          chips.forEach(c => c.addEventListener('click', () => {
            chips.forEach(x => x.classList.remove('active'));
            c.classList.add('active');
            const cat = c.dataset.cat;
            let visible = 0;
            rows.forEach(tr => {
              const show = (cat === 'all' || tr.dataset.cat === cat);
              tr.style.display = show ? '' : 'none';
              if (show) visible++;
            });
            if (none) none.style.display = visible === 0 ? '' : 'none';
          }));
        })();
        """;

    private static string FirstNonEmpty(params string[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? "";

    private static string Shorten(string s, int max) => s.Length <= max ? s : s[..max] + "…";

    private static string H(string? s) => System.Net.WebUtility.HtmlEncode(s ?? "");

    private const string Css = """
        :root{color-scheme:light}
        *{box-sizing:border-box}
        body{font-family:'Segoe UI',system-ui,sans-serif;margin:0;background:#f4f6f9;color:#1d2733;line-height:1.5}
        header{background:#182848;color:#fff;padding:26px 34px}
        header h1{margin:0;font-size:24px;font-weight:600}
        header .sub{margin:6px 0 0;opacity:.85;font-size:14px}
        section{max-width:1160px;margin:26px auto;padding:0 24px}
        h2{font-size:19px;border-bottom:2px solid #dbe2ec;padding-bottom:6px;margin:0 0 14px}
        .verdict{max-width:1160px;margin:24px auto 0;padding:18px 24px;border-radius:10px;display:flex;gap:18px;align-items:center;margin-left:auto;margin-right:auto;width:calc(100% - 48px)}
        .verdict.crit{background:#fdecea;border:1px solid #e74c3c}
        .verdict.warn{background:#fef5e7;border:1px solid #e67e22}
        .verdict.ok{background:#eafaf1;border:1px solid #27ae60}
        .verdict-label{font-size:12px;text-transform:uppercase;letter-spacing:1px;font-weight:700;opacity:.7}
        .verdict-text{font-size:16px;font-weight:600}
        .card{background:#fff;border:1px solid #dbe2ec;border-left-width:5px;border-radius:8px;padding:14px 18px;margin-bottom:12px}
        .card.crit{border-left-color:#e74c3c}.card.warn{border-left-color:#e67e22}.card.info{border-left-color:#2980b9}
        .card h3{margin:8px 0 6px;font-size:16px}
        .card p{margin:4px 0;font-size:14px}
        .card .reco{background:#f4f8fb;border-radius:6px;padding:8px 10px;margin-top:8px}
        .card-head{display:flex;justify-content:space-between;align-items:center}
        .badge{font-size:11px;font-weight:700;text-transform:uppercase;letter-spacing:.5px;padding:3px 9px;border-radius:20px;color:#fff}
        .badge.crit{background:#e74c3c}.badge.warn{background:#e67e22}.badge.info{background:#2980b9}
        .conf{font-size:12px;color:#5a6b7f}
        table{width:100%;border-collapse:collapse;background:#fff;border:1px solid #dbe2ec;border-radius:8px;overflow:hidden;font-size:13px}
        th{background:#eef2f7;text-align:left;padding:8px 10px;font-size:12px;text-transform:uppercase;letter-spacing:.4px;color:#44546a}
        td{padding:7px 10px;border-top:1px solid #e8edf3;vertical-align:top}
        tr:hover td{background:#f7fafc}
        .oldrow td{background:#fff8ef}
        .mono{font-family:Consolas,monospace}
        .small{font-size:12px;color:#42546b}
        .extract{color:#1a6fb5;font-size:12px}
        .empty{color:#6b7c91;font-size:13px;font-style:italic}
        .sub2{color:#44546a;font-size:13px;margin:0 0 10px}
        .chips{margin:0 0 12px}
        .chip{display:inline-block;background:#fff;border:1px solid #dbe2ec;border-radius:16px;padding:4px 12px;font-size:12px;margin:0 6px 6px 0;cursor:pointer;font-family:inherit;color:inherit}
        .chip:hover{border-color:#2470b3;background:#f0f6fc}
        .chip.active{background:#2470b3;border-color:#2470b3;color:#fff}
        .legend{background:#fffbe9;border:1px solid #e8d9a0;border-radius:8px;padding:10px 14px;font-size:13px;margin:0 0 12px}
        .agebadge{background:#e6a817;color:#fff;font-size:11px;font-weight:700;padding:2px 8px;border-radius:12px;white-space:nowrap}
        .repair{background:#fff;border:1px solid #dbe2ec;border-left:5px solid #27ae60;border-radius:8px;padding:14px 18px;font-size:14px}
        .repair pre{background:#182848;color:#d8e4f5;padding:10px 12px;border-radius:6px;overflow-x:auto;font-size:12px}
        .repair code{background:#eef2f7;padding:1px 5px;border-radius:4px;font-size:12px}
        .btn{background:#27ae60;color:#fff;border:0;border-radius:6px;padding:9px 16px;font-size:13px;font-weight:600;cursor:pointer;font-family:inherit}
        .btn:hover{background:#219652}
        .grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(330px,1fr));gap:12px}
        .syscard{background:#fff;border:1px solid #dbe2ec;border-radius:8px;padding:12px 16px}
        .syscard h4{margin:0 0 6px;font-size:13px;text-transform:uppercase;letter-spacing:.5px;color:#44546a}
        .syscard p{margin:0;font-size:13px}
        footer{max-width:1160px;margin:30px auto;padding:14px 24px;color:#6b7c91;font-size:12px;border-top:1px solid #dbe2ec}
        """;
}
