using System.Text.Json.Serialization;

/*
 * Représente le résumé enrichi d'un utilisateur dans l'API
 * RetroAchievements v2.
 */
namespace RA.Compagnon.Modeles.Api.V2.User;

/*
 * Transporte les informations de profil, d'activité récente, de jeux joués
 * et de succès récents d'un utilisateur.
 */
public sealed class UserSummaryV2
{
    [JsonPropertyName("User")]
    public string User { get; set; } = string.Empty;

    [JsonPropertyName("ULID")]
    public string Ulid { get; set; } = string.Empty;

    [JsonPropertyName("MemberSince")]
    public string MemberSince { get; set; } = string.Empty;

    [JsonPropertyName("LastActivity")]
    public UserLastActivityV2 LastActivity { get; set; } = new();

    [JsonPropertyName("RichPresenceMsg")]
    public string RichPresenceMsg { get; set; } = string.Empty;

    [JsonPropertyName("RichPresenceMsgDate")]
    public string RichPresenceMsgDate { get; set; } = string.Empty;

    [JsonPropertyName("LastGameID")]
    public int LastGameId { get; set; }

    [JsonPropertyName("ContribCount")]
    public int ContribCount { get; set; }

    [JsonPropertyName("ContribYield")]
    public int ContribYield { get; set; }

    [JsonPropertyName("TotalPoints")]
    public int TotalPoints { get; set; }

    [JsonPropertyName("TotalSoftcorePoints")]
    public int TotalSoftcorePoints { get; set; }

    [JsonPropertyName("TotalTruePoints")]
    public int TotalTruePoints { get; set; }

    [JsonPropertyName("Permissions")]
    public int Permissions { get; set; }

    [JsonPropertyName("Untracked")]
    public int Untracked { get; set; }

    [JsonPropertyName("ID")]
    public int Id { get; set; }

    [JsonPropertyName("UserWallActive")]
    public int UserWallActive { get; set; }

    [JsonPropertyName("Motto")]
    public string Motto { get; set; } = string.Empty;

    [JsonPropertyName("Rank")]
    public int Rank { get; set; }

    [JsonPropertyName("TotalRanked")]
    public int TotalRanked { get; set; }

    [JsonPropertyName("RecentlyPlayedCount")]
    public int RecentlyPlayedCount { get; set; }

    [JsonPropertyName("RecentlyPlayed")]
    public List<RecentlyPlayedGameV2> RecentlyPlayed { get; set; } = [];

    [JsonPropertyName("Awarded")]
    public Dictionary<string, UserAwardedGameSummaryV2> Awarded { get; set; } = [];

    [JsonPropertyName("RecentAchievements")]
    public Dictionary<
        string,
        Dictionary<string, UserRecentAchievementV2>
    > RecentAchievements { get; set; } = [];

    [JsonPropertyName("LastGame")]
    public LastGameV2? LastGame { get; set; }

    [JsonPropertyName("UserPic")]
    public string UserPic { get; set; } = string.Empty;

    [JsonPropertyName("Status")]
    public string Status { get; set; } = string.Empty;
}