using FaultTracePC.Core;
using Xunit;

namespace FaultTracePC.Tests;

/// <summary>
/// Point 43. L'archive des alertes est ce qui permettra un jour de dire « ce poste
/// alerte depuis six mois ». Deux erreurs la rendraient inutile sans rien casser :
/// archiver le même incident à chaque actualisation, ou perdre silencieusement ce
/// qu'on croyait conserver.
///
/// Ces tests écrivent dans le VRAI dossier d'archive — celui des Documents — parce
/// que c'est lui que le code calcule. Ils utilisent donc des noms de poste qui ne
/// peuvent appartenir à personne, et suppriment leur fichier en sortant.
/// </summary>
[Collection("Langue")]
public class ArchiveDesAlertesTests
{
    private static string PosteDeTest() => "ZZTEST-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

    private static void Nettoyer(string poste)
    {
        var f = ArchiveDesAlertes.FichierDe(poste);
        if (f.Length > 0 && File.Exists(f)) File.Delete(f);
    }

    private static PreventiveAlert Alerte(DateTime t, string regle, string titre = "titre") =>
        new() { Time = t, RuleId = regle, Level = "warn", Title = titre, Details = "détail accentué é à ù", Value = 91.5 };

    [Fact]
    public void Ce_qui_est_archive_se_relit()
    {
        var poste = PosteDeTest();
        try
        {
            var t = new DateTime(2026, 9, 19, 14, 30, 0);
            Assert.Equal(2, ArchiveDesAlertes.Archiver(poste, [Alerte(t, "cpu_temp"), Alerte(t.AddMinutes(5), "commit")]));

            var relues = ArchiveDesAlertes.Lire(poste);

            Assert.Equal(2, relues.Count);
            // La plus récente d'abord : c'est celle qu'on regarde.
            Assert.Equal("commit", relues[0].RuleId);
            Assert.Equal("détail accentué é à ù", relues[0].Details);
            Assert.Equal(91.5, relues[0].Value);
        }
        finally { Nettoyer(poste); }
    }

    [Fact]
    public void La_meme_alerte_relue_dix_fois_ne_s_archive_qu_une_fois()
    {
        // LE POINT CENTRAL. La console relit sept jours d'alertes à CHAQUE
        // actualisation : sans dédoublonnage, une alerte du lundi serait archivée
        // une fois par actualisation jusqu'au lundi suivant.
        var poste = PosteDeTest();
        try
        {
            var a = Alerte(new DateTime(2026, 9, 19, 14, 30, 0), "cpu_temp");

            Assert.Equal(1, ArchiveDesAlertes.Archiver(poste, [a]));
            for (int i = 0; i < 9; i++) Assert.Equal(0, ArchiveDesAlertes.Archiver(poste, [a]));

            Assert.Single(ArchiveDesAlertes.Lire(poste));
        }
        finally { Nettoyer(poste); }
    }

    [Fact]
    public void Deux_declenchements_de_la_meme_regle_sont_deux_alertes()
    {
        // Ce n'est pas un doublon : c'est la deuxième fois que la machine chauffe,
        // et c'est précisément ce qu'on veut pouvoir compter.
        var poste = PosteDeTest();
        try
        {
            var t = new DateTime(2026, 9, 19, 14, 30, 0);
            ArchiveDesAlertes.Archiver(poste, [Alerte(t, "cpu_temp")]);
            ArchiveDesAlertes.Archiver(poste, [Alerte(t.AddHours(3), "cpu_temp")]);

            Assert.Equal(2, ArchiveDesAlertes.Lire(poste).Count);
        }
        finally { Nettoyer(poste); }
    }

    [Fact]
    public void La_periode_filtre_sans_rien_effacer()
    {
        var poste = PosteDeTest();
        try
        {
            ArchiveDesAlertes.Archiver(poste,
            [
                Alerte(DateTime.Now.AddDays(-2), "recente"),
                Alerte(DateTime.Now.AddDays(-200), "ancienne"),
            ]);

            Assert.Single(ArchiveDesAlertes.Lire(poste, 90));
            Assert.Equal(1, ArchiveDesAlertes.Compter(poste, 90));

            // RIEN N'A ÉTÉ EFFACÉ : la période filtre l'affichage, elle ne touche
            // pas au fichier. C'est la décision du 19/09/2026.
            Assert.Equal(2, ArchiveDesAlertes.Lire(poste).Count);
        }
        finally { Nettoyer(poste); }
    }

