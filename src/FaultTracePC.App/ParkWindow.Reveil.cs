using System.Diagnostics;
using System.IO;
using System.Windows;
using FaultTracePC.Core;

namespace FaultTracePC.App;

/// <summary>
/// POINT 88, lot 3 — RÉVEILLER DES POSTES DEPUIS LA CONSOLE, sans rien déployer.
///
/// Demandé le 05/10/2026, redemandé le 19/10 puis le 17/11/2026 : générer le rapport
/// du parc quand la salle est allumée (le 17/11 à 08:15, il ne comparait que 6 postes
/// sur 12, la salle étant éteinte). L'auteur ne veut pas lancer un script de
/// déploiement pour démarrer un poste où le logiciel est déjà installé.
///
/// Choix arrêtés le 05/10 et le 19/10/2026 : un bouton dans l'Inventaire (postes
/// cochés) ; une entrée au clic droit de la Supervision (postes sélectionnés) ;
/// au-delà d'un poste, une confirmation qui annonce le nombre AVANT ; le compte rendu
/// dit, poste par poste, d'où vient l'adresse MAC ou quelles sources ont manqué.
///
/// LE MOTEUR N'EST PAS RÉÉCRIT : c'est le script de déploiement, en mode
/// « -ReveilSeulement » (ni copie, ni installation, ni réglage), éprouvé sur le parc
/// réel le 16/11/2026 — six postes réveillés depuis cette console. Ce fichier n'est
/// que l'interface.
/// </summary>
public partial class ParkWindow
{
    /// <summary>Au-delà, procéder en plusieurs fois — comme pour la vérification.</summary>
    private const int MaxParReveil = 100;

    private bool _reveilEnCours;

    /// <summary>Bouton de l'Inventaire : réveille les postes COCHÉS.</summary>
    private async void BtnInvReveiller_Click(object sender, RoutedEventArgs e)
    {
        if (_inventaireOccupe || _reveilEnCours) return;
        TxtInvStatus.ToolTip = null;

        // Comme pour Vérifier et Déployer : rien n'est coché d'office.
        var choisis = LignesAffichees().Where(l => l.Coche).ToList();
        if (choisis.Count == 0)
        {
            TxtInvStatus.Text = Lang.T("Aucun poste coché : rien à réveiller.", "No computer ticked: nothing to wake.");
            return;
        }

        OccuperInventaire(true, Lang.T(
            $"Réveil de {choisis.Count} poste(s) — le résultat s'affichera ici quand tu auras fermé la fenêtre PowerShell.",
            $"Waking {choisis.Count} computer(s) — the result will appear here once you close the PowerShell window."));
        try
        {
            foreach (var l in choisis) l.Etat = Lang.T("réveil en cours…", "waking…");

            var (lecture, message) = await ReveillerAsync(choisis.Select(l => l.Nom).ToList());
            if (lecture is null)
            {
                foreach (var l in choisis) l.Etat = "";
                TxtInvStatus.Text = message;
                return;
            }

            // L'état affiché change ; le CODE de vérification (Verdict) ne bouge pas :
            // il sert à l'aperçu du déploiement, et un réveil ne vérifie rien.
            foreach (var l in choisis)
                l.Etat = ParkDeployment.VerdictDuReveil(lecture, l.Nom).Phrase;

            Statut(ResumeDuReveil(lecture, choisis.Select(l => l.Nom).ToList()));
        }
        finally
        {
            OccuperInventaire(false, null);
        }
    }

    /// <summary>Entrée du clic droit de la Supervision : réveille les postes SÉLECTIONNÉS.</summary>
    private async void MnuReveiller_Click(object sender, RoutedEventArgs e)
    {
        if (_inventaireOccupe || _reveilEnCours) return;

        var choisis = PostesSelectionnes();
        if (choisis.Count == 0) return;

        TxtStatus.Text = Lang.T(
            $"Réveil de {choisis.Count} poste(s) — le résultat s'affichera ici quand tu auras fermé la fenêtre PowerShell.",
            $"Waking {choisis.Count} computer(s) — the result will appear here once you close the PowerShell window.");

        var noms = choisis.Select(r => r.Name).ToList();
        var (lecture, message) = await ReveillerAsync(noms);
        if (lecture is null)
        {
            TxtStatus.Text = message;
            return;
        }

        var resume = ResumeDuReveil(lecture, noms);
        TxtStatus.Text = resume;
        // Le détail poste par poste, au survol : la barre d'état n'en montre qu'une ligne.
        TxtStatus.ToolTip = string.Join("\n", noms.Select(n => Lang.T($"{n} : {ParkDeployment.VerdictDuReveil(lecture, n).Phrase}", $"{n}: {ParkDeployment.VerdictDuReveil(lecture, n).Phrase}")));
    }

