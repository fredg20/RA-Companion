using System.Text.Json.Serialization;

namespace RA.Compagnon.Modeles.Api;

/// <summary>
/// Représente une entrée de leaderboard.
/// </summary>
public sealed class EntreeLeaderboardRetroAchievements : ModeleApiRetroAchievementsBase
{
    [JsonPropertyName("Rank")]
    public int Rang { get; set; }

    [JsonPropertyName("User")]
    public string Utilisateur { get; set; } = string.Empty;

    [JsonPropertyName("Score")]
    public int Score { get; set; }

    [JsonPropertyName("DateSubmitted")]
    public string DateSoumission { get; set; } = string.Empty;
}
