using FaultTracePC.Core;
using Xunit;

namespace FaultTracePC.Tests;

/// <summary>
/// Point 64, lot B. Le script est embarqué dans le noyau : si quelqu'un renomme le
/// fichier, déplace le dossier ou retire la ligne du projet, RIEN ne le signale à la
/// compilation — la ressource disparaît simplement, et l'échec n'arrive qu'au premier
/// déploiement, devant un parc. Ces tests sont le seul endroit qui le voie avant.
/// </summary>
public class ScriptEmbarqueTests
{
    [Fact]
    public void Le_script_est_bien_compile_dans_le_noyau()
    {
        var octets = ScriptDeDeploiement.Octets();

        Assert.NotEmpty(octets);
        // Quelques kilo-octets au moins : une ressource vide ou tronquée passerait
        // un simple « non vide » sans être utilisable.
        Assert.True(octets.Length > 10_000, $"Script anormalement court : {octets.Length} octets.");
    }

    [Fact]
    public void Le_script_commence_par_la_marque_d_ordre_des_octets()
    {
        // CE TEST PROTÈGE LES ACCENTS. PowerShell 5.1 lit un .ps1 sans marque
        // d'ordre des octets comme s'il était en page de code ANSI : tous les
        // messages du script en ressortiraient abîmés, sur toutes les machines.
        var octets = ScriptDeDeploiement.Octets();

        Assert.Equal(0xEF, octets[0]);
        Assert.Equal(0xBB, octets[1]);
        Assert.Equal(0xBF, octets[2]);
    }

    [Fact]
    public void C_est_le_bon_fichier_et_pas_un_homonyme()
    {
        var texte = ScriptDeDeploiement.Lire();

        Assert.Contains("Déploiement de FaultTracePC", texte);
        Assert.Contains("$PSScriptRoot", texte);
        // Les accents doivent survivre à la lecture : s'ils sont abîmés ici, c'est
        // que la ressource a été recomposée quelque part au lieu d'être copiée.
        Assert.Contains("vérifie", texte);
    }

    [Fact]
    public void L_extraction_rend_un_fichier_identique_a_la_ressource()
    {
        var dossier = Path.Combine(Path.GetTempPath(), "ftpc_script_" + Guid.NewGuid().ToString("N"));
        try
        {
            var chemin = ScriptDeDeploiement.Extraire(dossier);

            Assert.Equal(Path.Combine(dossier, ScriptDeDeploiement.NomFichier), chemin);
            Assert.True(File.Exists(chemin));
            Assert.Equal(ScriptDeDeploiement.Octets(), File.ReadAllBytes(chemin));
        }
        finally
        {
            if (Directory.Exists(dossier)) Directory.Delete(dossier, recursive: true);
        }
    }

    [Fact]
    public void Une_copie_laissee_par_une_version_precedente_est_ecrasee()
    {
        // La version embarquée fait foi : elle est celle qui correspond au contrat
        // JSON de la version en cours. Une copie plus ancienne ne doit jamais
        // l'emporter, et surtout pas en silence.
        var dossier = Path.Combine(Path.GetTempPath(), "ftpc_script_" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(dossier);
            var chemin = Path.Combine(dossier, ScriptDeDeploiement.NomFichier);
            File.WriteAllText(chemin, "# une vieille version");

            ScriptDeDeploiement.Extraire(dossier);

            Assert.Equal(ScriptDeDeploiement.Octets(), File.ReadAllBytes(chemin));
        }
        finally
        {
            if (Directory.Exists(dossier)) Directory.Delete(dossier, recursive: true);
        }
    }

    [Fact]
    public void Le_dossier_par_defaut_n_est_pas_le_dossier_d_installation()
    {
        // Écrire dans Program Files demanderait des droits que le logiciel n'a pas
        // à réclamer pour ça, et laisserait une trace dans un dossier géré par
        // l'installateur.
        var defaut = ScriptDeDeploiement.DossierParDefaut;

        Assert.DoesNotContain("Program Files", defaut, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith(Path.Combine("FaultTracePC", "Deploiement"), defaut);
    }
}
