using RA.Compagnon.Modeles.Api.V2.Achievement;
using RA.Compagnon.Modeles.Api.V2.Feed;
using RA.Compagnon.Modeles.Presentation;

/*
 * Récupère les éléments d'activité récente utiles à l'application à partir
 * des points d'entrée RetroAchievements concernés.
 */
namespace RA.Compagnon.Services;

/*
 * Agrège les succès récents et les récompenses de jeux récentes pour alimenter
 * l'affichage de l'activité utilisateur.
 */
public sealed class ServiceActiviteRetroAchievements
{
    /*
     * Charge l'activité récente du compte sur une fenêtre temporelle donnée
     * en combinant plusieurs appels de l'API.
     */
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

    /*
     * Exécute une action asynchrone optionnelle en convertissant ses erreurs
     * en valeur par défaut pour ne pas bloquer l'agrégation globale.
     */
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