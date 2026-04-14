using System.Windows;
using System.Windows.Input;
using RA.Compagnon.Modeles.Api.V2.Game;
using RA.Compagnon.Modeles.Debug;
using RA.Compagnon.Modeles.Local;
using RA.Compagnon.Services;

/*
 * Regroupe les raccourcis et scénarios de test DEBUG liés à la simulation
 * de déblocages de succès dans la fenêtre principale.
 */
namespace RA.Compagnon;

/*
 * Porte la logique de déclenchement manuel des scénarios de test de succès
 * en mode DEBUG.
 */
public partial class MainWindow
{
#if DEBUG
    /*
     * Intercepte certains raccourcis clavier DEBUG pour déclencher un test de succès.
     */
    private async void FenetrePrincipale_PreviewKeyDown_Debug(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers != (ModifierKeys.Control | ModifierKeys.Shift))
        {
            return;
        }

        Key touche = e.Key == Key.System ? e.SystemKey : e.Key;

        if (touche != Key.F7 && touche != Key.F8 && touche != Key.F9 && touche != Key.F10)
        {
            return;
        }

        e.Handled = true;
        ServiceTestSuccesDebug.JournaliserEvenement(
            "test_succes_raccourci_recu",
            $"touche={touche};jeu={_identifiantJeuLocalActif};jeuSucces={_identifiantJeuSuccesCourant};nbSucces={_succesJeuCourant.Count}"
        );
        bool hardcore = touche != Key.F10;
        ModeDeclenchementTestSuccesDebug modeDeclenchement = touche switch
        {
            Key.F9 => ModeDeclenchementTestSuccesDebug.SourceLocale,
            Key.F7 => ModeDeclenchementTestSuccesDebug.Session,
            _ => ModeDeclenchementTestSuccesDebug.InterneUi,
        };
        await DeclencherTestSuccesDebugAsync(modeDeclenchement, hardcore);
    }

    /*
     * Déclenche un scénario de test de succès selon le mode demandé.
     */
    private async Task DeclencherTestSuccesDebugAsync(
        ModeDeclenchementTestSuccesDebug modeDeclenchement,
        bool hardcore
    )
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
            ServiceTestSuccesDebug.ConstruireScenarioDepuisContexte(
                nomEmulateur,
                identifiantJeu,
                titreJeu,
                _succesJeuCourant,
                SuccesEstDebloquePourAffichage,
                modeDeclenchement,
                hardcore
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
            $"mode={scenario.ModeDeclenchement};hardcore={scenario.Hardcore};source={scenario.SourceSimulee};emulateur={scenario.NomEmulateur};jeu={scenario.IdentifiantJeu};succes={scenario.IdentifiantSucces}"
        );

        if (scenario.ModeDeclenchement == ModeDeclenchementTestSuccesDebug.SourceLocale)
        {
            ResultatExecutionTestSuccesDebug execution =
                ServiceTestSuccesDebug.InjecterScenarioSourceLocale(scenario);

            ServiceTestSuccesDebug.JournaliserEvenement(
                execution.EstReussi
                    ? "test_succes_injection_reussie"
                    : "test_succes_injection_echec",
                $"source={scenario.SourceSimulee};typeSource={scenario.TypeSourceLocale};chemin={execution.Chemin};motif={execution.Motif};jeu={scenario.IdentifiantJeu};succes={scenario.IdentifiantSucces}"
            );

            if (!execution.EstReussi)
            {
                MessageBox.Show(
                    $"Injection source locale impossible : {execution.Motif}",
                    "Compagnon DEBUG",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
            }

            SignalSuccesLocal? signalSourceLocale =
                ServiceTestSuccesDebug.ConstruireSignalSourceLocale(scenario);

            if (signalSourceLocale is not null)
            {
                ServiceTestSuccesDebug.JournaliserEvenement(
                    ServiceCatalogueEmulateursLocaux.SurveillanceSuccesActive(scenario.NomEmulateur)
                        ? "test_succes_signal_source_locale"
                        : "test_succes_signal_source_locale_simule",
                    $"source={scenario.SourceSimulee};typeSource={scenario.TypeSourceLocale};emulateur={scenario.NomEmulateur};jeu={scenario.IdentifiantJeu};succes={scenario.IdentifiantSucces}"
                );
                await TraiterSignalSuccesLocalAsync(signalSourceLocale);
            }

            return;
        }

        if (scenario.ModeDeclenchement == ModeDeclenchementTestSuccesDebug.Session)
        {
            IReadOnlyDictionary<int, EtatObservationSuccesLocal> etatPrecedent =
                ServiceDetectionSuccesJeu.CapturerEtat(_succesJeuCourant);
            IReadOnlyList<GameAchievementV2> succesVirtuels =
                ServiceTestSuccesDebug.ConstruireSuccesVirtuelsSession(_succesJeuCourant, scenario);
            IReadOnlyList<SuccesDebloqueDetecte> succesDetectes =
                ServiceDetectionSuccesJeu.DetecterNouveauxSucces(
                    scenario.IdentifiantJeu,
                    scenario.TitreJeu,
                    etatPrecedent,
                    succesVirtuels
                );

            if (succesDetectes.Count == 0)
            {
                ServiceTestSuccesDebug.JournaliserEvenement(
                    "test_succes_session_aucun_resultat",
                    $"source={scenario.SourceSimulee};jeu={scenario.IdentifiantJeu};succes={scenario.IdentifiantSucces}"
                );
                MessageBox.Show(
                    "Le test de session n'a détecté aucun nouveau succès.",
                    "Compagnon DEBUG",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
                return;
            }

            bool unSuccesAffiche = false;

            foreach (SuccesDebloqueDetecte succesDetecte in succesDetectes)
            {
                if (SuccesDejaTraiteRecemment(succesDetecte))
                {
                    ServiceTestSuccesDebug.JournaliserEvenement(
                        "test_succes_session_ignore",
                        $"motif=deja_traite;source={scenario.SourceSimulee};jeu={succesDetecte.IdentifiantJeu};succes={succesDetecte.IdentifiantSucces}"
                    );
                    continue;
                }

                ServiceDetectionSuccesJeu.JournaliserDetection(
                    succesDetecte,
                    scenario.SourceSimulee
                );
                MarquerSuccesCommeTraite(succesDetecte);
                bool succesAffiche = await AfficherSuccesDebloqueDetecteAsync(succesDetecte);
                unSuccesAffiche |= succesAffiche;

                ServiceTestSuccesDebug.JournaliserEvenement(
                    succesAffiche ? "test_succes_session_affiche" : "test_succes_session_echec_ui",
                    $"source={scenario.SourceSimulee};jeu={succesDetecte.IdentifiantJeu};succes={succesDetecte.IdentifiantSucces}"
                );
            }

            if (!unSuccesAffiche)
            {
                MessageBox.Show(
                    "Le succès de test session a été détecté, mais l'UI n'a pas pu l'afficher.",
                    "Compagnon DEBUG",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
            }

            return;
        }

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
