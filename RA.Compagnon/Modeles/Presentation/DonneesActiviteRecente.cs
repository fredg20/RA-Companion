using RA.Compagnon.Modeles.Api.V2.Achievement;
using RA.Compagnon.Modeles.Api.V2.Feed;

namespace RA.Compagnon.Modeles.Presentation;

/// <summary>
/// Regroupe les données utiles à l'affichage de l'activité récente.
/// </summary>
public sealed class DonneesActiviteRecente
{
    public IReadOnlyList<AchievementUnlockV2> SuccesRecents { get; init; } = [];

    public IReadOnlyList<RecentGameAwardV2> RecompensesJeuxRecentes { get; init; } = [];
}
