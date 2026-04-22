using System.Windows.Threading;

/*
 * Regroupe les minuteurs utilisés par la fenêtre principale pour ses
 * rafraîchissements et temporisations visuelles.
 */
namespace RA.Compagnon;

/*
 * Porte la configuration et le pilotage des timers de la fenêtre principale.
 */
public partial class MainWindow
{
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
    private readonly DispatcherTimer _minuteurSauvegardeGeometrieFenetre = new();
    private readonly DispatcherTimer _minuteurMiseAJourApplication = new(
        DispatcherPriority.Background
    );

    /*
     * Configure tous les minuteurs utilisés par l'application et leurs callbacks.
     */
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

        _minuteurSauvegardeGeometrieFenetre.Interval = IntervalleSauvegardeGeometrieFenetre;
        _minuteurSauvegardeGeometrieFenetre.Tick += MinuteurSauvegardeGeometrieFenetre_Tick;

        _minuteurMiseAJourApplication.Interval = IntervalleRafraichissementMiseAJourApplication;
        _minuteurMiseAJourApplication.Tick += MinuteurMiseAJourApplication_Tick;
    }

    /*
     * Démarre les actualisations automatiques compatibles avec l'état courant.
     */
    private void DemarrerActualisationAutomatique()
    {
        if (!_minuteurMiseAJourApplication.IsEnabled)
        {
            _minuteurMiseAJourApplication.Start();
        }

        if (!ConfigurationConnexionEstComplete())
        {
            return;
        }

        if (_profilUtilisateurAccessible && !_minuteurActualisationApi.IsEnabled)
        {
            _minuteurActualisationApi.Start();
        }

        if (_profilUtilisateurAccessible && !_minuteurActualisationRichPresence.IsEnabled)
        {
            _minuteurActualisationRichPresence.Start();
        }

        if (!_minuteurPresenceLocaleCompte.IsEnabled)
        {
            _minuteurPresenceLocaleCompte.Start();
        }

        if (!_minuteurSondeLocaleEmulateurs.IsEnabled)
        {
            _minuteurSondeLocaleEmulateurs.Start();
        }
    }

    /*
     * Arrête l'ensemble des minuteurs actifs de la fenêtre principale.
     */
    private void ArreterActualisationAutomatique()
    {
        _minuteurActualisationApi.Stop();
        _minuteurActualisationRichPresence.Stop();
        _minuteurPresenceLocaleCompte.Stop();
        _minuteurSondeLocaleEmulateurs.Stop();
        _minuteurRotationVisuelsJeuEnCours.Stop();
        _minuteurSauvegardeGeometrieFenetre.Stop();
        _minuteurMiseAJourApplication.Stop();
    }
}
