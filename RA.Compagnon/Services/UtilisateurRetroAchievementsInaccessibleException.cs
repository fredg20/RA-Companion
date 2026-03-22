namespace RA.Compagnon.Services;

/// <summary>
/// Signale qu'un profil RetroAchievements ne peut pas être chargé car l'utilisateur
/// n'existe pas ou n'est pas accessible avec les informations fournies.
/// </summary>
public sealed class UtilisateurRetroAchievementsInaccessibleException : Exception
{
    /// <summary>
    /// Initialise une nouvelle exception de profil utilisateur inaccessible.
    /// </summary>
    public UtilisateurRetroAchievementsInaccessibleException(string pseudo)
        : base(
            $"L'utilisateur RetroAchievements \"{pseudo}\" n'existe pas ou n'est pas accessible."
        ) { }

    /// <summary>
    /// Initialise une nouvelle exception de profil utilisateur inaccessible.
    /// </summary>
    public UtilisateurRetroAchievementsInaccessibleException(
        string pseudo,
        Exception innerException
    )
        : base(
            $"L'utilisateur RetroAchievements \"{pseudo}\" n'existe pas ou n'est pas accessible.",
            innerException
        ) { }
}
