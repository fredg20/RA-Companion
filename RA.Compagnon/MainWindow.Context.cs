using System.Net.Http;
using System.Windows;
using System.Windows.Media;
using RA.Compagnon.Modeles.Api.V2.Game;
using RA.Compagnon.Modeles.Api.V2.User;
using RA.Compagnon.Modeles.Etat;
using RA.Compagnon.Modeles.Local;
using RA.Compagnon.Modeles.Presentation;
using RA.Compagnon.Services;
using SystemControls = System.Windows.Controls;

/*
 * Regroupe les dépendances, caches et champs d'état partagés par les
 * différentes parties de la fenêtre principale.
 */
namespace RA.Compagnon;

/*
 * Porte le contexte global de la fenêtre principale utilisé par ses
 * fichiers partiels.
 */
public partial class MainWindow
{
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
    private readonly ServiceAnalyseDescriptionsSucces _serviceAnalyseDescriptionsSucces = new();
    private readonly ServiceSondeLocaleEmulateurs _serviceSondeLocaleEmulateurs = new();
    private readonly ServiceSurveillanceSuccesLocaux _serviceSurveillanceSuccesLocaux = new();
    private readonly ServiceDetectionSuccesJeu _serviceDetectionSuccesJeu = new();
    private readonly ServiceDetectionSuccesUtilisateurLocal _serviceDetectionSuccesUtilisateurLocal =
        new();
    private readonly ServiceOrchestrateurEtatJeu _serviceOrchestrateurEtatJeu = new();
    private readonly ServiceMiseAJourApplication _serviceMiseAJourApplication = new();
    private readonly ServiceExportObs _serviceExportObs = new();
    private readonly ServiceServeurObsLocal _serviceServeurObsLocal = new();
#if DEBUG
    private readonly ServiceTestSuccesDebug _serviceTestSuccesDebug = new();
#endif
    private readonly Random _generateurAleatoireSuccesGrille = new();
    private readonly Dictionary<string, ImageSource> _cacheImagesDistantes = new(
        StringComparer.OrdinalIgnoreCase
    );
    private readonly Dictionary<DependencyObject, double> _taillesPoliceLocalesResponsive = [];
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
    private bool _rejeuDemarreEnAttenteChargement;
    private bool _suiviEtatJeuVisibleInitialise;
    private bool _dernierJeuAfficheModifie;
    private bool _dernierSuccesAfficheModifie;
    private bool _derniereListeSuccesAfficheeModifiee;
    private bool _modeAffichageSuccesModifie;
    private bool _miseAJourAnimationTitreJeuPlanifiee;
    private string _raisonStabilisationAffichage = string.Empty;
    private int _dernierIdentifiantJeuApi;
    private int _dernierIdentifiantJeuAvecInfos;
    private int _dernierIdentifiantJeuAvecProgression;
    private int _identifiantJeuMetaConsoleAffichee;
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
    private string _signatureDernierEtatJeuVisible = string.Empty;
    private ConfigurationConnexion _configurationConnexion = new();
    private int _indexVisuelJeuEnCours;
    private int _identifiantJeuSuccesCourant;
    private int _versionAffichageSuccesEnCours;
    private ResultatAnalyseDescriptionsSucces? _analyseSuccesEnCours;
    private double _largeurMaxVisuelJeuEnCours;
    private double _hauteurMaxVisuelJeuEnCours;
    private Dictionary<int, Dictionary<int, bool>> _succesDebloquesLocauxTemporaires = [];
    private Dictionary<string, DateTimeOffset> _succesDetectesRecemment = [];
    private IReadOnlyList<ConsoleV2> _consolesResolutionLocale = [];
    private Dictionary<int, EtatObservationSuccesLocal> _etatSuccesObserves = [];
    private List<GameAchievementV2> _succesJeuCourant = [];
    private SuccesDebloqueDetecte? _succesDebloqueDetecteEnAttente;
    private EtatSondeLocaleEmulateur? _dernierEtatSondeLocaleEmulateurs;
    private bool _emulateurValideDetecteEnDirect;
    private bool _presenceLocaleCompteActive;
    private string _signatureDernierSuccesLocalDirectAffiche = string.Empty;
    private string _signatureSuccesLocalDirectIgnoreeAuRejeu = string.Empty;
    private int _identifiantJeuSuccesObserve;
    private int _identifiantJeuDernierSignalSuccesLocal;
    private string _nomEmulateurDernierSignalSuccesLocal = string.Empty;
    private string _typeSourceDernierSignalSuccesLocal = string.Empty;
    private DateTimeOffset _horodatageDernierSignalSuccesLocalUtc = DateTimeOffset.MinValue;
    private int _identifiantJeuLocalActif;
    private string _titreJeuLocalActif = string.Empty;
    private int _identifiantJeuLocalResolutEnAttente;
    private string _titreJeuLocalResolutEnAttente = string.Empty;
    private int _identifiantJeuRejouableCourant;
    private string _nomEmulateurRejouableCourant = string.Empty;
    private string _cheminEmulateurRejouableCourant = string.Empty;
    private string _cheminJeuRejouableCourant = string.Empty;
    private EtatMiseAJourApplication _etatMiseAJourApplication =
        EtatMiseAJourApplication.CreerEtatInitial("inconnue");
    private DateTimeOffset _horodatageDerniereVerificationMiseAJourUtc = DateTimeOffset.MinValue;
    private bool _verificationMiseAJourApplicationEnCours;
    private bool _telechargementMiseAJourApplicationEnCours;
    private string _versionMiseAJourTelechargee = string.Empty;
    private string _messageTelechargementMiseAJourApplication = string.Empty;
    private string? _cheminFichierMiseAJourTelechargee;
    private double _facteurTypographieResponsive = 1;
    private bool _geometrieFenetrePretePourPersistance;
    private DateTimeOffset _horodatageDerniereSynchronisationEtatJeuUtc = DateTimeOffset.MinValue;
}
