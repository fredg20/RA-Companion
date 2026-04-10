using System.Windows.Input;
using System.Windows.Media;

namespace RA.Compagnon.ViewModels;

public sealed class CurrentGameViewModel : ViewModelBase
{
    private string _titre = string.Empty;
    private string _details = string.Empty;
    private string _etat = string.Empty;
    private string _progression = string.Empty;
    private string _pourcentage = string.Empty;
    private string _tempsDeJeu = string.Empty;
    private string _console = string.Empty;
    private string _genre = string.Empty;
    private string _dateSortie = string.Empty;
    private string _credits = string.Empty;
    private ImageSource? _imageConsole;
    private string _libelleActionRejouer = "Rejouer";
    private string _toolTipActionRejouer = string.Empty;
    private string _libelleActionDetails = "Détails";
    private string _toolTipActionDetails = string.Empty;
    private bool _actionRejouerVisible;
    private bool _actionRejouerActivee;
    private bool _actionDetailsVisible;
    private bool _actionDetailsActivee;
    private bool _informationsVisibles;
    private bool _consoleVisible;
    private bool _imageConsoleVisible;
    private bool _genreVisible;
    private bool _dateSortieVisible;
    private bool _creditsVisible;
    private bool _tempsDeJeuVisible;
    private bool _detailsVisible;
    private bool _visuelsSecondairesVisible;
    private string _libelleVisuelCourant = string.Empty;
    private string _texteVisuelPrincipal = string.Empty;
    private bool _actionVisuelPrecedentActivee;
    private bool _actionVisuelSuivantActivee;
    private bool _texteVisuelPrincipalVisible;
    private double _progressionValeur;
    private Action? _executerActionRejouer;
    private Action? _executerActionDetails;
    private Action? _executerActionVisuelPrecedent;
    private Action? _executerActionVisuelSuivant;

    public CurrentGameViewModel()
    {
        CommandeActionRejouer = new RelayCommand(
            () => _executerActionRejouer?.Invoke(),
            () => ActionRejouerActivee
        );
        CommandeActionDetails = new RelayCommand(
            () => _executerActionDetails?.Invoke(),
            () => ActionDetailsActivee
        );
        CommandeActionVisuelPrecedent = new RelayCommand(
            () => _executerActionVisuelPrecedent?.Invoke(),
            () => ActionVisuelPrecedentActivee
        );
        CommandeActionVisuelSuivant = new RelayCommand(
            () => _executerActionVisuelSuivant?.Invoke(),
            () => ActionVisuelSuivantActivee
        );
    }

    public string Titre
    {
        get => _titre;
        set => SetProperty(ref _titre, value);
    }

    public string Details
    {
        get => _details;
        set => SetProperty(ref _details, value);
    }

    public string Etat
    {
        get => _etat;
        set => SetProperty(ref _etat, value);
    }

    public string Progression
    {
        get => _progression;
        set => SetProperty(ref _progression, value);
    }

    public string Pourcentage
    {
        get => _pourcentage;
        set => SetProperty(ref _pourcentage, value);
    }

    public string TempsDeJeu
    {
        get => _tempsDeJeu;
        set => SetProperty(ref _tempsDeJeu, value);
    }

    public string Console
    {
        get => _console;
        set => SetProperty(ref _console, value);
    }

    public string Genre
    {
        get => _genre;
        set => SetProperty(ref _genre, value);
    }

    public string DateSortie
    {
        get => _dateSortie;
        set => SetProperty(ref _dateSortie, value);
    }

    public string Credits
    {
        get => _credits;
        set => SetProperty(ref _credits, value);
    }

    public string LibelleActionRejouer
    {
        get => _libelleActionRejouer;
        set => SetProperty(ref _libelleActionRejouer, value);
    }

    public bool ActionRejouerVisible
    {
        get => _actionRejouerVisible;
        set => SetProperty(ref _actionRejouerVisible, value);
    }

    public string ToolTipActionRejouer
    {
        get => _toolTipActionRejouer;
        set => SetProperty(ref _toolTipActionRejouer, value);
    }

    public bool ActionRejouerActivee
    {
        get => _actionRejouerActivee;
        set
        {
            if (SetProperty(ref _actionRejouerActivee, value))
            {
                CommandeActionRejouer.NotifierPeutExecuterChange();
            }
        }
    }

