using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using RA.Compagnon.Modeles.Presentation;

/*
 * Regroupe les propriétés et commandes exposées à la fenêtre principale
 * pour piloter l'ensemble de l'interface utilisateur.
 */
namespace RA.Compagnon.ViewModels;

/*
 * Porte l'état bindé de la fenêtre principale, des sous-cartes visibles
 * et des commandes globales de l'application.
 */
public sealed class MainWindowViewModel : ViewModelBase
{
    private string _titreFenetre = "RA-Compagnon";
    private string _etatConnexion = "Non configuré";
    private string _versionApplication = string.Empty;
    private string _titreCarteJeuEnCours = "Dernier jeu joué";
    private string _libelleMiseAJourApplication = "Mise à jour";
    private string _toolTipMiseAJourApplication = string.Empty;
    private string _libelleRechargerJeu = "Recharger";
    private string _toolTipRechargerJeu = string.Empty;
    private string _libelleOrdreSuccesGrille = "Normal";
    private string _etatSynchronisationJeu = string.Empty;
    private string _titreModuleActif = "Accueil";
    private string _messageBibliotheque = "Aucun jeu récent disponible pour le moment.";
    private bool _ordreSuccesNormalActif = true;
    private bool _ordreSuccesAleatoireActif;
    private bool _ordreSuccesFacileActif;
    private bool _ordreSuccesDifficileActif;
    private bool _moduleAccueilActif = true;
    private bool _moduleBibliothequeActif;
    private Brush? _contourOrdreSuccesNormal;
    private Brush? _contourOrdreSuccesAleatoire;
    private Brush? _contourOrdreSuccesFacile;
    private Brush? _contourOrdreSuccesDifficile;
    private Visibility _visibiliteContenuPrincipal = Visibility.Hidden;
    private Visibility _visibiliteBarreModules = Visibility.Collapsed;
    private Visibility _visibiliteCarteConnexion = Visibility.Collapsed;
    private Visibility _visibiliteCarteJeuEnCours = Visibility.Visible;
    private Visibility _visibiliteMiseAJourApplication = Visibility.Collapsed;
    private Visibility _visibiliteSynchronisationJeu = Visibility.Collapsed;
    private Visibility _visibiliteModuleAccueil = Visibility.Visible;
    private Visibility _visibiliteModuleBibliotheque = Visibility.Collapsed;
    private Visibility _visibiliteBibliothequeVide = Visibility.Visible;
    private Visibility _visibiliteBibliothequeListe = Visibility.Collapsed;
    private bool _miseAJourApplicationActivee;
    private bool _rechargerJeuActif;
    private Action? _executerActionAfficherCompte;
    private Action? _executerActionAfficherAide;
    private Action? _executerActionMiseAJourApplication;
    private Action? _executerActionRechargerJeu;
    private Action? _executerActionAfficherModuleAccueil;
    private Action? _executerActionAfficherModuleBibliotheque;
    private Action? _executerActionOrdreSuccesNormal;
    private Action? _executerActionOrdreSuccesAleatoire;
    private Action? _executerActionOrdreSuccesFacile;
    private Action? _executerActionOrdreSuccesDifficile;

