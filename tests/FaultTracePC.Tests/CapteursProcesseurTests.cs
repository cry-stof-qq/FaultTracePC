using System.Text.Json;
using FaultTracePC.Core;
using FaultTracePC.Core.Analysis;
using Xunit;

namespace FaultTracePC.Tests;

/// <summary>
/// Point 120, lot A : fréquence et consommation du processeur dans la boîte noire.
/// Les noms de sondes ci-dessous sont des EXEMPLES de forme ; les vrais noms, qui
/// varient selon la marque du processeur, seront relevés sur de vraies machines
/// (ligne de début de session du journal).
/// </summary>
[Collection("Langue")]
public class CapteursProcesseurTests
{
    [Fact]
    public void La_frequence_retenue_est_celle_du_coeur_le_plus_rapide()
    {
        var f = CapteursProcesseur.Frequence(new (string, double?)[]
        {
            ("Core #1", 3400.4), ("Core #2", 4212.7), ("Core #3", 1100),
        });
        Assert.Equal((double?)4213, f);
    }

    [Fact]
    public void La_frequence_du_bus_n_est_jamais_prise_pour_celle_d_un_coeur()
    {
        Assert.Null(CapteursProcesseur.Frequence(new (string, double?)[] { ("Bus Speed", 100) }));
        Assert.Equal((double?)2800, CapteursProcesseur.Frequence(new (string, double?)[] { ("Bus Speed", 100), ("Core #1", 2800) }));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-5.0)]
    [InlineData(7500.0)]
    [InlineData(double.NaN)]
    public void Une_frequence_invraisemblable_devient_absente_jamais_zero(double valeur)
    {
        Assert.Null(CapteursProcesseur.Frequence(new (string, double?)[] { ("Core #1", valeur) }));
    }

    [Fact]
    public void Sans_sonde_d_horloge_la_frequence_est_absente()
    {
        Assert.Null(CapteursProcesseur.Frequence(Array.Empty<(string, double?)>()));
        Assert.Null(CapteursProcesseur.Frequence(new (string, double?)[] { ("Core #1", null) }));
    }

    [Fact]
    public void La_consommation_retenue_est_celle_du_processeur_entier()
    {
        var p = CapteursProcesseur.Puissance(new (string, double?)[]
        {
            ("Core #1", 3.2), ("Package", 27.46), ("Core #2", 4.1),
        });
        Assert.Equal((double?)27.5, p);
    }

    [Fact]
    public void Les_consommations_par_coeur_ne_sont_jamais_additionnees()
    {
        Assert.Null(CapteursProcesseur.Puissance(new (string, double?)[] { ("Core #1", 3.2), ("Core #2", 4.1) }));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(650.0)]
    [InlineData(double.NaN)]
    public void Une_consommation_invraisemblable_devient_absente(double valeur)
    {
        Assert.Null(CapteursProcesseur.Puissance(new (string, double?)[] { ("CPU Package", valeur) }));
    }

    [Fact]
    public void Un_ancien_journal_sans_frequence_se_relit_sans_valeur_inventee()
    {
        var ancien = JsonSerializer.Deserialize<FlightSample>("{\"t\":\"2026-10-26T10:00:00\",\"k\":\"s\",\"cpu\":12.5,\"ct\":48}")!;
        Assert.Null(ancien.CpuClockMHz);
        Assert.Null(ancien.CpuPowerW);

        var nouveau = JsonSerializer.Deserialize<FlightSample>(JsonSerializer.Serialize(new FlightSample { CpuClockMHz = 4213, CpuPowerW = 27.5 }))!;
        Assert.Equal((double?)4213, nouveau.CpuClockMHz);
        Assert.Equal((double?)27.5, nouveau.CpuPowerW);
    }

    [Fact]
    public void Le_debut_de_session_porte_le_nom_des_sondes_de_frequence_et_de_puissance()
    {
        var brut = DiagnosticCapteurs.EncoderComplet(null,
            new List<(string, IReadOnlyList<string>, IReadOnlyList<string>, IReadOnlyList<string>)>
            {
                ("Processeur de test", new[] { "CPU Total" }, new[] { "Core #1", "Bus Speed" }, new[] { "Package" }),
            },
            new List<string>());

        Assert.Equal("v1|cpu=Processeur de test|charge=CPU Total|freq=Core #1,Bus Speed|puiss=Package", brut);
    }

    [Fact]
    public void Des_sondes_absentes_s_ecrivent_vides_pour_qu_on_le_sache()
    {
        var brut = DiagnosticCapteurs.EncoderComplet(null,
            new List<(string, IReadOnlyList<string>, IReadOnlyList<string>, IReadOnlyList<string>)>
            {
                ("Processeur virtuel", new[] { "CPU Total" }, Array.Empty<string>(), Array.Empty<string>()),
            },
            new List<string>());

        Assert.Equal("v1|cpu=Processeur virtuel|charge=CPU Total|freq=|puiss=", brut);
    }

    [Fact]
    public void Le_rapport_relit_la_nouvelle_forme_comme_l_ancienne()
    {
        var initial = Lang.Current;
        try
        {
            Lang.Apply(AppLanguage.French);
            var ancienne = DiagnosticCapteurs.Lire("v1|cpu=Processeur de test|charge=CPU Total");
            var nouvelle = DiagnosticCapteurs.Lire("v1|cpu=Processeur de test|charge=CPU Total|freq=Core #1|puiss=Package");
            Assert.Equal(ancienne, nouvelle);
        }
        finally { Lang.Apply(initial); }
    }

    [Fact]
    public void Une_erreur_d_ouverture_s_ecrit_comme_avant()
    {
        Assert.Equal("v1|erreur=Accès refusé", DiagnosticCapteurs.EncoderComplet("Accès refusé",
            new List<(string, IReadOnlyList<string>, IReadOnlyList<string>, IReadOnlyList<string>)>(), new List<string>()));
    }
}
