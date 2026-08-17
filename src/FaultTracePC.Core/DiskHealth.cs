using System.Text.Json;
using System.Text.Json.Serialization;

namespace FaultTracePC.Core;

/// <summary>
/// État de santé qu'un disque déclare lui-même, tel que Windows l'expose dans
/// MSFT_PhysicalDisk.HealthStatus (0 Healthy, 1 Warning, 2 Unhealthy, 5 Unknown).
///
/// POURQUOI UNE ÉNUMÉRATION ET PLUS UNE CHAÎNE
/// Jusqu'à la 1.2.3, cet état était rangé sous forme de texte français
/// (« Sain », « Avertissement », « Défaillant ») et TROIS endroits décidaient en
/// comparant ce texte. Un état qui sert à décider ne doit pas être la même chose
/// que le mot affiché à l'écran : traduire l'interface aurait rendu ces
/// comparaisons fausses sans qu'aucun test ne le voie, et un disque défaillant
/// serait passé pour sain sur une machine en anglais.
///
/// L'ORDRE DES VALEURS N'EXPRIME PAS UNE GRAVITÉ : « pas lu » et « inconnu » ne
/// sont ni meilleurs ni pires que « sain ». La comparaison passe par
/// <see cref="DiskHealthInfo.Rank"/>, qui refuse justement de classer ces deux-là.
/// </summary>
[JsonConverter(typeof(DiskHealthJsonConverter))]
public enum DiskHealth
{
    /// <summary>
    /// Aucune mesure : le disque n'a pas été trouvé dans MSFT_PhysicalDisk, ou
    /// l'espace de noms Storage n'a pas répondu. À ne JAMAIS afficher comme
    /// « sain » — c'est la règle de fond du logiciel : une mesure absente n'est
    /// pas un bon résultat.
    /// </summary>
    NotReported = 0,

    /// <summary>Windows a répondu « Unknown » (5), ou une valeur non documentée.</summary>
    Unknown,

    Healthy,
    Warning,
    Failing,
}

public static class DiskHealthInfo
{
    /// <summary>
    /// Traduit le code numérique de MSFT_PhysicalDisk.
    /// Source : documentation MSFT_PhysicalDisk (Microsoft Learn) —
    /// 0 Healthy, 1 Warning, 2 Unhealthy, 5 Unknown.
    /// </summary>
    public static DiskHealth FromWmi(ushort code) => code switch
    {
        0 => DiskHealth.Healthy,
        1 => DiskHealth.Warning,
        2 => DiskHealth.Failing,
        _ => DiskHealth.Unknown,
    };

    /// <summary>
    /// Lecture TOLÉRANTE, pour les données déjà écrites sur les postes.
    ///
    /// L'historique est conservé 90 jours : pendant trois mois, un scan lira des
    /// résumés produits par une version antérieure, où l'état était le mot
    /// français affiché à l'époque. Une mise à jour par GPO remplace le logiciel,
    /// jamais les fichiers qu'il a déjà écrits — refuser ces valeurs ferait
    /// silencieusement disparaître toute comparaison d'un disque pendant trois mois.
    /// </summary>
    public static DiskHealth Parse(string? raw) => (raw ?? "").Trim().ToLowerInvariant() switch
    {
        "" => DiskHealth.NotReported,
        "notreported" => DiskHealth.NotReported,
        "sain" or "healthy" or "ok" => DiskHealth.Healthy,
        // pas-de-traduction : valeurs écrites par la 1.2.x, on les relit telles quelles.
        "avertissement" or "warning" or "degraded" or "dégradé" or "degrade" => DiskHealth.Warning,
        // pas-de-traduction : idem.
        "défaillant" or "defaillant" or "failing" or "failed" or "unhealthy" => DiskHealth.Failing,
        _ => DiskHealth.Unknown,
    };

    /// <summary>Libellé affichable, dans la langue en cours.</summary>
    public static string Label(this DiskHealth h) => h switch
    {
        DiskHealth.Healthy => Lang.T("Sain", "Healthy"),
        DiskHealth.Warning => Lang.T("Avertissement", "Warning"),
        DiskHealth.Failing => Lang.T("Défaillant", "Failing"),
        DiskHealth.Unknown => Lang.T("Inconnu", "Unknown"),
        _ => Lang.T("non mesuré", "not measured"),
    };

    /// <summary>
    /// Gravité comparable : 0 sain, 1 avertissement, 2 défaillant, et -1 quand
    /// l'état ne permet PAS de conclure. Un appelant qui compare deux rangs doit
    /// donc écarter les -1 : passer de « inconnu » à « sain » n'est pas une
    /// amélioration, c'est simplement une mesure qui a fini par aboutir.
    /// </summary>
    public static int Rank(this DiskHealth h) => h switch
    {
        DiskHealth.Healthy => 0,
        DiskHealth.Warning => 1,
        DiskHealth.Failing => 2,
        _ => -1,
    };

    /// <summary>Vrai si le disque signale lui-même un problème.</summary>
    public static bool IsDegraded(this DiskHealth h) => h is DiskHealth.Warning or DiskHealth.Failing;

    /// <summary>Vrai si une mesure a réellement eu lieu et a donné un état interprétable.</summary>
    public static bool IsMeasured(this DiskHealth h) => h is not (DiskHealth.NotReported or DiskHealth.Unknown);
}

/// <summary>
/// Écrit le nom de la valeur (« Warning ») et relit aussi bien ce nom que les
/// anciens libellés français. Le convertisseur est posé sur l'énumération
/// elle-même : tout ce qui sérialise un <see cref="DiskHealth"/> — historique
/// local, protocole de parc — hérite de la tolérance sans avoir à y penser.
/// </summary>
public sealed class DiskHealthJsonConverter : JsonConverter<DiskHealth>
{
    public override DiskHealth Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => reader.TokenType switch
        {
            JsonTokenType.String => DiskHealthInfo.Parse(reader.GetString()),
            // Aucun fichier existant ne contient de nombre ici : l'état a toujours
            // été écrit en toutes lettres. Un nombre ne peut donc venir que d'une
            // version future qui aurait sérialisé l'énumération telle quelle — et
            // surtout PAS du code WMI, dont le 0 signifie « sain » là où le nôtre
            // signifie « pas mesuré ».
            JsonTokenType.Number => reader.TryGetInt32(out var n) && Enum.IsDefined(typeof(DiskHealth), n)
                ? (DiskHealth)n
                : DiskHealth.Unknown,
            _ => DiskHealth.NotReported,
        };

    public override void Write(Utf8JsonWriter writer, DiskHealth value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString());
}
