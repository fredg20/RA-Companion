namespace RA.Compagnon;

public partial class MainWindow
{
    /// <summary>
    /// Active uniquement les rafraîchissements API.
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
    }

    /// <summary>
    /// Redémarre le minuteur API pour repousser le prochain tick après un rafraîchissement ciblé.
    /// </summary>
    private void RedemarrerMinuteurActualisationApi()
    {
        if (!_profilUtilisateurAccessible)
        {
            return;
        }

        _minuteurActualisationApi.Stop();
        _minuteurActualisationApi.Start();
    }

    /// <summary>
    /// Arrête les rafraîchissements périodiques.
    /// </summary>
    private void ArreterActualisationAutomatique()
    {
        _minuteurActualisationApi.Stop();
        _minuteurRotationVisuelsJeuEnCours.Stop();
    }

    /// <summary>
    /// Aucun amorçage local : l'application reste entièrement autonome.
    /// </summary>
    private Task AmorcerEtatJeuLocalAuDemarrageAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Réinitialise les derniers marqueurs utilisés pour éviter les rechargements API inutiles.
    /// </summary>
    private void ReinitialiserContexteSurveillance()
    {
        _actualisationApiCibleeEnAttente = false;
        _dernierIdentifiantJeuApi = 0;
        _dernierIdentifiantJeuAvecInfos = 0;
        _dernierIdentifiantJeuAvecProgression = 0;
        _dernierTitreJeuApi = string.Empty;
        _dernierePresenceRiche = string.Empty;
        _dernierPseudoCharge = string.Empty;
        _dernierProfilUtilisateurCharge = null;
        _dernierResumeUtilisateurCharge = null;
    }
}
