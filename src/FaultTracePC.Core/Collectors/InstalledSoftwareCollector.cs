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
            errors.Add(Lang.T($"Logiciels installés : {ex.Message}", $"Installed software: {ex.Message}"));
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
    /// <summary>
    /// Forme comparable d'un nom : lettres et chiffres seulement, en minuscules.
    /// Un nom de produit (« Microsoft Edge ») et un nom de fichier (« MicrosoftEdgeUpdate »)
    /// ne s'écrivent pas pareil ; ce n'est pas une raison pour ne pas les reconnaître.
    /// </summary>
    internal static string NomComparable(string? valeur) =>
        new string((valeur ?? "").Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

    public static InstalledApp? FindByExecutable(IEnumerable<InstalledApp> apps, string exeName)
    {
        var stem = Path.GetFileNameWithoutExtension(exeName);
        if (string.IsNullOrWhiteSpace(stem)) return null;

        var list = apps as IList<InstalledApp> ?? apps.ToList();

        // Nom du produit contenant le nom de l'exécutable (ou l'inverse), ESPACES ET
        // PONCTUATION MIS DE CÔTÉ.
        //
        // Constaté le 18/09/2026 sur TECH-INFO-2025 : « MicrosoftEdgeUpdate.exe » ne
        // retrouvait pas « Microsoft Edge », pour la seule raison qu'un nom de produit
        // porte des espaces et pas un nom de fichier. Le rapport concluait « ce logiciel
        // ne figure plus parmi les programmes installés » pendant que six processus
        // msedge tournaient, listés dans le même rapport.
        var racine = NomComparable(stem);
        var byName = list.FirstOrDefault(a =>
        {
            var nom = NomComparable(a.Name);
            if (nom.Length == 0 || racine.Length == 0) return false;
            if (nom.Contains(racine, StringComparison.Ordinal)) return true;
            // Sens inverse : « MicrosoftEdgeUpdate » contient « MicrosoftEdge ». Les deux
            // longueurs minimales évitent qu'un nom de produit court — « Java », « Git » —
            // n'attrape tout exécutable qui le contient par hasard.
            return racine.Length >= 5 && nom.Length >= 5 && racine.Contains(nom, StringComparison.Ordinal);
        });
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
