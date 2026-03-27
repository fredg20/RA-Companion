using System.Text.Json.Serialization;

namespace RA.Compagnon.Modeles.Api.V2.User;

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
