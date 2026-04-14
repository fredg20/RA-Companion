using System.Text.Json.Serialization;

/*
 * Représente un succès récent d'utilisateur dans l'API RetroAchievements v2.
 */
namespace RA.Compagnon.Modeles.Api.V2.User;

/*
 * Transporte les informations essentielles d'un succès récemment obtenu par
 * un utilisateur.
 */
public sealed class UserRecentAchievementV2
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

    [JsonPropertyName("BadgeName")]
    public string BadgeName { get; set; } = string.Empty;

    [JsonPropertyName("DateAwarded")]
    public string DateAwarded { get; set; } = string.Empty;
}
