namespace RA.Compagnon.Modeles.Presentation;

/// <summary>
/// Représente l'évaluation calculée de la faisabilité d'un succès.
/// </summary>
public sealed class EvaluationFaisabiliteSucces
{
    public int Score { get; init; }

    public string Libelle { get; init; } = string.Empty;

    public string Confiance { get; init; } = string.Empty;

    public string Explication { get; init; } = string.Empty;

    public int NombreJoueursDebloques { get; init; }

    public int NombreJoueursDistincts { get; init; }
}
