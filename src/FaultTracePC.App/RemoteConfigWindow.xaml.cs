using System.Windows;
using FaultTracePC.Core;

namespace FaultTracePC.App;

/// <summary>
/// Configuration du mode réseau (Local / Client) de cette machine.
///
/// Le jeton n'est plus tiré au sort ni affiché : il se DÉDUIT du secret maître du
/// parc et du nom Windows de la machine, exactement comme le fait
/// « --configure-remote » en ligne de commande. Deux chemins qui se ressemblent
/// devaient produire le même résultat ; l'ancien bouton « Copier le token »
/// invitait par ailleurs à promener sur le réseau ce qu'on cherche à protéger.
/// </summary>
public partial class RemoteConfigWindow : Window
{
    public RemoteConfigWindow()
    {
        InitializeComponent();
        var cfg = RemoteConfig.Load();
        RbClient.IsChecked = string.Equals(cfg.Mode, "Client", StringComparison.OrdinalIgnoreCase);
        RbLocal.IsChecked = !RbClient.IsChecked;
        TxtPort.Text = cfg.Port.ToString();

        // Le nom qu'il faudra inscrire dans la console : le montrer évite d'aller
        // le chercher ailleurs, et de se tromper de libellé.
        TxtMachine.Text = Environment.MachineName;
        MajEtat(cfg);
    }

    private void MajEtat(RemoteConfig cfg) =>
        TxtEtat.Text = (string.IsNullOrEmpty(cfg.Token) ? L.NetTokenAbsent : L.NetTokenPresent)
                     + "  " + (RemoteConfig.ConfigEstProtegee ? L.NetFileProtected : L.NetFileOpen);

    private void BtnApply_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var cfg = RemoteConfig.Load();

            if (RbClient.IsChecked == true)
            {
                if (!int.TryParse(TxtPort.Text, out var port) || port is < 1024 or > 65535)
                {
                    // Défaut constaté au passage : ce message était en dur, donc
                    // français pour tout le monde. Aucun des trois signaux du garde
                    // de traduction ne le voyait — ni accent, ni chevron, ni espace
                    // avant ponctuation, et pas trois mots-outils français.
                    MessageBox.Show(this, Lang.T("Port invalide (1024–65535).", "Invalid port (1024–65535)."), "FaultTracePC",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                // Un secret saisi recalcule le jeton ; un champ laissé vide
                // conserve celui qui est déjà en place. Sans ni l'un ni l'autre,
                // on refuse : tirer un jeton au sort ici rendrait cette machine
                // introuvable par une console qui, elle, dérive.
                string jeton;
                var secret = PwdSecret.Password;

                if (!string.IsNullOrWhiteSpace(secret))
                {
                    try
                    {
                        jeton = RemoteConfig.DeriveToken(secret, Environment.MachineName);
                    }
                    catch (ArgumentException)
                    {
                        MessageBox.Show(this,
                            Lang.T($"Secret maître trop court — {RemoteConfig.MasterSecretMinLength} caractères au minimum.\n\n", $"Master secret too short — {RemoteConfig.MasterSecretMinLength} characters minimum.\n\n") +
                            Lang.T("Produis-en un avec « FaultTracePC.Cli.exe --generate-master-secret », et garde-le dans un gestionnaire de mots de passe.", "Produce one with “FaultTracePC.Cli.exe --generate-master-secret”, and keep it in a password manager."),
                            "FaultTracePC", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }
                else if (!string.IsNullOrEmpty(cfg.Token))
                {
                    jeton = cfg.Token;
                }
                else
                {
                    MessageBox.Show(this,
                        Lang.T("Le secret maître du parc est nécessaire pour calculer le jeton de cette machine.\n\n", "The fleet master secret is needed to compute this machine's token.\n\n") +
                        Lang.T("Il ne sera pas enregistré ici : la machine s'en sert le temps du calcul, puis l'oublie.", "It will not be stored here: the machine uses it for the computation, then forgets it."),
                        "FaultTracePC", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                cfg.Mode = "Client";
                cfg.Port = port;
                cfg.Token = jeton;
                cfg.Save();

                PwdSecret.Clear();     // il n'a rien à faire à l'écran une seconde de plus
                MajEtat(cfg);

                MonitorServiceManager.EnsureFirewallRule(port);
                TxtStatus.Text = Lang.T("Mode Client appliqué.", "Client mode applied.");
            }
            else
            {
                cfg.Mode = "Local";
                cfg.Save();
                MajEtat(cfg);
                MonitorServiceManager.RemoveFirewallRule();
                TxtStatus.Text = Lang.T("Mode Local appliqué — plus rien n'est exposé.", "Local mode applied — nothing is exposed any more.");
            }

            // Le service lit la configuration au démarrage. On REDÉPLOIE la dernière
            // compilation (pas un simple redémarrage) : sinon une ancienne version du
            // service, sans l'API, resterait en place et la machine serait injoignable.
            if (MonitorServiceManager.GetState() != MonitorState.NotInstalled)
            {
                var (ok, msg) = MonitorServiceManager.InstallAndStart();
                TxtStatus.Text += ok ? Lang.T(" Service mis à jour et redémarré.", " Service updated and restarted.") : $" ⚠ {msg}";
            }
            else if (RbClient.IsChecked == true)
            {
                TxtStatus.Text += Lang.T(" ⚠ Installe la surveillance (bouton 📡) pour activer l'API.", " ⚠ Install monitoring (📡 button) to enable the API.");
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, Lang.T("Échec : ", "Failed: ") + ex.Message, "FaultTracePC",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
}