    public string LibelleActionDetails
    {
        get => _libelleActionDetails;
        set => SetProperty(ref _libelleActionDetails, value);
    }

    public string ToolTipActionDetails
    {
        get => _toolTipActionDetails;
        set => SetProperty(ref _toolTipActionDetails, value);
    }

    public bool ActionDetailsVisible
    {
        get => _actionDetailsVisible;
        set => SetProperty(ref _actionDetailsVisible, value);
    }

    public bool ActionDetailsActivee
    {
        get => _actionDetailsActivee;
        set
        {
            if (SetProperty(ref _actionDetailsActivee, value))
            {
                CommandeActionDetails.NotifierPeutExecuterChange();
            }
        }
    }

    public bool InformationsVisibles
    {
        get => _informationsVisibles;
        set => SetProperty(ref _informationsVisibles, value);
    }

    public bool ConsoleVisible
    {
        get => _consoleVisible;
        set => SetProperty(ref _consoleVisible, value);
    }

    public ImageSource? ImageConsole
    {
        get => _imageConsole;
        set => SetProperty(ref _imageConsole, value);
    }

    public bool ImageConsoleVisible
    {
        get => _imageConsoleVisible;
        set => SetProperty(ref _imageConsoleVisible, value);
    }

    public bool GenreVisible
    {
        get => _genreVisible;
        set => SetProperty(ref _genreVisible, value);
    }

    public bool DateSortieVisible
    {
        get => _dateSortieVisible;
        set => SetProperty(ref _dateSortieVisible, value);
    }

    public bool CreditsVisible
    {
        get => _creditsVisible;
        set => SetProperty(ref _creditsVisible, value);
    }

    public bool TempsDeJeuVisible
    {
        get => _tempsDeJeuVisible;
        set => SetProperty(ref _tempsDeJeuVisible, value);
    }

    public bool DetailsVisible
    {
        get => _detailsVisible;
        set => SetProperty(ref _detailsVisible, value);
    }

    public bool VisuelsSecondairesVisible
    {
        get => _visuelsSecondairesVisible;
        set => SetProperty(ref _visuelsSecondairesVisible, value);
    }

    public string LibelleVisuelCourant
    {
        get => _libelleVisuelCourant;
        set => SetProperty(ref _libelleVisuelCourant, value);
    }

    public string TexteVisuelPrincipal
    {
        get => _texteVisuelPrincipal;
        set => SetProperty(ref _texteVisuelPrincipal, value);
    }

    public bool TexteVisuelPrincipalVisible
    {
        get => _texteVisuelPrincipalVisible;
        set => SetProperty(ref _texteVisuelPrincipalVisible, value);
    }

    public bool ActionVisuelPrecedentActivee
    {
        get => _actionVisuelPrecedentActivee;
        set
        {
            if (SetProperty(ref _actionVisuelPrecedentActivee, value))
            {
                CommandeActionVisuelPrecedent.NotifierPeutExecuterChange();
            }
        }
    }

    public bool ActionVisuelSuivantActivee
    {
        get => _actionVisuelSuivantActivee;
        set
        {
            if (SetProperty(ref _actionVisuelSuivantActivee, value))
            {
                CommandeActionVisuelSuivant.NotifierPeutExecuterChange();
            }
        }
    }

    public RelayCommand CommandeActionRejouer { get; }

    public RelayCommand CommandeActionDetails { get; }

    public RelayCommand CommandeActionVisuelPrecedent { get; }

    public RelayCommand CommandeActionVisuelSuivant { get; }

    public void ConfigurerActionRejouer(Action? action)
    {
        _executerActionRejouer = action;
        CommandeActionRejouer.NotifierPeutExecuterChange();
    }

    public void ConfigurerActionDetails(Action? action)
    {
        _executerActionDetails = action;
        CommandeActionDetails.NotifierPeutExecuterChange();
    }

    public void ConfigurerActionVisuelPrecedent(Action? action)
    {
        _executerActionVisuelPrecedent = action;
        CommandeActionVisuelPrecedent.NotifierPeutExecuterChange();
    }

    public void ConfigurerActionVisuelSuivant(Action? action)
    {
        _executerActionVisuelSuivant = action;
        CommandeActionVisuelSuivant.NotifierPeutExecuterChange();
    }

    public double ProgressionValeur
    {
        get => _progressionValeur;
        set => SetProperty(ref _progressionValeur, value);
    }
}
