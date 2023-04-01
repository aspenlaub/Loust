using System.Windows;

namespace Aspenlaub.Net.GitHub.CSharp.Loust;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App {
    protected override void OnStartup(StartupEventArgs e) {
        base.OnStartup(e);
        StartupUri = new System.Uri("Gui/LoustWindow.xaml", System.UriKind.Relative);
    }
}