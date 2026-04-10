using RA.Compagnon.Modeles.Api.V2.Game;
using RA.Compagnon.Modeles.Presentation;

namespace RA.Compagnon.Services;

/// <summary>
/// Calcule une estimation de faisabilite pour un succes.
/// </summary>
public sealed class ServiceEvaluationFaisabiliteSucces
{
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
        int score = (int)Math.Round(
            Math.Clamp(tauxDeblocage * 100d, 0d, 100d),
            MidpointRounding.AwayFromZero
        );

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
