using RA.Compagnon.Modeles.Api.V2.Game;

namespace RA.Compagnon.Services;

public sealed class ServiceCatalogueRetroAchievements
{
    private readonly SemaphoreSlim _verrouConsoles = new(1, 1);
    private readonly SemaphoreSlim _verrouJeuxSysteme = new(1, 1);
    private IReadOnlyList<ConsoleV2>? _consolesCache;
    private readonly Dictionary<int, IReadOnlyList<GameListEntryV2>> _jeuxSystemeCache = [];

    public async Task<IReadOnlyList<ConsoleV2>> ObtenirConsolesAsync(
        string cleApiWeb,
        CancellationToken jetonAnnulation = default
    )
    {
        if (_consolesCache is not null)
        {
            return _consolesCache;
        }

        await _verrouConsoles.WaitAsync(jetonAnnulation);

        try
        {
            if (_consolesCache is not null)
            {
                return _consolesCache;
            }

            _consolesCache = await ClientRetroAchievements.ObtenirConsolesAsync(
                cleApiWeb,
                jetonAnnulation
            );

            return _consolesCache;
        }
        finally
        {
            _verrouConsoles.Release();
        }
    }

    public async Task<IReadOnlyList<GameListEntryV2>> ObtenirJeuxSystemeAvecHashesAsync(
        string cleApiWeb,
        int identifiantConsole,
        CancellationToken jetonAnnulation = default
    )
    {
        if (
            _jeuxSystemeCache.TryGetValue(
                identifiantConsole,
                out IReadOnlyList<GameListEntryV2>? jeux
            )
        )
        {
            return jeux;
        }

        await _verrouJeuxSysteme.WaitAsync(jetonAnnulation);

        try
        {
            if (
                _jeuxSystemeCache.TryGetValue(
                    identifiantConsole,
                    out IReadOnlyList<GameListEntryV2>? jeuxCaches
                )
            )
            {
                return jeuxCaches;
            }

            IReadOnlyList<GameListEntryV2> jeuxCharges =
                await ClientRetroAchievements.ObtenirJeuxSystemeAvecHashesAsync(
                    cleApiWeb,
                    identifiantConsole,
                    jetonAnnulation
                );

            _jeuxSystemeCache[identifiantConsole] = jeuxCharges;
            return jeuxCharges;
        }
        finally
        {
            _verrouJeuxSysteme.Release();
        }
    }
}
