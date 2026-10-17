namespace FaultTracePC.Core;

/// <summary>
/// Colonne « Version » de la Supervision : la version d'un poste comparée à celle
/// de la console.
///
/// Volontairement symétrique : c'est parfois la CONSOLE qui est en retard, et une
/// colonne qui ne saurait dire que « le poste est vieux » ferait mettre à jour la
/// mauvaise machine.
///
/// Point 100 (1.9.0) : « ⬆ 1.8.0 — à mettre à jour » ne tenait pas dans la
/// colonne (« … à mettre à jou »), constaté le 12/10/2026. La forme courte
/// « ⬆ 1.8.0 → 1.8.1 » dit la même chose — et dit en plus VERS QUOI — en moitié
/// moins de place ; le sens des flèches est expliqué dans la bulle d'aide de
/// l'en-tête. Sorti de ParkWindow vers Core pour être testé.
/// </summary>
public static class VersionDuPoste
{
    public static string Decrire(bool joignable, string? versionPoste, string versionConsole)
    {
        if (!joignable) return "";
        if (string.IsNullOrEmpty(versionPoste)) return Lang.T("⬆ antérieure à 1.2.2", "⬆ older than 1.2.2");

        if (!Version.TryParse(versionPoste, out var poste) || !Version.TryParse(versionConsole, out var console))
            return versionPoste;

        var cmp = poste.CompareTo(console);
        return cmp < 0 ? $"⬆ {versionPoste} → {versionConsole}"
             : cmp > 0 ? $"⚠ {versionPoste}, console {versionConsole}"   // identique dans les deux langues
             : versionPoste;
    }
}
