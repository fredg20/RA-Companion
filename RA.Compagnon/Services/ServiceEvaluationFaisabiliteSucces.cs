using RA.Compagnon.Modeles.Api.V2.Game;
using RA.Compagnon.Modeles.Presentation;

/*
 * Évalue une faisabilité simplifiée d'un succès à partir de sa rareté observée,
 * de son mode hardcore et de quelques indices de difficulté.
 */
namespace RA.Compagnon.Services;

/*
 * Produit un score, un libellé et un niveau de confiance pour aider
 * l'interface à qualifier rapidement la difficulté pratique d'un succès.
 */
public sealed class ServiceEvaluationFaisabiliteSucces
{
    private const double PoidsTauxDeblocage = 0.72d;
    private const double PoidsTauxHardcore = 0.18d;
    private const double PoidsScoreStructurel = 0.10d;

    /*
     * Calcule l'évaluation de faisabilité en utilisant le nombre de joueurs
     * distincts du jeu, la rareté observée et les métadonnées du succès.
     */
    public static EvaluationFaisabiliteSucces Evaluer(
        GameAchievementV2 succes,
        int identifiantJeu,
        int nombreJoueursDistinctsJeu = 0,
        IReadOnlyList<GameAchievementV2>? succesJeu = null,
        AnalyseZoneRichPresence? analyseZoneCourante = null
    )
    {
        if (identifiantJeu <= 0)
        {
            return new EvaluationFaisabiliteSucces();
        }

        int joueursDistincts = nombreJoueursDistinctsJeu;

        if (joueursDistincts <= 0)
        {
            return new EvaluationFaisabiliteSucces();
        }

        double tauxDeblocage = (double)Math.Max(0, succes.NumAwarded) / joueursDistincts;
        double tauxHardcore = (double)Math.Max(0, succes.NumAwardedHardcore) / joueursDistincts;
        int scoreTauxDeblocage = ConvertirTauxEnScore(tauxDeblocage);
        int scoreHardcore = ConvertirTauxHardcoreEnScore(tauxDeblocage, tauxHardcore);
        int scoreStructurel = EvaluerScoreStructurel(succes);
        int ajustement = CalculerAjustementDifficulte(succes);
        int ajustementContexte = CalculerAjustementContexte(succes, succesJeu, analyseZoneCourante);
        int scoreGlobal = (int)
            Math.Round(
                Math.Clamp(
                    scoreTauxDeblocage * PoidsTauxDeblocage
                        + scoreHardcore * PoidsTauxHardcore
                        + scoreStructurel * PoidsScoreStructurel
                        + ajustement,
                    0d,
                    100d
                ),
                MidpointRounding.AwayFromZero
            );
        int score = (int)
            Math.Round(
                Math.Clamp(scoreGlobal + ajustementContexte, 0d, 100d),
                MidpointRounding.AwayFromZero
            );

        return new EvaluationFaisabiliteSucces
        {
            Score = score,
            Libelle = DeterminerLibelle(score),
            Confiance = DeterminerConfiance(joueursDistincts),
            Explication = ConstruireExplication(
                succes,
                joueursDistincts,
                scoreTauxDeblocage,
                scoreHardcore,
                scoreStructurel,
                ajustement,
                ajustementContexte,
                scoreGlobal
            ),
            NombreJoueursDebloques = Math.Max(0, succes.NumAwarded),
            NombreJoueursDistincts = joueursDistincts,
        };
    }

    /*
     * Ajuste la faisabilité selon ce que l'utilisateur a déjà accompli et selon
     * la zone courante détectée par le Rich Presence.
     */
    private static int CalculerAjustementContexte(
        GameAchievementV2 succes,
        IReadOnlyList<GameAchievementV2>? succesJeu,
        AnalyseZoneRichPresence? analyseZoneCourante
    )
    {
        int ajustement = 0;

        if (EstDebloque(succes))
        {
            ajustement += 12;
        }

        if (succesJeu is { Count: > 0 })
        {
            ajustement += CalculerBonusProgressionUtilisateur(succesJeu);
            ajustement += CalculerBonusVoisinage(succes, succesJeu);
        }

        ajustement += CalculerBonusZoneCourante(succes, analyseZoneCourante);

        return Math.Clamp(ajustement, -8, 22);
    }

