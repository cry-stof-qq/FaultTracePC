using FaultTracePC.Core;
using FaultTracePC.Core.Analysis;
using FaultTracePC.Core.Collectors;
using Xunit;

namespace FaultTracePC.Tests;

/// <summary>
/// Mettre un nom sur un support qui n'est plus branché.
///
/// Constaté le 18/09/2026 sur le poste de l'auteur : 479 erreurs de bloc défectueux sur
/// « \Device\Harddisk1 », un numéro qui n'existait plus au moment de l'analyse. Le
/// rapport ne savait écrire que « un disque qui portait le numéro 1 ». Windows retient
/// pourtant, dans SYSTEM\MountedDevices et Enum\USBSTOR, quelle lettre a été montée
/// par quel matériel.
///
/// Ce que ces tests verrouillent surtout, c'est la prudence : le rapprochement est une
/// PISTE, et « pas pu regarder » ne doit jamais se lire « rien trouvé ».
/// </summary>
[Collection("Langue")]
public class SupportAbsentIdentifieTests
{
    private static T EnFrancais<T>(Func<T> f)
    {
        var initial = Lang.Current;
        try { Lang.Apply(AppLanguage.French); return f(); }
        finally { Lang.Apply(initial); }
    }

    [Theory]
    [InlineData(@"\DosDevices\D:", "D:")]
    [InlineData(@"\DosDevices\c:", "C:")]
    [InlineData(@"\??\Volume{8c3a1f10-0000-0000-0000-100000000000}", "")]
    [InlineData(@"\DosDevices\Pipe", "")]
    [InlineData("", "")]
    public void Seules_les_lettres_de_lecteur_sont_retenues(string valeur, string attendu)
        => Assert.Equal(attendu, StorageHistoryCollector.ExtraireLettre(valeur));

    [Fact]
    public void Un_chemin_usb_livre_le_materiel_sans_le_numero_de_serie()
    {
        // Le numéro de série est un identifiant matériel unique. Il n'apporte rien au
        // diagnostic et n'a donc rien à faire dans un rapport, au même titre que
        // l'adresse MAC que ce logiciel masque déjà.
        const string chemin =
            @"\??\USBSTOR#Disk&Ven_SanDisk&Prod_Cruzer_Blade&Rev_1.00#4C530001234567890123&0#{53f56307-b6bf-11d0-94f2-00a0c91efb8b}";

        var id = StorageHistoryCollector.ExtraireIdentifiant(chemin);

        Assert.Equal("Disk&Ven_SanDisk&Prod_Cruzer_Blade&Rev_1.00", id);
        Assert.DoesNotContain("4C530001234567890123", id);
    }

    [Theory]
    // Un disque fixe ne laisse pas de chemin de périphérique mais une signature binaire
    // (MBR), ou un identifiant de gestionnaire de volumes (GPT). Décodées en UTF-16,
    // ces données ne ressemblent à rien : aucune ne doit passer pour un support USB.
    [InlineData("DMIO:ID:")]
    [InlineData("")]
    [InlineData(@"\??\IDE#DiskSamsung_SSD_980#5&1f2e3d4c&0&0.0.0")]
    [InlineData("")]
    public void Un_disque_fixe_n_est_pas_pris_pour_un_support_amovible(string donnee)
        => Assert.Equal("", StorageHistoryCollector.ExtraireIdentifiant(donnee));

    private static RememberedVolume Volume(string lettre, bool amovible, bool monte, string id = "", string joli = "") => new()
    {
        Letter = lettre,
        IsRemovable = amovible,
        CurrentlyMounted = monte,
        DeviceId = id,
        FriendlyName = joli,
    };

    [Fact]
    public void Le_nom_lisible_prime_et_l_identifiant_materiel_sert_de_repli()
    {
        Assert.Equal("SanDisk Cruzer Blade USB Device",
            Volume("D:", true, false, "Disk&Ven_SanDisk", "SanDisk Cruzer Blade USB Device").Label);
        Assert.Equal("Disk&Ven_SanDisk",
            Volume("D:", true, false, "Disk&Ven_SanDisk").Label);
    }

