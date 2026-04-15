using System.Text.Json.Serialization;

/*
 * Représente les points utilisateur retournés par l'API RetroAchievements v2.
 */
namespace RA.Compagnon.Modeles.Api.V2.User;

/*
 * Transporte les points totaux et softcore d'un utilisateur.
 */
public sealed class UserPointsV2
{
    [JsonPropertyName("Points")]
    public int Points { get; set; }

    [JsonPropertyName("SoftcorePoints")]
    public int SoftcorePoints { get; set; }
}