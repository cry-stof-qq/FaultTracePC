using FaultTracePC.Core;
using FaultTracePC.Core.Analysis;
using Xunit;

namespace FaultTracePC.Tests;

/// <summary>
/// Point 125 : comment le poste s'est arrêté, comment il a redémarré. Relevés RÉELS d'un
/// poste du parc qui ne se rallumait plus (lus par l'auteur le 12/11/2026, nom du poste
/// et compte remplacés).
/// </summary>
public class ArretsEtDemarragesTests
{
    private static EvenementAlimentation E(string quand, string source, int id, int? type = null,
                                          string? processus = null, string? action = null) => new()
    {
        Heure = DateTime.Parse(quand, System.Globalization.CultureInfo.InvariantCulture),
        Source = source, Id = id, TypeDemarrage = type, Processus = processus, Action = action,
        Compte = processus is null ? null : @"DOMAINE\admin",
    };

    private const string General = "Microsoft-Windows-Kernel-General";
    private const string Boot = "Microsoft-Windows-Kernel-Boot";
    private const string Power = "Microsoft-Windows-Kernel-Power";

    /// <summary>Le poste A, du 21/10 au 12/11/2026, tel que Windows l'a écrit.</summary>
    private static readonly EvenementAlimentation[] PosteA =
    {
        E("2026-10-21 16:15:51", General, 12), E("2026-10-21 16:15:51", Boot, 27, type: 0), E("2026-10-21 16:16:10", "EventLog", 6005),
        E("2026-10-21 20:30:01", "User32", 1074, processus: @"C:\Windows\System32\shutdown.exe (POSTE-A)", action: "Redémarrer"),
        E("2026-10-21 20:31:03", "EventLog", 6006), E("2026-10-21 20:31:08", General, 13),
        E("2026-10-21 20:31:26", General, 12), E("2026-10-21 20:31:26", Boot, 27, type: 0), E("2026-10-21 20:31:46", "EventLog", 6005),
        E("2026-10-21 21:00:02", "User32", 1074, processus: @"C:\Windows\System32\shutdown.exe (POSTE-A)", action: "s’arrêter."),
        E("2026-10-21 21:00:02", "EventLog", 6006), E("2026-10-21 21:00:05", General, 13),
        E("2026-11-12 08:02:06", General, 12), E("2026-11-12 08:02:06", Boot, 27, type: 0), E("2026-11-12 08:02:27", "EventLog", 6005),
    };

    [Fact]
    public void Le_poste_qui_ne_se_rallumait_plus_22_jours_eteint_apres_un_arret_programme()
    {
        var p = ArretsEtDemarrages.Periodes(PosteA);

        Assert.Equal(3, p.Count);
        Assert.Equal(FinDeSession.Inconnue, p[0].FinDeSession); // début du relevé

        Assert.Equal((TimeSpan?)TimeSpan.FromSeconds(18), p[1].Duree);   // le redémarrage programmé de 20:30
        Assert.Equal("Redémarrer", p[1].Action);

        var longue = p[2];
        Assert.Equal((DateTime?)DateTime.Parse("2026-10-21 21:00:05", System.Globalization.CultureInfo.InvariantCulture), longue.Debut);
        Assert.Equal(DateTime.Parse("2026-11-12 08:02:06", System.Globalization.CultureInfo.InvariantCulture), longue.Fin);
        Assert.True(longue.Duree > TimeSpan.FromDays(21) && longue.Duree < TimeSpan.FromDays(22));
        Assert.Equal(FinDeSession.ArretDemande, longue.FinDeSession);
        Assert.Equal(@"C:\Windows\System32\shutdown.exe (POSTE-A)", longue.Processus);
        Assert.Equal("s’arrêter.", longue.Action);
        Assert.Equal(@"DOMAINE\admin", longue.Compte);
        Assert.Equal(Reprise.DemarrageAFroid, longue.Reprise);
    }

    [Fact]
    public void Un_redemarrage_sans_arret_propre_est_inattendu()
    {
        var p = ArretsEtDemarrages.Periodes(new[]
        {
            E("2026-11-09 13:04:00", General, 12), E("2026-11-09 13:04:00", Boot, 27, type: 0),
            E("2026-11-09 16:30:10", General, 12), E("2026-11-09 16:30:12", Boot, 27, type: 0),
            E("2026-11-09 16:30:20", Power, 41), E("2026-11-09 16:30:25", "EventLog", 6008),
        });

        var derniere = p[^1];
        Assert.Equal(FinDeSession.Inattendue, derniere.FinDeSession);
        Assert.Null(derniere.Processus);
        Assert.Equal(Reprise.DemarrageAFroid, derniere.Reprise);
    }

    [Fact]
    public void Arret_sans_demande_lue_et_demarrage_rapide()
    {
        var p = ArretsEtDemarrages.Periodes(new[]
        {
            E("2026-11-01 08:00:00", General, 12),
            E("2026-11-01 18:00:00", General, 13),
            E("2026-11-02 08:00:00", General, 12), E("2026-11-02 08:00:01", Boot, 27, type: 1),
        });

        Assert.Equal(FinDeSession.ArretSansDemandeLue, p[^1].FinDeSession);
        Assert.Equal(Reprise.DemarrageRapide, p[^1].Reprise);
        Assert.Equal((TimeSpan?)TimeSpan.FromHours(14), p[^1].Duree);
    }

    [Fact]
    public void Une_mise_en_veille_est_une_periode_sans_nouveau_demarrage()
    {
        var p = ArretsEtDemarrages.Periodes(new[]
        {
            E("2026-11-03 08:00:00", General, 12),
            E("2026-11-03 12:00:00", Power, 42), E("2026-11-03 13:30:00", Power, 107),
        });

        var veille = Assert.Single(p, x => x.FinDeSession == FinDeSession.Veille);
        Assert.Equal((TimeSpan?)TimeSpan.FromMinutes(90), veille.Duree);
        Assert.Equal(Reprise.SortieDeVeille, veille.Reprise);
    }
}
