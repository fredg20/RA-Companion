using System.Text.Json.Serialization;

/*
 * Représente une récompense de jeu récente issue du flux d'activité
 * RetroAchievements v2.
 */
namespace RA.Compagnon.Modeles.Api.V2.Feed;

/*
 * Transporte l'utilisateur, le jeu et le type de récompense associés à une
 * entrée récente du flux.
 */
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