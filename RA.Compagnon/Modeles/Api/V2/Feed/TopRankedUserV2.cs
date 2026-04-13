using System.Text.Json.Serialization;

namespace RA.Compagnon.Modeles.Api.V2.Feed;

public sealed class TopRankedUserV2
{
    [JsonPropertyName("1")]
    public string UserName { get; set; } = string.Empty;

    [JsonPropertyName("2")]
    public int TotalPoints { get; set; }

    [JsonPropertyName("3")]
    public int TotalRatioPoints { get; set; }

    [JsonPropertyName("4")]
    public string? Ulid { get; set; }
}
