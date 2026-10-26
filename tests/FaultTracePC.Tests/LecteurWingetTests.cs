using FaultTracePC.Core.Collectors;
using Xunit;

namespace FaultTracePC.Tests;

/// <summary>
/// Point 15, lot 1a de la 1.10.0 : lire le tableau de « winget upgrade ».
///
/// D'OÙ VIENNENT CES SORTIES
/// Elles sont CONSTRUITES selon la structure mesurée à l'écran le 26/10/2026 sur le
/// poste de l'auteur (winget v1.29.380) : en-tête traduit, ligne de tirets continue,
/// valeurs alignées à gauche, phrase de compte sous le tableau. Seule la ligne
/// Notepad++ (8.8.5 → 8.9.8.1) reprend des valeurs réellement mesurées ; les autres
/// logiciels sont des exemples. Les captures BRUTES (sortie redirigée vers un
/// fichier, comme FaultTracePC la recevra) sont demandées à l'auteur et rejoindront
/// ces tests à l'étape suivante.
/// </summary>
public class LecteurWingetTests
{
    // Positions des colonnes : Nom 0, ID 20, Version 42, Disponible 51, Source 62.
    private static readonly int[] Positions = { 0, 20, 42, 51, 62 };

    /// <summary>Place chaque valeur au début de sa colonne, comme winget.</summary>
    private static string Ligne(params string[] cellules)
    {
        var s = "";
        for (int c = 0; c < cellules.Length; c++)
        {
            if (c > 0) s = s.PadRight(Positions[c]);
            s += cellules[c];
        }
        return s;
    }

    private static readonly string Tirets = new('-', 70);

    private static string Sortie(params string[] lignes) => string.Join("\r\n", lignes) + "\r\n";

    private static readonly string EnTeteFrancais = Ligne("Nom", "ID", "Version", "Disponible", "Source");
    private static readonly string LigneNotepad = Ligne("Notepad++", "Notepad++.Notepad++", "8.8.5", "8.9.8.1", "winget");
    private static readonly string LigneExemple = Ligne("Logiciel Exemple", "Editeur.Exemple", "1.0", "2.0", "winget");

    [Fact]
    public void Un_tableau_francais_est_lu_ligne_par_ligne()
    {
        var r = LecteurWinget.Lire(Sortie(EnTeteFrancais, Tirets, LigneNotepad, LigneExemple,
            "2 mises à jour disponibles."));

        Assert.Equal(EtatLectureWinget.Lu, r.Etat);
        Assert.Equal(2, r.MisesAJour.Count);
        var n = r.MisesAJour[0];
        Assert.Equal("Notepad++", n.Nom);
        Assert.Equal("Notepad++.Notepad++", n.Identifiant);
        Assert.Equal("8.8.5", n.VersionInstallee);
        Assert.Equal("8.9.8.1", n.VersionDisponible);
        Assert.Equal("winget", n.Source);
        Assert.Equal(0, n.Tableau);
        Assert.False(n.NomCoupe);
        Assert.False(n.VersionCoupee);
        Assert.Equal("Logiciel Exemple", r.MisesAJour[1].Nom);
        Assert.Equal(0, r.LignesIllisibles);
    }

    [Fact]
    public void Le_nombre_annonce_sous_le_tableau_est_rendu_pour_reconcilier_les_comptes()
    {
        var r = LecteurWinget.Lire(Sortie(EnTeteFrancais, Tirets, LigneNotepad, LigneExemple,
            "2 mises à jour disponibles.",
            "1 package(s) a(ont) des numéros de version qui ne peuvent pas être déterminés. Utilisez --include-unknown pour voir tous les résultats."));

        Assert.Equal(2, r.NombreAnnonce);
        Assert.Equal(2, r.MisesAJour.Count);
    }

