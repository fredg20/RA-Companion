using System.IO;
using RA.Compagnon.Services;

/*
 * Regroupe les journaux de diagnostic utilisés pour suivre les performances
 * et l'affichage du jeu courant.
 */
namespace RA.Compagnon;

/*
 * Porte les helpers de journalisation détaillée pour le changement de jeu
 * et l'affichage de la liste des succès.
 */
public partial class MainWindow
{
    private string _signatureDiagnosticChangementJeu = string.Empty;
    private System.Diagnostics.Stopwatch? _chronometreDiagnosticChangementJeu;
    private static readonly string CheminJournalDiagnosticPerformance =
        ServiceModeDiagnostic.ConstruireCheminJournal("journal-performance-jeu.log");
    private static readonly string CheminJournalDiagnosticListeSucces =
        ServiceModeDiagnostic.ConstruireCheminJournal("journal-liste-succes.log");

    /*
     * Réinitialise les journaux de diagnostic de performance utilisés au chargement.
     */
    public static void ReinitialiserJournalDiagnosticPerformance()
    {
        _ = ServiceModeDiagnostic.ReinitialiserJournalSession(CheminJournalDiagnosticPerformance);
    }

    /*
     * Réinitialise les journaux de diagnostic dédiés à la liste des succès.
     */
    public static void ReinitialiserJournalDiagnosticListeSucces()
    {
        _ = ServiceModeDiagnostic.ReinitialiserJournalSession(CheminJournalDiagnosticListeSucces);
    }

