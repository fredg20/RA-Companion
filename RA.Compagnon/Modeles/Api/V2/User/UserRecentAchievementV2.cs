using System.Text.Json.Serialization;

namespace RA.Compagnon.Modeles.Api.V2.User;

public sealed class UserRecentAchievementV2
{
    [JsonPropertyName("ID")]
    public int Id { get; set; }

    [JsonPropertyName("Title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("Description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("Points")]
    public int Points { get; set; }

    [JsonPropertyName("TrueRatio")]
    public double TrueRatio { get; set; }

    [JsonPropertyName("BadgeName")]
    public string BadgeName { get; set; } = string.Empty;

    [JsonPropertyName("DateAwarded")]
    public string DateAwarded { get; set; } = string.Empty;
}
