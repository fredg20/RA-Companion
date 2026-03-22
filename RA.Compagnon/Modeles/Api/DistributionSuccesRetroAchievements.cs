using System.Text.Json.Serialization;

namespace RA.Compagnon.Modeles.Api;

/// <summary>
/// Représente la distribution de déblocage d'un succès.
/// </summary>
public sealed class DistributionSuccesRetroAchievements : ModeleApiRetroAchievementsBase
{
    [JsonPropertyName("AchievementID")]
    public int IdentifiantSucces { get; set; }

    [JsonPropertyName("Title")]
    public string Titre { get; set; } = string.Empty;

    [JsonPropertyName("NumAwarded")]
    public int NombreObtentions { get; set; }

    [JsonPropertyName("NumAwardedHardcore")]
    public int NombreObtentionsHardcore { get; set; }
}
