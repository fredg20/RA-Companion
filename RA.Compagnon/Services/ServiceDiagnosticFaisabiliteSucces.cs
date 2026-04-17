using System.Globalization;
using System.IO;
using System.Text;
using RA.Compagnon.Modeles.Api.V2.Game;
using RA.Compagnon.Modeles.Presentation;

/*
 * Journalise le détail des évaluations de faisabilité calculées pour un jeu
 * lorsque le mode diagnostic est activé.
 */
namespace RA.Compagnon.Services;

/*
 * Produit un journal lisible des scores de faisabilité afin de faciliter
 * l'analyse et l'ajustement de l'algorithme.
 */
public sealed class ServiceDiagnosticFaisabiliteSucces
{
    private static readonly string CheminJournalFaisabilite =
        ServiceModeDiagnostic.ConstruireCheminJournal("journal-faisabilite-succes.log");

    private static readonly Lock Verrou = new();
    private static string _derniereSignature = string.Empty;
    private static readonly bool SessionInitialisee =
        ServiceModeDiagnostic.ReinitialiserJournalSession(CheminJournalFaisabilite);

    /*
     * Journalise l'ensemble des succès d'un jeu avec leur évaluation calculée,
     * en évitant les doublons strictement identiques.
     */
    public static void JournaliserJeu(
        int identifiantJeu,
        string titreJeu,
        IReadOnlyCollection<GameAchievementV2> succesJeu,
        int nombreJoueursDistinctsJeu = 0
    )
    {
        _ = SessionInitialisee;

        if (!ServiceModeDiagnostic.EstActif || identifiantJeu <= 0 || succesJeu.Count == 0)
        {
            return;
        }

        string signature = ConstruireSignature(
            identifiantJeu,
            succesJeu,
            nombreJoueursDistinctsJeu
        );

        lock (Verrou)
        {
            if (string.Equals(_derniereSignature, signature, StringComparison.Ordinal))
            {
                return;
            }

            _derniereSignature = signature;
        }

        List<(GameAchievementV2 Succes, EvaluationFaisabiliteSucces Evaluation)> lignes =
        [
            .. succesJeu.Select(succes =>
                (
                    Succes: succes,
                    Evaluation: ServiceEvaluationFaisabiliteSucces.Evaluer(
                        succes,
                        identifiantJeu,
                        nombreJoueursDistinctsJeu
                    )
                )
            ),
        ];

        lignes.Sort(
            (gauche, droite) =>
            {
                int comparaisonScore = gauche.Evaluation.Score.CompareTo(droite.Evaluation.Score);
                if (comparaisonScore != 0)
                {
                    return comparaisonScore;
                }

                return string.Compare(
                    gauche.Succes.Title,
                    droite.Succes.Title,
                    StringComparison.CurrentCultureIgnoreCase
                );
            }
        );

        StringBuilder contenu = new();
        contenu
            .Append('[')
            .Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture))
            .Append("] jeu=")
            .Append(identifiantJeu.ToString(CultureInfo.InvariantCulture))
            .Append(";titreJeu=")
            .Append(Nettoyer(titreJeu))
            .Append(";joueursDistincts=")
            .Append(nombreJoueursDistinctsJeu.ToString(CultureInfo.InvariantCulture))
            .Append(";succes=")
            .Append(succesJeu.Count.ToString(CultureInfo.InvariantCulture))
            .AppendLine();

        foreach ((GameAchievementV2 succes, EvaluationFaisabiliteSucces evaluation) in lignes)
        {
            contenu
                .Append("  score=")
                .Append(evaluation.Score.ToString(CultureInfo.InvariantCulture))
                .Append(";libelle=")
                .Append(Nettoyer(evaluation.Libelle))
                .Append(";confiance=")
                .Append(Nettoyer(evaluation.Confiance))
                .Append(";succes=")
                .Append(succes.Id.ToString(CultureInfo.InvariantCulture))
                .Append(";titre=")
                .Append(Nettoyer(succes.Title))
                .Append(";points=")
                .Append(succes.Points.ToString(CultureInfo.InvariantCulture))
                .Append(";trueRatio=")
                .Append(succes.TrueRatio.ToString(CultureInfo.InvariantCulture))
                .Append(";awarded=")
                .Append(succes.NumAwarded.ToString(CultureInfo.InvariantCulture))
                .Append(";awardedHardcore=")
                .Append(succes.NumAwardedHardcore.ToString(CultureInfo.InvariantCulture))
                .Append(";type=")
                .Append(Nettoyer(succes.Type))
                .Append(";explication=")
                .Append(Nettoyer(evaluation.Explication))
                .AppendLine();
        }

        contenu.AppendLine();

        _ = ServiceModeDiagnostic.JournaliserLigne(CheminJournalFaisabilite, contenu.ToString());
    }

    /*
     * Construit une signature stable du jeu et de ses succès pour éviter
     * d'écrire plusieurs fois la même photographie.
     */
    private static string ConstruireSignature(
        int identifiantJeu,
        IReadOnlyCollection<GameAchievementV2> succesJeu,
        int nombreJoueursDistinctsJeu
    )
    {
        StringBuilder signature = new();
        signature
            .Append(identifiantJeu.ToString(CultureInfo.InvariantCulture))
            .Append(':')
            .Append(nombreJoueursDistinctsJeu.ToString(CultureInfo.InvariantCulture))
            .Append('|');

        foreach (GameAchievementV2 succes in succesJeu.OrderBy(item => item.Id))
        {
            signature
                .Append(succes.Id.ToString(CultureInfo.InvariantCulture))
                .Append(':')
                .Append(succes.NumAwarded.ToString(CultureInfo.InvariantCulture))
                .Append(':')
                .Append(succes.NumAwardedHardcore.ToString(CultureInfo.InvariantCulture))
                .Append(':')
                .Append(succes.TrueRatio.ToString(CultureInfo.InvariantCulture))
                .Append(':')
                .Append(succes.Points.ToString(CultureInfo.InvariantCulture))
                .Append(':')
                .Append(succes.Type ?? string.Empty)
                .Append('|');
        }

        return signature.ToString();
    }

    /*
     * Nettoie une valeur textuelle avant de l'inscrire dans le journal
     * de diagnostic.
     */
    private static string Nettoyer(string? valeur)
    {
        return string.IsNullOrWhiteSpace(valeur)
            ? string.Empty
            : valeur.Replace("\r", " ").Replace("\n", " ").Trim();
    }
}
