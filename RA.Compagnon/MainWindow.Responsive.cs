using System.Windows;

/*
 * Regroupe la logique de réponse aux changements de taille et de géométrie
 * de la fenêtre principale.
 */
namespace RA.Compagnon;

/*
 * Porte les ajustements réactifs déclenchés lors des redimensionnements
 * et déplacements de la fenêtre principale.
 */
public partial class MainWindow
{
    /*
     * Réagit à un changement de taille de la fenêtre principale.
     */
    private void FenetrePrincipale_TailleChangee(object sender, SizeChangedEventArgs e)
    {
        TraiterChangementTaille(e.PreviousSize, e.NewSize);
        PlanifierSauvegardeGeometrieFenetre();
        PlanifierMiseAJourAnimationGrilleTousSucces();
    }

    /*
     * Réagit à un changement de taille de la zone principale de contenu.
     */
    private void ZonePrincipale_TailleChangee(object sender, SizeChangedEventArgs e)
    {
        TraiterChangementTaille(e.PreviousSize, e.NewSize);
    }

    /*
     * Applique les ajustements nécessaires après un changement réel de taille.
     */
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

    /*
     * Planifie la sauvegarde différée de la géométrie de la fenêtre.
     */
    private void PlanifierSauvegardeGeometrieFenetre()
    {
        if (!_geometrieFenetrePretePourPersistance || !IsLoaded)
        {
            return;
        }

        _minuteurSauvegardeGeometrieFenetre.Stop();
        _minuteurSauvegardeGeometrieFenetre.Start();
    }

    /*
     * Réagit à un déplacement de la fenêtre principale.
     */
    private void FenetrePrincipale_PositionChangee(object? sender, EventArgs e)
    {
        MettreAJourLargeurMinimaleFenetre(ObtenirZoneTravailFenetreCourante());
        PlanifierSauvegardeGeometrieFenetre();
    }

    /*
     * Réagit à un changement d'état de la fenêtre principale.
     */
    private void FenetrePrincipale_EtatChange(object? sender, EventArgs e)
    {
        MettreAJourLargeurMinimaleFenetre(ObtenirZoneTravailFenetreCourante());
        AjusterDisposition();
        PlanifierSauvegardeGeometrieFenetre();
    }

    /*
     * Sauvegarde effectivement la géométrie de la fenêtre à l'expiration du timer.
     */
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
        catch { }
    }
}
