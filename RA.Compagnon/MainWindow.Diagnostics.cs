using System.IO;
using RA.Compagnon.Services;

namespace RA.Compagnon;

/// <summary>
/// Regroupe le diagnostic temporel du changement de jeu.
/// </summary>
public partial class MainWindow
{
    private string _signatureDiagnosticChangementJeu = string.Empty;
    private System.Diagnostics.Stopwatch? _chronometreDiagnosticChangementJeu;
    private static readonly string CheminJournalDiagnosticPerformance = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "RA-Compagnon",
        "journal-performance-jeu.log"
    );
    private static readonly string CheminJournalDiagnosticListeSucces = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "RA-Compagnon",
        "journal-liste-succes.log"
    );

    public static void ReinitialiserJournalDiagnosticPerformance()
    {
        _ = ServiceModeDiagnostic.ReinitialiserJournalSession(CheminJournalDiagnosticPerformance);
    }

    public static void ReinitialiserJournalDiagnosticListeSucces()
    {
        _ = ServiceModeDiagnostic.ReinitialiserJournalSession(CheminJournalDiagnosticListeSucces);
    }

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
            $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] evenement={evenement};etat={etat}{(string.IsNullOrWhiteSpace(details) ? string.Empty : $";details={details}")}{Environment.NewLine}"
        );
    }

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

    private void JournaliserDiagnosticChangementJeu(string etape, string? details = null)
    {
        if (!ServiceModeDiagnostic.EstActif || _chronometreDiagnosticChangementJeu is null)
        {
            return;
        }

        _ = ServiceModeDiagnostic.JournaliserLigne(
            CheminJournalDiagnosticPerformance,
            $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] +{_chronometreDiagnosticChangementJeu.Elapsed.TotalMilliseconds, 6:0} ms | {etape}{(string.IsNullOrWhiteSpace(details) ? string.Empty : $" | {details}")}{Environment.NewLine}"
        );
    }

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
}
