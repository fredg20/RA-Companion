using RA.Compagnon.Modeles.Api.V2.Achievement;
using RA.Compagnon.Modeles.Api.V2.Feed;

namespace RA.Compagnon.Modeles.Presentation;

public sealed class DonneesActiviteRecente
{
    public IReadOnlyList<AchievementUnlockV2> SuccesRecents { get; init; } = [];

    public IReadOnlyList<RecentGameAwardV2> RecompensesJeuxRecentes { get; init; } = [];
}
