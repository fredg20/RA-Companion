/*
 * Décrit une ligne simple d'information déjà prête à afficher, composée
 * d'un libellé et d'une valeur.
 */
namespace RA.Compagnon.Modeles.Presentation;

/*
 * Transporte une paire libellé/valeur pour les sections d'information.
 */
public sealed class LigneInformationAffichee
{
    public required string Libelle { get; init; }

    public required string Valeur { get; init; }
}
