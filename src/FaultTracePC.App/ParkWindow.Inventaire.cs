using System.IO;
using System.Windows;
using System.Windows.Controls;
using FaultTracePC.Core;

namespace FaultTracePC.App;

/// <summary>
/// POINT 64, LOT A-3 — l'onglet « Inventaire » de la console de parc.
///
/// CE QU'IL FAIT, ET SURTOUT CE QU'IL NE FAIT PAS
/// Il lit trois sources — l'annuaire, <c>parc.json</c>, <c>postes.csv</c> — et les
/// fusionne. AUCUNE MACHINE N'EST CONTACTÉE. Cet onglet répond à « quels postes
/// existent », jamais à « lesquels répondent » : c'est l'onglet Supervision qui
/// interroge, et lui seul.
///
/// AUCUN MOT DE PASSE
/// L'annuaire est interrogé avec le ticket Kerberos du compte qui a lancé le
/// logiciel. Rien n'est demandé, rien n'est stocké.
///
/// CLASSE PARTIELLE À PART
/// <c>ParkWindow.xaml.cs</c> fait déjà plus de six cents lignes. Cet ajout n'en
/// modifie aucune : il se branche par l'événement <c>Loaded</c> déclaré dans le
/// XAML, et ne partage avec elle que <c>ParkFile</c>.
/// </summary>
public partial class ParkWindow
{
    /// <summary>
    /// Dossier des données de la console — celui de <c>parc.json</c>. C'est aussi
    /// là qu'est cherché l'annuaire d'adresses MAC quand aucun chemin n'est réglé.
    /// </summary>
    private static string DossierDesDonnees => Path.GetDirectoryName(ParkFile)!;

    /// <summary>
    /// Une ligne du tableau. Les libellés sont fabriqués ICI, dans la langue de
    /// l'interface : le modèle du noyau porte des dates et des booléens, pas des
    /// phrases.
    /// </summary>
    private sealed class LigneInventaire
    {
        public string Nom { get; init; } = "";
        public string Sources { get; init; } = "";
        public string Hote { get; init; } = "";
        public string Unite { get; init; } = "";
        public string DerniereSession { get; init; } = "";
        public string Compte { get; init; } = "";
        public string Mac { get; init; } = "";
    }

    /// <summary>Empêche deux interrogations simultanées de l'annuaire.</summary>
    private bool _inventaireOccupe;

