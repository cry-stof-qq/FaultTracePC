namespace FaultTracePC.Core.Analysis;

/// <summary>
/// POINT 87 — nommer le réglage des fichiers de plantage tel qu'il a été LU.
///
/// Constaté le 24/09/2026 : le rapport recommandait « d'activer les petits vidages »
/// sans avoir lu le réglage. Celui-ci était « Vidage mémoire automatique », la
/// configuration d'origine de Windows — et des petits vidages étaient d'ailleurs
/// présents sur le disque.
///
/// Valeurs : documentation Microsoft « Overview of memory dump file options for
/// Windows » (learn.microsoft.com). Les noms ci-dessous décrivent le réglage ; seul
/// « vidage mémoire automatique » reprend le libellé tel qu'il a été vu à l'écran,
/// sur la machine du 24/09/2026.
/// </summary>
public static class ReglageVidage
{
    /// <summary>Valeur d'origine de Windows : vidage mémoire automatique.</summary>
    public const int Automatique = 7;

    public static string Nom(OsInfo os) => os.CrashDumpEnabled switch
    {
        null => Lang.T("non lu", "not read"),
        0 => Lang.T("aucun — Windows n'écrit aucun fichier de plantage", "none — Windows writes no crash file"),
        1 when os.FilterPages => Lang.T("vidage mémoire actif", "active memory dump"),
        1 => Lang.T("vidage mémoire complet", "complete memory dump"),
        2 => Lang.T("vidage mémoire du noyau", "kernel memory dump"),
        3 => Lang.T("petit vidage mémoire", "small memory dump"),
        Automatique => Lang.T("vidage mémoire automatique (réglage d'origine de Windows)", "automatic memory dump (Windows default)"),
        var v => Lang.T($"valeur {v} non répertoriée", $"unlisted value {v}"),
    };

    public static string FichierEchange(OsInfo os) => os.FichierEchangeGereParWindows switch
    {
        true => Lang.T("géré par Windows", "managed by Windows"),
        false => Lang.T("réglé à la main", "set manually"),
        null => Lang.T("gestion non lue", "management not read"),
    };
}
