using FaultTracePC.Core;
using FaultTracePC.Core.Analysis;
using Xunit;

namespace FaultTracePC.Tests;

/// <summary>
/// POINT 87 — le réglage des fichiers de plantage, lu avant d'en recommander la
/// correction.
///
/// Constaté le 24/09/2026 : sur une machine réglée d'origine (« Vidage mémoire
/// automatique », fichier d'échange géré par Windows), le rapport recommandait
/// « d'activer les petits vidages » — qui étaient d'ailleurs présents sur le disque.
/// </summary>
[Collection("Langue")]
public class ReglageVidageTests
{
    private static Finding Analyser(int? reglage, bool? echangeAuto, int plantagesSansFichier = 2)
    {
        var r = new DiagnosticReport
        {
            GeneratedAt = new DateTime(2026, 9, 24, 8, 27, 0),
            ScanPeriodDays = 30,
            System = new SystemSnapshot
            {
                MachineName = "POSTE-TEST",
                Os = new OsInfo { CrashDumpEnabled = reglage, FichierEchangeGereParWindows = echangeAuto, PageFileInfo = @"C:\pagefile.sys" },
            },
        };
        r.Events.Add(new WinEvent
        {
            Category = EventCategory.DiskError, Provider = "volmgr", EventId = 161,
            TimeLocal = new DateTime(2026, 9, 22, 21, 0, 0), Message = "Vidage.",
        });
        // Des plantages sans fichier : un événement BugCheck 1001 sans vidage associé
        // (le moteur reconstruit les plantages à partir des événements et des vidages).
        for (int i = 0; i < plantagesSansFichier; i++)
        {
            var e = new WinEvent
            {
                Category = EventCategory.Bsod,
                Provider = "Microsoft-Windows-WER-SystemErrorReporting",
                EventId = 1001,
                TimeLocal = new DateTime(2026, 9, 22 + i, 20, 57, 0),
                Message = "Redemarrage apres erreur.",
            };
            e.Extracted["BugCheckCode"] = "0x00000154";
            r.Events.Add(e);
        }

        var initial = Lang.Current;
        try { Lang.Apply(AppLanguage.French); new RulesEngine().Analyze(r); }
        finally { Lang.Apply(initial); }
        return r.Findings.Single(f => f.Title.Contains("vidage de plantage"));
    }

    [Fact]
    public void Machine_du_24_09_reglee_d_origine_rien_a_corriger()
    {
        var f = Analyser(ReglageVidage.Automatique, echangeAuto: true);

        Assert.Contains("vidage mémoire automatique (réglage d'origine de Windows)", f.Details);
        Assert.Contains("ce n'est pas un réglage à corriger", f.Details);
        Assert.Contains("Ne pas modifier ces réglages", f.Recommendation);
        Assert.DoesNotContain("petits vidages", f.Recommendation);
        Assert.DoesNotContain("tant que ce réglage", f.Details);
        // Plus d'indication « sfc / DISM » : catégorie neutre.
        Assert.Equal(FaultCategory.None, f.Category);
    }

    [Fact]
    public void Reglage_sur_aucun_c_est_la_cause()
    {
        var f = Analyser(0, echangeAuto: true);
        Assert.Contains("c'est la cause", f.Details);
        Assert.Contains("tant que ce réglage n'est pas corrigé", f.Details);
        Assert.Contains("« Vidage mémoire automatique »", f.Recommendation);
    }

    [Fact]
    public void Fichier_d_echange_regle_a_la_main_est_signale()
    {
        var f = Analyser(ReglageVidage.Automatique, echangeAuto: false);
        Assert.Contains("réglé à la main", f.Details);
        Assert.Contains("Laisser Windows gérer le fichier d'échange", f.Recommendation);
    }

    [Fact]
    public void Reglages_non_lus_rien_n_est_affirme()
    {
        var f = Analyser(null, echangeAuto: null);
        Assert.Contains("n'ont pas pu être lus", f.Details);
        Assert.StartsWith("Vérifier les deux réglages", f.Recommendation);
    }

    [Theory]
    [InlineData(0, false, "aucun")]
    [InlineData(1, false, "vidage mémoire complet")]
    [InlineData(1, true, "vidage mémoire actif")]
    [InlineData(2, false, "vidage mémoire du noyau")]
    [InlineData(3, false, "petit vidage mémoire")]
    [InlineData(7, false, "vidage mémoire automatique")]
    [InlineData(9, false, "valeur 9 non répertoriée")]
    public void Chaque_valeur_documentee_a_son_nom(int valeur, bool filtre, string attendu)
    {
        var initial = Lang.Current;
        try
        {
            Lang.Apply(AppLanguage.French);
            Assert.StartsWith(attendu, ReglageVidage.Nom(new OsInfo { CrashDumpEnabled = valeur, FilterPages = filtre }));
        }
        finally { Lang.Apply(initial); }
    }
}
