using System.IO;
using System.Text;

using FaultTracePC.Core;

namespace FaultTracePC.App;

/// <summary>
/// Rapport de parc : une page HTML unique récapitulant l'état de toutes les
/// machines interrogées — qui va bien, qui ne répond plus, où sont les alertes.
/// Pensé pour être imprimé ou envoyé tel quel à un responsable.
/// </summary>
public static class ParkReportGenerator
{
    public sealed class MachineLine
    {
        public string Name { get; set; } = "";
        public string Host { get; set; } = "";
        public bool Reachable { get; set; }
        public bool MonitoringActive { get; set; }
        public string Error { get; set; } = "";
        public DateTime? LastSample { get; set; }
        public double? CpuLoad { get; set; }
        public double? CpuTemp { get; set; }
        public double? GpuTemp { get; set; }
        public double? MemPct { get; set; }
        public string TopProcesses { get; set; } = "";
        public int CriticalAlerts { get; set; }
        public int WarningAlerts { get; set; }
        public string LastAlert { get; set; } = "";
    }

    public static string Generate(IReadOnlyList<MachineLine> machines,
                                 FaultTracePC.Core.Analysis.ParkComparator.ParkAnalysis? comparison = null)
    {
        var now = DateTime.Now;
        var sb = new StringBuilder(32 * 1024);

        sb.Append("<!DOCTYPE html><html lang=\"fr\"><head><meta charset=\"utf-8\">");
        sb.Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        sb.Append(Lang.T($"<title>État du parc — {now:dd/MM/yyyy}</title><style>", $"<title>Fleet state — {now:yyyy-MM-dd}</title><style>")).Append(Css).Append("</style></head><body>");

        var unreachable = machines.Count(m => !m.Reachable);
        var critical = machines.Count(m => m.Reachable && m.CriticalAlerts > 0);
        var inactive = machines.Count(m => m.Reachable && !m.MonitoringActive);
        var healthy = machines.Count - unreachable - critical;

        sb.Append("<header><div class=\"brand\">");
        sb.Append($"<img src=\"{FaultTracePC.Core.Report.Branding.LogoDataUri}\" alt=\"FaultTracePC\" width=\"44\" height=\"44\">");
        sb.Append(Lang.T("<h1>État du parc — FaultTracePC</h1></div>", "<h1>Fleet state — FaultTracePC</h1></div>"));
        sb.Append(Lang.T($"<p class=\"sub\">{machines.Count} machine(s) · relevé du {now:dd/MM/yyyy à HH:mm}</p></header>", $"<p class=\"sub\">{machines.Count} machine(s) · reading of {now:yyyy-MM-dd HH:mm}</p></header>"));

        // Bandeau de synthèse : quatre nombres, pas de graphique inutile.
        sb.Append("<section><div class=\"tiles\">");
        Tile(sb, Lang.T("Machines saines", "Healthy machines"), healthy, "ok");
        Tile(sb, Lang.T("Alertes critiques", "Critical alerts"), critical, critical > 0 ? "crit" : "neutral");
        Tile(sb, Lang.T("Injoignables", "Unreachable"), unreachable, unreachable > 0 ? "warn" : "neutral");
        Tile(sb, Lang.T("Surveillance arrêtée", "Monitoring stopped"), inactive, inactive > 0 ? "warn" : "neutral");
        sb.Append("</div></section>");

        sb.Append(Lang.T("<section><h2>Détail par machine</h2>", "<section><h2>Detail by machine</h2>"));
        sb.Append(Lang.T("<table><thead><tr><th>Machine</th><th>Adresse</th><th>État</th><th>Dernier relevé</th>", "<table><thead><tr><th>Machine</th><th>Address</th><th>State</th><th>Last reading</th>"));
        sb.Append(Lang.T("<th>CPU %</th><th>T° CPU</th><th>T° GPU</th><th>RAM %</th><th>Alertes</th><th>Dernière alerte / erreur</th></tr></thead><tbody>", "<th>CPU %</th><th>CPU temp.</th><th>GPU temp.</th><th>RAM %</th><th>Alerts</th><th>Last alert / error</th></tr></thead><tbody>"));

        foreach (var m in machines.OrderBy(m => m.Reachable).ThenByDescending(m => m.CriticalAlerts).ThenBy(m => m.Name))
        {
            var (cls, state) = !m.Reachable ? ("crit", Lang.T("🔴 injoignable", "🔴 unreachable"))
                : m.CriticalAlerts > 0 ? ("crit", Lang.T("⛔ alerte critique", "⛔ critical alert"))
                : !m.MonitoringActive ? ("warn", Lang.T("🟠 surveillance arrêtée", "🟠 monitoring stopped"))
                : m.WarningAlerts > 0 ? ("warn", Lang.T("⚠ à surveiller", "⚠ to watch"))
                : ("ok", Lang.T("🟢 sain", "🟢 healthy"));

            sb.Append($"<tr class=\"{cls}\"><td><strong>{H(m.Name)}</strong></td><td class=\"small\">{H(m.Host)}</td>");
            sb.Append($"<td>{state}</td><td class=\"small\">{(m.LastSample is { } ls ? Lang.ShortDateMinute(ls) : "—")}</td>");
            sb.Append($"<td>{Num(m.CpuLoad)}</td><td>{Num(m.CpuTemp, " °C")}</td><td>{Num(m.GpuTemp, " °C")}</td><td>{Num(m.MemPct)}</td>");
            var alertes = Lang.T($"{m.CriticalAlerts} crit. / {m.WarningAlerts} avert.", $"{m.CriticalAlerts} crit. / {m.WarningAlerts} warn.");
            sb.Append($"<td>{(m.CriticalAlerts + m.WarningAlerts == 0 ? "—" : alertes)}</td>");
            sb.Append($"<td class=\"small\">{H(m.Reachable ? m.LastAlert : m.Error)}</td></tr>");
        }

        sb.Append("</tbody></table></section>");

        // Les processus dominants méritent leur propre lecture (colonne trop large sinon).
        var withTop = machines.Where(m => m.Reachable && !string.IsNullOrEmpty(m.TopProcesses)).ToList();
        if (withTop.Count > 0)
        {
            sb.Append(Lang.T("<section><h2>Processus dominants au moment du relevé</h2><table><thead><tr><th>Machine</th><th>Processus</th></tr></thead><tbody>", "<section><h2>Leading processes at the time of the reading</h2><table><thead><tr><th>Machine</th><th>Processes</th></tr></thead><tbody>"));
            foreach (var m in withTop)
                sb.Append($"<tr><td>{H(m.Name)}</td><td class=\"small\">{H(m.TopProcesses)}</td></tr>");
            sb.Append("</tbody></table></section>");
        }

        // ---------- Comparateur de parc ----------
        if (comparison is not null)
        {
            sb.Append(Lang.T("<section><h2>Ce que les postes ont en commun</h2>", "<section><h2>What the machines have in common</h2>"));
            sb.Append(Lang.T("<p class=\"explain\">Un diagnostic individuel ne peut pas voir ceci : un pilote ancien identique sur six postes ", "<p class=\"explain\">An individual diagnosis cannot see this: the same old driver on six machines ")
                    + Lang.T("n'est plus un suspect, c'est une image de déploiement à corriger — et la réparation se fait une fois pour tout le parc.</p>", "is no longer a suspect, it is a deployment image to fix — and the repair happens once, for the whole fleet.</p>"));
            sb.Append($"<p class=\"parkverdict\">{H(comparison.Summary)}</p>");

            foreach (var c in comparison.Correlations)
            {
                var badge = c.Severity switch
                {
                    "crit" => Lang.T("<span class=\"badge crit\">Critique</span>", "<span class=\"badge crit\">Critical</span>"),
                    "warn" => Lang.T("<span class=\"badge warn\">À surveiller</span>", "<span class=\"badge warn\">To watch</span>"),
                    _ => Lang.T("<span class=\"badge info\">Information</span>", "<span class=\"badge info\">Information</span>"),
                };
                var kind = c.Kind switch
                {
                    "divergence" => Lang.T("divergence de version", "version divergence"),
                    // pas-de-traduction : clé interne posée par ParkComparator.Kind.
                    "isolé" => Lang.T("poste isolé", "isolated machine"),
                    _ => Lang.T("point commun", "shared trait"),
                };
                sb.Append($"<div class=\"corr {c.Severity}\"><div class=\"corr-head\">{badge}<span class=\"kind\">{kind}</span></div>");
                sb.Append($"<h3>{H(c.Title)}</h3><p>{H(c.Details)}</p>");
                if (c.Machines.Count > 0)
                    sb.Append(Lang.T($"<p class=\"machines\"><strong>Postes concernés :</strong> {H(string.Join(", ", c.Machines))}</p>", $"<p class=\"machines\"><strong>Machines involved:</strong> {H(string.Join(", ", c.Machines))}</p>"));
                sb.Append(Lang.T($"<p class=\"reco\"><strong>Que faire :</strong> {H(c.Action)}</p></div>", $"<p class=\"reco\"><strong>What to do:</strong> {H(c.Action)}</p></div>"));
            }
            sb.Append("</section>");
        }

        sb.Append(Lang.T($"<footer>Généré par FaultTracePC (console Parc) le {now:dd/MM/yyyy à HH:mm}. ", $"<footer>Generated by FaultTracePC (Fleet console) on {now:yyyy-MM-dd HH:mm}. "));
        sb.Append(Lang.T("Les machines injoignables peuvent être éteintes, hors du réseau, ou avoir un service de surveillance arrêté.</footer>", "Unreachable machines may be switched off, off the network, or have a stopped monitoring service.</footer>"));
        sb.Append("</body></html>");
        return sb.ToString();
    }

