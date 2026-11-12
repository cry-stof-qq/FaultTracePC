using FaultTracePC.Core;
using Xunit;

namespace FaultTracePC.Tests;

/// <summary>Point 121 : l'ordre des colonnes de la console de parc, valeurs relevées le 12/11/2026.</summary>
public class TriColonnesTests
{
    private static List<object?> Trier(bool decroissant, params object?[] valeurs) =>
        valeurs.OrderBy(v => v, Comparer<object?>.Create((a, b) => TriColonnes.Comparer(a, b, decroissant))).ToList();

    [Fact]
    public void Les_noms_de_postes_suivent_l_ordre_naturel()
    {
        Assert.Equal(new object?[] { "POSTE-2", "POSTE-10", "poste-11", "POSTE-100" },
                     Trier(false, "POSTE-100", "poste-11", "POSTE-2", "POSTE-10"));
    }

    [Fact]
    public void Les_colonnes_chiffrees_se_trient_comme_des_nombres_et_les_vides_restent_en_bas()
    {
        // Colonne CPU % de la console, avec une case vide.
        Assert.Equal(new object?[] { "0,5", "2,1", "9,9", "68,7", "98,8", "" },
                     Trier(false, "98,8", "", "2,1", "68,7", "0,5", "9,9"));
        // Décroissant : les vides restent en bas.
        Assert.Equal(new object?[] { "98,8", "68,7", "9,9", "2,1", "0,5", "" },
                     Trier(true, "98,8", "", "2,1", "68,7", "0,5", "9,9"));
    }

    [Fact]
    public void Temperatures_et_alertes_se_trient_sur_leur_nombre()
    {
        Assert.Equal(new object?[] { "40 °C", "42 °C", "51,6 °C", "57 °C", "" },
                     Trier(false, "57 °C", "", "51,6 °C", "40 °C", "42 °C"));
        Assert.Equal(new object?[] { "61 (37 ⛔)", "48 (14 ⛔)", "13 (1 ⛔)", "5", "1", "—" },
                     Trier(true, "1", "—", "48 (14 ⛔)", "5", "61 (37 ⛔)", "13 (1 ⛔)"));
    }

    [Fact]
    public void Les_dates_typees_se_comparent_comme_des_dates()
    {
        var d1 = new DateTime(2026, 10, 31, 8, 0, 0);
        var d2 = new DateTime(2026, 11, 12, 8, 27, 18);
        Assert.Equal(new object?[] { d1, d2, null }, Trier(false, d2, null, d1));
        Assert.Equal(new object?[] { d2, d1, null }, Trier(true, d1, null, d2));
    }

    [Fact]
    public void Les_adresses_se_trient_numero_par_numero()
    {
        Assert.Equal(new object?[] { "10.129.2.28:58620", "10.129.3.232:58620", "10.129.9.1:58620" },
                     Trier(false, "10.129.9.1:58620", "10.129.2.28:58620", "10.129.3.232:58620"));
    }
}
