using System.Text.Json.Serialization;

namespace RA.Compagnon.Modeles.Api.V2.System;

/// <summary>
/// Représente un système RetroAchievements.
/// </summary>
public sealed class SystemEntryV2
{
    [JsonPropertyName("ID")]
    public int Id { get; set; }

    [JsonPropertyName("Name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("IconURL")]
    public string IconUrl { get; set; } = string.Empty;

    [JsonPropertyName("Active")]
    public bool? Active { get; set; }

    [JsonPropertyName("IsGameSystem")]
    public bool? IsGameSystem { get; set; }
}
