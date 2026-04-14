using System.Text.Json.Serialization;

/*
 * Représente la réponse des demandes de sets utilisateur dans l'API
 * RetroAchievements v2.
 */
namespace RA.Compagnon.Modeles.Api.V2.User;

/*
 * Transporte la liste des sets demandés par l'utilisateur.
 */
public sealed class UserSetRequestsResponseV2
{
    [JsonPropertyName("RequestedSets")]
    public List<UserSetRequestV2> RequestedSets { get; set; } = [];
}
