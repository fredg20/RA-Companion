using System.Reflection;
using System.Windows.Media;
using System.Windows.Media.Imaging;

/*
 * Regroupe l'initialisation de l'habillage de la fenêtre, notamment son
 * icône et sa version visible.
 */
namespace RA.Compagnon;

/*
 * Porte les helpers de configuration de l'enveloppe visuelle de la fenêtre.
 */
public partial class MainWindow
{
    /*
     * Initialise les éléments d'habillage visibles de la fenêtre principale.
     */
    private void InitialiserHabillageFenetre()
    {
        AppliquerIconeApplication();
        AppliquerVersionApplication();
    }

    /*
     * Applique l'icône de l'application à la fenêtre et à son en-tête.
     */
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

    /*
     * Applique la version de l'application dans l'interface et le ViewModel.
     */
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