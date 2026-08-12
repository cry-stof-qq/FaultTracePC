using System.Diagnostics;
using System.Management;

namespace FaultTracePC.Core.Collectors;

/// <summary>
/// Inventaire des pilotes noyau (Win32_SystemDriver) enrichi des infos de version
/// du fichier .sys (éditeur, version, date) — utile pour repérer un pilote tiers ancien.
/// </summary>
public static class DriverCollector
{
    public static List<DriverInfo> Collect()
    {
        var result = new List<DriverInfo>();
        using var searcher = new ManagementObjectSearcher(
            "SELECT Name, DisplayName, PathName, State, StartMode FROM Win32_SystemDriver");

        foreach (ManagementObject mo in searcher.Get())
        {
            var d = new DriverInfo
            {
                Name = mo["Name"]?.ToString() ?? "",
                DisplayName = mo["DisplayName"]?.ToString() ?? "",
                Path = NormalizePath(mo["PathName"]?.ToString() ?? ""),
                State = mo["State"]?.ToString() ?? "",
                StartMode = mo["StartMode"]?.ToString() ?? "",
            };

            if (!string.IsNullOrEmpty(d.Path) && File.Exists(d.Path))
            {
                try
                {
                    var vi = FileVersionInfo.GetVersionInfo(d.Path);
                    d.FileVersion = vi.FileVersion ?? "";
                    d.CompanyName = vi.CompanyName ?? "";
                    d.FileDate = File.GetLastWriteTime(d.Path);
                    d.IsMicrosoft = d.CompanyName.Contains("Microsoft", StringComparison.OrdinalIgnoreCase);
                }
                catch { /* fichier verrouillé ou inaccessible : on garde les infos WMI */ }
            }

            result.Add(d);
        }

        return result;
    }

    /// <summary>Convertit les chemins noyau (\SystemRoot\..., \??\C:\...) en chemins Win32.</summary>
    private static string NormalizePath(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";
        var p = raw.Trim().Trim('"');
        var windir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

        if (p.StartsWith(@"\SystemRoot\", StringComparison.OrdinalIgnoreCase))
            return Path.Combine(windir, p[@"\SystemRoot\".Length..]);
        if (p.StartsWith(@"\??\", StringComparison.OrdinalIgnoreCase))
            return p[@"\??\".Length..];
        if (p.StartsWith(@"system32\", StringComparison.OrdinalIgnoreCase) ||
            p.StartsWith(@"System32\", StringComparison.Ordinal))
            return Path.Combine(windir, p);
        return p;
    }

    /// <summary>
    /// Recherche un pilote par nom de fichier .sys (ex: "nvlddmkm.sys") dans l'inventaire.
    /// </summary>
    public static DriverInfo? FindBySysName(IEnumerable<DriverInfo> drivers, string sysFileName)
    {
        var baseName = Path.GetFileNameWithoutExtension(sysFileName);
        return drivers.FirstOrDefault(d =>
            string.Equals(Path.GetFileName(d.Path), sysFileName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(d.Name, baseName, StringComparison.OrdinalIgnoreCase));
    }
}
