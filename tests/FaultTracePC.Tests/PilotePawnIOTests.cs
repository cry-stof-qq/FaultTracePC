using System.Net;
using FaultTracePC.Core;
using FaultTracePC.Core.Analysis;
using FaultTracePC.Core.Report;
using Xunit;

namespace FaultTracePC.Tests;

/// <summary>
/// Point 120, décision 1 du 08/11/2026 : dire la VRAIE raison quand la température, la
/// fréquence ou la consommation du processeur manquent. Essai réel sur le poste de
/// l'auteur : sans le pilote PawnIO, les sondes existent mais restent vides ; avec lui,
/// elles sont lues. Le service note l'état du pilote, le rapport le dit.
/// </summary>
[Collection("Langue")]
public class PilotePawnIOTests
{
    private static T EnLangue<T>(AppLanguage langue, Func<T> action)
    {
        var initial = Lang.Current;
        try { Lang.Apply(langue); return action(); }
        finally { Lang.Apply(initial); }
    }

    private static T EnFrancais<T>(Func<T> action) => EnLangue(AppLanguage.French, action);

    /// <summary>Noms relevés le 08/11/2026 sur le poste de l'auteur (extrait).</summary>
    private static IReadOnlyList<(string, IReadOnlyList<string>, IReadOnlyList<string>, IReadOnlyList<string>)> Processeur() => new[]
    {
        ("AMD Ryzen 7 7735U with Radeon Graphics",
         (IReadOnlyList<string>)new[] { "CPU Core #1", "CPU Total" },
         (IReadOnlyList<string>)new[] { "Cores (Average)", "Core #1" },
         (IReadOnlyList<string>)new[] { "Package", "Core #1 (SMU)" }),
    };

    private static string Ligne(string? pawnio) =>
        DiagnosticCapteurs.EncoderComplet(null, Processeur(), new List<string>(), pawnio);

    private static FlightCrashContext Contexte(double? temperature, double? frequence, double? puissance) => new()
    {
        CrashTime = new DateTime(2026, 11, 8, 7, 20, 0),
        Samples =
        {
            new FlightSample { Time = new DateTime(2026, 11, 8, 7, 19, 49), CpuLoad = 12.5, CpuTemp = temperature, CpuClockMHz = frequence, CpuPowerW = puissance },
            new FlightSample { Time = new DateTime(2026, 11, 8, 7, 19, 59), CpuLoad = 12.6, CpuTemp = temperature, CpuClockMHz = frequence, CpuPowerW = puissance },
        },
    };

    [Fact]
    public void L_etat_du_pilote_s_ecrit_en_fin_de_ligne_et_se_relit()
    {
        var brut = Ligne(DiagnosticCapteurs.PawnIOAbsent);

        Assert.EndsWith("|puiss=Package,Core #1 (SMU)|pawnio=absent", brut);
        Assert.Equal("absent", DiagnosticCapteurs.EtatPawnIO(brut));
        Assert.Equal("Running", DiagnosticCapteurs.EtatPawnIO(Ligne("Running")));
        // Les autres lectures n'en sont pas gênées.
        Assert.Equal(new[] { "Package", "Core #1 (SMU)" }, DiagnosticCapteurs.Sondes(brut)!.Value.Puissance.ToArray());
        Assert.Equal(EnFrancais(() => DiagnosticCapteurs.Lire(Ligne(null))), EnFrancais(() => DiagnosticCapteurs.Lire(brut)));
    }

    [Fact]
    public void Sans_releve_ou_avec_un_ancien_service_l_etat_est_inconnu()
    {
        Assert.DoesNotContain("pawnio", Ligne(null));
        Assert.Null(DiagnosticCapteurs.EtatPawnIO(Ligne(null)));
        Assert.Null(DiagnosticCapteurs.EtatPawnIO("v1|cpu=Processeur de test|charge=CPU Total"));
        Assert.Null(DiagnosticCapteurs.EtatPawnIO(null));
        // Bibliothèque non ouverte : l'état du pilote est écrit quand même.
        Assert.Equal("absent", DiagnosticCapteurs.EtatPawnIO(
            DiagnosticCapteurs.EncoderComplet("Accès refusé", Processeur(), new List<string>(), "absent")));
    }

    [Fact]
    public void Pilote_absent_la_note_donne_la_raison_et_la_commande()
    {
        var f = new FlightInfo { DiagnosticCapteurs = Ligne(DiagnosticCapteurs.PawnIOAbsent) };

        var note = WebUtility.HtmlDecode(EnFrancais(() => HtmlReportGenerator.NoteFrequenceAbsente(f, Contexte(null, null, null))));

        Assert.Contains("<strong>Température, fréquence et consommation du processeur non mesurées</strong> : un tiret n'est pas un zéro.", note);
        Assert.Contains("Le pilote PawnIO, par lequel la bibliothèque de capteurs lit la température, la fréquence et la consommation du processeur, n'est pas installé sur ce poste", note);
        Assert.Contains("<code>winget install --id namazso.PawnIO -e</code>", note);
        Assert.Contains("<code>Restart-Service FaultTracePCMonitor</code>", note);
        Assert.Contains("fréquence : Cores (Average), Core #1 ; consommation : Package, Core #1 (SMU).", note);
        // L'ancienne raison, démentie par l'essai du 08/11/2026, ne revient pas.
        Assert.DoesNotContain("expose", note);
    }

