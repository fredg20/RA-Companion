using System.Text.Json.Serialization;

namespace RA.Compagnon.Modeles.Api;

/// <summary>
/// Représente le succès de la semaine.
/// </summary>
public sealed class SuccesSemaineRetroAchievements : ModeleApiRetroAchievementsBase
{
    [JsonPropertyName("AchievementID")]
    public int IdentifiantSucces { get; set; }

    [JsonPropertyName("Title")]
    public string Titre { get; set; } = string.Empty;

    [JsonPropertyName("Description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("GameID")]
    public int IdentifiantJeu { get; set; }

    [JsonPropertyName("GameTitle")]
    public string TitreJeu { get; set; } = string.Empty;
}
