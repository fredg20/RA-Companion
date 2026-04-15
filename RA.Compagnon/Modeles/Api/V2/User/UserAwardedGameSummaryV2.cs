using System.Text.Json.Serialization;

/*
 * Représente le résumé des récompenses d'un utilisateur pour un jeu dans
 * l'API RetroAchievements v2.
 */
namespace RA.Compagnon.Modeles.Api.V2.User;

/*
 * Transporte le nombre de récompenses obtenues et les niveaux de récompense
 * associés à un jeu.
 */
public sealed class UserAwardedGameSummaryV2
{
    [JsonPropertyName("NumAwarded")]
    public int NumAwarded { get; set; }

    [JsonPropertyName("NumAwardedHardcore")]
    public int NumAwardedHardcore { get; set; }

    [JsonPropertyName("FirstAwardKind")]
    public string FirstAwardKind { get; set; } = string.Empty;

    [JsonPropertyName("HighestAwardKind")]
    public string HighestAwardKind { get; set; } = string.Empty;
}