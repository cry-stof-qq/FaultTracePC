using FaultTracePC.Cli;
using Xunit;

namespace FaultTracePC.Tests;

/// <summary>
/// Analyse de la ligne de commande.
///
/// DÉFAUT CONSTATÉ LE 29/08/2026 : l'auteur lance
/// « FaultTracePC.Cli.exe --generate-master-secret » avec un exécutable trop
/// ancien pour connaître l'option. Au lieu de refuser, le programme a lancé une
/// analyse complète de trente jours — et rendu 0, donc annoncé un succès.
///
/// La cause n'était pas la vieille version : c'est que le « switch » d'analyse
/// n'avait pas de cas par défaut. Une faute de frappe dans un script de
/// déploiement (« --configure-remot ») aurait analysé tout un parc au lieu de le
/// configurer, silencieusement.
///
/// Les assertions ne portent que sur des jetons présents dans les deux langues :
/// ces tests ne doivent pas dépendre de la langue en cours.
/// </summary>
public class LigneDeCommandeTests
{
    [Theory]
    [InlineData("--generate-master-secrets")]
    [InlineData("--configure-remot")]
    [InlineData("-x")]
    [InlineData("nawak")]
    public void Une_option_inconnue_est_refusee_et_nommee(string inconnue)
    {
        var o = Program.CliOptions.Parse([inconnue]);

        Assert.NotNull(o.Error);
        Assert.Contains(inconnue, o.Error);
    }

    [Fact]
    public void La_premiere_erreur_est_celle_qui_est_rapportee()
    {
        // « --days abc » : la faute est sur --days. Rapporter « option inconnue :
        // abc » enverrait chercher au mauvais endroit.
        var o = Program.CliOptions.Parse(["--days", "abc"]);

        Assert.NotNull(o.Error);
        Assert.Contains("--days", o.Error);
    }

    [Fact]
    public void Une_valeur_manquante_pour_output_est_nommee()
    {
        var o = Program.CliOptions.Parse(["--output"]);

        Assert.NotNull(o.Error);
        Assert.Contains("--output", o.Error);
    }

    [Fact]
    public void Les_options_connues_passent_sans_erreur()
    {
        var o = Program.CliOptions.Parse(["--days", "7", "--quiet", "--no-deep", "--open"]);

        Assert.Null(o.Error);
        Assert.Equal(7, o.Days);
        Assert.True(o.Quiet);
        Assert.True(o.NoDeep);
        Assert.True(o.Open);
    }

    [Fact]
    public void La_valeur_de_lang_n_est_pas_prise_pour_une_option()
    {
        // La langue est résolue avant cette analyse ; sa VALEUR ne doit pas
        // retomber dans le cas par défaut qu'on vient d'ajouter.
        Assert.Null(Program.CliOptions.Parse(["--lang", "en"]).Error);
        Assert.Null(Program.CliOptions.Parse(["--langue", "fr", "--quiet"]).Error);
    }

    [Fact]
    public void L_aide_n_est_pas_une_erreur()
    {
        var o = Program.CliOptions.Parse(["--help"]);

        Assert.True(o.ShowHelp);
        Assert.Null(o.Error);
    }

    [Fact]
    public void Le_nombre_de_jours_est_borne()
    {
        Assert.Equal(90, Program.CliOptions.Parse(["--days", "5000"]).Days);
        Assert.Equal(1, Program.CliOptions.Parse(["--days", "0"]).Days);
    }
}
