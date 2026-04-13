using RA.Compagnon.Modeles.Api.V2.Game;

namespace RA.Compagnon.Modeles.Presentation;

public sealed class DonneesJeuAffiche
{
    public required GameInfoAndUserProgressV2 Jeu { get; init; }

    public GameExtendedDetailsV2? DetailsEtendus { get; init; }

    public GameProgressionV2? Progression { get; init; }

    public IReadOnlyList<GameRankAndScoreEntryV2> RangsEtScores { get; init; } = [];

    public DonneesCommunauteJeu? Communaute { get; set; }

    public CommunauteJeuAffichee? CommunauteAffichee { get; set; }
}
