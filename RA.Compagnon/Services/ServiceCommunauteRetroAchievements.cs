using RA.Compagnon.Modeles.Api.V2.User;
using RA.Compagnon.Modeles.Presentation;

namespace RA.Compagnon.Services;

public sealed class ServiceCommunauteRetroAchievements
{
    private static readonly TimeSpan DureeCacheClaims = TimeSpan.FromSeconds(30);
    private readonly SemaphoreSlim _verrouClaimsActives = new(1, 1);
    private readonly SemaphoreSlim _verrouClaimsUtilisateur = new(1, 1);
    private IReadOnlyList<UserClaimV2>? _claimsActivesCache;
    private DateTimeOffset _horodatageClaimsActivesCache;
    private IReadOnlyList<UserClaimV2>? _claimsUtilisateurCache;
    private DateTimeOffset _horodatageClaimsUtilisateurCache;
    private string _pseudoClaimsUtilisateurCache = string.Empty;

    public async Task<DonneesCommunauteJeu> ObtenirDonneesJeuAsync(
        string pseudo,
        string cleApiWeb,
        int identifiantJeu,
        CancellationToken jetonAnnulation = default
    )
    {
        Task<IReadOnlyList<UserClaimV2>> claimsActivesTask = ObtenirClaimsActivesAsync(
            cleApiWeb,
            jetonAnnulation
        );
        Task<IReadOnlyList<UserClaimV2>> claimsUtilisateurTask = ObtenirClaimsUtilisateurAsync(
            pseudo,
            cleApiWeb,
            jetonAnnulation
        );

        await Task.WhenAll(claimsActivesTask, claimsUtilisateurTask);

        IReadOnlyList<UserClaimV2> claimsActivesJeu =
        [
            .. (await claimsActivesTask).Where(item => item.GameId == identifiantJeu),
        ];
        IReadOnlyList<UserClaimV2> claimsUtilisateurJeu =
        [
            .. (await claimsUtilisateurTask).Where(item => item.GameId == identifiantJeu),
        ];

        return new DonneesCommunauteJeu
        {
            ClaimsActivesJeu = claimsActivesJeu,
            ClaimsUtilisateurJeu = claimsUtilisateurJeu,
        };
    }

    private async Task<IReadOnlyList<UserClaimV2>> ObtenirClaimsActivesAsync(
        string cleApiWeb,
        CancellationToken jetonAnnulation
    )
    {
        if (
            _claimsActivesCache is not null
            && DateTimeOffset.UtcNow - _horodatageClaimsActivesCache <= DureeCacheClaims
        )
        {
            return _claimsActivesCache;
        }

        await _verrouClaimsActives.WaitAsync(jetonAnnulation);

        try
        {
            if (
                _claimsActivesCache is not null
                && DateTimeOffset.UtcNow - _horodatageClaimsActivesCache <= DureeCacheClaims
            )
            {
                return _claimsActivesCache;
            }

            _claimsActivesCache =
                await TenterAsync(() =>
                    ClientRetroAchievements.ObtenirClaimsActivesAsync(cleApiWeb, jetonAnnulation)
                ) ?? [];
            _horodatageClaimsActivesCache = DateTimeOffset.UtcNow;
            return _claimsActivesCache;
        }
        finally
        {
            _verrouClaimsActives.Release();
        }
    }

    private async Task<IReadOnlyList<UserClaimV2>> ObtenirClaimsUtilisateurAsync(
        string pseudo,
        string cleApiWeb,
        CancellationToken jetonAnnulation
    )
    {
        if (
            _claimsUtilisateurCache is not null
            && string.Equals(_pseudoClaimsUtilisateurCache, pseudo, StringComparison.Ordinal)
            && DateTimeOffset.UtcNow - _horodatageClaimsUtilisateurCache <= DureeCacheClaims
        )
        {
            return _claimsUtilisateurCache;
        }

        await _verrouClaimsUtilisateur.WaitAsync(jetonAnnulation);

        try
        {
            if (
                _claimsUtilisateurCache is not null
                && string.Equals(_pseudoClaimsUtilisateurCache, pseudo, StringComparison.Ordinal)
                && DateTimeOffset.UtcNow - _horodatageClaimsUtilisateurCache <= DureeCacheClaims
            )
            {
                return _claimsUtilisateurCache;
            }

            _claimsUtilisateurCache =
                await TenterAsync(() =>
                    ClientRetroAchievements.ObtenirClaimsUtilisateurAsync(
                        pseudo,
                        cleApiWeb,
                        jetonAnnulation
                    )
                ) ?? [];
            _horodatageClaimsUtilisateurCache = DateTimeOffset.UtcNow;
            _pseudoClaimsUtilisateurCache = pseudo;
            return _claimsUtilisateurCache;
        }
        finally
        {
            _verrouClaimsUtilisateur.Release();
        }
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
