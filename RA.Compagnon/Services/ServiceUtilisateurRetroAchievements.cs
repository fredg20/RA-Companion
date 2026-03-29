using RA.Compagnon.Modeles.Api.V2.User;
using RA.Compagnon.Modeles.Presentation;

namespace RA.Compagnon.Services;

/// <summary>
/// Compose les données utilisateur RetroAchievements utiles à l'interface.
/// </summary>
public sealed class ServiceUtilisateurRetroAchievements
{
    public static async Task<UserProfileV2> ObtenirProfilAsync(
        string pseudo,
        string cleApiWeb,
        CancellationToken jetonAnnulation = default
    )
    {
        return await ClientRetroAchievements.ObtenirProfilUtilisateurAsync(
            pseudo,
            cleApiWeb,
            jetonAnnulation
        );
    }

    public static async Task<DonneesCompteUtilisateur> ObtenirDonneesCompteAsync(
        string pseudo,
        string cleApiWeb,
        CancellationToken jetonAnnulation = default
    )
    {
        Task<UserProfileV2?> profilTask = TenterAsync(() =>
            ClientRetroAchievements.ObtenirProfilUtilisateurAsync(
                pseudo,
                cleApiWeb,
                jetonAnnulation
            )
        );
        Task<UserSummaryV2?> resumeTask = TenterAsync(() =>
            ClientRetroAchievements.ObtenirResumeUtilisateurAsync(
                pseudo,
                cleApiWeb,
                jetonAnnulation
            )
        );
        Task<UserPointsV2?> pointsTask = TenterAsync(() =>
            ClientRetroAchievements.ObtenirPointsUtilisateurAsync(
                pseudo,
                cleApiWeb,
                jetonAnnulation
            )
        );
        Task<UserAwardsResponseV2?> recompensesTask = TenterAsync(() =>
            ClientRetroAchievements.ObtenirRecompensesUtilisateurAsync(
                pseudo,
                cleApiWeb,
                jetonAnnulation
            )
        );

        await Task.WhenAll(profilTask, resumeTask, pointsTask, recompensesTask);

        UserSummaryV2? resume = await resumeTask;
        UserProgressResponseV2? progression = null;

        List<int> identifiantsJeu = ConstruireIdentifiantsJeuPourProgression(resume);

        if (identifiantsJeu.Count > 0)
        {
            progression = await TenterAsync(() =>
                ClientRetroAchievements.ObtenirProgressionUtilisateurAsync(
                    pseudo,
                    cleApiWeb,
                    identifiantsJeu,
                    jetonAnnulation
                )
            );
        }

        return new DonneesCompteUtilisateur
        {
            Profil = await profilTask,
            Resume = resume,
            Points = await pointsTask,
            Recompenses = await recompensesTask,
            Progression = progression,
        };
    }

    public static async Task<UserSummaryV2?> ObtenirResumeAsync(
        string pseudo,
        string cleApiWeb,
        CancellationToken jetonAnnulation = default
    )
    {
        return await TenterAsync(() =>
            ClientRetroAchievements.ObtenirResumeUtilisateurAsync(
                pseudo,
                cleApiWeb,
                jetonAnnulation
            )
        );
    }

    public static async Task<IReadOnlyList<RecentlyPlayedGameV2>> ObtenirJeuxRecemmentJouesAsync(
        string pseudo,
        string cleApiWeb,
        CancellationToken jetonAnnulation = default
    )
    {
        return await ClientRetroAchievements.ObtenirJeuxRecemmentJouesAsync(
            pseudo,
            cleApiWeb,
            jetonAnnulation
        );
    }

    private static async Task<T?> TenterAsync<T>(Func<Task<T>> action)
        where T : class
    {
        try
        {
            return await action();
        }
        catch
        {
            return null;
        }
    }

    private static List<int> ConstruireIdentifiantsJeuPourProgression(UserSummaryV2? resume)
    {
        if (resume is null)
        {
            return [];
        }

        HashSet<int> identifiants = [];

        if (resume.LastGameId > 0)
        {
            identifiants.Add(resume.LastGameId);
        }

        foreach (RecentlyPlayedGameV2 jeu in resume.RecentlyPlayed)
        {
            if (jeu.IdentifiantJeu > 0)
            {
                identifiants.Add(jeu.IdentifiantJeu);
            }
        }

        return [.. identifiants];
    }
}
