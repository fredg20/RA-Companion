using System.Text.Json.Serialization;

namespace RA.Compagnon.Modeles.Api.V2.User;

public sealed class UserRecentAchievementV2
{
    [JsonPropertyName("ID")]
    public int IdentifiantSucces { get; set; }

    [JsonPropertyName("GameID")]
    public int IdentifiantJeu { get; set; }

    [JsonPropertyName("GameTitle")]
    public string TitreJeu { get; set; } = string.Empty;

    [JsonPropertyName("Title")]
    public string Titre { get; set; } = string.Empty;

    [JsonPropertyName("Description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("Points")]
    public int Points { get; set; }

    [JsonPropertyName("DateAwarded")]
    public string DateObtention { get; set; } = string.Empty;

    [JsonPropertyName("HardcoreAchieved")]
    public bool ModeHardcore { get; set; }
}
