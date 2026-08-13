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
            TxtStatus.Text = "Token copié dans le presse-papiers.";
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
                TxtStatus.Text = "Mode Client appliqué.";
            }
            else
            {
                cfg.Mode = "Local";
                cfg.Save();
                MonitorServiceManager.RemoveFirewallRule();
                TxtStatus.Text = "Mode Local appliqué — plus rien n'est exposé.";
            }

            // Le service lit la configuration au démarrage. On REDÉPLOIE la dernière
            // compilation (pas un simple redémarrage) : sinon une ancienne version du
            // service, sans l'API, resterait en place et la machine serait injoignable.
            if (MonitorServiceManager.GetState() != MonitorState.NotInstalled)
            {
                var (ok, msg) = MonitorServiceManager.InstallAndStart();
                TxtStatus.Text += ok ? " Service mis à jour et redémarré." : $" ⚠ {msg}";
            }
            else if (RbClient.IsChecked == true)
            {
                TxtStatus.Text += " ⚠ Installe la surveillance (bouton 📡) pour activer l'API.";
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Échec : " + ex.Message, "FaultTracePC",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
}
