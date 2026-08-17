using FaultTracePC.Core;
using Xunit;

namespace FaultTracePC.Tests;

/// <summary>
/// Collection réservée aux tests qui BASCULENT la langue en cours.
/// <c>Lang.Current</c> est un état global : sans barrière, un test de rapport
/// tournant en parallèle lirait la langue changée par un autre et échouerait une
/// fois sur deux, sans rapport avec ce qu'il vérifie.
///
/// Cette collection ne protège que les tests qui la rejoignent — elle ne dit rien
/// des autres collections, qui tournent en même temps. La vraie barrière est donc
/// posée à l'échelle de l'assemblage (voir AssemblyInfo.cs) ; celle-ci reste, car
/// elle DOCUMENTE quels tests touchent à la langue.
/// </summary>
[CollectionDefinition("Langue", DisableParallelization = true)]
public class LangueCollection;

/// <summary>
/// Choix de la langue. Tout passe par la règle pure <c>Lang.Resolve</c> : aucun
/// test ne lit de fichier ni n'interroge la culture de la machine, sinon la
/// suite passerait ou échouerait selon le poste qui l'exécute.
/// </summary>
[Collection("Langue")]
public class LangTests
{
    // --- Ordre de priorité -------------------------------------------------

    [Fact]
    public void Argument_prime_sur_tout_le_reste()
    {
        Assert.Equal(AppLanguage.English,
            Lang.Resolve(["--lang", "en"], storedRaw: "fr", machineRaw: null, sessionCulture: "fr"));
        Assert.Equal(AppLanguage.French,
            Lang.Resolve(["--lang", "fr"], storedRaw: "en", machineRaw: null, sessionCulture: "en"));
    }

    [Fact]
    public void Preference_enregistree_prime_sur_la_session()
    {
        Assert.Equal(AppLanguage.English, Lang.Resolve(null, "en", null, "fr"));
        Assert.Equal(AppLanguage.French, Lang.Resolve(null, "fr", null, "de"));
    }

    [Fact]
    public void Auto_ignore_la_preference_et_suit_windows()
    {
        // C'est tout l'intérêt de « auto » : sans lui, impossible de revenir au
        // comportement automatique sans supprimer le fichier à la main.
        Assert.Equal(AppLanguage.English, Lang.Resolve(["--lang", "auto"], "fr", null, "en"));
        Assert.Equal(AppLanguage.French, Lang.Resolve(["--lang", "auto"], "en", null, "fr"));
    }

    [Theory]
    [InlineData("fr", AppLanguage.French)]
    [InlineData("FR", AppLanguage.French)]
    [InlineData("en", AppLanguage.English)]
    [InlineData("de", AppLanguage.English)]   // toute autre langue : anglais
    [InlineData("zh", AppLanguage.English)]
    public void Session_windows_utilisee_a_defaut(string culture, AppLanguage attendu)
    {
        Assert.Equal(attendu, Lang.Resolve(null, null, null, culture));
    }

    [Fact]
    public void Sans_rien_de_determinable_le_repli_est_le_francais()
    {
        // Le logiciel a été exclusivement français jusqu'à la 1.2.3 : c'est le
        // repli qui ne surprend aucun utilisateur existant.
        Assert.Equal(AppLanguage.French, Lang.Resolve(null, null, null, null));
        Assert.Equal(AppLanguage.French, Lang.Resolve([], "   ", null, null));
    }

    // --- Lecture de l'argument --------------------------------------------

    [Theory]
    [InlineData("--lang", "en", "en")]
    [InlineData("--langue", "fr", "fr")]
    [InlineData("-lang", "auto", "auto")]
    public void Valeur_dans_l_argument_suivant(string nom, string valeur, string attendu)
    {
        Assert.Equal(attendu, Lang.FromArguments([nom, valeur]));
    }

