using System.ComponentModel;

namespace RA.Compagnon;

public partial class MainWindow
{
    private void FenetrePrincipale_Fermeture(object? sender, CancelEventArgs e)
    {
        ExecuterArretApplication();
    }

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
