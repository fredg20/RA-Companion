using System.Windows;
using System.Windows.Input;
using RA.Compagnon.Modeles.Debug;
using RA.Compagnon.Modeles.Local;
using RA.Compagnon.Services;

namespace RA.Compagnon;

public partial class MainWindow
{
#if DEBUG
    private async void FenetrePrincipale_PreviewKeyDown_Debug(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers != (ModifierKeys.Control | ModifierKeys.Shift))
        {
            return;
        }

        Key touche = e.Key == Key.System ? e.SystemKey : e.Key;

        if (touche != Key.F8 && touche != Key.F10)
        {
            return;
        }

        e.Handled = true;
        ServiceTestSuccesDebug.JournaliserEvenement(
            "test_succes_raccourci_recu",
            $"touche={touche};jeu={_identifiantJeuLocalActif};jeuSucces={_identifiantJeuSuccesCourant};nbSucces={_succesJeuCourant.Count}"
        );
        await DeclencherTestSuccesDebugAsync();
    }

    private async Task DeclencherTestSuccesDebugAsync()
    {
        string nomEmulateur =
            _dernierEtatSondeLocaleEmulateurs?.NomEmulateur?.Trim() ?? string.Empty;
        int identifiantJeu =
            _identifiantJeuLocalActif > 0
                ? _identifiantJeuLocalActif
                : _identifiantJeuSuccesCourant;
        string titreJeu = !string.IsNullOrWhiteSpace(_titreJeuLocalActif)
            ? _titreJeuLocalActif
            : _dernieresDonneesJeuAffichees?.Jeu.Title?.Trim() ?? string.Empty;

        ResultatScenarioTestSuccesDebug resultat =
            _serviceTestSuccesDebug.ConstruireScenarioDepuisContexte(
                nomEmulateur,
                identifiantJeu,
                titreJeu,
                _succesJeuCourant,
                SuccesEstDebloquePourAffichage
            );

        if (!resultat.EstValide || resultat.Scenario is null)
        {
            ServiceTestSuccesDebug.JournaliserEvenement(
                "test_succes_ignore",
                $"motif={resultat.Motif};emulateur={nomEmulateur};jeu={identifiantJeu}"
            );
            MessageBox.Show(
                $"Test succès ignoré : {resultat.Motif}",
                "Compagnon DEBUG",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
            return;
        }

        ScenarioTestSuccesDebug scenario = resultat.Scenario;
        SuccesDebloqueDetecte succes = ServiceTestSuccesDebug.ConstruireSuccesDebloque(scenario);

        ServiceTestSuccesDebug.JournaliserEvenement(
            "test_succes_declenche",
            $"mode={scenario.ModeDeclenchement};source={scenario.SourceSimulee};emulateur={scenario.NomEmulateur};jeu={scenario.IdentifiantJeu};succes={scenario.IdentifiantSucces}"
        );
        ServiceDetectionSuccesJeu.JournaliserDetection(succes, scenario.SourceSimulee);
        MarquerSuccesCommeTraite(succes);

        bool affiche = await AfficherSuccesDebloqueDetecteAsync(succes);

        ServiceTestSuccesDebug.JournaliserEvenement(
            affiche ? "test_succes_affiche" : "test_succes_echec_ui",
            $"source={scenario.SourceSimulee};jeu={scenario.IdentifiantJeu};succes={scenario.IdentifiantSucces}"
        );

        if (!affiche)
        {
            MessageBox.Show(
                "Le succès de test a été déclenché, mais l'UI n'a pas pu l'afficher.",
                "Compagnon DEBUG",
                MessageBoxButton.OK,
                MessageBoxImage.Warning
            );
        }
    }
#endif
}
