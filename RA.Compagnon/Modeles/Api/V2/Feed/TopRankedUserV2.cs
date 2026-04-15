using System.Text.Json.Serialization;

/*
 * Représente une entrée de classement utilisateur issue de certains flux
 * RetroAchievements v2.
 */
namespace RA.Compagnon.Modeles.Api.V2.Feed;

/*
 * Transporte le nom, les points et l'identifiant ULID d'un utilisateur classé.
 */
public sealed class TopRankedUserV2
{
    [JsonPropertyName("1")]
    public string UserName { get; set; } = string.Empty;

    [JsonPropertyName("2")]
    public int TotalPoints { get; set; }

    [JsonPropertyName("3")]
    public int TotalRatioPoints { get; set; }

    [JsonPropertyName("4")]
    public string? Ulid { get; set; }
}