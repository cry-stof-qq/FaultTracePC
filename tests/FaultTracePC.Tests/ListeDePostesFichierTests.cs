using FaultTracePC.Core;
using Xunit;

namespace FaultTracePC.Tests;

/// <summary>
/// Point 64, lot B. Le fichier par lequel la console dit au script sur quels postes
/// travailler. Ce qui se teste ici est ce qui, en cas d'erreur, ne lèverait pas :
/// une liste silencieusement raccourcie, un doublon qui fait passer deux fois sur
/// la même machine, un nom qui n'en est pas un.
/// </summary>
[Collection("Langue")]
public class ListeDePostesFichierTests
{
    private static string Dossier() =>
        Path.Combine(Path.GetTempPath(), "ftpc_liste_" + Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData("POSTE-01", true)]
    [InlineData("A1-23-1-2023", true)]
    [InlineData("poste01", true)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    // Tout ce qui suit n'a aucune chance d'être un vrai poste, et toutes les
    // chances d'être un accident : ce fichier est lu par un script qui en fait
    // des paramètres.
    [InlineData("POSTE 01", false)]
    [InlineData("POSTE';rm", false)]
    [InlineData("POSTE\t01", false)]
    [InlineData("POSTE.01", false)]
    [InlineData("POSTÉ-01", false)]
    public void Un_nom_de_poste_ne_contient_que_des_lettres_chiffres_et_traits_d_union(string nom, bool attendu)
        => Assert.Equal(attendu, ListeDePostes.EstNomValide(nom));

    [Fact]
    public void Ce_qui_est_ecrit_se_relit_a_l_identique()
    {
        var dossier = Dossier();
        try
        {
            var chemin = Path.Combine(dossier, "postes.txt");
            var ecrits = ListeDePostes.Ecrire(chemin, ["POSTE-01", "POSTE-02"]);

            Assert.Equal(2, ecrits);
            Assert.Equal(new[] { "POSTE-01", "POSTE-02" }, ListeDePostes.Lire(chemin));
        }
        finally { if (Directory.Exists(dossier)) Directory.Delete(dossier, true); }
    }

    [Fact]
    public void Les_trois_ecritures_du_meme_nom_ne_font_qu_une_ligne()
    {
        // Même clé que partout ailleurs. Sans ça, le script passerait deux fois sur
        // la même machine — et sur une installation, deux fois n'est pas une fois.
        var ecartes = new List<string>();
        var dossier = Dossier();
        try
        {
            var chemin = Path.Combine(dossier, "postes.txt");
            var ecrits = ListeDePostes.Ecrire(chemin,
                ["POSTE-01", "poste-01", "POSTE-01$", "poste-01.exemple.fr"], ecartes);

            Assert.Equal(1, ecrits);
            Assert.Empty(ecartes);
        }
        finally { if (Directory.Exists(dossier)) Directory.Delete(dossier, true); }
    }

    [Fact]
    public void Ce_qui_est_ecarte_est_nomme_jamais_tu()
    {
        // LE POINT CENTRAL. Une liste silencieusement raccourcie ferait croire à un
        // déploiement complet sur des postes qui n'ont jamais été touchés.
        var ecartes = new List<string>();
        var dossier = Dossier();
        try
        {
            var chemin = Path.Combine(dossier, "postes.txt");
            var ecrits = ListeDePostes.Ecrire(chemin, ["POSTE-01", "POSTE 02", "", "POSTE;03"], ecartes);

            Assert.Equal(1, ecrits);
            Assert.Equal(new[] { "POSTE 02", "POSTE;03" }, ecartes);
        }
        finally { if (Directory.Exists(dossier)) Directory.Delete(dossier, true); }
    }

    [Fact]
    public void Commentaires_et_lignes_vides_ne_sont_pas_des_postes()
    {
        var lus = ListeDePostes.Analyser(
        [
            "# FaultTracePC - liste de postes",
            "",
            "POSTE-01",
            "POSTE-02   # celui de la salle 12",
            "   ",
            "# POSTE-03 : mis de côté",
        ]);

        Assert.Equal(new[] { "POSTE-01", "POSTE-02" }, lus);
    }

    [Theory]
    [InlineData(AppLanguage.French)]
    [InlineData(AppLanguage.English)]
    public void Le_fichier_ecrit_ne_contient_que_de_l_ascii(AppLanguage langue)
    {
        // C'est ce qui rend la question de l'encodage sans objet : quelle que soit
        // la façon dont PowerShell relit le fichier, il lira la même chose.
        //
        // DANS LES DEUX LANGUES : l'en-tête du fichier est traduit, et c'est là que
        // le premier accent s'introduirait sans qu'on y pense.
        var avant = Lang.Current;
        var dossier = Dossier();
        try
        {
            Lang.Apply(langue);
            var chemin = Path.Combine(dossier, "postes.txt");
            ListeDePostes.Ecrire(chemin, ["POSTE-01", "A1-23-1-2023"]);

            Assert.All(File.ReadAllBytes(chemin), o => Assert.True(o < 128, $"octet non ASCII : {o}"));
        }
        finally
        {
            Lang.Apply(avant);
            if (Directory.Exists(dossier)) Directory.Delete(dossier, true);
        }
    }

    [Fact]
    public void Une_liste_absente_n_est_pas_une_liste_vide_qui_leve()
    {
        Assert.Empty(ListeDePostes.Lire(Path.Combine(Dossier(), "jamais-ecrit.txt")));
    }
}
