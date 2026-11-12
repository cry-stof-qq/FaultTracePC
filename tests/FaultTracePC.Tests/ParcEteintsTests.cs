using FaultTracePC.Core;
using FaultTracePC.Core.Analysis;
using FaultTracePC.Core.Report;
using Xunit;

namespace FaultTracePC.Tests;

/// <summary>
/// Point 126 : les postes restés éteints pendant que les autres postes du même modèle
/// démarraient. Calendrier SIMULÉ sur le modèle de la salle de l'auteur (cycle du soir,
/// postes allumés chaque matin) ; la période du poste touché est la période RÉELLE du
/// 21/10 au 12/11/2026.
/// </summary>
[Collection("Langue")]
public class ParcEteintsTests
{
    private const string Modele = "Wortmann_AG 1009944;2110345";

    private static List<ScanHistory.PeriodeBreve> Nuits(DateTime du, DateTime au)
    {
        var l = new List<ScanHistory.PeriodeBreve>();
        for (var d = du.Date; d < au.Date; d = d.AddDays(1))
            l.Add(new ScanHistory.PeriodeBreve { Debut = d.AddHours(21), Fin = d.AddDays(1).AddHours(8), FinDeSession = FinDeSession.ArretDemande });
        return l;
    }

    private static ParkComparator.MachineSummary Poste(string nom, string bios = "9005", List<ScanHistory.PeriodeBreve>? periodes = null) =>
        new(nom, new ScanHistory.ScanSummary
        {
            Modele = Modele, VersionBios = bios,
            CarteGraphique = "NVIDIA GeForce RTX 5060 Ti (32.0.16.1088)",
            WheaComposants = ["PCI Express Root Port 0x0:0x1:0x0"],
            PeriodesEteintes = periodes ?? Nuits(new DateTime(2026, 10, 13), new DateTime(2026, 11, 12)),
        });

    private static List<ScanHistory.PeriodeBreve> PosteA()
    {
        var l = Nuits(new DateTime(2026, 10, 13), new DateTime(2026, 10, 21));
        l.Add(new ScanHistory.PeriodeBreve
        {
            Debut = new DateTime(2026, 10, 21, 21, 0, 5), Fin = new DateTime(2026, 11, 12, 8, 2, 6), FinDeSession = FinDeSession.ArretDemande,
        });
        return l;
    }

    private static ParkComparator.Correlation? Carte(params ParkComparator.MachineSummary[] postes)
    {
        var initial = Lang.Current;
        try
        {
            Lang.Apply(AppLanguage.French);
            return ParkComparator.Analyze(postes, new DateTime(2026, 11, 12, 9, 0, 0)).Correlations
                .SingleOrDefault(c => c.Title.Contains("resté", StringComparison.Ordinal) && c.Title.Contains("éteint", StringComparison.Ordinal));
        }
        finally { Lang.Apply(initial); }
    }

    [Fact]
    public void Le_poste_reste_eteint_pendant_que_les_autres_demarraient_est_nomme_et_rien_ne_le_distingue()
    {
        var c = Carte(Poste("POSTE-A", periodes: PosteA()), Poste("POSTE-01"), Poste("POSTE-02"), Poste("POSTE-31"));

        Assert.NotNull(c);
        Assert.Equal("isolé", c!.Kind);
        Assert.Equal(new[] { "POSTE-A" }, c.Machines.ToArray());
        Assert.Contains("POSTE-A : éteint du 21/10 à 21:00 au 12/11 à 08:02 (21 j 11 h), après : arrêt propre, demandé ; pendant ce temps, 3 autre(s) poste(s) du même modèle ont démarré", c.Details);
        Assert.Contains("Soit ces postes n'ont pas été allumés, soit ils ne se sont pas allumés", c.Details);
        Assert.Contains("Rien ne les distingue des 3 autre(s) poste(s) du même modèle sur : version du BIOS, carte graphique et pilote, erreurs matérielles par composant.", c.Details);
    }

    [Fact]
    public void Deux_postes_touches_qui_partagent_un_BIOS_que_les_autres_n_ont_pas()
    {
        var b = Nuits(new DateTime(2026, 10, 13), new DateTime(2026, 11, 9));
        b.Add(new ScanHistory.PeriodeBreve { Debut = new DateTime(2026, 11, 9, 21, 0, 0), Fin = new DateTime(2026, 11, 12, 8, 0, 0), FinDeSession = FinDeSession.ArretDemande });

        var c = Carte(Poste("POSTE-A", "9005", PosteA()), Poste("POSTE-B", "9005", b),
                      Poste("POSTE-01", "9010"), Poste("POSTE-02", "9010"));

        Assert.NotNull(c);
        Assert.Equal("commun", c!.Kind);
        Assert.Equal(new[] { "POSTE-A", "POSTE-B" }, c.Machines.ToArray());
        Assert.Contains("Ce qui les distingue des 2 autre(s) poste(s) du même modèle : version du BIOS : 9005 — les autres : 9010.", c.Details);
    }

    [Fact]
    public void Des_vacances_ou_tout_le_monde_est_eteint_ne_comptent_pas()
    {
        var vacances = new List<ScanHistory.PeriodeBreve>
        {
            new() { Debut = new DateTime(2026, 10, 16, 21, 0, 0), Fin = new DateTime(2026, 11, 2, 8, 0, 0), FinDeSession = FinDeSession.ArretDemande },
        };
        Assert.Null(Carte(Poste("POSTE-A", periodes: vacances), Poste("POSTE-01", periodes: vacances), Poste("POSTE-02", periodes: vacances)));
    }

    [Fact]
    public void Un_autre_modele_ne_sert_pas_de_comparaison()
    {
        var autre = Poste("PORTABLE-1");
        autre.Summary.Modele = "HP ProBook 465";
        Assert.Null(Carte(Poste("POSTE-A", periodes: PosteA()), autre));
    }
}
