using FaultTracePC.Core;
using FaultTracePC.Core.Analysis;
using FaultTracePC.Core.Report;
using Xunit;

namespace FaultTracePC.Tests;

/// <summary>
/// POINT 80 — pourquoi un pilote n'est pas nommé.
///
/// Constaté le 24/09/2026 sur une machine inconnue : WinDbg était installé dans sa
/// version du Microsoft Store, Windows refusait de le lancer par son chemin, et le
/// rapport en tirait trois affirmations fausses — cinq fois la même erreur dans les
/// limitations, « installer WinDbg » sur chaque écran bleu, et ce même conseil sur
/// deux plantages qui n'avaient laissé aucun fichier à analyser.
///
/// Le lancement réel de CDB ne se teste pas ici : il demande WinDbg et des dumps.
/// Ce qui se teste, c'est ce que le logiciel CONCLUT quand le lancement échoue —
/// provoqué avec un fichier qui porte l'extension .exe sans en être un.
/// </summary>
[Collection("Langue")]
public class AnalyseProfondeTests
{
    private static T EnFrancais<T>(Func<T> f)
    {
        var initial = Lang.Current;
        try { Lang.Apply(AppLanguage.French); return f(); }
        finally { Lang.Apply(initial); }
    }

    private static DumpFileInfo DumpNoyau(string nom, int joursAvant) => new()
    {
        Path = $@"C:\Windows\Minidump\{nom}.dmp",
        Kind = DumpKind.KernelMinidump,
        BugCheckCode = 0x154,
        CrashTimeFromHeader = DateTime.Now.AddDays(-joursAvant),
        LastWriteTime = DateTime.Now.AddDays(-joursAvant),
    };

    /// <summary>Un faux cdb.exe que Windows refusera de lancer, dans le sous-dossier demandé.</summary>
    private static (string Racine, string Exe) FauxDebogueur(string sousDossier)
    {
        var racine = Path.Combine(Path.GetTempPath(), "ftpc_cdb_" + Guid.NewGuid().ToString("N"));
        var dossier = Path.Combine(racine, sousDossier);
        Directory.CreateDirectory(dossier);
        var exe = Path.Combine(dossier, "cdb.exe");
        File.WriteAllText(exe, "ce fichier n'est pas un executable");
        return (racine, exe);
    }

    // ------------------------------------------------------------------
    // L'analyseur
    // ------------------------------------------------------------------

    [Fact]
    public void Un_debogueur_qui_ne_se_lance_pas_est_signale_une_seule_fois()
    {
        var (racine, exe) = FauxDebogueur("outils");
        try
        {
            var dumps = new List<DumpFileInfo> { DumpNoyau("a", 1), DumpNoyau("b", 2), DumpNoyau("c", 3) };
            var erreurs = new List<string>();

            var etat = EnFrancais(() => new CdbAnalyzer(erreurs).AnalyzeAll(dumps, 5, TestContext.Current.CancellationToken, exe));

            Assert.Equal(EtatAnalyseProfonde.Inaccessible, etat);
            // Une ligne pour la machine, pas une par dump : le 24/09/2026 il y en avait cinq.
            Assert.Single(erreurs);
            Assert.All(dumps, d => Assert.False(d.DeepAnalyzed));
            Assert.All(dumps, d => Assert.Contains("impossible à lancer", d.DeepAnalysisError));
            // Le débogueur est présent : le conseil ne doit surtout pas être de l'installer.
            Assert.DoesNotContain("Pour l'installer", erreurs[0]);
            Assert.Contains("Debugging Tools for Windows", erreurs[0]);
            // Chemin ordinaire : rien ne permet de parler du Microsoft Store.
            Assert.DoesNotContain("Microsoft Store", erreurs[0]);
        }
        finally { try { Directory.Delete(racine, true); } catch { } }
    }

