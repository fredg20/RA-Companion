namespace RA.Compagnon.Services;

public sealed class UtilisateurRetroAchievementsInaccessibleException : Exception
{
    public UtilisateurRetroAchievementsInaccessibleException(string pseudo)
        : base(
            $"L'utilisateur RetroAchievements \"{pseudo}\" n'existe pas ou n'est pas accessible."
        ) { }

    public UtilisateurRetroAchievementsInaccessibleException(
        string pseudo,
        Exception innerException
    )
        : base(
            $"L'utilisateur RetroAchievements \"{pseudo}\" n'existe pas ou n'est pas accessible.",
            innerException
        ) { }
}
