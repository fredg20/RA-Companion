using System.Text.Json.Serialization;

namespace RA.Compagnon.Modeles.Api;

/// <summary>
/// Représente le classement d'un jeu.
/// </summary>
public sealed class ClassementScoreJeuRetroAchievements : ModeleApiRetroAchievementsBase
{
    [JsonPropertyName("Rank")]
    public int Rang { get; set; }

    [JsonPropertyName("User")]
    public string Utilisateur { get; set; } = string.Empty;

    [JsonPropertyName("Score")]
    public int Score { get; set; }
}
