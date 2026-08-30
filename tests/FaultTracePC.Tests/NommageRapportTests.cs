using FaultTracePC.Core;
using FaultTracePC.Core.Report;
using Xunit;

namespace FaultTracePC.Tests;

/// <summary>
/// Nom de fichier des rapports.
///
/// DÉFAUT CONSTATÉ LE 30/08/2026 en regardant un nom de fichier : la ligne de
/// commande écrivait « Diagnostic_TECH-INFO-2025_… » et l'application
/// « Diagnostic_PC_… ». Deux familles de noms pour la même chose ; et sur un
/// dossier partagé par plusieurs postes, deux machines analysant à la même
/// minute écrasaient le rapport l'une de l'autre.
///
/// L'invariant vérifié ici est celui qui compte : ce que le logiciel PRODUIT doit
/// toujours passer le contrôle qu'il applique lui-même avant de servir un fichier.
/// </summary>
public class NommageRapportTests
{
    private static DiagnosticReport Rapport(string machine) => new()
    {
        GeneratedAt = new DateTime(2026, 8, 30, 9, 7, 0),
        System = new SystemSnapshot { MachineName = machine },
    };

    [Fact]
    public void Le_nom_porte_celui_de_la_machine()
    {
        Assert.Equal("Diagnostic_TECH-INFO-2025_2026-08-30_0907.html",
                     HtmlReportGenerator.NomDuRapport(Rapport("TECH-INFO-2025")));
    }

    [Theory]
    [InlineData("POSTE 12")]              // espace
    [InlineData("SALLE-3/POSTE-1")]       // séparateur de chemin
    [InlineData("../../etc")]             // tentative de traversée
    [InlineData("C:POSTE")]               // deux-points
    [InlineData("PÔLE-INFO")]             // accent : légitime, doit survivre
    public void Un_nom_de_machine_hostile_produit_toujours_un_nom_valide(string machine)
    {
        var nom = HtmlReportGenerator.NomDuRapport(Rapport(machine));

        // L'invariant : ce qu'on écrit, on doit pouvoir le redemander.
        Assert.True(HtmlReportGenerator.EstUnNomDeRapport(nom), nom);
        Assert.DoesNotContain("/", nom);
        Assert.DoesNotContain("\\", nom);
        Assert.DoesNotContain("..", nom);
    }

    [Fact]
    public void L_ancienne_forme_reste_acceptee()
    {
        // Les rapports déposés avant la 1.5.0 dans le dossier partagé doivent
        // rester listables et téléchargeables : les rendre invisibles serait
        // perdre l'historique d'un parc du jour au lendemain.
        Assert.True(HtmlReportGenerator.EstUnNomDeRapport("Diagnostic_PC_2026-08-19_1109.html"));
    }

    [Theory]
    [InlineData("../secret.html")]
    [InlineData("Diagnostic_PC_../../secret.html")]
    [InlineData("Diagnostic_PC_2026.html.exe")]
    [InlineData("autre.html")]
    [InlineData("Diagnostic_PC_2026-08-19_1109.HTML")]   // la casse compte : le disque, lui, s'en moque
    [InlineData("")]
    public void Le_controle_refuse_ce_qui_n_est_pas_un_rapport(string nom)
    {
        Assert.False(HtmlReportGenerator.EstUnNomDeRapport(nom));
    }

    [Fact]
    public void Un_nom_de_machine_vide_ne_fait_pas_tomber_la_generation()
    {
        var nom = HtmlReportGenerator.NomDuRapport(Rapport(""));

        Assert.True(HtmlReportGenerator.EstUnNomDeRapport(nom), nom);
        Assert.StartsWith("Diagnostic_", nom);
    }
}
