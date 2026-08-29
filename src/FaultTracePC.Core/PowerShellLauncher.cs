namespace FaultTracePC.Core;

/// <summary>
/// Construit la ligne d'arguments d'une fenêtre PowerShell qui se ferme
/// vraiment — et qui reste ouverte quand il y a quelque chose à lire.
///
/// LE DÉFAUT CORRIGÉ
/// La 1.3.1 avait ajouté <c>-NoExit</c> pour qu'une console ne s'évapore plus
/// lorsqu'une stratégie de groupe refuse le script avant sa première ligne. Effet
/// non voulu : la fenêtre ne se referme alors PLUS JAMAIS d'elle-même, alors que
/// le script se termine par « Appuyer sur Entrée pour fermer ». Le logiciel
/// écrivait une phrase fausse — exactement la classe de défaut qu'il corrige
/// ailleurs.
///
/// L'ENROBAGE, ET POURQUOI C'EST LA COMBINAISON QUI COMPTE
///   · <c>-Command</c> en ligne n'est PAS soumis à la stratégie d'exécution :
///     l'enrobage démarre donc toujours, même quand le .ps1, lui, est refusé ;
///   · le <c>finally</c> garantit la pause dans TOUS les cas — refus, plantage,
///     erreur de syntaxe, interruption par un antivirus. C'est ce que
///     <c>-NoExit</c> apportait, sans son défaut ;
///   · plus de <c>-NoExit</c>, donc Entrée ferme réellement, et la phrase
///     redevient vraie.
///
/// LA STRATÉGIE N'EST PAS CONTOURNÉE : elle refuse toujours le fichier .ps1. On
/// affiche son refus au lieu de le laisser passer en un clin d'œil.
/// </summary>
public static class PowerShellLauncher
{
    private const string Prefixe = "-NoProfile -ExecutionPolicy Bypass -Command \"";

    /// <summary>
    /// Exécution d'un FICHIER de script. Le chemin est cité comme un littéral
    /// PowerShell : un dossier utilisateur nommé « O'Brien » suffirait sinon à
    /// couper la chaîne — c'est le défaut qui a coûté la 1.4.1, dans l'autre sens.
    /// </summary>
    /// <remarks>
    /// LA PAUSE EST CONDITIONNELLE, et ce n'est pas un détail : le script engendré
    /// se termine DÉJÀ par sa propre invite « Appuyer sur Entrée pour fermer ».
    /// Une pause inconditionnelle ici obligerait donc à appuyer deux fois. Le
    /// drapeau <c>$fini</c> n'est posé que si l'exécution est allée jusqu'au bout :
    /// la fenêtre ne retient l'utilisateur que lorsqu'il y a quelque chose à lire.
    /// </remarks>
    public static string ArgumentsForScript(string scriptPath, string pause) =>
        Prefixe + "& { $fini = $false; try { & " + Litteral(scriptPath)
        + "; $fini = $true } catch { Write-Host $_ } finally { if (-not $fini) { Read-Host "
        + Litteral(pause) + " } } }\"";

    /// <summary>
    /// Exécution d'une COMMANDE écrite par le logiciel (boîte à outils, assistant
    /// guidé). Ses guillemets doubles sont échappés pour l'hôte, comme avant.
    /// </summary>
    public static string ArgumentsForCommand(string command, string pause) =>
        Prefixe + "& { try { " + (command ?? "").Replace("\"", "\\\"") + " } catch { Write-Host $_ } finally { Read-Host "
        + Litteral(pause) + " } }\"";

    /// <summary>
    /// Littéral PowerShell à guillemets simples, sûr même si le texte contient
    /// une apostrophe — droite ou typographique.
    /// </summary>
    internal static string Litteral(string texte) =>
        "'" + Report.RepairScriptGenerator.PsEscape(texte) + "'";
}
