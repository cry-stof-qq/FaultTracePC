namespace FaultTracePC.Core.Repair;

/// <summary>Ce que sfc /scannow a répondu.</summary>
public enum SfcResult
{
    /// <summary>Compte rendu non interprétable — voir la remarque en tête de <see cref="RepairOutput"/>.</summary>
    Unreadable,
    /// <summary>Aucune violation d'intégrité.</summary>
    NoViolations,
    /// <summary>Des fichiers étaient endommagés, tous ont été réparés.</summary>
    Repaired,
    /// <summary>Des fichiers étaient endommagés, certains n'ont PAS pu être réparés.</summary>
    RepairIncomplete,
    /// <summary>sfc n'a pas pu travailler du tout (service de réparation, opération refusée…).</summary>
    CouldNotRun,
}

/// <summary>État de l'image Windows après DISM /ScanHealth (lecture seule).</summary>
public enum ImageHealth { Unreadable, Healthy, Repairable, NotRepairable }

/// <summary>Issue de DISM /RestoreHealth.</summary>
public enum ImageRepair { Unreadable, Completed, Failed }

/// <summary>Issue de Repair-Volume -Scan (lecture seule).</summary>
public enum VolumeScan { Unreadable, NoErrors, NeedsRepair }

/// <summary>
/// Lecture des comptes rendus des outils de réparation de Windows.
///
/// LE PROBLÈME QUE CE FICHIER TRAITE
/// Les réparations s'exécutent correctement quelle que soit la langue de Windows.
/// C'est leur LECTURE qui échoue : sfc et DISM écrivent leur conclusion en toutes
/// lettres, dans la langue d'affichage. Jusqu'à la 1.2.3, l'assistant guidé
/// cherchait « réparé » ou « repaired » et concluait « sfc : terminé » dans tous
/// les autres cas — donc aussi bien pour « aucun problème » que pour « des
/// fichiers endommagés n'ont PAS pu être réparés », et sur toute machine dont la
/// langue n'est ni le français ni l'anglais. Un faux négatif silencieux, c'est-à-dire
/// exactement ce que ce logiciel existe pour éviter.
///
/// LE PRINCIPE RETENU : ON NE DEVINE PAS LA LANGUE, ON DÉTECTE L'ÉCHEC DE LECTURE.
/// Détecter la langue de Windows pour choisir une liste de phrases ne règle rien :
/// il resterait toutes les langues non prévues, et la conclusion serait fausse
/// sans que personne le sache. Ici, une sortie qui ne correspond à aucun motif
/// connu vaut « Unreadable » — l'assistant le dit et renvoie l'utilisateur vers
/// une exécution manuelle, au lieu d'annoncer une machine saine qu'il n'a pas lue.
/// La justesse ne dépend donc PAS de l'exhaustivité des listes ci-dessous.
///
/// L'ORDRE DES TESTS EST SIGNIFIANT : « n'a pas pu réparer certains » contient
/// le mot « réparer », « cannot be repaired » contient « repaired ». Les échecs se
/// testent AVANT les succès, jamais l'inverse.
///
/// L'APOSTROPHE TYPOGRAPHIQUE — vérifié le 17/08/2026 sur un Windows 11 français
/// (26100), en lisant directement System32\fr-FR\sfc.exe.mui : le MÊME fichier de
/// ressources mélange les deux apostrophes.
///     « n\u2019a trouvé aucune violation d\u2019intégrité »  → U+2019 (typographique)
///     « n\u0027a pas pu réparer certains d\u0027entre eux »   → U+0027 (ASCII)
/// Une comparaison écrite avec l'apostrophe ASCII rate donc la première phrase :
/// une machine française parfaitement saine aurait été déclarée « illisible ».
/// Les deux formes sont ramenées à l'apostrophe ASCII avant toute comparaison, de
/// même que les espaces insécables — que la typographie française sème volontiers
/// devant « : » et « ! ».
/// </summary>
public static class RepairOutput
{
    /// <summary>
    /// Ramène les variantes typographiques à leur forme simple. Ce n'est pas de
    /// la cosmétique : c'est ce qui décide si une phrase est reconnue ou non.
    /// </summary>
    private static string Normalize(string s)
    {
        var sb = new System.Text.StringBuilder(s.Length);
        foreach (var c in s)
            sb.Append(c switch
            {
                '\u2019' or '\u2018' or '\u02BC' or '\u00B4' or '`' => '\'',
                '\u00A0' or '\u202F' or '\u2007' => ' ',
                _ => c,
            });
        return sb.ToString();
    }

    private static bool Has(string haystack, string needle) =>
        haystack.Contains(Normalize(needle), StringComparison.OrdinalIgnoreCase);

    private static bool HasAny(string haystack, params string[] needles) =>
        needles.Any(n => Has(haystack, n));

    // ------------------------------------------------------------------
    // sfc /scannow
    // ------------------------------------------------------------------

