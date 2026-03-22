using System.Text.Json.Serialization;

namespace RA.Compagnon.Modeles.Api;

/// <summary>
/// Représente la présence d'un utilisateur dans un leaderboard de jeu.
/// </summary>
public sealed class LeaderboardUtilisateurJeuRetroAchievements : ModeleApiRetroAchievementsBase
{
    [JsonPropertyName("LeaderboardID")]
    public int IdentifiantLeaderboard { get; set; }

    [JsonPropertyName("Title")]
    public string Titre { get; set; } = string.Empty;

    [JsonPropertyName("BestScore")]
    public int MeilleurScore { get; set; }

    [JsonPropertyName("BestRank")]
    public int MeilleurRang { get; set; }
}
