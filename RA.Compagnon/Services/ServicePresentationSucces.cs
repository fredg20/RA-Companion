using System.Globalization;
using RA.Compagnon.Modeles.Api.V2.Game;
using RA.Compagnon.Modeles.Presentation;

/*
 * Transforme les succès bruts de l'API en modèles d'affichage adaptés à la
 * carte principale et à la grille des succès.
 */
namespace RA.Compagnon.Services;

/*
 * Centralise la présentation visuelle des succès, de leurs badges et de leurs
 * libellés complémentaires pour l'interface.
 */
public sealed class ServicePresentationSucces
{
    /*
     * Construit le modèle simplifié utilisé par la grille des succès.
     */
    public static SuccesGrilleAffiche ConstruirePourGrille(GameAchievementV2 succes)
    {
        bool estDebloque = EstDebloque(succes);

        return new SuccesGrilleAffiche
        {
            IdentifiantSucces = succes.Id,
            Titre = succes.Title,
            ToolTip = ConstruireToolTipGrilleSucces(succes),
            UrlBadge = ConstruireUrlBadgeDepuisNom(succes.BadgeName, !estDebloque),
            EstDebloque = estDebloque,
            EstHardcore = !string.IsNullOrWhiteSpace(succes.DateEarnedHardcore),
        };
    }

    /*
     * Construit le modèle complet utilisé par la carte du succès mis en avant.
     */
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

    /*
     * Traduit le type technique d'un succès en libellé lisible en français.
     */
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

    /*
     * Construit la ligne de détails décrivant les points, le type et les
     * rétropoints du succès.
     */
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

    /*
     * Met en forme la faisabilité calculée pour affichage dans la carte
     * principale du succès.
     */
    private static string ConstruireDetailsFaisabiliteSucces(EvaluationFaisabiliteSucces evaluation)
    {
        if (string.IsNullOrWhiteSpace(evaluation.Libelle) || evaluation.NombreJoueursDistincts <= 0)
        {
            return string.Empty;
        }

        double pourcentage =
            evaluation.NombreJoueursDistincts > 0
                ? (double)evaluation.NombreJoueursDebloques
                    / evaluation.NombreJoueursDistincts
                    * 100d
                : 0d;
        return $"{evaluation.Libelle} ({pourcentage:0.#} %)";
    }

    /*
     * Détermine si un succès doit être considéré comme débloqué à partir des
     * dates softcore et hardcore présentes dans les données.
     */
    private static bool EstDebloque(GameAchievementV2 succes)
    {
        return !string.IsNullOrWhiteSpace(succes.DateEarned)
            || !string.IsNullOrWhiteSpace(succes.DateEarnedHardcore);
    }

    /*
     * Construit l'infobulle affichée au survol d'un badge dans la grille.
     */
    private static string ConstruireToolTipGrilleSucces(GameAchievementV2 succes)
    {
        string titre = succes.Title?.Trim() ?? string.Empty;
        string mode = DeterminerModeObtentionSucces(succes);

        if (string.IsNullOrWhiteSpace(mode))
        {
            return titre;
        }

        return string.IsNullOrWhiteSpace(titre) ? mode : $"{titre}{Environment.NewLine}{mode}";
    }

    /*
     * Déduit le mode d'obtention visible d'un succès selon ses dates de
     * déblocage disponibles.
     */
    private static string DeterminerModeObtentionSucces(GameAchievementV2 succes)
    {
        if (!string.IsNullOrWhiteSpace(succes.DateEarnedHardcore))
        {
            return "Hardcore";
        }

        if (!string.IsNullOrWhiteSpace(succes.DateEarned))
        {
            return "Softcore";
        }

        return string.Empty;
    }

    /*
     * Transforme un nom de badge ou un chemin relatif en URL complète, avec
     * prise en charge de la variante verrouillée si nécessaire.
     */
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