    /*
     * Initialise les sous-ViewModels et les commandes exposées à la fenêtre.
     */
    public MainWindowViewModel()
    {
        JeuCourant = new CurrentGameViewModel();
        SuccesEnCours = new CurrentAchievementViewModel();
        Compte = new AccountSummaryViewModel();
        JeuxBibliotheque = [];
        CommandeAfficherCompte = new RelayCommand(() => _executerActionAfficherCompte?.Invoke());
        CommandeAfficherAide = new RelayCommand(() => _executerActionAfficherAide?.Invoke());
        CommandeMiseAJourApplication = new RelayCommand(
            () => _executerActionMiseAJourApplication?.Invoke(),
            () => MiseAJourApplicationActivee
        );
        CommandeRechargerJeu = new RelayCommand(
            () => _executerActionRechargerJeu?.Invoke(),
            () => RechargerJeuActif
        );
        CommandeAfficherModuleAccueil = new RelayCommand(() =>
            _executerActionAfficherModuleAccueil?.Invoke()
        );
        CommandeAfficherModuleBibliotheque = new RelayCommand(() =>
            _executerActionAfficherModuleBibliotheque?.Invoke()
        );
        CommandeOrdreSuccesNormal = new RelayCommand(() =>
            _executerActionOrdreSuccesNormal?.Invoke()
        );
        CommandeOrdreSuccesAleatoire = new RelayCommand(() =>
            _executerActionOrdreSuccesAleatoire?.Invoke()
        );
        CommandeOrdreSuccesFacile = new RelayCommand(() =>
            _executerActionOrdreSuccesFacile?.Invoke()
        );
        CommandeOrdreSuccesDifficile = new RelayCommand(() =>
            _executerActionOrdreSuccesDifficile?.Invoke()
        );
    }

    public CurrentGameViewModel JeuCourant { get; }

    public CurrentAchievementViewModel SuccesEnCours { get; }

    public AccountSummaryViewModel Compte { get; }

    public ObservableCollection<BibliothequeJeuViewModel> JeuxBibliotheque { get; }

    public RelayCommand CommandeAfficherCompte { get; }

    public RelayCommand CommandeAfficherAide { get; }

    public RelayCommand CommandeMiseAJourApplication { get; }

    public RelayCommand CommandeRechargerJeu { get; }

    public RelayCommand CommandeAfficherModuleAccueil { get; }

    public RelayCommand CommandeAfficherModuleBibliotheque { get; }

    public RelayCommand CommandeOrdreSuccesNormal { get; }

    public RelayCommand CommandeOrdreSuccesAleatoire { get; }

    public RelayCommand CommandeOrdreSuccesFacile { get; }

    public RelayCommand CommandeOrdreSuccesDifficile { get; }

    public string TitreFenetre
    {
        get => _titreFenetre;
        set => SetProperty(ref _titreFenetre, value);
    }

    public string EtatConnexion
    {
        get => _etatConnexion;
        set => SetProperty(ref _etatConnexion, value);
    }

    public string VersionApplication
    {
        get => _versionApplication;
        set => SetProperty(ref _versionApplication, value);
    }

    public string TitreCarteJeuEnCours
    {
        get => _titreCarteJeuEnCours;
        set => SetProperty(ref _titreCarteJeuEnCours, value);
    }

    public string LibelleMiseAJourApplication
    {
        get => _libelleMiseAJourApplication;
        set => SetProperty(ref _libelleMiseAJourApplication, value);
    }

    public string ToolTipMiseAJourApplication
    {
        get => _toolTipMiseAJourApplication;
        set => SetProperty(ref _toolTipMiseAJourApplication, value);
    }

    public string LibelleRechargerJeu
    {
        get => _libelleRechargerJeu;
        set => SetProperty(ref _libelleRechargerJeu, value);
    }

    public string ToolTipRechargerJeu
    {
        get => _toolTipRechargerJeu;
        set => SetProperty(ref _toolTipRechargerJeu, value);
    }

    public string LibelleOrdreSuccesGrille
    {
        get => _libelleOrdreSuccesGrille;
        set => SetProperty(ref _libelleOrdreSuccesGrille, value);
    }

    public string EtatSynchronisationJeu
    {
        get => _etatSynchronisationJeu;
        set => SetProperty(ref _etatSynchronisationJeu, value);
    }

    public string TitreModuleActif
    {
        get => _titreModuleActif;
        set => SetProperty(ref _titreModuleActif, value);
    }

    public string MessageBibliotheque
    {
        get => _messageBibliotheque;
        set => SetProperty(ref _messageBibliotheque, value);
    }

    public bool OrdreSuccesNormalActif
    {
        get => _ordreSuccesNormalActif;
        set => SetProperty(ref _ordreSuccesNormalActif, value);
    }

