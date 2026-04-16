using System.Windows.Media;

/*
 * Représente un badge de succès affiché dans le groupe contextuel
 * rattaché au succès actuellement mis en avant.
 */
namespace RA.Compagnon.ViewModels;

/*
 * Transporte les informations minimales nécessaires pour afficher un badge,
 * son état visuel et son infobulle dans le groupe du succès courant.
 */
public sealed class CurrentAchievementGroupBadgeViewModel : ViewModelBase
{
    private ImageSource? _image;
    private bool _imageVisible;
    private double _imageOpacity = 1;
    private string _toolTip = string.Empty;

    public int IdentifiantSucces { get; init; }

    public string Titre { get; init; } = string.Empty;

    public string ToolTip
    {
        get => _toolTip;
        set => SetProperty(ref _toolTip, value);
    }

    public string TexteSecours { get; init; } = string.Empty;

    public bool EstHardcore { get; init; }

    public ImageSource? Image
    {
        get => _image;
        set => SetProperty(ref _image, value);
    }

    public bool ImageVisible
    {
        get => _imageVisible;
        set => SetProperty(ref _imageVisible, value);
    }

    public double ImageOpacity
    {
        get => _imageOpacity;
        set => SetProperty(ref _imageOpacity, value);
    }
}
