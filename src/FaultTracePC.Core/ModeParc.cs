using Microsoft.Win32;

namespace FaultTracePC.Core;

/// <summary>
/// Point 79 (1.9.0) : le mode parc devient une case à cocher de l'installation.
///
/// CE QUE LA CASE GOUVERNE — décision D1 du 17/10/2026 : seulement l'interface
/// d'administration, les boutons « Parc » (console) et « Réseau » de la fenêtre
/// principale. Le service de surveillance, lui, continue de n'obéir qu'à
/// remote.json. Un poste client déjà supervisé ne peut donc pas sortir de la
/// supervision à cause de cette case, quelle que soit la façon dont il a été mis
/// à jour.
///
/// COMMENT ON LE SAIT — deux valeurs de registre, posées par l'installateur
/// (lot 2), sous la clé que le MSI utilise déjà (« Installed ») :
///  · <see cref="ValeurGeree"/> : posée par la fonctionnalité principale d'un
///    installateur qui CONNAÎT la case. Absente = version portable (pas
///    d'installateur), ou installateur antérieur à la 1.9.0 : on montre tout,
///    comme avant (décision D4, et aucune régression pendant la transition).
///  · <see cref="ValeurCase"/> : posée par la fonctionnalité « Mode parc »
///    elle-même. Présente seulement si la case est cochée.
///
/// Pourquoi pas la valeur « Installed » existante : elle est posée depuis
/// longtemps par tous les MSI. S'en servir pour dire « installé sans la case »
/// aurait caché la console à tous les postes en 1.8.x dès cette version.
/// </summary>
public static class ModeParc
{
    public const string CleRegistre = @"Software\Cry-stof\FaultTracePC";
    public const string ValeurGeree = "ParkFeatureManaged";
    public const string ValeurCase = "ParkFeature";

    /// <summary>
    /// La console est visible si l'installateur ne gère pas la case (portable,
    /// ou version antérieure), ou s'il la gère et qu'elle a été cochée.
    /// </summary>
    public static bool ConsoleVisible(bool caseGeree, bool caseCochee) => !caseGeree || caseCochee;

    /// <summary>
    /// Décision D3 : « Réseau » reste visible sur un poste exposé en mode Client,
    /// même sans la case — qui s'assoit devant doit pouvoir voir qu'il est exposé.
    /// </summary>
    public static bool ReseauVisible(bool consoleVisible, bool posteEnModeClient) =>
        consoleVisible || posteEnModeClient;

    /// <summary>
    /// Lit les deux valeurs dans la vue 64 bits du registre (le MSI est x64).
    /// Registre illisible : on se comporte comme une version portable — mieux
    /// vaut montrer une console qu'en priver un administrateur sans raison.
    /// </summary>
    public static (bool CaseGeree, bool CaseCochee) LireInstallation()
    {
        try
        {
            using var racine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            using var cle = racine.OpenSubKey(CleRegistre);
            if (cle is null) return (false, false);
            return (ValeurVraie(cle.GetValue(ValeurGeree)), ValeurVraie(cle.GetValue(ValeurCase)));
        }
        catch { return (false, false); }
    }

    /// <summary>Une valeur DWORD 1 (ce que pose WiX avec Type="integer"), ou le texte « 1 ».</summary>
    internal static bool ValeurVraie(object? valeur) => valeur switch
    {
        int i => i == 1,
        string s => s.Trim() == "1",
        _ => false,
    };
}
