using System.Text.Json;

namespace FaultTracePC.Core;

/// <summary>
/// Niveau de conclusion d'un scan, aligné sur les codes de sortie de la ligne de
/// commande (0 sain, 1 avertissements, 2 critique). Une seule règle, partagée par
/// le code de sortie et par le protocole de parc : sans cela, un poste pourrait
/// répondre « critique » à la console et rendre 0 à la tâche planifiée qui l'a lancé.
/// </summary>
public enum ScanLevel { Healthy, Warnings, Critical }

public static class ScanLevelInfo
{
    public static ScanLevel Of(DiagnosticReport r)
    {
        if (r.Findings.Any(f => f.Severity == Severity.Critical)) return ScanLevel.Critical;
        if (r.Findings.Any(f => f.Severity == Severity.Warning)) return ScanLevel.Warnings;
        return ScanLevel.Healthy;
    }

    /// <summary>Code transmis sur le réseau. Court, stable, jamais traduit.</summary>
    public static string Code(this ScanLevel l) => l switch
    {
        ScanLevel.Critical => "crit",
        ScanLevel.Warnings => "warn",
        _ => "ok",
    };

    public static ScanLevel? ParseCode(string? code) => (code ?? "").Trim().ToLowerInvariant() switch
    {
        "crit" => ScanLevel.Critical,
        "warn" => ScanLevel.Warnings,
        "ok" => ScanLevel.Healthy,
        _ => null,
    };

    /// <summary>0 sain · 1 avertissements · 2 critique (3 = erreur d'exécution, hors de cette échelle).</summary>
    public static int ExitCode(this ScanLevel l) => l switch
    {
        ScanLevel.Critical => 2,
        ScanLevel.Warnings => 1,
        _ => 0,
    };

    /// <summary>Phrase construite CHEZ CELUI QUI LIT, donc dans SA langue.</summary>
    public static string Sentence(this ScanLevel l, int critical, int warnings) => l switch
    {
        ScanLevel.Critical => Lang.T($"{critical} problème(s) critique(s), {warnings} avertissement(s).",
                                     $"{critical} critical problem(s), {warnings} warning(s)."),
        ScanLevel.Warnings => Lang.T($"aucun problème critique, {warnings} avertissement(s).",
                                     $"no critical problem, {warnings} warning(s)."),
        _ => Lang.T("aucun problème significatif.", "no significant problem."),
    };
}

/// <summary>
/// Réponse d'un poste à un diagnostic déclenché à distance.
/// <see cref="Level"/> vaut null quand le poste est trop ancien pour l'envoyer.
/// </summary>
public sealed class RemoteScanResult
{
    public bool Ok { get; init; }
    public string ReportName { get; init; } = "";
    public ScanLevel? Level { get; init; }
    public int Critical { get; init; }
    public int Warnings { get; init; }

    /// <summary>Phrase de verdict telle que le POSTE l'a écrite — donc dans SA langue.</summary>
    public string RemoteSentence { get; init; } = "";

    /// <summary>Message d'échec renvoyé par le poste, ou null.</summary>
    public string? Error { get; init; }
}

/// <summary>
/// Lecture des réponses du protocole de parc.
///
/// POURQUOI UN CODE PLUTÔT QU'UNE PHRASE
/// L'administrateur d'un parc ne lit pas forcément la même langue que les postes
/// qu'il administre : un poste peut très bien tourner en anglais pendant que la
/// console est en français, et l'inverse arrivera dès qu'un parc mixte existera.
/// Le poste transmet donc « crit » et deux compteurs ; la phrase est écrite par
/// la console, dans la langue de celui qui la lit.
///
/// LES DEUX FORMATS SONT ACCEPTÉS
/// Un poste resté en 1.2.3 n'envoie pas de code : sa phrase est alors affichée
/// telle quelle. C'est la seule chose honnête à faire — la retraduire serait
/// inventer, et l'effacer priverait l'administrateur du seul résultat disponible.
/// Un parc ne se met pas à jour en un jour, et une console qui refuse de parler
/// aux postes d'hier est inutilisable le jour du déploiement.
/// </summary>
public static class ParkProtocol
{
    public static RemoteScanResult ReadScanResponse(string? json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json ?? "");
            var root = doc.RootElement;

            bool ok = root.TryGetProperty("ok", out var o) && o.ValueKind == JsonValueKind.True;
            if (!ok)
                return new RemoteScanResult { Ok = false, Error = Str(root, "error") };

            return new RemoteScanResult
            {
                Ok = true,
                ReportName = Str(root, "report") ?? "",
                Level = ScanLevelInfo.ParseCode(Str(root, "level")),
                Critical = Int(root, "critical"),
                Warnings = Int(root, "warnings"),
                RemoteSentence = Str(root, "verdict") ?? "",
            };
        }
        catch (JsonException)
        {
            // Réponse illisible : on le dit, on n'invente pas un résultat.
            return new RemoteScanResult { Ok = false, Error = null };
        }
    }

    private static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static int Int(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var n) ? n : 0;
}
