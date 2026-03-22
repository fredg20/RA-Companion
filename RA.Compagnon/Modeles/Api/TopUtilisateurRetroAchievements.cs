using System.Text.Json.Serialization;

namespace RA.Compagnon.Modeles.Api;

/// <summary>
/// Représente un utilisateur du top classé.
/// </summary>
public sealed class TopUtilisateurRetroAchievements : ModeleApiRetroAchievementsBase
{
    [JsonPropertyName("User")]
    public string Utilisateur { get; set; } = string.Empty;

    [JsonPropertyName("Rank")]
    public int Rang { get; set; }

    [JsonPropertyName("Points")]
    public int Points { get; set; }

    [JsonPropertyName("TruePoints")]
    public int TruePoints { get; set; }
}
