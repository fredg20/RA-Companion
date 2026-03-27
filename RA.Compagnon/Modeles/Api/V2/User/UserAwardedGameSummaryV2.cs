using System.Text.Json.Serialization;

namespace RA.Compagnon.Modeles.Api.V2.User;

public sealed class UserAwardedGameSummaryV2
{
    [JsonPropertyName("NumAwarded")]
    public int NumAwarded { get; set; }

    [JsonPropertyName("NumAwardedHardcore")]
    public int NumAwardedHardcore { get; set; }

    [JsonPropertyName("FirstAwardKind")]
    public string FirstAwardKind { get; set; } = string.Empty;

    [JsonPropertyName("HighestAwardKind")]
    public string HighestAwardKind { get; set; } = string.Empty;
}
