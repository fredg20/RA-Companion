using System.Text.Json.Serialization;

namespace RA.Compagnon.Modeles.Api;

/// <summary>
/// Représente la dernière activité retournée par le endpoint User Summary.
/// </summary>
public sealed class ActiviteUtilisateurRetroAchievements
{
    /// <summary>
    /// Horodatage Unix éventuel de l'activité.
    /// </summary>
    [JsonPropertyName("timestamp")]
    public long? Horodatage { get; set; }

    /// <summary>
    /// Date de dernière mise à jour fournie par l'API.
    /// </summary>
    [JsonPropertyName("lastupdate")]
    public string DerniereMiseAJour { get; set; } = string.Empty;

    /// <summary>
    /// Type d'activité si disponible.
    /// </summary>
    [JsonPropertyName("activitytype")]
    public string TypeActivite { get; set; } = string.Empty;
}
