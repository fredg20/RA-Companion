using System.Text.Json.Serialization;

namespace RA.Compagnon.Modeles.Api.V2.User;

public sealed class UserProfileV2
{
    [JsonPropertyName("User")]
    public string User { get; set; } = string.Empty;

    [JsonPropertyName("ULID")]
    public string Ulid { get; set; } = string.Empty;

    [JsonPropertyName("UserPic")]
    public string UserPic { get; set; } = string.Empty;

    [JsonPropertyName("MemberSince")]
    public string MemberSince { get; set; } = string.Empty;

    [JsonPropertyName("RichPresenceMsg")]
    public string RichPresenceMsg { get; set; } = string.Empty;

    [JsonPropertyName("LastGameID")]
    public int LastGameId { get; set; }

    [JsonPropertyName("LastGame")]
    public string LastGame { get; set; } = string.Empty;

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
}
