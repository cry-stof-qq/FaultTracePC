using FaultTracePC.Core;
using Xunit;

namespace FaultTracePC.Tests;

/// <summary>
/// Point 79, lot 1 (décisions D1, D3 et D4 du 17/10/2026) : ce qui décide
/// d'afficher les boutons « Parc » et « Réseau ».
/// </summary>
public class ModeParcTests
{
    [Theory]
    [InlineData(false, false, true)]   // portable ou installateur antérieur : tout visible (D4, transition)
    [InlineData(false, true,  true)]
    [InlineData(true,  true,  true)]   // installateur 1.9.0, case cochée
    [InlineData(true,  false, false)]  // installateur 1.9.0, case laissée vide
    public void Console_visible_selon_la_case(bool geree, bool cochee, bool attendu) =>
        Assert.Equal(attendu, ModeParc.ConsoleVisible(geree, cochee));

    [Theory]
    [InlineData(true,  false, true)]
    [InlineData(false, true,  true)]   // D3 : poste exposé, « Réseau » reste visible
    [InlineData(false, false, false)]
    public void Reseau_visible_avec_la_console_ou_en_mode_client(bool console, bool client, bool attendu) =>
        Assert.Equal(attendu, ModeParc.ReseauVisible(console, client));

    [Theory]
    [InlineData(1, true)]
    [InlineData(0, false)]
    [InlineData("1", true)]
    [InlineData(" 1 ", true)]
    [InlineData("0", false)]
    [InlineData(null, false)]
    public void Valeur_de_registre_lue(object? valeur, bool attendu) =>
        Assert.Equal(attendu, ModeParc.ValeurVraie(valeur));

    [Fact]
    public void La_valeur_Installed_existante_n_est_pas_reutilisee()
    {
        // Elle est posée par tous les MSI depuis longtemps : s'en servir aurait
        // caché la console à tous les postes encore en 1.8.x.
        Assert.NotEqual("Installed", ModeParc.ValeurGeree);
        Assert.NotEqual("Installed", ModeParc.ValeurCase);
    }
}
