using System.ComponentModel;

/*
 * Regroupe la logique d'arrêt de la fenêtre principale et la persistance
 * finale de l'état de l'application.
 */
namespace RA.Compagnon;

/*
 * Porte les opérations de fermeture propre de la fenêtre principale.
 */
public partial class MainWindow
{
    /*
     * Réagit à la fermeture de la fenêtre principale.
     */
    private void FenetrePrincipale_Fermeture(object? sender, CancelEventArgs e)
    {
        ExecuterArretApplication();
    }

    /*
     * Exécute l'arrêt applicatif en stoppant les services actifs et en
     * sauvegardant l'état nécessaire.
     */
    private void ExecuterArretApplication()
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
        catch { }
    }
}
