using System.Text.Json.Serialization;

namespace RA.Compagnon.Modeles.Api;

/// <summary>
/// Représente les points d'un utilisateur.
/// </summary>
public sealed class PointsUtilisateurRetroAchievements : ModeleApiRetroAchievementsBase
{
    [JsonPropertyName("User")]
    public string Utilisateur { get; set; } = string.Empty;

    [JsonPropertyName("Points")]
    public int Points { get; set; }

    [JsonPropertyName("SoftcorePoints")]
    public int PointsSoftcore { get; set; }

    [JsonPropertyName("TruePoints")]
    public int TruePoints { get; set; }
}