    public bool OrdreSuccesAleatoireActif
    {
        get => _ordreSuccesAleatoireActif;
        set => SetProperty(ref _ordreSuccesAleatoireActif, value);
    }

    public bool OrdreSuccesFacileActif
    {
        get => _ordreSuccesFacileActif;
        set => SetProperty(ref _ordreSuccesFacileActif, value);
    }

    public bool OrdreSuccesDifficileActif
    {
        get => _ordreSuccesDifficileActif;
        set => SetProperty(ref _ordreSuccesDifficileActif, value);
    }

    public bool ModuleAccueilActif
    {
        get => _moduleAccueilActif;
        set => SetProperty(ref _moduleAccueilActif, value);
    }

    public bool ModuleBibliothequeActif
    {
        get => _moduleBibliothequeActif;
        set => SetProperty(ref _moduleBibliothequeActif, value);
    }

    public Brush? ContourOrdreSuccesNormal
    {
        get => _contourOrdreSuccesNormal;
        set => SetProperty(ref _contourOrdreSuccesNormal, value);
    }

    public Brush? ContourOrdreSuccesAleatoire
    {
        get => _contourOrdreSuccesAleatoire;
        set => SetProperty(ref _contourOrdreSuccesAleatoire, value);
    }

    public Brush? ContourOrdreSuccesFacile
    {
        get => _contourOrdreSuccesFacile;
        set => SetProperty(ref _contourOrdreSuccesFacile, value);
    }

    public Brush? ContourOrdreSuccesDifficile
    {
        get => _contourOrdreSuccesDifficile;
        set => SetProperty(ref _contourOrdreSuccesDifficile, value);
    }

    public Visibility VisibiliteContenuPrincipal
    {
        get => _visibiliteContenuPrincipal;
        set => SetProperty(ref _visibiliteContenuPrincipal, value);
    }

    public Visibility VisibiliteBarreModules
    {
        get => _visibiliteBarreModules;
        set => SetProperty(ref _visibiliteBarreModules, value);
    }

    public Visibility VisibiliteCarteConnexion
    {
        get => _visibiliteCarteConnexion;
        set => SetProperty(ref _visibiliteCarteConnexion, value);
    }

    public Visibility VisibiliteCarteJeuEnCours
    {
        get => _visibiliteCarteJeuEnCours;
        set => SetProperty(ref _visibiliteCarteJeuEnCours, value);
    }

    public Visibility VisibiliteMiseAJourApplication
    {
        get => _visibiliteMiseAJourApplication;
        set => SetProperty(ref _visibiliteMiseAJourApplication, value);
    }

    public Visibility VisibiliteSynchronisationJeu
    {
        get => _visibiliteSynchronisationJeu;
        set => SetProperty(ref _visibiliteSynchronisationJeu, value);
    }

    public Visibility VisibiliteModuleAccueil
    {
        get => _visibiliteModuleAccueil;
        set => SetProperty(ref _visibiliteModuleAccueil, value);
    }

    public Visibility VisibiliteModuleBibliotheque
    {
        get => _visibiliteModuleBibliotheque;
        set => SetProperty(ref _visibiliteModuleBibliotheque, value);
    }

    public Visibility VisibiliteBibliothequeVide
    {
        get => _visibiliteBibliothequeVide;
        set => SetProperty(ref _visibiliteBibliothequeVide, value);
    }

    public Visibility VisibiliteBibliothequeListe
    {
        get => _visibiliteBibliothequeListe;
        set => SetProperty(ref _visibiliteBibliothequeListe, value);
    }

    public bool MiseAJourApplicationActivee
    {
        get => _miseAJourApplicationActivee;
        set
        {
            if (SetProperty(ref _miseAJourApplicationActivee, value))
            {
                CommandeMiseAJourApplication.NotifierPeutExecuterChange();
            }
        }
    }

    public bool RechargerJeuActif
    {
        get => _rechargerJeuActif;
        set
        {
            if (SetProperty(ref _rechargerJeuActif, value))
            {
                CommandeRechargerJeu.NotifierPeutExecuterChange();
            }
        }
    }

