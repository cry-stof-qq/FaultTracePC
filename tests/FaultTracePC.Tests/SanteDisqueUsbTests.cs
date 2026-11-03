using FaultTracePC.Core;
using FaultTracePC.Core.Report;
using Xunit;

namespace FaultTracePC.Tests;

/// <summary>
/// Point 119, constaté le 03/11/2026 : un disque dur externe USB renvoyait des
/// centaines de « blocs défectueux » pendant que la fiche « Disques physiques »
/// l'affichait « santé : Sain », et l'alerte conseillait de « mettre à jour le
/// firmware du SSD ».
/// </summary>
[Collection("Langue")]
public class SanteDisqueUsbTests
{
    private static T EnFrancais<T>(Func<T> action)
    {
        var initial = Lang.Current;
        try { Lang.Apply(AppLanguage.French); return action(); }
        finally { Lang.Apply(initial); }
    }

    private static DiskInfo Disque(string modele, string interfaceType, DiskHealth sante) => new()
    {
        Model = modele, InterfaceType = interfaceType, Health = sante, WmiStatus = "OK",
    };

    [Fact]
    public void Un_disque_USB_dit_sain_n_est_pas_affiche_sain_sans_reserve()
    {
        var d = Disque("TOSHIBA EXTERNAL_USB USB Device", "USB", DiskHealth.Healthy);

        var texte = EnFrancais(() => HtmlReportGenerator.SanteLisible(d));

        Assert.StartsWith("non mesurable à travers l'USB", texte);
        Assert.Contains(EnFrancais(() => DiskHealth.Healthy.Label()), texte); // ce que dit Windows reste écrit
    }

    [Fact]
    public void Un_disque_USB_sans_interface_lue_est_reconnu_a_son_modele()
    {
        Assert.True(HtmlReportGenerator.EstUsb(Disque("TOSHIBA EXTERNAL_USB USB Device", "", DiskHealth.Healthy)));
        Assert.False(HtmlReportGenerator.EstUsb(Disque("Samsung SSD 980", "SCSI", DiskHealth.Healthy)));
    }

    [Fact]
    public void Un_mauvais_etat_rapporte_pour_un_disque_USB_reste_affiche_tel_quel()
    {
        var d = Disque("TOSHIBA EXTERNAL_USB USB Device", "USB", DiskHealth.Warning);

        Assert.Equal(EnFrancais(() => DiskHealth.Warning.Label()), EnFrancais(() => HtmlReportGenerator.SanteLisible(d)));
    }

    [Fact]
    public void Un_disque_interne_sain_reste_affiche_sain()
    {
        var d = Disque("Samsung SSD 980", "SCSI", DiskHealth.Healthy);

        Assert.Equal(EnFrancais(() => DiskHealth.Healthy.Label()), EnFrancais(() => HtmlReportGenerator.SanteLisible(d)));
    }

    [Fact]
    public void L_alerte_d_erreur_disque_ne_conseille_plus_le_firmware_SSD_a_tout_disque()
    {
        var a = new PreventiveAlert
        {
            Time = new DateTime(2026, 11, 3, 20, 33, 0), RuleId = "disk_event", Level = "warn",
            Extract = "Le périphérique \\Device\\Harddisk1\\DR6 comporte un bloc défectueux.",
        };

        Assert.True(EnFrancais(() => AlertCatalog.Localize(a)));

        Assert.DoesNotContain("mettre à jour le firmware du SSD", a.Recommendation);
        Assert.Contains("Disque externe : le recopier avant tout, sans lancer de réparation dessus.", a.Recommendation);
        Assert.Contains("SSD interne : vérifier aussi son firmware", a.Recommendation);
    }
}
