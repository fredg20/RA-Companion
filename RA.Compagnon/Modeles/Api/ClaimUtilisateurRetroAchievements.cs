using System.Text.Json.Serialization;

namespace RA.Compagnon.Modeles.Api;

/// <summary>
/// Représente une claim utilisateur ou une claim de set.
/// </summary>
public sealed class ClaimUtilisateurRetroAchievements : ModeleApiRetroAchievementsBase
{
    [JsonPropertyName("ID")]
    public int IdentifiantClaim { get; set; }

    [JsonPropertyName("GameID")]
    public int IdentifiantJeu { get; set; }

    [JsonPropertyName("GameTitle")]
    public string TitreJeu { get; set; } = string.Empty;

    [JsonPropertyName("User")]
    public string Utilisateur { get; set; } = string.Empty;

    [JsonPropertyName("Created")]
    public string DateCreation { get; set; } = string.Empty;

    [JsonPropertyName("Finished")]
    public string DateFin { get; set; } = string.Empty;

    [JsonPropertyName("ClaimType")]
    public string TypeClaim { get; set; } = string.Empty;
}
