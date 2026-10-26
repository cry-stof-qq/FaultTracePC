using FaultTracePC.Core;
using FaultTracePC.Core.Analysis;
using FaultTracePC.Core.Collectors;
using Xunit;

namespace FaultTracePC.Tests;

/// <summary>
/// Point 15, lot 1c de la 1.10.0 : ce que le rapport dit de la liste de winget.
/// Les listes réelles sont les captures du 26/10/2026 (voir <see cref="LecteurWingetTests"/>).
/// Règle vérifiée partout : le rapport ne dit jamais « tout est à jour ».
/// </summary>
[Collection("Langue")]
public class MisesAJourLogicielsTests
{
    private static T EnLangue<T>(AppLanguage langue, Func<T> action)
    {
        var initial = Lang.Current;
        try { Lang.Apply(langue); return action(); }
        finally { Lang.Apply(initial); }
    }

    private static T EnFrancais<T>(Func<T> action) => EnLangue(AppLanguage.French, action);

    private static InterrogationWinget Reelle(EtendueWinget e) => WingetCollector.Interpreter(e, 0,
        e == EtendueWinget.Machine ? LecteurWingetTests.SortieMachine : LecteurWingetTests.SortieUtilisateur);

    private static BilanWinget BilanReel() =>
        WingetCollector.Bilan(new[] { Reelle(EtendueWinget.Machine), Reelle(EtendueWinget.Utilisateur) });

    private static InterrogationWinget Etat(EtendueWinget e, EtatWinget etat, int? code = null, string? message = null) =>
        new(e, etat, code, message, null);

    [Fact]
    public void Les_captures_reelles_donnent_une_conclusion_d_information_complete()
    {
        var f = EnFrancais(() => MisesAJourLogiciels.Conclusion(BilanReel()));

        Assert.Equal(Severity.Info, f.Severity);
        Assert.Equal(FaultCategory.None, f.Category);
        Assert.Equal(Confidence.High, f.Confidence);
        Assert.Equal(MisesAJourLogiciels.CodeConclusion, f.Code);
        Assert.Equal("8 logiciel(s) avec une mise à jour disponible", f.Title);
        Assert.Contains("Notepad++ (8.8.5 → 8.9.8.1)", f.Details);
        Assert.Contains("Sous-système Windows pour Linux (2.7.13.0 → 2.7.14)", f.Details);
        Assert.DoesNotContain("INCOMPLÈTE", f.Details);
        Assert.DoesNotContain("autre(s)", f.Details); // 8 noms, la limite exacte
    }

    [Fact]
    public void Au_dela_de_huit_la_conclusion_renvoie_au_reste()
    {
        var lignes = Enumerable.Range(1, 10)
            .Select(k => new MiseAJourWinget($"Outil {k}", $"Editeur.Outil{k}", "1.0", "2.0", "winget", 0, false, false))
            .ToArray();
        var bilan = WingetCollector.Bilan(new[]
        {
            new InterrogationWinget(EtendueWinget.Machine, EtatWinget.Lu, 0, null,
                new LectureWinget(EtatLectureWinget.Lu, lignes, 0, 10, 0)),
            Etat(EtendueWinget.Utilisateur, EtatWinget.RienDeSignale, 0),
        });

        var f = EnFrancais(() => MisesAJourLogiciels.Conclusion(bilan));

        Assert.Contains(" et 2 autre(s).", f.Details);
        Assert.DoesNotContain("Outil 9", f.Details);
    }

    [Fact]
    public void Une_etendue_en_echec_rend_la_liste_incomplete_avec_sa_raison_et_son_code()
    {
        var bilan = WingetCollector.Bilan(new[]
        {
            Reelle(EtendueWinget.Machine),
            Etat(EtendueWinget.Utilisateur, EtatWinget.Echec, unchecked((int)0x8A15000F), "Échec de la source"),
        });

        var f = EnFrancais(() => MisesAJourLogiciels.Conclusion(bilan));

        Assert.Equal("2 logiciel(s) avec une mise à jour disponible — liste incomplète", f.Title);
        Assert.Contains("La liste est INCOMPLÈTE.", f.Details);
        Assert.Contains("Recherche pour ce compte : winget a échoué (code 0x8A15000F) : « Échec de la source ».", f.Details);
        Assert.Equal(Confidence.Medium, f.Confidence);
    }

    [Fact]
    public void Hors_ligne_le_rapport_explique_le_code_0x8A15004B()
    {
        var bilan = WingetCollector.Bilan(new[]
        {
            WingetCollector.Interpreter(EtendueWinget.Machine, LecteurWingetTests.CodeHorsLigne, LecteurWingetTests.SortieHorsLigne),
            WingetCollector.Interpreter(EtendueWinget.Utilisateur, LecteurWingetTests.CodeHorsLigne, LecteurWingetTests.SortieHorsLigne),
        });

        var f = EnFrancais(() => MisesAJourLogiciels.Conclusion(bilan));

        Assert.Equal("Mises à jour des logiciels : vérification incomplète", f.Title);
        Assert.Contains("Recherche pour tout le poste : winget a échoué (code 0x8A15004B) : « Failed when opening source(s); try the 'source reset' command if the problem persists. » — winget n'a pas pu joindre sa source : pas d'accès à Internet, ou accès filtré. Réessayer avec une connexion qui l'autorise.", f.Details);
        Assert.Contains("ne veut PAS dire que tout est à jour", f.Details);
    }

