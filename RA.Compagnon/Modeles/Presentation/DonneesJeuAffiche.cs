using RA.Compagnon.Modeles.Api.V2.Game;

/*
 * Regroupe l'ensemble des données brutes et enrichies nécessaires à
 * l'affichage complet d'un jeu dans l'interface.
 */
namespace RA.Compagnon.Modeles.Presentation;

/*
 * Transporte les données de jeu, de progression et de communauté utilisées
 * par les services de présentation.
 */
public sealed class DonneesJeuAffiche
{
    public required GameInfoAndUserProgressV2 Jeu { get; init; }

    public GameExtendedDetailsV2? DetailsEtendus { get; init; }

    public GameProgressionV2? Progression { get; init; }

    public IReadOnlyList<GameRankAndScoreEntryV2> RangsEtScores { get; init; } = [];

    public DonneesCommunauteJeu? Communaute { get; set; }

    public CommunauteJeuAffichee? CommunauteAffichee { get; set; }
}