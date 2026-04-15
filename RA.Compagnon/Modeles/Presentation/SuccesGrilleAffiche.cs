/*
 * Décrit le modèle de présentation léger utilisé pour chaque badge de la
 * grille complète des succès d'un jeu.
 */
namespace RA.Compagnon.Modeles.Presentation;

/*
 * Transporte les données minimales nécessaires au rendu et aux interactions
 * d'un succès dans la grille.
 */
public sealed class SuccesGrilleAffiche
{
    public int IdentifiantSucces { get; init; }

    public string Titre { get; init; } = string.Empty;

    public string ToolTip { get; init; } = string.Empty;

    public string UrlBadge { get; init; } = string.Empty;

    public bool EstDebloque { get; init; }

    public bool EstHardcore { get; init; }
}