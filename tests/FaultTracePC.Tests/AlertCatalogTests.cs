using FaultTracePC.Core;
using Xunit;

namespace FaultTracePC.Tests;

/// <summary>
/// Refabrication du texte des alertes préventives à partir du fait observé.
///
/// L'enjeu n'est pas la traduction : c'est qu'une alerte écrite hier en français
/// se relise aujourd'hui en anglais sans que rien du fait ne se perde — ni la
/// valeur mesurée, ni le nom du disque, ni l'extrait cité de Windows.
/// </summary>
[Collection("Langue")]
public class AlertCatalogTests
{
    private static PreventiveAlert Alerte(string ruleId, string level = "warn",
                                          double? value = null, string? extract = null) =>
        new()
        {
            Time = new DateTime(2026, 8, 17, 14, 3, 0),
            RuleId = ruleId,
            Level = level,
            Value = value,
            Extract = extract,
            Title = "TEXTE D'ORIGINE",
            Details = "DÉTAIL D'ORIGINE",
            Recommendation = "CONSEIL D'ORIGINE",
        };

    private static void EnLangue(AppLanguage langue, Action action)
    {
        var initial = Lang.Current;
        try { Lang.Apply(langue); action(); }
        finally { Lang.Apply(initial); }
    }

    // ==================================================================

    [Fact]
    public void Une_alerte_de_seuil_se_relit_dans_la_langue_en_cours()
    {
        var a = Alerte("cpu_temp", "crit", value: 92);

        EnLangue(AppLanguage.French, () =>
        {
            Assert.True(AlertCatalog.Localize(a));
            Assert.Contains("Température du processeur", a.Title);
            Assert.Contains("92", a.Title);
        });

        EnLangue(AppLanguage.English, () =>
        {
            Assert.True(AlertCatalog.Localize(a));
            Assert.Contains("Processor temperature", a.Title);
            Assert.Contains("92", a.Title);
            Assert.DoesNotContain("Température", a.Details);
        });
    }

    [Fact]
    public void La_valeur_mesuree_survit_a_la_bascule()
    {
        // C'est le cœur du mécanisme : le CHIFFRE vient du fichier, la PHRASE est
        // refabriquée autour. Perdre le chiffre viderait l'alerte de son contenu.
        var a = Alerte("gpu_temp", "warn", value: 87);
        EnLangue(AppLanguage.English, () => AlertCatalog.Localize(a));
        Assert.Contains("87", a.Title);
        Assert.Contains("87", a.Details);
    }

    [Fact]
    public void Le_nom_du_disque_est_lu_dans_l_identifiant_de_regle()
    {
        // Il n'est stocké nulle part ailleurs : l'identifiant le porte.
        var a = Alerte("disk_health_Samsung SSD 980", "crit");

        EnLangue(AppLanguage.English, () =>
        {
            Assert.True(AlertCatalog.Localize(a));
            Assert.Contains("Samsung SSD 980", a.Title);
            Assert.Contains("Drive in poor health", a.Title);
        });

        EnLangue(AppLanguage.French, () =>
        {
            AlertCatalog.Localize(a);
            Assert.Contains("Samsung SSD 980", a.Title);
            Assert.Contains("mauvaise santé", a.Title);
        });
    }

    [Fact]
    public void L_etat_du_disque_se_deduit_du_niveau()
    {
        // « crit » a été émis pour un disque défaillant, « warn » pour un disque à
        // surveiller : le niveau suffit à retrouver l'état sans le stocker deux fois.
        var grave = Alerte("disk_health_WDC", "crit");
        var leger = Alerte("disk_health_WDC", "warn");

        EnLangue(AppLanguage.English, () =>
        {
            AlertCatalog.Localize(grave);
            AlertCatalog.Localize(leger);
        });

        Assert.NotEqual(grave.Details, leger.Details);
    }

    [Fact]
    public void Les_regles_sans_donnee_se_refabriquent_toujours()
    {
        foreach (var id in new[] { "whea", "power41" })
        {
            var a = Alerte(id, "crit");
            EnLangue(AppLanguage.English, () => Assert.True(AlertCatalog.Localize(a)));
            Assert.DoesNotContain("D'ORIGINE", a.Title);
        }
    }

    [Fact]
    public void L_extrait_de_Windows_est_conserve_et_replace()
    {
        var a = Alerte("disk_event", "warn", extract: "\\Device\\Harddisk1\\DR1");
        EnLangue(AppLanguage.English, () => Assert.True(AlertCatalog.Localize(a)));
        Assert.Contains("Disk error reported by Windows", a.Title);
        Assert.Contains("\\Device\\Harddisk1\\DR1", a.Details);
    }

    [Fact]
    public void Sans_l_extrait_le_texte_d_origine_est_laisse_intact()
    {
        // Cas des alertes écrites AVANT la 1.3.0 : le champ n'existait pas. Une
        // phrase dans la mauvaise langue vaut mieux qu'une phrase amputée du fait
        // qu'elle rapporte.
        var a = Alerte("exhaustion", "crit");   // pas d'extrait
        EnLangue(AppLanguage.English, () => Assert.False(AlertCatalog.Localize(a)));
        Assert.Equal("TEXTE D'ORIGINE", a.Title);
        Assert.Equal("DÉTAIL D'ORIGINE", a.Details);
        Assert.Equal("CONSEIL D'ORIGINE", a.Recommendation);
    }

    [Fact]
    public void Une_regle_inconnue_laisse_le_texte_d_origine()
    {
        // Une alerte écrite par une version PLUS RÉCENTE, dont cette version ne
        // connaît pas la règle. On n'invente rien.
        var a = Alerte("regle_du_futur", "crit", value: 1);
        EnLangue(AppLanguage.English, () => Assert.False(AlertCatalog.Localize(a)));
        Assert.Equal("TEXTE D'ORIGINE", a.Title);
    }

    [Fact]
    public void Une_regle_de_seuil_sans_valeur_laisse_le_texte_d_origine()
    {
        // Le chiffre fait partie de la phrase : sans lui, elle serait fausse.
        var a = Alerte("cpu_temp", "crit");   // pas de valeur
        EnLangue(AppLanguage.English, () => Assert.False(AlertCatalog.Localize(a)));
        Assert.Equal("TEXTE D'ORIGINE", a.Title);
    }

    [Fact]
    public void Les_processus_dominants_ne_sont_ajoutes_que_s_ils_existent()
    {
        var avec = Alerte("commit", "crit", value: 96, extract: "vmmem (12 Go)");
        var sans = Alerte("commit", "crit", value: 96);

        EnLangue(AppLanguage.English, () =>
        {
            Assert.True(AlertCatalog.Localize(avec));
            Assert.True(AlertCatalog.Localize(sans));   // la valeur suffit
        });

        Assert.Contains("vmmem (12 Go)", avec.Details);
        Assert.DoesNotContain("Dominant processes", sans.Details);
    }

    [Fact]
    public void LocalizeAll_ne_touche_que_ce_qu_elle_sait_refaire()
    {
        var liste = new List<PreventiveAlert>
        {
            Alerte("cpu_temp", "crit", value: 92),
            Alerte("regle_du_futur", "warn"),
        };

        EnLangue(AppLanguage.English, () => AlertCatalog.LocalizeAll(liste));

        Assert.DoesNotContain("D'ORIGINE", liste[0].Title);
        Assert.Equal("TEXTE D'ORIGINE", liste[1].Title);
    }
}
