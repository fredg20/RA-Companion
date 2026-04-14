using RA.Compagnon.Modeles.Api.V2.Achievement;
using RA.Compagnon.Modeles.Api.V2.Feed;

/*
 * Regroupe les données brutes nécessaires à la construction de l'activité
 * récente affichée dans l'interface.
 */
namespace RA.Compagnon.Modeles.Presentation;

/*
 * Transporte les succès récents et les récompenses de jeux récentes issus
 * des API RetroAchievements.
 */
public sealed class DonneesActiviteRecente
{
    public IReadOnlyList<AchievementUnlockV2> SuccesRecents { get; init; } = [];

    public IReadOnlyList<RecentGameAwardV2> RecompensesJeuxRecentes { get; init; } = [];
}
