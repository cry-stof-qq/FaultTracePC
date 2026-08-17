using System.Diagnostics;
using System.Runtime.InteropServices;

using FaultTracePC.Core;

namespace FaultTracePC.App;

/// <summary>
/// Suit les fenêtres PowerShell ouvertes par la boîte à outils.
///
/// Pourquoi ce n'est pas qu'un simple avertissement : deux réparations qui écrivent
/// en même temps ne sont pas seulement désordonnées, elles sont dangereuses.
/// « sfc /scannow » et « DISM /RestoreHealth » lancés simultanément se disputent le
/// magasin de composants ; deux chkdsk sur le même volume, ou un nettoyage de disque
/// pendant une analyse antivirus complète, se gênent mutuellement et peuvent laisser
/// le système dans un état incohérent.
///
/// D'où la distinction :
///  · action EXCLUSIVE (elle modifie le système) — une seule à la fois, on propose
///    de basculer vers la fenêtre déjà ouverte ;
///  · action de LECTURE (inventaire, rapport, consultation) — aucune restriction,
///    il serait absurde d'empêcher de regarder l'espace disque pendant un sfc.
/// </summary>
public static class RunningTools
{
    private sealed record Entry(string Label, Process Process, bool Exclusive);

    private static readonly List<Entry> Entries = new();
    private static readonly object Gate = new();

    // ------------------------------------------------------------------
    // Win32 : ramener une fenêtre existante au premier plan
    // ------------------------------------------------------------------

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private const int SwRestore = 9;

    // ------------------------------------------------------------------

    /// <summary>Retire les processus terminés. À appeler avant toute interrogation.</summary>
    private static void Prune()
    {
        Entries.RemoveAll(e =>
        {
            try { if (!e.Process.HasExited) return false; } catch { /* accès perdu */ }
            try { e.Process.Dispose(); } catch { }
            return true;
        });
    }

    /// <summary>Action exclusive en cours, ou null. Le libellé sert au message.</summary>
    public static string? BlockingLabel()
    {
        lock (Gate)
        {
            Prune();
            return Entries.FirstOrDefault(e => e.Exclusive)?.Label;
        }
    }

    public static int RunningCount
    {
        get { lock (Gate) { Prune(); return Entries.Count; } }
    }

    public static void Track(string label, Process process, bool exclusive)
    {
        lock (Gate) { Prune(); Entries.Add(new Entry(label, process, exclusive)); }
    }

    /// <summary>
    /// Ramène au premier plan la fenêtre de l'action exclusive en cours.
    /// Renvoie faux si la fenêtre n'a pas (ou plus) de handle exploitable.
    /// </summary>
    public static bool FocusBlocking()
    {
        lock (Gate)
        {
            Prune();
            var e = Entries.FirstOrDefault(x => x.Exclusive);
            if (e is null) return false;
            try
            {
                e.Process.Refresh();
                var h = e.Process.MainWindowHandle;
                if (h == IntPtr.Zero) return false;
                ShowWindow(h, SwRestore);
                return SetForegroundWindow(h);
            }
            catch { return false; }
        }
    }

    /// <summary>
    /// Les actions qui MODIFIENT le système. Tout ce qui n'est pas listé ici est
    /// considéré comme de la consultation et n'est jamais bloqué.
    /// </summary>
    private static readonly HashSet<string> ExclusiveTools = new(StringComparer.OrdinalIgnoreCase)
    {
        "sfc", "dismscan", "dismrestore", "chkdskscan", "chkdskfix", "mdsched",
        "wureset", "componentcleanup", "temp", "cleanmgr",
        "defenderquick", "defenderfull", "networkreset", "restorepoint", "uninstallkb",
        // Installer un logiciel modifie le système : même règle que les autres.
        // « linkpower » n'y figure PAS : il affiche un réglage et ouvre un panneau,
        // il n'écrit rien.
        "windbg",
    };

    public static bool IsExclusive(string tool) => ExclusiveTools.Contains(tool);

    /// <summary>
    /// Libellés lisibles, pour que le message dise QUOI tourne encore.
    /// Les deux langues sont stockées côte à côte : une table « static readonly »
    /// est construite une seule fois, donc appeler Lang.T ici figerait la langue
    /// au premier accès au type. La résolution se fait dans LabelOf, à la lecture.
    /// </summary>
    private static readonly Dictionary<string, (string Fr, string En)> Labels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["sfc"] = ("Vérification des fichiers système (sfc /scannow)", "System file check (sfc /scannow)"),
        ["dismscan"] = ("Vérification de l'image Windows (DISM)", "Windows image check (DISM)"),
        ["dismrestore"] = ("Réparation de l'image Windows (DISM)", "Windows image repair (DISM)"),
        ["chkdskscan"] = ("Analyse du disque système", "System disk scan"),
        ["chkdskfix"] = ("Planification de chkdsk", "chkdsk scheduling"),
        ["mdsched"] = ("Diagnostic mémoire Windows", "Windows Memory Diagnostic"),
        ["wureset"] = ("Réinitialisation des composants Windows Update", "Windows Update components reset"),
        ["componentcleanup"] = ("Purge des composants Windows obsolètes", "Superseded Windows components cleanup"),
        ["temp"] = ("Vidage des fichiers temporaires", "Temporary files cleanup"),
        ["cleanmgr"] = ("Nettoyage de disque Windows", "Windows Disk Cleanup"),
        ["defenderquick"] = ("Analyse rapide Microsoft Defender", "Microsoft Defender quick scan"),
        ["defenderfull"] = ("Analyse complète Microsoft Defender", "Microsoft Defender full scan"),
        ["networkreset"] = ("Réinitialisation de la pile réseau", "Network stack reset"),
        ["restorepoint"] = ("Création du point de restauration", "Restore point creation"),
        ["uninstallkb"] = ("Désinstallation d'une mise à jour Windows", "Windows update uninstall"),
        ["energy"] = ("Rapport d'énergie", "Energy report"),
        ["battery"] = ("Rapport de batterie", "Battery report"),
        ["smart"] = ("Lecture SMART des disques", "Disk SMART read"),
        ["diskusage"] = ("Analyse de l'occupation disque", "Disk usage analysis"),
        ["startup"] = ("Inventaire des programmes au démarrage", "Startup programs inventory"),
        ["defenderhistory"] = ("Historique des menaces détectées", "Detected threats history"),
    };

    public static string LabelOf(string tool) =>
        Labels.TryGetValue(tool, out var l) ? Lang.T(l.Fr, l.En) : tool;
}
