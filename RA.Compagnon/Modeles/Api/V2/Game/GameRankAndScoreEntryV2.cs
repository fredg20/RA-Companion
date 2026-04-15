using System.Text.Json.Serialization;

/*
 * Représente une entrée de classement ou de score pour un jeu dans l'API
 * RetroAchievements v2.
 */
namespace RA.Compagnon.Modeles.Api.V2.Game;

/*
 * Transporte le rang, le score total et la dernière récompense d'un joueur
 * pour un jeu donné.
 */
public sealed class GameRankAndScoreEntryV2
{
    [JsonPropertyName("User")]
    public string User { get; set; } = string.Empty;

    [JsonPropertyName("ULID")]
    public string Ulid { get; set; } = string.Empty;

    [JsonPropertyName("UserRank")]
    public int UserRank { get; set; }

    [JsonPropertyName("TotalScore")]
    public int TotalScore { get; set; }

    [JsonPropertyName("LastAward")]
    public string LastAward { get; set; } = string.Empty;
}