using System.Text.Json.Serialization;

/*
 * Représente une entrée de progression utilisateur pour un jeu dans l'API
 * RetroAchievements v2.
 */
namespace RA.Compagnon.Modeles.Api.V2.User;

/*
 * Transporte les compteurs de succès et de score obtenus en normal et en
 * hardcore pour un jeu.
 */
public sealed class UserGameProgressEntryV2
{
    [JsonPropertyName("NumPossibleAchievements")]
    public int NumPossibleAchievements { get; set; }

    [JsonPropertyName("PossibleScore")]
    public int PossibleScore { get; set; }

    [JsonPropertyName("NumAchieved")]
    public int NumAchieved { get; set; }

    [JsonPropertyName("ScoreAchieved")]
    public int ScoreAchieved { get; set; }

    [JsonPropertyName("NumAchievedHardcore")]
    public int NumAchievedHardcore { get; set; }

    [JsonPropertyName("ScoreAchievedHardcore")]
    public int ScoreAchievedHardcore { get; set; }
}