using System.Text.Json.Serialization;

namespace RA.Compagnon.Modeles.Api;

/// <summary>
/// Représente un hash officiellement associé à un jeu RetroAchievements.
/// </summary>
public sealed class HashJeuRetroAchievements
{
    /// <summary>
    /// Empreinte MD5 du fichier connu par RetroAchievements.
    /// </summary>
    [JsonPropertyName("MD5")]
    public string EmpreinteMd5 { get; set; } = string.Empty;
}