    [Fact]
    public void Hors_ligne_au_dela_du_delai_le_code_0x80072EE7_a_la_meme_explication()
    {
        var i = WingetCollector.Interpreter(EtendueWinget.Utilisateur,
            WingetCollectorTests.CodeNomNonResolu, WingetCollectorTests.SortieNomNonResolu);

        Assert.Equal(
            "winget a échoué (code 0x80072EE7) : « Failed in attempting to update the source: winget » — winget n'a pas pu joindre sa source : pas d'accès à Internet, ou accès filtré. Réessayer avec une connexion qui l'autorise",
            EnFrancais(() => MisesAJourLogiciels.Raison(i)));
    }

    [Fact]
    public void Un_code_inconnu_n_a_pas_d_explication_inventee()
    {
        Assert.Equal("", MisesAJourLogiciels.Explication(1));
        Assert.Equal("", MisesAJourLogiciels.Explication(null));
    }

    [Fact]
    public void Rien_de_signale_des_deux_cotes_cite_winget_sans_dire_a_jour()
    {
        var bilan = WingetCollector.Bilan(new[]
        {
            Etat(EtendueWinget.Machine, EtatWinget.RienDeSignale, 0, "Phrase de winget A"),
            Etat(EtendueWinget.Utilisateur, EtatWinget.RienDeSignale, 0, "Phrase de winget B"),
        });

        var f = EnFrancais(() => MisesAJourLogiciels.Conclusion(bilan));

        Assert.Equal("Logiciels : winget ne signale aucune mise à jour", f.Title);
        Assert.Contains("pour tout le poste : « Phrase de winget A »", f.Details);
        Assert.Contains("pour ce compte : « Phrase de winget B »", f.Details);
        Assert.DoesNotContain("tout est à jour", f.Title + f.Details);
    }

    [Fact]
    public void Sous_le_compte_systeme_la_conclusion_dit_non_verifiees_et_jamais_a_jour()
    {
        var bilan = WingetCollector.Bilan(new[]
        {
            Etat(EtendueWinget.Machine, EtatWinget.NonVerifieSousSysteme),
            Etat(EtendueWinget.Utilisateur, EtatWinget.NonVerifieSousSysteme),
        });

        var f = EnFrancais(() => MisesAJourLogiciels.Conclusion(bilan));

        Assert.Equal("Mises à jour des logiciels : non vérifiées", f.Title);
        Assert.Contains("ne veut PAS dire que tout est à jour", f.Details);
        Assert.Contains("compte système", f.Details);
    }

    [Fact]
    public void Un_delai_depasse_sans_liste_donne_une_verification_incomplete()
    {
        var bilan = WingetCollector.Bilan(new[]
        {
            Etat(EtendueWinget.Machine, EtatWinget.DelaiDepasse),
            Etat(EtendueWinget.Utilisateur, EtatWinget.WingetAbsent),
        });

        var f = EnFrancais(() => MisesAJourLogiciels.Conclusion(bilan));

        Assert.Equal("Mises à jour des logiciels : vérification incomplète", f.Title);
        Assert.Contains("n'a pas répondu en une minute", f.Details);
        Assert.Contains("introuvable pour ce compte", f.Details);
    }

    [Fact]
    public void Un_ecart_entre_le_compte_annonce_et_les_lignes_lues_est_signale()
    {
        var lignes = new[] { new MiseAJourWinget("Outil", "Editeur.Outil", "1.0", "2.0", "winget", 0, false, false) };
        var i = new InterrogationWinget(EtendueWinget.Machine, EtatWinget.Lu, 0, null,
            new LectureWinget(EtatLectureWinget.Lu, lignes, 0, 3, 0));

        Assert.Equal("winget en annonce 3 pour tout le poste, 1 ont été lues",
            EnFrancais(() => MisesAJourLogiciels.EcartDeCompte(i)));
        Assert.Null(MisesAJourLogiciels.EcartDeCompte(Reelle(EtendueWinget.Utilisateur))); // 7 sur 7
    }

    [Theory]
    [InlineData("notepad++.exe", "Notepad++.Notepad++")]
    [InlineData("python.exe", "Python.Python.3.14")]
    [InlineData("WinSCP.exe", "WinSCP.WinSCP")]
    [InlineData("MobaXterm.exe", "Mobatek.MobaXterm")]
    public void L_executable_qui_plante_retrouve_sa_mise_a_jour_par_le_nom(string exe, string identifiant)
    {
        Assert.Equal(identifiant, MisesAJourLogiciels.MiseAJourPour(BilanReel(), exe)?.Identifiant);
    }

