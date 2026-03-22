using System.Text.Json.Serialization;

namespace RA.Compagnon.Modeles.Api;

/// <summary>
/// Représente la progression de complétion d'un jeu pour un utilisateur.
/// </summary>
public sealed class ProgressionCompletionUtilisateurRetroAchievements
    : ModeleApiRetroAchievementsBase
{
    [JsonPropertyName("GameID")]
    public int IdentifiantJeu { get; set; }

    [JsonPropertyName("Title")]
    public string TitreJeu { get; set; } = string.Empty;

    [JsonPropertyName("ConsoleName")]
    public string NomConsole { get; set; } = string.Empty;

    [JsonPropertyName("ImageIcon")]
    public string CheminImageIcone { get; set; } = string.Empty;

    [JsonPropertyName("MaxPossible")]
    public int MaximumPossible { get; set; }

    [JsonPropertyName("NumAwarded")]
    public int NombreObtenus { get; set; }

    [JsonPropertyName("NumAwardedHardcore")]
    public int NombreObtenusHardcore { get; set; }
}
