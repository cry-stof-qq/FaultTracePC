using FaultTracePC.Core.Repair;
using Xunit;

namespace FaultTracePC.Tests;

/// <summary>
/// Lecture des comptes rendus de sfc et DISM.
///
/// Le test qui compte n'est pas « sait-il lire le français et l'anglais » : c'est
/// « que fait-il quand il ne sait PAS lire ». Tant que la réponse est « il le
/// dit », une langue non prévue ne produit jamais de conclusion fausse.
/// </summary>
public class RepairOutputTests
{
    // --- sfc ---------------------------------------------------------------

    [Fact]
    public void Sfc_reparation_partielle_n_est_pas_un_succes()
    {
        // LE bug historique : jusqu'à la 1.2.3 ce cas se résumait à « sfc : terminé »,
        // mot pour mot la même phrase que pour une machine parfaitement saine.
        Assert.Equal(SfcResult.RepairIncomplete, RepairOutput.ReadSfc(
            "Windows Resource Protection found corrupt files but was unable to fix some of them."));
        // Phrase relevée telle quelle dans System32\\fr-FR\\sfc.exe.mui (Windows 11 26100).
        Assert.Equal(SfcResult.RepairIncomplete, RepairOutput.ReadSfc(
            "La Protection des ressources Windows a détecté des fichiers corrompus, mais n'a pas pu réparer certains d'entre eux."));
    }

    [Fact]
    public void Sfc_lit_les_trois_autres_issues()
    {
        Assert.Equal(SfcResult.NoViolations, RepairOutput.ReadSfc(
            "Windows Resource Protection did not find any integrity violations."));
        Assert.Equal(SfcResult.Repaired, RepairOutput.ReadSfc(
            "Windows Resource Protection found corrupt files and successfully repaired them."));
        Assert.Equal(SfcResult.CouldNotRun, RepairOutput.ReadSfc(
            "Windows Resource Protection could not perform the requested operation."));
    }

    /// <summary>
    /// Phrases COPIÉES depuis System32\\fr-FR\\sfc.exe.mui d'un Windows 11 26100,
    /// apostrophes comprises. Elles ne sont pas retapées à la main : c'est
    /// justement la frappe manuelle qui produit le bug testé juste en dessous.
    /// </summary>
    [Theory]
    [InlineData("Le programme de protection des ressources Windows n\u2019a trouvé aucune violation d\u2019intégrité.", SfcResult.NoViolations)]
    [InlineData("La Protection des ressources Windows a détecté des fichiers corrompus et les a réparés.", SfcResult.Repaired)]
    [InlineData("La Protection des ressources Windows a détecté des fichiers corrompus, mais n'a pas pu réparer certains d'entre eux.", SfcResult.RepairIncomplete)]
    [InlineData("La protection des ressources Windows n\u2019a pas réussi à effectuer l\u2019opération demandée.", SfcResult.CouldNotRun)]
    [InlineData("La protection des ressources Windows n\u2019a pas réussi à démarrer le service de réparation.", SfcResult.CouldNotRun)]
    // Sortie de « sfc /verifyonly » : des violations existent et rien n'a été réparé.
    [InlineData("La Protection des ressources Windows a détecté des violations de l'intégrité.", SfcResult.RepairIncomplete)]
    public void Sfc_phrases_francaises_reelles(string sortie, SfcResult attendu)
    {
        Assert.Equal(attendu, RepairOutput.ReadSfc(sortie));
    }

