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

        Check(alerts, "cpu_temp", s.CpuTemp, _settings.CpuTempWarn, _settings.CpuTempCrit,
            v => $"Température du processeur élevée : {v:0} °C",
            v => $"Le CPU dépasse {v:0} °C de façon soutenue. Au-delà de ~95 °C, le processeur se bride puis la machine peut s'éteindre brutalement pour se protéger.",
            "Vérifier la ventilation : dépoussiérer radiateur et ventilateurs, contrôler leur rotation, renouveler la pâte thermique si la machine a plus de 3-4 ans. Fermer les applications qui chargent le CPU pour tester.");

        Check(alerts, "gpu_temp", s.GpuTemp, _settings.GpuTempWarn, _settings.GpuTempCrit,
            v => $"Température de la carte graphique élevée : {v:0} °C",
            v => $"Le GPU dépasse {v:0} °C de façon soutenue — risque d'écran noir, de réinitialisation du pilote (TDR) ou d'arrêt brutal.",
            "Dépoussiérer la carte et le flux d'air du boîtier ; vérifier la courbe de ventilation ; retirer tout overclocking.");

        Check(alerts, "commit", s.CommitPct, _settings.CommitWarn, _settings.CommitCrit,
            v => $"Mémoire virtuelle presque saturée : {v:0} %",
            v => $"La mémoire engagée (RAM + fichier d'échange) atteint {v:0} %. À saturation, Windows gèle, les applications plantent et des écrans bleus mémoire peuvent survenir."
                 + (s.TopProcesses is not null ? $" Processus dominants : {s.TopProcesses}." : ""),
            "Fermer les applications les plus gourmandes ; si la virtualisation (vmmem/WSL/Docker) est en tête, lui fixer une limite via %USERPROFILE%\\.wslconfig ([wsl2] puis memory=8GB), puis « wsl --shutdown ».");

        return alerts;
    }

    /// <summary>Alerte immédiate sur un événement critique observé en direct (WHEA, disque).</summary>
    public PreventiveAlert? EvaluateEvent(string providerAndId, string message)
    {
        if (!_settings.Enabled) return null;

        if (providerAndId.Contains("WHEA-Logger", StringComparison.OrdinalIgnoreCase))
            return Emit("whea", "crit", "Erreur matérielle signalée par le processeur (WHEA)",
                "Le matériel vient de signaler une erreur corrigée ou fatale. Répétées, ces erreurs annoncent une défaillance CPU, mémoire, carte mère ou alimentation.",
                "Vérifier températures et alimentation, retirer tout overclocking/XMP, mettre à jour le BIOS. Si les erreurs persistent, faire tester le matériel.", null);

        // Resource-Exhaustion-Detector 2004 : Windows lui-même constate que la mémoire
        // virtuelle est épuisée et nomme les processus responsables. Signal en or.
        if (providerAndId.Contains("Resource-Exhaustion-Detector", StringComparison.OrdinalIgnoreCase))
            return Emit("exhaustion", "crit", "Mémoire épuisée — Windows a manqué de mémoire virtuelle",
                "Windows signale l'épuisement de la mémoire virtuelle : " + Shorten(message, 200),
                "Fermer le programme le plus gourmand cité ci-dessus. Si c'est la virtualisation (vmmem/WSL/Docker), lui fixer une limite via %USERPROFILE%\\.wslconfig ([wsl2] puis memory=8GB) et exécuter « wsl --shutdown ». Vérifier aussi que le fichier d'échange est géré automatiquement.", null);

        // Kernel-Power 41 : la machine s'est éteinte sans arrêt propre lors de la session précédente.
        if (providerAndId.Contains("Kernel-Power", StringComparison.OrdinalIgnoreCase))
            return Emit("power41", "crit", "Arrêt brutal détecté (coupure sans arrêt propre)",
                "Le système s'est éteint ou a redémarré sans arrêt propre. Causes typiques : alimentation défaillante, surchauffe déclenchant la protection, ou blocage matériel complet.",
                "Vérifier les températures en charge et le branchement électrique ; si cela se répète, tester une autre alimentation. Le journal de la boîte noire montre les relevés juste avant la coupure.", null);

        if (providerAndId.StartsWith("disk#", StringComparison.OrdinalIgnoreCase) ||
            providerAndId.StartsWith("stornvme#", StringComparison.OrdinalIgnoreCase) ||
            providerAndId.StartsWith("storahci#", StringComparison.OrdinalIgnoreCase) ||
            providerAndId.StartsWith("Ntfs#", StringComparison.OrdinalIgnoreCase))
            return Emit("disk_event", "warn", "Erreur disque signalée par Windows",
                "Windows vient d'enregistrer une erreur d'entrée/sortie sur un disque : " + Shorten(message, 160),
                "Sauvegarder les données importantes sans attendre, vérifier la santé SMART du disque et ses câbles, mettre à jour le firmware du SSD.", null);

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
                var health = Convert.ToInt32(mo["HealthStatus"] ?? 0);
                if (health is 1 or 2) // 1 = Warning, 2 = Unhealthy
                {
                    var a = Emit($"disk_health_{name}", health == 2 ? "crit" : "warn",
                        $"Disque en mauvaise santé : {name}",
                        $"Windows signale l'état « {(health == 2 ? "défaillant" : "avertissement")} » pour ce disque. Une panne de disque fait perdre les données ET rend la machine non démarrable.",
                        "SAUVEGARDER immédiatement les données, puis prévoir le remplacement du disque. Vérifier le rapport SMART complet pour confirmation.", null);
                    if (a is not null) alerts.Add(a);
                }
            }
        }
        catch { /* espace de noms indisponible : contrôle simplement sauté */ }

        return alerts;
    }

    // ------------------------------------------------------------------

    private void Check(List<PreventiveAlert> alerts, string ruleId, double? value,
        double warn, double crit,
        Func<double, string> title, Func<double, string> details, string reco)
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
        var alert = Emit(ruleId, level, title(v), details(v), reco, v);
        if (alert is not null) alerts.Add(alert);
    }

    /// <summary>Crée l'alerte si le délai anti-répétition est écoulé, sinon retourne null.</summary>
    private PreventiveAlert? Emit(string ruleId, string level, string title, string details, string reco, double? value)
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
            Title = title,
            Details = details,
            Recommendation = reco,
            Value = value,
        };
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
