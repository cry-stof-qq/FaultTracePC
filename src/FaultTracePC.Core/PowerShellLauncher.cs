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
        ArgumentsForScript(scriptPath, pause, null);

    /// <summary>
    /// La même chose, avec des PARAMÈTRES passés au script — ce dont le
    /// déploiement de parc a besoin (point 64, lot B).
    ///
    /// Une valeur <c>null</c> désigne un COMMUTATEUR : <c>-VerifierSeulement</c>
    /// s'écrit sans rien derrière. Toute autre valeur devient un littéral
    /// PowerShell à guillemets simples, donc insensible aux espaces comme aux
    /// apostrophes d'un chemin.
    ///
    /// LE NOM DU PARAMÈTRE EST CONTRÔLÉ, PAS SEULEMENT SA VALEUR. Un nom est écrit
    /// tel quel dans la ligne de commande : n'y accepter que des lettres ferme la
    /// seule porte par laquelle du texte arbitraire pourrait s'y glisser. Le code
    /// appelant ne devrait jamais en fabriquer d'autre — et s'il le fait, mieux
    /// vaut une exception qu'une commande inattendue.
    /// </summary>
    /// <param name="pauseTousLesCas">
    /// <c>true</c> : la fenetre attend une touche MEME QUAND TOUT S'EST BIEN PASSE.
    ///
    /// La pause conditionnelle convient a une reparation, dont on veut seulement
    /// savoir si elle a echoue. Elle ne convient pas a une VERIFICATION, dont le
    /// deroule est justement le resultat : le 19/09/2026, la fenetre s'est fermee
    /// avant que l'on ait pu lire quoi que ce soit, alors que tout avait reussi.
    /// </param>
    public static string ArgumentsForScript(
        string scriptPath, string pause, IEnumerable<KeyValuePair<string, string?>>? parametres,
        bool pauseTousLesCas = false)
    {
        var appel = new System.Text.StringBuilder("& " + Litteral(scriptPath));

        foreach (var p in parametres ?? [])
        {
            if (p.Key is null || p.Key.Length == 0 || !p.Key.All(char.IsAsciiLetter))
                throw new ArgumentException(
                    Lang.T($"Nom de paramètre PowerShell inattendu : « {p.Key} »",
                           $"Unexpected PowerShell parameter name: “{p.Key}”"),
                    nameof(parametres));

            appel.Append(" -").Append(p.Key);
            if (p.Value is not null) appel.Append(' ').Append(Litteral(p.Value));
        }

        // ATTENTION AUX ACCOLADES ORPHELINES.
        //
        // En PowerShell, « { ... } » sans rien devant n'est PAS un bloc de code qui
        // s'exécute : c'est un objet qui représente du code, créé puis jeté. Écrire
        // « finally { { Read-Host } } » ne demande donc RIEN, et la fenêtre se
        // referme aussitôt. Constaté le 19/09/2026, en retirant le « if » qui
        // précédait ces accolades sans retirer les accolades elles-mêmes.
        //
        // Les deux formes sont donc écrites en entier, plutôt que composées par
        // morceaux : c'est la composition par morceaux qui a produit la faute.
        var pause_ = pauseTousLesCas
            ? "Read-Host " + Litteral(pause)
            : "if (-not $fini) { Read-Host " + Litteral(pause) + " }";

        return Prefixe + "& { $fini = $false; try { " + appel
            + "; $fini = $true } catch { Write-Host $_ } finally { " + pause_ + " } }\"";
    }

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
