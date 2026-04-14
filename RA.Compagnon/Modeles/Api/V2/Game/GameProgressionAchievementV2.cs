using System.Text.Json.Serialization;

/*
 * Représente les statistiques de progression d'un succès dans l'API
 * RetroAchievements v2.
 */
namespace RA.Compagnon.Modeles.Api.V2.Game;

/*
 * Transporte les métriques médianes et les taux de déblocage associés à un
 * succès de jeu.
 */
public sealed class GameProgressionAchievementV2
{
    [JsonPropertyName("ID")]
    public int Id { get; set; }

    [JsonPropertyName("Title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("Description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("Points")]
    public int Points { get; set; }

    [JsonPropertyName("TrueRatio")]
    public double TrueRatio { get; set; }

    [JsonPropertyName("Type")]
    public string? Type { get; set; }

    [JsonPropertyName("BadgeName")]
    public string BadgeName { get; set; } = string.Empty;

    [JsonPropertyName("NumAwarded")]
    public int NumAwarded { get; set; }

    [JsonPropertyName("NumAwardedHardcore")]
    public int NumAwardedHardcore { get; set; }

    [JsonPropertyName("TimesUsedInUnlockMedian")]
    public int TimesUsedInUnlockMedian { get; set; }

    [JsonPropertyName("TimesUsedInHardcoreUnlockMedian")]
    public int TimesUsedInHardcoreUnlockMedian { get; set; }

    [JsonPropertyName("MedianTimeToUnlock")]
    public int MedianTimeToUnlock { get; set; }

    [JsonPropertyName("MedianTimeToUnlockHardcore")]
    public int MedianTimeToUnlockHardcore { get; set; }
}
