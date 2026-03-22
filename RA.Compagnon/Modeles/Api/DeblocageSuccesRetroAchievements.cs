using System.Text.Json.Serialization;

namespace RA.Compagnon.Modeles.Api;

/// <summary>
/// Représente un utilisateur ayant débloqué un succès.
/// </summary>
public sealed class DeblocageSuccesRetroAchievements : ModeleApiRetroAchievementsBase
{
    [JsonPropertyName("User")]
    public string Utilisateur { get; set; } = string.Empty;

    [JsonPropertyName("DateAwarded")]
    public string DateObtention { get; set; } = string.Empty;

    [JsonPropertyName("HardcoreMode")]
    public bool ModeHardcore { get; set; }
}
