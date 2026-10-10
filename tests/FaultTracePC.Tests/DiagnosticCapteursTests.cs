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

    [Fact]
    public void Le_service_ecrit_sans_langue_le_rapport_lit_dans_la_sienne()
    {
        // Constaté le 10/10/2026 : le service, sous SYSTEM, écrivait en anglais.
        var brut = DiagnosticCapteurs.Encoder(null, Processeur("Processeur de test", "CPU Core #1", "CPU Total"), new List<string>());
        Assert.Equal("v1|cpu=Processeur de test|charge=CPU Core #1,CPU Total", brut);

        Assert.Equal(EnFrancais(() => DiagnosticCapteurs.Decrire(null, Processeur("Processeur de test", "CPU Core #1", "CPU Total"), new List<string>())),
                     EnFrancais(() => DiagnosticCapteurs.Lire(brut)));

        var initial = Lang.Current;
        try
        {
            Lang.Apply(AppLanguage.English);
            Assert.StartsWith("processor “Processeur de test”; load sensors: CPU Core #1, CPU Total", DiagnosticCapteurs.Lire(brut));
        }
        finally { Lang.Apply(initial); }
    }

    [Fact]
    public void Les_trois_situations_survivent_a_l_aller_retour()
    {
        Assert.Contains("non ouverte : Accès refusé",
            EnFrancais(() => DiagnosticCapteurs.Lire(DiagnosticCapteurs.Encoder("Accès refusé", AucunProcesseur, new List<string>()))));
        Assert.Contains("GpuAmd (Carte de test)",
            EnFrancais(() => DiagnosticCapteurs.Lire(DiagnosticCapteurs.Encoder(null, AucunProcesseur, new List<string> { "GpuAmd (Carte de test)" }))));
        Assert.Contains("pas de sonde « Total »",
            EnFrancais(() => DiagnosticCapteurs.Lire(DiagnosticCapteurs.Encoder(null, Processeur("Processeur de test", "CPU Core #1"), new List<string>()))));
    }

    [Fact]
    public void Un_texte_d_une_version_de_developpement_est_rendu_tel_quel()
    {
        Assert.Equal("texte libre", DiagnosticCapteurs.Lire("texte libre"));
    }
}
