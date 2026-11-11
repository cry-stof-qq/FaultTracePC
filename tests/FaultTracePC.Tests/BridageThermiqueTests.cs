using FaultTracePC.Core.Analysis;
using Xunit;

namespace FaultTracePC.Tests;

/// <summary>
/// Point 120, lot C1 : reconnaître un processeur bridé par la chaleur. Les relevés
/// « chauds » ci-dessous sont RÉELS (postes de l'auteur, 08/11 et 11/11/2026) ; les
/// périodes fraîches qui servent de référence sont simulées et le disent.
/// </summary>
public class BridageThermiqueTests
{
    private static readonly DateTime Jour = new(2026, 11, 11);

    /// <summary>Période fraîche SIMULÉE : 20 relevés au repos et 10 pointes à 4,0 GHz (turbo maximal annoncé par Intel).</summary>
    private static void PeriodeFraicheIntel(BridageThermique b)
    {
        var t = Jour.AddHours(11).AddMinutes(30);
        for (int i = 0; i < 30; i++, t = t.AddSeconds(10))
            b.Ajouter(t, 8, 60, i % 3 == 0 ? 4007 : 1200);
    }

    /// <summary>Essai de charge du 11/11/2026, Intel Core i5-8300H — relevés réels (heure, charge %, °C, MHz).</summary>
    private static readonly (string Heure, double Charge, double Temp, double Freq)[] EssaiIntel =
    {
        ("12:05:42", 20.5, 75, 4007), ("12:05:52", 18.2, 92, 3907), ("12:06:04", 99.2, 98, 3807),
        ("12:06:12", 100, 98, 3707), ("12:06:24", 100, 98, 3506), ("12:06:32", 100, 99, 3707),
        ("12:06:44", 100, 98, 3807), ("12:06:52", 100, 98, 3607), ("12:07:04", 100, 98, 3807),
        ("12:07:12", 100, 99, 3807), ("12:07:24", 100, 96, 3506), ("12:07:32", 100, 96, 3406),
        ("12:07:44", 100, 98, 3506), ("12:07:52", 100, 98, 3506), ("12:08:04", 100, 95, 3306),
        ("12:08:12", 100, 97, 3506), ("12:08:24", 100, 95, 3506), ("12:08:32", 100, 96, 3506),
        ("12:08:44", 100, 96, 3406), ("12:08:52", 100, 99, 3506), ("12:09:04", 30.1, 87, 4007),
        ("12:09:12", 6.4, 70, 902), ("12:09:22", 7, 78, 3907),
    };

    private static DateTime A(string heure) => Jour.Add(TimeSpan.Parse(heure));

    private static void Rejouer(BridageThermique b, IEnumerable<(string Heure, double Charge, double Temp, double Freq)> releves)
    {
        foreach (var r in releves) b.Ajouter(A(r.Heure), r.Charge, r.Temp, r.Freq);
    }

    [Fact]
    public void L_essai_de_charge_Intel_donne_un_episode_de_bridage_d_environ_13_pour_cent()
    {
        var b = new BridageThermique(85);
        PeriodeFraicheIntel(b);
        Rejouer(b, EssaiIntel);

        var bilan = b.Bilan();

        Assert.Equal((double?)4007, bilan.ReferenceMHz);
        Assert.False(bilan.ReferenceSousCharge); // aucun relevé frais ET chargé : référence moins sûre
        var e = Assert.Single(bilan.Episodes);
        Assert.Equal(A("12:07:24"), e.Debut);
        Assert.Equal(A("12:08:52"), e.Fin);
        Assert.Equal(TimeSpan.FromSeconds(88), e.Duree);
        Assert.Equal(3466, e.FrequenceMoyenneMHz);
        Assert.Equal(0.865, e.Rapport);
        Assert.Equal(99, e.TemperatureMax);
        Assert.False(e.SuiviDArretBrutal);
    }

