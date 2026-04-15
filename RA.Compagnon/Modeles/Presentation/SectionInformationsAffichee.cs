/*
 * Décrit une section d'informations déjà prête à afficher dans la zone de
 * compte ou dans une carte d'aide.
 */
namespace RA.Compagnon.Modeles.Presentation;

/*
 * Regroupe un titre de section et les lignes d'information qui lui sont
 * associées dans l'interface.
 */
public sealed class SectionInformationsAffichee
{
    public required string Titre { get; init; }

    public IReadOnlyList<LigneInformationAffichee> Lignes { get; init; } = [];
}