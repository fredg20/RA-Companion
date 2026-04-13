using System.Windows;

namespace RA.Compagnon;

public partial class MainWindow
{
    private void FenetrePrincipale_TailleChangee(object sender, SizeChangedEventArgs e)
    {
        TraiterChangementTaille(e.PreviousSize, e.NewSize);
        PlanifierSauvegardeGeometrieFenetre();
        PlanifierMiseAJourAnimationGrilleTousSucces();
    }

    private void ZonePrincipale_TailleChangee(object sender, SizeChangedEventArgs e)
    {
        TraiterChangementTaille(e.PreviousSize, e.NewSize);
    }

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

    private void PlanifierSauvegardeGeometrieFenetre()
    {
        if (!_geometrieFenetrePretePourPersistance || !IsLoaded)
        {
            return;
        }

        _minuteurSauvegardeGeometrieFenetre.Stop();
        _minuteurSauvegardeGeometrieFenetre.Start();
    }

    private void FenetrePrincipale_PositionChangee(object? sender, EventArgs e)
    {
        PlanifierSauvegardeGeometrieFenetre();
    }

    private void FenetrePrincipale_EtatChange(object? sender, EventArgs e)
    {
        PlanifierSauvegardeGeometrieFenetre();
    }

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
