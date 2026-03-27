using RA.Compagnon.Modeles.Api.V2.User;
using RA.Compagnon.Modeles.Presentation;

namespace RA.Compagnon.Services;

/// <summary>
/// Compose les données communautaires RetroAchievements d'un jeu.
/// </summary>
public sealed class ServiceCommunauteRetroAchievements
{
    public async Task<DonneesCommunauteJeu> ObtenirDonneesJeuAsync(
        string pseudo,
        string cleApiWeb,
        int identifiantJeu,
        CancellationToken jetonAnnulation = default
    )
    {
        Task<IReadOnlyList<UserClaimV2>?> claimsActivesTask = TenterAsync(() =>
            ClientRetroAchievements.ObtenirClaimsActivesAsync(cleApiWeb, jetonAnnulation)
        );
        Task<IReadOnlyList<UserClaimV2>?> claimsUtilisateurTask = TenterAsync(() =>
            ClientRetroAchievements.ObtenirClaimsUtilisateurAsync(
                pseudo,
                cleApiWeb,
                jetonAnnulation
            )
        );

        await Task.WhenAll(claimsActivesTask, claimsUtilisateurTask);

        IReadOnlyList<UserClaimV2> claimsActivesJeu =
        [
            .. ((await claimsActivesTask) ?? []).Where(item => item.GameId == identifiantJeu),
        ];
        IReadOnlyList<UserClaimV2> claimsUtilisateurJeu =
        [
            .. ((await claimsUtilisateurTask) ?? []).Where(item => item.GameId == identifiantJeu),
        ];

        return new DonneesCommunauteJeu
        {
            ClaimsActivesJeu = claimsActivesJeu,
            ClaimsUtilisateurJeu = claimsUtilisateurJeu,
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
