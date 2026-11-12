using FaultTracePC.Core;
using FaultTracePC.Core.Analysis;
using Xunit;

namespace FaultTracePC.Tests;

/// <summary>
/// Point 123 — constaté le 12/11/2026 sur un poste du parc : « LibreOffice Maintenance
/// Service » signalé comme la trace d'une désinstallation incomplète, alors que
/// LibreOffice, jamais désinstallé, fonctionne. Le fichier absent est un fait ; la
/// désinstallation n'en est qu'une cause possible, et la carte doit le dire.
/// </summary>
[Collection("Langue")]
public class InscriptionOrphelineNuanceTests
{
    [Fact]
    public void Un_logiciel_toujours_installe_n_est_pas_presente_comme_desinstalle()
    {
        var initial = Lang.Current;
        try
        {
            Lang.Apply(AppLanguage.French);
            var r = new DiagnosticReport { System = new SystemSnapshot { MachineName = "POSTE-TEST" } };
            r.System.ServicesOrphelins.Add(new ServiceOrphelin
            {
                Nom = "LibreOfficeMaintenance", NomAffiche = "LibreOffice Maintenance Service",
                Chemin = @"C:\Program Files\LibreOffice\program\update_service.exe", Demarrage = "Manual", Etat = "Stopped",
            });

            RulesEngine.AnalyzeInscriptionsOrphelines(r);

            var f = Assert.Single(r.Findings);
            Assert.Equal("1 service(s) ou pilote(s) inscrit(s) dont le programme est introuvable", f.Title);
            Assert.Contains("le fichier qu'elle désigne est absent", f.Details);
            Assert.Contains("un logiciel toujours installé, et qui fonctionne", f.Details);
            Assert.DoesNotContain("C'est en général la trace d'une désinstallation incomplète", f.Details);
            Assert.StartsWith("Si le logiciel concerné n'est plus installé", f.Recommendation);
            Assert.Contains("S'il l'est encore et fonctionne, ce n'est pas une panne", f.Recommendation);
            Assert.Contains("Ne pas effacer une inscription à la main", f.Recommendation);
        }
        finally { Lang.Apply(initial); }
    }
}
