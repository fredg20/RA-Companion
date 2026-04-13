using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using RA.Compagnon.Services;

namespace RA.Compagnon;

public partial class MainWindow
{
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

    private async Task ChargerConfigurationInitialeAsync()
    {
        App.JournaliserDemarrage("FenetrePrincipaleChargee avant ChargerConfig");
        _configurationConnexion = await _serviceConfigurationLocale.ChargerAsync();
        App.JournaliserDemarrage("FenetrePrincipaleChargee apres ChargerConfig");
    }

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

    private async Task<bool> RestaurerEtatInitialAsync()
    {
        App.JournaliserDemarrage("FenetrePrincipaleChargee avant DernierJeuSauvegarde");
        await AppliquerDernierJeuSauvegardeAsync();
        App.JournaliserDemarrage("FenetrePrincipaleChargee apres DernierJeuSauvegarde");
        return _configurationConnexion.DernierJeuAffiche is not null;
    }

    private async Task ChargerJeuInitialAsync(bool conserverEtatSauvegardeAuPremierChargement)
    {
        App.JournaliserDemarrage("FenetrePrincipaleChargee avant ChargerJeuEnCours");
        await ChargerJeuEnCoursAsync(!conserverEtatSauvegardeAuPremierChargement, true);
        App.JournaliserDemarrage("FenetrePrincipaleChargee apres ChargerJeuEnCours");
    }

    private void PlanifierDemarrageActualisations()
    {
        _ = Dispatcher.BeginInvoke(
            () => DemarrerActualisationAutomatique(),
            DispatcherPriority.ApplicationIdle
        );
    }
}
