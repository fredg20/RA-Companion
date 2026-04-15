using System.Text.Json.Serialization;
using RA.Compagnon.Modeles.Api.V2.Common;

/*
 * Représente une entrée de jeu appartenant à un système dans l'API
 * RetroAchievements v2.
 */
namespace RA.Compagnon.Modeles.Api.V2.System;

/*
 * Transporte l'identité d'un jeu de système, ses images et ses empreintes.
 */
public sealed class SystemGameEntryV2 : ApiDtoV2Base
{
    [JsonPropertyName("ID")]
    public int Id { get; set; }

    [JsonPropertyName("Title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("ConsoleID")]
    public int ConsoleId { get; set; }

    [JsonPropertyName("ConsoleName")]
    public string ConsoleName { get; set; } = string.Empty;

    [JsonPropertyName("ImageIcon")]
    public string ImageIcon { get; set; } = string.Empty;

    [JsonPropertyName("ImageBoxArt")]
    public string ImageBoxArt { get; set; } = string.Empty;

    [JsonPropertyName("Hashes")]
    public List<string> Hashes { get; set; } = [];
}