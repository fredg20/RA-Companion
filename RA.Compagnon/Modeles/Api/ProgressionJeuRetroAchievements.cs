using System.Text.Json.Serialization;

namespace RA.Compagnon.Modeles.Api;

/// <summary>
/// Représente la progression globale d'un jeu.
/// </summary>
public sealed class ProgressionJeuRetroAchievements : ModeleApiRetroAchievementsBase
{
    [JsonPropertyName("GameID")]
    public int IdentifiantJeu { get; set; }

    [JsonPropertyName("Title")]
    public string TitreJeu { get; set; } = string.Empty;

    [JsonPropertyName("NumRetropoints")]
    public int Retropoints { get; set; }

    [JsonPropertyName("NumPlayers")]
    public int NombreJoueurs { get; set; }

    [JsonPropertyName("NumAchievements")]
    public int NombreSucces { get; set; }
}
