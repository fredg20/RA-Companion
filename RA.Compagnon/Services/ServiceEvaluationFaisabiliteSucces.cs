using RA.Compagnon.Modeles.Api.V2.Game;
using RA.Compagnon.Modeles.Presentation;

/*
 * Évalue une faisabilité simplifiée d'un succès à partir de son taux de
 * déblocage observé sur le jeu concerné.
 */
namespace RA.Compagnon.Services;

/*
 * Produit un score, un libellé et un niveau de confiance pour aider
 * l'interface à qualifier rapidement la difficulté pratique d'un succès.
 */
public sealed class ServiceEvaluationFaisabiliteSucces
{
    /*
     * Calcule l'évaluation de faisabilité en utilisant le nombre de joueurs
     * distincts du jeu et le nombre de déblocages connus.
     */
    public static EvaluationFaisabiliteSucces Evaluer(
        GameAchievementV2 succes,
        int identifiantJeu,
        int nombreJoueursDistinctsJeu = 0
    )
    {
        if (identifiantJeu <= 0)
        {
            return new EvaluationFaisabiliteSucces();
        }

        int joueursDistincts = nombreJoueursDistinctsJeu;

        if (joueursDistincts <= 0 || succes.NumAwarded <= 0)
        {
            return new EvaluationFaisabiliteSucces();
        }

        double tauxDeblocage = (double)succes.NumAwarded / joueursDistincts;
        int score = (int)
            Math.Round(Math.Clamp(tauxDeblocage * 100d, 0d, 100d), MidpointRounding.AwayFromZero);

        return new EvaluationFaisabiliteSucces
        {
            Score = score,
            Libelle = DeterminerLibelle(score),
            Confiance = DeterminerConfiance(joueursDistincts),
            Explication = $"Succès obtenu : {succes.NumAwarded} joueurs sur {joueursDistincts}",
            NombreJoueursDebloques = succes.NumAwarded,
            NombreJoueursDistincts = joueursDistincts,
        };
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