    [Fact]
    public void Pilote_actif_la_note_ne_parle_pas_d_installation()
    {
        var f = new FlightInfo { DiagnosticCapteurs = Ligne(DiagnosticCapteurs.PawnIOActif) };

        var note = WebUtility.HtmlDecode(EnFrancais(() => HtmlReportGenerator.NoteFrequenceAbsente(f, Contexte(66.1, null, 10.7))));

        Assert.Contains("<strong>Fréquence du processeur non mesurée</strong>", note);
        Assert.Contains("Le pilote PawnIO est pourtant actif", note);
        Assert.DoesNotContain("winget", note);
    }

    [Fact]
    public void Pilote_installe_mais_arrete_l_etat_de_Windows_est_cite()
    {
        var f = new FlightInfo { DiagnosticCapteurs = Ligne("Stopped") };

        var note = WebUtility.HtmlDecode(EnFrancais(() => HtmlReportGenerator.NoteFrequenceAbsente(f, Contexte(null, 4221, 20.4))));

        Assert.Contains("<strong>Température du processeur non mesurée</strong>", note);
        Assert.Contains("installé mais pas en marche (état relevé par Windows : Stopped)", note);
    }

    [Fact]
    public void Tout_mesure_aucune_note_meme_sans_pilote_releve()
    {
        var f = new FlightInfo { DiagnosticCapteurs = Ligne(DiagnosticCapteurs.PawnIOAbsent) };
        Assert.Equal("", HtmlReportGenerator.NoteFrequenceAbsente(f, Contexte(75.9, 4221, 20.4)));
    }

    [Fact]
    public void Temperatures_dans_la_duree_sans_processeur_et_sans_pilote()
    {
        var gpu = new ThermalStats { Sensor = ThermalHistory.CapteurGpu, SampleCount = 10, MaxC = 84, AverageC = 43.8 };
        var sansPilote = new FlightInfo { DiagnosticCapteurs = Ligne(DiagnosticCapteurs.PawnIOAbsent), Thermal = { gpu } };

        var note = WebUtility.HtmlDecode(EnFrancais(() => HtmlReportGenerator.NoteTemperatureProcesseurAbsente(sansPilote)));
        Assert.Contains("<strong>Pas de ligne pour le processeur</strong> : sa température n'est pas lue, et les alertes « Température du processeur élevée » ne peuvent donc pas se déclencher.", note);
        Assert.Contains("winget install --id namazso.PawnIO -e", note);

        // Raison inconnue (service antérieur) : on ne prétend rien.
        Assert.Equal("", HtmlReportGenerator.NoteTemperatureProcesseurAbsente(new FlightInfo { DiagnosticCapteurs = Ligne(null), Thermal = { gpu } }));
        // Le processeur a sa ligne : rien à dire.
        var cpu = new ThermalStats { Sensor = ThermalHistory.CapteurCpu, SampleCount = 10, MaxC = 75.9 };
        Assert.Equal("", HtmlReportGenerator.NoteTemperatureProcesseurAbsente(
            new FlightInfo { DiagnosticCapteurs = Ligne(DiagnosticCapteurs.PawnIOAbsent), Thermal = { gpu, cpu } }));
    }

    [Fact]
    public void La_note_des_temperatures_apparait_dans_le_rapport()
    {
        var r = new DiagnosticReport
        {
            ScanPeriodDays = 30,
            System = new SystemSnapshot { MachineName = "POSTE-TEST" },
            Flight = new FlightInfo
            {
                JournalFound = true,
                DiagnosticCapteurs = Ligne(DiagnosticCapteurs.PawnIOAbsent),
                Thermal = { new ThermalStats { Sensor = ThermalHistory.CapteurGpu, SampleCount = 10, MaxC = 84, AverageC = 43.8 } },
            },
        };

        var html = WebUtility.HtmlDecode(EnFrancais(() => HtmlReportGenerator.Generate(r)));

        var section = html.IndexOf("Températures dans la durée", StringComparison.Ordinal);
        var note = html.IndexOf("Pas de ligne pour le processeur", StringComparison.Ordinal);
        Assert.True(section >= 0 && note > section, "la note doit suivre le tableau des températures");
    }

    [Fact]
    public void Les_notes_existent_en_anglais()
    {
        var f = new FlightInfo { DiagnosticCapteurs = Ligne(DiagnosticCapteurs.PawnIOAbsent) };

        var note = EnLangue(AppLanguage.English, () => HtmlReportGenerator.NoteFrequenceAbsente(f, Contexte(null, null, null)));

        Assert.Contains("<strong>Processor temperature, frequency and power not measured</strong>: a dash is not a zero.", note);
        Assert.Contains("is not installed on this computer; FaultTracePC does not install it itself.", note);
    }
}
