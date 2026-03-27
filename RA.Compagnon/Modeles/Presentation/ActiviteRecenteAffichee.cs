namespace RA.Compagnon.Modeles.Presentation;

/// <summary>
/// Représente le contenu prêt à afficher de la section d'activité récente.
/// </summary>
public sealed class ActiviteRecenteAffichee
{
    public string TexteEtat { get; init; } = string.Empty;

    public IReadOnlyList<string> Lignes { get; init; } = [];
}