    [Theory]
    [InlineData("inconnu.exe")]
    [InlineData("")]
    public void Sans_ressemblance_de_nom_aucune_mise_a_jour_n_est_attribuee(string exe)
    {
        Assert.Null(MisesAJourLogiciels.MiseAJourPour(BilanReel(), exe));
    }

    [Fact]
    public void Sans_bilan_rien_n_est_attribue()
    {
        Assert.Null(MisesAJourLogiciels.MiseAJourPour(null, "notepad++.exe"));
    }

    [Fact]
    public void La_phrase_de_la_carte_nomme_le_paquet_et_les_deux_versions()
    {
        var notepad = MisesAJourLogiciels.MiseAJourPour(BilanReel(), "notepad++.exe")!;

        Assert.Equal(
            " Une mise à jour est disponible pour « Notepad++ » selon winget : 8.8.5 → 8.9.8.1. Rapprochement fait sur le nom : vérifier qu'il s'agit bien du même logiciel.",
            EnFrancais(() => MisesAJourLogiciels.PhraseSurLaCarte(notepad)));
    }

    // ---- Dans le moteur de règles ---------------------------------------------

    private static DiagnosticReport RapportAvecPlantages(string app, BilanWinget? bilan)
    {
        var r = new DiagnosticReport
        {
            GeneratedAt = new DateTime(2026, 10, 26, 18, 0, 0),
            ScanPeriodDays = 30,
            System = new SystemSnapshot { MachineName = "POSTE-TEST" },
            Winget = bilan,
        };
        for (int k = 0; k < 4; k++)
            r.Events.Add(new WinEvent
            {
                Category = EventCategory.AppCrash,
                Provider = "Application Error",
                EventId = 1000,
                TimeLocal = new DateTime(2026, 10, 20, 10, 0, 0).AddMinutes(k),
                Extracted = { ["App"] = app, ["Module"] = "KERNELBASE.dll" },
            });
        EnFrancais(() => { new RulesEngine().Analyze(r); return 0; });
        return r;
    }

    [Fact]
    public void La_carte_d_une_application_qui_plante_dit_qu_une_mise_a_jour_existe()
    {
        var r = RapportAvecPlantages("notepad++.exe", BilanReel());

        var carte = Assert.Single(r.Findings, f => f.Title.Contains("notepad++.exe"));
        Assert.Contains("Une mise à jour est disponible pour « Notepad++ » selon winget : 8.8.5 → 8.9.8.1.", carte.Details);
        Assert.Single(r.Findings, f => f.Code == MisesAJourLogiciels.CodeConclusion);
    }

    [Fact]
    public void Sans_recherche_demandee_le_rapport_ne_dit_rien_de_winget()
    {
        var r = RapportAvecPlantages("notepad++.exe", null);

        Assert.DoesNotContain(r.Findings, f => f.Code == MisesAJourLogiciels.CodeConclusion);
        Assert.DoesNotContain(r.Findings, f => f.Details.Contains("winget"));
    }

    [Fact]
    public void La_conclusion_winget_ne_change_aucune_autre_conclusion()
    {
        // Elle est ajoutée APRÈS « Aucune anomalie significative détectée » : une liste
        // de mises à jour n'est pas une anomalie, et ne doit pas faire disparaître
        // cette phrase ni toucher au verdict.
        DiagnosticReport Analyse(BilanWinget? bilan)
        {
            var r = new DiagnosticReport
            {
                GeneratedAt = new DateTime(2026, 10, 26, 18, 0, 0),
                ScanPeriodDays = 30,
                System = new SystemSnapshot { MachineName = "POSTE-TEST" },
                Winget = bilan,
            };
            EnFrancais(() => { new RulesEngine().Analyze(r); return 0; });
            return r;
        }

        var sans = Analyse(null);
        var avec = Analyse(BilanReel());

        Assert.Equal(sans.Findings.Select(f => f.Title),
                     avec.Findings.Where(f => f.Code != MisesAJourLogiciels.CodeConclusion).Select(f => f.Title));
        Assert.Equal(sans.Verdict, avec.Verdict);
        Assert.Equal(sans.VerdictCategory, avec.VerdictCategory);
        Assert.Single(avec.Findings, f => f.Code == MisesAJourLogiciels.CodeConclusion);
    }

    [Fact]
    public void Par_defaut_l_analyse_ne_demande_rien_a_winget()
    {
        // Lot 1d : winget contacte Internet ; sans case cochée, jamais.
        Assert.False(new ScanOptions().RechercherMisesAJourLogiciels);
    }

    [Fact]
    public void Les_textes_existent_en_anglais()
    {
        var f = EnLangue(AppLanguage.English, () => MisesAJourLogiciels.Conclusion(WingetCollector.Bilan(new[]
        {
            Reelle(EtendueWinget.Machine),
            Etat(EtendueWinget.Utilisateur, EtatWinget.DelaiDepasse),
        })));

        Assert.Equal("2 program(s) with an update available — incomplete list", f.Title);
        Assert.Contains("Search for this account: winget did not answer within one minute; it was stopped.", f.Details);
    }
}
