namespace FaultTracePC.Core.Analysis;

/// <summary>
/// Le mode de fonctionnement de Windows Defender (propriété AMRunningMode de
/// MSFT_MpComputerStatus), dit dans la langue du rapport.
///
/// Constaté le 17/10/2026 dans le bac à sable Windows : un rapport FRANÇAIS
/// affichait « (mode Not running) » — la valeur brute, en anglais, telle que
/// Windows la renvoie. Les valeurs connues sont traduites ; une valeur inconnue
/// est rendue telle quelle plutôt que devinée.
/// </summary>
public static class ModeDefender
{
    public static string Nom(string brut) => (brut ?? "").Trim().ToLowerInvariant() switch
    {
        "normal" => Lang.T("normal", "normal"),
        "passive" or "passive mode" => Lang.T("passif", "passive"),
        "sxs passive mode" => Lang.T("passif, à côté d'un autre antivirus", "passive, alongside another antivirus"),
        "edr block mode" => Lang.T("blocage EDR", "EDR block"),
        "not running" => Lang.T("arrêté", "not running"),
        _ => (brut ?? "").Trim(),
    };

    /// <summary>« (mode arrêté) » / « (mode: not running) », ou rien si le mode n'a pas été lu.</summary>
    public static string EntreParentheses(string brut) =>
        string.IsNullOrWhiteSpace(brut) ? "" : Lang.T($" (mode {Nom(brut)})", $" (mode: {Nom(brut)})");
}
