namespace RA.Compagnon.Modeles.Presentation;

/// <summary>
/// Représente un jeu récent prêt à être affiché dans une liste compacte.
/// </summary>
public sealed class JeuRecentAffiche
{
    public required string Titre { get; init; }

    public required string SousTitre { get; init; }
}
