using FaultTracePC.Core;
using Xunit;

namespace FaultTracePC.Tests;

/// <summary>
/// Point 36 : les fenêtres PowerShell ne se fermaient plus jamais toutes seules.
///
/// Ce qui est vérifié ici est la LIGNE D'ARGUMENTS produite — le texte que l'hôte
/// PowerShell va relire. C'est la même leçon qu'en 1.4.1 : un texte écrit pour
/// qu'un autre programme le relise se teste en le relisant.
/// </summary>
public class LancementPowerShellTests
{
    private const string Pause = "Appuyer sur Entrée pour fermer";

    [Fact]
    public void Plus_aucun_NoExit()
    {
        // C'est -NoExit qui rendait fausse la phrase « Appuyer sur Entrée pour
        // fermer » : sa présence suffit à rouvrir le défaut.
        Assert.DoesNotContain("-NoExit", PowerShellLauncher.ArgumentsForScript(@"C:\x\y.ps1", Pause));
        Assert.DoesNotContain("-NoExit", PowerShellLauncher.ArgumentsForCommand("sfc /scannow", Pause));
    }

    [Fact]
    public void Une_commande_met_toujours_en_pause_a_la_fin()
    {
        // Une commande de la boîte à outils n'a pas d'invite à elle : sans cette
        // pause, sa fenêtre disparaîtrait avec son résultat.
        var args = PowerShellLauncher.ArgumentsForCommand("sfc /scannow", Pause);

        Assert.Contains("finally { Read-Host '" + Pause + "' }", args);
        Assert.Contains("-Command \"", args);
    }

    [Fact]
    public void Un_script_ne_met_en_pause_que_s_il_n_est_pas_alle_au_bout()
    {
        // Le script engendré porte DÉJÀ sa propre invite de fin. Une pause
        // inconditionnelle obligerait à appuyer deux fois sur Entrée — le genre
        // de détail qui fait dire que le logiciel est mal fini.
        var args = PowerShellLauncher.ArgumentsForScript(@"C:\x\y.ps1", Pause);

        Assert.Contains("$fini = $false", args);
        Assert.Contains("; $fini = $true }", args);
        Assert.Contains("finally { if (-not $fini) { Read-Host '" + Pause + "' } }", args);
    }

    [Theory]
    [InlineData(@"C:\Users\O'Brien\Documents\Reparation.ps1")]
    [InlineData("C:\\Users\\O\u2019Brien\\Documents\\Reparation.ps1")]
    public void Un_chemin_avec_apostrophe_ne_casse_pas_la_chaine(string chemin)
    {
        var args = PowerShellLauncher.ArgumentsForScript(chemin, Pause);

        // Aucune apostrophe typographique ne survit, et les apostrophes droites
        // sont en nombre pair : la chaîne se referme.
        Assert.DoesNotContain('\u2019', args);
        Assert.Equal(0, args.Count(c => c == '\'') % 2);
        Assert.Contains("O''Brien", args);
    }

    [Fact]
    public void Les_guillemets_de_la_commande_sont_echappes_pour_l_hote()
    {
        var args = PowerShellLauncher.ArgumentsForCommand("Get-Content \"C:\\log.txt\"", Pause);

        Assert.Contains("\\\"C:\\log.txt\\\"", args);
    }

    [Fact]
    public void Une_commande_nulle_ne_fait_pas_tomber_le_lancement()
    {
        Assert.Contains("finally { Read-Host", PowerShellLauncher.ArgumentsForCommand(null!, Pause));
    }
}
