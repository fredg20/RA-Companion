using System.Text.Json.Serialization;

/*
 * Représente la réponse du flux des récompenses de jeux récentes dans l'API
 * RetroAchievements v2.
 */
namespace RA.Compagnon.Modeles.Api.V2.Feed;

/*
 * Transporte le nombre total et la liste des récompenses de jeux récentes.
 */
public sealed class RecentGameAwardsResponseV2
{
    [JsonPropertyName("Count")]
    public int Count { get; set; }

    [JsonPropertyName("Total")]
    public int Total { get; set; }

    [JsonPropertyName("Results")]
    public List<RecentGameAwardV2> Results { get; set; } = [];
}