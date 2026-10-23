using FaultTracePC.Core;
using Xunit;

namespace FaultTracePC.Tests;

/// <summary>
/// Point 116 de la feuille de route : exporter les journaux de Windows en .evtx, en
/// option, pour la personne choisie par l'utilisateur. Seules les fonctions pures sont
/// testées ici : aucun test ne lance wevtutil.
/// </summary>
public class ExportJournauxTests
{
    [Fact]
    public void Le_choix_courant_exporte_Systeme_et_Application_mais_jamais_Securite()
    {
        var journaux = ExportJournaux.Journaux(JournauxAExporter.SystemeEtApplication);
        Assert.Equal(new[] { "System", "Application" }, journaux);
        Assert.DoesNotContain("Security", journaux);
    }

    [Fact]
    public void Le_journal_Securite_ne_s_exporte_que_s_il_est_choisi_et_seul()
    {
        Assert.Equal(new[] { "Security" }, ExportJournaux.Journaux(JournauxAExporter.Securite));
    }

    [Theory]
    [InlineData(30, "*[System[TimeCreated[timediff(@SystemTime) <= 2592000000]]]")]
    [InlineData(90, "*[System[TimeCreated[timediff(@SystemTime) <= 7776000000]]]")]
    public void La_periode_devient_une_requete_de_temps_en_millisecondes(int jours, string attendu) =>
        Assert.Equal(attendu, ExportJournaux.RequeteDePeriode(jours));

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    public void Sans_periode_l_export_est_complet(int? jours) =>
        Assert.Null(ExportJournaux.RequeteDePeriode(jours));

    [Fact]
    public void Les_arguments_d_export_portent_la_requete_seulement_s_il_y_a_une_periode()
    {
        var avec = ExportJournaux.ArgumentsExport("System", @"C:\Temp\System.evtx", 30);
        Assert.Equal("epl", avec[0]);
        Assert.Equal("System", avec[1]);
        Assert.Equal(@"C:\Temp\System.evtx", avec[2]);
        Assert.Contains(avec, a => a.StartsWith("/q:", StringComparison.Ordinal));
        Assert.Contains("/ow:true", avec);

        var complet = ExportJournaux.ArgumentsExport("System", @"C:\Temp\System.evtx", null);
        Assert.DoesNotContain(complet, a => a.StartsWith("/q:", StringComparison.Ordinal));
    }

    [Fact]
    public void L_archivage_joint_les_textes_d_une_langue()
    {
        Assert.Equal(new List<string> { "al", @"C:\Temp\System.evtx", "/l:fr-FR" },
                     ExportJournaux.ArgumentsArchive(@"C:\Temp\System.evtx", "fr-FR"));
    }

    [Fact]
    public void L_anglais_est_toujours_joint_et_jamais_en_double()
    {
        var langues = ExportJournaux.Langues();
        Assert.Contains("en-US", langues, StringComparer.OrdinalIgnoreCase);
        Assert.Equal(langues.Count, langues.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Theory]
    [InlineData("POSTE-TEST", "Journaux_POSTE-TEST_2026-10-23_1430")]
    [InlineData("", "Journaux_PC_2026-10-23_1430")]
    [InlineData("A:B", "Journaux_A_B_2026-10-23_1430")]
    public void Le_dossier_porte_le_nom_de_la_machine_et_la_date(string machine, string attendu) =>
        Assert.Equal(attendu, ExportJournaux.NomDuDossier(machine, new DateTime(2026, 10, 23, 14, 30, 0)));
}
