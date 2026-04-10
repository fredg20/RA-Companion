using System.Globalization;
using RA.Compagnon.Modeles.Api.V2.Game;
using RA.Compagnon.Modeles.Presentation;

namespace RA.Compagnon.Services;

/// <summary>
/// Transforme un succès en contenu prêt à afficher.
/// </summary>
public sealed class ServicePresentationSucces
{
    public static SuccesGrilleAffiche ConstruirePourGrille(GameAchievementV2 succes)
    {
        bool estDebloque = EstDebloque(succes);

        return new SuccesGrilleAffiche
        {
            IdentifiantSucces = succes.Id,
            Titre = succes.Title,
            UrlBadge = ConstruireUrlBadgeDepuisNom(succes.BadgeName, !estDebloque),
            EstDebloque = estDebloque,
        };
    }

    public static SuccesAffiche Construire(
        GameAchievementV2 succes,
        int identifiantJeu,
        int nombreJoueursDistinctsJeu = 0
    )
    {
        bool estDebloque = EstDebloque(succes);
        EvaluationFaisabiliteSucces evaluation = ServiceEvaluationFaisabiliteSucces.Evaluer(
            succes,
            identifiantJeu,
            nombreJoueursDistinctsJeu
        );

        return new SuccesAffiche
        {
            Titre = succes.Title,
            Description = string.IsNullOrWhiteSpace(succes.Description)
                ? "Aucune description disponible."
                : succes.Description,
            DetailsPoints = ConstruireDetailsPointsSucces(succes),
            DetailsFaisabilite = ConstruireDetailsFaisabiliteSucces(evaluation),
            ScoreFaisabilite = evaluation.Score,
            LibelleFaisabilite = evaluation.Libelle,
            ConfianceFaisabilite = evaluation.Confiance,
            ExplicationFaisabilite = evaluation.Explication,
            UrlBadge = ConstruireUrlBadgeDepuisNom(succes.BadgeName, !estDebloque),
            EstDebloque = estDebloque,
        };
    }

    private static string TraduireTypeSucces(string type)
    {
        return (type ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "progression" => "Succès de progression",
            "win_condition" => "Succès de victoire",
            "missable" => "Succès manquable",
            _ => string.Empty,
        };
    }

    private static string ConstruireDetailsPointsSucces(GameAchievementV2 succes)
    {
        List<string> segments = [];
        string typeSucces = TraduireTypeSucces(succes.Type);

        if (!string.IsNullOrWhiteSpace(typeSucces))
        {
            segments.Add(typeSucces);
        }

        if (succes.Points > 0)
        {
            segments.Add($"{succes.Points.ToString(CultureInfo.CurrentCulture)} points");
        }

        if (succes.TrueRatio > 0)
        {
            segments.Add($"{succes.TrueRatio.ToString(CultureInfo.CurrentCulture)} rétropoints");
        }

        return string.Join(" • ", segments);
    }

    private static string ConstruireDetailsFaisabiliteSucces(EvaluationFaisabiliteSucces evaluation)
    {
        if (
            string.IsNullOrWhiteSpace(evaluation.Libelle)
            || evaluation.NombreJoueursDistincts <= 0
        )
        {
            return string.Empty;
        }

        double pourcentage =
            evaluation.NombreJoueursDistincts > 0
                ? (double)evaluation.NombreJoueursDebloques / evaluation.NombreJoueursDistincts * 100d
                : 0d;
        return $"{evaluation.Libelle} ({pourcentage:0.#} %)";
    }

    private static bool EstDebloque(GameAchievementV2 succes)
    {
        return !string.IsNullOrWhiteSpace(succes.DateEarned)
            || !string.IsNullOrWhiteSpace(succes.DateEarnedHardcore);
    }

    private static string ConstruireUrlBadgeDepuisNom(string nomBadge, bool versionVerrouillee)
    {
        if (string.IsNullOrWhiteSpace(nomBadge))
        {
            return string.Empty;
        }

        string badgeNettoye = nomBadge.Trim();

        if (badgeNettoye.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            return badgeNettoye;
        }

        if (badgeNettoye.StartsWith('/'))
        {
            return badgeNettoye.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? badgeNettoye
                : $"https://retroachievements.org{badgeNettoye}";
        }

        string suffixe = versionVerrouillee ? "_lock" : string.Empty;
        return $"https://i.retroachievements.org/Badge/{badgeNettoye}{suffixe}.png";
    }
}
