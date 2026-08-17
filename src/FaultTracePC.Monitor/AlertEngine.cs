using System.Management;
using System.Text;
using System.Text.Json;
using FaultTracePC.Core;

namespace FaultTracePC.Monitor;

/// <summary>
/// Moteur d'alertes préventives : observe les échantillons de la boîte noire et
/// les compteurs de santé disque pour prévenir AVANT la panne.
///
/// Principes anti-bruit :
///  - une alerte n'est émise qu'après N échantillons CONSÉCUTIFS au-dessus du seuil
///    (un pic de température d'une seconde n'alerte pas) ;
///  - une même règle ne se répète pas avant le délai configuré (60 min par défaut) ;
///  - le passage sous le seuil réarme la règle immédiatement.
/// Les alertes sont journalisées dans Flight\alerts.jsonl (lu par l'app et l'API).
/// </summary>
public sealed class AlertEngine
{
    private readonly AlertSettings _settings;
    private readonly Dictionary<string, int> _streaks = new();
    private readonly Dictionary<string, DateTime> _lastEmitted = new();
    private DateTime _lastDiskCheck = DateTime.MinValue;

    public AlertEngine(AlertSettings settings) => _settings = settings;

    /// <summary>Évalue un échantillon ; retourne les alertes à émettre (souvent aucune).</summary>
    public List<PreventiveAlert> Evaluate(FlightSample s)
    {
        var alerts = new List<PreventiveAlert>();
        if (!_settings.Enabled) return alerts;

        Check(alerts, "cpu_temp", s.CpuTemp, _settings.CpuTempWarn, _settings.CpuTempCrit);

        Check(alerts, "gpu_temp", s.GpuTemp, _settings.GpuTempWarn, _settings.GpuTempCrit);

        Check(alerts, "commit", s.CommitPct, _settings.CommitWarn, _settings.CommitCrit, s.TopProcesses);

        return alerts;
    }

    /// <summary>Alerte immédiate sur un événement critique observé en direct (WHEA, disque).</summary>
    public PreventiveAlert? EvaluateEvent(string providerAndId, string message)
    {
        if (!_settings.Enabled) return null;

        if (providerAndId.Contains("WHEA-Logger", StringComparison.OrdinalIgnoreCase))
            return Emit("whea", "crit", null, null);

        // Resource-Exhaustion-Detector 2004 : Windows lui-même constate que la mémoire
        // virtuelle est épuisée et nomme les processus responsables. Signal en or.
        if (providerAndId.Contains("Resource-Exhaustion-Detector", StringComparison.OrdinalIgnoreCase))
            return Emit("exhaustion", "crit", null, Shorten(message, 200));

        // Kernel-Power 41 : la machine s'est éteinte sans arrêt propre lors de la session précédente.
        if (providerAndId.Contains("Kernel-Power", StringComparison.OrdinalIgnoreCase))
            return Emit("power41", "crit", null, null);

        if (providerAndId.StartsWith("disk#", StringComparison.OrdinalIgnoreCase) ||
            providerAndId.StartsWith("stornvme#", StringComparison.OrdinalIgnoreCase) ||
            providerAndId.StartsWith("storahci#", StringComparison.OrdinalIgnoreCase) ||
            providerAndId.StartsWith("Ntfs#", StringComparison.OrdinalIgnoreCase))
            return Emit("disk_event", "warn", null, Shorten(message, 160));

        return null;
    }

