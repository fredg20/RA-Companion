/*
 * Déclare l'exception métier utilisée lorsque l'utilisateur demandé n'est
 * pas accessible via l'API RetroAchievements.
 */
namespace RA.Compagnon.Services;

/*
 * Signale qu'un pseudo RetroAchievements n'existe pas ou ne peut pas être
 * résolu correctement par les appels réseau en cours.
 */
public sealed class UtilisateurRetroAchievementsInaccessibleException : Exception
{
    /*
     * Initialise l'exception avec le pseudo concerné.
     */
    public UtilisateurRetroAchievementsInaccessibleException(string pseudo)
        : base(
            $"L'utilisateur RetroAchievements \"{pseudo}\" n'existe pas ou n'est pas accessible."
        ) { }

    /*
     * Initialise l'exception avec le pseudo concerné et l'erreur interne
     * qui a conduit à cet état.
     */
    public UtilisateurRetroAchievementsInaccessibleException(
        string pseudo,
        Exception innerException
    )
        : base(
            $"L'utilisateur RetroAchievements \"{pseudo}\" n'existe pas ou n'est pas accessible.",
            innerException
        ) { }
}
