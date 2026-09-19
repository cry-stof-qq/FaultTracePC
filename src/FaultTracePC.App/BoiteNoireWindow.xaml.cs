using System.Windows;
using System.Windows.Controls;
using FaultTracePC.Core;

namespace FaultTracePC.App;

/// <summary>
/// POINT 46 — la boîte noire d'un poste DISTANT, vue depuis la console.
///
/// POURQUOI CE N'EST PAS « DU TEMPS RÉEL »
/// Au moment d'une coupure, la machine s'arrête et le réseau avec elle : aucune
/// actualisation, si rapide soit-elle, ne verrait quoi que ce soit de plus. Ce qui
/// répond à la question, c'est le journal que le service a écrit AVANT — avec
/// écriture forcée sur le disque, pour que les dernières secondes survivent à la
/// coupure. Cette fenêtre le relit. La réponse arrive après, parce que c'est le
/// seul moment où elle peut arriver.
///
/// LE PLUS RÉCENT EN HAUT
/// Ce qu'on vient chercher après une coupure, ce sont les DERNIÈRES lignes. Les
/// faire défiler depuis le début reviendrait à cacher la réponse au bas d'un
/// tableau de plusieurs centaines de lignes.
///
/// CETTE FENÊTRE NE SAIT PAS PARLER RÉSEAU. Elle reçoit une fonction qui va
/// chercher les relevés — c'est la console qui signe la requête, parce que c'est
/// elle qui détient le jeton du poste.
/// </summary>
public partial class BoiteNoireWindow : Window
{
    private readonly string _poste;
    private readonly Func<int, Task<(List<FlightSample> Echantillons, string Erreur)>> _chercher;
    private bool _pret;

    public BoiteNoireWindow(Window proprietaire, string poste,
                            Func<int, Task<(List<FlightSample>, string)>> chercher)
    {
        InitializeComponent();

        Owner = proprietaire;
        _poste = poste;
        _chercher = chercher;

        TxtEntete.Text = Lang.T($"📈 Boîte noire de {poste}", $"📈 Flight recorder of {poste}");

        CbDuree.ItemsSource = new[]
        {
            new Duree(15, Lang.T("les 15 dernières minutes", "the last 15 minutes")),
            new Duree(60, Lang.T("la dernière heure", "the last hour")),
            new Duree(240, Lang.T("les 4 dernières heures", "the last 4 hours")),
            new Duree(1440, Lang.T("les 24 dernières heures", "the last 24 hours")),
        };
        CbDuree.DisplayMemberPath = nameof(Duree.Libelle);
        CbDuree.SelectedIndex = 1;
    }

    private sealed record Duree(int Minutes, string Libelle);

    /// <summary>Une ligne du tableau. Les libellés sont fabriqués ici, pas dans le noyau.</summary>
    private sealed class LigneDeVol
    {
        public string Heure { get; init; } = "";
        public string Cpu { get; init; } = "";
        public string TempCpu { get; init; } = "";
        public string TempGpu { get; init; } = "";
        public string Gpu { get; init; } = "";
        public string Ram { get; init; } = "";
        public string Commit { get; init; } = "";
        public string Detail { get; init; } = "";

        /// <summary>Événement, démarrage ou arrêt : une ligne qu'on doit voir tout de suite.</summary>
        public bool Marquant { get; init; }
    }

    private async void Fenetre_Loaded(object sender, RoutedEventArgs e)
    {
        _pret = true;
        await Charger();
    }

    private async void BtnActualiser_Click(object sender, RoutedEventArgs e) => await Charger();

