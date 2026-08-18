using FaultTracePC.Core;
using Xunit;

namespace FaultTracePC.Tests;

/// <summary>
/// Lecture de la stratégie d'exécution PowerShell.
///
/// L'enjeu est une erreur de raisonnement facile à commettre : croire que
/// « -ExecutionPolicy Bypass » suffit. Il ne fixe que la portée Process, la plus
/// faible de toutes ; une stratégie de groupe la remplace. Se tromper ici, c'est
/// annoncer à l'utilisateur que tout va bien juste avant qu'une console s'ouvre
/// et se referme sans un mot.
/// </summary>
public class PowerShellPolicyTests
{
    [Theory]
    [InlineData("Restricted")]
    [InlineData("AllSigned")]
    public void Une_strategie_de_groupe_restrictive_bloque(string valeur)
    {
        var etat = PowerShellPolicy.Interpret(
            $"MachinePolicy={valeur}\nUserPolicy=Undefined\nProcess=Bypass\nCurrentUser=Undefined\nLocalMachine=Undefined");

        Assert.True(etat.Blocked);
        Assert.Equal("MachinePolicy", etat.Scope);
        Assert.Equal(valeur, etat.Policy);
    }

    [Fact]
    public void La_portee_utilisateur_bloque_aussi()
    {
        var etat = PowerShellPolicy.Interpret(
            "MachinePolicy=Undefined\nUserPolicy=AllSigned\nProcess=Bypass\nLocalMachine=Restricted");

        Assert.True(etat.Blocked);
        Assert.Equal("UserPolicy", etat.Scope);
    }

    [Fact]
    public void LocalMachine_restrictive_ne_bloque_pas()
    {
        // C'est le cas le plus courant, et le piège : LocalMachine est la valeur
        // par défaut de Windows, elle est souvent Restricted, et notre Bypass de
        // portée Process la remplace parfaitement. Confondre les deux ferait
        // refuser la réparation sur la majorité des machines saines.
        var etat = PowerShellPolicy.Interpret(
            "MachinePolicy=Undefined\nUserPolicy=Undefined\nProcess=Bypass\nCurrentUser=Undefined\nLocalMachine=Restricted");

        Assert.False(etat.Blocked);
        Assert.Null(etat.Scope);
    }

    [Fact]
    public void Une_strategie_de_groupe_permissive_ne_bloque_pas()
    {
        var etat = PowerShellPolicy.Interpret("MachinePolicy=RemoteSigned\nUserPolicy=Undefined");
        Assert.False(etat.Blocked);
    }

    [Fact]
    public void La_lecture_tolere_les_espaces_les_retours_Windows_et_la_casse()
    {
        var etat = PowerShellPolicy.Interpret("  machinepolicy = restricted  \r\nUserPolicy=Undefined\r\n");
        Assert.True(etat.Blocked);
    }

    [Fact]
    public void Une_sortie_incomprehensible_ne_bloque_rien()
    {
        // Mieux vaut tenter la réparation et montrer l'échec que la refuser sur
        // une lecture ratée : le refus, lui, serait définitif pour l'utilisateur.
        Assert.False(PowerShellPolicy.Interpret("Get-ExecutionPolicy : terme non reconnu").Blocked);
        Assert.False(PowerShellPolicy.Interpret("").Blocked);
    }
}
