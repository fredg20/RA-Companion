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
    private const long TailleMaxJournalOctets = 1_048_576;
    private const long TailleMaxJournalDemarrageOctets = 262_144;
    private const long TailleMaxJournalVisuelsJeuOctets = 524_288;
    private const int NombreArchivesJournaux = 3;
    private static readonly Lazy<bool> ModeDiagnosticActif = new(DeterminerSiModeDiagnosticActif);
    private static readonly HashSet<string> NomsJournauxActifs =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "journal-configuration-locale.log",
            "journal-demarrage.log",
            "journal-detection-succes.log",
            "journal-faisabilite-succes.log",
            "journal-liste-succes.log",
            "journal-performance-jeu.log",
            "journal-rejouer.log",
            "journal-resolution-locale.log",
            "journal-richpresence.log",
            "journal-sonde-locale.log",
            "journal-surveillance-succes-locaux.log",
            "journal-test-succes-debug.log",
            "journal-visuels-jeu.log",
        };

    public static bool EstActif => ModeDiagnosticActif.Value;

    public static string DossierApplication =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "RA-Compagnon"
        );

    public static string DossierJournaux => Path.Combine(DossierApplication, "Logs");

    public static string CheminDrapeauActivation =>
        Path.Combine(DossierApplication, "diagnostic.enabled");

    /*
     * Construit le chemin absolu d'un journal de Compagnon dans le dossier
     * centralisé réservé aux fichiers log.
     */
    public static string ConstruireCheminJournal(string nomFichier)
    {
        return Path.Combine(DossierJournaux, nomFichier);
    }

    /*
     * Déplace les anciens journaux stockés à la racine du dossier applicatif
     * vers le sous-dossier centralisé réservé aux logs.
     */
    public static void MigrerJournauxExistants()
    {
        try
        {
            if (!Directory.Exists(DossierApplication))
            {
                return;
            }

            Directory.CreateDirectory(DossierJournaux);
            FusionnerAnciensJournauxListeSucces();
            FusionnerAnciensJournauxPerformanceJeu();
            SupprimerJournauxObsoletes();
            AppliquerRotationAuxJournauxExistants();

            foreach (string cheminSource in Directory.GetFiles(DossierApplication, "journal-*.log"))
            {
                string nomFichier = Path.GetFileName(cheminSource);
                string cheminDestination = ConstruireCheminJournal(nomFichier);

                if (string.Equals(cheminSource, cheminDestination, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (File.Exists(cheminDestination))
                {
                    File.Delete(cheminSource);
                    continue;
                }

                File.Move(cheminSource, cheminDestination);
            }
        }
        catch
        {
            // La migration ne doit jamais bloquer le démarrage.
        }
    }

    /*
     * Supprime les anciens journaux qui ne correspondent plus à aucun fichier
     * actif du projet afin de garder le dossier Logs lisible.
     */
    private static void SupprimerJournauxObsoletes()
    {
        if (!Directory.Exists(DossierJournaux))
        {
            return;
        }

        foreach (string cheminJournal in Directory.GetFiles(DossierJournaux, "journal-*.log"))
        {
            string nomFichier = Path.GetFileName(cheminJournal);

            if (NomsJournauxActifs.Contains(nomFichier))
            {
                continue;
            }

            File.Delete(cheminJournal);
        }
    }

    /*
     * Applique la politique de rotation aux journaux déjà présents afin de
     * remettre immédiatement de l'ordre dans le dossier Logs.
     */
    private static void AppliquerRotationAuxJournauxExistants()
    {
        if (!Directory.Exists(DossierJournaux))
        {
            return;
        }

        foreach (string cheminJournal in Directory.GetFiles(DossierJournaux, "journal-*.log"))
        {
            FaireRotationSiNecessaire(cheminJournal);
        }
    }

    /*
     * Réunit l'ancien journal de dimensions de la liste des succès avec le
     * journal principal désormais fusionné.
     */
    private static void FusionnerAnciensJournauxListeSucces()
    {
        string ancienJournalRacine = Path.Combine(
            DossierApplication,
            "journal-dimensions-liste-succes.log"
        );
        string ancienJournalCentralise = ConstruireCheminJournal("journal-dimensions-liste-succes.log");
        string journalListe = ConstruireCheminJournal("journal-liste-succes.log");

        FusionnerJournalSiPresent(ancienJournalRacine, journalListe);
        FusionnerJournalSiPresent(ancienJournalCentralise, journalListe);
    }

    /*
     * Réunit l'ancien journal d'affichage du jeu avec le journal principal de
     * performance désormais fusionné.
     */
    private static void FusionnerAnciensJournauxPerformanceJeu()
    {
        string ancienJournalRacine = Path.Combine(DossierApplication, "journal-affichage-jeu.log");
        string ancienJournalCentralise = ConstruireCheminJournal("journal-affichage-jeu.log");
        string journalPerformance = ConstruireCheminJournal("journal-performance-jeu.log");

        FusionnerJournalSiPresent(ancienJournalRacine, journalPerformance);
        FusionnerJournalSiPresent(ancienJournalCentralise, journalPerformance);
    }

    /*
     * Ajoute le contenu d'un ancien journal à un journal cible puis supprime
     * la source pour éviter de conserver un doublon historique.
     */
    private static void FusionnerJournalSiPresent(string cheminSource, string cheminDestination)
    {
        if (!File.Exists(cheminSource))
        {
            return;
        }

        if (string.Equals(cheminSource, cheminDestination, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        Directory.CreateDirectory(DossierJournaux);
        File.AppendAllText(cheminDestination, File.ReadAllText(cheminSource));
        File.Delete(cheminSource);
    }

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

            FaireRotationSiNecessaire(cheminJournal);
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

            FaireRotationSiNecessaire(cheminJournal, longueurAjout: ligne.Length * sizeof(char));
            File.AppendAllText(cheminJournal, ligne);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /*
     * Effectue une rotation simple d'un journal lorsqu'il devient trop volumineux.
     */
    private static void FaireRotationSiNecessaire(string cheminJournal, long longueurAjout = 0)
    {
        if (!File.Exists(cheminJournal))
        {
            return;
        }

        FileInfo informations = new(cheminJournal);
        long tailleMax = DeterminerTailleMaxJournal(cheminJournal);

        if (informations.Length + longueurAjout < tailleMax)
        {
            return;
        }

        for (int index = NombreArchivesJournaux; index >= 1; index--)
        {
            string cheminArchive = $"{cheminJournal}.{index}";

            if (index == NombreArchivesJournaux)
            {
                if (File.Exists(cheminArchive))
                {
                    File.Delete(cheminArchive);
                }

                continue;
            }

            string cheminArchiveSuivante = $"{cheminJournal}.{index + 1}";

            if (File.Exists(cheminArchive))
            {
                File.Move(cheminArchive, cheminArchiveSuivante, overwrite: true);
            }
        }

        File.Move(cheminJournal, $"{cheminJournal}.1", overwrite: true);
    }

    /*
     * Détermine la taille maximale autorisée selon le type de journal afin
     * d'être plus strict avec les fichiers qui grossissent trop vite.
     */
    private static long DeterminerTailleMaxJournal(string cheminJournal)
    {
        string nomFichier = Path.GetFileName(cheminJournal);

        if (string.Equals(nomFichier, "journal-demarrage.log", StringComparison.OrdinalIgnoreCase))
        {
            return TailleMaxJournalDemarrageOctets;
        }

        if (
            string.Equals(
                nomFichier,
                "journal-visuels-jeu.log",
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            return TailleMaxJournalVisuelsJeuOctets;
        }

        return TailleMaxJournalOctets;
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