    private async void CbDuree_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // La liste est remplie dans le constructeur : sans ce garde-fou, le premier
        // remplissage déclencherait un chargement avant que la fenêtre existe.
        if (_pret) await Charger();
    }

    private async Task Charger()
    {
        if (CbDuree.SelectedItem is not Duree duree) return;

        BtnActualiser.IsEnabled = false;
        CbDuree.IsEnabled = false;
        Cursor = System.Windows.Input.Cursors.Wait;
        TxtStatut.Text = Lang.T($"Lecture de la boîte noire de {_poste}…", $"Reading the flight recorder of {_poste}…");

        List<FlightSample> echantillons;
        string erreur;
        try
        {
            (echantillons, erreur) = await _chercher(duree.Minutes);
        }
        catch (Exception ex)
        {
            echantillons = [];
            erreur = ex.Message;
        }
        finally
        {
            BtnActualiser.IsEnabled = true;
            CbDuree.IsEnabled = true;
            Cursor = null;
        }

        if (erreur.Length > 0)
        {
            LvVol.ItemsSource = null;
            TxtCompte.Text = "";
            TxtStatut.Text = Lang.T($"Lecture impossible : {erreur}", $"Could not read: {erreur}");
            return;
        }

        // Le plus récent en haut : voir le commentaire de classe.
        var lignes = echantillons.OrderByDescending(s => s.Time).Select(Convertir).ToList();
        LvVol.ItemsSource = lignes;

        int marquants = lignes.Count(l => l.Marquant);

        TxtCompte.Text = Lang.T($"{lignes.Count} ligne(s)", $"{lignes.Count} line(s)");

        if (lignes.Count == 0)
        {
            // RIEN N'EST PAS UNE PANNE. Un poste éteint pendant toute la période, ou
            // dont le service vient d'être installé, n'a simplement rien à montrer.
            TxtStatut.Text = Lang.T(
                "Aucun relevé sur cette période. Le poste était peut-être éteint, ou la surveillance n'était pas en marche — ce n'est pas la même chose qu'une panne.",
                "No reading over this period. The computer may have been off, or monitoring was not running — that is not the same as a failure.");
            return;
        }

        var plusAncien = echantillons.Min(s => s.Time);
        var plusRecent = echantillons.Max(s => s.Time);

        TxtStatut.Text = Lang.T(
            $"Du {Lang.ShortDateSecond(plusAncien)} au {Lang.ShortDateSecond(plusRecent)}. {marquants} ligne(s) marquante(s) — événements Windows, démarrages et arrêts de la surveillance.",
            $"From {Lang.ShortDateSecond(plusAncien)} to {Lang.ShortDateSecond(plusRecent)}. {marquants} notable line(s) — Windows events, monitoring starts and stops.");
    }

    private static LigneDeVol Convertir(FlightSample s)
    {
        var (detail, marquant) = Decrire(s);

        return new LigneDeVol
        {
            Heure = Lang.ShortDateSecond(s.Time),
            Cpu = s.CpuLoad is { } c ? $"{c:0.#} %" : "",
            TempCpu = s.CpuTemp is { } ct ? $"{ct:0.#} °C" : "",
            TempGpu = s.GpuTemp is { } gt ? $"{gt:0.#} °C" : "",
            Gpu = s.GpuLoad is { } gl ? $"{gl:0.#} %" : "",
            Ram = s.MemPct is { } m ? $"{m:0.#} %" : "",
            Commit = s.CommitPct is { } cm ? $"{cm:0.#} %" : "",
            Detail = detail,
            Marquant = marquant,
        };
    }

    /// <summary>
    /// Ce que dit une ligne, et si elle doit sauter aux yeux.
    ///
    /// LA LIGNE LA PLUS PRÉCIEUSE DE TOUT CE TABLEAU est celle d'un démarrage qui
    /// signale que la session précédente s'est terminée brutalement : c'est la
    /// preuve, écrite par la machine elle-même, qu'elle n'a pas été éteinte
    /// proprement. Sans elle, « le poste a redémarré » et « le poste a planté » se
    /// ressemblent.
    /// </summary>
    private static (string Detail, bool Marquant) Decrire(FlightSample s) => s.Kind switch
    {
        "e" => (string.Join(" — ", new[] { s.EventCategory, s.EventMessage }.Where(x => !string.IsNullOrWhiteSpace(x))), true),

        "b" when s.PreviousEndedAbruptly == true =>
            (Lang.T("▲ Démarrage de la surveillance — LA SESSION PRÉCÉDENTE S'EST TERMINÉE BRUTALEMENT",
                    "▲ Monitoring started — THE PREVIOUS SESSION ENDED ABRUPTLY"), true),

        "b" => (Lang.T("▲ Démarrage de la surveillance", "▲ Monitoring started"), true),

        "x" => (Lang.T("■ Arrêt propre de la surveillance", "■ Clean monitoring shutdown"), true),

        _ => (s.TopProcesses ?? "", false),
    };
}