    /*
     * Plus l'utilisateur a progressé dans le jeu, plus un succès restant est
     * susceptible d'être atteignable dans le contexte actuel.
     */
    private static int CalculerBonusProgressionUtilisateur(
        IReadOnlyList<GameAchievementV2> succesJeu
    )
    {
        int total = succesJeu.Count;
        int obtenus = succesJeu.Count(EstDebloque);

        if (total <= 0 || obtenus <= 0)
        {
            return 0;
        }

        double progression = (double)obtenus / total;

        return progression switch
        {
            >= 0.85d => 8,
            >= 0.65d => 6,
            >= 0.40d => 4,
            >= 0.20d => 2,
            _ => 1,
        };
    }

    /*
     * Si les succès proches dans l'ordre d'affichage sont déjà obtenus, le
     * succès courant est probablement dans une zone ou séquence déjà accessible.
     */
    private static int CalculerBonusVoisinage(
        GameAchievementV2 succes,
        IReadOnlyList<GameAchievementV2> succesJeu
    )
    {
        List<GameAchievementV2> succesOrdonnes =
        [
            .. succesJeu.OrderBy(item => item.DisplayOrder).ThenBy(item => item.Id),
        ];
        int index = succesOrdonnes.FindIndex(item => item.Id == succes.Id);

        if (index < 0)
        {
            return 0;
        }

        int bonus = 0;

        if (index > 0 && EstDebloque(succesOrdonnes[index - 1]))
        {
            bonus += 4;
        }

        if (index + 1 < succesOrdonnes.Count && EstDebloque(succesOrdonnes[index + 1]))
        {
            bonus += 4;
        }

        return bonus;
    }

    /*
     * Donne un bonus si le Rich Presence semble placer l'utilisateur dans la
     * même zone que celle mentionnée par le succès courant.
     */
    private static int CalculerBonusZoneCourante(
        GameAchievementV2 succes,
        AnalyseZoneRichPresence? analyseZoneCourante
    )
    {
        if (
            analyseZoneCourante is not { EstFiable: true }
            || string.IsNullOrWhiteSpace(analyseZoneCourante.ZoneDetectee)
        )
        {
            return 0;
        }

        string zone = NormaliserTexte(analyseZoneCourante.ZoneDetectee);
        string texteSucces = NormaliserTexte($"{succes.Title} {succes.Description}");

        if (string.IsNullOrWhiteSpace(zone) || string.IsNullOrWhiteSpace(texteSucces))
        {
            return 0;
        }

        if (texteSucces.Contains(zone, StringComparison.OrdinalIgnoreCase))
        {
            return analyseZoneCourante.ScoreConfiance >= 80 ? 12 : 8;
        }

        return MotsSignificatifs(zone).Any(mot => texteSucces.Contains(mot)) ? 5 : 0;
    }

    /*
     * Convertit un taux brut en pourcentage borné entre 0 et 100.
     */
    private static int ConvertirTauxEnScore(double taux)
    {
        return (int)Math.Round(Math.Clamp(taux * 100d, 0d, 100d), MidpointRounding.AwayFromZero);
    }

    /*
     * Transforme le taux hardcore en indice complémentaire sans écraser
     * totalement le taux softcore lorsqu'un succès est surtout obtenu en casual.
     */
    private static int ConvertirTauxHardcoreEnScore(double tauxDeblocage, double tauxHardcore)
    {
        if (tauxDeblocage <= 0d)
        {
            return 0;
        }

        double ratioHardcore = Math.Clamp(tauxHardcore / tauxDeblocage, 0d, 1d);
        double scoreRelatif = ratioHardcore switch
        {
            >= 0.8d => 100d,
            >= 0.6d => 82d,
            >= 0.4d => 64d,
            >= 0.2d => 46d,
            > 0d => 28d,
            _ => 12d,
        };

        return (int)Math.Round(scoreRelatif, MidpointRounding.AwayFromZero);
    }

    /*
     * Donne une base de faisabilité à partir des métadonnées du succès, sans
     * tenir compte du nombre de joueurs qui l'ont obtenu.
     */
    private static int EvaluerScoreStructurel(GameAchievementV2 succes)
    {
        int score = 68;
        string type = (succes.Type ?? string.Empty).Trim().ToLowerInvariant();

        score += type switch
        {
            "progression" => 10,
            "win_condition" => -2,
            "missable" => -10,
            _ => 0,
        };

        score += succes.Points switch
        {
            <= 0 => 0,
            <= 5 => 5,
            <= 10 => 2,
            <= 25 => -4,
            <= 50 => -9,
            _ => -14,
        };

        if (succes.Points > 0 && succes.TrueRatio > 0)
        {
            double ratioRarete = (double)succes.TrueRatio / succes.Points;
            score += ratioRarete switch
            {
                <= 1.5d => 5,
                <= 3d => 1,
                <= 6d => -5,
                <= 10d => -10,
                _ => -16,
            };
        }

        return Math.Clamp(score, 0, 100);
    }

