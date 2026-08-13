using Microsoft.Win32;

namespace FaultTracePC.Core.Collectors;

/// <summary>
/// Inventaire des logiciels installés, lu dans les clés de désinstallation du
/// registre (64 bits, 32 bits, et par utilisateur). Sert à répondre à la question
/// qui manquait au rapport : « ce logiciel qui plantait il y a trois semaines,
/// est-il encore installé, a-t-il été mis à jour, ou l'ai-je désinstallé ? »
///
/// On lit le registre plutôt que Win32_Product : cette classe WMI déclenche une
/// revalidation MSI de chaque produit installé, ce qui est lent et peut générer
/// des événements d'installation parasites.
/// </summary>
public static class InstalledSoftwareCollector
{
    private static readonly string[] UninstallKeys =
    [
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
        @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall",
    ];

    public static List<InstalledApp> Collect(List<string> errors)
    {
        var apps = new List<InstalledApp>();
        try
        {
            foreach (var path in UninstallKeys)
            {
                ReadFrom(Registry.LocalMachine, path, apps);
                ReadFrom(Registry.CurrentUser, path, apps);
            }
        }
        catch (Exception ex)
        {
            errors.Add($"Logiciels installés : {ex.Message}");
        }

        return apps
            .GroupBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private static void ReadFrom(RegistryKey root, string path, List<InstalledApp> apps)
    {
        try
        {
            using var key = root.OpenSubKey(path);
            if (key is null) return;

            foreach (var subName in key.GetSubKeyNames())
            {
                try
                {
                    using var sub = key.OpenSubKey(subName);
                    if (sub is null) continue;

                    var name = sub.GetValue("DisplayName")?.ToString();
                    if (string.IsNullOrWhiteSpace(name)) continue;
                    // Correctifs et composants système : sans intérêt pour l'utilisateur.
                    if (sub.GetValue("SystemComponent") is int sc && sc == 1) continue;
                    if (sub.GetValue("ParentKeyName") is not null) continue;

                    apps.Add(new InstalledApp
                    {
                        Name = name.Trim(),
                        Version = sub.GetValue("DisplayVersion")?.ToString() ?? "",
                        Publisher = sub.GetValue("Publisher")?.ToString() ?? "",
                        InstallDate = ParseInstallDate(sub.GetValue("InstallDate")?.ToString()),
                        InstallLocation = sub.GetValue("InstallLocation")?.ToString() ?? "",
                    });
                }
                catch { /* sous-clé illisible : ignorée */ }
            }
        }
        catch { /* ruche inaccessible */ }
    }

    /// <summary>Le registre stocke la date au format AAAAMMJJ.</summary>
    private static DateTime? ParseInstallDate(string? raw) =>
        !string.IsNullOrWhiteSpace(raw) &&
        DateTime.TryParseExact(raw, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out var d)
            ? d : null;

    /// <summary>
    /// Retrouve un logiciel à partir du nom d'exécutable relevé dans un crash
    /// (ex. « photoshop.exe ») : correspondance sur le nom du produit, puis sur
    /// le dossier d'installation.
    /// </summary>
    public static InstalledApp? FindByExecutable(IEnumerable<InstalledApp> apps, string exeName)
    {
        var stem = Path.GetFileNameWithoutExtension(exeName);
        if (string.IsNullOrWhiteSpace(stem)) return null;

        var list = apps as IList<InstalledApp> ?? apps.ToList();

        // Nom du produit contenant le nom de l'exécutable (ou l'inverse).
        var byName = list.FirstOrDefault(a =>
            a.Name.Contains(stem, StringComparison.OrdinalIgnoreCase) ||
            (stem.Length >= 5 && stem.Contains(a.Name, StringComparison.OrdinalIgnoreCase)));
        if (byName is not null) return byName;

        // Sinon : l'exécutable existe-t-il dans le dossier d'installation ?
        foreach (var a in list.Where(a => !string.IsNullOrEmpty(a.InstallLocation)))
        {
            try
            {
                if (Directory.Exists(a.InstallLocation) &&
                    Directory.EnumerateFiles(a.InstallLocation, stem + ".exe", SearchOption.TopDirectoryOnly).Any())
                    return a;
            }
            catch { /* chemin invalide ou inaccessible */ }
        }
        return null;
    }
}
