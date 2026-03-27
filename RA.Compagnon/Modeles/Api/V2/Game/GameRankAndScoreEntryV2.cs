using System.Text.Json.Serialization;

namespace RA.Compagnon.Modeles.Api.V2.Game;

public sealed class GameRankAndScoreEntryV2
{
    [JsonPropertyName("User")]
    public string User { get; set; } = string.Empty;

    [JsonPropertyName("ULID")]
    public string Ulid { get; set; } = string.Empty;

    [JsonPropertyName("UserRank")]
    public int UserRank { get; set; }

    [JsonPropertyName("TotalScore")]
    public int TotalScore { get; set; }

    [JsonPropertyName("LastAward")]
    public string LastAward { get; set; } = string.Empty;
}