    [Theory]
    [InlineData("POSTE-01", true)]
    [InlineData("poste-01.exemple.fr", true)]   // ramené au nom court
    [InlineData("POSTE-01$", true)]             // compte d'ordinateur
    [InlineData("..", false)]
    [InlineData("../../ailleurs", false)]
    [InlineData("C:\\Windows\\System32", false)]
    [InlineData("poste 01", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Un_nom_qui_n_est_pas_un_nom_de_poste_ne_devient_pas_un_chemin(string? poste, bool accepte)
    {
        // GARDE-FOU. Un nom venu d'ailleurs qui contiendrait « .. » ou une barre
        // oblique désignerait un fichier ailleurs sur le disque.
        var fichier = ArchiveDesAlertes.FichierDe(poste);

        Assert.Equal(accepte, fichier.Length > 0);
        if (accepte) Assert.StartsWith(ArchiveDesAlertes.Dossier, fichier);
    }

    [Fact]
    public void Un_nom_refuse_est_signale_pas_ignore()
    {
        var notes = new List<string>();

        Assert.Equal(0, ArchiveDesAlertes.Archiver("poste 01", [Alerte(DateTime.Now, "x")], notes));
        Assert.NotEmpty(notes);
    }

    [Fact]
    public void Un_poste_sans_archive_rend_une_liste_vide_sans_lever()
    {
        Assert.Empty(ArchiveDesAlertes.Lire(PosteDeTest()));
        Assert.Equal(0, ArchiveDesAlertes.Compter(PosteDeTest(), 90));
    }

    [Fact]
    public void Le_fichier_ecrit_ne_contient_que_de_l_ascii()
    {
        // Le sérialiseur de .NET échappe tout caractère non ASCII : la question de
        // l'encodage ne se pose donc pas, quelle que soit la façon dont le fichier
        // sera relu un jour.
        var poste = PosteDeTest();
        try
        {
            ArchiveDesAlertes.Archiver(poste, [Alerte(DateTime.Now, "cpu_temp", "température élevée")]);

            var octets = File.ReadAllBytes(ArchiveDesAlertes.FichierDe(poste));
            Assert.All(octets, o => Assert.True(o < 128, $"octet non ASCII : {o}"));
        }
        finally { Nettoyer(poste); }
    }

    [Fact]
    public void Une_ligne_abimee_n_empeche_pas_de_lire_les_autres()
    {
        var poste = PosteDeTest();
        try
        {
            ArchiveDesAlertes.Archiver(poste, [Alerte(new DateTime(2026, 9, 19, 10, 0, 0), "bonne")]);
            File.AppendAllLines(ArchiveDesAlertes.FichierDe(poste), ["ceci n'est pas du JSON"]);
            ArchiveDesAlertes.Archiver(poste, [Alerte(new DateTime(2026, 9, 19, 11, 0, 0), "autre")]);

            Assert.Equal(2, ArchiveDesAlertes.Lire(poste).Count);
        }
        finally { Nettoyer(poste); }
    }

    // ==================================================================
    // L'ARCHIVE BRANCHÉE SUR L'ACTUALISATION — deuxième partie du point 43.
    // Ces trois tests décrivent ce que la console fait réellement à chaque
    // actualisation, et ce dont la fenêtre d'historique a besoin pour afficher
    // l'archive des mois plus tard.
    // ==================================================================

    [Fact]
    public void Ce_que_le_poste_a_oublie_reste_dans_l_archive()
    {
        // LE POINT 43 TOUT ENTIER TIENT DANS CE TEST.
        // Le poste ne garde que sept jours : à chaque actualisation, la console
        // reçoit une fenêtre GLISSANTE. L'alerte la plus ancienne finit par en
        // sortir. Si l'archive se contentait de recopier la dernière fenêtre
        // reçue, elle oublierait exactement en même temps que le poste — et ne
        // servirait à rien.
        var poste = PosteDeTest();
        try
        {
            var lundi   = Alerte(new DateTime(2026, 9, 7, 9, 0, 0), "cpu_temp");
            var mercredi = Alerte(new DateTime(2026, 9, 9, 9, 0, 0), "commit");
            var vendredi = Alerte(new DateTime(2026, 9, 11, 9, 0, 0), "gpu_temp");

            // Actualisation 1 : le poste montre lundi et mercredi.
            Assert.Equal(2, ArchiveDesAlertes.Archiver(poste, [lundi, mercredi]));

            // Actualisation 2 : il montre les trois. Seule vendredi est nouvelle.
            Assert.Equal(1, ArchiveDesAlertes.Archiver(poste, [lundi, mercredi, vendredi]));

            // Actualisation 3 : lundi est sorti de ses sept jours. Rien de neuf,
            // et surtout rien de perdu.
            Assert.Equal(0, ArchiveDesAlertes.Archiver(poste, [mercredi, vendredi]));

            var archivees = ArchiveDesAlertes.Lire(poste);
            Assert.Equal(3, archivees.Count);
            Assert.Contains(archivees, a => a.RuleId == "cpu_temp");
        }
        finally { Nettoyer(poste); }
    }

    [Fact]
    public void Le_decompte_separe_les_critiques_des_alertes_a_surveiller()
    {
        // C'est le contrat de la colonne « Alertes 90 j » : dix alertes dont
        // aucune critique et dix alertes dont six critiques ne demandent pas la
        // même chose, et la colonne doit pouvoir le dire.
        var poste = PosteDeTest();
        try
        {
            ArchiveDesAlertes.Archiver(poste,
            [
                new PreventiveAlert { Time = DateTime.Now.AddDays(-1), RuleId = "cpu_temp", Level = "crit", Value = 96 },
                new PreventiveAlert { Time = DateTime.Now.AddDays(-2), RuleId = "cpu_temp", Level = "warn", Value = 88 },
                new PreventiveAlert { Time = DateTime.Now.AddDays(-3), RuleId = "commit",   Level = "crit", Value = 98 },
                new PreventiveAlert { Time = DateTime.Now.AddDays(-200), RuleId = "commit", Level = "crit", Value = 99 },
            ]);

            var surQuatreVingtDix = ArchiveDesAlertes.Lire(poste, 90);

            Assert.Equal(3, surQuatreVingtDix.Count);
            Assert.Equal(2, surQuatreVingtDix.Count(a => a.Level == "crit"));

            // Et l'ancienne n'a pas disparu pour autant.
            Assert.Equal(4, ArchiveDesAlertes.Compter(poste));
        }
        finally { Nettoyer(poste); }
    }

    [Fact]
    public void Une_alerte_archivee_se_relit_dans_la_langue_d_aujourd_hui()
    {
        // CE QUE LA FENÊTRE D'HISTORIQUE ATTEND DE L'ARCHIVE.
        // Une alerte écrite il y a six mois porte la phrase de l'époque, dans la
        // langue de l'époque. Le fichier conserve le FAIT — la règle et la valeur
        // mesurée — ce qui permet de refabriquer la phrase aujourd'hui. Si l'un
        // des deux manquait au fichier, l'historique afficherait du français au
        // milieu d'une fenêtre anglaise, sans qu'aucun autre test ne le voie.
        var poste = PosteDeTest();
        var initiale = Lang.Current;
        try
        {
            Lang.Apply(AppLanguage.French);
            var a = new PreventiveAlert { Time = DateTime.Now.AddDays(-3), RuleId = "cpu_temp", Level = "crit", Value = 96 };
            AlertCatalog.Localize(a);
            Assert.Contains("Température", a.Title);

            ArchiveDesAlertes.Archiver(poste, [a]);

            Lang.Apply(AppLanguage.English);
            var relues = ArchiveDesAlertes.Lire(poste, 90);
            AlertCatalog.LocalizeAll(relues);

            Assert.Single(relues);
            Assert.Contains("Processor temperature", relues[0].Title);
        }
        finally { Lang.Apply(initiale); Nettoyer(poste); }
    }
}
