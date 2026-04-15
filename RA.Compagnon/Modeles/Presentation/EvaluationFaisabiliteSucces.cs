/*
 * Décrit le résultat formaté d'une évaluation de faisabilité pour un succès.
 */
namespace RA.Compagnon.Modeles.Presentation;

/*
 * Transporte le score, le niveau de confiance et les métriques utiles à
 * l'affichage de la faisabilité d'un succès.
 */
public sealed class EvaluationFaisabiliteSucces
{
    public int Score { get; init; }

    public string Libelle { get; init; } = string.Empty;

    public string Confiance { get; init; } = string.Empty;

    public string Explication { get; init; } = string.Empty;

    public int NombreJoueursDebloques { get; init; }

    public int NombreJoueursDistincts { get; init; }
}