    [Fact]
    public void Le_Microsoft_Store_n_est_nomme_que_si_le_chemin_le_designe()
    {
        var (racine, exe) = FauxDebogueur(Path.Combine("WindowsApps", "Microsoft.WinDbg_test", "amd64"));
        try
        {
            var erreurs = new List<string>();
            EnFrancais(() => new CdbAnalyzer(erreurs).AnalyzeAll([DumpNoyau("a", 1)], 5, TestContext.Current.CancellationToken, exe));

            Assert.Contains("Microsoft Store", Assert.Single(erreurs));
        }
        finally { try { Directory.Delete(racine, true); } catch { } }
    }

    [Fact]
    public void Sans_dump_noyau_rien_n_est_reclame()
    {
        // Jusqu'au 09/10/2026, une machine qui n'avait que des dumps d'applications
        // recevait « WinDbg introuvable » pour une analyse qui n'avait rien à faire.
        var dumps = new List<DumpFileInfo>
        {
            new() { Path = @"C:\ProgramData\Microsoft\Windows\WER\appli.dmp", Kind = DumpKind.UserModeMinidump },
        };
        var erreurs = new List<string>();

        var etat = new CdbAnalyzer(erreurs).AnalyzeAll(dumps, 5, TestContext.Current.CancellationToken);

        Assert.Equal(EtatAnalyseProfonde.SansObjet, etat);
        Assert.Empty(erreurs);
    }

    // ------------------------------------------------------------------
    // Ce que le rapport écrit à la place du pilote
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(EtatAnalyseProfonde.NonDemandee, "analyse profonde non demandée")]
    [InlineData(EtatAnalyseProfonde.Absente, "installer WinDbg")]
    [InlineData(EtatAnalyseProfonde.Inaccessible, "impossible à lancer")]
    public void La_raison_suit_l_etat_de_l_analyse(EtatAnalyseProfonde etat, string attendu)
    {
        var r = new DiagnosticReport { AnalyseProfonde = etat, Dumps = [DumpNoyau("a", 1)] };
        var b = new BsodIncident { DumpPath = r.Dumps[0].Path };

        Assert.Contains(attendu, EnFrancais(() => HtmlReportGenerator.RaisonPiloteNonNomme(r, b)));
    }

    [Fact]
    public void Un_plantage_sans_fichier_ne_demande_jamais_d_installer_quoi_que_ce_soit()
    {
        // Le 24/09/2026, deux écrans bleus sans aucun fichier portaient « installer
        // WinDbg ». Aucun débogueur ne nomme un pilote sans fichier à lire.
        foreach (var etat in Enum.GetValues<EtatAnalyseProfonde>())
        {
            var r = new DiagnosticReport { AnalyseProfonde = etat };
            var texte = EnFrancais(() => HtmlReportGenerator.RaisonPiloteNonNomme(r, new BsodIncident { DumpPath = null }));

            Assert.Contains("aucun fichier d'incident", texte);
            Assert.DoesNotContain("installer", texte);
        }
    }

    [Fact]
    public void Un_dump_laisse_de_cote_par_le_plafond_est_dit_comme_tel()
    {
        var analyse = DumpNoyau("recent", 1);
        analyse.DeepAnalyzed = true;
        var ancien = DumpNoyau("ancien", 40);   // au-delà du plafond : jamais lancé
        var r = new DiagnosticReport { AnalyseProfonde = EtatAnalyseProfonde.Faite, Dumps = [analyse, ancien] };

        var texte = EnFrancais(() => HtmlReportGenerator.RaisonPiloteNonNomme(r, new BsodIncident { DumpPath = ancien.Path }));

        Assert.Contains("seuls les plus récents", texte);
    }

    [Fact]
    public void Un_etat_inconnu_n_ajoute_aucun_conseil()
    {
        // Valeur par défaut : rapport d'une version antérieure, ou chemin qui ne la
        // renseigne pas. On s'en tient au constat.
        var r = new DiagnosticReport { Dumps = [DumpNoyau("a", 1)] };

        var texte = EnFrancais(() => HtmlReportGenerator.RaisonPiloteNonNomme(r, new BsodIncident { DumpPath = r.Dumps[0].Path }));

        Assert.Equal("non identifié", texte);
    }
}
