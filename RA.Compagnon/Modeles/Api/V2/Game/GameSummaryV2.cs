using System.Text.Json.Serialization;

namespace RA.Compagnon.Modeles.Api.V2.Game;

public sealed class GameSummaryV2
{
    [JsonPropertyName("Title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("GameTitle")]
    public string GameTitle { get; set; } = string.Empty;

    [JsonPropertyName("ConsoleID")]
    public int ConsoleId { get; set; }

    [JsonPropertyName("ConsoleName")]
    public string ConsoleName { get; set; } = string.Empty;

    [JsonPropertyName("Console")]
    public string Console { get; set; } = string.Empty;

    [JsonPropertyName("ForumTopicID")]
    public int ForumTopicId { get; set; }

    [JsonPropertyName("Flags")]
    public int? Flags { get; set; }

    [JsonPropertyName("GameIcon")]
    public string GameIcon { get; set; } = string.Empty;

    [JsonPropertyName("ImageIcon")]
    public string ImageIcon { get; set; } = string.Empty;

    [JsonPropertyName("ImageTitle")]
    public string ImageTitle { get; set; } = string.Empty;

    [JsonPropertyName("ImageIngame")]
    public string ImageIngame { get; set; } = string.Empty;

    [JsonPropertyName("ImageBoxArt")]
    public string ImageBoxArt { get; set; } = string.Empty;

    [JsonPropertyName("Publisher")]
    public string Publisher { get; set; } = string.Empty;

    [JsonPropertyName("Developer")]
    public string Developer { get; set; } = string.Empty;

    [JsonPropertyName("Genre")]
    public string Genre { get; set; } = string.Empty;

    [JsonPropertyName("Released")]
    public string Released { get; set; } = string.Empty;

    [JsonPropertyName("ReleasedAtGranularity")]
    public string ReleasedAtGranularity { get; set; } = string.Empty;
}
