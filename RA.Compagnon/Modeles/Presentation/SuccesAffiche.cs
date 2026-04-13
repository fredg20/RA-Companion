namespace RA.Compagnon.Modeles.Presentation;

public sealed class SuccesAffiche
{
    public string Titre { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public string DetailsPoints { get; init; } = string.Empty;

    public string DetailsFaisabilite { get; init; } = string.Empty;

    public int ScoreFaisabilite { get; init; }

    public string LibelleFaisabilite { get; init; } = string.Empty;

    public string ConfianceFaisabilite { get; init; } = string.Empty;

    public string ExplicationFaisabilite { get; init; } = string.Empty;

    public string UrlBadge { get; init; } = string.Empty;

    public bool EstDebloque { get; init; }
}
