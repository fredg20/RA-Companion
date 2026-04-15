using System.Windows.Media;

/*
 * Porte l'état visuel et les commandes de la carte du succès actuellement
 * mis en avant dans la fenêtre principale.
 */
namespace RA.Compagnon.ViewModels;

/*
 * Expose les informations, détails et actions de navigation autour
 * du succès courant affiché à l'utilisateur.
 */
public sealed class CurrentAchievementViewModel : ViewModelBase
{
    private ImageSource? _image;
    private bool _imageVisible;
    private double _imageOpacity = 0.58;
    private string _texteVisuel = string.Empty;
    private bool _texteVisuelVisible;
    private string _titre = string.Empty;
    private bool _titreVisible;
    private string _description = string.Empty;
    private bool _descriptionVisible;
    private string _detailsPoints = string.Empty;
    private bool _detailsPointsVisible;
    private string _detailsFaisabilite = string.Empty;
    private bool _detailsFaisabiliteVisible;
    private string _toolTipDetailsFaisabilite = string.Empty;
    private bool _navigationVisible;
    private bool _precedentActif;
    private bool _suivantActif;
    private double _precedentOpacity = 1;
    private double _suivantOpacity = 1;
    private bool _passerActif;
    private Action? _executerNavigationPrecedente;
    private Action? _executerNavigationSuivante;
    private Action? _executerPassage;

    /*
     * Initialise les commandes de navigation et de report du succès courant.
     */
    public CurrentAchievementViewModel()
    {
        CommandePrecedent = new RelayCommand(
            () => _executerNavigationPrecedente?.Invoke(),
            () => PrecedentActif
        );
        CommandeSuivant = new RelayCommand(
            () => _executerNavigationSuivante?.Invoke(),
            () => SuivantActif
        );
        CommandePasser = new RelayCommand(() => _executerPassage?.Invoke(), () => PasserActif);
    }

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

    public string TexteVisuel
    {
        get => _texteVisuel;
        set => SetProperty(ref _texteVisuel, value);
    }

    public bool TexteVisuelVisible
    {
        get => _texteVisuelVisible;
        set => SetProperty(ref _texteVisuelVisible, value);
    }

    public string Titre
    {
        get => _titre;
        set => SetProperty(ref _titre, value);
    }

    public bool TitreVisible
    {
        get => _titreVisible;
        set => SetProperty(ref _titreVisible, value);
    }

    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    public bool DescriptionVisible
    {
        get => _descriptionVisible;
        set => SetProperty(ref _descriptionVisible, value);
    }

    public string DetailsPoints
    {
        get => _detailsPoints;
        set => SetProperty(ref _detailsPoints, value);
    }

    public bool DetailsPointsVisible
    {
        get => _detailsPointsVisible;
        set => SetProperty(ref _detailsPointsVisible, value);
    }

    public string DetailsFaisabilite
    {
        get => _detailsFaisabilite;
        set => SetProperty(ref _detailsFaisabilite, value);
    }

    public bool DetailsFaisabiliteVisible
    {
        get => _detailsFaisabiliteVisible;
        set => SetProperty(ref _detailsFaisabiliteVisible, value);
    }

    public string ToolTipDetailsFaisabilite
    {
        get => _toolTipDetailsFaisabilite;
        set => SetProperty(ref _toolTipDetailsFaisabilite, value);
    }

    public bool NavigationVisible
    {
        get => _navigationVisible;
        set => SetProperty(ref _navigationVisible, value);
    }

    public bool PrecedentActif
    {
        get => _precedentActif;
        set
        {
            if (SetProperty(ref _precedentActif, value))
            {
                CommandePrecedent.NotifierPeutExecuterChange();
            }
        }
    }

    public bool SuivantActif
    {
        get => _suivantActif;
        set
        {
            if (SetProperty(ref _suivantActif, value))
            {
                CommandeSuivant.NotifierPeutExecuterChange();
            }
        }
    }

    public double PrecedentOpacity
    {
        get => _precedentOpacity;
        set => SetProperty(ref _precedentOpacity, value);
    }

    public double SuivantOpacity
    {
        get => _suivantOpacity;
        set => SetProperty(ref _suivantOpacity, value);
    }

    public bool PasserActif
    {
        get => _passerActif;
        set
        {
            if (SetProperty(ref _passerActif, value))
            {
                CommandePasser.NotifierPeutExecuterChange();
            }
        }
    }

    public RelayCommand CommandePrecedent { get; }

    public RelayCommand CommandeSuivant { get; }

    public RelayCommand CommandePasser { get; }

    /*
     * Associe l'action à exécuter pour naviguer vers le succès précédent.
     */
    public void ConfigurerNavigationPrecedente(Action? action)
    {
        _executerNavigationPrecedente = action;
        CommandePrecedent.NotifierPeutExecuterChange();
    }

    /*
     * Associe l'action à exécuter pour naviguer vers le succès suivant.
     */
    public void ConfigurerNavigationSuivante(Action? action)
    {
        _executerNavigationSuivante = action;
        CommandeSuivant.NotifierPeutExecuterChange();
    }

    /*
     * Associe l'action à exécuter pour reporter le succès courant.
     */
    public void ConfigurerActionPasser(Action? action)
    {
        _executerPassage = action;
        CommandePasser.NotifierPeutExecuterChange();
    }
}