    [Theory]
    [InlineData("--lang=en", "en")]
    [InlineData("--lang:FR", "fr")]
    [InlineData("/lang=english", "en")]
    [InlineData("--langue=français", "fr")]
    public void Valeur_collee_a_l_argument(string arg, string attendu)
    {
        Assert.Equal(attendu, Lang.FromArguments([arg]));
    }

    [Fact]
    public void Argument_inconnu_ou_incomplet_est_ignore_sans_erreur()
    {
        // Une tâche planifiée ou une GPO ne doit jamais échouer sur une faute de
        // frappe dans un paramètre de confort.
        Assert.Null(Lang.FromArguments(["--lang", "klingon"]));
        Assert.Null(Lang.FromArguments(["--lang"]));
        Assert.Null(Lang.FromArguments(["--quiet", "--json"]));
        Assert.Null(Lang.FromArguments([]));
        Assert.Null(Lang.FromArguments(null));
    }

    [Fact]
    public void Argument_de_langue_ne_mange_pas_l_argument_suivant()
    {
        // « --lang » suivi d'une autre option : la valeur est invalide, mais
        // l'option qui suit doit rester lisible par l'analyseur du CLI.
        Assert.Null(Lang.FromArguments(["--lang", "--quiet"]));
        Assert.Equal("en", Lang.FromArguments(["--days", "90", "--lang", "en", "--quiet"]));
    }

    [Fact]
    public void Chemin_de_la_preference_est_dans_le_profil_utilisateur()
    {
        // Régression : une première version enregistrait ce marqueur à côté de
        // l'exécutable, donc commun à tous les comptes d'un poste partagé.
        var chemin = Lang.PreferencePath;
        Assert.EndsWith(Path.Combine("FaultTracePC", "langue.txt"), chemin);
        Assert.StartsWith(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            chemin, StringComparison.OrdinalIgnoreCase);
    }

    // --- Bascule ----------------------------------------------------------

    [Fact]
    public void Apply_change_la_langue_et_la_culture_de_mise_en_forme()
    {
        var initial = Lang.Current;
        try
        {
            Lang.Apply(AppLanguage.English);
            Assert.Equal("today", Lang.T("aujourd'hui", "today"));
            Assert.Equal("en-GB", Lang.Culture.Name);
            Assert.False(Lang.IsFrench);

            Lang.Apply(AppLanguage.French);
            Assert.Equal("aujourd'hui", Lang.T("aujourd'hui", "today"));
            Assert.Equal("fr-FR", Lang.Culture.Name);
            Assert.True(Lang.IsFrench);
        }
        finally
        {
            Lang.Apply(initial);
        }
    }

    // --- Sélecteur de l'interface -----------------------------------------

    [Theory]
    [InlineData(AppLanguage.French, "fr")]
    [InlineData(AppLanguage.English, "en")]
    public void Code_ecrit_exactement_ce_que_le_fichier_attend(AppLanguage langue, string attendu)
    {
        // Ce code est relu par Resolve : s'ils divergent, la préférence
        // enregistrée devient silencieusement « automatique » au prochain lancement.
        Assert.Equal(attendu, Lang.Code(langue));
        Assert.Equal(langue, Lang.Resolve(null, Lang.Code(langue), null, "de"));
    }

    [Fact]
    public void Code_de_l_absence_de_preference_est_auto()
    {
        Assert.Equal("auto", Lang.Code(null));
        // « auto » ne doit surtout pas être lu comme une langue : la session gagne.
        Assert.Equal(AppLanguage.English, Lang.Resolve(null, Lang.Code(null), null, "en"));
        Assert.Equal(AppLanguage.French, Lang.Resolve(null, Lang.Code(null), null, "fr"));
    }

    [Fact]
    public void Effective_rend_la_langue_choisie_quelle_que_soit_la_machine()
    {
        // Une préférence explicite ne dépend ni du poste ni de la session : c'est
        // sur cette égalité que le sélecteur décide de proposer un redémarrage.
        // Le cas « automatique » n'est pas testé ici — il dépend de la machine,
        // et un test qui passe ou échoue selon le poste ne prouve rien.
        Assert.Equal(AppLanguage.French, Lang.Effective(AppLanguage.French));
        Assert.Equal(AppLanguage.English, Lang.Effective(AppLanguage.English));
    }

