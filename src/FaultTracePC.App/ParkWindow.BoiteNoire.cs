using System.Net.Http;
using System.Text.Json;
using System.Windows;
using FaultTracePC.Core;

namespace FaultTracePC.App;

/// <summary>
/// POINT 46 — ouvrir la boîte noire d'un poste distant depuis l'onglet Supervision.
///
/// LE POINT D'ACCÈS EXISTAIT DÉJÀ ET N'ÉTAIT APPELÉ PAR PERSONNE. Le service expose
/// <c>/api/flight?minutes=N</c> depuis la 1.5.0 ; il ne manquait que la fenêtre qui
/// le lit. C'est pour ça que ce lot est court.
///
/// CLASSE PARTIELLE À PART, comme l'onglet Inventaire : <c>ParkWindow.xaml.cs</c>
/// fait déjà plus de six cents lignes, et rien n'oblige à les augmenter.
/// </summary>
public partial class ParkWindow
{
    /// <summary>
    /// Client dédié à la lecture de la boîte noire.
    ///
    /// LE CLIENT ORDINAIRE DE LA CONSOLE ABANDONNE AU BOUT DE 4 SECONDES — c'est
    /// voulu pour interroger vingt postes d'affilée, où un poste muet ne doit pas
    /// retenir les autres. Mais vingt-quatre heures de relevés, c'est près de neuf
    /// mille lignes : le même délai couperait la lecture en plein milieu, et
    /// l'erreur ressemblerait à un poste injoignable.
    /// </summary>
    private static readonly HttpClient BoiteNoireHttp = new() { Timeout = TimeSpan.FromSeconds(30) };

    private void BtnBoiteNoire_Click(object sender, RoutedEventArgs e)
    {
        if (LvMachines.SelectedItem is not Row ligne)
        {
            MessageBox.Show(this,
                Lang.T("Sélectionne d'abord une machine dans la liste.", "Select a machine in the list first."),
                "FaultTracePC", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var machine = _machines.FirstOrDefault(m => m.Name == ligne.Name && $"{m.Host}:{m.Port}" == ligne.Host);
        if (machine is null) return;

        // Le jeton est contrôlé ICI, avant d'ouvrir une fenêtre qui ne pourrait rien
        // afficher : c'est la même vérification que pour le diagnostic distant.
        if (SansJeton(machine)) return;

        new BoiteNoireWindow(this, machine.Name, minutes => LireLaBoiteNoire(machine, minutes)).ShowDialog();
    }

    /// <summary>
    /// Va chercher les relevés sur le poste. Rend la liste ET le message d'erreur :
    /// une liste vide après une lecture réussie et une liste vide après un refus ne
    /// veulent pas dire la même chose, et seule la première autorise à conclure que
    /// la machine n'avait rien à montrer.
    /// </summary>
    private async Task<(List<FlightSample>, string)> LireLaBoiteNoire(ParkMachine machine, int minutes)
    {
        try
        {
            using var requete = SignedRequest(machine, HttpMethod.Get, "/api/flight", $"minutes={minutes}");
            if (requete is null)
                return ([], Lang.T("Aucun jeton pour ce poste.", "No token for this computer."));

            using var reponse = await BoiteNoireHttp.SendAsync(requete);

            if (!reponse.IsSuccessStatusCode)
                return ([], Lang.T($"le poste a répondu {(int)reponse.StatusCode}",
                                   $"the computer answered {(int)reponse.StatusCode}"));

            var corps = await reponse.Content.ReadAsStringAsync();

            var echantillons = JsonSerializer.Deserialize<List<FlightSample>>(corps) ?? [];
            return (echantillons, "");
        }
        catch (Exception ex)
        {
            // Le message du réseau est plus utile qu'une reformulation : « connexion
            // refusée », « délai dépassé » et « nom introuvable » désignent chacun
            // une cause différente et une correction différente.
            return ([], ex.Message);
        }
    }
}
