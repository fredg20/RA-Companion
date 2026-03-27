using System.Text.Json.Serialization;

namespace RA.Compagnon.Modeles.Api.V2.Feed;

/// <summary>
/// Représente une récompense de jeu récemment attribuée sur le site.
/// </summary>
public sealed class RecentGameAwardV2
{
    [JsonPropertyName("User")]
    public string User { get; set; } = string.Empty;

    [JsonPropertyName("ULID")]
    public string? Ulid { get; set; }

    [JsonPropertyName("AwardKind")]
    public string AwardKind { get; set; } = string.Empty;

    [JsonPropertyName("AwardDate")]
    public DateTimeOffset? AwardDate { get; set; }

    [JsonPropertyName("GameID")]
    public int GameId { get; set; }

    [JsonPropertyName("GameTitle")]
    public string GameTitle { get; set; } = string.Empty;

    [JsonPropertyName("ConsoleID")]
    public int ConsoleId { get; set; }

    [JsonPropertyName("ConsoleName")]
    public string ConsoleName { get; set; } = string.Empty;
}
