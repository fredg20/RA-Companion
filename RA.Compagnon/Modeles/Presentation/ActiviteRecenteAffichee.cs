namespace RA.Compagnon.Modeles.Presentation;

public sealed class ActiviteRecenteAffichee
{
    public string TexteEtat { get; init; } = string.Empty;

    public IReadOnlyList<string> Lignes { get; init; } = [];
}
