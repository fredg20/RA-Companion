using System.Text.Json.Serialization;

namespace RA.Compagnon.Modeles.Api;

/// <summary>
/// Représente le classement et le score d'un utilisateur sur un jeu.
/// </summary>
public sealed class ClassementScoreJeuUtilisateurRetroAchievements : ModeleApiRetroAchievementsBase
{
    [JsonPropertyName("GameID")]
    public int IdentifiantJeu { get; set; }

    [JsonPropertyName("User")]
    public string Utilisateur { get; set; } = string.Empty;

    [JsonPropertyName("Rank")]
    public int Rang { get; set; }

    [JsonPropertyName("Score")]
    public int Score { get; set; }
}
