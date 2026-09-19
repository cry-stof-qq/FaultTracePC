using FaultTracePC.Core;
using Xunit;

namespace FaultTracePC.Tests;

/// <summary>
/// Point 64, lot D. Retrouver le dernier journal pour reproposer les postes en
/// échec. Ce qui se teste ici est le choix DU fichier : se tromper de journal
/// ferait recocher les échecs d'avant-hier, ce que rien à l'écran ne signalerait.
/// </summary>
public class DernierJournalTests
{
    private static string Dossier()
    {
        var d = Path.Combine(Path.GetTempPath(), "ftpc_journaux_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(d);
        return d;
    }

    [Fact]
    public void Le_plus_recent_se_reconnait_a_son_nom_pas_a_la_date_du_fichier()
    {
        // LE POINT CENTRAL. On écrit l'ANCIEN en dernier, donc sa date de fichier
        // est la plus récente. Si le tri s'appuyait dessus, il gagnerait — et on
        // recocherait les échecs d'une autre exécution.
        var dossier = Dossier();
        try
        {
            var recent = Path.Combine(dossier, "deploiement_2026-09-19_153000.jsonl");
            var ancien = Path.Combine(dossier, "deploiement_2026-09-18_090000.jsonl");

            File.WriteAllText(recent, "");
            Thread.Sleep(20);
            File.WriteAllText(ancien, "");

            Assert.Equal(recent, ParkDeployment.DernierJournal(dossier));
        }
        finally { Directory.Delete(dossier, true); }
    }

    [Fact]
    public void Les_deux_familles_de_journaux_comptent()
    {
        var dossier = Dossier();
        try
        {
            File.WriteAllText(Path.Combine(dossier, "deploiement_2026-09-19_090000.jsonl"), "");
            var verif = Path.Combine(dossier, "verification_2026-09-19_170000.jsonl");
            File.WriteAllText(verif, "");

            Assert.Equal(verif, ParkDeployment.DernierJournal(dossier));
        }
        finally { Directory.Delete(dossier, true); }
    }

    [Fact]
    public void Un_fichier_etranger_au_dossier_n_est_pas_un_journal()
    {
        var dossier = Dossier();
        try
        {
            File.WriteAllText(Path.Combine(dossier, "notes_2026-12-31_235959.jsonl"), "");

            Assert.Equal("", ParkDeployment.DernierJournal(dossier));
        }
        finally { Directory.Delete(dossier, true); }
    }

    [Fact]
    public void Un_dossier_absent_ou_vide_ne_leve_pas()
    {
        Assert.Equal("", ParkDeployment.DernierJournal(Path.Combine(Path.GetTempPath(), "ftpc_jamais_" + Guid.NewGuid().ToString("N"))));

        var vide = Dossier();
        try { Assert.Equal("", ParkDeployment.DernierJournal(vide)); }
        finally { Directory.Delete(vide, true); }
    }

    [Theory]
    [InlineData("deploiement_2026-09-19_153000.jsonl", "deploiement")]
    [InlineData("verification_2026-09-19_153000.jsonl", "verification")]
    [InlineData("autre_2026-09-19_153000.jsonl", "")]
    [InlineData("", "")]
    public void La_nature_du_journal_se_lit_dans_son_nom(string nom, string attendu)
        => Assert.Equal(attendu, ParkDeployment.NatureDuJournal(nom));

    [Fact]
    public void Le_moment_se_lit_dans_le_nom()
    {
        var moment = ParkDeployment.MomentDuJournal(@"C:\x\deploiement_2026-09-19_153042.jsonl");

        Assert.NotNull(moment);
        Assert.Equal(new DateTime(2026, 9, 19, 15, 30, 42), moment!.Value);
    }

    [Theory]
    [InlineData("deploiement.jsonl")]
    [InlineData("deploiement_2026-09-19.jsonl")]
    [InlineData("deploiement_2026-13-45_999999.jsonl")]
    [InlineData("")]
    public void Un_nom_qui_n_annonce_pas_de_moment_n_en_invente_pas(string nom)
    {
        // Mieux vaut n'afficher aucune date que d'aller la chercher dans la date du
        // fichier, qui ne veut plus rien dire dès qu'il a été copié.
        Assert.Null(ParkDeployment.MomentDuJournal(nom));
    }
}