    /// <summary>
    /// Le travail commun aux deux entrées : contrôles, confirmation, lancement du
    /// script en mode réveil, lecture de son journal. Rend le journal lu, ou
    /// <c>null</c> et la raison si rien n'a été lancé.
    /// </summary>
    private async Task<(LectureDuJournal? Lecture, string Message)> ReveillerAsync(IReadOnlyList<string> noms)
    {
        if (noms.Count > MaxParReveil)
            return (null, Lang.T(
                $"{noms.Count} postes choisis, le maximum est de {MaxParReveil} par réveil. Procéder en plusieurs fois.",
                $"{noms.Count} computers chosen, the maximum is {MaxParReveil} per wake. Work in several passes."));

        // La stratégie d'exécution d'abord : elle refuserait le script avant sa
        // première ligne. Ce logiciel ne la contourne pas — il la nomme.
        var politique = PowerShellPolicy.Read(TimeSpan.FromSeconds(8));
        if (politique is { Blocked: true })
            return (null, Lang.T(
                $"Le réveil ne peut pas démarrer : une stratégie de groupe interdit l'exécution de scripts sur ce poste ({politique.Scope} = {politique.Policy}). FaultTracePC ne la contourne pas — volontairement.",
                $"The wake cannot start: a Group Policy forbids running scripts on this machine ({politique.Scope} = {politique.Policy}). FaultTracePC does not work around it — deliberately."));

        // Les réglages du réveil sont pris dans la fenêtre (onglet Inventaire), comme
        // pour le déploiement : ils viennent peut-être d'être saisis.
        var serveurDhcp = (TxtServeurDhcp.Text ?? "").Trim();
        var fichierMac = (TxtFichierMac.Text ?? "").Trim();

        // Au-delà d'un poste, le nombre est annoncé AVANT (décision du 19/10/2026).
        // Un seul poste : le geste lui-même vaut confirmation.
        if (noms.Count > 1)
        {
            var texte = Lang.T($"Réveiller {noms.Count} postes ?", $"Wake {noms.Count} computers?")
                + "\n\n"
                + Lang.T("Rien n'est installé ni modifié sur les postes : un poste déjà allumé est seulement constaté allumé.",
                         "Nothing is installed or changed on the computers: a computer that is already on is only noted as on.")
                + "\n"
                + Lang.T("Le signal de réveil ne traverse pas les routeurs : un poste sur un autre réseau (VLAN) que ce poste ne se réveillera pas.",
                         "The wake signal does not cross routers: a computer on another network (VLAN) than this one will not wake up.")
                + (serveurDhcp.Length == 0 && fichierMac.Length == 0
                    ? "\n\n" + Lang.T("Ni serveur DHCP ni fichier d'adresses MAC renseigné (onglet Inventaire) : seule la mémoire réseau de ce poste (cache ARP) peut donner l'adresse d'un poste éteint, et elle est souvent vide. Le réveil risque d'échouer.",
                                      "Neither a DHCP server nor a MAC address file is set (Inventory tab): only this computer's network memory (ARP cache) can give the address of a computer that is off, and it is often empty. The wake is likely to fail.")
                    : "");
            if (MessageBox.Show(this, texte, "FaultTracePC", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return (null, Lang.T("Réveil annulé.", "Wake cancelled."));
        }

        _reveilEnCours = true;
        try
        {
            var dossier = ScriptDeDeploiement.DossierParDefaut;
            var script = ScriptDeDeploiement.Extraire(dossier);

            var ecartes = new List<string>();
            var listeDesPostes = Path.Combine(dossier, "postes-a-reveiller.txt");
            if (ListeDePostes.Ecrire(listeDesPostes, noms, ecartes) == 0)
                return (null, Lang.T("Aucun nom de poste exploitable dans la sélection.", "No usable computer name in the selection."));

            // Un journal par réveil, jamais écrasé, à côté de ceux des vérifications
            // et des déploiements.
            var journal = Path.Combine(dossier, $"{ParkDeployment.PrefixeReveil}{DateTime.Now:yyyy-MM-dd_HHmmss}.jsonl");

            var parametres = new List<KeyValuePair<string, string?>>
            {
                new("FichierPostes", listeDesPostes),
                new("ReveilSeulement", null),
                new("SortieJson", journal),
                new("Langue", Lang.Current == AppLanguage.English ? "en" : "fr"),
            };
            if (serveurDhcp.Length > 0)
                parametres.Add(new("ServeurDhcp", serveurDhcp));
            if (fichierMac.Length > 0)
                parametres.Add(new("FichierMac", ParkInventory.CheminAdressesMac(fichierMac, DossierDesDonnees)));

            using var processus = Process.Start(new ProcessStartInfo
            {
                FileName = "powershell.exe",
                // La fenêtre attend une touche, même quand tout va bien : son déroulé
                // dit d'où vient chaque adresse et quel poste a répondu.
                Arguments = PowerShellLauncher.ArgumentsForScript(script, L.PsClose, parametres, pauseTousLesCas: true),
                UseShellExecute = true,
            });
            if (processus is null)
                return (null, Lang.T("PowerShell n'a pas pu être lancé.", "PowerShell could not be started."));

            await processus.WaitForExitAsync();
            return (ParkDeployment.Lire(journal), "");
        }
        catch (Exception ex)
        {
            return (null, Lang.T($"Réveil impossible : {ex.Message}", $"Wake failed: {ex.Message}"));
        }
        finally
        {
            _reveilEnCours = false;
        }
    }

    /// <summary>Une phrase de bilan, comptée sur les CODES — jamais sur des phrases traduites.</summary>
    private static string ResumeDuReveil(LectureDuJournal lecture, IReadOnlyList<string> noms)
    {
        var codes = noms.Select(n => ParkDeployment.VerdictDuReveil(lecture, n).Code).ToList();
        int reveilles = codes.Count(c => c == "reveille");
        int deja = codes.Count(c => c == "deja");
        int autres = codes.Count - reveilles - deja;
        return Lang.T(
            $"Réveil : {reveilles} réveillé(s), {deja} déjà allumé(s), {autres} non réveillé(s) ou sans trace — détail poste par poste dans la liste (ou au survol). Un poste réveillé met une à deux minutes à répondre à la console.",
            $"Wake: {reveilles} woken, {deja} already on, {autres} not woken or without trace — computer-by-computer detail in the list (or on hover). A woken computer takes one to two minutes to answer the console.");
    }
}
