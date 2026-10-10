using FaultTracePC.Core;
using FaultTracePC.Core.Collectors;
using FaultTracePC.Core.Report;
using Xunit;

namespace FaultTracePC.Tests;

/// <summary>
/// POINT 85, LOT 2b — la pile des pilotes-filtres lue dans « fltmc filters ».
///
/// SORTIE RECONSTITUÉE, PAS CAPTURÉE : la disposition (en-têtes, trait de tirets,
/// colonnes nom / instances / altitude / cadre) est celle de fltmc ; les en-têtes
/// varient avec la langue de Windows et ne servent pas à la lecture. À remplacer
/// par une sortie réelle dès qu'on en a une.
/// </summary>
[Collection("Langue")]
public class FiltresDisqueTests
{
    private const string Sortie = """

        Filter Name                     Num Instances    Altitude    Frame
        ------------------------------  -------------  ------------  -----
        bindflt                                 1       409800         0
        WdFilter                                5       328010         0
        aswSP                                   5       388400         0
        FileInfo                                5        40500         0

        """;

    [Fact]
    public void Les_lignes_apres_le_trait_sont_lues_quels_que_soient_les_en_tetes()
    {
        var filtres = FiltreCollector.Lire(Sortie);
        Assert.NotNull(filtres);
        Assert.Equal(4, filtres.Count);
        Assert.Equal("WdFilter", filtres[1].Nom);
        Assert.Equal(5, filtres[1].Instances);
        Assert.Equal("328010", filtres[1].Altitude);

        // Mêmes données, en-têtes dans une autre langue : même résultat.
        var autreLangue = Sortie.Replace("Filter Name                     Num Instances    Altitude    Frame",
                                         "Nom du filtre                   Instances        Altitude    Cadre");
        Assert.Equal(4, FiltreCollector.Lire(autreLangue)!.Count);
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
        s.Drivers.Add(new DriverInfo { Name = "aswSP", CompanyName = "Gen Digital Inc." });

        var initial = Lang.Current;
        string html;
        try { Lang.Apply(AppLanguage.French); html = System.Net.WebUtility.HtmlDecode(HtmlReportGenerator.LignesFiltres(s)); }
        finally { Lang.Apply(initial); }

        Assert.Contains("WdFilter — altitude 328010 — Microsoft Corporation", html);
        Assert.DoesNotContain("<strong>WdFilter", html);
        Assert.Contains("<strong>aswSP — altitude 388400 — Gen Digital Inc.</strong>", html);
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
