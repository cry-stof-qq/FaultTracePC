using System.Diagnostics;
using System.Windows;
using FaultTracePC.Core;

namespace FaultTracePC.App;

/// <summary>
/// POINT 116 (demandé le 23/10/2026, lot 2). Exporter les journaux de Windows en
/// .evtx, pour les confier à la personne de son choix.
///
/// Ce que l'auteur a arrêté, et que cette fenêtre tient :
///   · une OPTION : sans clic sur « Exporter », rien ne se passe ;
///   · deux choix : Système et Application, ou le journal Sécurité — celui-ci
///     derrière une case à cocher que l'utilisateur coche en connaissance de cause ;
///   · trois périodes : 30 jours, 90 jours, ou complet — avec la taille réelle des
///     journaux affichée avant, et l'avertissement qu'un gros fichier est difficile
///     à envoyer et que des données anciennes servent rarement ;
///   · deux avertissements : ce n'est pas un diagnostic ; choisir à qui on le
///     confie, en disant CONCRÈTEMENT ce que le fichier contient ;
///   · rien n'est envoyé : un dossier local, ouvert dans l'Explorateur ;
///   · « Annuler » est le bouton par défaut.
/// </summary>
public partial class ExportJournauxWindow : Window
{
    /// <summary>Dossier écrit, si au moins un fichier a été exporté.</summary>
    public string? DossierEcrit { get; private set; }

    public ExportJournauxWindow(Window proprietaire)
    {
        InitializeComponent();
        Owner = proprietaire;

        Title = Lang.T("Exporter les journaux Windows (.evtx)", "Export the Windows logs (.evtx)");
        TxtTitre.Text = Lang.T("📤 Exporter les journaux Windows pour les confier à quelqu'un",
                               "📤 Export the Windows logs to hand them to someone");
        TxtIntro.Text = Lang.T(
            "Les journaux de Windows sont écrits dans des fichiers .evtx, que la personne de ton choix pourra ouvrir sur son propre PC. Rien n'est envoyé : les fichiers sont écrits dans ton dossier Documents, et c'est toi qui décides à qui les transmettre.",
            "The Windows logs are written to .evtx files that the person of your choice can open on their own PC. Nothing is sent: the files are written to your Documents folder, and you decide who to pass them to.");
        TxtPasUnDiagnostic.Text = Lang.T(
            "ℹ Ce n'est pas un diagnostic. Ces fichiers contiennent les événements bruts de Windows, sans aucune analyse — ni les écrans bleus, ni la santé des disques, ni les températures, ni la boîte noire. Si quelqu'un doit t'aider à trouver une panne, envoie-lui d'abord le rapport de FaultTracePC : c'est lui qui contient l'analyse. Ces fichiers ne servent qu'en complément.",
            "ℹ This is not a diagnosis. These files contain the raw Windows events, with no analysis at all — no blue screens, no drive health, no temperatures, no black box. If someone is to help you find a fault, send them the FaultTracePC report first: that is where the analysis is. These files are only a complement.");

        TxtQuoi.Text = Lang.T("Quels journaux ?", "Which logs?");
        RadSystemeApplication.Content = Lang.T("Système et Application — ce qui sert à comprendre une panne",
                                               "System and Application — what helps to understand a fault");
        RadSecurite.Content = Lang.T("Sécurité — les ouvertures de session de chaque personne (rarement utile à un diagnostic)",
                                     "Security — the sign-ins of every person (rarely useful for a diagnosis)");

        TxtPeriode.Text = Lang.T("Quelle période ?", "Which period?");
        RadJours30.Content = Lang.T("Les 30 derniers jours (comme l'analyse)", "The last 30 days (like the analysis)");
        RadJours90.Content = Lang.T("Les 90 derniers jours", "The last 90 days");
        RadComplet.Content = Lang.T("Le journal complet — tout ce que Windows a gardé", "The full log — everything Windows has kept");

        TxtChkSecurite.Text = Lang.T(
            "Je sais que ce fichier contient les ouvertures de session de toutes les personnes qui utilisent ce PC, et je le confie à quelqu'un de confiance.",
            "I know this file contains the sign-ins of everyone who uses this PC, and I am handing it to someone I trust.");

        BtnAnnuler.Content = Lang.T("Annuler", "Cancel");
        BtnExporter.Content = Lang.T("Exporter", "Export");

        MettreAJour();
    }

    private JournauxAExporter Choix =>
        RadSecurite?.IsChecked == true ? JournauxAExporter.Securite : JournauxAExporter.SystemeEtApplication;

    /// <summary>Null = journal complet.</summary>
    private int? Jours =>
        RadComplet?.IsChecked == true ? null : RadJours90?.IsChecked == true ? 90 : 30;

    private void Option_Changed(object sender, RoutedEventArgs e) => MettreAJour();

    /// <summary>
    /// Appelée aussi PENDANT InitializeComponent (les boutons cochés dans le XAML
    /// déclenchent leur événement) : les éléments pas encore créés sont ignorés.
    /// </summary>
    private void MettreAJour()
    {
        if (PanComplet is null || TxtComplet is null || ChkSecurite is null
            || TxtConfidentialite is null || BtnExporter is null) return;

        bool securite = Choix == JournauxAExporter.Securite;
        bool complet = Jours is null;

        PanComplet.Visibility = complet ? Visibility.Visible : Visibility.Collapsed;
        if (complet) TxtComplet.Text = TexteComplet();

        ChkSecurite.Visibility = securite ? Visibility.Visible : Visibility.Collapsed;
        if (!securite) ChkSecurite.IsChecked = false;

        TxtConfidentialite.Text = TexteConfidentialite(securite);

        // Garde-fou : pas d'export du journal Sécurité sans la case cochée.
        BtnExporter.IsEnabled = !securite || ChkSecurite.IsChecked == true;
    }

