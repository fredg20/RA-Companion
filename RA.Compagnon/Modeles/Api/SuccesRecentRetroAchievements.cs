using System.Text.Json.Serialization;

namespace RA.Compagnon.Modeles.Api;

/// <summary>
/// Représente un succès récemment débloqué par un utilisateur.
/// </summary>
public sealed class SuccesRecentRetroAchievements
{
    /// <summary>
    /// Date de déblocage du succès.
    /// </summary>
    [JsonPropertyName("Date")]
    public string DateDeblocage { get; set; } = string.Empty;

    /// <summary>
    /// Indique si le succès a été obtenu en mode Hardcore.
    /// </summary>
    [JsonPropertyName("HardcoreMode")]
    public bool ModeHardcore { get; set; }

    /// <summary>
    /// Identifiant du succès.
    /// </summary>
    [JsonPropertyName("AchievementID")]
    public int IdentifiantSucces { get; set; }

    /// <summary>
    /// Titre du succès.
    /// </summary>
    [JsonPropertyName("Title")]
    public string Titre { get; set; } = string.Empty;

    /// <summary>
    /// Description du succès.
    /// </summary>
    [JsonPropertyName("Description")]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Nombre de points du succès.
    /// </summary>
    [JsonPropertyName("Points")]
    public int Points { get; set; }

    /// <summary>
    /// Identifiant du jeu lié au succès.
    /// </summary>
    [JsonPropertyName("GameID")]
    public int IdentifiantJeu { get; set; }

    /// <summary>
    /// Titre du jeu lié au succès.
    /// </summary>
    [JsonPropertyName("GameTitle")]
    public string TitreJeu { get; set; } = string.Empty;
}