    /*
     * Journalise l'état courant de la liste des succès pour analyse.
     */
    private void JournaliserDiagnosticListeSucces(string evenement, string? details = null)
    {
        double offset = ConteneurGrilleTousSuccesJeuEnCours?.VerticalOffset ?? 0;
        double hauteurVisible = ConteneurGrilleTousSuccesJeuEnCours?.ViewportHeight ?? 0;
        double hauteurDefilable = ConteneurGrilleTousSuccesJeuEnCours?.ScrollableHeight ?? 0;
        int nbBadges = GrilleTousSuccesJeuEnCours?.Children.Count ?? 0;
        string etat =
            $"offset={offset:0.##};amplitude={_etatListeSuccesUi.AmplitudeAnimation:0.##};sens={(_etatListeSuccesUi.AnimationVersBas ? "bas" : "haut")};etat={_etatListeSuccesUi.EtatInteraction};visible={hauteurVisible:0.##};scrollable={hauteurDefilable:0.##};badges={nbBadges}";

        _ = ServiceModeDiagnostic.JournaliserLigne(
            CheminJournalDiagnosticListeSucces,
            $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] categorie=liste;evenement={evenement};etat={etat}{(string.IsNullOrWhiteSpace(details) ? string.Empty : $";details={details}")}{Environment.NewLine}"
        );
    }

    /*
     * Journalise les dimensions courantes de la zone de liste des succès.
     */
    private void JournaliserDimensionsListeSucces(string evenement, string? details = null)
    {
        double largeurZonePrincipale = ZonePrincipaleListeSuccesJeuEnCours?.ActualWidth ?? 0;
        double hauteurZonePrincipale = ZonePrincipaleListeSuccesJeuEnCours?.ActualHeight ?? 0;
        double largeurZoneVisible = ZoneVisibleListeSuccesJeuEnCours?.ActualWidth ?? 0;
        double hauteurZoneVisible = ZoneVisibleListeSuccesJeuEnCours?.ActualHeight ?? 0;
        double largeurCarte = CarteListeSuccesJeuEnCours?.ActualWidth ?? 0;
        double hauteurCarte = CarteListeSuccesJeuEnCours?.ActualHeight ?? 0;
        double largeurConteneur = ConteneurGrilleTousSuccesJeuEnCours?.ActualWidth ?? 0;
        double hauteurConteneur = ConteneurGrilleTousSuccesJeuEnCours?.ActualHeight ?? 0;
        double largeurViewport = ConteneurGrilleTousSuccesJeuEnCours?.ViewportWidth ?? 0;
        double hauteurViewport = ConteneurGrilleTousSuccesJeuEnCours?.ViewportHeight ?? 0;
        double largeurGrille = GrilleTousSuccesJeuEnCours?.ActualWidth ?? 0;
        double hauteurGrille = GrilleTousSuccesJeuEnCours?.ActualHeight ?? 0;
        double extentLargeur = ConteneurGrilleTousSuccesJeuEnCours?.ExtentWidth ?? 0;
        double extentHauteur = ConteneurGrilleTousSuccesJeuEnCours?.ExtentHeight ?? 0;
        string etat =
            $"carteW={largeurCarte:0.##};carteH={hauteurCarte:0.##};contenuW={largeurZonePrincipale:0.##};contenuH={hauteurZonePrincipale:0.##};zoneW={largeurZoneVisible:0.##};zoneH={hauteurZoneVisible:0.##};conteneurW={largeurConteneur:0.##};conteneurH={hauteurConteneur:0.##};viewportW={largeurViewport:0.##};viewportH={hauteurViewport:0.##};grilleW={largeurGrille:0.##};grilleH={hauteurGrille:0.##};extentW={extentLargeur:0.##};extentH={extentHauteur:0.##}";

        _ = ServiceModeDiagnostic.JournaliserLigne(
            CheminJournalDiagnosticListeSucces,
            $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] categorie=dimensions;evenement={evenement};etat={etat}{(string.IsNullOrWhiteSpace(details) ? string.Empty : $";details={details}")}{Environment.NewLine}"
        );
    }

    /*
     * Démarre une séquence de diagnostic pour un changement de jeu.
     */
    private void DemarrerDiagnosticChangementJeu(string signature, string? details = null)
    {
        if (!ServiceModeDiagnostic.EstActif || string.IsNullOrWhiteSpace(signature))
        {
            return;
        }

        if (string.Equals(_signatureDiagnosticChangementJeu, signature, StringComparison.Ordinal))
        {
            return;
        }

        _signatureDiagnosticChangementJeu = signature;
        _chronometreDiagnosticChangementJeu = System.Diagnostics.Stopwatch.StartNew();
        JournaliserDiagnosticChangementJeu("diagnostic_debut", details ?? signature);
    }

    /*
     * Journalise une étape intermédiaire du diagnostic de changement de jeu.
     */
    private void JournaliserDiagnosticChangementJeu(string etape, string? details = null)
    {
        if (!ServiceModeDiagnostic.EstActif || _chronometreDiagnosticChangementJeu is null)
        {
            return;
        }

        _ = ServiceModeDiagnostic.JournaliserLigne(
            CheminJournalDiagnosticPerformance,
            $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] categorie=performance;+{_chronometreDiagnosticChangementJeu.Elapsed.TotalMilliseconds, 6:0}ms;evenement={etape}{(string.IsNullOrWhiteSpace(details) ? string.Empty : $";details={details}")}{Environment.NewLine}"
        );
    }

    /*
     * Journalise un événement lié à l'affichage du jeu courant.
     */
    private void JournaliserDiagnosticAffichageJeu(string evenement, string? details = null)
    {
        if (!ServiceModeDiagnostic.EstActif)
        {
            return;
        }

        _ = ServiceModeDiagnostic.JournaliserLigne(
            CheminJournalDiagnosticPerformance,
            $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] categorie=affichage;evenement={evenement}{(string.IsNullOrWhiteSpace(details) ? string.Empty : $";details={details}")}{Environment.NewLine}"
        );
    }

    /*
     * Terminé la séquence de diagnostic en cours et réinitialise son état.
     */
    private void TerminerDiagnosticChangementJeu(string etape, string? details = null)
    {
        if (!ServiceModeDiagnostic.EstActif)
        {
            _signatureDiagnosticChangementJeu = string.Empty;
            _chronometreDiagnosticChangementJeu = null;
            return;
        }

        JournaliserDiagnosticChangementJeu(etape, details);
        _signatureDiagnosticChangementJeu = string.Empty;
        _chronometreDiagnosticChangementJeu = null;
    }

    /*
     * Journalise une exception non bloquante survenue dans un flux asynchrone
     * afin de préserver la stabilité tout en gardant une trace exploitable.
     */
    private void JournaliserExceptionNonBloquante(string contexte, Exception exception)
    {
        if (!ServiceModeDiagnostic.EstActif || exception is null)
        {
            return;
        }

        string details =
            $"{exception.GetType().Name}: {NettoyerDetailsDiagnostic(exception.Message)}";

        JournaliserDiagnosticAffichageJeu($"exception_{contexte}", details);
    }

    /*
     * Observe une tâche lancée en arrière-plan afin d'éviter qu'une exception
     * non attendue ne reste silencieuse et ne laisse l'interface dans un état
     * incohérent.
     */
    private void LancerTacheNonBloquante(Task tache, string contexte)
    {
        _ = ObserverTacheNonBloquanteAsync(tache, contexte);
    }

    /*
     * Attend la fin d'une tâche de fond et journalise proprement toute erreur
     * non bloquante utile au diagnostic.
     */
    private async Task ObserverTacheNonBloquanteAsync(Task tache, string contexte)
    {
        try
        {
            await tache;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            JournaliserExceptionNonBloquante(contexte, exception);
        }
    }

    /*
     * Nettoie un texte libre avant son écriture dans un journal de diagnostic.
     */
    private static string NettoyerDetailsDiagnostic(string? details)
    {
        return string.IsNullOrWhiteSpace(details)
            ? string.Empty
            : details.Replace("\r", " ").Replace("\n", " ").Trim();
    }
}
