using System.Globalization;
using RA.Compagnon.Modeles.Api.V2.Game;
using RA.Compagnon.Modeles.Presentation;

namespace RA.Compagnon.Services;

public sealed class ServicePresentationJeu
{
    public static JeuAffiche Construire(DonneesJeuAffiche donneesJeu)
    {
        GameInfoAndUserProgressV2 jeu = donneesJeu.Jeu;

        return new JeuAffiche
        {
            Titre = jeu.Title,
            TempsJeu =
                jeu.UserTotalPlaytime > 0
                    ? FormaterTempsJeuTotal(jeu.UserTotalPlaytime)
                    : string.Empty,
            Statut = DeterminerStatutJeu(jeu),
            Details = ConstruireDetailsJeu(donneesJeu),
            ResumeProgression = FormaterResumeProgression(
                jeu.NumAwardedToUser,
                jeu.NumAchievements
            ),
            PourcentageTexte = NormaliserPourcentage(jeu.UserCompletion),
            PourcentageValeur = ExtrairePourcentage(jeu.UserCompletion),
        };
    }

    private static string FormaterResumeProgression(int nbSuccesDebloques, int nbSuccesTotal)
    {
        return $"{nbSuccesDebloques} / {nbSuccesTotal} succès";
    }

    public static string ConstruireResumePoints(IReadOnlyCollection<GameAchievementV2> succes)
    {
        if (succes.Count == 0)
        {
            return string.Empty;
        }

        int totalPoints = succes.Sum(item => Math.Max(0, item.Points));

        if (totalPoints <= 0)
        {
            return string.Empty;
        }

        int pointsHardcore = succes
            .Where(item => !string.IsNullOrWhiteSpace(item.DateEarnedHardcore))
            .Sum(item => Math.Max(0, item.Points));
        int pointsSoftcore = succes
            .Where(item =>
                string.IsNullOrWhiteSpace(item.DateEarnedHardcore)
                && !string.IsNullOrWhiteSpace(item.DateEarned)
            )
            .Sum(item => Math.Max(0, item.Points));

        return string.Create(
            CultureInfo.CurrentCulture,
            $"{pointsSoftcore} / {totalPoints} en softcore - {pointsHardcore} / {totalPoints} en hardcore"
        );
    }

    private static string ConstruireDetailsJeu(DonneesJeuAffiche donneesJeu)
    {
        GameInfoAndUserProgressV2 jeu = donneesJeu.Jeu;
        List<string> segments = [];
        string resumePoints = ConstruireResumePoints(jeu.Achievements.Values);

        if (!string.IsNullOrWhiteSpace(resumePoints))
        {
            segments.Add(resumePoints);
        }

        if (
            donneesJeu.CommunauteAffichee is not null
            && !string.IsNullOrWhiteSpace(donneesJeu.CommunauteAffichee.Resume)
        )
        {
            segments.Add(donneesJeu.CommunauteAffichee.Resume);
        }

        return string.Join(" • ", segments);
    }

    private static string DeterminerStatutJeu(GameInfoAndUserProgressV2 jeu)
    {
        string etatApi = jeu.HighestAwardKind.Trim().ToLowerInvariant();

        string etatDirect = etatApi switch
        {
            "mastered" => "Jeu maîtrisé",
            "completed" => "Jeu complété",
            "beaten" => "Jeu battu",
            "beaten-hardcore" => "Jeu battu en hardcore",
            "beaten-softcore" => "Jeu battu en softcore",
            _ => string.Empty,
        };

        if (!string.IsNullOrWhiteSpace(etatDirect))
        {
            return etatDirect;
        }

        if (jeu.NumAchievements > 0 && jeu.NumAwardedToUserHardcore == jeu.NumAchievements)
        {
            return "Jeu maîtrisé";
        }

        if (
            jeu.NumAchievements > 0
            && jeu.NumAwardedToUser == jeu.NumAchievements
            && jeu.NumAwardedToUserHardcore < jeu.NumAchievements
        )
        {
            return "Jeu complété";
        }

        return string.Empty;
    }

    private static string FormaterTempsJeuTotal(int totalSecondes)
    {
        if (totalSecondes <= 0)
        {
            return "0 min";
        }

        int totalMinutes = totalSecondes / 60;
        int jours = totalMinutes / (24 * 60);
        int heures = (totalMinutes % (24 * 60)) / 60;
        int minutes = totalMinutes % 60;
        List<string> segments = [];

        if (jours > 0)
        {
            segments.Add(jours == 1 ? "1 j" : $"{jours} j");
        }

        if (heures > 0)
        {
            segments.Add(heures == 1 ? "1 h" : $"{heures} h");
        }

        if (minutes > 0 || segments.Count == 0)
        {
            segments.Add(minutes == 1 ? "1 min" : $"{minutes} min");
        }

        return string.Join(" ", segments);
    }

    private static double ExtrairePourcentage(string pourcentageApi)
    {
        if (string.IsNullOrWhiteSpace(pourcentageApi))
        {
            return 0;
        }

        string valeurNormalisee = pourcentageApi.Replace("%", string.Empty).Trim();

        if (
            double.TryParse(
                valeurNormalisee,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out double pourcentage
            )
        )
        {
            return Math.Clamp(pourcentage, 0, 100);
        }

        if (
            double.TryParse(
                valeurNormalisee,
                NumberStyles.Float,
                CultureInfo.CurrentCulture,
                out pourcentage
            )
        )
        {
            return Math.Clamp(pourcentage, 0, 100);
        }

        return 0;
    }

    private static string NormaliserPourcentage(string pourcentageApi)
    {
        double valeur = ExtrairePourcentage(pourcentageApi);
        return $"{valeur:0.##} % complété";
    }
}