    /// <summary>08/11/2026 10:01–10:05, Ryzen 7 7735U — relevés réels : chaud, mais un ou deux cœurs seulement.</summary>
    private static readonly (string Heure, double Charge, double Temp, double Freq)[] PointesAmd =
    {
        ("10:01:08", 57.4, 98, 4453), ("10:01:18", 45.6, 93, 4453), ("10:01:28", 8.6, 98.9, 4616),
        ("10:01:48", 38.6, 100, 4616), ("10:01:58", 8.4, 97.4, 4616), ("10:02:08", 8.3, 96.1, 4766),
        ("10:02:18", 8.9, 94.9, 4641), ("10:02:38", 8.1, 99.4, 4666), ("10:02:48", 8.7, 99.9, 4716),
        ("10:03:28", 7.7, 100.1, 4641), ("10:03:38", 8.2, 100.2, 4641), ("10:03:48", 8, 100.2, 4566),
        ("10:04:48", 11.3, 100.4, 4417), ("10:05:38", 9.4, 100.2, 4666), ("10:05:58", 10.6, 99, 4641),
    };

    [Fact]
    public void Les_pointes_du_poste_AMD_ne_sont_pas_un_bridage_et_la_frequence_tient_au_chaud()
    {
        var b = new BridageThermique(85);
        // Période fraîche SIMULÉE : chargée, à 4,7 GHz (relevés frais et chargés → référence sûre).
        var t = Jour.AddHours(9);
        for (int i = 0; i < 6; i++, t = t.AddSeconds(10)) b.Ajouter(t, 60, 70, 4716);
        Rejouer(b, PointesAmd);

        var bilan = b.Bilan();

        Assert.True(bilan.ReferenceSousCharge);
        Assert.Equal((double?)4716, bilan.ReferenceMHz);
        Assert.Empty(bilan.Episodes);
        Assert.Equal(PointesAmd.Length, bilan.RelevesChaudsAvecFrequence);
        Assert.True(bilan.RapportMedianAuChaud >= BridageThermique.RapportBride);
    }

    [Fact]
    public void Moins_de_60_secondes_ou_une_coupure_ne_font_pas_un_episode()
    {
        var b = new BridageThermique(85);
        PeriodeFraicheIntel(b);
        var t = Jour.AddHours(14);
        // 50 s de bridage, puis un relevé frais, puis 50 s de bridage : deux morceaux trop courts.
        for (int i = 0; i < 6; i++, t = t.AddSeconds(10)) b.Ajouter(t, 100, 96, 3400);
        b.Ajouter(t, 100, 88, 3400); t = t.AddSeconds(10);
        for (int i = 0; i < 6; i++, t = t.AddSeconds(10)) b.Ajouter(t, 100, 96, 3400);
        // Un trou de 40 s dans le journal coupe aussi.
        t = t.AddSeconds(30);
        for (int i = 0; i < 6; i++, t = t.AddSeconds(10)) b.Ajouter(t, 100, 96, 3400);

        Assert.Empty(b.Bilan().Episodes);
    }

    [Fact]
    public void Un_arret_brutal_juste_apres_est_rattache_a_l_episode()
    {
        var b = new BridageThermique(85);
        PeriodeFraicheIntel(b);
        Rejouer(b, EssaiIntel.Take(20)); // s'arrête à 12:08:52, en plein bridage
        b.ArretBrutal(A("12:08:52"));

        Assert.True(Assert.Single(b.Bilan().Episodes).SuiviDArretBrutal);
    }

    [Fact]
    public void Sans_assez_de_releves_frais_on_ne_conclut_rien()
    {
        var b = new BridageThermique(85);
        Rejouer(b, EssaiIntel); // 3 relevés frais seulement

        var bilan = b.Bilan();

        Assert.Null(bilan.ReferenceMHz);
        Assert.Null(bilan.RapportMedianAuChaud);
        Assert.Empty(bilan.Episodes);
    }

    [Fact]
    public void Un_releve_sans_frequence_ou_sans_temperature_est_ignore()
    {
        var b = new BridageThermique(85);
        for (int i = 0; i < 40; i++) b.Ajouter(Jour.AddSeconds(10 * i), 100, null, 3000);
        for (int i = 40; i < 80; i++) b.Ajouter(Jour.AddSeconds(10 * i), 100, 96, null);

        var bilan = b.Bilan();
        Assert.Null(bilan.ReferenceMHz);
        Assert.Equal(0, bilan.RelevesChaudsAvecFrequence);
    }
}
