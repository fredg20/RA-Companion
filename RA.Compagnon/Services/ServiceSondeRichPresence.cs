using System.Globalization;
using System.IO;
using RA.Compagnon.Modeles.Presentation;

/*
 * Interprète les données de profil et de résumé utilisateur pour produire un
 * état de Rich Presence lisible par l'interface.
 */
namespace RA.Compagnon.Services;

/*
 * Fournit la sonde de Rich Presence utilisée pour déterminer si le joueur
 * est en jeu, sur son dernier jeu ou inactif.
 */
public sealed class ServiceSondeRichPresence
{
    private static readonly TimeSpan DelaiPresenceActive = TimeSpan.FromMinutes(10);
    private static readonly string CheminJournalRichPresence = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "RA-Compagnon",
        "journal-richpresence.log"
    );

    /*
     * Réinitialise le journal de session du Rich Presence.
     */
    public static void ReinitialiserJournalSession()
    {
        _ = ServiceModeDiagnostic.ReinitialiserJournalSession(CheminJournalRichPresence);
    }

    /*
     * Analyse les données de compte pour construire un état de présence
     * riche utilisable par l'interface.
     */
    public static EtatRichPresence Sonder(DonneesCompteUtilisateur donnees, bool journaliser = true)
    {
        string messageRichPresenceResume = donnees.Resume?.RichPresenceMsg?.Trim() ?? string.Empty;
        string messageRichPresenceProfil = donnees.Profil?.RichPresenceMsg?.Trim() ?? string.Empty;
        string sourceRichPresence =
            !string.IsNullOrWhiteSpace(messageRichPresenceResume) ? "resume"
            : !string.IsNullOrWhiteSpace(messageRichPresenceProfil) ? "profil"
            : string.Empty;
        string messageRichPresence = !string.IsNullOrWhiteSpace(messageRichPresenceResume)
            ? messageRichPresenceResume
            : messageRichPresenceProfil;
        string statutSite = donnees.Resume?.Status?.Trim() ?? string.Empty;
        int identifiantDernierJeu = DeterminerDernierIdentifiantJeu(donnees);
        string datePresenceBrute = donnees.Resume?.RichPresenceMsgDate?.Trim() ?? string.Empty;
        bool presenceDateValide = EssayerParserDatePresence(
            datePresenceBrute,
            out DateTimeOffset datePresenceUtc
        );
        bool presenceManifestementAncienne =
            presenceDateValide && DateTimeOffset.UtcNow - datePresenceUtc > DelaiPresenceActive;

        bool estEnJeu =
            !string.IsNullOrWhiteSpace(messageRichPresenceResume)
            && !presenceManifestementAncienne
            && !EstStatutHorsLigne(statutSite);

        string statutAffiche;
        string sousStatutAffiche;

        if (estEnJeu)
        {
            statutAffiche = "En jeu";
            sousStatutAffiche = messageRichPresence;
        }
        else if (identifiantDernierJeu > 0)
        {
            statutAffiche = "Dernier jeu";
            sousStatutAffiche = string.Empty;
        }
        else
        {
            statutAffiche = "Inactif";
            sousStatutAffiche = string.Empty;
        }

        EtatRichPresence etat = new()
        {
            SourceRichPresence = sourceRichPresence,
            MessageRichPresence = messageRichPresence,
            StatutSite = statutSite,
            StatutAffiche = statutAffiche,
            SousStatutAffiche = sousStatutAffiche,
            IdentifiantDernierJeu = identifiantDernierJeu,
            DatePresenceBrute = datePresenceBrute,
            DatePresenceUtc = presenceDateValide ? datePresenceUtc : null,
            PresenceDateValide = presenceDateValide,
            PresenceManifestementAncienne = presenceManifestementAncienne,
            EstEnJeu = estEnJeu,
        };

        if (journaliser)
        {
            JournaliserSonde(etat);
        }

        return etat;
    }

    /*
     * Détermine l'identifiant du dernier jeu connu à partir des différentes
     * sources utilisateur disponibles.
     */
    private static int DeterminerDernierIdentifiantJeu(DonneesCompteUtilisateur donnees)
    {
        if (donnees.Resume?.LastGameId > 0)
        {
            return donnees.Resume.LastGameId;
        }

        if (donnees.Profil?.LastGameId > 0)
        {
            return donnees.Profil.LastGameId;
        }

        if (donnees.Resume?.LastGame?.IdentifiantJeu > 0)
        {
            return donnees.Resume.LastGame.IdentifiantJeu;
        }

        return 0;
    }

    /*
     * Tente de parser la date brute de Rich Presence en UTC.
     */
    private static bool EssayerParserDatePresence(
        string datePresence,
        out DateTimeOffset dateParsee
    )
    {
        if (string.IsNullOrWhiteSpace(datePresence))
        {
            dateParsee = default;
            return false;
        }

        if (
            DateTimeOffset.TryParseExact(
                datePresence,
                "yyyy-MM-dd HH:mm:ss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AllowWhiteSpaces,
                out dateParsee
            )
        )
        {
            return true;
        }

        return DateTimeOffset.TryParse(
            datePresence,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AllowWhiteSpaces,
            out dateParsee
        );
    }

    /*
     * Indique si le statut du site doit être considéré comme hors ligne.
     */
    private static bool EstStatutHorsLigne(string statutSite)
    {
        return string.Equals(statutSite, "Offline", StringComparison.OrdinalIgnoreCase);
    }

    /*
     * Journalise le résultat détaillé d'une sonde de Rich Presence.
     */
    private static void JournaliserSonde(EtatRichPresence etat)
    {
        _ = ServiceModeDiagnostic.JournaliserLigne(
            CheminJournalRichPresence,
            string.Create(
                CultureInfo.InvariantCulture,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] source={NettoyerPourJournal(etat.SourceRichPresence)};statutSite={NettoyerPourJournal(etat.StatutSite)};message={NettoyerPourJournal(etat.MessageRichPresence)};dateBrute={NettoyerPourJournal(etat.DatePresenceBrute)};dateValide={etat.PresenceDateValide};ancienne={etat.PresenceManifestementAncienne};dernierJeu={etat.IdentifiantDernierJeu};enJeu={etat.EstEnJeu};statutAffiche={NettoyerPourJournal(etat.StatutAffiche)};sousStatut={NettoyerPourJournal(etat.SousStatutAffiche)}{Environment.NewLine}"
            )
        );
    }

    /*
     * Nettoie une valeur avant son écriture dans le journal de diagnostic.
     */
    private static string NettoyerPourJournal(string? valeur)
    {
        return string.IsNullOrWhiteSpace(valeur)
            ? string.Empty
            : valeur.Replace("\r", " ").Replace("\n", " ").Trim();
    }
}