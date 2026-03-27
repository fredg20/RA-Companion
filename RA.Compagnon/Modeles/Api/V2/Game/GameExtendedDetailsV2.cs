using System.Text.Json.Serialization;
using RA.Compagnon.Modeles.Api.V2.User;

namespace RA.Compagnon.Modeles.Api.V2.Game;

public sealed class GameExtendedDetailsV2
{
    [JsonPropertyName("ID")]
    public int Id { get; set; }

    [JsonPropertyName("Title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("ConsoleID")]
    public int ConsoleId { get; set; }

    [JsonPropertyName("ForumTopicID")]
    public int ForumTopicId { get; set; }

    [JsonPropertyName("Flags")]
    public int? Flags { get; set; }

    [JsonPropertyName("ImageIcon")]
    public string ImageIcon { get; set; } = string.Empty;

    [JsonPropertyName("ImageTitle")]
    public string ImageTitle { get; set; } = string.Empty;

    [JsonPropertyName("ImageIngame")]
    public string ImageIngame { get; set; } = string.Empty;

    [JsonPropertyName("ImageBoxArt")]
    public string ImageBoxArt { get; set; } = string.Empty;

    [JsonPropertyName("Publisher")]
    public string Publisher { get; set; } = string.Empty;

    [JsonPropertyName("Developer")]
    public string Developer { get; set; } = string.Empty;

    [JsonPropertyName("Genre")]
    public string Genre { get; set; } = string.Empty;

    [JsonPropertyName("Released")]
    public string Released { get; set; } = string.Empty;

    [JsonPropertyName("ReleasedAtGranularity")]
    public string ReleasedAtGranularity { get; set; } = string.Empty;

    [JsonPropertyName("IsFinal")]
    public bool IsFinal { get; set; }

    [JsonPropertyName("RichPresencePatch")]
    public string RichPresencePatch { get; set; } = string.Empty;

    [JsonPropertyName("GuideURL")]
    public string? GuideUrl { get; set; }

    [JsonPropertyName("Updated")]
    public string Updated { get; set; } = string.Empty;

    [JsonPropertyName("ConsoleName")]
    public string ConsoleName { get; set; } = string.Empty;

    [JsonPropertyName("ParentGameID")]
    public int? ParentGameId { get; set; }

    [JsonPropertyName("NumDistinctPlayers")]
    public int NumDistinctPlayers { get; set; }

    [JsonPropertyName("NumAchievements")]
    public int NumAchievements { get; set; }

    [JsonPropertyName("Achievements")]
    public Dictionary<string, GameAchievementV2> Achievements { get; set; } = [];

    [JsonPropertyName("Claims")]
    public List<UserClaimV2> Claims { get; set; } = [];

    [JsonPropertyName("NumDistinctPlayersCasual")]
    public int NumDistinctPlayersCasual { get; set; }

    [JsonPropertyName("NumDistinctPlayersHardcore")]
    public int NumDistinctPlayersHardcore { get; set; }
}
