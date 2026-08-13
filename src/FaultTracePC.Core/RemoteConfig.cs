using System.Net;
using System.Security.Cryptography;
using System.Text.Json;

namespace FaultTracePC.Core;

/// <summary>
/// Configuration du mode réseau, partagée entre l'application et le service
/// (C:\ProgramData\FaultTracePC\remote.json).
///
/// Modes : "Local" (rien d'exposé, défaut) ou "Client" (le service publie une API
/// HTTP en LECTURE SEULE, réservée aux adresses privées RFC 1918 munies du token).
/// Le mode « Maître » n'est pas un état : c'est simplement l'usage de la console
/// Parc depuis n'importe quelle installation.
/// </summary>
public sealed class RemoteConfig
{
    public string Mode { get; set; } = "Local";   // Local | Client
    public int Port { get; set; } = 58620;
    public string Token { get; set; } = "";

    public static string BaseDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "FaultTracePC");

    public static string ConfigPath => Path.Combine(BaseDir, "remote.json");

    /// <summary>Dossier des rapports partagés (copie servie par l'API distante).</summary>
    public static string SharedReportsDir => Path.Combine(BaseDir, "Reports");

    public static RemoteConfig Load()
    {
        try
        {
            if (File.Exists(ConfigPath) &&
                JsonSerializer.Deserialize<RemoteConfig>(File.ReadAllText(ConfigPath)) is { } cfg)
            {
                if (cfg.Port is < 1024 or > 65535) cfg.Port = 58620;
                return cfg;
            }
        }
        catch { /* config corrompue : retour au défaut Local */ }
        return new RemoteConfig();
    }

    public void Save()
    {
        Directory.CreateDirectory(BaseDir);
        File.WriteAllText(ConfigPath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }

    /// <summary>Génère un token aléatoire de 256 bits (hexadécimal).</summary>
    public static string GenerateToken() =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    /// <summary>Plages autorisées : boucle locale + RFC 1918 (10/8, 172.16/12, 192.168/16).</summary>
    public static bool IsPrivateOrLoopback(IPAddress? address)
    {
        if (address is null) return false;
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        if (IPAddress.IsLoopback(address)) return true;
        if (address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork) return false;
        var b = address.GetAddressBytes();
        return b[0] == 10
            || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
            || (b[0] == 192 && b[1] == 168);
    }

    /// <summary>Comparaison de tokens en temps constant (anti-mesure de temps).</summary>
    public static bool TokenMatches(string expected, string? provided)
    {
        if (string.IsNullOrEmpty(expected) || string.IsNullOrEmpty(provided)) return false;
        var a = System.Text.Encoding.UTF8.GetBytes(expected);
        var b = System.Text.Encoding.UTF8.GetBytes(provided);
        return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b);
    }
}
