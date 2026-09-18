using FaultTracePC.Core;
using FaultTracePC.Core.Analysis;
using Xunit;

namespace FaultTracePC.Tests;

/// <summary>
/// Constaté le 18/09/2026 sur TECH-INFO-2025 : un événement Ntfs 55 nommait « le
/// volume D: », et la lecture du registre proposait, deux cartes plus loin,
/// « D: (Kingston DataTraveler 3.0 USB Device) ». Le logiciel tenait les deux
/// moitiés de la réponse sans jamais les coller.
///
/// Ce que ces tests protègent, c'est la mesure : le rapprochement resserre la piste,
/// il ne la transforme pas en preuve, et il ne doit se déclencher que sur une lettre
/// réellement citée ET réellement absente.
/// </summary>
[Collection("Langue")]
public class VolumeRapprocheTests
{
    private static WinEvent Evenement(string message) => new()
    {
        Category = EventCategory.DiskError,
        Provider = "Ntfs",
        EventId = 55,
        TimeLocal = new DateTime(2026, 9, 14, 11, 8, 0),
        Message = message,
    };

    private static RememberedVolume Support(string lettre, string nom, bool monte = false) => new()
    {
        Letter = lettre,
        IsRemovable = true,
        CurrentlyMounted = monte,
        FriendlyName = nom,
    };

    private static DiagnosticReport Rapport(params RememberedVolume[] supports)
    {
        var r = new DiagnosticReport
        {
            GeneratedAt = new DateTime(2026, 9, 18, 8, 4, 0),
            ScanPeriodDays = 30,
            System = new SystemSnapshot { MachineName = "POSTE-TEST" },
        };
        r.System.StorageHistory = new StorageHistoryInfo { Readable = true, Volumes = [.. supports] };
        return r;
    }

    private static string EnFrancais(Func<string> f)
    {
        var initial = Lang.Current;
        try { Lang.Apply(AppLanguage.French); return f(); }
        finally { Lang.Apply(initial); }
    }

    // ------------------------------------------------------------------
    // Extraction de la lettre
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("Une défaillance a été détectée sur le volume D:.", "D:")]
    [InlineData("Défaillance sur le volume d: durant l'opération", "D:")]
    [InlineData("Le volume E: est plein", "E:")]
    public void La_lettre_citee_est_relevee(string message, string attendue)
        => Assert.Equal(new[] { attendue }, RulesEngine.LettresCitees([Evenement(message)]).ToArray());

    [Theory]
    // Un chemin système cite bien C:, mais le faire remonter rendrait la moitié des
    // messages de Windows « porteurs d'une lettre de volume ».
    [InlineData(@"Impossible d'ouvrir C:\Windows\System32\config")]
    // L'heure n'est pas une lettre : le motif exige une lettre, pas un chiffre.
    [InlineData("Événement survenu à 09:24 puis à 11:08")]
    // Rien d'alphanumérique ne doit précéder, sans quoi « Note: » ferait un volume.
    [InlineData("Note: le périphérique ne répond plus")]
    [InlineData(@"Le périphérique \Device\Harddisk1\DR1 comporte un bloc défectueux.")]
    public void Ce_qui_n_est_pas_une_lettre_de_volume_n_en_devient_pas_une(string message)
        => Assert.Empty(RulesEngine.LettresCitees([Evenement(message)]));

    // ------------------------------------------------------------------
    // Le rapprochement
    // ------------------------------------------------------------------

    [Fact]
    public void Le_volume_cite_et_absent_est_rattache_au_materiel_connu()
    {
        var r = Rapport(Support("D:", "Kingston DataTraveler 3.0 USB Device"));
        var evts = new[] { Evenement("Une défaillance a été détectée dans la structure du système de fichiers sur le volume D:.") };

        var phrase = EnFrancais(() => RulesEngine.VolumeCiteReconnu(r, evts));

        Assert.Contains("D: (Kingston DataTraveler 3.0 USB Device)", phrase);
        // La mesure est dans la phrase, pas seulement dans l'intention.
        Assert.Contains("pas une preuve", phrase);
    }

    [Fact]
    public void Une_lettre_montee_aujourd_hui_ne_declenche_rien()
    {
        // Si le support est là, il n'y a rien à retrouver : la phrase n'a pas lieu d'être.
        var r = Rapport(Support("D:", "Kingston DataTraveler 3.0 USB Device", monte: true));
        var evts = new[] { Evenement("Défaillance sur le volume D:.") };

        Assert.Equal("", EnFrancais(() => RulesEngine.VolumeCiteReconnu(r, evts)));
    }

    [Fact]
    public void Un_support_absent_dont_la_lettre_n_est_pas_citee_ne_declenche_rien()
    {
        // F: est connu et absent, mais aucun événement ne le nomme : le rapprocher
        // reviendrait à désigner un support au hasard.
        var r = Rapport(Support("F:", "SONY WALKMAN USB Device"));
        var evts = new[] { Evenement("Défaillance sur le volume D:.") };

        Assert.Equal("", EnFrancais(() => RulesEngine.VolumeCiteReconnu(r, evts)));
    }

    [Fact]
    public void Sans_lecture_du_registre_aucun_rapprochement_n_est_tente()
    {
        var r = Rapport();
        r.System.StorageHistory = new StorageHistoryInfo
        {
            Readable = false,
            Volumes = [Support("D:", "Kingston DataTraveler 3.0 USB Device")],
        };
        var evts = new[] { Evenement("Défaillance sur le volume D:.") };

        Assert.Equal("", EnFrancais(() => RulesEngine.VolumeCiteReconnu(r, evts)));
    }

    [Fact]
    public void La_carte_du_volume_porte_le_rapprochement()
    {
        var r = Rapport(Support("D:", "Kingston DataTraveler 3.0 USB Device"));
        for (int i = 0; i < 4; i++)
            r.Events.Add(Evenement("Une défaillance a été détectée dans la structure du système de fichiers sur le volume D:."));

        var initial = Lang.Current;
        try { Lang.Apply(AppLanguage.French); new RulesEngine().Analyze(r); }
        finally { Lang.Apply(initial); }

        var carte = r.Findings.First(f => f.Code.StartsWith("disk_event"));
        Assert.Contains("Kingston DataTraveler 3.0 USB Device", carte.Details);
    }
}
