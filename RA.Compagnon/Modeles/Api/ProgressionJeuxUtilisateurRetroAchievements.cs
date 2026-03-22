using System.Text.Json.Serialization;

namespace RA.Compagnon.Modeles.Api;

/// <summary>
/// Représente la progression d'un utilisateur sur un jeu précis dans les endpoints de progression ciblée.
/// </summary>
public sealed class ProgressionJeuxUtilisateurRetroAchievements : ModeleApiRetroAchievementsBase
{
    [JsonPropertyName("GameID")]
    public int IdentifiantJeu { get; set; }

    [JsonPropertyName("Title")]
    public string TitreJeu { get; set; } = string.Empty;

    [JsonPropertyName("NumPossibleAchievements")]
    public int NombreSuccesPossibles { get; set; }

    [JsonPropertyName("NumAchieved")]
    public int NombreSuccesObtenus { get; set; }

    [JsonPropertyName("NumAchievedHardcore")]
    public int NombreSuccesObtenusHardcore { get; set; }
}
