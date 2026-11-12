using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using FaultTracePC.Core;

namespace FaultTracePC.App;

/// <summary>
/// POINT 121 (validé par l'auteur le 12/11/2026) — trier une liste de la console de parc
/// en cliquant sur l'en-tête d'une colonne : un clic, ordre croissant ; un second clic,
/// décroissant ; une flèche ▲ ou ▼ dans l'en-tête dit quelle colonne est triée.
///
/// Le tri se fait sur la VUE de la liste (<see cref="ListCollectionView.CustomSort"/>),
/// jamais sur la liste elle-même : le code qui relit les lignes (cases cochées de
/// l'inventaire, sélection) les retrouve telles quelles. Les listes sont reconstruites à
/// chaque actualisation : la fenêtre rappelle <see cref="Appliquer"/> juste après, pour
/// que le tri choisi tienne. Il vaut pour la session : rien n'est enregistré.
///
/// Une colonne se trie sur la propriété qu'elle affiche ; si la ligne a une propriété du
/// même nom suivie de « Tri » (par exemple une vraie date derrière un texte « 12/11
/// 08:27 »), c'est elle qui sert. L'ordre lui-même est décidé par <see cref="TriColonnes"/>
/// (testé). Une colonne sans valeur affichée — les cases à cocher — ne se trie pas.
/// </summary>
internal sealed class TriParEnTete
{
    private const string Haut = " ▲";
    private const string Bas = " ▼";

    private readonly ListView _liste;
    private string? _propriete;
    private bool _decroissant;
    private GridViewColumn? _colonne;
    private object? _enTeteOriginal;

    public TriParEnTete(ListView liste)
    {
        _liste = liste;
        liste.AddHandler(GridViewColumnHeader.ClickEvent, new RoutedEventHandler(EnTete_Click));
    }

    private void EnTete_Click(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is not GridViewColumnHeader { Column: { } colonne } ||
            colonne.DisplayMemberBinding is not Binding { Path.Path: { Length: > 0 } propriete })
            return;

        _decroissant = _propriete == propriete && !_decroissant;
        _propriete = propriete;
        Fleche(colonne);
        Appliquer();
    }

    /// <summary>Remet le tri choisi sur la liste — à rappeler après chaque remplacement de ItemsSource.</summary>
    public void Appliquer()
    {
        if (_propriete is null || _liste.ItemsSource is null) return;
        if (CollectionViewSource.GetDefaultView(_liste.ItemsSource) is ListCollectionView vue)
            vue.CustomSort = new Comparateur(_propriete, _decroissant);
    }

    /// <summary>Met la flèche sur la colonne triée, et rend son en-tête d'origine à la précédente.</summary>
    private void Fleche(GridViewColumn colonne)
    {
        if (_colonne is not null && !ReferenceEquals(_colonne, colonne)) Restaurer();
        if (!ReferenceEquals(_colonne, colonne))
        {
            _colonne = colonne;
            _enTeteOriginal = colonne.Header is TextBlock tb ? tb.Text : colonne.Header;
        }
        var fleche = _decroissant ? Bas : Haut;
        if (colonne.Header is TextBlock bloc) bloc.Text = $"{_enTeteOriginal}{fleche}";
        else colonne.Header = $"{_enTeteOriginal}{fleche}";
    }

    private void Restaurer()
    {
        if (_colonne is null) return;
        if (_colonne.Header is TextBlock bloc) bloc.Text = _enTeteOriginal as string ?? "";
        else _colonne.Header = _enTeteOriginal;
    }

    private sealed class Comparateur(string propriete, bool decroissant) : IComparer
    {
        public int Compare(object? x, object? y) =>
            TriColonnes.Comparer(Valeur(x), Valeur(y), decroissant);

        private object? Valeur(object? ligne)
        {
            if (ligne is null) return null;
            var type = ligne.GetType();
            var p = type.GetProperty(propriete + "Tri") ?? type.GetProperty(propriete);
            return p?.GetValue(ligne);
        }
    }
}
