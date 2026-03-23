namespace RA.Compagnon.Modeles.Local;

/// <summary>
/// Représente les informations d'utilisateur RetroAchievements stockées localement.
/// </summary>
public sealed class EtatUtilisateurLocal
{
    /// <summary>
    /// Pseudo RetroAchievements saisi par l'utilisateur.
    /// </summary>
    public string Pseudo { get; set; } = string.Empty;

    /// <summary>
    /// Clé Web API utilisée pour les appels authentifiés.
    /// </summary>
    public string CleApiWeb { get; set; } = string.Empty;
}
