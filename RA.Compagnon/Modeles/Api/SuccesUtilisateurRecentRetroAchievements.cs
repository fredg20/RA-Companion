using System.Text.Json.Serialization;

namespace RA.Compagnon.Modeles.Api;

/// <summary>
/// Représente un succès utilisateur pour les endpoints de succès récents ou d'un jour donné.
/// </summary>
public sealed class SuccesUtilisateurRecentRetroAchievements : ModeleApiRetroAchievementsBase
{
    [JsonPropertyName("AchievementID")]
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

    [JsonPropertyName("DateAwardedHardcore")]
    public string DateObtentionHardcore { get; set; } = string.Empty;
}
