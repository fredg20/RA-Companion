using RA.Compagnon.Modeles.Api.V2.Game;

namespace RA.Compagnon.Services;

/// <summary>
/// Fournit l'accès aux référentiels RetroAchievements partagés par l'interface.
/// </summary>
public sealed class ServiceCatalogueRetroAchievements
{
    public async Task<IReadOnlyList<ConsoleV2>> ObtenirConsolesAsync(
        string cleApiWeb,
        CancellationToken jetonAnnulation = default
    )
    {
        return await ClientRetroAchievements.ObtenirConsolesAsync(cleApiWeb, jetonAnnulation);
    }

    public async Task<IReadOnlyList<GameListEntryV2>> ObtenirJeuxSystemeAvecHashesAsync(
        string cleApiWeb,
        int identifiantConsole,
        CancellationToken jetonAnnulation = default
    )
    {
        return await ClientRetroAchievements.ObtenirJeuxSystemeAvecHashesAsync(
            cleApiWeb,
            identifiantConsole,
            jetonAnnulation
        );
    }
}
