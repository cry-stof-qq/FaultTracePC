using FaultTracePC.Core;
using FaultTracePC.Core.Analysis;
using Xunit;

namespace FaultTracePC.Tests;

/// <summary>
/// Constaté le 18/09/2026 sur TECH-INFO-2025 : « Échecs de services Windows
/// répétés (29) », suivi de « Consulter le détail dans la section Événements pour
/// identifier le(s) service(s) concerné(s) ».
///
/// Le logiciel avait les 29 événements sous la main, chacun portant le nom du
/// service dans ses données. Il renvoyait au lecteur un travail qu'il pouvait faire
/// lui-même — le reproche exact du thème de la 1.6.0.
/// </summary>
[Collection("Langue")]
public class ServicesEnEchecTests
{
    private static DiagnosticReport Rapport(params (string Service, int Combien)[] services)
    {
        var r = new DiagnosticReport
        {
            GeneratedAt = new DateTime(2026, 9, 18, 8, 4, 0),
            ScanPeriodDays = 30,
            System = new SystemSnapshot { MachineName = "POSTE-TEST" },
        };
        var t = new DateTime(2026, 9, 10, 9, 0, 0);
        foreach (var (service, combien) in services)
            for (int i = 0; i < combien; i++)
            {
                var e = new WinEvent
                {
                    Category = EventCategory.ServiceFailure,
                    Provider = "Service Control Manager",
                    EventId = 7031,
                    TimeLocal = t,
                    Message = "Le service s'est arrêté de manière inattendue.",
                };
                if (service.Length > 0) e.Extracted["Service"] = service;
                r.Events.Add(e);
                t = t.AddMinutes(7);
            }
        return r;
    }

    private static Finding Conclusion(DiagnosticReport r)
    {
        var initial = Lang.Current;
        try { Lang.Apply(AppLanguage.French); new RulesEngine().Analyze(r); }
        finally { Lang.Apply(initial); }
        return r.Findings.First(f => f.Code == "service_failure");
    }

    [Fact]
    public void Les_services_sont_nommes_au_lieu_d_etre_comptes()
    {
        var f = Conclusion(Rapport(("Service de sauvegarde ACME", 8), ("Windows Update", 3)));

        Assert.Contains("Service de sauvegarde ACME", f.Details);
        Assert.Contains("Windows Update", f.Details);
        // La phrase qui renvoyait le lecteur au tableau des événements a disparu.
        Assert.DoesNotContain("section Événements", f.Recommendation);
    }

    [Fact]
    public void Le_service_qui_concentre_les_echecs_est_designe()
    {
        var f = Conclusion(Rapport(("Service de sauvegarde ACME", 20), ("Windows Update", 3)));

        Assert.Contains("concentre 20 des 23 échecs", f.Details);
        // Le geste vise services.msc, et jamais sc.exe : le nom relevé est le nom
        // d'affichage, sc.exe attend le nom court et échouerait dessus.
        Assert.Contains("services.msc", f.Recommendation);
        Assert.DoesNotContain("sc.exe", f.Recommendation);
    }

    [Fact]
    public void Deux_services_qui_couvrent_presque_tout_ne_sont_pas_une_dispersion()
    {
        // LE cas réel de TECH-INFO-2025, 18/09/2026 : 14, 14 et 1. Aucun service
        // n'atteint la moitié, et pourtant deux d'entre eux couvrent 28 échecs sur 29.
        // La première version de la règle concluait « aucun ne domine, ce qui désigne
        // plutôt le système » et renvoyait vers sfc et DISM — pour deux agents tiers.
        var f = Conclusion(Rapport(("GLPI Agent", 14), ("TmWSCSvc", 14), ("Agent tiers", 1)));

        Assert.Contains("Deux services concentrent 28 des 29 échecs", f.Details);
        Assert.Contains("GLPI Agent", f.Details);
        Assert.Contains("TmWSCSvc", f.Details);
        Assert.DoesNotContain("aucun ne domine", f.Details);
        // Et surtout : on n'envoie plus réparer Windows pour des services tiers.
        Assert.Contains("services.msc", f.Recommendation);
        Assert.DoesNotContain("image Windows", f.Recommendation);
    }

    [Fact]
    public void Des_echecs_disperses_designent_le_systeme_et_pas_un_service()
    {
        var f = Conclusion(Rapport(("Service A", 2), ("Service B", 2), ("Service C", 2), ("Service D", 2), ("Service E", 2)));

        Assert.Contains("aucun ne domine", f.Details);
        Assert.Contains("fichiers système", f.Recommendation);
    }

    [Fact]
    public void Seuls_les_quatre_plus_touches_sont_nommes_le_reste_est_compte()
    {
        var f = Conclusion(Rapport(
            ("Service A", 5), ("Service B", 4), ("Service C", 3),
            ("Service D", 2), ("Service E", 2), ("Service F", 2)));

        Assert.Contains("Service A", f.Details);
        Assert.Contains("Service D", f.Details);
        Assert.Contains("2 autre(s) service(s)", f.Details);
    }

    [Fact]
    public void Sans_nom_de_service_lisible_le_rapport_le_dit()
    {
        // « Pas pu lire » ne se présente pas comme « pas cherché ».
        var f = Conclusion(Rapport(("", 6)));

        Assert.Contains("Aucun de ces événements ne porte de nom de service exploitable", f.Details);
        Assert.Contains("Service Control Manager", f.Recommendation);
    }

    [Fact]
    public void En_dessous_du_seuil_aucune_conclusion_n_est_produite()
    {
        var r = Rapport(("Service A", 4));
        var initial = Lang.Current;
        try { Lang.Apply(AppLanguage.French); new RulesEngine().Analyze(r); }
        finally { Lang.Apply(initial); }

        Assert.DoesNotContain(r.Findings, f => f.Code == "service_failure");
    }
}
