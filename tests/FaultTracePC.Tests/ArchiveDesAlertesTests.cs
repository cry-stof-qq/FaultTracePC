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
}
