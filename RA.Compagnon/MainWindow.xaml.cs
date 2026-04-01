using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using RA.Compagnon.Modeles.Api.V2.Game;
using RA.Compagnon.Modeles.Api.V2.User;
using RA.Compagnon.Modeles.Etat;
using RA.Compagnon.Modeles.Local;
using RA.Compagnon.Modeles.Presentation;
using RA.Compagnon.Services;
using SystemControls = System.Windows.Controls;
using UiControls = Wpf.Ui.Controls;

namespace RA.Compagnon;

/// <summary>
/// Fenêtre principale du compagnon RetroAchievements.
/// </summary>
public partial class MainWindow : UiControls.FluentWindow
{
    private sealed record VisuelJeuEnCours(string Libelle, string CheminImage);

    private sealed record BadgeSuccesGrilleContexte(
        int IdentifiantJeu,
        int IdentifiantSucces,
        string UrlBadge
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
    private static readonly HttpClient HttpClientImages = new();
    private const double LargeurMinimaleDispositionDouble = 920;
    private const double LargeurContenuModaleConnexion = 360;
    private const double MargeInterieureModaleConnexion = 16;
    private const double LargeurZoneDetectionBarreDefilement = 18;
    private const double TaillePoliceTitreJeuNormale = 26;
    private const double TaillePoliceTitreJeuMinimale = 18;
    private const double TailleBadgeGrilleSucces = 34;
    private const double EspaceMinimalGrilleSucces = 6;
    private const double HauteurMinimaleGrilleSucces = 0;
    private const double VitesseDefilementGrilleSuccesPixelsParSeconde = 22;
    private const double SeuilDeclenchementDefilementGrilleSucces = 4;
    private const double DureeFonduImageJeuEnCoursMillisecondes = 1000;
    private const double RayonFlouTransitionImageJeuEnCours = 14;
    private static readonly TimeSpan IntervalleRotationVisuelsJeuEnCours = TimeSpan.FromSeconds(8);

    private readonly ServiceConfigurationLocale _serviceConfigurationLocale = new();
    private readonly ServiceTraductionTexte _serviceTraductionTexte = new();
    private readonly ServiceUtilisateurRetroAchievements _serviceUtilisateurRetroAchievements =
        new();
    private readonly ServiceJeuRetroAchievements _serviceJeuRetroAchievements = new();
    private readonly ServiceActiviteRetroAchievements _serviceActiviteRetroAchievements = new();
    private readonly ServiceCommunauteRetroAchievements _serviceCommunauteRetroAchievements = new();
    private readonly ServiceCatalogueRetroAchievements _serviceCatalogueRetroAchievements = new();
    private readonly ServiceCatalogueJeuxLocal _serviceCatalogueJeuxLocal = new();
    private readonly ServiceEtatUtilisateurJeuxLocal _serviceEtatUtilisateurJeuxLocal = new();
    private readonly ServiceSondeLocaleEmulateurs _serviceSondeLocaleEmulateurs = new();
    private readonly ServiceSurveillanceSuccesLocaux _serviceSurveillanceSuccesLocaux = new();
    private readonly ServiceDetectionSuccesJeu _serviceDetectionSuccesJeu = new();
    private readonly ServiceDetectionSuccesUtilisateurLocal _serviceDetectionSuccesUtilisateurLocal =
        new();
    private readonly ServiceOrchestrateurEtatJeu _serviceOrchestrateurEtatJeu = new();
#if DEBUG
    private readonly ServiceTestSuccesDebug _serviceTestSuccesDebug = new();
#endif
    private readonly DispatcherTimer _minuteurActualisationApi = new(DispatcherPriority.Background);
    private readonly DispatcherTimer _minuteurActualisationRichPresence = new(
        DispatcherPriority.Background
    );
    private readonly DispatcherTimer _minuteurPresenceLocaleCompte = new(
        DispatcherPriority.Background
    );
    private readonly DispatcherTimer _minuteurSondeLocaleEmulateurs = new(
        DispatcherPriority.Background
    );
    private readonly DispatcherTimer _minuteurMasquageBarreDefilement = new();
    private readonly DispatcherTimer _minuteurRelayoutApresRedimensionnement = new();
    private readonly DispatcherTimer _minuteurRepriseAnimationGrilleSucces = new();
    private readonly DispatcherTimer _minuteurAffichageTemporaireSuccesGrille = new();
    private readonly DispatcherTimer _minuteurRotationVisuelsJeuEnCours = new();
    private readonly Random _generateurAleatoireSuccesGrille = new();
    private readonly Dictionary<string, ImageSource> _cacheImagesDistantes = new(
        StringComparer.OrdinalIgnoreCase
    );
    private readonly Dictionary<string, Task<ImageSource?>> _chargementsImagesDistantesEnCours =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly List<VisuelJeuEnCours> _visuelsJeuEnCours = [];
    private SystemControls.Primitives.ScrollBar? _barreDefilementVerticalePrincipale;
    private readonly EtatListeSuccesUi _etatListeSuccesUi = new();
    private bool _connexionInitialeAffichee;
    private bool _chargementJeuEnCoursActif;
    private bool _actualisationApiCibleeEnAttente;
    private bool _surveillanceRichPresenceEnCours;
    private bool _surveillancePresenceLocaleCompteEnCours;
    private bool _surveillanceLocaleEmulateursEnCours;
    private bool _profilUtilisateurAccessible = true;
    private bool _dernierJeuAfficheModifie;
    private bool _dernierSuccesAfficheModifie;
    private bool _derniereListeSuccesAfficheeModifiee;
    private bool _miseAJourAnimationTitreJeuPlanifiee;
    private int _dernierIdentifiantJeuApi;
    private int _dernierIdentifiantJeuAvecInfos;
    private int _dernierIdentifiantJeuAvecProgression;
    private int _versionChargementContenuJeu;
    private EtatPipelineChargementJeu _etatPipelineChargementJeu = EtatPipelineChargementJeu.Vide;
    private UserProfileV2? _dernierProfilUtilisateurCharge;
    private UserSummaryV2? _dernierResumeUtilisateurCharge;
    private DonneesJeuAffiche? _dernieresDonneesJeuAffichees;
    private string _dernierTitreJeuApi = string.Empty;
    private string _dernierePresenceRiche = string.Empty;
    private string _signatureDerniereNoticeCompteJournalisee = string.Empty;
    private string _dernierPseudoCharge = string.Empty;
    private string _signatureAnimationTitreJeu = string.Empty;
    private string _etatConnexionCourant = "Non configuré";
    private string _cheminImageJeuEnCoursDemande = string.Empty;
    private string _cheminImageJeuEnCoursAffiche = string.Empty;
    private ConfigurationConnexion _configurationConnexion = new();
    private int _indexVisuelJeuEnCours;
    private int _identifiantJeuSuccesCourant;
    private double _largeurMaxVisuelJeuEnCours;
    private double _hauteurMaxVisuelJeuEnCours;
    private Dictionary<int, HashSet<int>> _succesDebloquesLocauxTemporaires = [];
    private Dictionary<string, DateTimeOffset> _succesDetectesRecemment = [];
    private IReadOnlyList<ConsoleV2> _consolesResolutionLocale = [];
    private Dictionary<int, EtatObservationSuccesLocal> _etatSuccesObserves = [];
    private List<GameAchievementV2> _succesJeuCourant = [];
    private SuccesDebloqueDetecte? _succesDebloqueDetecteEnAttente;
    private EtatSondeLocaleEmulateur? _dernierEtatSondeLocaleEmulateurs;
    private bool _presenceLocaleCompteActive;
    private string _signatureDernierSuccesLocalDirectAffiche = string.Empty;
    private int _identifiantJeuSuccesObserve;
    private int _identifiantJeuLocalActif;
    private string _titreJeuLocalActif = string.Empty;
    private int _identifiantJeuLocalResolutEnAttente;
    private string _titreJeuLocalResolutEnAttente = string.Empty;

    /// <summary>
    /// Initialise la fenêtre principale.
    /// </summary>
    public MainWindow()
    {
        App.JournaliserDemarrage("MainWindow ctor début");
        InitializeComponent();
        App.JournaliserDemarrage("MainWindow ctor fin");
        ServiceSurveillanceSuccesLocaux.ReinitialiserJournalSession();
        ReinitialiserJournalDiagnosticListeSucces();
        MettreAJourLibelleOrdreSuccesGrilleEtModes();
        AppliquerIconeApplication();
        AppliquerVersionApplication();
        ReinitialiserJeuEnCours();
        ConfigurerActualisationAutomatique();
        _serviceSurveillanceSuccesLocaux.SignalRecu += SurveillanceSuccesLocaux_SignalRecu;
#if DEBUG
        PreviewKeyDown += FenetrePrincipale_PreviewKeyDown_Debug;
#endif
        Loaded += FenetrePrincipaleChargee;
        Closing += FenetrePrincipale_Fermeture;
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
    }

    /// <summary>
    /// Prépare les minuteurs qui pilotent l'actualisation API et l'UI locale.
    /// </summary>
    private void ConfigurerActualisationAutomatique()
    {
        _minuteurActualisationApi.Interval = IntervalleActualisationApi;
        _minuteurActualisationApi.Tick += ActualisationApi_Tick;

        _minuteurActualisationRichPresence.Interval = IntervalleActualisationRichPresence;
        _minuteurActualisationRichPresence.Tick += ActualisationRichPresence_Tick;

        _minuteurPresenceLocaleCompte.Interval = IntervallePresenceLocaleCompte;
        _minuteurPresenceLocaleCompte.Tick += ActualisationPresenceLocaleCompte_Tick;

        _minuteurSondeLocaleEmulateurs.Interval = IntervalleSondeLocaleEmulateurs;
        _minuteurSondeLocaleEmulateurs.Tick += ActualisationSondeLocaleEmulateurs_Tick;

        _minuteurMasquageBarreDefilement.Interval = IntervalleMasquageBarreDefilement;
        _minuteurMasquageBarreDefilement.Tick += MinuteurMasquageBarreDefilement_Tick;

        _minuteurRelayoutApresRedimensionnement.Interval = IntervalleRelayoutApresRedimensionnement;
        _minuteurRelayoutApresRedimensionnement.Tick += MinuteurRelayoutApresRedimensionnement_Tick;

        _minuteurRepriseAnimationGrilleSucces.Interval = IntervalleRepriseAnimationGrilleSucces;
        _minuteurRepriseAnimationGrilleSucces.Tick += MinuteurRepriseAnimationGrilleSucces_Tick;

        _minuteurAffichageTemporaireSuccesGrille.Interval = DureeAffichageTemporaireSuccesGrille;
        _minuteurAffichageTemporaireSuccesGrille.Tick +=
            MinuteurAffichageTemporaireSuccesGrille_Tick;

        _minuteurRotationVisuelsJeuEnCours.Interval = IntervalleRotationVisuelsJeuEnCours;
        _minuteurRotationVisuelsJeuEnCours.Tick += MinuteurRotationVisuelsJeuEnCours_Tick;
    }

    /// <summary>
    /// Charge la configuration locale puis affiche la modale de connexion au premier lancement.
    /// </summary>
    private async void FenetrePrincipaleChargee(object sender, RoutedEventArgs e)
    {
        App.JournaliserDemarrage("FenetrePrincipaleChargee début");
        Mouse.OverrideCursor = null;

        if (_connexionInitialeAffichee)
        {
            return;
        }

        _connexionInitialeAffichee = true;
        DefinirVisibiliteContenuPrincipal(true);
        AjusterDisposition();
        App.JournaliserDemarrage("FenetrePrincipaleChargee avant ChargerConfig");
        _configurationConnexion = await _serviceConfigurationLocale.ChargerAsync();
        App.JournaliserDemarrage("FenetrePrincipaleChargee apres ChargerConfig");
        AppliquerGeometrieFenetre();
        MettreAJourResumeConnexion();
        AjusterDisposition();
        _ = Dispatcher.BeginInvoke(
            DefinirVisibiliteBarreDefilementPrincipale,
            DispatcherPriority.Loaded
        );

        if (!ConfigurationConnexionEstComplete())
        {
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            await AfficherModaleConnexionAsync();

            if (!ConfigurationConnexionEstComplete())
            {
                App.JournaliserDemarrage("FenetrePrincipaleChargee fin sans configuration");
                return;
            }
        }

        App.JournaliserDemarrage("FenetrePrincipaleChargee avant DernierJeuSauvegarde");
        await AppliquerDernierJeuSauvegardeAsync();
        App.JournaliserDemarrage("FenetrePrincipaleChargee apres DernierJeuSauvegarde");
        bool conserverEtatSauvegardeAuPremierChargement =
            _configurationConnexion.DernierJeuAffiche is not null;

        App.JournaliserDemarrage("FenetrePrincipaleChargee avant ChargerJeuEnCours");
        await ChargerJeuEnCoursAsync(!conserverEtatSauvegardeAuPremierChargement, true);
        App.JournaliserDemarrage("FenetrePrincipaleChargee apres ChargerJeuEnCours");
        _ = Dispatcher.BeginInvoke(
            () => DemarrerActualisationAutomatique(),
            DispatcherPriority.ApplicationIdle
        );
        App.JournaliserDemarrage("FenetrePrincipaleChargee fin");
    }

    /// <summary>
    /// Sauvegarde la géométrie de la fenêtre au moment de la fermeture.
    /// </summary>
    private void FenetrePrincipale_Fermeture(object? sender, CancelEventArgs e)
    {
        ArreterActualisationAutomatique();
        _serviceSurveillanceSuccesLocaux.Dispose();
        MemoriserGeometrieFenetre();

        try
        {
            Task.Run(() =>
                    _serviceConfigurationLocale.SauvegarderEtatApplicationAsync(
                        _configurationConnexion
                    )
                )
                .GetAwaiter()
                .GetResult();
        }
        catch
        {
            // évite de bloquer la fermeture si la persistance locale échoue au tout dernier moment.
        }
    }

    /// <summary>
    /// Réorganise l'interface quand la fenêtre devient trop étroite.
    /// </summary>
    private void FenetrePrincipale_TailleChangee(object sender, SizeChangedEventArgs e)
    {
        if (
            Math.Abs(e.PreviousSize.Width - e.NewSize.Width) > 0.01
            || Math.Abs(e.PreviousSize.Height - e.NewSize.Height) > 0.01
        )
        {
            _etatListeSuccesUi.RedimensionnementFenetreActif = true;
            ReinitialiserListeSuccesPourRedimensionnement();
        }

        AjusterDisposition();
        AjusterHauteurCarteJeuEnCours();
        PlanifierMiseAJourDispositionGrilleTousSucces();
        PlanifierAjustementHauteurListeSuccesJeuEnCours();
        PlanifierMiseAJourAnimationGrilleTousSucces();
        PlanifierRelayoutListeSuccesApresRedimensionnement();
    }

    /// <summary>
    /// Réagit au changement de taille de la zone de contenu visible.
    /// </summary>
    private void ZonePrincipale_TailleChangee(object sender, SizeChangedEventArgs e)
    {
        if (
            Math.Abs(e.PreviousSize.Width - e.NewSize.Width) > 0.01
            || Math.Abs(e.PreviousSize.Height - e.NewSize.Height) > 0.01
        )
        {
            _etatListeSuccesUi.RedimensionnementFenetreActif = true;
            ReinitialiserListeSuccesPourRedimensionnement();
        }

        AjusterHauteurCarteJeuEnCours();
        PlanifierMiseAJourDispositionGrilleTousSucces();
        PlanifierAjustementHauteurListeSuccesJeuEnCours();
        PlanifierRelayoutListeSuccesApresRedimensionnement();
    }
}
