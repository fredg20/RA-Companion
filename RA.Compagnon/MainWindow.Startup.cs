using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using RA.Compagnon.Services;

namespace RA.Compagnon;

public partial class MainWindow
{
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
        await DemarrerCycleApplicatifAsync();
        App.JournaliserDemarrage("FenetrePrincipaleChargee fin");
    }

    /// <summary>
    /// Exécute le scénario de démarrage complet de la fenêtre principale.
    /// </summary>
    private async Task DemarrerCycleApplicatifAsync()
    {
        DefinirVisibiliteContenuPrincipal(true);
        AjusterDisposition();

        await ChargerConfigurationInitialeAsync();
        AppliquerConfigurationInitiale();

        if (!await VerifierOuObtenirConfigurationConnexionAsync())
        {
            App.JournaliserDemarrage("FenetrePrincipaleChargee fin sans configuration");
            return;
        }

        bool conserverEtatSauvegardeAuPremierChargement = await RestaurerEtatInitialAsync();
        await ChargerJeuInitialAsync(conserverEtatSauvegardeAuPremierChargement);
        PlanifierDemarrageActualisations();
        _ = VerifierMiseAJourApplicationSiNecessaireAsync();
    }

    /// <summary>
    /// Charge la configuration persistée au démarrage.
    /// </summary>
    private async Task ChargerConfigurationInitialeAsync()
    {
        App.JournaliserDemarrage("FenetrePrincipaleChargee avant ChargerConfig");
        _configurationConnexion = await _serviceConfigurationLocale.ChargerAsync();
        App.JournaliserDemarrage("FenetrePrincipaleChargee apres ChargerConfig");
    }

    /// <summary>
    /// Applique la configuration chargée à l'interface et aux services statiques.
    /// </summary>
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
        AjusterTypographieResponsive(true);
        MettreAJourResumeConnexion();
        AjusterDisposition();
        _ = Dispatcher.BeginInvoke(
            DefinirVisibiliteBarreDefilementPrincipale,
            DispatcherPriority.Loaded
        );
    }

    /// <summary>
    /// Vérifie si la configuration de connexion est suffisante, sinon ouvre la modale de connexion.
    /// </summary>
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

    /// <summary>
    /// Restaure l'état sauvegardé et indique s'il faut le conserver lors du premier chargement.
    /// </summary>
    private async Task<bool> RestaurerEtatInitialAsync()
    {
        App.JournaliserDemarrage("FenetrePrincipaleChargee avant DernierJeuSauvegarde");
        await AppliquerDernierJeuSauvegardeAsync();
        App.JournaliserDemarrage("FenetrePrincipaleChargee apres DernierJeuSauvegarde");
        return _configurationConnexion.DernierJeuAffiche is not null;
    }

    /// <summary>
    /// Charge le jeu courant initial en respectant l'état restauré.
    /// </summary>
    private async Task ChargerJeuInitialAsync(bool conserverEtatSauvegardeAuPremierChargement)
    {
        App.JournaliserDemarrage("FenetrePrincipaleChargee avant ChargerJeuEnCours");
        await ChargerJeuEnCoursAsync(!conserverEtatSauvegardeAuPremierChargement, true);
        App.JournaliserDemarrage("FenetrePrincipaleChargee apres ChargerJeuEnCours");
    }

    /// <summary>
    /// Planifie le démarrage des actualisations périodiques une fois l'UI stabilisée.
    /// </summary>
    private void PlanifierDemarrageActualisations()
    {
        _ = Dispatcher.BeginInvoke(
            () => DemarrerActualisationAutomatique(),
            DispatcherPriority.ApplicationIdle
        );
    }
}
