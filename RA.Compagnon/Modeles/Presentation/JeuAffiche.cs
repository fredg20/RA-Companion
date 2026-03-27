namespace RA.Compagnon.Modeles.Presentation;

/// <summary>
/// Représente le contenu textuel prêt à afficher de la carte Jeu.
/// </summary>
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
