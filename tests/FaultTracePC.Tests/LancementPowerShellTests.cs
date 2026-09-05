using System;
using System.Linq;
using FaultTracePC.Core;
using FaultTracePC.Core.Report;
using Xunit;

namespace FaultTracePC.Tests;

/// <summary>
/// Point 36 : les fenêtres PowerShell ne se fermaient plus jamais toutes seules.
///
/// Ce qui est vérifié ici est la LIGNE D'ARGUMENTS produite — le texte que l'hôte
/// PowerShell va relire. C'est la même leçon qu'en 1.4.1 : un texte écrit pour
/// qu'un autre programme le relise se teste en le relisant.
/// </summary>
[Collection("Langue")]
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

    // --- Le lanceur .bat, resté en arrière jusqu'à la 1.5.2 ----------------

    private const string PauseBat = "Appuyer sur Entree pour fermer";

    private static string Bat() =>
        RepairScriptGenerator.Lanceur("Reparation_PC_2026-09-05_2130.ps1", PauseBat);

    /// <summary>
    /// La seule ligne qui s'exécute. Les lignes « rem » sont du texte pour
    /// l'utilisateur : y chercher un drapeau reviendrait à interdire d'expliquer
    /// pourquoi on ne s'en sert plus.
    /// </summary>
    private static string LigneDeCommande() =>
        Bat().Split("\r\n").Single(l => l.StartsWith("powershell", StringComparison.Ordinal));

    [Fact]
    public void Le_lanceur_bat_n_impose_plus_NoExit()
    {
        // Les trois boutons de l'application ont été corrigés en 1.5.0 ; le .bat
        // double-cliqué à côté du rapport, lui, gardait sa fenêtre ouverte
        // indéfiniment. C'était la dernière moitié du point 36.
        Assert.DoesNotContain("-NoExit", LigneDeCommande());
        Assert.DoesNotContain("'-File'", LigneDeCommande());
    }

    [Fact]
    public void Le_lanceur_bat_met_en_pause_seulement_si_le_script_a_echoue()
    {
        var bat = Bat();
        // Le drapeau conditionnel : sans lui, l'utilisateur appuierait deux fois
        // sur Entrée, le .ps1 ayant déjà sa propre invite.
        Assert.Contains("$fini = $false", bat);
        Assert.Contains("if (-not $fini) { Read-Host", bat);
        Assert.Contains("catch { Write-Host $_ }", bat);
        // -Command et non -File : c'est ce qui permet à l'enrobage de démarrer
        // même quand la stratégie de groupe refuse le fichier .ps1.
        Assert.Contains("'-Command'", bat);
    }

    [Fact]
    public void Le_lanceur_bat_designe_le_script_pose_a_cote_de_lui()
    {
        // %~dp0 : les deux fichiers se déplacent ensemble. Les apostrophes sont
        // doublées parce que le tout voyage déjà dans un littéral PowerShell.
        Assert.Contains("''%~dp0Reparation_PC_2026-09-05_2130.ps1''", Bat());
    }

    [Theory]
    [InlineData(AppLanguage.French)]
    [InlineData(AppLanguage.English)]
    public void Le_lanceur_bat_est_integralement_ascii(AppLanguage langue)
    {
        // Le fichier est écrit en ASCII : un seul caractère accentué s'y
        // transformerait en « ? » à l'écran, invite comprise. Le test porte sur
        // l'invite réellement employée, pas sur une constante de test.
        var initial = Lang.Current;
        try
        {
            Lang.Apply(langue);
            var bat = RepairScriptGenerator.Lanceur(
                "Reparation_PC_2026-09-05_2130.ps1",
                RepairScriptGenerator.PauseLanceur);
            var fautif = bat.FirstOrDefault(c => c > 127);
            Assert.True(fautif == default, $"caractere non-ASCII dans le lanceur : {fautif}");
        }
        finally
        {
            Lang.Apply(initial);
        }
    }
}
