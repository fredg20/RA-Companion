using System.Text.Json.Serialization;

namespace RA.Compagnon.Modeles.Api;

/// <summary>
/// Représente un jeu terminé par l'utilisateur.
/// </summary>
public sealed class JeuCompleteUtilisateurRetroAchievements : ModeleApiRetroAchievementsBase
{
    [JsonPropertyName("GameID")]
    public int IdentifiantJeu { get; set; }

    [JsonPropertyName("Title")]
    public string TitreJeu { get; set; } = string.Empty;

    [JsonPropertyName("ConsoleName")]
    public string NomConsole { get; set; } = string.Empty;

    [JsonPropertyName("DateCompleted")]
    public string DateCompletion { get; set; } = string.Empty;
}
