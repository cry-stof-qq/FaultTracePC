using FaultTracePC.Core;
using Xunit;

namespace FaultTracePC.Tests;

/// <summary>
/// Point 64, lot A-2. L'interrogation de l'annuaire elle-même ne se teste pas ici —
/// elle demande un domaine. Ce qui se teste, et qui est justement là où les erreurs
/// se logent : la construction du chemin, la lecture du nom distinctif, le bit de
/// compte désactivé et l'horodatage Windows.
/// </summary>
[Collection("Langue")]
public class AnnuaireTests
{
    // ------------------------------------------------------------------
    // Chemin LDAP
    // ------------------------------------------------------------------

    [Theory]
    // Vide : Windows résout la racine du domaine du poste. C'est le cas par défaut,
    // celui où l'utilisateur n'a rien à saisir.
    [InlineData("", "LDAP://")]
    [InlineData("   ", "LDAP://")]
    [InlineData(null, "LDAP://")]
    [InlineData("OU=Postes,DC=exemple,DC=fr", "LDAP://OU=Postes,DC=exemple,DC=fr")]
    // Les deux écritures sont acceptées : on ne double pas le préfixe.
    [InlineData("LDAP://OU=Postes,DC=exemple,DC=fr", "LDAP://OU=Postes,DC=exemple,DC=fr")]
    [InlineData("ldap://OU=Postes,DC=exemple,DC=fr", "ldap://OU=Postes,DC=exemple,DC=fr")]
    public void Le_chemin_ldap_accepte_les_deux_ecritures(string? saisie, string attendu)
        => Assert.Equal(attendu, ParkDirectory.CheminLdap(saisie));

