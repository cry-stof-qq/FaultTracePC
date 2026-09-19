using FaultTracePC.Core;
using Xunit;

namespace FaultTracePC.Tests;

/// <summary>
/// Point 64, lot C. Le paquet à déployer est contrôlé AVANT le premier octet copié.
/// Un chemin fautif découvert au milieu d'un lot de cinquante machines laisse la
/// moitié du parc dans un état et l'autre moitié dans un autre.
/// </summary>
[Collection("Langue")]
public class PaquetADeployerTests
{
    [Fact]
    public void Un_chemin_vide_est_refuse_et_dit_pourquoi()
    {
        Assert.NotEmpty(ParkDeployment.VerifierLePaquet(""));
        Assert.NotEmpty(ParkDeployment.VerifierLePaquet(null));
        Assert.NotEmpty(ParkDeployment.VerifierLePaquet("   "));
    }

    [Fact]
    public void Un_fichier_qui_n_est_pas_un_msi_est_refuse_sans_toucher_au_disque()
    {
        // Le contrôle de l'extension passe AVANT celui de l'existence : inutile
        // d'aller interroger un partage réseau pour un fichier qui ne peut pas
        // convenir de toute façon.
        var message = ParkDeployment.VerifierLePaquet(@"\\serveur\partage\FaultTracePC-1.6.2.zip");

        Assert.Contains(".msi", message);
    }

    [Fact]
    public void Un_paquet_absent_est_refuse()
    {
        var absent = Path.Combine(Path.GetTempPath(), "ftpc_absent_" + Guid.NewGuid().ToString("N") + ".msi");

        Assert.NotEmpty(ParkDeployment.VerifierLePaquet(absent));
    }

    [Fact]
    public void Un_paquet_present_est_accepte()
    {
        var dossier = Path.Combine(Path.GetTempPath(), "ftpc_paquet_" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(dossier);
            var chemin = Path.Combine(dossier, "FaultTracePC-1.6.2.msi");
            File.WriteAllText(chemin, "ceci n'est pas un vrai MSI, et ce controle ne l'ouvre pas");

            Assert.Equal("", ParkDeployment.VerifierLePaquet(chemin));

            // Les guillemets d'un « Copier en tant que chemin d'accès » sont retirés,
            // comme pour l'annuaire d'adresses MAC : c'est le même geste d'utilisateur.
            Assert.Equal("", ParkDeployment.VerifierLePaquet("\"" + chemin + "\""));
        }
        finally { if (Directory.Exists(dossier)) Directory.Delete(dossier, true); }
    }

    [Theory]
    [InlineData(@"\\serveur\partage\FaultTracePC-1.6.2.msi", "1.6.2")]
    [InlineData(@"C:\paquets\FaultTracePC-1.10.0.msi", "1.10.0")]
    // Un fichier renommé ne dit plus rien : mieux vaut ne rien annoncer que
    // d'annoncer une version qui n'est peut-être pas celle du paquet.
    [InlineData(@"C:\paquets\paquet.msi", "")]
    [InlineData(@"C:\paquets\FaultTracePC.msi", "")]
    [InlineData(@"C:\paquets\FaultTracePC-1.6.msi", "")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void La_version_se_lit_dans_le_nom_du_fichier_ou_pas_du_tout(string? chemin, string attendu)
        => Assert.Equal(attendu, ParkDeployment.VersionAnnonceeParLeNom(chemin));

    [Fact]
    public void Le_reglage_du_paquet_n_est_pas_un_secret()
    {
        // Même garde-fou que pour le reste des réglages : si quelqu'un ajoute un
        // jour un mot de passe dans ce fichier, un test doit tomber.
        var proprietes = typeof(ParametresParc).GetProperties().Select(p => p.Name.ToLowerInvariant()).ToList();

        Assert.Contains("cheminDuPaquet".ToLowerInvariant(), proprietes);
        foreach (var interdit in new[] { "password", "motdepasse", "token", "jeton", "secret", "clef", "key" })
            Assert.DoesNotContain(proprietes, p => p.Contains(interdit));
    }
}