    [Fact]
    public void Seuls_les_amovibles_non_montes_sont_des_candidats()
    {
        var h = new StorageHistoryInfo
        {
            Readable = true,
            Volumes =
            [
                Volume("C:", amovible: false, monte: true),
                Volume("D:", amovible: true, monte: false, joli: "SanDisk Cruzer Blade USB Device"),
                Volume("E:", amovible: true, monte: true, joli: "Kingston DataTraveler USB Device"),
            ],
        };

        var seul = Assert.Single(h.AmoviblesAbsents);
        Assert.Equal("D:", seul.Letter);
    }

    [Fact]
    public void Pas_pu_regarder_ne_se_lit_pas_rien_trouve()
    {
        var r = new DiagnosticReport();
        r.System.StorageHistory = new StorageHistoryInfo
        {
            Readable = false,
            Note = "La lecture de l'historique des montages demande des droits d'administrateur : elle n'a pas pu être faite.",
        };

        var phrase = EnFrancais(() => RulesEngine.PistesSupportsAbsents(r));

        Assert.Contains("droits d'administrateur", phrase);
        // Surtout : aucune affirmation sur ce que Windows connaîtrait ou non.
        Assert.DoesNotContain("ne sont pas montés", phrase);
    }

    [Fact]
    public void Aucun_candidat_ne_produit_aucune_phrase()
    {
        var r = new DiagnosticReport();
        r.System.StorageHistory = new StorageHistoryInfo
        {
            Readable = true,
            Volumes = [Volume("C:", amovible: false, monte: true)],
        };

        Assert.Equal("", EnFrancais(() => RulesEngine.PistesSupportsAbsents(r)));
    }

    [Fact]
    public void Le_support_est_nomme_et_annonce_comme_une_piste()
    {
        var r = new DiagnosticReport();
        r.System.StorageHistory = new StorageHistoryInfo
        {
            Readable = true,
            Volumes = [Volume("D:", amovible: true, monte: false, joli: "SanDisk Cruzer Blade USB Device")],
        };

        var phrase = EnFrancais(() => RulesEngine.PistesSupportsAbsents(r));

        Assert.Contains("D: (SanDisk Cruzer Blade USB Device)", phrase);
        // Le mot compte : rien ne relie une lettre au numéro de disque du jour.
        Assert.Contains("PISTE", phrase);
        Assert.Contains("pas une identification", phrase);
    }

    [Fact]
    public void La_carte_du_support_debranche_porte_le_nom_propose()
    {
        var r = new DiagnosticReport
        {
            GeneratedAt = new DateTime(2026, 9, 18, 5, 7, 0),
            ScanPeriodDays = 30,
            System = new SystemSnapshot { MachineName = "POSTE-TEST" },
        };
        for (int i = 0; i < 4; i++)
            r.Events.Add(new WinEvent
            {
                Category = EventCategory.DiskError,
                Provider = "disk",
                EventId = 7,
                Message = @"Le périphérique \Device\Harddisk1\DR1 comporte un bloc défectueux.",
                TimeLocal = new DateTime(2026, 9, 11, 9, 24, 0).AddMinutes(i),
            });
        r.System.Disks.Add(new DiskInfo { Model = "RPEYJ1T24MML1AWX", Index = 0, InterfaceType = "SCSI", Health = DiskHealth.Healthy });
        r.System.StorageHistory = new StorageHistoryInfo
        {
            Readable = true,
            Volumes = [Volume("D:", amovible: true, monte: false, joli: "SanDisk Cruzer Blade USB Device")],
        };

        EnFrancais(() => { new RulesEngine().Analyze(r); return 0; });

        var carte = r.Findings.First(f => f.Code == "disk_event");
        Assert.Contains("ABSENT", carte.Details);
        Assert.Contains("SanDisk Cruzer Blade USB Device", carte.Details);
        Assert.Contains("PISTE", carte.Details);
    }
}
