using FaultTracePC.Core;
using Xunit;

namespace FaultTracePC.Tests;

/// <summary>
/// Le journal des pannes du logiciel lui-même.
///
/// Ce qui est vérifié ici n'est pas qu'il écrit — cela dépend des droits sur
/// ProgramData, donc de la façon dont la suite est lancée, et un test qui en
/// dépendrait serait rouge un jour sur deux sans rien apprendre à personne.
/// Ce qui est vérifié, c'est la promesse qui rend ce journal utilisable dans un
/// chemin de panne : il ne lève jamais d'exception, il ne perd rien de ce qu'on
/// lui confie, et son contenu ne dépend pas de la langue.
/// </summary>
[Collection("Langue")]
public class ErrorLogTests
{
    [Fact]
    public void Le_chemin_est_commun_a_la_machine_et_toujours_calculable()
    {
        // Pas dans Documents : le service de surveillance tourne sous SYSTEM et
        // n'en a pas. C'est la raison d'être de ce choix, elle mérite un test.
        var commun = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        Assert.StartsWith(commun, ErrorLog.FilePath, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith("erreurs.log", ErrorLog.FilePath, StringComparison.OrdinalIgnoreCase);
        Assert.NotEqual(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            Path.GetDirectoryName(ErrorLog.FilePath));
    }

    [Fact]
    public void Une_exception_nulle_ne_fait_rien_et_ne_casse_rien()
    {
        // AppDomain.UnhandledException livre un object : la conversion peut ne
        // rien donner, et l'appelant ne doit pas avoir à s'en soucier.
        Assert.Null(ErrorLog.Write("test", (Exception?)null));
    }

    [Fact]
    public void Le_compte_rendu_remonte_toutes_les_exceptions_internes()
    {
        // Sans cela, on lirait « une erreur est survenue » sans jamais voir la
        // cause réelle, enfouie deux niveaux plus bas.
        var profonde = new InvalidOperationException("CAUSE PROFONDE");
        var milieu = new ApplicationException("NIVEAU INTERMEDIAIRE", profonde);
        var surface = new Exception("CE QUE VOIT L UTILISATEUR", milieu);

        var texte = ErrorLog.Describe(surface);

        Assert.Contains("CAUSE PROFONDE", texte);
        Assert.Contains("NIVEAU INTERMEDIAIRE", texte);
        Assert.Contains("CE QUE VOIT L UTILISATEUR", texte);
        Assert.Contains(nameof(InvalidOperationException), texte);
    }

    [Fact]
    public void Le_compte_rendu_ne_depend_pas_de_la_langue()
    {
        // C'est une pièce technique : un journal venu d'une machine anglaise et
        // un autre d'une machine française doivent se comparer ligne à ligne.
        var ex = new InvalidOperationException("MESSAGE STABLE");

        var initial = Lang.Current;
        try
        {
            Lang.Apply(AppLanguage.French);
            var fr = ErrorLog.Describe(ex);
            Lang.Apply(AppLanguage.English);
            var en = ErrorLog.Describe(ex);
            Assert.Equal(fr, en);
        }
        finally { Lang.Apply(initial); }
    }

    [Fact]
    public void Une_exception_sans_pile_ni_message_ne_fait_pas_tomber_le_journal()
    {
        // Une exception jamais levée n'a pas de pile d'appels. C'est le cas des
        // exceptions construites à la main, et il ne doit pas être un cas d'échec.
        var texte = ErrorLog.Describe(new Exception(""));
        Assert.NotNull(texte);
    }
}
