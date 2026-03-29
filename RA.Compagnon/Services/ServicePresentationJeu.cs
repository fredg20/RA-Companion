using System.Globalization;
using RA.Compagnon.Modeles.Api.V2.Game;
using RA.Compagnon.Modeles.Presentation;

namespace RA.Compagnon.Services;

/// <summary>
/// Transforme les données d'un jeu en contenu prêt à afficher.
/// </summary>
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
            ResumeProgression = $"{jeu.NumAwardedToUser} / {jeu.NumAchievements}",
            PourcentageTexte = NormaliserPourcentage(jeu.UserCompletion),
            PourcentageValeur = ExtrairePourcentage(jeu.UserCompletion),
        };
    }

    private static int CalculerTotalPointsJeu(GameInfoAndUserProgressV2 jeu)
    {
        return jeu.Achievements.Values.Sum(succes => Math.Max(0, succes.Points));
    }

    private static string ConstruireDetailsJeu(DonneesJeuAffiche donneesJeu)
    {
        GameInfoAndUserProgressV2 jeu = donneesJeu.Jeu;
        List<string> segments = [];
        int totalPoints = CalculerTotalPointsJeu(jeu);

        if (totalPoints > 0)
        {
            segments.Add($"{totalPoints.ToString(CultureInfo.CurrentCulture)} points totaux");
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

        return "Progression en cours";
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
