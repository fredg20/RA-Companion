using System.Globalization;
using System.IO;

/*
 * Gère l'activation du mode diagnostic local et les écritures de journaux
 * associées à ce mode.
 */
namespace RA.Compagnon.Services;

/*
 * Expose les helpers de bas niveau qui permettent d'activer la journalisation
 * détaillée via un drapeau local ou une variable d'environnement.
 */
public static class ServiceModeDiagnostic
{
    private static readonly Lazy<bool> ModeDiagnosticActif = new(DeterminerSiModeDiagnosticActif);

    public static bool EstActif => ModeDiagnosticActif.Value;

    public static string CheminDrapeauActivation =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "RA-Compagnon",
            "diagnostic.enabled"
        );

    /*
     * Réinitialise le journal de session lorsque le mode diagnostic est actif.
     */
    public static bool ReinitialiserJournalSession(string cheminJournal)
    {
        if (!EstActif)
        {
            return false;
        }

        try
        {
            string? repertoire = Path.GetDirectoryName(cheminJournal);

            if (!string.IsNullOrWhiteSpace(repertoire))
            {
                Directory.CreateDirectory(repertoire);
            }

            File.WriteAllText(
                cheminJournal,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] nouvelle_session{Environment.NewLine}"
            );
            return true;
        }
        catch
        {
            return false;
        }
    }

    /*
     * Ajoute une ligne au journal de diagnostic si ce mode est activé.
     */
    public static bool JournaliserLigne(string cheminJournal, string ligne)
    {
        if (!EstActif)
        {
            return false;
        }

        try
        {
            string? repertoire = Path.GetDirectoryName(cheminJournal);

            if (!string.IsNullOrWhiteSpace(repertoire))
            {
                Directory.CreateDirectory(repertoire);
            }

            File.AppendAllText(cheminJournal, ligne);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /*
     * Détermine si le mode diagnostic doit être activé à partir de la variable
     * d'environnement ou du fichier drapeau local.
     */
    private static bool DeterminerSiModeDiagnosticActif()
    {
        string? variableEnvironnement = Environment.GetEnvironmentVariable(
            "RA_COMPAGNON_DIAGNOSTIC"
        );

        if (!string.IsNullOrWhiteSpace(variableEnvironnement))
        {
            string valeur = variableEnvironnement.Trim();

            if (
                string.Equals(valeur, "1", StringComparison.OrdinalIgnoreCase)
                || string.Equals(valeur, "true", StringComparison.OrdinalIgnoreCase)
                || string.Equals(valeur, "yes", StringComparison.OrdinalIgnoreCase)
                || string.Equals(valeur, "on", StringComparison.OrdinalIgnoreCase)
            )
            {
                return true;
            }

            if (
                string.Equals(valeur, "0", StringComparison.OrdinalIgnoreCase)
                || string.Equals(valeur, "false", StringComparison.OrdinalIgnoreCase)
                || string.Equals(valeur, "no", StringComparison.OrdinalIgnoreCase)
                || string.Equals(valeur, "off", StringComparison.OrdinalIgnoreCase)
            )
            {
                return false;
            }
        }

        return File.Exists(CheminDrapeauActivation);
    }
}
