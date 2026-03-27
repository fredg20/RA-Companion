using System.Text.Json.Serialization;

namespace RA.Compagnon.Modeles.Api.V2.User;

public sealed class VisibleUserAwardV2
{
    [JsonPropertyName("AwardedAt")]
    public string AwardedAt { get; set; } = string.Empty;

    [JsonPropertyName("AwardType")]
    public string AwardType { get; set; } = string.Empty;

    [JsonPropertyName("AwardData")]
    public int AwardData { get; set; }

    [JsonPropertyName("AwardDataExtra")]
    public int AwardDataExtra { get; set; }

    [JsonPropertyName("DisplayOrder")]
    public int DisplayOrder { get; set; }

    [JsonPropertyName("Title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("ConsoleID")]
    public int ConsoleId { get; set; }

    [JsonPropertyName("ConsoleName")]
    public string ConsoleName { get; set; } = string.Empty;

    [JsonPropertyName("Flags")]
    public int Flags { get; set; }

    [JsonPropertyName("ImageIcon")]
    public string ImageIcon { get; set; } = string.Empty;
}
