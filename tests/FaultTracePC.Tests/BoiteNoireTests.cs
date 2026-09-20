using FaultTracePC.Core;
using FaultTracePC.Core.Collectors;
using Xunit;

namespace FaultTracePC.Tests;

/// <summary>
/// Point 62 de la feuille de route. Deux défauts constatés le 17/09/2026 sur le
/// rapport de POSTE-TEMOIN : le MÊME incident affiché trois fois sous des titres à une
/// minute d'écart, et un tableau « dernières secondes avant l'incident de 12 h 29 »
/// qui s'arrêtait à 12 h 27 min 26 s sans que rien ne le signale.
/// </summary>
public class BoiteNoireTests
{
    [Fact]
    public void Un_seul_plantage_vu_par_trois_sources_ne_fait_qu_un_incident()
    {
        // L'en-tête du vidage, l'événement Kernel-Power et la date du fichier ne
        // donnent pas la même seconde pour le même arrêt.
        var t = new DateTime(2026, 9, 17, 12, 28, 0);
        var instants = new[] { t, t.AddSeconds(40), t.AddSeconds(75) };

        var groupes = FlightJournalCollector.Regrouper(instants);

        Assert.Single(groupes);
        // On retient le plus tardif : sa fenêtre d'échantillons est la plus complète.
        Assert.Equal(t.AddSeconds(75), groupes[0]);
    }

    [Fact]
    public void Deux_plantages_reellement_distincts_restent_distincts()
    {
        var t = new DateTime(2026, 9, 14, 8, 48, 0);
        var instants = new[] { t, t.AddMinutes(29) };

        var groupes = FlightJournalCollector.Regrouper(instants);

        Assert.Equal(2, groupes.Count);
    }

    [Fact]
    public void Le_silence_qui_precede_l_incident_est_mesure()
    {
        var crash = new DateTime(2026, 9, 17, 12, 29, 0);
        var ctx = new FlightCrashContext
        {
            CrashTime = crash,
            Samples =
            [
                new FlightSample { Time = crash.AddSeconds(-104), Kind = "s" },
                new FlightSample { Time = crash.AddSeconds(-94), Kind = "s" },
            ],
        };

        // 94 secondes sans le moindre relevé : ce n'est pas une fin de tableau,
        // c'est l'échantillonneur qui a cessé de répondre avec la machine.
        Assert.Equal(94, ctx.SilenceBefore!.Value.TotalSeconds);
    }

    [Fact]
    public void Sans_echantillon_aucun_silence_n_est_annonce()
    {
        var ctx = new FlightCrashContext { CrashTime = DateTime.Now };
        Assert.Null(ctx.SilenceBefore);
    }
}
