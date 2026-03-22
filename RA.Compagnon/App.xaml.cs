using System.Windows;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace RA.Compagnon;

/// <summary>
/// Initialise l'application et applique le thème WPF-UI au démarrage.
/// </summary>
public partial class App : Application
{
    /// <summary>
    /// Applique le thème global avant l'affichage de la première fenêtre.
    /// </summary>
    protected override void OnStartup(StartupEventArgs e)
    {
        ApplicationThemeManager.Apply(
            ApplicationTheme.Dark,
            WindowBackdropType.Mica,
            updateAccent: true
        );

        base.OnStartup(e);
    }
}
