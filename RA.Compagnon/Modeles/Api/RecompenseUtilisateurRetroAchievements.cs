using System.Text.Json.Serialization;

namespace RA.Compagnon.Modeles.Api;

/// <summary>
/// Représente une récompense ou un badge utilisateur.
/// </summary>
public sealed class RecompenseUtilisateurRetroAchievements : ModeleApiRetroAchievementsBase
{
    [JsonPropertyName("AwardedAt")]
    public string DateObtention { get; set; } = string.Empty;

    [JsonPropertyName("AwardType")]
    public string TypeRecompense { get; set; } = string.Empty;

    [JsonPropertyName("ConsoleName")]
    public string NomConsole { get; set; } = string.Empty;

    [JsonPropertyName("GameID")]
    public int IdentifiantJeu { get; set; }

    [JsonPropertyName("GameTitle")]
    public string TitreJeu { get; set; } = string.Empty;

    [JsonPropertyName("ImageIcon")]
    public string CheminImageIcone { get; set; } = string.Empty;
}
