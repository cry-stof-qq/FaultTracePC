using FaultTracePC.Core;
using Xunit;

namespace FaultTracePC.Tests;

/// <summary>
/// Point 64, lot A-1. La règle qui tient tout ce fichier tient en une phrase, et
/// elle vient de l'en-tête du script de déploiement : <b>postes.csv n'est pas une
/// liste de cibles</b>. Il contient les téléphones et les tablettes que le DHCP a
/// vus ; il ne doit créer aucun poste, seulement en compléter.
/// </summary>
[Collection("Langue")]
public class ListeDePostesTests
{
    private static PosteDuParc Annuaire(string nom, string hote = "", string ou = "", bool desactive = false) => new()
    {
        Name = nom,
        Sources = SourcesDuPoste.ActiveDirectory,
        Host = hote,
        OrganizationalUnit = ou,
        Disabled = desactive,
    };

    private static PosteDuParc Console(string nom, string hote = "", int port = 0) => new()
    {
        Name = nom,
        Sources = SourcesDuPoste.Console,
        Host = hote,
        Port = port,
    };

    // ------------------------------------------------------------------
    // Nom Windows : la clé de dédoublonnage
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("POSTE-01", "POSTE-01")]
    [InlineData("poste-01", "POSTE-01")]                       // la casse ne compte pas
    [InlineData("POSTE-01$", "POSTE-01")]                      // sAMAccountName d'un compte d'ordinateur
    [InlineData("poste-01.exemple.fr", "POSTE-01")]  // dNSHostName
    [InlineData("  POSTE-01  ", "POSTE-01")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    public void Les_trois_ecritures_du_meme_nom_se_rejoignent(string brut, string attendu)
        => Assert.Equal(attendu, ParkInventory.NomWindows(brut));

    [Fact]
    public void Un_poste_vu_par_l_annuaire_et_par_la_console_ne_fait_qu_une_ligne()
    {
        var liste = ParkInventory.Fusionner(
            [Annuaire("POSTE-01$", ou: "Salle informatique")],
            [Console("poste-01.exemple.fr", "10.10.1.24", 58620)]);

        var p = Assert.Single(liste);
        Assert.Equal("POSTE-01", p.Name);
        Assert.True(p.Sources.HasFlag(SourcesDuPoste.ActiveDirectory));
        Assert.True(p.Sources.HasFlag(SourcesDuPoste.Console));
        Assert.Equal("Salle informatique", p.OrganizationalUnit);
    }

    [Fact]
    public void L_adresse_saisie_dans_la_console_passe_devant_celle_de_l_annuaire()
    {
        // Une adresse saisie à la main l'a été pour une raison : souvent une IP fixe
        // qu'aucun DNS ne rendra. L'écraser avec le nom DNS casserait l'accès.
        var liste = ParkInventory.Fusionner(
            [Annuaire("POSTE-01", hote: "poste-01.exemple.fr")],
            [Console("POSTE-01", "10.10.1.24", 58700)]);

        var p = Assert.Single(liste);
        Assert.Equal("10.10.1.24", p.Host);
        Assert.Equal(58700, p.Port);
    }

    [Fact]
    public void Sans_adresse_dans_la_console_celle_de_l_annuaire_sert()
    {
        var liste = ParkInventory.Fusionner(
            [Annuaire("POSTE-01", hote: "poste-01.exemple.fr")],
            [Console("POSTE-01")]);

        var p = Assert.Single(liste);
        Assert.Equal("poste-01.exemple.fr", p.Host);
        Assert.Equal(ParkInventory.PortParDefaut, p.Port);
    }

    [Fact]
    public void Un_compte_desactive_dans_l_annuaire_le_reste()
    {
        var liste = ParkInventory.Fusionner([Annuaire("POSTE-01", desactive: true)], [Console("POSTE-01")]);
        Assert.True(Assert.Single(liste).Disabled);
    }

    [Fact]
    public void Un_poste_sans_nom_n_entre_pas_dans_la_liste()
    {
        // Le nom n'est pas un libellé : c'est ce dont le jeton du mode parc se
        // déduit, et ce que le déploiement va nommer.
        var liste = ParkInventory.Fusionner([Annuaire("")], [Console("   ")]);
        Assert.Empty(liste);
    }

    [Fact]
    public void La_liste_est_triee_par_nom()
    {
        var liste = ParkInventory.Fusionner([Annuaire("POSTE-10"), Annuaire("POSTE-02")], [Console("ATELIER-07")]);
        Assert.Equal(new[] { "ATELIER-07", "POSTE-02", "POSTE-10" }, liste.Select(p => p.Name).ToArray());
    }

    // ------------------------------------------------------------------
    // postes.csv : il enrichit, il ne crée pas
    // ------------------------------------------------------------------

    [Fact]
    public void Une_adresse_mac_rejoint_un_poste_deja_liste()
    {
        var macs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["POSTE-01"] = "8C-04-BA-11-22-33",
        };

        var liste = ParkInventory.Fusionner([Annuaire("POSTE-01")], null, macs);

        Assert.Equal("8C-04-BA-11-22-33", Assert.Single(liste).Mac);
    }

    [Fact]
    public void Un_nom_present_seulement_dans_l_annuaire_mac_ne_cree_aucun_poste()
    {
        // LE test de ce lot. postes.csv contient les téléphones et les tablettes que
        // le DHCP a rendus ; aucun ne doit apparaître comme un poste.
        var macs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["POSTE-01"] = "8C-04-BA-11-22-33",
            ["IPHONE-DE-QUELQU-UN"] = "AA-BB-CC-11-22-33",
            ["TABLETTE-042"] = "AA-BB-CC-44-55-66",
        };

        var liste = ParkInventory.Fusionner([Annuaire("POSTE-01")], null, macs);

        var p = Assert.Single(liste);
        Assert.Equal("POSTE-01", p.Name);
    }

    [Fact]
    public void Une_mac_inconnue_laisse_le_champ_vide_sans_rien_inventer()
    {
        var liste = ParkInventory.Fusionner([Annuaire("POSTE-09")], null, new Dictionary<string, string>());
        Assert.Equal("", Assert.Single(liste).Mac);
    }

    // ------------------------------------------------------------------
    // Lecture des fichiers
    // ------------------------------------------------------------------

    private static string FichierTemporaire(string contenu, string extension)
    {
        var chemin = Path.Combine(Path.GetTempPath(), $"ftpc-test-{Guid.NewGuid():N}{extension}");
        File.WriteAllText(chemin, contenu, new System.Text.UTF8Encoding(true));   // avec BOM, comme Export-Csv
        return chemin;
    }

    [Fact]
    public void L_annuaire_mac_se_lit_quel_que_soit_l_ordre_des_colonnes()
    {
        var chemin = FichierTemporaire(
            "\"MAC\";\"Nom\"\n\"8C-04-BA-11-22-33\";\"POSTE-01\"\n\"AA-BB-CC-11-22-33\";\"poste-02.exemple.fr\"\n",
            ".csv");
        try
        {
            var macs = ParkInventory.LireAnnuaireMac(chemin);
            Assert.Equal("8C-04-BA-11-22-33", macs["POSTE-01"]);
            // Le DHCP écrit le nom long : il doit rejoindre le nom court.
            Assert.Equal("AA-BB-CC-11-22-33", macs["POSTE-02"]);
        }
        finally { File.Delete(chemin); }
    }

    [Fact]
    public void Un_annuaire_mac_absent_ne_signale_rien()
    {
        // Ce fichier est facultatif : son absence n'est pas un incident et ne doit
        // pas remplir la liste des remarques.
        var notes = new List<string>();
        var macs = ParkInventory.LireAnnuaireMac(Path.Combine(Path.GetTempPath(), "ftpc-inexistant.csv"), notes);
        Assert.Empty(macs);
        Assert.Empty(notes);
    }

    [Fact]
    public void Un_annuaire_mac_sans_les_bonnes_colonnes_le_dit()
    {
        var chemin = FichierTemporaire("Machine;Adresse\nPOSTE-01;8C-04-BA-11-22-33\n", ".csv");
        try
        {
            var notes = new List<string>();
            Assert.Empty(ParkInventory.LireAnnuaireMac(chemin, notes));
            Assert.Single(notes);
        }
        finally { File.Delete(chemin); }
    }

    [Theory]
    [InlineData("a;b;c", new[] { "a", "b", "c" })]
    [InlineData("\"a\";\"b\"", new[] { "a", "b" })]
    [InlineData("\"nom;avec;points\";\"b\"", new[] { "nom;avec;points", "b" })]
    [InlineData("\"guillemet \"\"double\"\"\";b", new[] { "guillemet \"double\"", "b" })]
    [InlineData("", new[] { "" })]
    public void Le_decoupage_csv_respecte_les_guillemets(string ligne, string[] attendu)
        => Assert.Equal(attendu, ParkInventory.DecouperCsv(ligne));

    [Fact]
    public void La_liste_de_la_console_se_lit_sans_reprendre_le_jeton()
    {
        // Le jeton ne doit pas voyager dans une liste destinée à être affichée,
        // exportée ou journalisée : ce qui n'est pas lu ne peut pas fuir.
        var chemin = FichierTemporaire(
            "[{\"Name\":\"POSTE-01\",\"Host\":\"10.10.1.24\",\"Port\":58620,\"Token\":\"ne-doit-pas-ressortir\"}]",
            ".json");
        try
        {
            var postes = ParkInventory.LireParcJson(chemin);
            var p = Assert.Single(postes);
            Assert.Equal("POSTE-01", p.Name);
            Assert.Equal("10.10.1.24", p.Host);
            Assert.Equal(SourcesDuPoste.Console, p.Sources);
            // Aucune propriété ne peut porter le jeton : il n'existe pas dans le modèle.
            Assert.DoesNotContain("ne-doit-pas-ressortir", string.Join("|", p.Name, p.Host, p.Mac, p.OrganizationalUnit));
        }
        finally { File.Delete(chemin); }
    }

    [Fact]
    public void Une_liste_de_console_abimee_est_signalee_au_lieu_d_etre_videe_en_silence()
    {
        var chemin = FichierTemporaire("{ceci n'est pas du JSON", ".json");
        try
        {
            var notes = new List<string>();
            Assert.Empty(ParkInventory.LireParcJson(chemin, notes));
            Assert.Single(notes);
        }
        finally { File.Delete(chemin); }
    }

    [Fact]
    public void Une_liste_de_console_absente_rend_une_liste_vide_sans_remarque()
    {
        var notes = new List<string>();
        Assert.Empty(ParkInventory.LireParcJson(Path.Combine(Path.GetTempPath(), "ftpc-inexistant.json"), notes));
        Assert.Empty(notes);
    }
}
