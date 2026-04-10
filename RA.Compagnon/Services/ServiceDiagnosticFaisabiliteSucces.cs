using System.Globalization;
using System.IO;
using System.Text;
using RA.Compagnon.Modeles.Api.V2.Game;
using RA.Compagnon.Modeles.Presentation;

namespace RA.Compagnon.Services;

/// <summary>
/// Exporte un journal lisible pour calibrer la faisabilité des succès sur des cas réels.
/// </summary>
public sealed class ServiceDiagnosticFaisabiliteSucces
{
    private static readonly string CheminJournalFaisabilite = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "RA-Compagnon",
        "journal-faisabilite-succes.log"
    );

    private static readonly Lock Verrou = new();
    private static string _derniereSignature = string.Empty;
    private static readonly bool SessionInitialisee =
        ServiceModeDiagnostic.ReinitialiserJournalSession(CheminJournalFaisabilite);

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

    private static string Nettoyer(string? valeur)
    {
        return string.IsNullOrWhiteSpace(valeur)
            ? string.Empty
            : valeur.Replace("\r", " ").Replace("\n", " ").Trim();
    }
}
