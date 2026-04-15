using System.Text.Json.Serialization;

/*
 * Représente une demande de set utilisateur dans l'API RetroAchievements v2.
 */
namespace RA.Compagnon.Modeles.Api.V2.User;

/*
 * Transporte l'identité du jeu demandé, sa console et son image.
 */
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