    private void ParkWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // Le RÉGLAGE est relu, pas l'annuaire. Interroger au démarrage ferait
        // attendre tout le monde — y compris sur un poste hors domaine, où il n'y
        // a rien à trouver. L'utilisateur déclenche quand il en a besoin.
        var reglages = ParametresParc.Charger();
        TxtUnite.Text = reglages.UniteOrganisation;
        TxtFichierMac.Text = reglages.FichierAdressesMac;
    }

    // ------------------------------------------------------------------
    // Choisir son unité plutôt que la saisir
    // ------------------------------------------------------------------

    private async void BtnInvListerUnites_Click(object sender, RoutedEventArgs e)
    {
        if (_inventaireOccupe) return;

        OccuperInventaire(true, Lang.T("Lecture des unités d'organisation…", "Reading organizational units…"));

        var notes = new List<string>();
        List<ParkDirectory.UniteOrganisation> unites;
        try
        {
            unites = await Task.Run(() => ParkDirectory.ListerUnites(null, notes));
        }
        catch (Exception ex)
        {
            // Les méthodes du noyau rattrapent déjà leurs propres erreurs. Ce filet
            // est là pour ce qu'elles ne prévoient pas : dans un gestionnaire
            // « async void », une exception qui remonte ferme l'application.
            TxtInvStatus.Text = Lang.T($"Lecture des unités impossible : {ex.Message}",
                                       $"Could not read the units: {ex.Message}");
            return;
        }
        finally
        {
            OccuperInventaire(false, null);
        }

        CbUnite.ItemsSource = unites;
        CbUnite.DisplayMemberPath = nameof(ParkDirectory.UniteOrganisation.Libelle);
        CbUnite.IsEnabled = unites.Count > 0;

        // TROIS SITUATIONS, ET PAS DEUX. Une liste vide après une ERREUR ne se dit
        // pas comme une liste vide après une lecture réussie : la première
        // n'autorise aucune conclusion, la seconde en autorise une. Le 19/09/2026,
        // le rapport annonçait « aucune unité, ce qui convient dans la plupart des
        // cas » alors que l'annuaire venait de refuser le chemin — rassurant, et faux.
        var etat = unites.Count > 0
            ? Lang.T($"{unites.Count} unité(s) d'organisation trouvée(s). Choisir dans la liste remplit le champ du dessus.",
                     $"{unites.Count} organizational unit(s) found. Picking from the list fills the field above.")
            : notes.Count > 0
                ? Lang.T("Les unités d'organisation n'ont pas pu être lues — le message de l'annuaire suit. Rien ne permet de dire s'il en existe ou non.",
                         "The organizational units could not be read — the directory's message follows. Nothing here says whether any exist.")
                : Lang.T("Aucune unité d'organisation dans cet annuaire. Laisser le champ vide interroge la racine du domaine, ce qui convient dans la plupart des cas.",
                         "No organizational unit in this directory. Leaving the field empty queries the domain root, which is what most setups need.");

        TxtInvStatus.Text = AvecNotes(etat, notes);
    }

    private void BtnInvParcourirMac_Click(object sender, RoutedEventArgs e)
    {
        var boite = new Microsoft.Win32.OpenFileDialog
        {
            Title = Lang.T("Choisir l'annuaire d'adresses MAC", "Choose the MAC address directory"),
            Filter = Lang.T("Fichiers CSV (*.csv)|*.csv|Tous les fichiers (*.*)|*.*",
                            "CSV files (*.csv)|*.csv|All files (*.*)|*.*"),
            FileName = ParkInventory.NomAnnuaireMac,
            CheckFileExists = true,
        };

        // Choisir plutôt que saisir : un chemin recopié à la main se trompe, et
        // l'erreur ne se voit qu'à une colonne restée vide.
        if (boite.ShowDialog(this) == true) TxtFichierMac.Text = boite.FileName;
    }

    private void CbUnite_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // C'est le NOM DISTINCTIF qui part dans le réglage, pas le libellé lisible :
        // « Postes / Salle 12 » ne désigne rien pour l'annuaire.
        if (CbUnite.SelectedItem is ParkDirectory.UniteOrganisation u)
            TxtUnite.Text = u.NomDistinctif;
    }

    // ------------------------------------------------------------------
    // Construire l'inventaire
    // ------------------------------------------------------------------

    private async void BtnInvActualiser_Click(object sender, RoutedEventArgs e)
    {
        if (_inventaireOccupe) return;

        var saisie = (TxtUnite.Text ?? "").Trim();

        // LE CONTRÔLE AVANT L'INTERROGATION, et pas l'inverse. Une virgule non
        // échappée ne produit pas « virgule non échappée » : elle produit une liste
        // vide sans erreur, qu'on interpréterait comme « aucun poste ».
        var probleme = ParkDirectory.VerifierUnite(saisie);
        if (probleme.Length > 0)
        {
            TxtInvStatus.Text = probleme;
            return;
        }

        OccuperInventaire(true, Lang.T("Lecture de l'annuaire…", "Reading the directory…"));

        // Les réglages ne sont enregistrés QUE si la saisie a passé le contrôle :
        // mémoriser une unité fautive la ferait revenir à chaque ouverture.
        var reglages = ParametresParc.Charger();
        reglages.UniteOrganisation = saisie;
        reglages.FichierAdressesMac = (TxtFichierMac.Text ?? "").Trim();
        bool memorise = reglages.Enregistrer();

        var cheminMac = ParkInventory.CheminAdressesMac(reglages.FichierAdressesMac, DossierDesDonnees);

        var notes = new List<string>();
        List<PosteDuParc> annuaire;
        List<PosteDuParc> console;
        Dictionary<string, string> macs;
        try
        {
            // Les trois lectures partent ensemble sur le fil d'arrière-plan : une
            // interrogation d'annuaire peut durer, et l'interface doit rester
            // utilisable pendant ce temps.
            (annuaire, console, macs) = await Task.Run(() => (
                ParkDirectory.Interroger(saisie, notes),
                ParkInventory.LireParcJson(ParkFile, notes),
                ParkInventory.LireAnnuaireMac(cheminMac, notes)));
        }
        catch (Exception ex)
        {
            // Même filet que ci-dessus, pour la même raison.
            TxtInvStatus.Text = Lang.T($"Lecture de l'inventaire impossible : {ex.Message}",
                                       $"Could not read the inventory: {ex.Message}");
            return;
        }
        finally
        {
            OccuperInventaire(false, null);
        }

        var postes = ParkInventory.Fusionner(annuaire, console, macs);
        LvInventaire.ItemsSource = postes.Select(Convertir).ToList();

        // « Fichier absent » et « fichier lu, zéro adresse » ne se disent pas pareil :
        // le premier explique une colonne vide, le second signale un fichier à revoir.
        var motMac = File.Exists(cheminMac)
            ? Lang.T($"adresses MAC : {macs.Count} lue(s) dans {cheminMac}",
                     $"MAC addresses: {macs.Count} read from {cheminMac}")
            : Lang.T($"aucun annuaire d'adresses MAC à {cheminMac} — le script de déploiement écrit le sien à côté de lui, indiquer son chemin ci-dessus remplit la colonne",
                     $"no MAC address directory at {cheminMac} — the deployment script writes its own next to itself; setting its path above fills the column");

        var resume = Lang.T(
            $"{postes.Count} poste(s) — annuaire : {annuaire.Count}, console : {console.Count}. {motMac}.",
            $"{postes.Count} computer(s) — directory: {annuaire.Count}, console: {console.Count}. {motMac}.");

        if (!memorise)
            resume += Lang.T(" L'unité d'organisation n'a pas pu être mémorisée : elle sera à ressaisir à la prochaine ouverture.",
                             " The organizational unit could not be saved: it will have to be typed again next time.");

        TxtInvStatus.Text = AvecNotes(resume, notes);
    }

    // ------------------------------------------------------------------
    // Mise en forme
    // ------------------------------------------------------------------

    private static LigneInventaire Convertir(PosteDuParc p)
    {
        // Hors annuaire, « actif » et une date de session seraient des affirmations
        // sans source : un poste saisi à la main dans la console n'a pas de compte
        // d'ordinateur connu de ce logiciel.
        bool vuParLAnnuaire = p.Sources.HasFlag(SourcesDuPoste.ActiveDirectory);
        var horsAnnuaire = Lang.T("hors annuaire", "not in directory");

        return new LigneInventaire
        {
            Nom = p.Name,
            Sources = p.SourcesLabel,
            Hote = p.Host,
            Unite = p.OrganizationalUnit,
            DerniereSession = vuParLAnnuaire ? DateCourte(p.LastLogon) : horsAnnuaire,
            Compte = vuParLAnnuaire
                     ? (p.Disabled ? Lang.T("désactivé", "disabled") : Lang.T("actif", "enabled"))
                     : horsAnnuaire,

            // L'ADRESSE ELLE-MÊME NE S'AFFICHE PAS. Elle identifie un matériel, elle
            // n'aide à aucun diagnostic, et ce logiciel la masque déjà partout
            // ailleurs. Savoir qu'on l'a suffit : c'est ce qui dit si le réveil
            // réseau est possible.
            Mac = p.MacConnue ? Lang.T("oui", "yes") : "",
        };
    }

    private static string DateCourte(DateTime? date) =>
        date is null
            ? Lang.T("inconnue", "unknown")
            : date.Value.ToString(Lang.T("dd/MM/yyyy", "yyyy-MM-dd"));

    /// <summary>
    /// Colle les notes des lectures au résumé. Une note dit pourquoi une source n'a
    /// rien rendu — annuaire injoignable, fichier illisible, accès refusé — et c'est
    /// précisément ce qui manque quand une liste ressort vide sans explication.
    /// </summary>
    private static string AvecNotes(string resume, List<string> notes) =>
        notes.Count == 0 ? resume : resume + "  " + string.Join("  ", notes);

    private void OccuperInventaire(bool occupe, string? message)
    {
        _inventaireOccupe = occupe;
        BtnInvListerUnites.IsEnabled = !occupe;
        BtnInvActualiser.IsEnabled = !occupe;
        Cursor = occupe ? System.Windows.Input.Cursors.Wait : null;
        if (message is not null) TxtInvStatus.Text = message;
    }
}
