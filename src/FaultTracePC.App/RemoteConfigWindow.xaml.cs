using System.Windows;
using FaultTracePC.Core;

namespace FaultTracePC.App;

/// <summary>Configuration du mode réseau (Local / Client) de cette machine.</summary>
public partial class RemoteConfigWindow : Window
{
    public RemoteConfigWindow()
    {
        InitializeComponent();
        var cfg = RemoteConfig.Load();
        RbClient.IsChecked = string.Equals(cfg.Mode, "Client", StringComparison.OrdinalIgnoreCase);
        RbLocal.IsChecked = !RbClient.IsChecked;
        TxtPort.Text = cfg.Port.ToString();
        TxtToken.Text = cfg.Token;
    }

    private void BtnGenerate_Click(object sender, RoutedEventArgs e) =>
        TxtToken.Text = RemoteConfig.GenerateToken();

    private void BtnCopyToken_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(TxtToken.Text))
        {
            Clipboard.SetText(TxtToken.Text);
            TxtStatus.Text = Lang.T("Token copié dans le presse-papiers.", "Token copied to the clipboard.");
        }
    }

    private void BtnApply_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var cfg = RemoteConfig.Load();

            if (RbClient.IsChecked == true)
            {
                if (!int.TryParse(TxtPort.Text, out var port) || port is < 1024 or > 65535)
                {
                    MessageBox.Show(this, "Port invalide (1024–65535).", "FaultTracePC",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                if (string.IsNullOrEmpty(TxtToken.Text))
                    TxtToken.Text = RemoteConfig.GenerateToken();

                cfg.Mode = "Client";
                cfg.Port = port;
                cfg.Token = TxtToken.Text;
                cfg.Save();

                MonitorServiceManager.EnsureFirewallRule(port);
                TxtStatus.Text = Lang.T("Mode Client appliqué.", "Client mode applied.");
            }
            else
            {
                cfg.Mode = "Local";
                cfg.Save();
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
