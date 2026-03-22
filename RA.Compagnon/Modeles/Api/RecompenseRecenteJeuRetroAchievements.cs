using System.Text.Json.Serialization;

namespace RA.Compagnon.Modeles.Api;

/// <summary>
/// Représente une récompense récente liée à un jeu.
/// </summary>
public sealed class RecompenseRecenteJeuRetroAchievements : ModeleApiRetroAchievementsBase
{
    [JsonPropertyName("User")]
    public string Utilisateur { get; set; } = string.Empty;

    [JsonPropertyName("GameID")]
    public int IdentifiantJeu { get; set; }

    [JsonPropertyName("GameTitle")]
    public string TitreJeu { get; set; } = string.Empty;

    [JsonPropertyName("AwardKind")]
    public string TypeRecompense { get; set; } = string.Empty;

    [JsonPropertyName("AwardedAt")]
    public string DateObtention { get; set; } = string.Empty;
}
