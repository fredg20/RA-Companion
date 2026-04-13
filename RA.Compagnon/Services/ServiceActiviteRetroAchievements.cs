using RA.Compagnon.Modeles.Api.V2.Achievement;
using RA.Compagnon.Modeles.Api.V2.Feed;
using RA.Compagnon.Modeles.Presentation;

namespace RA.Compagnon.Services;

public sealed class ServiceActiviteRetroAchievements
{
    public static async Task<DonneesActiviteRecente> ObtenirActiviteRecenteAsync(
        string pseudo,
        string cleApiWeb,
        DateTimeOffset debut,
        DateTimeOffset fin,
        CancellationToken jetonAnnulation = default
    )
    {
        Task<IReadOnlyList<AchievementUnlockV2>> succesTask =
            ClientRetroAchievements.ObtenirSuccesDebloquesEntreAsync(
                pseudo,
                cleApiWeb,
                debut,
                fin,
                jetonAnnulation
            );

        Task<IReadOnlyList<RecentGameAwardV2>?> recompensesTask = TenterAsync(async () =>
        {
            RecentGameAwardsResponseV2 reponse =
                await ClientRetroAchievements.ObtenirRecompensesJeuxRecentesAsync(
                    cleApiWeb,
                    DateOnly.FromDateTime(debut.UtcDateTime.Date),
                    25,
                    0,
                    null,
                    jetonAnnulation
                );

            return (IReadOnlyList<RecentGameAwardV2>)reponse.Results;
        });

        await Task.WhenAll(succesTask, recompensesTask);

        return new DonneesActiviteRecente
        {
            SuccesRecents = await succesTask,
            RecompensesJeuxRecentes = await recompensesTask ?? [],
        };
    }

    private static async Task<T?> TenterAsync<T>(Func<Task<T>> action)
    {
        try
        {
            return await action();
        }
        catch
        {
            return default;
        }
    }
}
