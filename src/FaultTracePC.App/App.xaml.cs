using System.Windows;
using FaultTracePC.Core;

namespace FaultTracePC.App;

/// <summary>
/// Logique applicative de App.xaml.
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // AVANT base.OnStartup, qui crée la fenêtre désignée par StartupUri :
        // un texte affiché avant la résolution de la langue le serait dans la
        // mauvaise, et une fenêtre déjà construite ne se retraduit pas toute
        // seule.
        Lang.Initialize(e.Args);
        base.OnStartup(e);
    }
}