    /// <summary>Écrit le rapport dans Documents\FaultTracePC et retourne son chemin.</summary>
    public static string WriteToDisk(IReadOnlyList<MachineLine> machines,
                                     FaultTracePC.Core.Analysis.ParkComparator.ParkAnalysis? comparison = null)
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "FaultTracePC");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"Parc_{DateTime.Now:yyyy-MM-dd_HHmm}.html");
        File.WriteAllText(path, Generate(machines, comparison), Encoding.UTF8);
        return path;
    }

    private static void Tile(StringBuilder sb, string label, int value, string tone) =>
        sb.Append($"<div class=\"tile {tone}\"><div class=\"tile-value\">{value}</div><div class=\"tile-label\">{H(label)}</div></div>");

    private static string Num(double? v, string suffix = "") => v is null ? "—" : $"{v:0.#}{suffix}";

    private static string H(string? s) => System.Net.WebUtility.HtmlEncode(s ?? "");

    private const string Css = """
        *{box-sizing:border-box}
        body{font-family:'Segoe UI',system-ui,sans-serif;margin:0;background:#f4f6f9;color:#1d2733;line-height:1.5}
        header{background:#182848;color:#fff;padding:22px 30px}
        header h1{margin:0;font-size:22px;font-weight:600}
        .brand{display:flex;align-items:center;gap:14px}
        .brand img{border-radius:8px;flex:0 0 auto}
        header .sub{margin:6px 0 0;opacity:.85;font-size:13px}
        section{max-width:1200px;margin:22px auto;padding:0 24px}
        h2{font-size:18px;border-bottom:2px solid #dbe2ec;padding-bottom:6px;margin:0 0 14px}
        .tiles{display:grid;grid-template-columns:repeat(auto-fit,minmax(180px,1fr));gap:12px}
        .tile{background:#fff;border:1px solid #dbe2ec;border-left-width:5px;border-radius:8px;padding:14px 18px}
        .tile.ok{border-left-color:#1baf7a}.tile.warn{border-left-color:#eda100}
        .tile.crit{border-left-color:#e34948}.tile.neutral{border-left-color:#c3c9d4}
        .tile-value{font-size:30px;font-weight:600}
        .tile-label{font-size:12px;color:#52514e;text-transform:uppercase;letter-spacing:.5px}
        table{width:100%;border-collapse:collapse;background:#fff;border:1px solid #dbe2ec;border-radius:8px;overflow:hidden;font-size:13px}
        th{background:#eef2f7;text-align:left;padding:8px 10px;font-size:12px;text-transform:uppercase;letter-spacing:.4px;color:#44546a}
        td{padding:7px 10px;border-top:1px solid #e8edf3;vertical-align:top}
        tr.crit td{background:#fdecea}tr.warn td{background:#fef8ec}
        .small{font-size:12px;color:#42546b}
        .badge{display:inline-block;padding:2px 9px;border-radius:11px;color:#fff;font-size:11px;font-weight:700}
        .badge.crit{background:#c0392b}.badge.warn{background:#e67e22}.badge.info{background:#6b7c91}
        .parkverdict{font-size:15px;font-weight:600;color:#182848;margin:0 0 16px}
        .corr{background:#fff;border:1px solid #dbe2ec;border-left:5px solid #6b7c91;border-radius:8px;padding:14px 18px;margin:0 0 12px}
        .corr.crit{border-left-color:#c0392b}
        .corr.warn{border-left-color:#e67e22}
        .corr-head{display:flex;align-items:center;gap:10px;margin-bottom:6px}
        .corr .kind{font-size:11px;color:#6b7c91;text-transform:uppercase;letter-spacing:.4px}
        .corr h3{margin:4px 0 6px;font-size:15px}
        .corr p{margin:5px 0;font-size:13px}
        .corr .machines{color:#44546a}
        .corr .reco{background:#f4f6f9;border-radius:6px;padding:8px 10px}
        footer{max-width:1200px;margin:28px auto;padding:14px 24px;color:#6b7c91;font-size:12px;border-top:1px solid #dbe2ec}
        @media print{body{background:#fff}header{background:#fff;color:#000;border-bottom:2px solid #000}}
        """;
}
