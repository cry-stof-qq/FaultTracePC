using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

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

        // Le fichier porte le jeton de CETTE machine, et les permissions par défaut
        // de ProgramData laissent le groupe Utilisateurs lire ce qui s'y trouve.
        // L'échec n'interrompt pas l'enregistrement — une configuration écrite mais
        // mal protégée vaut mieux qu'une configuration perdue —, mais il laisse une
        // trace : personne ne doit croire le fichier protégé sans l'avoir vérifié.
        if (!FileProtection.RestrictToSystemAndAdministrators(ConfigPath, out var erreur))
            ErrorLog.Write("RemoteConfig.Save/ACL", erreur);
    }

    /// <summary>
    /// Vrai si <see cref="ConfigPath"/> est effectivement réservé à SYSTEM et aux
    /// Administrateurs. Se relit sur le disque : on ne suppose pas qu'un appel
    /// passé a réussi, et une main humaine a pu rouvrir le fichier depuis.
    /// </summary>
    public static bool ConfigEstProtegee => FileProtection.IsRestricted(ConfigPath);

    /// <summary>
    /// Vrai si cette configuration doit faire écouter l'API.
    ///
    /// Le jeton vide compte autant que le mode : un poste installé par MSI mais
    /// pas encore configuré n'a rien à protéger, et exposer un port sans clé
    /// serait le pire des deux mondes.
    ///
    /// JsonIgnore n'est pas décoratif : sans lui, cette propriété CALCULÉE partait
    /// dans remote.json — constaté le 30/08/2026 sur une machine réelle. Elle y
    /// était inoffensive (aucun setter, donc ignorée à la relecture) mais le
    /// fichier portait un champ redondant capable de contredire les trois autres.
    /// </summary>
    [JsonIgnore]
    public bool ModeClientActif =>
        string.Equals(Mode, "Client", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(Token);

    /// <summary>
    /// Vrai si l'autre configuration expose EXACTEMENT la même chose : même mode
    /// effectif, même port, même jeton. Sert au service à savoir s'il doit
    /// repartir sur de nouvelles bases — réécrire le fichier à l'identique ne doit
    /// pas couper les connexions en cours.
    /// </summary>
    public bool MemeExpositionQue(RemoteConfig? autre) =>
        autre is not null
        && ModeClientActif == autre.ModeClientActif
        && Port == autre.Port
        && string.Equals(Token, autre.Token, StringComparison.Ordinal);

    /// <summary>
    /// Génère un token aléatoire de 256 bits (hexadécimal).
    ///
    /// PLUS AUCUN CHEMIN DE CONFIGURATION NE L'APPELLE : un poste tire désormais
    /// son jeton du secret maître (<see cref="DeriveToken"/>), sans quoi la console
    /// ne saurait pas le recalculer. Conservé pour ce qu'il reste de jetons posés
    /// avant — et pour les tests, qui en fabriquent un afin de vérifier que la
    /// dérogation l'emporte bien sur la dérivation.
    /// </summary>
    public static string GenerateToken() =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    /// <summary>
    /// Longueur minimale du secret maître.
    ///
    /// Ce n'est pas une coquetterie : un secret deviné ou forcé donne accès à
    /// TOUT le parc d'un coup, puisque tous les jetons s'en déduisent. Une phrase
    /// de passe retenue de tête n'est donc pas acceptable ici, et le refuser
    /// franchement vaut mieux que l'accepter en espérant qu'elle soit bonne.
    /// <see cref="GenerateMasterSecret"/> en produit un correct ; il se range dans
    /// un gestionnaire de mots de passe, il n'a pas à être mémorisé.
    /// </summary>
    public const int MasterSecretMinLength = 32;

    /// <summary>Secret maître de parc : 256 bits aléatoires, en hexadécimal.</summary>
    public static string GenerateMasterSecret() =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    /// <summary>
    /// Jeton d'un poste, DÉDUIT du secret maître et du nom de la machine.
    ///
    /// POURQUOI DÉDUIRE PLUTÔT QUE TIRER AU SORT
    /// Jusqu'ici chaque poste tirait un jeton aléatoire, et la console gardait la
    /// liste complète dans un fichier de son dossier Documents. Trois conséquences :
    /// aucun export possible, la liste disparaît avec un profil Windows reconstruit,
    /// et sur un poste dont le dossier Documents est redirigé, les jetons de tout le
    /// parc atterrissent sur un partage réseau.
    ///
    /// Déduits, il n'y a plus de liste : la console recalcule le jeton de chaque
    /// machine à partir d'un seul secret.
    ///
    /// CE QUE CELA DÉPLACE, ET QU'IL FAUT SAVOIR
    /// Le secret maître devient le seul point sensible. Mais il ne remplace pas un
    /// risque par un pire : le fichier d'aujourd'hui expose DÉJÀ tous les jetons
    /// d'un coup, dans un dossier utilisateur redirigeable. Le secret, lui, ne vit
    /// que sur la console — un poste ne le reçoit jamais, il ne reçoit que son
    /// propre jeton dérivé.
    ///
    /// LE NOM EST NORMALISÉ
    /// Windows rend le nom de machine tantôt en majuscules, tantôt tel qu'il a été
    /// saisi. Une console qui écrirait « poste-01 » et un poste qui se nomme
    /// « POSTE-01 » ne calculeraient pas le même jeton, et l'interrogation
    /// échouerait sans que rien n'explique pourquoi.
    /// </summary>
    public static string DeriveToken(string masterSecret, string machineName)
    {
        var nom = (machineName ?? "").Trim().ToUpperInvariant();
        if (nom.Length == 0)
            throw new ArgumentException("machineName", nameof(machineName));
        if (masterSecret is null || masterSecret.Trim().Length < MasterSecretMinLength)
            throw new ArgumentException("masterSecret", nameof(masterSecret));

        using var hmac = new HMACSHA256(System.Text.Encoding.UTF8.GetBytes(masterSecret.Trim()));
        return Convert.ToHexString(hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(nom)));
    }

    /// <summary>
    /// Jeton à employer pour une machine du parc.
    ///
    /// Règle : un jeton INSCRIT l'emporte — c'est la dérogation qui laisse vivre
    /// les postes configurés avant le secret maître, avec leur jeton tiré au sort.
    /// Sinon on le déduit. Null quand ni l'un ni l'autre n'est possible, et c'est
    /// un résultat, pas une panne : l'appelant doit le DIRE à l'utilisateur plutôt
    /// que de signer sa requête avec une chaîne vide, qui reviendrait avec un
    /// « refusé » que personne ne saurait interpréter.
    ///
    /// Cette règle vit ici, et non dans la fenêtre de la console, pour qu'un test
    /// puisse l'exercer : le projet WPF n'est pas compilé par « dotnet test ».
    /// </summary>
    public static string? TokenFor(string? jetonInscrit, string? masterSecret, string machineName)
    {
        if (!string.IsNullOrWhiteSpace(jetonInscrit)) return jetonInscrit.Trim();
        if (string.IsNullOrWhiteSpace(masterSecret)) return null;

        try { return DeriveToken(masterSecret, machineName); }
        catch (ArgumentException) { return null; }
    }

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

    /// <summary>Comparaison de chaînes secrètes en temps constant (anti-mesure de temps).</summary>
    public static bool TokenMatches(string expected, string? provided)
    {
        if (string.IsNullOrEmpty(expected) || string.IsNullOrEmpty(provided)) return false;
        var a = System.Text.Encoding.UTF8.GetBytes(expected);
        var b = System.Text.Encoding.UTF8.GetBytes(provided);
        return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b);
    }

    // ------------------------------------------------------------------
    // Authentification par signature HMAC-SHA256
    //
    // Le token ne circule JAMAIS sur le réseau : le client signe chaque requête
    // (méthode + chemin + paramètres + horodatage + nonce) avec le token comme
    // clé, et le serveur recalcule la même signature. Une écoute du trafic ne
    // révèle donc rien d'exploitable, et l'horodatage + le nonce empêchent de
    // rejouer une requête capturée.
    // ------------------------------------------------------------------

    public const string HeaderTimestamp = "X-FaultTrace-Ts";
    public const string HeaderNonce = "X-FaultTrace-Nonce";
    public const string HeaderSignature = "X-FaultTrace-Sig";

    /// <summary>Tolérance d'horloge entre les deux machines (secondes).</summary>
    public const int ClockToleranceSeconds = 300;

    /// <summary>
    /// Dérive la clé HMAC du token. Un token généré par l'application est de
    /// l'hexadécimal ; s'il a été saisi à la main (phrase quelconque), on en prend
    /// le SHA-256 — les deux extrémités appliquent la même règle, donc pas d'échec
    /// silencieux ni d'exception sur un token « non conforme ».
    /// </summary>
    private static byte[] KeyFromToken(string token)
    {
        var t = token.Trim();
        return t.Length > 0 && t.Length % 2 == 0 && t.All(Uri.IsHexDigit)
            ? Convert.FromHexString(t)
            : SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(t));
    }

    private static string ComputeSignature(string token, string method, string path, string query, string timestamp, string nonce)
    {
        var payload = $"{method.ToUpperInvariant()}\n{path.ToLowerInvariant()}\n{query}\n{timestamp}\n{nonce}";
        using var hmac = new HMACSHA256(KeyFromToken(token));
        return Convert.ToHexString(hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(payload)));
    }

    /// <summary>En-têtes d'authentification à joindre à une requête (côté console maître).</summary>
    public static Dictionary<string, string> BuildAuthHeaders(string token, string method, string path, string query)
    {
        var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(12));
        return new Dictionary<string, string>
        {
            [HeaderTimestamp] = ts,
            [HeaderNonce] = nonce,
            [HeaderSignature] = ComputeSignature(token, method, path, query, ts, nonce),
        };
    }

    /// <summary>
    /// Vérifie la signature d'une requête entrante (côté service).
    /// <paramref name="isNonceFresh"/> permet à l'appelant de rejeter un nonce déjà vu.
    /// </summary>
    public static bool VerifySignature(string token, string method, string path, string query,
                                       string? timestamp, string? nonce, string? signature,
                                       Func<string, bool> isNonceFresh)
    {
        if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(timestamp) ||
            string.IsNullOrEmpty(nonce) || string.IsNullOrEmpty(signature))
            return false;

        if (!long.TryParse(timestamp, out var unix)) return false;
        var age = Math.Abs(DateTimeOffset.UtcNow.ToUnixTimeSeconds() - unix);
        if (age > ClockToleranceSeconds) return false;   // requête trop ancienne ou horloge décalée

        // ORDRE IMPORTANT : on vérifie la signature AVANT de consommer le nonce.
        // Sinon, n'importe qui sur le réseau pourrait remplir la mémoire du service
        // de nonces sans jamais s'authentifier.
        var expected = ComputeSignature(token, method, path, query, timestamp, nonce);
        if (!TokenMatches(expected, signature)) return false;

        return isNonceFresh(nonce);                      // rejeu d'une requête déjà servie
    }
}
