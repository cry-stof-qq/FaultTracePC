using FaultTracePC.Core;
using FaultTracePC.Core.Analysis;
using FaultTracePC.Core.Report;
using Xunit;

namespace FaultTracePC.Tests;

/// <summary>
/// POINT 86 — la charge processeur absente de la boîte noire. Cause non établie :
/// ces tests fixent ce que le service ÉCRIT et ce que le rapport DIT, pas une cause.
/// Les noms de processeurs et de sondes sont des données de test.
/// </summary>
[Collection("Langue")]
public class DiagnosticCapteursTests
{
    private static T EnFrancais<T>(Func<T> f)
    {
        var initial = Lang.Current;
        try { Lang.Apply(AppLanguage.French); return f(); }
        finally { Lang.Apply(initial); }
    }

    private static List<(string Nom, IReadOnlyList<string> SondesCharge)> Processeur(string nom, params string[] sondes) =>
        new() { (nom, sondes.ToList()) };

    private static readonly List<(string Nom, IReadOnlyList<string> SondesCharge)> AucunProcesseur = new();

    [Fact]
    public void Bibliotheque_non_ouverte()
    {
        var d = EnFrancais(() => DiagnosticCapteurs.Decrire("Accès refusé", AucunProcesseur, new List<string>()));
        Assert.Equal("bibliothèque de capteurs non ouverte : Accès refusé", d);
    }

    [Fact]
    public void Aucun_processeur_vu_on_nomme_ce_qui_l_a_ete()
    {
        var d = EnFrancais(() => DiagnosticCapteurs.Decrire(null, AucunProcesseur, new List<string> { "GpuAmd (Processeur graphique de test)" }));
        Assert.Contains("aucun processeur vu", d);
        Assert.Contains("GpuAmd (Processeur graphique de test)", d);
    }

    [Fact]
    public void Processeur_sans_la_sonde_lue_par_le_service()
    {
        var d = EnFrancais(() => DiagnosticCapteurs.Decrire(null, Processeur("Processeur de test", "CPU Core #1", "CPU Core #2"), new List<string>()));
        Assert.Contains("processeur « Processeur de test »", d);
        Assert.Contains("CPU Core #1, CPU Core #2", d);
        Assert.Contains("pas de sonde « Total »", d);
    }

    [Fact]
    public void Processeur_avec_la_sonde_rien_a_redire()
    {
        var d = EnFrancais(() => DiagnosticCapteurs.Decrire(null, Processeur("Processeur de test", "CPU Total", "CPU Core #1"), new List<string>()));
        Assert.DoesNotContain("pas de sonde", d);
    }

    private static FlightCrashContext Contexte(double? charge) => new()
    {
        CrashTime = new DateTime(2026, 9, 24, 7, 50, 0),
        Samples = [new FlightSample { Time = new DateTime(2026, 9, 24, 7, 46, 1), Kind = "s", CpuLoad = charge, MemPct = 47 }],
    };

    [Fact]
    public void Le_rapport_dit_que_la_charge_manque_et_pourquoi()
    {
        var f = new FlightInfo { DiagnosticCapteurs = "processeur « Processeur de test » ; aucune sonde de charge" };
        var html = System.Net.WebUtility.HtmlDecode(EnFrancais(() => HtmlReportGenerator.NoteChargeAbsente(f, Contexte(null))));
        Assert.Contains("Charge processeur non mesurée", html);
        Assert.Contains("aucune sonde de charge", html);
    }

    [Fact]
    public void Journal_ancien_la_raison_n_a_pas_ete_enregistree()
    {
        var html = System.Net.WebUtility.HtmlDecode(EnFrancais(() => HtmlReportGenerator.NoteChargeAbsente(new FlightInfo(), Contexte(null))));
        Assert.Contains("antérieur à la version 1.8.0", html);
    }

    [Fact]
    public void Une_charge_mesuree_ne_produit_aucune_note()
    {
        Assert.Equal("", HtmlReportGenerator.NoteChargeAbsente(new FlightInfo(), Contexte(12.5)));
    }
}
