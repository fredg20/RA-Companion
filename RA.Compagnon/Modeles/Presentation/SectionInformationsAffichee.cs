namespace RA.Compagnon.Modeles.Presentation;

/// <summary>
/// Représente une section d'informations prête à être affichée.
/// </summary>
public sealed class SectionInformationsAffichee
{
    public required string Titre { get; init; }

    public IReadOnlyList<LigneInformationAffichee> Lignes { get; init; } = [];
}
