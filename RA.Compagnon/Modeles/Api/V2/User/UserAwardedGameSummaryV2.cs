using System.Text.Json.Serialization;

namespace RA.Compagnon.Modeles.Api.V2.User;

public sealed class UserAwardedGameSummaryV2
{
    [JsonPropertyName("NumPossibleAchievements")]
    public int NombreSuccesPossibles { get; set; }

    [JsonPropertyName("PossibleScore")]
    public int ScorePossible { get; set; }

    [JsonPropertyName("NumAchieved")]
    public int NombreSuccesObtenus { get; set; }

    [JsonPropertyName("ScoreAchieved")]
    public int ScoreObtenu { get; set; }

    [JsonPropertyName("NumAchievedHardcore")]
    public int NombreSuccesObtenusHardcore { get; set; }

    [JsonPropertyName("ScoreAchievedHardcore")]
    public int ScoreObtenuHardcore { get; set; }
}