    [Fact]
    public void T_avec_langue_imposee_ignore_la_langue_en_cours()
    {
        // Sert à poser la question « redémarrer ? » dans la langue que
        // l'utilisateur vient de choisir, pas dans celle qu'il quitte.
        var initial = Lang.Current;
        try
        {
            Lang.Apply(AppLanguage.French);
            Assert.Equal("Language", Lang.T(AppLanguage.English, "Langue", "Language"));

            Lang.Apply(AppLanguage.English);
            Assert.Equal("Langue", Lang.T(AppLanguage.French, "Langue", "Language"));
        }
        finally
        {
            Lang.Apply(initial);
        }
    }

    // --- Préférence de portée machine -------------------------------------

    [Fact]
    public void Preference_machine_prime_sur_la_session_windows()
    {
        // C'est toute sa raison d'être : sans elle, un administrateur ne peut pas
        // imposer l'anglais à un parc dont les sessions sont françaises.
        Assert.Equal(AppLanguage.English, Lang.Resolve(null, storedRaw: null, machineRaw: "en", sessionCulture: "fr"));
        Assert.Equal(AppLanguage.French, Lang.Resolve(null, storedRaw: null, machineRaw: "fr", sessionCulture: "en"));
    }

    [Fact]
    public void Choix_de_l_utilisateur_prime_sur_le_reglage_machine()
    {
        // Un réglage d'administrateur est un DÉFAUT, pas une contrainte. Le
        // contraire ferait passer le sélecteur de l'application pour cassé :
        // l'utilisateur clique « English », et le lancement suivant revient au
        // français sans explication.
        Assert.Equal(AppLanguage.English, Lang.Resolve(null, storedRaw: "en", machineRaw: "fr", sessionCulture: "fr"));
        Assert.Equal(AppLanguage.French, Lang.Resolve(null, storedRaw: "fr", machineRaw: "en", sessionCulture: "en"));
    }

    [Fact]
    public void Argument_et_auto_passent_devant_le_reglage_machine()
    {
        // --lang impose. « auto » demande explicitement la session Windows : il
        // saute les DEUX préférences, pas seulement celle de l'utilisateur.
        Assert.Equal(AppLanguage.French, Lang.Resolve(["--lang", "fr"], "en", "en", "en"));
        Assert.Equal(AppLanguage.English, Lang.Resolve(["--lang", "auto"], "fr", "fr", "en"));
        Assert.Equal(AppLanguage.French, Lang.Resolve(["--lang", "auto"], "en", "en", "fr"));
    }

    [Fact]
    public void Reglage_machine_illisible_ne_bloque_pas_la_resolution()
    {
        // Un fichier vide, tronqué ou contenant n'importe quoi doit être ignoré,
        // pas provoquer une erreur : il est écrit par un installeur.
        Assert.Equal(AppLanguage.English, Lang.Resolve(null, null, "", "en"));
        Assert.Equal(AppLanguage.English, Lang.Resolve(null, null, "klingon", "en"));
        Assert.Equal(AppLanguage.French, Lang.Resolve(null, null, "  \r\n", "fr"));
    }

    [Fact]
    public void Chemin_du_reglage_machine_est_commun_a_tous_les_comptes()
    {
        // Sous ProgramData, et non dans un profil : le service de surveillance
        // tourne en SYSTEM et ne verrait jamais un fichier rangé dans Documents.
        var chemin = Lang.MachinePreferencePath;
        Assert.EndsWith(Path.Combine("FaultTracePC", "langue.txt"), chemin);
        Assert.StartsWith(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            chemin, StringComparison.OrdinalIgnoreCase);
        Assert.NotEqual(Lang.PreferencePath, chemin);
    }
}
