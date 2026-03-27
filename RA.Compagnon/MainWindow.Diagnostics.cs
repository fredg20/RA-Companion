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
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] +{_chronometreDiagnosticChangementJeu.Elapsed.TotalMilliseconds, 6:0} ms | {etape}{(string.IsNullOrWhiteSpace(details) ? string.Empty : $" | {details}")}{Environment.NewLine}"
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
