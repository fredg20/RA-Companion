using System.Globalization;
using System.IO;
using RA.Compagnon.Modeles.Api.V2.Game;
using RA.Compagnon.Modeles.Local;

/*
 * Détecte les nouveaux déblocages de succès en comparant l'état courant d'un
 * jeu avec un instantané précédent mémorisé localement.
 */
namespace RA.Compagnon.Services;

/*
 * Fournit les helpers de détection et de journalisation pour les succès
 * nouvellement obtenus pendant une session.
 */
public sealed class ServiceDetectionSuccesJeu
{
    private static readonly string CheminJournalDetectionSucces = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "RA-Compagnon",
        "journal-detection-succes.log"
    );

    /*
     * Réinitialise le journal de session dédié à la détection des succès.
     */
    public static void ReinitialiserJournalSession()
    {
        _ = ServiceModeDiagnostic.ReinitialiserJournalSession(CheminJournalDetectionSucces);
    }

    /*
     * Compare un état précédent et les succès courants pour retourner les
     * nouveaux déblocages softcore ou hardcore détectés.
     */
    public static IReadOnlyList<SuccesDebloqueDetecte> DetecterNouveauxSucces(
        int identifiantJeu,
        string titreJeu,
        IReadOnlyDictionary<int, EtatObservationSuccesLocal> etatPrecedent,
        IReadOnlyCollection<GameAchievementV2> succesCourants
    )
    {
        if (identifiantJeu <= 0 || succesCourants.Count == 0)
        {
            return [];
        }

        List<SuccesDebloqueDetecte> resultats = [];

        foreach (GameAchievementV2 succes in succesCourants)
        {
            etatPrecedent.TryGetValue(succes.Id, out EtatObservationSuccesLocal? precedent);
            string dateSoft = (succes.DateEarned ?? string.Empty).Trim();
            string dateHard = (succes.DateEarnedHardcore ?? string.Empty).Trim();

            bool nouveauHardcore =
                !string.IsNullOrWhiteSpace(dateHard)
                && !string.Equals(
                    precedent?.DateObtentionHardcore,
                    dateHard,
                    StringComparison.Ordinal
                );

            bool nouveauSoftcore =
                !nouveauHardcore
                && !string.IsNullOrWhiteSpace(dateSoft)
                && !string.Equals(precedent?.DateObtention, dateSoft, StringComparison.Ordinal);

            if (!nouveauHardcore && !nouveauSoftcore)
            {
                continue;
            }

            resultats.Add(
                new SuccesDebloqueDetecte
                {
                    IdentifiantJeu = identifiantJeu,
                    TitreJeu = titreJeu?.Trim() ?? string.Empty,
                    IdentifiantSucces = succes.Id,
                    TitreSucces = succes.Title?.Trim() ?? string.Empty,
                    Points = succes.Points,
                    Hardcore = nouveauHardcore,
                    DateObtention = nouveauHardcore ? dateHard : dateSoft,
                }
            );
        }

        return resultats;
    }

    /*
     * Capture un instantané simplifié de l'état des succès du jeu afin de
     * préparer une future comparaison.
     */
    public static Dictionary<int, EtatObservationSuccesLocal> CapturerEtat(
        IReadOnlyCollection<GameAchievementV2> succesCourants
    )
    {
        Dictionary<int, EtatObservationSuccesLocal> resultat = [];

        foreach (GameAchievementV2 succes in succesCourants)
        {
            resultat[succes.Id] = new EtatObservationSuccesLocal
            {
                IdentifiantSucces = succes.Id,
                DateObtention = (succes.DateEarned ?? string.Empty).Trim(),
                DateObtentionHardcore = (succes.DateEarnedHardcore ?? string.Empty).Trim(),
            };
        }

        return resultat;
    }

    /*
     * Journalise l'initialisation du suivi de détection pour le jeu courant.
     */
    public static void JournaliserInitialisation(
        int identifiantJeu,
        string titreJeu,
        int nombreSucces
    )
    {
        JournaliserLigne(
            $"etat=initialisation;source=session;jeu={identifiantJeu};titreJeu={Nettoyer(titreJeu)};succes={nombreSucces.ToString(CultureInfo.InvariantCulture)}"
        );
    }

    /*
     * Journalise un succès détecté comme débloqué pour faciliter le diagnostic.
     */
    public static void JournaliserDetection(SuccesDebloqueDetecte succes, string source = "session")
    {
        JournaliserLigne(
            $"etat=deblocage;source={Nettoyer(source)};jeu={succes.IdentifiantJeu};titreJeu={Nettoyer(succes.TitreJeu)};succes={succes.IdentifiantSucces};titreSucces={Nettoyer(succes.TitreSucces)};points={succes.Points.ToString(CultureInfo.InvariantCulture)};mode={(succes.Hardcore ? "hardcore" : "softcore")};date={Nettoyer(succes.DateObtention)}"
        );
    }

    /*
     * Écrit une ligne horodatée dans le journal de détection des succès.
     */
    private static void JournaliserLigne(string details)
    {
        _ = ServiceModeDiagnostic.JournaliserLigne(
            CheminJournalDetectionSucces,
            $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {details}{Environment.NewLine}"
        );
    }

    /*
     * Nettoie une valeur textuelle avant de l'injecter dans une ligne
     * de diagnostic.
     */
    private static string Nettoyer(string? valeur)
    {
        return string.IsNullOrWhiteSpace(valeur)
            ? string.Empty
            : valeur.Replace("\r", " ").Replace("\n", " ").Trim();
    }
}
