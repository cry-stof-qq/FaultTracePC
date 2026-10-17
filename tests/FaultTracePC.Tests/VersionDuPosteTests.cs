using FaultTracePC.Core;
using Xunit;

namespace FaultTracePC.Tests;

/// <summary>Point 100 : colonne « Version » de la Supervision, forme courte.</summary>
[Collection("Langue")]
public class VersionDuPosteTests
{
    private static string EnFrancais(bool joignable, string? poste, string console)
    {
        var initial = Lang.Current;
        try { Lang.Apply(AppLanguage.French); return VersionDuPoste.Decrire(joignable, poste, console); }
        finally { Lang.Apply(initial); }
    }

    [Fact]
    public void Poste_en_retard_dit_vers_quelle_version() =>
        Assert.Equal("⬆ 1.8.0 → 1.9.0", EnFrancais(true, "1.8.0", "1.9.0"));

    [Fact]
    public void Console_en_retard_le_dit() =>
        Assert.Equal("⚠ 1.9.1, console 1.9.0", EnFrancais(true, "1.9.1", "1.9.0"));

    [Fact]
    public void Meme_version_rien_de_plus() =>
        Assert.Equal("1.9.0", EnFrancais(true, "1.9.0", "1.9.0"));

    [Fact]
    public void Poste_injoignable_colonne_vide() =>
        Assert.Equal("", EnFrancais(false, "1.8.0", "1.9.0"));

    [Fact]
    public void Poste_trop_ancien_pour_donner_sa_version() =>
        Assert.Equal("⬆ antérieure à 1.2.2", EnFrancais(true, "", "1.9.0"));

    [Fact]
    public void La_forme_courte_tient_dans_la_colonne()
    {
        // Le texte qui débordait faisait 25 caractères (« ⬆ 1.8.0 — à mettre à jour »).
        Assert.True(EnFrancais(true, "1.10.12", "1.11.0").Length <= 20);
        Assert.True(EnFrancais(true, "1.11.0", "1.10.12").Length <= 25);
    }
}
