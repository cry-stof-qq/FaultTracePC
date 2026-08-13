using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;

namespace FaultTracePC.Core;

/// <summary>
/// Vérification de version, volontairement minimale et honnête.
///
/// Choix assumés :
///  • la source de vérité est l'API GitHub Releases du dépôt — rien à héberger,
///    et c'est déjà là que sont publiés le MSI et le ZIP ;
///  • FaultTracePC ne télécharge JAMAIS la mise à jour et ne s'installe JAMAIS
///    tout seul. Sur un parc déployé par GPO, un exécutable qui se met à jour
///    sans qu'on le lui demande est un risque, pas un service. On informe, on
///    ouvre la page de téléchargement, l'administrateur décide ;
///  • la vérification au démarrage est DÉSACTIVÉE par défaut. Un logiciel de
///    diagnostic installé en établissement ne doit pas sortir sur Internet
///    sans que quelqu'un l'ait explicitement autorisé ;
///  • en cas d'échec (pas d'Internet — c'est le cas normal en mode parc, proxy,
///    filtrage), on le dit clairement et on ne bloque rien.
/// </summary>
public static class UpdateChecker
{
    /// <summary>Dépôt de référence. Une seule constante à changer en cas de renommage.</summary>
    public const string Repository = "cry-stof-qq/FaultTracePC";

    private const string ApiUrl = "https://api.github.com/repos/" + Repository + "/releases/latest";

    /// <summary>Page vers laquelle on renvoie l'utilisateur (aucun téléchargement automatique).</summary>
    public const string ReleasesPage = "https://github.com/" + Repository + "/releases/latest";

    // ------------------------------------------------------------------

    public sealed class UpdateInfo
    {
        /// <summary>Version installée, lue dans l'assembly (jamais codée en dur).</summary>
        public Version Current { get; init; } = new(0, 0, 0);

        /// <summary>Version publiée, ou null si la vérification n'a pas abouti.</summary>
        public Version? Latest { get; init; }

        public string LatestTag { get; init; } = "";
        public string ReleaseName { get; init; } = "";
        public string ReleaseNotes { get; init; } = "";
        public DateTimeOffset? PublishedAt { get; init; }
        public string DownloadPage { get; init; } = ReleasesPage;

        /// <summary>Nom + taille des fichiers publiés (MSI, ZIP…), pour information.</summary>
        public List<(string Name, long Bytes)> Assets { get; init; } = new();

        /// <summary>Message d'échec lisible, ou null si tout s'est bien passé.</summary>
        public string? Error { get; init; }

        public bool Succeeded => Error is null && Latest is not null;
        public bool UpdateAvailable => Succeeded && Latest! > Current;

        public string Summary => Error is not null
            ? "Vérification impossible : " + Error
            : Latest is null
                ? "Aucune version publiée n'a été trouvée sur GitHub."
                : UpdateAvailable
                    ? $"Version {Latest} disponible (tu utilises la {Current})."
                    : Latest < Current
                        ? $"Ta version ({Current}) est plus récente que la dernière publiée ({Latest}) — build de développement."
                        : $"FaultTracePC est à jour (version {Current}).";
    }

    // ------------------------------------------------------------------

    /// <summary>Version de l'application en cours d'exécution.</summary>
    public static Version CurrentVersion
    {
        get
        {
            var asm = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
            // AssemblyInformationalVersion porte le « 1.1.0 » du csproj, parfois
            // suffixé d'un hash de commit (« 1.1.0+abc1234 ») : on tronque.
            var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (!string.IsNullOrWhiteSpace(info))
            {
                var clean = info.Split('+', '-')[0];
                if (Version.TryParse(clean, out var v)) return Normalize(v);
            }
            return Normalize(asm.GetName().Version ?? new Version(0, 0, 0));
        }
    }

    /// <summary>Ramène toujours à Majeur.Mineur.Correctif (le champ « revision » de .NET fausse les comparaisons).</summary>
    private static Version Normalize(Version v) =>
        new(v.Major, v.Minor, v.Build < 0 ? 0 : v.Build);

