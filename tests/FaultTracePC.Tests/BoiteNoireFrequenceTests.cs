using System.Net;
using FaultTracePC.Core;
using FaultTracePC.Core.Analysis;
using FaultTracePC.Core.Report;
using Xunit;

namespace FaultTracePC.Tests;

/// <summary>
/// Point 120, lot B : la fréquence et la consommation du processeur dans les dernières
/// secondes avant un incident, et une note quand elles manquent — un tiret n'est pas un zéro.
/// </summary>
[Collection("Langue")]
public class BoiteNoireFrequenceTests
{
    private static T EnLangue<T>(AppLanguage langue, Func<T> action)
    {
        var initial = Lang.Current;
        try { Lang.Apply(langue); return action(); }
        finally { Lang.Apply(initial); }
    }

    private static T EnFrancais<T>(Func<T> action) => EnLangue(AppLanguage.French, action);

    private static FlightCrashContext Contexte(double? frequence, double? puissance) => new()
    {
        CrashTime = new DateTime(2026, 11, 7, 14, 33, 0),
        Samples =
        {
            new FlightSample { Time = new DateTime(2026, 11, 7, 14, 32, 40), CpuLoad = 97, CpuTemp = 98, CpuClockMHz = frequence, CpuPowerW = puissance },
            new FlightSample { Time = new DateTime(2026, 11, 7, 14, 32, 50), CpuLoad = 98, CpuTemp = 99, CpuClockMHz = frequence, CpuPowerW = puissance },
        },
    };

    [Fact]
    public void La_frequence_s_affiche_en_GHz()
    {
        Assert.Equal("4,21 GHz", EnFrancais(() => HtmlReportGenerator.FmtGHz(4213)));
        Assert.Equal("4.21 GHz", EnLangue(AppLanguage.English, () => HtmlReportGenerator.FmtGHz(4213)));
        Assert.Equal("—", HtmlReportGenerator.FmtGHz(null));
    }

    [Fact]
    public void Le_tableau_avant_l_incident_porte_frequence_et_puissance()
    {
        var r = new DiagnosticReport
        {
            ScanPeriodDays = 30,
            System = new SystemSnapshot { MachineName = "POSTE-TEST" },
            Flight = new FlightInfo { JournalFound = true, Contexts = { Contexte(1100, 15.2) } },
        };

        var html = WebUtility.HtmlDecode(EnFrancais(() => HtmlReportGenerator.Generate(r)));

        Assert.Contains("<th>Fréquence CPU</th><th>Puissance CPU</th>", html);
        // La puissance suit le format des autres colonnes (culture de la machine) : on
        // vérifie l'unité, pas le séparateur décimal.
        Assert.Contains("<td>1,10 GHz</td><td>15", html);
        Assert.Contains("2 W</td>", html);
        Assert.DoesNotContain("non mesurée", html);
    }

    [Fact]
    public void Mesurees_toutes_les_deux_aucune_note()
    {
        Assert.Equal("", HtmlReportGenerator.NoteFrequenceAbsente(new FlightInfo(), Contexte(4213, 27.5)));
    }

    [Fact]
    public void Un_ancien_journal_dit_que_la_1_11_0_est_la_premiere_a_les_enregistrer()
    {
        var f = new FlightInfo { DiagnosticCapteurs = "v1|cpu=Processeur de test|charge=CPU Total" };

        var note = WebUtility.HtmlDecode(EnFrancais(() => HtmlReportGenerator.NoteFrequenceAbsente(f, Contexte(null, null))));

        Assert.Contains("Fréquence et consommation du processeur non mesurées", note);
        Assert.Contains("un tiret n'est pas un zéro", note);
        Assert.Contains("1.11.0", note);
        Assert.DoesNotContain("Capteurs trouvés", note);
    }

    [Fact]
    public void Sans_capteur_la_note_dit_ce_que_le_service_a_trouve()
    {
        var f = new FlightInfo { DiagnosticCapteurs = "v1|cpu=Processeur virtuel|charge=CPU Total|freq=|puiss=" };

        var note = WebUtility.HtmlDecode(EnFrancais(() => HtmlReportGenerator.NoteFrequenceAbsente(f, Contexte(null, null))));

        Assert.Contains("Capteurs trouvés par le service à son dernier démarrage — fréquence : aucun ; consommation : aucun.", note);
    }

    [Fact]
    public void Seule_la_consommation_manque()
    {
        var f = new FlightInfo { DiagnosticCapteurs = "v1|cpu=Processeur de test|charge=CPU Total|freq=Core #1,Bus Speed|puiss=" };

        var note = WebUtility.HtmlDecode(EnFrancais(() => HtmlReportGenerator.NoteFrequenceAbsente(f, Contexte(3400, null))));

        Assert.Contains("Consommation du processeur non mesurée", note);
        Assert.Contains("fréquence : Core #1, Bus Speed ; consommation : aucun.", note);
    }

    [Fact]
    public void Les_sondes_se_relisent_depuis_la_ligne_de_debut_de_session()
    {
        var s = DiagnosticCapteurs.Sondes("v1|cpu=Processeur de test|charge=CPU Total|freq=Core #1,Core #2|puiss=Package");
        Assert.NotNull(s);
        Assert.Equal(new[] { "Core #1", "Core #2" }, s!.Value.Frequence.ToArray());
        Assert.Equal(new[] { "Package" }, s.Value.Puissance.ToArray());

        Assert.Null(DiagnosticCapteurs.Sondes("v1|cpu=Processeur de test|charge=CPU Total")); // service antérieur
        Assert.Null(DiagnosticCapteurs.Sondes(null));
    }

    [Fact]
    public void La_note_existe_en_anglais()
    {
        var note = EnLangue(AppLanguage.English, () => HtmlReportGenerator.NoteFrequenceAbsente(new FlightInfo(), Contexte(null, 20)));
        Assert.Contains("<strong>Processor frequency not measured</strong>: a dash is not a zero.", note);
    }
}
