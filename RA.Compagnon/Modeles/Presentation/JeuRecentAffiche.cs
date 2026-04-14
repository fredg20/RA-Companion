/*
 * Décrit un jeu récent au format léger pour l'affichage dans les listes de
 * compte utilisateur.
 */
namespace RA.Compagnon.Modeles.Presentation;

/*
 * Transporte le titre et le sous-titre d'un jeu récent déjà formatés.
 */
public sealed class JeuRecentAffiche
{
    public required string Titre { get; init; }

    public required string SousTitre { get; init; }
}