    /*
     * Associe l'action à exécuter pour l'ouverture du compte.
     */
    public void ConfigurerActionAfficherCompte(Action? action)
    {
        _executerActionAfficherCompte = action;
        CommandeAfficherCompte.NotifierPeutExecuterChange();
    }

    /*
     * Associe l'action à exécuter pour l'ouverture de l'aide.
     */
    public void ConfigurerActionAfficherAide(Action? action)
    {
        _executerActionAfficherAide = action;
        CommandeAfficherAide.NotifierPeutExecuterChange();
    }

    /*
     * Associe l'action à exécuter pour la mise à jour de l'application.
     */
    public void ConfigurerActionMiseAJourApplication(Action? action)
    {
        _executerActionMiseAJourApplication = action;
        CommandeMiseAJourApplication.NotifierPeutExecuterChange();
    }

    /*
     * Associe l'action à exécuter pour le rechargement du jeu courant.
     */
    public void ConfigurerActionRechargerJeu(Action? action)
    {
        _executerActionRechargerJeu = action;
        CommandeRechargerJeu.NotifierPeutExecuterChange();
    }

    /*
     * Associe l'action à exécuter pour l'affichage du module d'accueil.
     */
    public void ConfigurerActionAfficherModuleAccueil(Action? action)
    {
        _executerActionAfficherModuleAccueil = action;
        CommandeAfficherModuleAccueil.NotifierPeutExecuterChange();
    }

    /*
     * Associe l'action à exécuter pour l'affichage du module bibliothèque.
     */
    public void ConfigurerActionAfficherModuleBibliotheque(Action? action)
    {
        _executerActionAfficherModuleBibliotheque = action;
        CommandeAfficherModuleBibliotheque.NotifierPeutExecuterChange();
    }

    /*
     * Associe l'action à exécuter pour l'affichage du module diagnostic.
     */

    /*
     * Associe l'action à exécuter pour l'affichage du module paramètres.
     */

    /*
     * Associe l'action à exécuter pour le mode d'ordre normal des succès.
     */
    public void ConfigurerActionOrdreSuccesNormal(Action? action)
    {
        _executerActionOrdreSuccesNormal = action;
        CommandeOrdreSuccesNormal.NotifierPeutExecuterChange();
    }

    /*
     * Associe l'action à exécuter pour le mode d'ordre aléatoire des succès.
     */
    public void ConfigurerActionOrdreSuccesAleatoire(Action? action)
    {
        _executerActionOrdreSuccesAleatoire = action;
        CommandeOrdreSuccesAleatoire.NotifierPeutExecuterChange();
    }

    /*
     * Associe l'action à exécuter pour le mode d'ordre facile des succès.
     */
    public void ConfigurerActionOrdreSuccesFacile(Action? action)
    {
        _executerActionOrdreSuccesFacile = action;
        CommandeOrdreSuccesFacile.NotifierPeutExecuterChange();
    }

    /*
     * Associe l'action à exécuter pour le mode d'ordre difficile des succès.
     */
    public void ConfigurerActionOrdreSuccesDifficile(Action? action)
    {
        _executerActionOrdreSuccesDifficile = action;
        CommandeOrdreSuccesDifficile.NotifierPeutExecuterChange();
    }

    /*
     * Remplace les jeux visibles dans la bibliothèque et ajuste l'état de la
     * liste ou du message vide.
     */
    public void RemplacerJeuxBibliotheque(
        IEnumerable<BibliothequeJeuViewModel> jeux,
        string messageVide
    )
    {
        JeuxBibliotheque.Clear();

        foreach (BibliothequeJeuViewModel jeu in jeux)
        {
            JeuxBibliotheque.Add(jeu);
        }

        MessageBibliotheque = messageVide;
        VisibiliteBibliothequeListe =
            JeuxBibliotheque.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        VisibiliteBibliothequeVide =
            JeuxBibliotheque.Count > 0 ? Visibility.Collapsed : Visibility.Visible;
    }
}
