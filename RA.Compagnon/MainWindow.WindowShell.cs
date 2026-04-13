using System.Reflection;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace RA.Compagnon;

public partial class MainWindow
{
    private void InitialiserHabillageFenetre()
    {
        AppliquerIconeApplication();
        AppliquerVersionApplication();
    }

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