    [Fact]
    public void Apostrophe_typographique_ne_fait_pas_rater_la_lecture()
    {
        // LE piège, découvert en lisant le vrai fichier de ressources : sfc mélange
        // les DEUX apostrophes dans le même fichier. « n\u2019a trouvé aucune violation
        // d\u2019intégrité » porte l'apostrophe typographique U+2019, tandis que
        // « n'a pas pu réparer » porte l'apostrophe ASCII. Une comparaison écrite au
        // clavier ne voit que la seconde : une machine française SAINE aurait été
        // déclarée illisible.
        const string typographique = "Le programme de protection des ressources Windows n\u2019a trouvé aucune violation d\u2019intégrité.";
        const string ascii = "Le programme de protection des ressources Windows n'a trouvé aucune violation d'intégrité.";
        Assert.NotEqual(typographique, ascii);                       // ce sont bien deux chaînes différentes
        Assert.Equal(SfcResult.NoViolations, RepairOutput.ReadSfc(typographique));
        Assert.Equal(SfcResult.NoViolations, RepairOutput.ReadSfc(ascii));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Der Windows-Ressourcenschutz hat keine Integritätsverletzungen gefunden.")]
    [InlineData("Windows 资源保护未找到任何完整性冲突。")]
    public void Sfc_langue_non_prevue_ou_sortie_vide_se_declare_illisible(string sortie)
    {
        // Une machine allemande ou chinoise ne doit pas être déclarée saine par
        // défaut : c'est la seule garantie qui ne dépende pas de l'exhaustivité
        // des listes de phrases.
        Assert.Equal(SfcResult.Unreadable, RepairOutput.ReadSfc(sortie));
    }

    // --- DISM /ScanHealth --------------------------------------------------

    [Fact]
    public void Image_non_reparable_n_est_pas_lue_comme_reparable()
    {
        // « cannot be repaired » contient « repaired » : l'ordre des tests est
        // ce qui empêche de conclure l'inverse de ce qui est écrit.
        Assert.Equal(ImageHealth.NotRepairable, RepairOutput.ReadImageScan(
            "The component store cannot be repaired."));
        Assert.Equal(ImageHealth.Repairable, RepairOutput.ReadImageScan(
            "The component store is repairable."));
    }

    [Fact]
    public void Image_saine_et_illisible()
    {
        Assert.Equal(ImageHealth.Healthy, RepairOutput.ReadImageScan(
            "No component store corruption detected.\r\nThe operation completed successfully."));
        Assert.Equal(ImageHealth.Healthy, RepairOutput.ReadImageScan(
            "Aucune altération du magasin de composants n'a été détectée."));
        // Relevé le 17/08/2026 sur un Windows 11 français, sortie brute de
        // « DISM /Online /Cleanup-Image /CheckHealth » :
        Assert.Equal(ImageHealth.Repairable, RepairOutput.ReadImageScan(
            "Outil Gestion et maintenance des images de déploiement\r\nVersion : 10.0.26100.8972\r\n\r\n"
            + "Version de l'image : 10.0.26200.9168\r\n\r\nLe magasin de composants est réparable.\r\nL'opération a réussi."));
        Assert.Equal(ImageHealth.Unreadable, RepairOutput.ReadImageScan(
            "Version : 10.0.26100.1\r\n[==========================100.0%==========================]"));
        Assert.Equal(ImageHealth.Unreadable, RepairOutput.ReadImageScan(null));
    }

    // --- DISM /RestoreHealth -----------------------------------------------

    [Fact]
    public void Reparation_d_image_lue_par_le_code_d_erreur()
    {
        // 0x800f081f : fichiers sources introuvables. C'est l'échec le plus
        // fréquent, sur un poste sans accès à Windows Update ou filtré par un
        // WSUS — exactement le parc scolaire visé.
        Assert.Equal(ImageRepair.Failed, RepairOutput.ReadImageRepair(
            "Error: 0x800f081f\r\nThe source files could not be found."));
        Assert.Equal(ImageRepair.Completed, RepairOutput.ReadImageRepair(
            "The restore operation completed successfully."));
        Assert.Equal(ImageRepair.Unreadable, RepairOutput.ReadImageRepair("[====100.0%====]"));
    }

    [Fact]
    public void Refus_de_l_option_english_detecte()
    {
        Assert.True(RepairOutput.RejectedEnglishOption(
            "Error: 87\r\nThe english option is unknown."));
        Assert.False(RepairOutput.RejectedEnglishOption(
            "No component store corruption detected."));
        // Un 87 sans rapport avec l'option ne doit pas déclencher une seconde
        // exécution de plusieurs minutes.
        Assert.False(RepairOutput.RejectedEnglishOption("Version: 10.0.87.1"));
    }

    // --- Repair-Volume -----------------------------------------------------

    [Fact]
    public void Etat_de_volume_lu_sur_des_identifiants_pas_sur_des_phrases()
    {
        Assert.Equal(VolumeScan.NeedsRepair, RepairOutput.ReadVolumeScan("NeedsScan"));
        Assert.Equal(VolumeScan.NeedsRepair, RepairOutput.ReadVolumeScan("SpotFixNeeded"));
        Assert.Equal(VolumeScan.NoErrors, RepairOutput.ReadVolumeScan("NoErrorsFound"));
        Assert.Equal(VolumeScan.Unreadable, RepairOutput.ReadVolumeScan("Repair-Volume : Accès refusé"));
    }
}
