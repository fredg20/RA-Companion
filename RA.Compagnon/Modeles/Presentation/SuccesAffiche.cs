namespace RA.Compagnon.Modeles.Presentation;

/// <summary>
/// Représente le contenu prêt à afficher d'un succès.
/// </summary>
public sealed class SuccesAffiche
{
    public string Titre { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public string DetailsPoints { get; init; } = string.Empty;

    public string DetailsFaisabilite { get; init; } = string.Empty;

    public string UrlBadge { get; init; } = string.Empty;

    public bool EstDebloque { get; init; }
}