    /*
     * Repère certains mots de description qui signalent souvent une contrainte
     * forte ou, au contraire, une action de progression plus directe.
     */
    private static int CalculerAjustementDifficulte(GameAchievementV2 succes)
    {
        string texte = $"{succes.Title} {succes.Description}".ToLowerInvariant();
        int ajustement = 0;

        if (
            ContientUn(
                texte,
                "without",
                "no damage",
                "without taking damage",
                "without being hit",
                "deathless",
                "without dying"
            )
        )
        {
            ajustement -= 8;
        }

        if (ContientUn(texte, "hard mode", "hard difficulty", "expert", "master", "nightmare"))
        {
            ajustement -= 7;
        }

        if (ContientUn(texte, "complete all", "collect all", "find all", "100%", "perfect"))
        {
            ajustement -= 5;
        }

        if (ContientUn(texte, "speedrun", "time trial", "under ", "within "))
        {
            ajustement -= 5;
        }

        if (ContientUn(texte, "finish level", "complete level", "clear level", "defeat "))
        {
            ajustement += 3;
        }

        return Math.Clamp(ajustement, -20, 10);
    }

    /*
     * Indique si un texte contient l'une des expressions recherchées.
     */
    private static bool ContientUn(string texte, params string[] expressions)
    {
        return expressions.Any(expression =>
            texte.Contains(expression, StringComparison.OrdinalIgnoreCase)
        );
    }

    /*
     * Détermine si un succès est déjà obtenu en softcore ou hardcore.
     */
    private static bool EstDebloque(GameAchievementV2 succes)
    {
        return !string.IsNullOrWhiteSpace(succes.DateEarned)
            || !string.IsNullOrWhiteSpace(succes.DateEarnedHardcore);
    }

    /*
     * Normalise un texte court pour les comparaisons de contexte.
     */
    private static string NormaliserTexte(string valeur)
    {
        return string.Join(
            ' ',
            valeur
                .ToLowerInvariant()
                .Split(
                    [' ', '\t', '\r', '\n', '-', '_', ':', ';', ',', '.', '!', '?', '\'', '"'],
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
                )
        );
    }

    /*
     * Extrait les mots assez distinctifs d'une zone Rich Presence.
     */
    private static IEnumerable<string> MotsSignificatifs(string valeur)
    {
        return valeur
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(mot => mot.Length >= 3)
            .Where(mot =>
                !string.Equals(mot, "the", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(mot, "and", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(mot, "level", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(mot, "stage", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(mot, "world", StringComparison.OrdinalIgnoreCase)
            );
    }

    /*
     * Résume les composantes du calcul pour que l'infobulle explique la décision
     * plutôt que de seulement afficher un ratio brut.
     */
    private static string ConstruireExplication(
        GameAchievementV2 succes,
        int joueursDistincts,
        int scoreTauxDeblocage,
        int scoreHardcore,
        int scoreStructurel,
        int ajustement,
        int ajustementContexte,
        int scoreGlobal
    )
    {
        string signeAjustement = ajustement >= 0 ? "+" : string.Empty;
        string signeContexte = ajustementContexte >= 0 ? "+" : string.Empty;

        return string.Join(
            " | ",
            $"Obtenu : {Math.Max(0, succes.NumAwarded)} joueurs sur {joueursDistincts}",
            $"Hardcore : {Math.Max(0, succes.NumAwardedHardcore)}",
            $"Taux : {scoreTauxDeblocage} %",
            $"Indice hardcore : {scoreHardcore}",
            $"Indice structurel : {scoreStructurel}",
            $"Ajustement difficulté : {signeAjustement}{ajustement}",
            $"Difficulté globale : {scoreGlobal}",
            $"Contexte utilisateur : {signeContexte}{ajustementContexte}"
        );
    }

    /*
     * Associe un score numérique à un libellé de faisabilité lisible.
     */
    private static string DeterminerLibelle(int score)
    {
        return score switch
        {
            >= 80 => "Très faisable",
            >= 60 => "Faisable",
            >= 40 => "Intermédiaire",
            >= 20 => "Difficile",
            _ => "Extrême",
        };
    }

    /*
     * Détermine le niveau de confiance de l'évaluation selon la taille de
     * l'échantillon disponible pour le jeu.
     */
    private static string DeterminerConfiance(int joueursDistincts)
    {
        return joueursDistincts switch
        {
            < 20 => "Faible",
            < 50 => "Moyenne",
            _ => "Bonne",
        };
    }
}
