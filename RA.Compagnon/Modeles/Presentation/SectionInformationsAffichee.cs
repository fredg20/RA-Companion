namespace RA.Compagnon.Modeles.Presentation;

public sealed class SectionInformationsAffichee
{
    public required string Titre { get; init; }

    public IReadOnlyList<LigneInformationAffichee> Lignes { get; init; } = [];
}
