using System.ComponentModel;
using System.Diagnostics;
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
    private sealed class LigneInventaire : INotifyPropertyChanged
    {
        public string Nom { get; init; } = "";
        public string Sources { get; init; } = "";
        public string Hote { get; init; } = "";
        public string Unite { get; init; } = "";
        public string DerniereSession { get; init; } = "";
        public string Compte { get; init; } = "";
        public string Mac { get; init; } = "";

        // CES DEUX-LÀ CHANGENT APRÈS COUP, les autres jamais. Sans
        // INotifyPropertyChanged, cocher une case à la main marcherait, mais
        // « Tout cocher » ne se verrait pas à l'écran : la liaison ne saurait pas
        // que la valeur a bougé.
        private bool _coche;
        public bool Coche
        {
            get => _coche;
            set { if (_coche != value) { _coche = value; Prevenir(nameof(Coche)); } }
        }

        private string _etat = "";
        public string Etat
        {
            get => _etat;
            set { if (_etat != value) { _etat = value; Prevenir(nameof(Etat)); } }
        }

        /// <summary>
        /// Le même verdict sous forme de CODE, pour compter sans relire du texte.
        /// Compter des phrases traduites reviendrait à faire dépendre un décompte
        /// de la langue de l'interface.
        /// </summary>
        public string Verdict { get; set; } = "";

        public event PropertyChangedEventHandler? PropertyChanged;
        private void Prevenir(string nom) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nom));
    }

    /// <summary>
    /// Plafond par vérification. Ce n'est pas une limite technique : c'est le
    /// nombre au-delà duquel on ne sait plus ce qu'on a lancé. Chaque poste ouvre
    /// plusieurs connexions réseau, et une vérification de tout un parc d'un seul
    /// clic est une décision qui mérite d'être prise en plusieurs fois.
    /// </summary>
    private const int MaxParVerification = 100;

    /// <summary>
    /// Plafond par déploiement, plus bas que celui de la vérification et pour une
    /// raison simple : ici on MODIFIE des machines. Cinquante postes, c'est encore
    /// une liste qu'on peut relire avant de valider ; deux cents, non.
    /// </summary>
    private const int MaxParDeploiement = 50;

    /// <summary>Empêche deux interrogations simultanées de l'annuaire.</summary>
    private bool _inventaireOccupe;

    /// <summary>
    /// Ce que la barre d'état montre AU SURVOL : le texte long, dont la barre
    /// elle-même n'affiche que les deux premières lignes.
    /// </summary>
    private string _detailDuStatut = "";

    private void ParkWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // Le RÉGLAGE est relu, pas l'annuaire. Interroger au démarrage ferait
        // attendre tout le monde — y compris sur un poste hors domaine, où il n'y
        // a rien à trouver. L'utilisateur déclenche quand il en a besoin.
        var reglages = ParametresParc.Charger();
        TxtUnite.Text = reglages.UniteOrganisation;
        TxtFichierMac.Text = reglages.FichierAdressesMac;
        TxtServeurDhcp.Text = reglages.ServeurDhcp;
        TxtPaquet.Text = reglages.CheminDuPaquet;
        MajEtatDuPaquet();
    }

    // ------------------------------------------------------------------
    // Choisir son unité plutôt que la saisir
    // ------------------------------------------------------------------

    private async void BtnInvListerUnites_Click(object sender, RoutedEventArgs e)
    {
        if (_inventaireOccupe) return;

        // Une info-bulle appartient au message qui l'a posée : elle part avec lui.
        TxtInvStatus.ToolTip = null;

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

    private void BtnInvParcourirPaquet_Click(object sender, RoutedEventArgs e)
    {
        var boite = new Microsoft.Win32.OpenFileDialog
        {
            Title = Lang.T("Choisir le paquet d'installation", "Choose the installer package"),
            Filter = Lang.T("Paquet Windows (*.msi)|*.msi|Tous les fichiers (*.*)|*.*",
                            "Windows package (*.msi)|*.msi|All files (*.*)|*.*"),
            CheckFileExists = true,
        };

        if (boite.ShowDialog(this) == true)
        {
            TxtPaquet.Text = boite.FileName;
            MajEtatDuPaquet();
        }
    }

    /// <summary>
    /// Dit à côté du champ ce que vaut le paquet indiqué, SANS L'OUVRIR : présent
    /// ou non, et quelle version son nom annonce. C'est une indication, pas un
    /// fait — un fichier renommé mentirait, et le texte le dit.
    /// </summary>
    private void MajEtatDuPaquet()
    {
        var chemin = (TxtPaquet.Text ?? "").Trim();
        if (chemin.Length == 0) { TxtPaquetEtat.Text = ""; return; }

        var probleme = ParkDeployment.VerifierLePaquet(chemin);
        if (probleme.Length > 0) { TxtPaquetEtat.Text = "⚠ " + probleme; return; }

        var version = ParkDeployment.VersionAnnonceeParLeNom(chemin);
        TxtPaquetEtat.Text = version.Length > 0
            ? Lang.T($"✔ trouvé — son nom annonce la version {version}",
                     $"✔ found — its name announces version {version}")
            : Lang.T("✔ trouvé — son nom n'annonce aucune version",
                     "✔ found — its name announces no version");
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

        // Une info-bulle appartient au message qui l'a posée : elle part avec lui.
        TxtInvStatus.ToolTip = null;

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
        reglages.ServeurDhcp = (TxtServeurDhcp.Text ?? "").Trim();
        reglages.CheminDuPaquet = (TxtPaquet.Text ?? "").Trim();
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
        // COURT À L'ÉCRAN, COMPLET AU SURVOL. La barre d'état a deux lignes ; le
        // détail d'un chemin et son explication n'y tiennent pas et ne doivent pas
        // écraser le tableau pour autant.
        var motMac = File.Exists(cheminMac)
            ? Lang.T($"adresses MAC : {macs.Count}", $"MAC addresses: {macs.Count}")
            : Lang.T("aucun annuaire d'adresses MAC", "no MAC address directory");

        _detailDuStatut = File.Exists(cheminMac)
            ? Lang.T($"Annuaire d'adresses MAC lu dans {cheminMac}.",
                     $"MAC address directory read from {cheminMac}.")
            : Lang.T($"Aucun annuaire d'adresses MAC à {cheminMac}. Le script de déploiement écrit le sien à côté de lui : indiquer son chemin ci-dessus remplit la colonne « MAC connue ».",
                     $"No MAC address directory at {cheminMac}. The deployment script writes its own next to itself: setting its path above fills the “MAC known” column.");

        var resume = Lang.T(
            $"{postes.Count} poste(s) — annuaire : {annuaire.Count}, console : {console.Count}. {motMac}.",
            $"{postes.Count} computer(s) — directory: {annuaire.Count}, console: {console.Count}. {motMac}.");

        if (!memorise)
            resume += Lang.T(" L'unité d'organisation n'a pas pu être mémorisée : elle sera à ressaisir à la prochaine ouverture.",
                             " The organizational unit could not be saved: it will have to be typed again next time.");

        Statut(AvecNotes(resume, notes));
    }

    // ------------------------------------------------------------------
    // Vérifier la sélection — point 64, lot B
    // ------------------------------------------------------------------

    private IEnumerable<LigneInventaire> LignesAffichees() =>
        LvInventaire.ItemsSource as IEnumerable<LigneInventaire> ?? [];

    /// <summary>
    /// POINT 64, LOT D — recocher les seuls postes en échec du dernier journal.
    ///
    /// RIEN N'EST LANCÉ ICI. Ce bouton PRÉPARE une sélection, il ne la traite pas :
    /// reprendre un déploiement est une décision, et elle se prend en cliquant sur
    /// « Déployer », avec sa confirmation chiffrée, comme la première fois.
    /// </summary>
    private void BtnInvReprendre_Click(object sender, RoutedEventArgs e)
    {
        if (_inventaireOccupe) return;
        TxtInvStatus.ToolTip = null;

        var journal = ParkDeployment.DernierJournal(ScriptDeDeploiement.DossierParDefaut);
        if (journal.Length == 0)
        {
            Statut(Lang.T("Aucun journal : rien n'a encore été vérifié ni déployé depuis cette console.",
                          "No journal: nothing has been checked or deployed from this console yet."));
            return;
        }

        var affichees = LignesAffichees().ToList();

        // LA VRAIE CAUSE D'ABORD. Sans inventaire chargé, il n'y a aucune case à
        // recocher — et le message « 12 en échec, 12 absents de la liste » ferait
        // chercher du côté de l'unité d'organisation, alors qu'il suffit
        // d'actualiser. Constaté le 19/09/2026 au premier essai.
        if (affichees.Count == 0)
        {
            Statut(Lang.T("L'inventaire n'est pas encore chargé : cliquer d'abord sur « Actualiser l'inventaire ».",
                          "The inventory is not loaded yet: click “Refresh the inventory” first."));
            return;
        }

        var lecture = ParkDeployment.Lire(journal);
        var enEchec = new HashSet<string>(lecture.PostesEnEchec, StringComparer.OrdinalIgnoreCase);
        var vusDansLeJournal = new HashSet<string>(lecture.Postes, StringComparer.OrdinalIgnoreCase);

        // Le journal REMET AUSSI LE RÉSULTAT dans la colonne — mais seulement pour
        // les postes qu'il connaît. L'appliquer aux autres les marquerait « aucune
        // trace », ce qui serait faux : ils n'étaient simplement pas du lot.
        AppliquerLesVerdicts(lecture, affichees.Where(l => vusDansLeJournal.Contains(l.Nom)).ToList());

        foreach (var l in affichees) l.Coche = enEchec.Contains(l.Nom);

        int recoches = affichees.Count(l => l.Coche);
        int introuvables = enEchec.Count - recoches;

        var quoi = ParkDeployment.NatureDuJournal(journal) == "deploiement"
            ? Lang.T("déploiement", "deployment")
            : Lang.T("vérification", "check");

        var moment = ParkDeployment.MomentDuJournal(journal);
        var quand = moment is null
            ? ""
            : Lang.T($" du {moment:dd/MM/yyyy} à {moment:HH:mm}", $" of {moment:yyyy-MM-dd} at {moment:HH:mm}");

        var texte = enEchec.Count == 0
            ? Lang.T($"Aucun poste en échec dans le dernier journal de {quoi}{quand}. Rien à reprendre.",
                     $"No failed computer in the last {quoi} journal{quand}. Nothing to resume.")
            : Lang.T($"{enEchec.Count} poste(s) en échec dans le journal de {quoi}{quand} — {recoches} recoché(s).",
                     $"{enEchec.Count} failed computer(s) in the {quoi} journal{quand} — {recoches} ticked.");

        if (introuvables > 0)
        {
            // ON NE FAIT PAS SEMBLANT. Un poste en échec absent de la liste affichée
            // n'a pas été recoché, et le dire évite de croire qu'on reprend tout.
            texte += Lang.T($" {introuvables} absent(s) de la liste affichée.",
                            $" {introuvables} not in the displayed list.");

            var noms = enEchec.Where(nom => !affichees.Any(l =>
                           string.Equals(l.Nom, nom, StringComparison.OrdinalIgnoreCase)))
                       .OrderBy(x => x, StringComparer.OrdinalIgnoreCase);

            _detailDuStatut = Lang.T(
                $"Postes en échec absents de la liste affichée — ils sont peut-être hors de l'unité d'organisation interrogée : {string.Join(", ", noms)}.",
                $"Failed computers missing from the displayed list — they may be outside the queried organizational unit: {string.Join(", ", noms)}.");
        }

        Statut(texte);
    }

    private void BtnInvToutCocher_Click(object sender, RoutedEventArgs e)
    {
        foreach (var l in LignesAffichees()) l.Coche = true;
    }

    private void BtnInvRienCocher_Click(object sender, RoutedEventArgs e)
    {
        foreach (var l in LignesAffichees()) l.Coche = false;
    }

    private async void BtnInvVerifier_Click(object sender, RoutedEventArgs e)
    {
        if (_inventaireOccupe) return;

        // Une info-bulle appartient au message qui l'a posée : elle part avec lui.
        TxtInvStatus.ToolTip = null;

        var choisis = LignesAffichees().Where(l => l.Coche).ToList();

        // AUCUNE CASE N'EST COCHÉE AU DÉPART, ET RIEN NE LES COCHE TOUT SEUL.
        // Un bouton qui agit sur « tout » par défaut finit par agir sur tout un
        // jour où on ne le voulait pas.
        if (choisis.Count == 0)
        {
            TxtInvStatus.Text = Lang.T("Aucun poste coché : rien à vérifier.",
                                       "No computer ticked: nothing to check.");
            return;
        }

        if (choisis.Count > MaxParVerification)
        {
            TxtInvStatus.Text = Lang.T(
                $"{choisis.Count} postes cochés, le maximum est de {MaxParVerification} par vérification. En décocher, ou procéder en plusieurs fois.",
                $"{choisis.Count} computers ticked, the maximum is {MaxParVerification} per check. Untick some, or work in several passes.");
            return;
        }

        // LA STRATÉGIE D'EXÉCUTION AVANT DE LANCER QUOI QUE CE SOIT.
        // Une stratégie de groupe qui interdit les scripts refuse le fichier avant
        // sa première ligne, et la fenêtre se referme trop vite pour être lue. Ce
        // logiciel ne contourne pas ce réglage : il le constate et le nomme.
        var politique = PowerShellPolicy.Read(TimeSpan.FromSeconds(8));
        if (politique is { Blocked: true })
        {
            MessageBox.Show(this,
                Lang.T($"La vérification ne peut pas démarrer : une stratégie de groupe interdit l'exécution de scripts sur ce poste ({politique.Scope} = {politique.Policy}).",
                       $"The check cannot start: a Group Policy forbids running scripts on this machine ({politique.Scope} = {politique.Policy}).")
                + "\n\n"
                + Lang.T("Ce réglage vient de l'administration du parc, et FaultTracePC ne le contourne pas — volontairement.",
                         "This setting comes from your fleet administration, and FaultTracePC does not work around it — deliberately."),
                "FaultTracePC", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // On annonce ce qu'on va faire AVANT de le faire, et on dit aussi ce qu'on
        // ne fera pas : c'est la moitié la plus importante de la phrase.
        var confirmation = MessageBox.Show(this,
            Lang.T($"Vérifier {choisis.Count} poste(s) ?", $"Check {choisis.Count} computer(s)?")
            + "\n\n"
            + Lang.T("Aucune machine ne sera modifiée : ni copie, ni installation, ni réveil réseau. Le logiciel lit seulement le compte d'ordinateur, la réponse réseau, le partage administratif et la gestion à distance.",
                     "No machine will be modified: no copy, no install, no wake-on-LAN. The software only reads the computer account, the network answer, the administrative share and remote management."),
            "FaultTracePC", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (confirmation != MessageBoxResult.Yes) return;

        OccuperInventaire(true, Lang.T(
            $"Vérification de {choisis.Count} poste(s) en cours — le résultat s'affichera ici quand tu auras fermé la fenêtre PowerShell.",
            $"Checking {choisis.Count} computer(s) — the result will appear here once you close the PowerShell window."));
        try
        {
            var dossier = ScriptDeDeploiement.DossierParDefaut;
            var script = ScriptDeDeploiement.Extraire(dossier);

            var ecartes = new List<string>();
            var listeDesPostes = Path.Combine(dossier, "postes-a-verifier.txt");
            int retenus = ListeDePostes.Ecrire(listeDesPostes, choisis.Select(l => l.Nom), ecartes);

            if (retenus == 0)
            {
                TxtInvStatus.Text = Lang.T("Aucun nom de poste exploitable dans la sélection.",
                                           "No usable computer name in the selection.");
                return;
            }

            // UN JOURNAL PAR EXÉCUTION, JAMAIS ÉCRASÉ. On ne supprime rien : les
            // journaux précédents servent à comparer, et c'est aussi ce sur quoi
            // le lot D s'appuiera pour reprendre les seuls postes en échec.
            var journal = Path.Combine(dossier, $"verification_{DateTime.Now:yyyy-MM-dd_HHmmss}.jsonl");

            foreach (var l in choisis) l.Etat = Lang.T("en cours…", "checking…");

            // LA FENÊTRE ATTEND UNE TOUCHE, MÊME QUAND TOUT VA BIEN.
            // Le déroulé d'une vérification EST le résultat : une fenêtre qui se
            // ferme d'elle-même sur un succès ne laisse rien à lire.
            var arguments = PowerShellLauncher.ArgumentsForScript(script, L.PsClose,
            [
                new("FichierPostes", listeDesPostes),
                new("VerifierSeulement", null),
                new("SortieJson", journal),
            ],
            pauseTousLesCas: true);

            using var processus = Process.Start(new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = arguments,
                UseShellExecute = true,
            });

            if (processus is null)
            {
                TxtInvStatus.Text = Lang.T("PowerShell n'a pas pu être lancé.", "PowerShell could not be started.");
                return;
            }

            await processus.WaitForExitAsync();

            var lecture = ParkDeployment.Lire(journal);
            AppliquerLesVerdicts(lecture, choisis);

            TxtInvStatus.Text = ResumeDeVerification(lecture, choisis.Count, ecartes, journal);
        }
        catch (Exception ex)
        {
            TxtInvStatus.Text = Lang.T($"Vérification impossible : {ex.Message}",
                                       $"Check failed: {ex.Message}");
        }
        finally
        {
            OccuperInventaire(false, null);
        }
    }

    /// <summary>
    /// Ce que le journal dit d'UN poste : un code invariant, et la phrase à
    /// afficher. « Aucune trace » n'est pas « tout va bien » — c'est un poste que
    /// le script n'a pas atteint, et il faut le dire.
    ///
    /// L'ORDRE DE LECTURE VA DU PLUS AVANCÉ AU MOINS AVANCÉ : un poste installé
    /// ET mis en parc doit s'annoncer comme tel, pas comme « prêt ».
    /// </summary>
    private static (string Code, string Phrase) VerdictDuPoste(LectureDuJournal lecture, string nom)
    {
        var lignes = lecture.Lignes
            .Where(l => string.Equals(l.Poste, nom, StringComparison.OrdinalIgnoreCase)).ToList();

        if (lignes.Count == 0)
            return ("absent", Lang.T("aucune trace", "no trace"));

        // LES ÉCHECS RATTRAPÉS NE COMPTENT PAS. Un poste éteint fait échouer
        // « reponse » avant d'être réveillé, et une gestion à distance muette fait
        // échouer « winrm » avant d'être démarrée : dans les deux cas le script
        // répare et continue. Voir LectureDuJournal.EchecsDecisifs — la règle vit
        // dans le noyau parce que la reprise du lot D s'en sert aussi.
        var echec = lecture.EchecsDecisifs(nom).FirstOrDefault();
        if (echec is not null)
            return ("echec", Lang.T($"échec : {LibelleEtape(echec.Etape)}",
                                    $"failed: {LibelleEtape(echec.Etape)}"));

        bool Reussi(string etape) => lignes.Any(l =>
            string.Equals(l.Etape, etape, StringComparison.OrdinalIgnoreCase)
            && string.Equals(l.Etat, ParkDeployment.EtatOk, StringComparison.OrdinalIgnoreCase));

        if (Reussi(ParkDeployment.EtapeParc))
            return ("parc", Lang.T("installé et visible de la console", "installed and visible from the console"));

        if (Reussi(ParkDeployment.EtapeInstallation))
            return ("installe", Lang.T("installé — pas mis en parc, donc invisible ici",
                                       "installed — not in fleet mode, so invisible here"));

        if (Reussi(ParkDeployment.EtapeGestionADistance))
            return ("pret", Lang.T("prêt à recevoir le paquet", "ready for the package"));

        return ("partiel", Lang.T("vérifié en partie", "partly checked"));
    }

    private static void AppliquerLesVerdicts(LectureDuJournal lecture, List<LigneInventaire> lignes)
    {
        foreach (var l in lignes)
        {
            var (code, phrase) = VerdictDuPoste(lecture, l.Nom);
            l.Verdict = code;
            l.Etat = phrase;
        }
    }

    private static string LibelleEtape(string etape) => etape switch
    {
        ParkDeployment.EtapeCompte => Lang.T("compte d'ordinateur", "computer account"),
        ParkDeployment.EtapeReponse => Lang.T("pas de réponse réseau", "no network answer"),
        ParkDeployment.EtapePartageAdmin => Lang.T("partage administratif", "administrative share"),
        ParkDeployment.EtapeGestionADistance => Lang.T("gestion à distance", "remote management"),
        ParkDeployment.EtapeReveil => Lang.T("réveil réseau", "wake-on-LAN"),
        ParkDeployment.EtapeCopie => Lang.T("copie du paquet", "package copy"),
        ParkDeployment.EtapeInstallation => Lang.T("installation", "installation"),
        ParkDeployment.EtapeParc => Lang.T("mise en mode parc", "switch to fleet mode"),
        _ => etape,
    };

    private static string ResumeDeVerification(
        LectureDuJournal lecture, int demandes, List<string> ecartes, string journal)
    {
        int prets = lecture.Postes.Count(p => lecture.Lignes.Any(l =>
            string.Equals(l.Poste, p, StringComparison.OrdinalIgnoreCase)
            && string.Equals(l.Etape, ParkDeployment.EtapeGestionADistance, StringComparison.OrdinalIgnoreCase)
            && string.Equals(l.Etat, ParkDeployment.EtatOk, StringComparison.OrdinalIgnoreCase)));

        var resume = Lang.T(
            $"{demandes} poste(s) demandé(s), {lecture.Postes.Count} vu(s) dans le journal, {prets} prêt(s). Aucune machine n'a été modifiée.",
            $"{demandes} computer(s) requested, {lecture.Postes.Count} seen in the journal, {prets} ready. No machine was modified.");

        if (ecartes.Count > 0)
            resume += Lang.T($" {ecartes.Count} nom(s) écarté(s) : {string.Join(", ", ecartes)}.",
                             $" {ecartes.Count} name(s) set aside: {string.Join(", ", ecartes)}.");

        if (lecture.FichierAbsent)
            resume += Lang.T(" Le journal n'a pas été écrit : le script ne l'a peut-être pas atteint.",
                             " The journal was not written: the script may not have reached it.");

        foreach (var note in lecture.Notes) resume += " " + note;

        return resume + Lang.T($" Journal : {journal}", $" Journal: {journal}");
    }

    // ------------------------------------------------------------------
    // Déployer la sélection — point 64, lot C
    // ------------------------------------------------------------------

    private async void BtnInvDeployer_Click(object sender, RoutedEventArgs e)
    {
        if (_inventaireOccupe) return;

        // Une info-bulle appartient au message qui l'a posée : elle part avec lui.
        TxtInvStatus.ToolTip = null;

        var choisis = LignesAffichees().Where(l => l.Coche).ToList();

        if (choisis.Count == 0)
        {
            TxtInvStatus.Text = Lang.T("Aucun poste coché : rien à déployer.",
                                       "No computer ticked: nothing to deploy.");
            return;
        }

        if (choisis.Count > MaxParDeploiement)
        {
            TxtInvStatus.Text = Lang.T(
                $"{choisis.Count} postes cochés, le maximum est de {MaxParDeploiement} par déploiement. Procéder en plusieurs fois.",
                $"{choisis.Count} computers ticked, the maximum is {MaxParDeploiement} per deployment. Work in several passes.");
            return;
        }

        // LE PAQUET AVANT TOUT. Un chemin fautif découvert au milieu du lot
        // laisserait la moitié du parc installée et l'autre moitié non.
        var paquet = (TxtPaquet.Text ?? "").Trim().Trim('"').Trim();
        var problemePaquet = ParkDeployment.VerifierLePaquet(paquet);
        if (problemePaquet.Length > 0)
        {
            MajEtatDuPaquet();
            TxtInvStatus.Text = problemePaquet;
            return;
        }

        var politique = PowerShellPolicy.Read(TimeSpan.FromSeconds(8));
        if (politique is { Blocked: true })
        {
            MessageBox.Show(this,
                Lang.T($"Le déploiement ne peut pas démarrer : une stratégie de groupe interdit l'exécution de scripts sur ce poste ({politique.Scope} = {politique.Policy}).",
                       $"The deployment cannot start: a Group Policy forbids running scripts on this machine ({politique.Scope} = {politique.Policy}).")
                + "\n\n"
                + Lang.T("Ce réglage vient de l'administration du parc, et FaultTracePC ne le contourne pas — volontairement.",
                         "This setting comes from your fleet administration, and FaultTracePC does not work around it — deliberately."),
                "FaultTracePC", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        bool modeParc = ChkModeParc.IsChecked == true;

        // L'APERÇU : ce que la vérification a déjà appris de ces postes-là. Il ne
        // bloque rien — le script refait tous ses contrôles poste par poste — mais
        // il évite de découvrir au bout de vingt minutes ce qu'on pouvait savoir
        // en deux.
        int prets    = choisis.Count(l => l.Verdict is "pret" or "installe" or "parc");
        int echoues  = choisis.Count(l => l.Verdict == "echec");
        int inconnus = choisis.Count - prets - echoues;

        var version = ParkDeployment.VersionAnnonceeParLeNom(paquet);

        var question = Lang.T($"Déployer FaultTracePC sur {choisis.Count} poste(s) ?",
                              $"Deploy FaultTracePC on {choisis.Count} computer(s)?");

        var detail = Lang.T(
            $"{prets} déjà vérifié(s) et joignable(s) · {echoues} vérifié(s) en échec · {inconnus} non vérifié(s).",
            $"{prets} already checked and reachable · {echoues} checked and failing · {inconnus} not checked.")
            + "\n"
            + (version.Length > 0
                ? Lang.T($"Paquet : {Path.GetFileName(paquet)} — son nom annonce la version {version}.",
                         $"Package: {Path.GetFileName(paquet)} — its name announces version {version}.")
                : Lang.T($"Paquet : {Path.GetFileName(paquet)} — son nom n'annonce aucune version.",
                         $"Package: {Path.GetFileName(paquet)} — its name announces no version."))
            + "\n"
            + Lang.T("« Non vérifié » ne veut pas dire « en panne » : cela veut dire qu'on ne sait rien de ce poste. Le script refera tous ses contrôles, un par un.",
                     "“Not checked” does not mean “broken”: it means nothing is known about that computer. The script will redo every check, one by one.");

        // CE QUI VA RÉELLEMENT SE PASSER, écrit avant, pas découvert après.
        var consequences = Lang.T("Sur chaque poste :", "On each computer:") + "\n"
            + Lang.T("• le paquet est copié dans C:\\Windows\\Temp, installé en silence, puis effacé ;",
                     "• the package is copied to C:\\Windows\\Temp, installed silently, then deleted;") + "\n"
            + Lang.T("• un poste éteint sera RÉVEILLÉ par le réseau, puis traité ;",
                     "• a computer that is off will be WOKEN over the network, then processed;") + "\n"
            + Lang.T("• si la gestion à distance est arrêtée, elle sera démarrée et réglée pour démarrer à chaque ouverture du poste — ce réglage est DURABLE ;",
                     "• if remote management is stopped, it will be started and set to start on every boot — this setting is PERMANENT;") + "\n"
            + Lang.T("• une version plus ancienne est remplacée ; une version plus récente fait refuser l'installation.",
                     "• an older version is replaced; a newer version makes the install refuse.")
            + "\n\n"
            + (modeParc
                ? Lang.T("Mode parc demandé : le secret maître sera demandé UNE FOIS dans la fenêtre PowerShell, sans rien afficher. Il n'est jamais écrit sur le disque.",
                         "Fleet mode requested: the master secret will be asked ONCE in the PowerShell window, with nothing displayed. It is never written to disk.")
                : Lang.T("Mode parc NON demandé : les postes seront installés mais resteront invisibles depuis cette console.",
                         "Fleet mode NOT requested: the computers will be installed but will stay invisible from this console."));

        if (!ConfirmationChiffreeWindow.Demander(this, choisis.Count, question, detail, consequences)) return;

        OccuperInventaire(true, Lang.T(
            $"Déploiement sur {choisis.Count} poste(s) — le résultat s'affichera ici quand tu auras fermé la fenêtre PowerShell.",
            $"Deploying to {choisis.Count} computer(s) — the result will appear here once you close the PowerShell window."));
        try
        {
            var reglages = ParametresParc.Charger();
            reglages.CheminDuPaquet = paquet;

            // LES RÉGLAGES DU RÉVEIL RÉSEAU SONT PRIS DANS LA FENÊTRE, pas sur le
            // disque : ils viennent peut-être d'être saisis, et un déploiement lancé
            // dans la foulée doit en tenir compte sans qu'il faille d'abord
            // actualiser l'inventaire pour les enregistrer.
            reglages.ServeurDhcp = (TxtServeurDhcp.Text ?? "").Trim();
            reglages.FichierAdressesMac = (TxtFichierMac.Text ?? "").Trim();
            reglages.Enregistrer();

            var dossier = ScriptDeDeploiement.DossierParDefaut;
            var script = ScriptDeDeploiement.Extraire(dossier);

            var ecartes = new List<string>();
            var listeDesPostes = Path.Combine(dossier, "postes-a-deployer.txt");
            int retenus = ListeDePostes.Ecrire(listeDesPostes, choisis.Select(l => l.Nom), ecartes);

            if (retenus == 0)
            {
                TxtInvStatus.Text = Lang.T("Aucun nom de poste exploitable dans la sélection.",
                                           "No usable computer name in the selection.");
                return;
            }

            var journal = Path.Combine(dossier, $"deploiement_{DateTime.Now:yyyy-MM-dd_HHmmss}.jsonl");

            foreach (var l in choisis) l.Etat = Lang.T("en cours…", "in progress…");

            // La langue du parc suit celle de l'interface : un rapport produit sur un
            // poste distant sort dans la langue de l'administrateur, pas dans celle
            // que le poste a par hasard.
            var langue = Lang.Current == AppLanguage.English ? "en" : "fr";

            var parametres = new List<KeyValuePair<string, string?>>
            {
                new("Msi", paquet),
                new("FichierPostes", listeDesPostes),
                new("SortieJson", journal),
                new("Langue", langue),
            };
            if (modeParc) parametres.Add(new("ConfigurerParc", null));

            // CE QUI MANQUAIT POUR QUE LE RÉVEIL RÉSEAU PUISSE ABOUTIR.
            //
            // Le script cherche l'adresse MAC d'un poste éteint dans trois sources,
            // par ordre de fiabilité : le serveur DHCP, postes.csv, le cache ARP.
            // Lancé depuis cette console il recevait -SortieJson, se taisait par
            // construction, et ne demandait donc JAMAIS le nom du serveur DHCP ;
            // quant au champ « Annuaire d'adresses MAC », il ne remplissait qu'une
            // colonne et n'arrivait pas jusqu'au script. Les deux premières sources
            // étaient hors d'atteinte, la troisième est vide pour un poste éteint :
            // aucun réveil ne pouvait aboutir, quelle que soit la machine visée.
            //
            // Constaté le 21/09/2026 sur quatre postes que d'autres outils
            // réveillent sans difficulté.
            //
            // LE CHEMIN N'EST TRANSMIS QUE S'IL EST SAISI. Vide, le script garde son
            // comportement d'origine — postes.csv à côté de lui — qui est le bon
            // quand on le lance à la main depuis une clé USB.
            if (reglages.ServeurDhcp.Length > 0)
                parametres.Add(new("ServeurDhcp", reglages.ServeurDhcp));

            if (reglages.FichierAdressesMac.Length > 0)
                parametres.Add(new("FichierMac",
                    ParkInventory.CheminAdressesMac(reglages.FichierAdressesMac, DossierDesDonnees)));

            using var processus = Process.Start(new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = PowerShellLauncher.ArgumentsForScript(script, L.PsClose, parametres, pauseTousLesCas: true),
                UseShellExecute = true,
            });

            if (processus is null)
            {
                TxtInvStatus.Text = Lang.T("PowerShell n'a pas pu être lancé.", "PowerShell could not be started.");
                return;
            }

            await processus.WaitForExitAsync();

            var lecture = ParkDeployment.Lire(journal);
            AppliquerLesVerdicts(lecture, choisis);

            int inscrits = InscrireDansLaSupervision(choisis);

            TxtInvStatus.Text = ResumeDeDeploiement(lecture, choisis, ecartes, journal, inscrits);
        }
        catch (Exception ex)
        {
            TxtInvStatus.Text = Lang.T($"Déploiement impossible : {ex.Message}",
                                       $"Deployment failed: {ex.Message}");
        }
        finally
        {
            OccuperInventaire(false, null);
        }
    }

    /// <summary>
    /// Inscrit dans l'onglet Supervision les postes qui viennent d'être mis en mode
    /// parc, et rend le nombre de lignes ajoutées.
    ///
    /// POURQUOI C'EST AUTOMATIQUE DEPUIS LE 21/09/2026. Le script affichait « À
    /// saisir dans la console de parc » et l'administrateur recopiait à la main un
    /// nom, un hôte et un port que le logiciel venait lui-même d'établir. Sur un
    /// poste c'est agaçant ; sur trente c'est une source de fautes de frappe, et une
    /// faute de frappe dans un nom donne un jeton faux, donc un refus 403 que rien
    /// n'explique — exactement le piège du 19/09/2026.
    ///
    /// SEULS LES POSTES EN MODE PARC SONT INSCRITS. Un poste simplement installé
    /// n'écoute pas : l'inscrire fabriquerait une ligne qui échoue toujours, et une
    /// ligne rouge sans cause est pire que pas de ligne du tout.
    ///
    /// L'HÔTE EST LE NOM, JAMAIS L'ADRESSE. En DHCP l'adresse change ; le nom, non.
    /// C'est déjà ce que le script recommande à l'écran.
    ///
    /// AUCUN JETON N'EST INSCRIT : il se déduit du secret maître et du nom de
    /// machine à chaque interrogation. Écrire un jeton ici ressusciterait la liste
    /// de secrets dans Documents dont on s'est débarrassé.
    ///
    /// UN POSTE DÉJÀ PRÉSENT N'EST PAS TOUCHÉ. On ne réécrit pas une ligne que
    /// l'administrateur a peut-être ajustée à la main — port différent, jeton
    /// historique. Ajouter, jamais écraser.
    /// </summary>
    private int InscrireDansLaSupervision(List<LigneInventaire> choisis)
    {
        var aInscrire = choisis.Where(l => l.Verdict == "parc" && l.Nom.Length > 0).ToList();
        if (aInscrire.Count == 0) return 0;

        var deja = new HashSet<string>(_machines.Select(m => m.Name), StringComparer.OrdinalIgnoreCase);

        int ajoutes = 0;
        foreach (var ligne in aInscrire)
        {
            if (!deja.Add(ligne.Nom)) continue;

            // Le port reste celui que ParkMachine porte par défaut : c'est aussi
            // celui que le script emploie, et la console ne lui en impose pas
            // d'autre. Le jour où elle le ferait, les deux devraient se lire au
            // même endroit — pas être recopiés ici.
            _machines.Add(new ParkMachine { Name = ligne.Nom, Host = ligne.Nom });
            ajoutes++;
        }

        if (ajoutes == 0) return 0;

        SaveMachines();
        RenderRows(null);
        return ajoutes;
    }

    /// <summary>
    /// Le bilan, compté sur les CODES et non sur les phrases affichées : un
    /// décompte ne doit pas dépendre de la langue de l'interface.
    /// </summary>
    private static string ResumeDeDeploiement(
        LectureDuJournal lecture, List<LigneInventaire> choisis, List<string> ecartes, string journal, int inscrits)
    {
        int enParc     = choisis.Count(l => l.Verdict == "parc");
        int installes  = choisis.Count(l => l.Verdict == "installe") + enParc;
        int echoues    = choisis.Count(l => l.Verdict == "echec");
        int sansTrace  = choisis.Count(l => l.Verdict == "absent");

        var resume = Lang.T(
            $"{choisis.Count} poste(s) demandé(s) : {installes} installé(s), dont {enParc} visible(s) de la console · {echoues} en échec · {sansTrace} sans aucune trace dans le journal.",
            $"{choisis.Count} computer(s) requested: {installes} installed, of which {enParc} visible from the console · {echoues} failed · {sansTrace} with no trace at all in the journal.");

        if (inscrits > 0)
            resume += Lang.T($" {inscrits} poste(s) ajouté(s) à l'onglet Supervision — rien à recopier.",
                             $" {inscrits} computer(s) added to the Monitoring tab — nothing to retype.");

        if (sansTrace > 0)
            resume += Lang.T(" Un poste sans trace n'a pas été atteint par le script : il n'est ni installé, ni en échec, on ne sait simplement rien de lui.",
                             " A computer with no trace was never reached by the script: it is neither installed nor failed, nothing is known about it.");

        if (ecartes.Count > 0)
            resume += Lang.T($" {ecartes.Count} nom(s) écarté(s) : {string.Join(", ", ecartes)}.",
                             $" {ecartes.Count} name(s) set aside: {string.Join(", ", ecartes)}.");

        if (lecture.FichierAbsent)
            resume += Lang.T(" Le journal n'a pas été écrit : le script ne l'a peut-être pas atteint.",
                             " The journal was not written: the script may not have reached it.");

        foreach (var note in lecture.Notes) resume += " " + note;

        return resume + Lang.T($" Journal : {journal}", $" Journal: {journal}");
    }

    // ------------------------------------------------------------------
    // Mise en forme
    // ------------------------------------------------------------------

    private static LigneInventaire Convertir(PosteDuParc p)
    {
        // Sans compte trouvé, « actif » et une date de session seraient des
        // affirmations sans source : un poste saisi à la main dans la console n'a
        // pas de compte d'ordinateur connu de ce logiciel.
        //
        // LE LIBELLÉ DIT CE QU'ON A CHERCHÉ, PAS CE QUI EXISTE.
        //
        // La recherche annuaire part de l'unité d'organisation saisie et descend
        // (SearchScope.Subtree dans ParkDirectory). Elle ne voit donc RIEN de ce
        // qui vit ailleurs dans le domaine. Écrire « hors annuaire » — ce que
        // faisait ce code jusqu'au 21/09/2026 — affirmait que le poste n'existe
        // pas dans l'annuaire, alors que le logiciel n'en sait rien : il a
        // seulement constaté que cette unité-là ne le contient pas.
        //
        // Signalé le 21/09/2026 : des postes bien présents dans l'annuaire, mais
        // dans une autre unité, étaient annoncés « hors annuaire ». Le fait était
        // juste, la phrase était fausse.
        bool vuParLAnnuaire = p.Sources.HasFlag(SourcesDuPoste.ActiveDirectory);
        var horsAnnuaire = Lang.T("pas dans cette unité", "not in this OU");

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

    /// <summary>
    /// Écrit la barre d'état, et accroche au survol le détail quand il y en a un.
    /// Passer par ici plutôt que par <c>TxtInvStatus.Text</c> évite qu'une info-bulle
    /// périmée survive au message qu'elle accompagnait.
    /// </summary>
    private void Statut(string texte)
    {
        TxtInvStatus.Text = texte;

        // LE SURVOL DONNE TOUJOURS QUELQUE CHOSE À LIRE : le détail quand il y en a
        // un, sinon le texte complet. La barre n'affiche que deux lignes et coupe le
        // reste — sans ça, une phrase un peu longue finirait par « … » et personne
        // ne pourrait en voir la fin. Constaté le 19/09/2026.
        TxtInvStatus.ToolTip = _detailDuStatut.Length > 0 ? _detailDuStatut : texte;
        _detailDuStatut = "";
    }

    private void OccuperInventaire(bool occupe, string? message)
    {
        _inventaireOccupe = occupe;
        BtnInvListerUnites.IsEnabled = !occupe;
        BtnInvActualiser.IsEnabled = !occupe;
        BtnInvVerifier.IsEnabled = !occupe;
        BtnInvDeployer.IsEnabled = !occupe;
        BtnInvReprendre.IsEnabled = !occupe;
        Cursor = occupe ? System.Windows.Input.Cursors.Wait : null;
        if (message is not null) Statut(message);
    }
}
