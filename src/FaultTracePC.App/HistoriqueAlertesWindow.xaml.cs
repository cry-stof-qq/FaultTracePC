using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using FaultTracePC.Core;

namespace FaultTracePC.App;

/// <summary>
/// POINT 43 — l'HISTORIQUE DES ALERTES d'un poste, tel que la console l'a archivé.
///
/// LA QUESTION À LAQUELLE ELLE RÉPOND
/// « Ce poste-là, est-ce qu'il alerte depuis longtemps ? » Le poste lui-même ne
/// sait pas répondre : il ne garde que sept jours, et un poste réimagé repart
/// vide. La console, elle, a tout gardé.
///
/// AUCUNE MACHINE N'EST CONTACTÉE. On relit un fichier sur ce disque. La fenêtre
/// répond donc sur un poste éteint, débranché, ou remplacé — ce qui est justement
/// le moment où on la consulte.
///
/// LE TEXTE EST REFABRIQUÉ, PAS RECOPIÉ
/// Une alerte a été écrite dans la langue du moment. AlertCatalog la réécrit dans
/// la langue d'aujourd'hui à partir du fait conservé — la règle et la valeur
/// mesurée. Sans cela, une archive de six mois serait un mélange de deux langues.
/// </summary>
public partial class HistoriqueAlertesWindow : Window
{
    private readonly string _poste;
    private bool _pret;

    public HistoriqueAlertesWindow(Window proprietaire, string poste)
    {
        InitializeComponent();

        Owner = proprietaire;
        _poste = poste;

        TxtEntete.Text = Lang.T($"📜 Historique des alertes de {poste}", $"📜 Alert history of {poste}");

        CbPeriode.ItemsSource = new[]
        {
            new Periode(7, Lang.T("les 7 derniers jours", "the last 7 days")),
            new Periode(30, Lang.T("les 30 derniers jours", "the last 30 days")),
            new Periode(90, Lang.T("les 90 derniers jours", "the last 90 days")),
            new Periode(365, Lang.T("les 12 derniers mois", "the last 12 months")),
            // ZÉRO VEUT DIRE « TOUT » : rien n'est jamais effacé de l'archive, il
            // faut donc pouvoir tout voir.
            new Periode(0, Lang.T("tout l'historique", "the whole history")),
        };
        CbPeriode.DisplayMemberPath = nameof(Periode.Libelle);
        CbPeriode.SelectedIndex = 2;
    }

    private sealed record Periode(int Jours, string Libelle);

    /// <summary>Une ligne du tableau. Les libellés sont fabriqués ici, pas dans le noyau.</summary>
    private sealed class LigneDAlerte
    {
        public string Quand { get; init; } = "";
        public string Niveau { get; init; } = "";
        public string Titre { get; init; } = "";
        public string Detail { get; init; } = "";

        /// <summary>Texte complet — détail ET recommandation — montré au survol.</summary>
        public string Complet { get; init; } = "";

        public bool Critique { get; init; }
    }

    private void Fenetre_Loaded(object sender, RoutedEventArgs e)
    {
        _pret = true;
        Charger();
    }

