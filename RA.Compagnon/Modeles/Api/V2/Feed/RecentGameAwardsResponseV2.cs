using System.Text.Json.Serialization;

namespace RA.Compagnon.Modeles.Api.V2.Feed;

/// <summary>
/// Représente une page de récompenses de jeu récentes.
/// </summary>
public sealed class RecentGameAwardsResponseV2
{
    [JsonPropertyName("Count")]
    public int Count { get; set; }

    [JsonPropertyName("Total")]
    public int Total { get; set; }

    [JsonPropertyName("Results")]
    public List<RecentGameAwardV2> Results { get; set; } = [];
}
