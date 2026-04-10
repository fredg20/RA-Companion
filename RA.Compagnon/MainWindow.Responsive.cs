using System.Windows;

namespace RA.Compagnon;

public partial class MainWindow
{
    /// <summary>
    /// Réagit au changement de taille de la fenêtre principale.
    /// </summary>
    private void FenetrePrincipale_TailleChangee(object sender, SizeChangedEventArgs e)
    {
        TraiterChangementTaille(e.PreviousSize, e.NewSize);
        PlanifierSauvegardeGeometrieFenetre();
        PlanifierMiseAJourAnimationGrilleTousSucces();
    }

    /// <summary>
    /// Réagit au changement de taille de la zone de contenu visible.
    /// </summary>
    private void ZonePrincipale_TailleChangee(object sender, SizeChangedEventArgs e)
    {
        TraiterChangementTaille(e.PreviousSize, e.NewSize);
    }

    /// <summary>
    /// Applique les ajustements communs nécessaires lors d'un redimensionnement.
    /// </summary>
    private void TraiterChangementTaille(Size taillePrecedente, Size nouvelleTaille)
    {
        if (
            Math.Abs(taillePrecedente.Width - nouvelleTaille.Width) > 0.01
            || Math.Abs(taillePrecedente.Height - nouvelleTaille.Height) > 0.01
        )
        {
            _etatListeSuccesUi.RedimensionnementFenetreActif = true;
            ReinitialiserListeSuccesPourRedimensionnement();
        }

        AjusterTypographieResponsive();
        AjusterDisposition();
        AjusterHauteurCarteJeuEnCours();
        PlanifierMiseAJourDispositionGrilleTousSucces();
        PlanifierAjustementHauteurListeSuccesJeuEnCours();
        PlanifierRelayoutListeSuccesApresRedimensionnement();
    }

    /// <summary>
    /// Planifie une sauvegarde locale de la géométrie après déplacement ou redimensionnement.
    /// </summary>
    private void PlanifierSauvegardeGeometrieFenetre()
    {
        if (!_geometrieFenetrePretePourPersistance || !IsLoaded)
        {
            return;
        }

        _minuteurSauvegardeGeometrieFenetre.Stop();
        _minuteurSauvegardeGeometrieFenetre.Start();
    }

    /// <summary>
    /// Mémorise la nouvelle position de la fenêtre après un déplacement.
    /// </summary>
    private void FenetrePrincipale_PositionChangee(object? sender, EventArgs e)
    {
        PlanifierSauvegardeGeometrieFenetre();
    }

    /// <summary>
    /// Mémorise aussi la géométrie de restauration quand l'état de la fenêtre change.
    /// </summary>
    private void FenetrePrincipale_EtatChange(object? sender, EventArgs e)
    {
        PlanifierSauvegardeGeometrieFenetre();
    }

    /// <summary>
    /// Sauvegarde la géométrie sur disque une fois que le déplacement ou le redimensionnement est stabilisé.
    /// </summary>
    private async void MinuteurSauvegardeGeometrieFenetre_Tick(object? sender, EventArgs e)
    {
        _minuteurSauvegardeGeometrieFenetre.Stop();

        if (!_geometrieFenetrePretePourPersistance)
        {
            return;
        }

        MemoriserGeometrieFenetre();

        try
        {
            await _serviceConfigurationLocale.SauvegarderEtatApplicationAsync(
                _configurationConnexion
            );
        }
        catch
        {
            // évite de gêner l'UI si la persistance locale échoue pendant un déplacement.
        }
    }
}