    [Fact]
    public void Un_en_tete_anglais_est_lu_de_la_meme_facon()
    {
        var r = LecteurWinget.Lire(Sortie(Ligne("Name", "Id", "Version", "Available", "Source"), Tirets,
            LigneNotepad, "1 upgrades available."));

        Assert.Equal(EtatLectureWinget.Lu, r.Etat);
        Assert.Equal("Notepad++.Notepad++", Assert.Single(r.MisesAJour).Identifiant);
        Assert.Equal(1, r.NombreAnnonce);
    }

    [Fact]
    public void Une_valeur_qui_remplit_sa_colonne_ne_laisse_qu_un_espace_et_reste_lue()
    {
        // Le cas du point 24 : « deux espaces ou plus » aurait collé le nom et l'ID.
        // Nom de 19 caractères, ID en colonne 20 : un seul espace entre les deux.
        var nomPlein = "Logiciel Exemple 19";
        Assert.Equal(19, nomPlein.Length);
        var ligne = Ligne(nomPlein, "Editeur.NomLong", "10.2.3", "10.4.0", "winget");
        Assert.Contains("19 Editeur.NomLong", ligne);

        var r = LecteurWinget.Lire(Sortie(EnTeteFrancais, Tirets, ligne));

        var m = Assert.Single(r.MisesAJour);
        Assert.Equal(nomPlein, m.Nom);
        Assert.Equal("Editeur.NomLong", m.Identifiant);
    }

    [Fact]
    public void Un_nom_coupe_par_winget_est_garde_et_signale()
    {
        var r = LecteurWinget.Lire(Sortie(EnTeteFrancais, Tirets,
            Ligne("Microsoft Visual C…", "Microsoft.VCRedist", "14.4", "14.5", "winget")));

        var m = Assert.Single(r.MisesAJour);
        Assert.True(m.NomCoupe);
        Assert.Equal("Microsoft.VCRedist", m.Identifiant);
    }

    [Fact]
    public void Un_identifiant_coupe_n_est_jamais_devine()
    {
        var r = LecteurWinget.Lire(Sortie(EnTeteFrancais, Tirets,
            Ligne("Outil Exemple", "Editeur.OutilExemp…", "3.1", "3.2", "winget")));

        var m = Assert.Single(r.MisesAJour);
        Assert.Null(m.Identifiant);
        Assert.Equal("Outil Exemple", m.Nom);
        Assert.Equal("3.2", m.VersionDisponible);
    }

    [Fact]
    public void Une_version_coupee_est_signalee()
    {
        var r = LecteurWinget.Lire(Sortie(EnTeteFrancais, Tirets,
            Ligne("Outil Exemple", "Editeur.Outil", "1.2.3.4…", "1.3", "winget")));

        Assert.True(Assert.Single(r.MisesAJour).VersionCoupee);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("\r\n")]
    public void Une_sortie_vide_ne_dit_pas_a_jour_elle_dit_aucun_tableau(string? sortie)
    {
        var r = LecteurWinget.Lire(sortie);

        Assert.Equal(EtatLectureWinget.AucunTableau, r.Etat);
        Assert.Empty(r.MisesAJour);
        Assert.Null(r.NombreAnnonce);
    }

    [Fact]
    public void Un_message_d_erreur_de_source_donne_aucun_tableau()
    {
        // Forme d'un message d'échec de source ; le texte exact de winget sera repris
        // d'une capture réelle.
        var r = LecteurWinget.Lire(Sortie(
            "Échec lors de la recherche dans la source : winget",
            "Une erreur inattendue s'est produite lors de l'exécution de la commande :",
            "0x8a15005e : The server certificate did not match any of the expected values."));

        Assert.Equal(EtatLectureWinget.AucunTableau, r.Etat);
        Assert.Empty(r.MisesAJour);
    }

