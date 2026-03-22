using System.Text.Json.Serialization;

namespace RA.Compagnon.Modeles.Api;

/// <summary>
/// Représente la réponse de l'API des hashes connus pour un jeu donné.
/// </summary>
public sealed class ReponseHashesJeuRetroAchievements
{
    /// <summary>
    /// Liste des hashes MD5 associés au jeu.
    /// </summary>
    [JsonPropertyName("Results")]
    public List<HashJeuRetroAchievements> Resultats { get; set; } = [];
}