    // ------------------------------------------------------------------
    // Compte désactivé
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(4096, false)]    // WORKSTATION_TRUST_ACCOUNT : un poste ordinaire
    [InlineData(4098, true)]     // le même, avec le bit ACCOUNTDISABLE
    [InlineData(0, false)]
    [InlineData(2, true)]
    [InlineData(532480, false)]  // contrôleur de domaine
    public void Le_bit_de_compte_desactive_est_lu_et_pas_devine(int userAccountControl, bool attendu)
        => Assert.Equal(attendu, ParkDirectory.EstDesactive(userAccountControl));

    // ------------------------------------------------------------------
    // Unité d'organisation lisible
    // ------------------------------------------------------------------

    [Fact]
    public void L_unite_se_lit_du_plus_general_au_plus_precis()
    {
        // Le nom distinctif écrit l'arborescence à l'envers : on la remet à l'endroit.
        Assert.Equal("Postes / Salle 12",
            ParkDirectory.UniteLisible("CN=POSTE-01,OU=Salle 12,OU=Postes,DC=exemple,DC=fr"));
    }

    [Fact]
    public void Un_poste_a_la_racine_n_a_pas_d_unite()
    {
        Assert.Equal("", ParkDirectory.UniteLisible("CN=POSTE-01,CN=Computers,DC=exemple,DC=fr"));
        Assert.Equal("", ParkDirectory.UniteLisible(""));
        Assert.Equal("", ParkDirectory.UniteLisible(null));
    }

    [Fact]
    public void Une_virgule_dans_un_nom_d_unite_ne_la_coupe_pas_en_deux()
    {
        // Une unité peut légitimement contenir une virgule ; elle est alors échappée
        // dans le nom distinctif. Un découpage naïf en produirait deux.
        Assert.Equal("Ecole primaire, batiment B",
            ParkDirectory.UniteLisible(@"CN=POSTE-01,OU=Ecole primaire\, batiment B,DC=exemple,DC=fr"));
    }

    [Theory]
    [InlineData("CN=A,OU=B,DC=c", 3)]
    [InlineData(@"CN=A,OU=B\,C,DC=d", 3)]
    [InlineData("DC=exemple", 1)]
    public void Le_decoupage_respecte_l_echappement(string dn, int morceaux)
        => Assert.Equal(morceaux, ParkDirectory.DecouperNomDistinctif(dn).Count);

    // ------------------------------------------------------------------
    // Dernière ouverture de session
    // ------------------------------------------------------------------

    [Fact]
    public void Un_horodatage_windows_devient_une_date_locale()
    {
        // 133 000 000 000 000 000 en temps de fichier Windows = 2022-06-24 (UTC).
        var date = ParkDirectory.DateDeConnexion(133_000_000_000_000_000L);
        Assert.NotNull(date);
        Assert.Equal(2022, date!.Value.Year);
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    // Une valeur aberrante ne doit pas ressortir en date de 1601 : « inconnue » est
    // la seule réponse honnête.
    [InlineData(long.MaxValue)]
    public void Une_valeur_absente_ou_aberrante_rend_inconnue(long valeur)
        => Assert.Null(ParkDirectory.DateDeConnexion(valeur));

    // ------------------------------------------------------------------
    // Réglages
    // ------------------------------------------------------------------

    [Fact]
    public void Les_reglages_par_defaut_interrogent_la_racine_du_domaine()
    {
        // Rien à saisir dans le cas courant : c'est le but.
        Assert.Equal("", new ParametresParc().UniteOrganisation);
        Assert.Equal("LDAP://", ParkDirectory.CheminLdap(new ParametresParc().UniteOrganisation));
    }

    [Fact]
    public void Les_reglages_ne_portent_aucun_champ_de_secret()
    {
        // Garde-fou volontaire : si quelqu'un ajoute un jour un mot de passe ou un
        // jeton dans ce fichier, ce test tombe. Il n'a aucune autre raison de tomber.
        var proprietes = typeof(ParametresParc).GetProperties().Select(p => p.Name.ToLowerInvariant()).ToList();
        foreach (var interdit in new[] { "password", "motdepasse", "token", "jeton", "secret", "clef", "key" })
            Assert.DoesNotContain(proprietes, p => p.Contains(interdit));
    }

    // ------------------------------------------------------------------
    // Contrôle de la saisie
    // ------------------------------------------------------------------

    private static string EnFrancais(Func<string> f)
    {
        var initial = Lang.Current;
        try { Lang.Apply(AppLanguage.French); return f(); }
        finally { Lang.Apply(initial); }
    }

    [Theory]
    // Vide = la racine du domaine : c'est une saisie valide, et c'est le cas par défaut.
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("OU=Postes,DC=exemple,DC=fr")]
    [InlineData("LDAP://OU=Postes,DC=exemple,DC=fr")]
    // Virgule correctement échappée : rien à signaler.
    [InlineData(@"OU=Ecole primaire\, batiment B,OU=Postes,DC=exemple,DC=fr")]
    public void Une_saisie_exploitable_ne_produit_aucun_message(string? saisie)
        => Assert.Equal("", EnFrancais(() => ParkDirectory.VerifierUnite(saisie)));

    [Fact]
    public void Une_virgule_non_echappee_est_nommee_avec_sa_correction()
    {
        // L'annuaire, lui, répondrait « syntaxe non valide » — ou rien du tout.
        var message = EnFrancais(() => ParkDirectory.VerifierUnite("OU=Ecole primaire, batiment B,DC=exemple,DC=fr"));

        Assert.Contains("virgule", message);
        Assert.Contains(@"\,", message);
    }

    [Fact]
    public void Un_chemin_sans_racine_de_domaine_est_signale()
    {
        var message = EnFrancais(() => ParkDirectory.VerifierUnite("OU=Postes"));

        Assert.Contains("racine du domaine", message);
        Assert.Contains("DC=", message);
    }

    [Fact]
    public void Deux_fautes_a_la_fois_sont_dites_toutes_les_deux()
    {
        // Sinon l'utilisateur corrige, relance, et découvre la seconde faute.
        var message = EnFrancais(() => ParkDirectory.VerifierUnite("OU=Ecole, batiment B"));

        Assert.Contains("virgule", message);
        Assert.Contains("racine du domaine", message);
    }

    [Theory]
    [InlineData("OU=Postes", true)]
    [InlineData("DC=fr", true)]
    [InlineData("CN=POSTE-01", true)]
    [InlineData(@"OU=Ecole primaire\, batiment B", true)]
    // Le fragment laissé par une virgule non échappée.
    [InlineData(" batiment B", false)]
    [InlineData("", false)]
    [InlineData("=valeur", false)]
    [InlineData("OU=", false)]
    [InlineData("1OU=x", false)]
    public void Un_morceau_de_nom_distinctif_a_la_forme_attribut_egale_valeur(string morceau, bool valide)
        => Assert.Equal(valide, ParkDirectory.EstMorceauValide(morceau));
}
