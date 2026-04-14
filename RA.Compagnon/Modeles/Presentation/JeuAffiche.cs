/*
 * Décrit le modèle d'affichage simplifié d'un jeu pour la carte principale
 * de l'interface utilisateur.
 */
namespace RA.Compagnon.Modeles.Presentation;

/*
 * Regroupe les textes et valeurs déjà formatés nécessaires à l'affichage
 * du jeu courant ou du dernier jeu.
 */
public sealed class JeuAffiche
{
    public string Titre { get; init; } = string.Empty;

    public string TempsJeu { get; init; } = string.Empty;

    public string Statut { get; init; } = string.Empty;

    public string Details { get; init; } = string.Empty;

    public string ResumeProgression { get; init; } = string.Empty;

    public string PourcentageTexte { get; init; } = string.Empty;

    public double PourcentageValeur { get; init; }
}
