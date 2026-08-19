namespace FaultTracePC.Core.Analysis;

/// <summary>Ce qui s'affiche d'emblée, et ce qui se replie derrière un bouton.</summary>
public sealed record FindingSplit(IReadOnlyList<Finding> Visibles, IReadOnlyList<Finding> Repliees);

/// <summary>
/// Combien de conclusions montrer d'un coup.
///
/// LE PROBLÈME
/// Un rapport peut porter huit conclusions, toutes visibles d'emblée. Un
/// technicien lit une liste ; quelqu'un qui découvre son problème ne sait pas par
/// où commencer, et l'important se noie dans le reste.
///
/// LA RÈGLE, ET CE QU'ELLE S'INTERDIT
/// Toute conclusion CRITIQUE reste visible, sans exception : replier un problème
/// grave derrière un bouton serait exactement le défaut que ce logiciel corrige
/// ailleurs — annoncer moins que ce qu'on sait. S'y ajoute le premier
/// avertissement, pour que la page ne s'arrête pas sur du critique seul quand il
/// y a une suite à lire.
///
/// POURQUOI UN SEUIL
/// Replier une unique conclusion cache plus qu'elle n'aide : le lecteur doit
/// cliquer pour découvrir une seule ligne. En dessous de deux éléments à replier,
/// on montre tout.
///
/// RIEN N'EST MASQUÉ EN SILENCE : le bouton annonce le nombre exact et sa
/// répartition, et le rendu à l'impression rouvre tout — un PDF transmis à un
/// réparateur ne doit pas être amputé sans que son destinataire le sache.
/// </summary>
public static class FindingDisplay
{
    /// <summary>En dessous de ce nombre d'éléments à replier, on n'en replie aucun.</summary>
    public const int SeuilRepliement = 2;

    public static FindingSplit Split(IReadOnlyList<Finding> findings)
    {
        var visibles = new List<Finding>();
        var reste = new List<Finding>();
        var premierAvertissementPris = false;

        foreach (var f in findings)
        {
            if (f.Severity == Severity.Critical) { visibles.Add(f); continue; }

            if (f.Severity == Severity.Warning && !premierAvertissementPris)
            {
                premierAvertissementPris = true;
                visibles.Add(f);
                continue;
            }

            reste.Add(f);
        }

        // Sous le seuil, on rend la liste d'origine telle quelle : cela garantit
        // l'ordre exact, sans dépendre du fait que les conclusions soient triées.
        return reste.Count < SeuilRepliement
            ? new FindingSplit(findings, [])
            : new FindingSplit(visibles, reste);
    }

    /// <summary>
    /// Libellé du bouton. Il annonce le nombre et la répartition : « voir la
    /// suite » laisserait croire à un détail, et masquer sans dire combien est ce
    /// que le logiciel reproche aux autres.
    /// </summary>
    public static string FoldLabel(IReadOnlyList<Finding> repliees)
    {
        var avert = repliees.Count(f => f.Severity == Severity.Warning);
        var infos = repliees.Count - avert;

        var detail = (avert, infos) switch
        {
            (0, _) => Lang.T($"{infos} information(s)", $"{infos} information item(s)"),
            (_, 0) => Lang.T($"{avert} avertissement(s)", $"{avert} warning(s)"),
            _ => Lang.T($"{avert} avertissement(s), {infos} information(s)", $"{avert} warning(s), {infos} information item(s)"),
        };

        return Lang.T($"Voir les {repliees.Count} autres conclusions ({detail})",
                      $"Show the {repliees.Count} other findings ({detail})");
    }
}