    /// <summary>Analyse « v1.2.0 », « 1.2 », « 1.2.0-beta » → Version, ou null.</summary>
    public static Version? ParseTag(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) return null;
        var s = tag.Trim().TrimStart('v', 'V').Split('+', '-')[0];
        var parts = s.Split('.');
        if (parts.Length is < 2 or > 4) return null;
        var nums = new int[3];
        for (int i = 0; i < 3; i++)
        {
            if (i >= parts.Length) { nums[i] = 0; continue; }
            if (!int.TryParse(parts[i], out nums[i]) || nums[i] < 0) return null;
        }
        return new Version(nums[0], nums[1], nums[2]);
    }

    // ------------------------------------------------------------------

    /// <summary>
    /// Interroge GitHub. Ne lève jamais : toute erreur revient dans <c>Error</c>.
    /// </summary>
    public static async Task<UpdateInfo> CheckAsync(CancellationToken ct = default)
    {
        var current = CurrentVersion;
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
            // GitHub refuse les requêtes sans User-Agent.
            http.DefaultRequestHeaders.UserAgent.Add(
                new ProductInfoHeaderValue("FaultTracePC", current.ToString()));
            http.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

            using var resp = await http.GetAsync(ApiUrl, ct).ConfigureAwait(false);

            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
                // GitHub renvoie 404 dans DEUX cas indiscernables de l'extérieur :
                // aucune version publiée, ou dépôt privé (une requête anonyme ne le
                // voit pas). On ne tranche pas ce qu'on ne peut pas savoir.
                return new UpdateInfo
                {
                    Current = current,
                    Error = "aucune version publiée n'est visible sur le dépôt (soit aucune n'existe encore, "
                          + "soit le dépôt est privé — une requête anonyme ne peut pas le distinguer).",
                };
            if ((int)resp.StatusCode == 403)
                return new UpdateInfo { Current = current, Error = "GitHub a refusé la requête (quota d'appels anonymes atteint). Réessaie dans une heure." };
            if (!resp.IsSuccessStatusCode)
                return new UpdateInfo { Current = current, Error = $"réponse HTTP {(int)resp.StatusCode} de GitHub." };

            await using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
            var root = doc.RootElement;

            string tag = root.TryGetProperty("tag_name", out var t) ? t.GetString() ?? "" : "";
            var latest = ParseTag(tag);
            if (latest is null)
                return new UpdateInfo { Current = current, Error = $"numéro de version illisible côté GitHub (« {tag} »)." };

            var assets = new List<(string, long)>();
            if (root.TryGetProperty("assets", out var arr) && arr.ValueKind == JsonValueKind.Array)
            {
                foreach (var a in arr.EnumerateArray())
                {
                    var name = a.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                    long size = a.TryGetProperty("size", out var s) && s.TryGetInt64(out var sv) ? sv : 0;
                    if (name.Length > 0) assets.Add((name, size));
                }
            }

            var notes = root.TryGetProperty("body", out var b) ? b.GetString() ?? "" : "";
            if (notes.Length > 4000) notes = notes[..4000] + "…";

            return new UpdateInfo
            {
                Current = current,
                Latest = latest,
                LatestTag = tag,
                ReleaseName = root.TryGetProperty("name", out var rn) ? rn.GetString() ?? tag : tag,
                ReleaseNotes = notes.Trim(),
                PublishedAt = root.TryGetProperty("published_at", out var p) && p.TryGetDateTimeOffset(out var d) ? d : null,
                DownloadPage = root.TryGetProperty("html_url", out var u) ? u.GetString() ?? ReleasesPage : ReleasesPage,
                Assets = assets,
            };
        }
        catch (OperationCanceledException)
        {
            return new UpdateInfo { Current = current, Error = "délai dépassé — pas de connexion vers github.com (normal sur un poste sans Internet)." };
        }
        catch (HttpRequestException ex)
        {
            return new UpdateInfo { Current = current, Error = $"github.com est injoignable ({ex.Message.Trim()}). Poste sans Internet, proxy ou filtrage." };
        }
        catch (Exception ex)
        {
            return new UpdateInfo { Current = current, Error = ex.Message.Trim() };
        }
    }

    /// <summary>Ouvre la page de téléchargement dans le navigateur par défaut. Aucun téléchargement automatique.</summary>
    public static void OpenDownloadPage(string? url = null)
    {
        var target = string.IsNullOrWhiteSpace(url) ? ReleasesPage : url!;
        if (!target.StartsWith("https://github.com/", StringComparison.OrdinalIgnoreCase)) target = ReleasesPage;
        try { Process.Start(new ProcessStartInfo(target) { UseShellExecute = true }); } catch { /* pas de navigateur */ }
    }

    // ------------------------------------------------------------------
    // Préférence « vérifier au démarrage » — désactivée par défaut.
    // ------------------------------------------------------------------

    private static string PrefPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "FaultTracePC", "maj.txt");

    /// <summary>Vrai uniquement si l'utilisateur l'a explicitement demandé.</summary>
    public static bool CheckAtStartup
    {
        get { try { return File.Exists(PrefPath) && File.ReadAllText(PrefPath).Trim() == "1"; } catch { return false; } }
        set
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(PrefPath)!);
                File.WriteAllText(PrefPath, value ? "1" : "0");
            }
            catch { /* préférence non critique */ }
        }
    }
}