    [Fact]
    public void L_animation_d_attente_ne_decale_pas_l_en_tete()
    {
        // winget anime son attente par « - \ | / » et des retours chariot seuls.
        var r = LecteurWinget.Lire(Sortie("\r   - \r   \\ \r   | \r   / \r" + EnTeteFrancais, Tirets, LigneNotepad));

        Assert.Equal("Notepad++.Notepad++", Assert.Single(r.MisesAJour).Identifiant);
    }

    [Fact]
    public void Les_fins_de_ligne_Unix_et_les_sequences_du_terminal_sont_acceptees()
    {
        var sortie = "\u001b[0m" + EnTeteFrancais + "\n" + Tirets + "\n" + LigneNotepad + "\u001b[0m\n";

        Assert.Single(LecteurWinget.Lire(sortie).MisesAJour);
    }

    [Fact]
    public void Un_en_tete_qui_n_a_pas_cinq_colonnes_est_refuse_plutot_que_mal_lu()
    {
        var r = LecteurWinget.Lire(Sortie(Ligne("Nom", "ID", "Version", "Disponible"), Tirets,
            Ligne("Notepad++", "Notepad++.Notepad++", "8.8.5", "8.9.8.1")));

        Assert.Equal(EtatLectureWinget.EnTeteInattendu, r.Etat);
        Assert.Empty(r.MisesAJour);
        Assert.Equal(1, r.EnTetesRefuses);
    }

    [Fact]
    public void Une_ligne_decalee_est_comptee_illisible_et_les_autres_restent_lues()
    {
        // Un caractère large dans un nom pousse tout le reste d'une case : l'ID ne
        // commence plus au début de sa colonne.
        var decalee = Ligne("Logiciel Exemple", "Editeur.Exemple", "1.0", "2.0", "winget").Insert(5, "X");

        var r = LecteurWinget.Lire(Sortie(EnTeteFrancais, Tirets, LigneNotepad, decalee, LigneExemple,
            "3 mises à jour disponibles."));

        Assert.Equal(2, r.MisesAJour.Count);
        Assert.Equal(1, r.LignesIllisibles);
        Assert.Equal(3, r.NombreAnnonce); // 2 lues + 1 illisible = le compte de winget
    }

    [Fact]
    public void Une_phrase_longue_juste_sous_le_tableau_le_termine_sans_compter_d_illisible()
    {
        var r = LecteurWinget.Lire(Sortie(EnTeteFrancais, Tirets, LigneNotepad,
            "1 package(s) a(ont) des numéros de version qui ne peuvent pas être déterminés. Utilisez --include-unknown pour voir tous les résultats."));

        Assert.Single(r.MisesAJour);
        Assert.Equal(0, r.LignesIllisibles);
    }

    [Fact]
    public void Un_second_tableau_est_lu_et_numerote()
    {
        var r = LecteurWinget.Lire(Sortie(EnTeteFrancais, Tirets, LigneNotepad, "1 mises à jour disponibles.",
            "",
            "Les packages suivants ont une mise à jour disponible, mais nécessitent un ciblage explicite pour la mise à niveau :",
            EnTeteFrancais, Tirets, LigneExemple));

        Assert.Equal(2, r.MisesAJour.Count);
        Assert.Equal(0, r.MisesAJour[0].Tableau);
        Assert.Equal(1, r.MisesAJour[1].Tableau);
        Assert.Equal(1, r.NombreAnnonce); // celui du PREMIER tableau
    }

    [Fact]
    public void Les_colonnes_se_prennent_aux_mots_de_l_en_tete()
    {
        Assert.Equal(new[] { 0, 20, 42, 51, 62 }, LecteurWinget.DebutsDeColonnes(EnTeteFrancais));
    }

    [Theory]
    [InlineData("----------", true)]
    [InlineData("  ------------------------------  ", true)]
    [InlineData("---", false)]
    [InlineData("----- -----", false)]
    public void Seule_une_ligne_de_tirets_continue_separe_l_en_tete(string ligne, bool attendu) =>
        Assert.Equal(attendu, LecteurWinget.EstLigneDeTirets(ligne));
}
