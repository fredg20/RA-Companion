using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using RA.Compagnon.Services;

/*
 * Regroupe la séquence de démarrage de la fenêtre principale, depuis son
 * chargement visuel jusqu'au premier chargement de jeu.
 */
namespace RA.Compagnon;

/*
 * Porte la logique d'initialisation différée de la fenêtre principale et
 * du cycle applicatif au lancement.
 */
public partial class MainWindow
{
    /*
     * Réagit au chargement initial de la fenêtre principale et déclenche
     * le démarrage complet de l'application.
     */
    private async void FenetrePrincipaleChargee(object sender, RoutedEventArgs e)
    {
        App.JournaliserDemarrage("FenetrePrincipaleChargee début");
        Mouse.OverrideCursor = null;

        if (_connexionInitialeAffichee)
        {
            return;
        }

        _connexionInitialeAffichee = true;
        await DemarrerCycleApplicatifAsync();
        App.JournaliserDemarrage("FenetrePrincipaleChargee fin");
    }

    /*
     * Orchestre le démarrage applicatif après l'affichage de la fenêtre.
     */
    private async Task DemarrerCycleApplicatifAsync()
    {
        DefinirVisibiliteContenuPrincipal(true);
        AjusterDisposition();

        await ChargerConfigurationInitialeAsync();
        AppliquerConfigurationInitiale();
        await InitialiserMiseEnAvantBoutonAidePremiereUtilisationAsync();

        if (!await VerifierOuObtenirConfigurationConnexionAsync())
        {
            App.JournaliserDemarrage("FenetrePrincipaleChargee fin sans configuration");
            return;
        }

        bool conserverEtatSauvegardeAuPremierChargement = await RestaurerEtatInitialAsync();
        await ChargerJeuInitialAsync(conserverEtatSauvegardeAuPremierChargement);
        _serviceServeurObsLocal.Demarrer();
        _ = ExporterEtatObsAsync();
        PlanifierDemarrageActualisations();
        _ = VerifierMiseAJourApplicationSiNecessaireAsync();
    }

    /*
     * Charge la configuration locale initiale de l'application.
     */
    private async Task ChargerConfigurationInitialeAsync()
    {
        App.JournaliserDemarrage("FenêtrePrincipaleChargée avant ChargerConfig");
        _configurationConnexion = await _serviceConfigurationLocale.ChargerAsync();
        App.JournaliserDemarrage("FenêtrePrincipaleChargée après ChargerConfig");
    }

    /*
     * Applique à l'interface les réglages issus de la configuration initiale.
     */
    private void AppliquerConfigurationInitiale()
    {
        ServiceSourcesLocalesEmulateurs.ConfigurerEmplacementsEmulateursManuels(
            _configurationConnexion.EmplacementsEmulateursManuels
        );
        ServiceSourcesLocalesEmulateurs.ConfigurerEmplacementsEmulateursDetectes(
            _configurationConnexion.EmplacementsEmulateursDetectes
        );
        AppliquerGeometrieFenetre();
        _geometrieFenetrePretePourPersistance = true;
        AppliquerModeAffichageSuccesDepuisConfiguration();
        AjusterTypographieResponsive(true);
        MettreAJourResumeConnexion();
        AjusterDisposition();
        _ = Dispatcher.BeginInvoke(
            DefinirVisibiliteBarreDefilementPrincipale,
            DispatcherPriority.Loaded
        );
    }

    /*
     * Vérifie que la configuration de connexion est complète ou ouvre
     * la modale de connexion si nécessaire.
     */
    private async Task<bool> VerifierOuObtenirConfigurationConnexionAsync()
    {
        if (ConfigurationConnexionEstComplete())
        {
            return true;
        }

        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        await AfficherModaleConnexionAsync();
        return ConfigurationConnexionEstComplete();
    }

    /*
     * Restaure l'état visuel initial sauvegardé avant le premier chargement.
     */
    private async Task<bool> RestaurerEtatInitialAsync()
    {
        App.JournaliserDemarrage("FenêtrePrincipaleChargée avant DernierJeuSauvegarde");
        await AppliquerDernierJeuSauvegardeAsync();
        App.JournaliserDemarrage("FenêtrePrincipaleChargée après DernierJeuSauvegarde");
        return _configurationConnexion.DernierJeuAffiche is not null;
    }

    /*
     * Charge le jeu initial visible au démarrage en tenant compte de l'état restauré.
     */
    private async Task ChargerJeuInitialAsync(bool conserverEtatSauvegardeAuPremierChargement)
    {
        App.JournaliserDemarrage("FenêtrePrincipaleChargée avant ChargerJeuEnCours");
        await ChargerJeuEnCoursAsync(!conserverEtatSauvegardeAuPremierChargement, true);
        App.JournaliserDemarrage("FenêtrePrincipaleChargée après ChargerJeuEnCours");
    }

    /*
     * Planifié le démarrage des actualisations automatiques après l'initialisation.
     */
    private void PlanifierDemarrageActualisations()
    {
        _ = Dispatcher.BeginInvoke(
            () => DemarrerActualisationAutomatique(),
            DispatcherPriority.ApplicationIdle
        );
    }
}
