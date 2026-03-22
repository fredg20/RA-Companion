using System.Text.Json.Serialization;

namespace RA.Compagnon.Modeles.Api;

/// <summary>
/// Représente une relation d'utilisateur suivie ou suiveur.
/// </summary>
public sealed class RelationUtilisateurRetroAchievements : ModeleApiRetroAchievementsBase
{
    [JsonPropertyName("User")]
    public string Utilisateur { get; set; } = string.Empty;

    [JsonPropertyName("ULID")]
    public string Ulid { get; set; } = string.Empty;

    [JsonPropertyName("UserPic")]
    public string Avatar { get; set; } = string.Empty;

    [JsonPropertyName("RichPresence")]
    public string PresenceRiche { get; set; } = string.Empty;
}
