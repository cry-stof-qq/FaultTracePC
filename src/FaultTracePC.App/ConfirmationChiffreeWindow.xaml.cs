using System.Windows;
using FaultTracePC.Core;

namespace FaultTracePC.App;

/// <summary>
/// Confirmation d'une action qui modifie plusieurs machines : il faut TAPER LE
/// NOMBRE de postes concernés pour que le bouton devienne actif.
///
/// POURQUOI UNE SAISIE PLUTÔT QU'UN « ÊTES-VOUS SÛR ? »
/// Un « Oui / Non » se clique par réflexe — c'est même le geste qu'on fait le plus
/// vite quand on est pressé, donc exactement au mauvais moment. Recopier un nombre
/// oblige à le LIRE, et lire « 47 » quand on croyait en sélectionner trois est la
/// seule chose qui arrête la main à temps.
///
/// Le bouton d'annulation est le bouton PAR DÉFAUT : la touche Entrée annule, elle
/// ne valide pas.
/// </summary>
public partial class ConfirmationChiffreeWindow : Window
{
    private readonly int _attendu;

    private ConfirmationChiffreeWindow(Window proprietaire, int nombre,
                                       string question, string detail, string consequences)
    {
        InitializeComponent();

        Owner = proprietaire;
        _attendu = nombre;

        TxtQuestion.Text = question;
        TxtDetail.Text = detail;
        TxtConsequences.Text = consequences;
        TxtInvite.Text = Lang.T($"Taper {nombre} pour confirmer :", $"Type {nombre} to confirm:");
        TxtAide.Text = Lang.T("(recopier le nombre oblige à le lire)", "(retyping the number forces you to read it)");
    }

    /// <summary>
    /// Rend <c>true</c> seulement si l'utilisateur a recopié le nombre exact ET
    /// cliqué sur le bouton de validation.
    /// </summary>
    public static bool Demander(Window proprietaire, int nombre,
                                string question, string detail, string consequences)
    {
        // Un nombre nul ou négatif n'a rien à faire ici : l'appelant a un défaut, et
        // afficher « taper 0 pour confirmer » serait absurde autant que dangereux.
        if (nombre <= 0) return false;

        var fenetre = new ConfirmationChiffreeWindow(proprietaire, nombre, question, detail, consequences);
        return fenetre.ShowDialog() == true;
    }

    private void Fenetre_Loaded(object sender, RoutedEventArgs e) => TxtSaisie.Focus();

    private void TxtSaisie_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        // Comparaison sur le NOMBRE, pas sur le texte : « 047 » et « 47 » sont le
        // même nombre, et refuser le premier n'aiderait personne.
        BtnValider.IsEnabled = int.TryParse((TxtSaisie.Text ?? "").Trim(), out var saisi)
                               && saisi == _attendu;
    }

    private void BtnValider_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}
