using System.Windows.Threading;

namespace RA.Compagnon;

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

        _minuteurSauvegardeGeometrieFenetre.Interval = IntervalleSauvegardeGeometrieFenetre;
        _minuteurSauvegardeGeometrieFenetre.Tick += MinuteurSauvegardeGeometrieFenetre_Tick;
    }

    /// <summary>
    /// Active les rafraîchissements API généraux ainsi que la surveillance légère du Rich Presence.
    /// </summary>
    private void DemarrerActualisationAutomatique()
    {
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

    /// <summary>
    /// Arrête les rafraîchissements périodiques.
    /// </summary>
    private void ArreterActualisationAutomatique()
    {
        _minuteurActualisationApi.Stop();
        _minuteurActualisationRichPresence.Stop();
        _minuteurPresenceLocaleCompte.Stop();
        _minuteurSondeLocaleEmulateurs.Stop();
        _minuteurRotationVisuelsJeuEnCours.Stop();
        _minuteurSauvegardeGeometrieFenetre.Stop();
    }
}
