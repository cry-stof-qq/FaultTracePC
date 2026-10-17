using FaultTracePC.Core;
using Xunit;

namespace FaultTracePC.Tests;

/// <summary>
/// Point 94, lot 2 : « Mettre à jour ce poste » coche dans l'Inventaire la ligne
/// qui désigne le même poste que la Supervision. Le poste coché est celui qui
/// sera mis à jour : une fausse correspondance installerait sur la mauvaise
/// machine. Dans le doute, rien n'est coché.
/// </summary>
public class MemePosteTests
{
    [Theory]
    [InlineData("POSTE-01", "POSTE-01", true)]
    [InlineData("poste-01", "POSTE-01", true)]                  // casse
    [InlineData("POSTE-01.domaine.example", "POSTE-01", true)]  // forme longue
    [InlineData("POSTE-01$", "poste-01", true)]                 // compte d'ordinateur
    [InlineData("POSTE-01", "POSTE-010", false)]
    [InlineData("POSTE-01", "POSTE-02", false)]
    [InlineData("", "", false)]
    [InlineData(null, "POSTE-01", false)]
    [InlineData("10.0.0.5", "10", false)]                       // une adresse n'est pas un nom
    [InlineData("10.0.0.5", "10.0.0.5", false)]
    [InlineData("POSTE 01", "POSTE 01", false)]                 // nom invalide : jamais coché
    public void Meme_poste(string? a, string? b, bool attendu) =>
        Assert.Equal(attendu, ListeDePostes.MemePoste(a, b));
}
