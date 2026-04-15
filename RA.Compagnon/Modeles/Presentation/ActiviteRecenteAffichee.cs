/*
 * Décrit l'activité récente déjà formatée pour l'affichage dans la fenêtre
 * principale.
 */
namespace RA.Compagnon.Modeles.Presentation;

/*
 * Transporte un état principal et plusieurs lignes résumant les derniers
 * événements visibles pour l'utilisateur.
 */
public sealed class ActiviteRecenteAffichee
{
    public string TexteEtat { get; init; } = string.Empty;

    public IReadOnlyList<string> Lignes { get; init; } = [];
}