    /// <summary>
    /// sfc n'a aucune option pour forcer l'anglais : sa sortie est toujours dans
    /// la langue d'affichage, et en UTF-16 lorsqu'elle est redirigée.
    /// </summary>
    public static SfcResult ReadSfc(string? output)
    {
        // Les phrases françaises ci-dessous sont RELEVÉES dans
        // System32\fr-FR\sfc.exe.mui d'un Windows 11 26100, pas déduites.
        var o = Normalize(output ?? "");
        if (o.Trim().Length == 0) return SfcResult.Unreadable;

        // 1. Impossible de travailler.
        if (HasAny(o, "could not perform the requested operation",
                      "could not start the repair service",
                      "n'a pas réussi à effectuer l'opération",
                      "n'a pas réussi à démarrer le service de réparation"))
            return SfcResult.CouldNotRun;

        // 2. Réparation partielle — testée AVANT la réparation réussie.
        if (HasAny(o, "unable to fix", "unable to repair", "n'a pas pu réparer certains"))
            return SfcResult.RepairIncomplete;

        // 3. Réparation complète.
        if (HasAny(o, "successfully repaired", "et les a réparés"))
            return SfcResult.Repaired;

        // 3 bis. Violations constatées sans réparation : c'est ce qu'écrit
        // « sfc /verifyonly », qui vérifie sans rien toucher. L'assistant ne lance
        // que /scannow, mais un technicien qui colle une sortie de /verifyonly doit
        // obtenir la bonne conclusion, et surtout pas « aucun problème ».
        // Attention au « des » : « n'a pas détecté DE violations » est la phrase
        // inverse, et elle ne doit pas tomber ici.
        if (HasAny(o, "found integrity violations", "a détecté des violations de l'intégrité"))
            return SfcResult.RepairIncomplete;

        // 4. Rien à signaler.
        if (HasAny(o, "did not find any integrity violation", "aucune violation d'intégrité"))
            return SfcResult.NoViolations;

        return SfcResult.Unreadable;
    }

    // ------------------------------------------------------------------
    // DISM /Online /Cleanup-Image /ScanHealth
    // ------------------------------------------------------------------

    public static ImageHealth ReadImageScan(string? output)
    {
        // Français relevé le 17/08/2026 : « Le magasin de composants est réparable. »
        var o = Normalize(output ?? "");
        if (o.Trim().Length == 0) return ImageHealth.Unreadable;

        // Irréparable d'abord : la phrase contient le mot « réparable ».
        if (HasAny(o, "cannot be repaired", "not repairable",
                      "ne peut pas être réparé", "n'est pas réparable"))
            return ImageHealth.NotRepairable;

        if (HasAny(o, "is repairable", "est réparable", "est endommagé", "est endommagée"))
            return ImageHealth.Repairable;

        if (HasAny(o, "no component store corruption",
                      "aucune altération du magasin de composants",
                      "aucune corruption du magasin de composants"))
            return ImageHealth.Healthy;

        return ImageHealth.Unreadable;
    }

    // ------------------------------------------------------------------
    // DISM /Online /Cleanup-Image /RestoreHealth
    // ------------------------------------------------------------------

    public static ImageRepair ReadImageRepair(string? output)
    {
        // Français relevé le 17/08/2026 : « L'opération a réussi. »
        var o = Normalize(output ?? "");
        if (o.Trim().Length == 0) return ImageRepair.Unreadable;

        // DISM annonce ses échecs par un code d'erreur : 0x800f081f (fichiers
        // sources introuvables) est de loin le plus fréquent, sur un poste sans
        // accès à Windows Update ou filtré par un WSUS.
        if (HasAny(o, "error: 0x", "erreur : 0x", "erreur: 0x")) return ImageRepair.Failed;

        if (HasAny(o, "the operation completed successfully",
                      "the restore operation completed successfully",
                      "l'opération a réussi",
                      "l'opération de restauration a réussi",
                      "s'est terminée avec succès"))
            return ImageRepair.Completed;

        if (HasAny(o, "failed", "a échoué", "n'a pas abouti")) return ImageRepair.Failed;

        return ImageRepair.Unreadable;
    }

    /// <summary>
    /// DISM a-t-il refusé l'option globale /English ? Le cas ne devrait pas se
    /// produire — l'option est documentée pour Windows 10 et suivants, et elle a
    /// été VÉRIFIÉE le 17/08/2026 sur un Windows 11 français (26100), qui répond
    /// bien « The component store is repairable. » là où il écrit « Le magasin de
    /// composants est réparable. » sans l'option. Mais un refus se traduirait par
    /// un contrôle non effectué, donc par une réparation silencieusement sautée :
    /// le détecter permet de relancer sans l'option.
    /// </summary>
    public static bool RejectedEnglishOption(string? output)
    {
        var o = Normalize(output ?? "");
        return Has(o, "87") && HasAny(o, "english", "anglais");
    }

    // ------------------------------------------------------------------
    // Repair-Volume -Scan
    // ------------------------------------------------------------------

    /// <summary>
    /// Ici les valeurs viennent d'une énumération de l'API de stockage
    /// (« NoErrorsFound », « NeedsScan »…) et non d'une phrase traduite : ce sont
    /// des identifiants, ils ne bougent pas avec la langue.
    /// </summary>
    public static VolumeScan ReadVolumeScan(string? output)
    {
        var o = Normalize(output ?? "");
        if (o.Trim().Length == 0) return VolumeScan.Unreadable;

        if (HasAny(o, "NeedsScan", "SpotFixNeeded", "FullRepairNeeded")) return VolumeScan.NeedsRepair;
        if (Has(o, "NoErrorsFound")) return VolumeScan.NoErrors;

        return VolumeScan.Unreadable;
    }
}
