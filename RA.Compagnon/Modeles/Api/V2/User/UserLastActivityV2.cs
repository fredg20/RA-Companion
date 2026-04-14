using System.Text.Json.Serialization;

/*
 * Représente la dernière activité connue d'un utilisateur dans l'API
 * RetroAchievements v2.
 */
namespace RA.Compagnon.Modeles.Api.V2.User;

/*
 * Transporte l'identifiant, le type et l'horodatage de la dernière activité
 * d'un utilisateur.
 */
public sealed class UserLastActivityV2
{
    [JsonPropertyName("ID")]
    public int Identifiant { get; set; }

    [JsonPropertyName("timestamp")]
    public long? Horodatage { get; set; }

    [JsonPropertyName("lastupdate")]
    public string DerniereMiseAJour { get; set; } = string.Empty;

    [JsonPropertyName("activitytype")]
    public string TypeActivite { get; set; } = string.Empty;

    [JsonPropertyName("User")]
    public string NomUtilisateur { get; set; } = string.Empty;
}
