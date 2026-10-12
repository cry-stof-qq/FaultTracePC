using FaultTracePC.Core;
using FaultTracePC.Core.Analysis;
using FaultTracePC.Core.Report;
using Xunit;

namespace FaultTracePC.Tests;

/// <summary>
/// Point 97 de la feuille de route, constaté le 12/10/2026 sur un rapport du parc
/// réel : la comparaison portait sur 3 postes sur 10 sans le dire, et un poste
/// jamais analysé était compté parmi les « résumés produits par une version
/// antérieure ».
/// </summary>
[Collection("Langue")]
public class PerimetreComparaisonTests
{
    private static T EnFrancais<T>(Func<T> f)
    {
        var initial = Lang.Current;
        try { Lang.Apply(AppLanguage.French); return f(); }
        finally { Lang.Apply(initial); }
    }

    [Fact]
    public void Un_poste_jamais_analyse_n_est_pas_pris_pour_une_version_anterieure()
    {
        // Ce que répond réellement le service d'un poste jamais analysé.
        var (resume, motif) = ParkComparator.LireReponse("""{"available":false,"reason":"Cette machine n'a jamais été analysée."}""");
        Assert.Null(resume);
        Assert.Equal(ParkComparator.MotifAbsence.JamaisAnalyse, motif);
    }

    [Fact]
    public void Un_resume_sans_tampon_de_format_est_incompatible()
    {
        var (resume, motif) = ParkComparator.LireReponse("""{"GeneratedAt":"2026-01-01T10:00:00"}""");
        Assert.Null(resume);
        Assert.Equal(ParkComparator.MotifAbsence.FormatIncompatible, motif);
    }

    [Fact]
    public void Un_resume_au_format_actuel_est_lu()
    {
        var (resume, motif) = ParkComparator.LireReponse($$"""{"format":{{ScanHistory.FormatActuel}},"GeneratedAt":"2026-10-12T10:00:00"}""");
        Assert.NotNull(resume);
        Assert.Null(motif);
    }

    [Theory]
    [InlineData("pas du json")]
    [InlineData("[1,2,3]")]
    public void Une_reponse_illisible_est_dite_illisible(string json) =>
        Assert.Equal(ParkComparator.MotifAbsence.Illisible, ParkComparator.LireReponse(json).Motif);

    [Fact]
    public void Le_perimetre_nomme_les_postes_compares_et_les_autres_avec_leur_raison()
    {
        var postes = new[] { "POSTE-B", "POSTE-A", "POSTE-C" }
            .Select(n => new ParkComparator.MachineSummary(n, new ScanHistory.ScanSummary { GeneratedAt = DateTime.Now }))
            .ToList();
        var a = ParkComparator.Analyze(postes);
        a.NonCompares.Add(new("POSTE-E", ParkComparator.MotifAbsence.Injoignable));
        a.NonCompares.Add(new("POSTE-D", ParkComparator.MotifAbsence.JamaisAnalyse));

        Assert.Equal(new[] { "POSTE-A", "POSTE-B", "POSTE-C" }, a.Compares);

        var texte = EnFrancais(() => ParkComparator.Perimetre(a));
        Assert.Contains("3 poste(s) sur 5", texte);
        Assert.Contains("POSTE-A, POSTE-B, POSTE-C", texte);
        Assert.Contains("POSTE-D (jamais analysé", texte);
        Assert.Contains("POSTE-E (injoignable)", texte);
        Assert.Contains("seulement les postes comparés", texte);
    }

    [Fact]
    public void Tout_le_parc_compare_pas_de_liste_des_absents()
    {
        var postes = new[] { "POSTE-A", "POSTE-B" }
            .Select(n => new ParkComparator.MachineSummary(n, new ScanHistory.ScanSummary { GeneratedAt = DateTime.Now }))
            .ToList();
        var texte = EnFrancais(() => ParkComparator.Perimetre(ParkComparator.Analyze(postes)));
        Assert.Contains("2 poste(s) sur 2", texte);
        Assert.DoesNotContain("Non comparés", texte);
    }

    [Fact]
    public void Chaque_motif_a_son_texte()
    {
        foreach (var motif in Enum.GetValues<ParkComparator.MotifAbsence>())
            Assert.False(string.IsNullOrWhiteSpace(EnFrancais(() => ParkComparator.TexteMotif(motif))));
    }
}
