using System.IO;

namespace RA.Compagnon;

/// <summary>
/// Regroupe le diagnostic temporel du changement de jeu.
/// </summary>
public partial class MainWindow
{
    private static readonly bool ActiverJournalDiagnosticChangementJeu = false;
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
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CheminJournalDiagnosticPerformance)!);
            File.WriteAllText(
                CheminJournalDiagnosticPerformance,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] nouvelle_session{Environment.NewLine}"
            );
        }
        catch
        {
            // Ce journal reste purement diagnostique.
        }
    }

    public static void ReinitialiserJournalDiagnosticListeSucces()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CheminJournalDiagnosticListeSucces)!);
            File.WriteAllText(
                CheminJournalDiagnosticListeSucces,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] nouvelle_session{Environment.NewLine}"
            );
        }
        catch
        {
            // Ce journal reste purement diagnostique.
        }
    }

    private void JournaliserDiagnosticListeSucces(string evenement, string? details = null)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CheminJournalDiagnosticListeSucces)!);
            double offset = ConteneurGrilleTousSuccesJeuEnCours?.VerticalOffset ?? 0;
            double hauteurVisible = ConteneurGrilleTousSuccesJeuEnCours?.ViewportHeight ?? 0;
            double hauteurDefilable = ConteneurGrilleTousSuccesJeuEnCours?.ScrollableHeight ?? 0;
            int nbBadges = GrilleTousSuccesJeuEnCours?.Children.Count ?? 0;
            string etat =
                $"offset={offset:0.##};amplitude={_amplitudeAnimationGrilleSucces:0.##};sens={(_animationGrilleSuccesVersBas ? "bas" : "haut")};interaction={_interactionListeSuccesActive};survolBadge={_survolBadgeGrilleSuccesActif};visible={hauteurVisible:0.##};scrollable={hauteurDefilable:0.##};badges={nbBadges}";

            File.AppendAllText(
                CheminJournalDiagnosticListeSucces,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] evenement={evenement};etat={etat}{(string.IsNullOrWhiteSpace(details) ? string.Empty : $";details={details}")}{Environment.NewLine}"
            );
        }
        catch
        {
            // Ce journal reste purement diagnostique.
        }
    }

    private void DemarrerDiagnosticChangementJeu(string signature, string? details = null)
    {
        if (!ActiverJournalDiagnosticChangementJeu || string.IsNullOrWhiteSpace(signature))
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
        if (!ActiverJournalDiagnosticChangementJeu || _chronometreDiagnosticChangementJeu is null)
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CheminJournalDiagnosticPerformance)!);
            File.AppendAllText(
                CheminJournalDiagnosticPerformance,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] +{_chronometreDiagnosticChangementJeu.Elapsed.TotalMilliseconds,6:0} ms | {etape}{(string.IsNullOrWhiteSpace(details) ? string.Empty : $" | {details}")}{Environment.NewLine}"
            );
        }
        catch
        {
            // Ignore un échec de diagnostic pour ne pas gêner l'application.
        }
    }

    private void TerminerDiagnosticChangementJeu(string etape, string? details = null)
    {
        if (!ActiverJournalDiagnosticChangementJeu)
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
