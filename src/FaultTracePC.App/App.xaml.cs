using System.Windows;
using System.Windows.Threading;
using FaultTracePC.Core;

namespace FaultTracePC.App;

/// <summary>
/// Logique applicative de App.xaml.
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // LES GARDES D'ABORD, AVANT MÊME LA LANGUE
        //
        // Un utilisateur a signalé une fenêtre qui « s'ouvre et se referme
        // aussitôt » : aucune exception n'était rattrapée, rien n'était écrit, et
        // ni lui ni nous n'avons pu conclure quoi que ce soit. Une panne survenue
        // pendant l'initialisation de la langue doit donc, elle aussi, laisser une
        // trace — le message serait alors en français, langue par défaut, ce qui
        // est un moindre mal devant une fenêtre qui disparaît sans un mot.
        DispatcherUnhandledException += (_, args) =>
        {
            Report("ui", args.Exception);

            // On maintient l'application en vie. Une exception d'interface ne
            // laisse pas forcément un état inutilisable, et l'utilisateur ferme
            // lui-même s'il juge que plus rien ne répond. Disparaître en silence
            // est le seul comportement qu'on s'interdit.
            args.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            // Le processus s'arrête de toute façon : écrire vient avant afficher.
            Report("process", args.ExceptionObject as Exception);

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Report("background task", args.Exception);
            args.SetObserved();
        };

        // AVANT base.OnStartup, qui crée la fenêtre désignée par StartupUri :
        // un texte affiché avant la résolution de la langue le serait dans la
        // mauvaise, et une fenêtre déjà construite ne se retraduit pas toute
        // seule.
        Lang.Initialize(e.Args);
        base.OnStartup(e);
    }

    /// <summary>
    /// Écrit la panne dans le journal, puis la montre. Dans cet ordre : si
    /// l'affichage échouait à son tour, la trace serait déjà sur le disque.
    /// </summary>
    private static void Report(string origin, Exception? ex)
    {
        if (ex is null) return;

        var chemin = ErrorLog.Write(origin, ex);

        var message =
            Lang.T("FaultTracePC a rencontré une erreur inattendue.", "FaultTracePC hit an unexpected error.")
            + "\n\n" + Shorten(ex.Message) + "\n\n"
            + (chemin is null
                ? Lang.T("Le détail technique n'a pas pu être enregistré — le dossier ProgramData est peut-être protégé sur cette machine.",
                         "The technical detail could not be written — the ProgramData folder may be protected on this machine.")
                : Lang.T("Le détail technique a été enregistré dans ce fichier :", "The technical detail has been written to this file:")
                  + "\n" + chemin + "\n\n"
                  + Lang.T("Envoie-le pour que la panne puisse être analysée.", "Send it so the fault can be analysed."));

        try
        {
            MessageBox.Show(message, "FaultTracePC", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        catch
        {
            // Plus d'interface disponible — le journal reste, c'est l'essentiel.
        }
    }

    /// <summary>Un message d'exception peut faire plusieurs milliers de caractères.</summary>
    private static string Shorten(string s) =>
        s.Length <= 400 ? s : s[..400] + "…";
}
