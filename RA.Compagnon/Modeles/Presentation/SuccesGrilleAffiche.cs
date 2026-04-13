namespace RA.Compagnon.Modeles.Presentation;

/// <summary>
/// Représente un badge de succès prêt à afficher dans la grille.
/// </summary>
public sealed class SuccesGrilleAffiche
{
    public int IdentifiantSucces { get; init; }

    public string Titre { get; init; } = string.Empty;

    public string ToolTip { get; init; } = string.Empty;

    public string UrlBadge { get; init; } = string.Empty;

    public bool EstDebloque { get; init; }

    public bool EstHardcore { get; init; }
}
