using RA.Compagnon.Modeles.Api.V2.User;
using RA.Compagnon.Modeles.Presentation;

/*
 * Centralise les chargements relatifs au compte utilisateur RetroAchievements
 * nécessaires à l'application.
 */
namespace RA.Compagnon.Services;

/*
 * Rassemble les appels de profil, résumé, points, récompenses et jeux récents
 * pour exposer des données utilisateur plus simples à consommer.
 */
public sealed class ServiceUtilisateurRetroAchievements
{
    /*
     * Charge le profil utilisateur complet à partir des identifiants fournis.
     */
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

    /*
     * Agrège les principales données de compte en parallélisant les appels
     * indépendants puis en complétant la progression si possible.
     */
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

    /*
     * Charge le résumé utilisateur sans propager les erreurs non critiques.
     */
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

    /*
     * Retourne la liste des jeux récemment joués pour le compte demandé.
     */
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

    /*
     * Exécute un appel asynchrone tolérant aux erreurs lorsque la donnée
     * concernée reste optionnelle pour l'interface.
     */
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

    /*
     * Construit la liste des Game ID à utiliser pour charger une progression
     * utilisateur utile au compte affiché.
     */
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
