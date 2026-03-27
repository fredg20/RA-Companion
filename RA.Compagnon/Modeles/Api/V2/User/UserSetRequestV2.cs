using System.Text.Json.Serialization;

namespace RA.Compagnon.Modeles.Api.V2.User;

public sealed class UserSetRequestV2
{
    [JsonPropertyName("GameID")]
    public int GameId { get; set; }

    [JsonPropertyName("Title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("ConsoleID")]
    public int ConsoleId { get; set; }

    [JsonPropertyName("ConsoleName")]
    public string ConsoleName { get; set; } = string.Empty;

    [JsonPropertyName("ImageIcon")]
    public string ImageIcon { get; set; } = string.Empty;
}
