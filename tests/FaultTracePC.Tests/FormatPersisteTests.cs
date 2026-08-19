using System.Text.Json;
using FaultTracePC.Core;
using FaultTracePC.Core.Report;
using Xunit;

namespace FaultTracePC.Tests;

/// <summary>
/// Le tampon de format des fichiers persistés.
///
/// Jusqu'à la 1.3, aucun fichier ne disait de quel format il était : chaque
/// évolution obligeait à reconnaître les anciennes écritures à leur allure, et
/// ce code de reconnaissance ne s'enlevait jamais. Ce qui est vérifié ici n'est
/// pas que le numéro existe, mais qu'il sert : un fichier étranger doit être
/// REFUSÉ, et son refus doit être DIT sans qu'aucune donnée soit supprimée.
/// </summary>
[Collection("Langue")]
public class FormatPersisteTests
{
    [Fact]
    public void Un_resume_ecrit_aujourd_hui_porte_le_tampon()
    {
        var r = new DiagnosticReport { GeneratedAt = new DateTime(2026, 8, 19, 9, 0, 0) };
        Assert.Equal(ScanHistory.FormatActuel, ScanHistory.Summarize(r).Format);
    }

    [Fact]
    public void Un_resume_ecrit_avant_la_1_4_est_refuse()
    {
        // Le cas réel : un fichier parfaitement valide, simplement dépourvu du
        // champ. Il vaut 0, et se distingue donc sans ambiguïté.
        var ancien = """{"GeneratedAt":"2026-07-01T10:00:00","ScanPeriodDays":30}""";
        Assert.Null(ScanHistory.Lire(ancien));
    }

    [Fact]
    public void Un_resume_ecrit_par_une_version_plus_recente_est_refuse_aussi()
    {
        // Symétrie indispensable : cette version-ci ne saurait pas lire
        // correctement un format qu'elle ne connaît pas. Se taire vaut mieux que
        // comparer sur des champs mal compris.
        var futur = $$"""{"format":{{ScanHistory.FormatActuel + 1}},"GeneratedAt":"2027-01-01T10:00:00"}""";
        Assert.Null(ScanHistory.Lire(futur));
    }

    [Fact]
    public void Un_resume_au_format_courant_passe()
    {
        var r = new DiagnosticReport { GeneratedAt = new DateTime(2026, 8, 19, 9, 0, 0) };
        var json = JsonSerializer.Serialize(ScanHistory.Summarize(r));

        var relu = ScanHistory.Lire(json);

        Assert.NotNull(relu);
        Assert.Equal(new DateTime(2026, 8, 19, 9, 0, 0), relu!.GeneratedAt);
    }

    [Fact]
    public void Un_contenu_illisible_ne_fait_pas_tomber_la_lecture()
    {
        Assert.Null(ScanHistory.Lire("ceci n'est pas du JSON"));
        Assert.Null(ScanHistory.Lire(""));
    }

    [Fact]
    public void Rien_n_est_annonce_quand_rien_n_a_ete_ecarte()
    {
        Assert.Null(ScanHistory.NoteAncienFormat(0));
        Assert.Null(ScanHistory.NoteAncienFormat(-1));
    }

    [Fact]
    public void Ce_qui_est_ecarte_est_annonce_ET_conserve()
    {
        // La promesse du logiciel depuis la 1.2.3 : rien d'irréversible sans le
        // dire. Refuser de relire est un choix technique ; effacer les données de
        // quelqu'un en serait un autre, et celui-là ne se prend pas en silence.
        var note = ScanHistory.NoteAncienFormat(3);

        Assert.NotNull(note);
        Assert.Contains("3", note!);
        Assert.Contains("rien n'a été supprimé", note, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void L_annonce_existe_dans_les_deux_langues()
    {
        var initial = Lang.Current;
        try
        {
            Lang.Apply(AppLanguage.English);
            var en = ScanHistory.NoteAncienFormat(2);
            Assert.NotNull(en);
            Assert.Contains("nothing has been deleted", en!, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("supprimé", en, StringComparison.OrdinalIgnoreCase);
        }
        finally { Lang.Apply(initial); }
    }
}