    private void CbPeriode_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // La liste est remplie dans le constructeur : sans ce garde-fou, le premier
        // remplissage déclencherait un chargement avant que la fenêtre existe.
        if (_pret) Charger();
    }

    private void BtnOuvrirDossier_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(ArchiveDesAlertes.Dossier);
            Process.Start(new ProcessStartInfo(ArchiveDesAlertes.Dossier) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            TxtStatut.Text = Lang.T($"Ouverture du dossier impossible : {ex.Message}",
                                    $"Could not open the folder: {ex.Message}");
        }
    }

    private void Charger()
    {
        if (CbPeriode.SelectedItem is not Periode periode) return;

        if (ArchiveDesAlertes.FichierDe(_poste).Length == 0)
        {
            // Le nom n'a pas pu devenir un chemin de fichier. Ce n'est pas « aucune
            // alerte » : c'est « on n'a jamais pu en archiver ».
            LvAlertes.ItemsSource = null;
            TxtCompte.Text = "";
            TxtStatut.Text = Lang.T(
                $"Aucune archive possible pour « {_poste} » : ce nom ne peut pas servir de nom de fichier. Les noms attendus ne contiennent que des lettres, des chiffres et des traits d'union.",
                $"No archive is possible for “{_poste}”: this name cannot be used as a file name. Expected names contain only letters, digits and hyphens.");
            return;
        }

        List<PreventiveAlert> alertes;
        try
        {
            alertes = ArchiveDesAlertes.Lire(_poste, periode.Jours);
        }
        catch (Exception ex)
        {
            LvAlertes.ItemsSource = null;
            TxtCompte.Text = "";
            TxtStatut.Text = Lang.T($"Lecture de l'archive impossible : {ex.Message}",
                                    $"Could not read the archive: {ex.Message}");
            return;
        }

        // Le texte est réécrit dans la langue d'aujourd'hui — voir le commentaire
        // de classe.
        AlertCatalog.LocalizeAll(alertes);

        LvAlertes.ItemsSource = alertes.Select(Convertir).ToList();

        var critiques = alertes.Count(a => a.Level == "crit");
        TxtCompte.Text = critiques > 0
            ? Lang.T($"{alertes.Count} alerte(s), dont {critiques} critique(s)", $"{alertes.Count} alert(s), {critiques} critical")
            : Lang.T($"{alertes.Count} alerte(s)", $"{alertes.Count} alert(s)");

        if (alertes.Count == 0)
        {
            // RIEN N'EST PAS UNE PREUVE DE BONNE SANTÉ. Un poste jamais interrogé
            // par cette console a une archive vide, exactement comme un poste sain.
            TxtStatut.Text = Lang.T(
                "Aucune alerte archivée sur cette période. Attention : l'archive ne contient que ce que CETTE console a vu lors de ses actualisations — un poste jamais interrogé est vide lui aussi.",
                "No archived alert over this period. Note: the archive holds only what THIS console saw during its refreshes — a machine that was never queried is empty too.");
            return;
        }

        TxtStatut.Text = Resume(alertes);
    }

    /// <summary>
    /// La phrase du bas : la période réellement couverte, et la règle qui revient
    /// le plus.
    ///
    /// C'EST CETTE DERNIÈRE QUI SERT. Vingt alertes réparties sur vingt causes,
    /// c'est un poste normal ; vingt alertes qui sont vingt fois la même
    /// surchauffe, c'est un ventilateur à changer. Le tableau seul ne fait pas la
    /// différence — il faut compter.
    ///
    /// LE LIBELLÉ DE LA RÈGLE EST CELUI DE SON ALERTE LA PLUS RÉCENTE, parce que
    /// c'est le seul texte disponible : le fichier conserve un identifiant de
    /// règle, pas un nom de règle. Inventer un nom ici en créerait un deuxième,
    /// différent de celui affiché juste au-dessus.
    /// </summary>
    private static string Resume(List<PreventiveAlert> alertes)
    {
        var plusAncienne = alertes.Min(a => a.Time);
        var plusRecente = alertes.Max(a => a.Time);

        var periode = Lang.T(
            $"De {Lang.ShortDateMinute(plusAncienne)} à {Lang.ShortDateMinute(plusRecente)}.",
            $"From {Lang.ShortDateMinute(plusAncienne)} to {Lang.ShortDateMinute(plusRecente)}.");

        var dominante = alertes
            .GroupBy(a => a.RuleId, StringComparer.Ordinal)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key, StringComparer.Ordinal)
            .First();

        if (dominante.Count() < 2) return periode;

        var libelle = dominante.OrderByDescending(a => a.Time).First().Title;

        return periode + Lang.T(
            $" La plus fréquente, {dominante.Count()} fois sur {alertes.Count} : « {libelle} ».",
            $" The most frequent, {dominante.Count()} times out of {alertes.Count}: “{libelle}”.");
    }

    private static LigneDAlerte Convertir(PreventiveAlert a)
    {
        var critique = a.Level == "crit";

        var complet = string.Join("\n\n", new[] { a.Details, a.Recommendation }
                                          .Where(x => !string.IsNullOrWhiteSpace(x)));

        return new LigneDAlerte
        {
            Quand = Lang.ShortDateMinute(a.Time),
            Niveau = critique ? Lang.T("⛔ critique", "⛔ critical") : Lang.T("⚠ à surveiller", "⚠ watch"),
            Titre = a.Title,
            Detail = a.Details,
            Complet = complet.Length > 0 ? complet : a.Title,
            Critique = critique,
        };
    }
}
