using FaultTracePC.Core;
using FaultTracePC.Core.Collectors;
using FaultTracePC.Core.Report;
using Xunit;

namespace FaultTracePC.Tests;

/// <summary>
/// POINT 85, LOT 2b — la pile des pilotes-filtres lue dans « fltmc filters ».
///
/// SORTIE RÉELLE : copiée le 10/10/2026 sur le poste de l'auteur (Windows en
/// français), et comparée à la carte de son rapport du même jour — quatorze
/// filtres, tous Microsoft, mêmes altitudes. Elle remplace une sortie reconstituée
/// de la première version de ce test. À noter : l'en-tête « Nom. d’instn. » porte
/// une apostrophe typographique, et une altitude peut avoir une décimale
/// (« 385250.5 »).
/// </summary>
[Collection("Langue")]
public class FiltresDisqueTests
{
    private const string Sortie = """
        Nom de filtre                   Nom. d’instn.  Altitude      Cadre
        ------------------------------  -------------  ------------  -----
        bindflt                                 1       409800         0
        FsDepends                               4       407000         0
        UCPD                                    5       385250.5       0
        WdFilter                                5       328010         0
        storqosflt                              1       244000         0
        wcifs                                   1       189900         0
        CldFlt                                  1       180451         0
        bfs                                     7       150000         0
        FileCrypt                               0       141100         0
        luafv                                   1       135000         0
        UnionFS                                 0       130850         0
        npsvctrig                               1        46000         0
        Wof                                     2        40700         0
        FileInfo                                5        40500         0
        """;

    [Fact]
    public void Les_lignes_apres_le_trait_sont_lues_quels_que_soient_les_en_tetes()
    {
        var filtres = FiltreCollector.Lire(Sortie);
        Assert.NotNull(filtres);
        Assert.Equal(14, filtres.Count);
        Assert.Equal("WdFilter", filtres[3].Nom);
        Assert.Equal(5, filtres[3].Instances);
        Assert.Equal("328010", filtres[3].Altitude);
        Assert.Equal("385250.5", filtres[2].Altitude);
        Assert.Equal(0, filtres[8].Instances);

        // Mêmes données, en-têtes dans une autre langue : même résultat.
        var autreLangue = Sortie.Replace("Nom de filtre                   Nom. d’instn.  Altitude      Cadre",
                                         "Filter Name                     Num Instances    Altitude    Frame");
        Assert.Equal(14, FiltreCollector.Lire(autreLangue)!.Count);
    }

    [Fact]
    public void Une_sortie_sans_trait_n_est_pas_une_pile_vide()
    {
        // Ex. droits insuffisants : un message, pas de tableau.
        Assert.Null(FiltreCollector.Lire("Echec de l'appel. Acces refuse."));
    }

    [Fact]
    public void Les_filtres_d_un_autre_editeur_que_Microsoft_sont_en_gras()
    {
        var s = new SystemSnapshot { FiltresLus = true, Filtres = FiltreCollector.Lire(Sortie)! };
        s.Drivers.Add(new DriverInfo { Name = "WdFilter", CompanyName = "Microsoft Corporation", IsMicrosoft = true });
        // Donnée de test : sur le poste réel, tous les filtres sont Microsoft.
        s.Drivers.Add(new DriverInfo { Name = "FileCrypt", CompanyName = "Editeur tiers de test" });

        var initial = Lang.Current;
        string html;
        try { Lang.Apply(AppLanguage.French); html = System.Net.WebUtility.HtmlDecode(HtmlReportGenerator.LignesFiltres(s)); }
        finally { Lang.Apply(initial); }

        Assert.Contains("WdFilter — altitude 328010 — Microsoft Corporation", html);
        Assert.DoesNotContain("<strong>WdFilter", html);
        Assert.Contains("<strong>FileCrypt — altitude 141100 — Editeur tiers de test</strong>", html);
        // Éditeur inconnu : en gras aussi, on ne présume pas qu'il est sain.
        Assert.Contains("<strong>bindflt — altitude 409800 — éditeur inconnu</strong>", html);
    }

    [Fact]
    public void Non_lu_n_est_pas_aucun()
    {
        var initial = Lang.Current;
        try
        {
            Lang.Apply(AppLanguage.French);
            Assert.Equal("non lus", HtmlReportGenerator.LignesFiltres(new SystemSnapshot()));
            Assert.Equal("aucun", HtmlReportGenerator.LignesFiltres(new SystemSnapshot { FiltresLus = true }));
        }
        finally { Lang.Apply(initial); }
    }
}
