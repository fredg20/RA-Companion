using System.Text.Json.Serialization;

/*
 * Représente une entrée de système dans l'API RetroAchievements v2.
 */
namespace RA.Compagnon.Modeles.Api.V2.System;

/*
 * Transporte l'identité, le nom et l'état d'activation d'un système.
 */
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