    private string TexteComplet()
    {
        var tailles = ExportJournaux.Journaux(Choix)
            .Select(j => (Journal: NomAffiche(j), Taille: ExportJournaux.TailleDuJournal(j)))
            .Select(x => x.Taille is long t
                ? Lang.T($"{x.Journal} : {Mo(t)}", $"{x.Journal}: {Mo(t)}")
                : Lang.T($"{x.Journal} : taille non lue", $"{x.Journal}: size not read"));
        return Lang.T(
            $"⚠ Taille actuelle des journaux sur ce PC — {string.Join(" · ", tailles)}. Un gros fichier est difficile à envoyer : beaucoup de messageries refusent les pièces jointes au-delà de 20 à 25 Mo. Et le journal complet peut remonter à plusieurs mois : des événements anciens aident rarement à comprendre une panne récente. Si tu n'as pas de raison précise, préfère 30 ou 90 jours.",
            $"⚠ Current size of the logs on this PC — {string.Join(" · ", tailles)}. A large file is hard to send: many mail services refuse attachments above 20 to 25 MB. And the full log can go back several months: old events rarely help to understand a recent fault. Unless you have a specific reason, prefer 30 or 90 days.");
    }

    private static string TexteConfidentialite(bool securite)
    {
        var texte = Lang.T(
            "🔒 Choisis bien à qui tu confies ces fichiers. Ils contiennent beaucoup d'informations sur ce PC et sur ceux qui s'en servent : le nom du PC ; des noms de comptes et des chemins du type C:\\Users\\prénom.nom ; les logiciels installés et désinstallés ; les clés USB et appareils branchés ; des noms de réseaux ; les dates et heures où le PC a été allumé et utilisé. Ne les dépose pas sur un forum ou un site public : n'importe qui pourrait les lire, et un fichier envoyé ne se reprend pas.",
            "🔒 Choose carefully who you hand these files to. They contain a lot of information about this PC and the people who use it: the PC's name; account names and paths such as C:\\Users\\firstname.lastname; the software installed and uninstalled; the USB sticks and devices plugged in; network names; the dates and times the PC was switched on and used. Do not post them on a forum or a public website: anyone could read them, and a file once sent cannot be taken back.");
        if (securite)
            texte += Lang.T(
                " Le journal Sécurité y ajoute chaque ouverture et fermeture de session, avec le nom du compte et l'heure — et, sur un PC partagé, celles des autres personnes, pas seulement les tiennes.",
                " The Security log adds every sign-in and sign-out, with the account name and the time — and, on a shared PC, those of the other people too, not only yours.");
        return texte;
    }

    /// <summary>Nom du journal tel que l'utilisateur le connaît (wevtutil, lui, veut l'anglais).</summary>
    private static string NomAffiche(string journal) => journal switch
    {
        "System" => Lang.T("Système", "System"),
        "Security" => Lang.T("Sécurité", "Security"),
        _ => journal,
    };

    private static string Mo(long octets) =>
        Lang.T($"{octets / 1048576.0:0.#} Mo", $"{octets / 1048576.0:0.#} MB");

    private async void BtnExporter_Click(object sender, RoutedEventArgs e)
    {
        // Deuxième verrou, au cas où le bouton aurait été actif par erreur.
        if (Choix == JournauxAExporter.Securite && ChkSecurite.IsChecked != true) return;

        var choix = Choix;
        var jours = Jours;
        var dossier = ExportJournaux.DossierParDefaut(Environment.MachineName, DateTime.Now);

        PanOptions.IsEnabled = false;
        ChkSecurite.IsEnabled = false;
        BtnExporter.IsEnabled = false;
        BtnAnnuler.IsEnabled = false;
        TxtEtat.Text = jours is null
            ? Lang.T("Export en cours… Un journal complet peut demander plusieurs minutes.", "Exporting… A full log can take several minutes.")
            : Lang.T("Export en cours…", "Exporting…");

        var resultat = await Task.Run(() => ExportJournaux.Exporter(choix, jours, dossier));

        if (resultat.Fichiers.Count > 0)
        {
            DossierEcrit = resultat.Dossier;
            try { Process.Start("explorer.exe", $"\"{resultat.Dossier}\""); }
            catch { /* le chemin est aussi donné dans la boîte à outils */ }
        }

        if (resultat.Reussi)
        {
            DialogResult = true;
            return;
        }

        var detail = string.Join(Environment.NewLine, resultat.Erreurs);
        if (resultat.Fichiers.Count > 0)
        {
            MessageBox.Show(this,
                Lang.T($"Export terminé, avec des réserves :{Environment.NewLine}{Environment.NewLine}{detail}",
                       $"Export finished, with reservations:{Environment.NewLine}{Environment.NewLine}{detail}"),
                Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            DialogResult = true;
            return;
        }

        MessageBox.Show(this,
            Lang.T($"L'export a échoué :{Environment.NewLine}{Environment.NewLine}{detail}",
                   $"The export failed:{Environment.NewLine}{Environment.NewLine}{detail}"),
            Title, MessageBoxButton.OK, MessageBoxImage.Error);
        TxtEtat.Text = "";
        PanOptions.IsEnabled = true;
        ChkSecurite.IsEnabled = true;
        BtnAnnuler.IsEnabled = true;
        MettreAJour();
    }
}
