using System.Text.Json.Serialization;

namespace RA.Compagnon.Modeles.Api.V2.Game;

public sealed class GameProgressionV2
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

    [JsonPropertyName("NumDistinctPlayers")]
    public int NumDistinctPlayers { get; set; }

    [JsonPropertyName("TimesUsedInBeatMedian")]
    public int TimesUsedInBeatMedian { get; set; }

    [JsonPropertyName("TimesUsedInHardcoreBeatMedian")]
    public int TimesUsedInHardcoreBeatMedian { get; set; }

    [JsonPropertyName("MedianTimeToBeat")]
    public int MedianTimeToBeat { get; set; }

    [JsonPropertyName("MedianTimeToBeatHardcore")]
    public int MedianTimeToBeatHardcore { get; set; }

    [JsonPropertyName("TimesUsedInCompletionMedian")]
    public int TimesUsedInCompletionMedian { get; set; }

    [JsonPropertyName("TimesUsedInMasteryMedian")]
    public int TimesUsedInMasteryMedian { get; set; }

    [JsonPropertyName("MedianTimeToComplete")]
    public int MedianTimeToComplete { get; set; }

    [JsonPropertyName("MedianTimeToMaster")]
    public int MedianTimeToMaster { get; set; }

    [JsonPropertyName("NumAchievements")]
    public int NumAchievements { get; set; }

    [JsonPropertyName("Achievements")]
    public List<GameProgressionAchievementV2> Achievements { get; set; } = [];
}
