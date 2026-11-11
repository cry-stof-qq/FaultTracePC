using System.Net;
using System.Text.Json;
using FaultTracePC.Core;
using FaultTracePC.Core.Analysis;
using FaultTracePC.Core.Report;
using Xunit;

namespace FaultTracePC.Tests;

/// <summary>
/// Point 120, (a) — go de l'auteur le 11/11/2026. Le poste AMD de l'auteur affichait
/// jusqu'à 100,4 °C, au-delà des 95 °C annoncés par AMD, sans baisser sa fréquence :
/// impossible de dire quelle sonde parlait. Le service note désormais le nom des sondes
/// de température au démarrage, et, dans chaque relevé à 80 °C ou plus, le nom de celle
/// qui a donné le maximum.
/// </summary>
[Collection("Langue")]
public class SondesTemperatureTests
{
    private static T EnFrancais<T>(Func<T> action)
    {
        var initial = Lang.Current;
        try { Lang.Apply(AppLanguage.French); return action(); }
        finally { Lang.Apply(initial); }
    }

    [Fact]
    public void Le_maximum_est_garde_et_sa_sonde_nommee_a_partir_de_80()
    {
        var chaud = CapteursProcesseur.Temperature(new (string, double?)[]
        {
            ("Sonde A", 71.04), ("Sonde B", 100.42), ("Sonde C", 88.0),
        });
        Assert.Equal(100.4, chaud.Valeur);
        Assert.Equal("Sonde B", chaud.Sonde);

        var tiede = CapteursProcesseur.Temperature(new (string, double?)[] { ("Sonde A", 51.2), ("Sonde B", 79.9) });
        Assert.Equal(79.9, tiede.Valeur);
        Assert.Null(tiede.Sonde); // en dessous de 80 °C, le nom n'est pas écrit

        Assert.Equal("Sonde A", CapteursProcesseur.Temperature(new (string, double?)[] { ("Sonde A", 80) }).Sonde);
    }

    [Fact]
    public void Meme_filtre_qu_avant_une_sonde_qui_decroche_annule_la_mesure()
    {
        // Comportement d'avant cet ajout, conservé : le maximum est pris, PUIS filtré.
        Assert.Equal((null, null), CapteursProcesseur.Temperature(new (string, double?)[] { ("A", 255), ("B", 60) }));
        Assert.Equal((null, null), CapteursProcesseur.Temperature(new (string, double?)[] { ("A", 0.5) }));
        Assert.Equal((null, null), CapteursProcesseur.Temperature(new (string, double?)[] { ("A", null), ("B", double.NaN) }));
        Assert.Equal((null, null), CapteursProcesseur.Temperature(Array.Empty<(string, double?)>()));
        Assert.Equal(60.0, CapteursProcesseur.Temperature(new (string, double?)[] { ("A", double.NaN), ("B", 60) }).Valeur);
    }

    private static readonly IReadOnlyList<(string, IReadOnlyList<string>, IReadOnlyList<string>, IReadOnlyList<string>)> Processeur = new[]
    {
        ("Processeur de test", (IReadOnlyList<string>)new[] { "CPU Total" },
         (IReadOnlyList<string>)new[] { "Core #1" }, (IReadOnlyList<string>)new[] { "Package" }),
    };

    [Fact]
    public void Les_noms_des_sondes_de_temperature_s_ecrivent_et_se_relisent()
    {
        var brut = DiagnosticCapteurs.EncoderComplet(null, Processeur, new List<string>(), "Running", new[] { "Sonde A", "Sonde B" });

        Assert.EndsWith("|puiss=Package|temp=Sonde A,Sonde B|pawnio=Running", brut);
        Assert.Equal(new[] { "Sonde A", "Sonde B" }, DiagnosticCapteurs.SondesTemperature(brut)!.ToArray());
        Assert.Equal("Running", DiagnosticCapteurs.EtatPawnIO(brut));
        Assert.Equal(new[] { "Package" }, DiagnosticCapteurs.Sondes(brut)!.Value.Puissance.ToArray());

        // Aucune sonde : « temp= » vide, relu comme une liste vide.
        Assert.Empty(DiagnosticCapteurs.SondesTemperature(
            DiagnosticCapteurs.EncoderComplet(null, Processeur, new List<string>(), "absent", new string[0]))!);
        // Ancien service : rien d'écrit, rien de relu.
        Assert.DoesNotContain("temp=", DiagnosticCapteurs.EncoderComplet(null, Processeur, new List<string>(), "absent"));
        Assert.Null(DiagnosticCapteurs.SondesTemperature("v1|cpu=Processeur de test|charge=CPU Total|freq=|puiss="));
        Assert.Null(DiagnosticCapteurs.SondesTemperature(null));
    }

    [Fact]
    public void La_note_cite_aussi_les_sondes_de_temperature()
    {
        var f = new FlightInfo
        {
            DiagnosticCapteurs = DiagnosticCapteurs.EncoderComplet(null, Processeur, new List<string>(), "absent", new[] { "Sonde A" }),
        };
        var ctx = new FlightCrashContext
        {
            CrashTime = new DateTime(2026, 11, 11, 12, 10, 0),
            Samples = { new FlightSample { Time = new DateTime(2026, 11, 11, 12, 9, 50), CpuLoad = 100 } },
        };

        var note = WebUtility.HtmlDecode(EnFrancais(() => HtmlReportGenerator.NoteFrequenceAbsente(f, ctx)));

        Assert.Contains("fréquence : Core #1 ; consommation : Package ; température : Sonde A.", note);
    }

    [Fact]
    public void Le_nom_de_la_sonde_s_ecrit_dans_le_journal_sous_la_cle_ctn()
    {
        var json = JsonSerializer.Serialize(new FlightSample { CpuTemp = 100.4, CpuTempSensor = "Sonde B" });
        Assert.Contains("\"ctn\":\"Sonde B\"", json);

        var relu = JsonSerializer.Deserialize<FlightSample>("{\"t\":\"2026-11-08T10:04:48\",\"k\":\"s\",\"ct\":100.4}")!;
        Assert.Null(relu.CpuTempSensor); // journal d'avant cet ajout
    }
}
