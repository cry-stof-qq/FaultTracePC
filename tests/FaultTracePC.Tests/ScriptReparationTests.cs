using FaultTracePC.Core;
using FaultTracePC.Core.Report;
using Xunit;

namespace FaultTracePC.Tests;

/// <summary>
/// Le script de réparation doit être un script PowerShell VALIDE.
///
/// Défaut constaté le 19/08/2026 sur le PC d'un tiers : le script n'a pas démarré
/// du tout — erreur d'analyse, aucune réparation exécutée. En cause, la
/// description d'un pilote Intel contenant « d’E/S ». Ce « ’ » est U+2019, et
/// PowerShell le traite comme une apostrophe : la chaîne se terminait là, et la
/// fin de la ligne était lue comme du code.
///
/// Aucun contrôle existant ne pouvait le voir : le générateur produisait du C#
/// parfaitement valide, et le défaut n'apparaissait qu'aux yeux de l'interpréteur
/// qui relit le résultat.
/// </summary>
[Collection("Langue")]
public class ScriptReparationTests
{
    /// <summary>Les caractères que PowerShell traite comme des apostrophes.</summary>
    private static readonly char[] ApostrophesPowerShell = ['\u2018', '\u2019', '\u201B', '\u2032'];

    [Theory]
    [InlineData("d\u2019E/S")]
    [InlineData("d\u2018E/S")]
    [InlineData("d\u201BE/S")]
    [InlineData("5\u2032 de lecture")]
    [InlineData("l'apostrophe droite")]
    public void Toute_apostrophe_est_neutralisee(string dangereux)
    {
        var echappe = RepairScriptGenerator.PsEscape(dangereux);

        Assert.DoesNotContain(echappe, c => ApostrophesPowerShell.Contains(c));
        // Nombre PAIR d'apostrophes droites : c'est la règle d'échappement de
        // PowerShell, et c'est ce qui garantit que la chaîne ne se referme pas.
        Assert.Equal(0, echappe.Count(c => c == '\'') % 2);
    }

    [Fact]
    public void Un_texte_sans_apostrophe_n_est_pas_modifie()
    {
        Assert.Equal("Intel Corporation", RepairScriptGenerator.PsEscape("Intel Corporation"));
    }

    [Fact]
    public void Un_texte_nul_ne_fait_pas_tomber_la_generation()
    {
        Assert.Equal("", RepairScriptGenerator.PsEscape(null!));
    }

    [Fact]
    public void Le_script_genere_ne_contient_aucune_apostrophe_typographique()
    {
        // LE test de non-régression : on repart du cas réel, un pilote dont la
        // description contient « d’E/S », et on vérifie le script PRODUIT.
        var r = RapportAvecPiloteHostile();

        var script = RepairScriptGenerator.Generate(r);

        Assert.DoesNotContain(script, c => ApostrophesPowerShell.Contains(c));
    }

    [Fact]
    public void Chaque_ligne_du_script_ferme_ses_chaines()
    {
        // Une ligne qui laisse une chaîne ouverte avale la ligne suivante : c'est
        // exactement la cascade d'erreurs observée sur la machine de l'utilisateur.
        var script = RepairScriptGenerator.Generate(RapportAvecPiloteHostile());

        // Les commentaires sont retirés d'abord : une apostrophe y est inoffensive,
        // et la compter ferait échouer le test sur une phrase française anodine.
        var fautives = SansCommentaires(script)
                             .Select((l, i) => (Ligne: i + 1, Texte: l))
                             .Where(x => ChaineOuverte(x.Texte))
                             .ToList();

        Assert.True(fautives.Count == 0,
            "Ligne(s) laissant une chaîne ouverte : "
            + string.Join(", ", fautives.Select(f => f.Ligne + " → " + f.Texte.Trim())));
    }

    /// <summary>
    /// Vrai si la ligne se termine alors qu'une chaîne est encore ouverte.
    ///
    /// Compter les apostrophes ne suffit pas : une apostrophe entre guillemets
    /// doubles est un caractère ordinaire — <c>Write-Host "vérifie l'image"</c> est
    /// valide. On suit donc quel délimiteur a ouvert la chaîne, en tenant compte du
    /// doublage (<c>''</c> et <c>""</c>), de l'accent grave qui échappe dans une
    /// chaîne à guillemets doubles, et du <c>#</c> qui ouvre un commentaire hors
    /// chaîne.
    /// </summary>
    private static bool ChaineOuverte(string ligne)
    {
        char? ouvert = null;

        for (var i = 0; i < ligne.Length; i++)
        {
            var c = ligne[i];

            if (ouvert is null)
            {
                if (c == '\'' || c == '"') ouvert = c;
                else if (c == '`') i++;                       // échappement hors chaîne
                else if (c == '#' && (i == 0 || char.IsWhiteSpace(ligne[i - 1])))
                    return false;                             // commentaire de fin de ligne
            }
            else if (c == ouvert)
            {
                if (i + 1 < ligne.Length && ligne[i + 1] == c) i++;   // délimiteur doublé
                else ouvert = null;
            }
            else if (ouvert == '"' && c == '`')
            {
                i++;                                          // ` n'échappe que dans "…"
            }
        }

        return ouvert is not null;
    }

    /// <summary>Lignes de code seules : blocs &lt;# … #&gt; et lignes commençant par # retirés.</summary>
    private static IEnumerable<string> SansCommentaires(string script)
    {
        var dansBloc = false;
        foreach (var brute in script.Split('\n'))
        {
            var ligne = brute.Trim();

            if (dansBloc)
            {
                if (ligne.Contains("#>")) dansBloc = false;
                yield return "";
                continue;
            }

            if (ligne.StartsWith("<#"))
            {
                dansBloc = !ligne.Contains("#>");
                yield return "";
                continue;
            }

            yield return ligne.StartsWith('#') ? "" : brute;
        }
    }

    private static DiagnosticReport RapportAvecPiloteHostile() => new()
    {
        GeneratedAt = new DateTime(2026, 8, 19, 19, 20, 0),
        ScanPeriodDays = 30,
        System = new SystemSnapshot
        {
            MachineName = "TEST",
            Cpu = new CpuInfo { Name = "Intel(R) Core(TM) i5" },
            Drivers =
            [
                new DriverInfo
                {
                    Path = @"C:\Windows\System32\drivers\iaLPSS2i_I2C.sys",
                    CompanyName = "Intel Corporation",
                    DisplayName = "Pilote v2 I2C d\u2019E/S s\u00E9rie Intel(R)",
                    FileDate = new DateTime(2022, 5, 7),
                },
            ],
        },
        Findings =
        [
            new Finding
            {
                Severity = Severity.Critical,
                Confidence = Confidence.High,
                Category = FaultCategory.Driver,
                Title = "Pilote ancien",
                Details = "d",
            },
        ],
    };
}