    /// <summary>Contrôle périodique de la santé des disques (SMART via l'espace de noms Storage).</summary>
    public List<PreventiveAlert> CheckDisksIfDue()
    {
        var alerts = new List<PreventiveAlert>();
        if (!_settings.Enabled || DateTime.Now - _lastDiskCheck < TimeSpan.FromMinutes(_settings.DiskCheckMinutes))
            return alerts;
        _lastDiskCheck = DateTime.Now;

        try
        {
            using var searcher = new ManagementObjectSearcher(
                @"root\Microsoft\Windows\Storage", "SELECT FriendlyName, HealthStatus FROM MSFT_PhysicalDisk");
            foreach (ManagementObject mo in searcher.Get())
            {
                var name = mo["FriendlyName"]?.ToString() ?? "disque";
                // Même table de correspondance que le scan, dans le Core. Valeur
                // absente → 5 (« Unknown »), donc aucune alerte : on ne réveille
                // personne sur une mesure qui n'a pas eu lieu.
                var health = DiskHealthInfo.FromWmi(Convert.ToUInt16(mo["HealthStatus"] ?? (ushort)5));
                if (health.IsDegraded())
                {
                    // Le niveau porte l'état : AlertCatalog le relit pour refabriquer
                    // la phrase. Le modèle du disque, lui, est dans l'identifiant.
                    var a = Emit($"disk_health_{name}", health == DiskHealth.Failing ? "crit" : "warn", null, null);
                    if (a is not null) alerts.Add(a);
                }
            }
        }
        catch { /* espace de noms indisponible : contrôle simplement sauté */ }

        return alerts;
    }

    // ------------------------------------------------------------------

    private void Check(List<PreventiveAlert> alerts, string ruleId, double? value,
        double warn, double crit, string? extract = null)
    {
        if (value is not { } v) return;

        if (v < warn)
        {
            _streaks[ruleId] = 0;   // repassé sous le seuil : la règle se réarme
            return;
        }

        var streak = _streaks.GetValueOrDefault(ruleId) + 1;
        _streaks[ruleId] = streak;
        if (streak < _settings.ConsecutiveSamples) return;

        var level = v >= crit ? "crit" : "warn";
        var alert = Emit(ruleId, level, v, extract);
        if (alert is not null) alerts.Add(alert);
    }

    /// <summary>Crée l'alerte si le délai anti-répétition est écoulé, sinon retourne null.</summary>
    private PreventiveAlert? Emit(string ruleId, string level, double? value, string? extract)
    {
        if (_lastEmitted.TryGetValue(ruleId, out var last) &&
            DateTime.Now - last < TimeSpan.FromMinutes(_settings.RepeatMinutes))
            return null;

        _lastEmitted[ruleId] = DateTime.Now;
        var alert = new PreventiveAlert
        {
            Time = DateTime.Now,
            RuleId = ruleId,
            Level = level,
            Value = value,
            Extract = extract,
        };

        // Le texte est posé par la MÊME table que celle du lecteur. On l'écrit
        // quand même dans le fichier : un journal qu'un humain ouvre au bloc-notes
        // doit rester lisible, et une version future qui ne connaîtrait plus la
        // règle retombera dessus.
        AlertCatalog.Localize(alert);

        Append(alert);
        return alert;
    }

    /// <summary>Journalise l'alerte (une ligne JSON, flush immédiat comme la boîte noire).</summary>
    private static void Append(PreventiveAlert alert)
    {
        try
        {
            var path = AlertSettings.AlertsLogPath;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            RotateIfTooLarge(path);
            using var fs = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read,
                                          bufferSize: 1024, FileOptions.WriteThrough);
            var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(alert) + "\n");
            fs.Write(bytes, 0, bytes.Length);
            fs.Flush(flushToDisk: true);
        }
        catch { /* journal indisponible : l'alerte reste retournée à l'appelant */ }
    }

    /// <summary>
    /// Rotation du journal d'alertes : au-delà de 512 Ko, l'ancien est conservé sous
    /// .old (un seul historique) — le fichier ne peut donc pas croître indéfiniment.
    /// </summary>
    private static void RotateIfTooLarge(string path)
    {
        try
        {
            if (!File.Exists(path) || new FileInfo(path).Length < 512 * 1024) return;
            var old = path + ".old";
            if (File.Exists(old)) File.Delete(old);
            File.Move(path, old);
        }
        catch { }
    }

    private static string Shorten(string s, int max) => s.Length <= max ? s : s[..max] + "…";
}
