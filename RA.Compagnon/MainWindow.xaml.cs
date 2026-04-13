using System.Net.Http;
using Wpf.Ui.Controls;

namespace RA.Compagnon;

/// <summary>
/// Fenêtre principale du compagnon RetroAchievements.
/// </summary>
public partial class MainWindow : FluentWindow
{
    private sealed record VisuelJeuEnCours(string Libelle, string CheminImage);

    private sealed record BadgeSuccesGrilleContexte(
        int IdentifiantJeu,
        int IdentifiantSucces,
        string UrlBadge,
        bool EstHardcore = false
    )
    {
        public int Id => IdentifiantSucces;
    }

    private enum OrdreSuccesGrille
    {
        Normal,
        Aleatoire,
        Facile,
        Difficile,
    }

    private static readonly TimeSpan IntervalleActualisationApi = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan IntervalleActualisationRichPresence = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan IntervalleSondeLocaleEmulateurs = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan IntervallePresenceLocaleCompte = TimeSpan.FromMilliseconds(
        250
    );
    private static readonly TimeSpan IntervalleMasquageBarreDefilement = TimeSpan.FromSeconds(1.2);
    private static readonly TimeSpan IntervalleRelayoutApresRedimensionnement =
        TimeSpan.FromMilliseconds(90);
    private static readonly TimeSpan IntervalleRepriseAnimationGrilleSucces = TimeSpan.FromSeconds(
        1.3
    );
    private static readonly TimeSpan DureeAffichageTemporaireSuccesGrille = TimeSpan.FromSeconds(
        10
    );
    private static readonly TimeSpan IntervalleRotationVisuelsJeuEnCours = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan IntervalleSauvegardeGeometrieFenetre =
        TimeSpan.FromMilliseconds(350);
    private static readonly TimeSpan IntervalleDebounceSynchronisationEtatJeu =
        TimeSpan.FromSeconds(2);
    private static readonly TimeSpan IntervalleRafraichissementMiseAJourApplication =
        TimeSpan.FromHours(6);
    private static readonly HttpClient HttpClientImages = new();

    private const double LargeurMinimaleDispositionDouble = 920;
    private const double RatioLargeurDispositionEtendue = 0.60;
    private const double LargeurMinimaleDispositionTriple = 1680;
    private const double LargeurMinimaleCarteJeuDispositionEtendue = 940;
    private const double LargeurContenuModaleConnexion = 360;
    private const double MargeInterieureModaleConnexion = 16;
    private const double LargeurZoneDetectionBarreDefilement = 18;
    private const double TaillePoliceInterfaceBase = 14.5;
    private const double LargeurMinimaleTypographieResponsive = 360;
    private const double LargeurReferenceTypographieResponsive = 1100;
    private const double LargeurMaximaleTypographieResponsive = 1800;
    private const double FacteurTypographieResponsiveMinimal = 0.88;
    private const double FacteurTypographieResponsiveMaximal = 1.18;
    private const double TaillePoliceTitreJeuNormale = 26;
    private const double TaillePoliceTitreJeuMinimale = 18;
    private const double TailleBadgeGrilleSucces = 34;
    private const double EspaceMinimalGrilleSucces = 6;
    private const double HauteurMinimaleGrilleSucces = 0;
    private const double VitesseDefilementGrilleSuccesPixelsParSeconde = 22;
    private const double SeuilDeclenchementDefilementGrilleSucces = 4;
    private const double DureeFonduImageJeuEnCoursMillisecondes = 1000;
    private const double RayonFlouTransitionImageJeuEnCours = 14;

    /// <summary>
    /// Initialise la fenêtre principale.
    /// </summary>
    public MainWindow()
    {
        App.JournaliserDemarrage("MainWindow ctor début");
        InitializeComponent();
        App.JournaliserDemarrage("MainWindow ctor fin");
        InitialiserVueModele();
        RA.Compagnon.Services.ServiceSurveillanceSuccesLocaux.ReinitialiserJournalSession();
        ReinitialiserJournalDiagnosticListeSucces();
        MettreAJourLibelleOrdreSuccesGrilleEtModes();
        InitialiserHabillageFenetre();
        _etatMiseAJourApplication =
            RA.Compagnon.Services.ServiceMiseAJourApplication.CreerEtatInitial();
        ReinitialiserJeuEnCours();
        ConfigurerActualisationAutomatique();
        _serviceSurveillanceSuccesLocaux.SignalRecu += SurveillanceSuccesLocaux_SignalRecu;
#if DEBUG
        PreviewKeyDown += FenetrePrincipale_PreviewKeyDown_Debug;
#endif
        Loaded += FenetrePrincipaleChargee;
        Closing += FenetrePrincipale_Fermeture;
        LocationChanged += FenetrePrincipale_PositionChangee;
        StateChanged += FenetrePrincipale_EtatChange;
    }
}
