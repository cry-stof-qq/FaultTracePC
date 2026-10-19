using FaultTracePC.Core;
using FaultTracePC.Core.Analysis;
using FaultTracePC.Core.Collectors;
using Xunit;

namespace FaultTracePC.Tests;

/// <summary>
/// POINT 103, correction validée le 19/10/2026. Reproduit en machine virtuelle :
/// un pilote en démarrage automatique dont le fichier manque fait échouer Windows
/// à chaque démarrage, mais WMI ne le liste pas. Il est désormais lu dans le
/// registre. Le cas réel était un pilote de filtrage d'un ancien antivirus.
/// </summary>
[Collection("Langue")]
public class PilotesOrphelinsTests
{
    [Theory]
    [InlineData(1, 2, true)]    // pilote noyau, automatique — le cas réel
    [InlineData(2, 0, true)]    // système de fichiers, au démarrage
    [InlineData(1, 1, true)]    // pilote noyau, système
    [InlineData(1, 3, false)]   // manuel : Windows ne le lance pas de lui-même
    [InlineData(1, 4, false)]   // désactivé
    [InlineData(16, 2, false)]  // un service, pas un pilote : déjà vu par Win32_Service
    public void Seuls_les_pilotes_qui_demarrent_avec_Windows_sont_retenus(int type, int start, bool attendu)
        => Assert.Equal(attendu, ServiceCollector.EstPiloteAuDemarrage(type, start));

    [Fact]
    public void Une_cle_sans_type_ou_sans_mode_de_demarrage_n_est_pas_retenue()
    {
        // La coquille vide « tmWfp » relevée le 19/10/2026 : aucune valeur.
        Assert.False(ServiceCollector.EstPiloteAuDemarrage(null, null));
        Assert.False(ServiceCollector.EstPiloteAuDemarrage(1, null));
    }

    [Fact]
    public void Le_chemin_noyau_est_converti_en_chemin_Windows()
    {
        Assert.Equal(@"C:\Program Files (x86)\Editeur\Agent\filtre.sys",
            ServiceCollector.CheminDuPilote(@"\??\C:\Program Files (x86)\Editeur\Agent\filtre.sys", "FiltreTest"));
        Assert.Equal(@"C:\Editeur\pilote.sys",
            ServiceCollector.CheminDuPilote(@"""C:\Editeur\pilote.sys""", "PiloteTest"));
    }

    [Fact]
    public void Sans_ImagePath_le_chemin_par_defaut_de_Windows_est_verifie()
    {
        var windir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        Assert.Equal(Path.Combine(windir, @"System32\drivers\PiloteTest.sys"),
            ServiceCollector.CheminDuPilote(null, "PiloteTest"));
    }

    [Theory]
    [InlineData(0, "Boot")]
    [InlineData(1, "System")]
    [InlineData(2, "Auto")]
    public void Le_mode_de_demarrage_porte_le_nom_que_WMI_emploie(int start, string attendu)
        => Assert.Equal(attendu, ServiceCollector.ModeDeDemarrage(start));

    private static ServiceOrphelin PiloteDuCasReel() => new()
    {
        Nom = "TmPreFilter", NomAffiche = "Trend Micro PreFilter",
        Chemin = @"C:\Program Files (x86)\Trend Micro\Security Agent\TmPreFlt.sys",
        Demarrage = "Auto", EstPilote = true,
    };

    [Fact]
    public void Le_pilote_est_cite_comme_pilote_dans_la_carte_des_inscriptions_orphelines()
    {
        var r = new DiagnosticReport();
        r.System.ServicesOrphelins.Add(PiloteDuCasReel());

        var initial = Lang.Current;
        try { Lang.Apply(AppLanguage.French); RulesEngine.AnalyzeInscriptionsOrphelines(r); }
        finally { Lang.Apply(initial); }

        var f = Assert.Single(r.Findings, x => x.Code == "inscriptions.orphelines");
        Assert.Contains(@"pilote « Trend Micro PreFilter » (TmPreFilter), démarrage Auto — C:\Program Files (x86)\Trend Micro\Security Agent\TmPreFlt.sys", f.Details);
        Assert.DoesNotContain("service « Trend Micro PreFilter »", f.Details);
    }

    [Fact]
    public void Ses_echecs_de_demarrage_sont_mis_a_part_avec_le_point_104()
    {
        var r = new DiagnosticReport();
        r.System.ServicesOrphelins.Add(PiloteDuCasReel());
        Assert.True(RulesEngine.ProgrammeDisparu(r, "TmPreFilter"));
    }
}
