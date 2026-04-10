using System.Reflection;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace RA.Compagnon;

public partial class MainWindow
{
    /// <summary>
    /// Applique les éléments de chrome de la fenêtre principale.
    /// </summary>
    private void InitialiserHabillageFenetre()
    {
        AppliquerIconeApplication();
        AppliquerVersionApplication();
    }

    /// <summary>
    /// Charge l'icône applicative depuis le fichier ICO embarqué et l'applique à la fenêtre.
    /// </summary>
    private void AppliquerIconeApplication()
    {
        Uri uriIcone = new("pack://application:,,,/rac.ico", UriKind.Absolute);
        IconBitmapDecoder decodeur = new(
            uriIcone,
            BitmapCreateOptions.PreservePixelFormat,
            BitmapCacheOption.OnLoad
        );

        if (decodeur.Frames.Count == 0)
        {
            return;
        }

        ImageSource imageIcone = decodeur.Frames[0];
        Icon = imageIcone;
        ImageIconeTitre.Source = imageIcone;
    }

    /// <summary>
    /// Affiche la version courante de l'application dans l'interface.
    /// </summary>
    private void AppliquerVersionApplication()
    {
        if (TexteVersionApplication is null)
        {
            return;
        }

        Assembly assembly = typeof(MainWindow).Assembly;
        Version? versionAssembly = assembly.GetName().Version;
        string? version = versionAssembly is null
            ? null
            : $"{versionAssembly.Major}.{versionAssembly.Minor}.{Math.Max(0, versionAssembly.Build)}";

        if (string.IsNullOrWhiteSpace(version))
        {
            version = "inconnue";
        }

        TexteVersionApplication.Text = $"Version {version}";
        TexteVersionApplication.ToolTip = $"RA-Compagnon {version}";
        _vueModele.VersionApplication = version;
    }
}
