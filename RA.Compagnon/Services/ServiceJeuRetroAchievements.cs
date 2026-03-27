using RA.Compagnon.Modeles.Api.V2.Game;
using RA.Compagnon.Modeles.Presentation;

namespace RA.Compagnon.Services;

/// <summary>
/// Compose les données de jeu RetroAchievements utiles à l'affichage principal.
/// </summary>
public sealed class ServiceJeuRetroAchievements
{
    public async Task<DonneesJeuAffiche> ObtenirDonneesJeuAsync(
        string pseudo,
        string cleApiWeb,
        int identifiantJeu,
        CancellationToken jetonAnnulation = default
    )
    {
        GameInfoAndUserProgressV2 jeu =
            await ClientRetroAchievements.ObtenirJeuEtProgressionUtilisateurAsync(
                pseudo,
                cleApiWeb,
                identifiantJeu,
                jetonAnnulation
            );

        Task<GameExtendedDetailsV2?> detailsTask = TenterAsync(() =>
            ClientRetroAchievements.ObtenirDetailsEtendusJeuAsync(
                cleApiWeb,
                identifiantJeu,
                jetonAnnulation
            )
        );
        Task<GameProgressionV2?> progressionTask = TenterAsync(() =>
            ClientRetroAchievements.ObtenirProgressionJeuAsync(
                cleApiWeb,
                identifiantJeu,
                false,
                jetonAnnulation
            )
        );
        Task<IReadOnlyList<GameRankAndScoreEntryV2>?> rangsTask = TenterAsync(() =>
            ClientRetroAchievements.ObtenirRangEtScoreJeuAsync(
                pseudo,
                cleApiWeb,
                identifiantJeu,
                jetonAnnulation
            )
        );

        await Task.WhenAll(detailsTask, progressionTask, rangsTask);

        GameExtendedDetailsV2? details = await detailsTask;
        GameProgressionV2? progression = await progressionTask;
        IReadOnlyList<GameRankAndScoreEntryV2> rangsEtScores = await rangsTask ?? [];

        HydraterJeu(jeu, details);

        return new DonneesJeuAffiche
        {
            Jeu = jeu,
            DetailsEtendus = details,
            Progression = progression,
            RangsEtScores = rangsEtScores,
        };
    }

    private static void HydraterJeu(GameInfoAndUserProgressV2 jeu, GameExtendedDetailsV2? details)
    {
        if (details is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(jeu.Title) && !string.IsNullOrWhiteSpace(details.Title))
        {
            jeu.Title = details.Title.Trim();
        }

        if (
            string.IsNullOrWhiteSpace(jeu.ConsoleName)
            && !string.IsNullOrWhiteSpace(details.ConsoleName)
        )
        {
            jeu.ConsoleName = details.ConsoleName.Trim();
        }

        if (jeu.ConsoleId <= 0 && details.ConsoleId > 0)
        {
            jeu.ConsoleId = details.ConsoleId;
        }

        if (string.IsNullOrWhiteSpace(jeu.Released) && !string.IsNullOrWhiteSpace(details.Released))
        {
            jeu.Released = details.Released.Trim();
        }

        if (
            string.IsNullOrWhiteSpace(jeu.Developer)
            && !string.IsNullOrWhiteSpace(details.Developer)
        )
        {
            jeu.Developer = details.Developer.Trim();
        }

        if (
            string.IsNullOrWhiteSpace(jeu.Publisher)
            && !string.IsNullOrWhiteSpace(details.Publisher)
        )
        {
            jeu.Publisher = details.Publisher.Trim();
        }

        if (string.IsNullOrWhiteSpace(jeu.Genre) && !string.IsNullOrWhiteSpace(details.Genre))
        {
            jeu.Genre = details.Genre.Trim();
        }

        if (
            string.IsNullOrWhiteSpace(jeu.ImageBoxArt)
            && !string.IsNullOrWhiteSpace(details.ImageBoxArt)
        )
        {
            jeu.ImageBoxArt = details.ImageBoxArt.Trim();
        }

        if (
            string.IsNullOrWhiteSpace(jeu.ImageTitle)
            && !string.IsNullOrWhiteSpace(details.ImageTitle)
        )
        {
            jeu.ImageTitle = details.ImageTitle.Trim();
        }

        if (
            string.IsNullOrWhiteSpace(jeu.ImageIngame)
            && !string.IsNullOrWhiteSpace(details.ImageIngame)
        )
        {
            jeu.ImageIngame = details.ImageIngame.Trim();
        }

        if (jeu.NumDistinctPlayers <= 0 && details.NumDistinctPlayers > 0)
        {
            jeu.NumDistinctPlayers = details.NumDistinctPlayers;
        }

        if (
            (jeu.Achievements is null || jeu.Achievements.Count == 0)
            && details.Achievements.Count > 0
        )
        {
            jeu.Achievements = details.Achievements;
            jeu.NumAchievements = details.NumAchievements;
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
