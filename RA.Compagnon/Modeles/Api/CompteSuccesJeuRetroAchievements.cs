using System.Text.Json.Serialization;

namespace RA.Compagnon.Modeles.Api;

/// <summary>
/// Représente les identifiants de succès d'un jeu.
/// </summary>
public sealed class CompteSuccesJeuRetroAchievements : ModeleApiRetroAchievementsBase
{
    [JsonPropertyName("GameID")]
    public int IdentifiantJeu { get; set; }

    [JsonPropertyName("AchievementIDs")]
    public List<int> IdentifiantsSucces { get; set; } = [];
}
