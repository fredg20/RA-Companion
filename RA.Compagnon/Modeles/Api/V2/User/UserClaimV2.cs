using System.Text.Json.Serialization;

/*
 * Représente un claim utilisateur sur un jeu dans l'API RetroAchievements v2.
 */
namespace RA.Compagnon.Modeles.Api.V2.User;

/*
 * Transporte les informations d'un claim, son statut et le jeu concerné.
 */
public sealed class UserClaimV2
{
    [JsonPropertyName("ID")]
    public int Id { get; set; }

    [JsonPropertyName("User")]
    public string User { get; set; } = string.Empty;

    [JsonPropertyName("ULID")]
    public string Ulid { get; set; } = string.Empty;

    [JsonPropertyName("GameID")]
    public int GameId { get; set; }

    [JsonPropertyName("GameTitle")]
    public string GameTitle { get; set; } = string.Empty;

    [JsonPropertyName("GameIcon")]
    public string GameIcon { get; set; } = string.Empty;

    [JsonPropertyName("ConsoleID")]
    public int ConsoleId { get; set; }

    [JsonPropertyName("ConsoleName")]
    public string ConsoleName { get; set; } = string.Empty;

    [JsonPropertyName("ClaimType")]
    public int ClaimType { get; set; }

    [JsonPropertyName("SetType")]
    public int SetType { get; set; }

    [JsonPropertyName("Status")]
    public int Status { get; set; }

    [JsonPropertyName("Extension")]
    public int Extension { get; set; }

    [JsonPropertyName("Special")]
    public int Special { get; set; }

    [JsonPropertyName("Created")]
    public string Created { get; set; } = string.Empty;

    [JsonPropertyName("DoneTime")]
    public string DoneTime { get; set; } = string.Empty;

    [JsonPropertyName("Updated")]
    public string Updated { get; set; } = string.Empty;

    [JsonPropertyName("UserIsJrDev")]
    public int UserIsJrDev { get; set; }

    [JsonPropertyName("MinutesLeft")]
    public int MinutesLeft { get; set; }
}
