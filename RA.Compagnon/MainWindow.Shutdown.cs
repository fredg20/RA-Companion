using System.ComponentModel;

namespace RA.Compagnon;

public partial class MainWindow
{
    /// <summary>
    /// Sauvegarde la géométrie et l'état de l'application au moment de la fermeture.
    /// </summary>
    private void FenetrePrincipale_Fermeture(object? sender, CancelEventArgs e)
    {
        ExecuterArretApplication();
    }

    /// <summary>
    /// Exécute la séquence d'arrêt applicative sans bloquer la fermeture sur un échec de persistance.
    /// </summary>
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
        catch
        {
            // évite de bloquer la fermeture si la persistance locale échoue au tout dernier moment.
        }
    }
}
