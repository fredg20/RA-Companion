using System.Text.Json.Serialization;

namespace RA.Compagnon.Modeles.Api;

/// <summary>
/// Représente les champs utiles du endpoint User Summary pour l'en-tête de la modale utilisateur.
/// </summary>
public sealed class ResumeUtilisateurRetroAchievements
{
    /// <summary>
    /// Date d'inscription du compte.
    /// </summary>
    [JsonPropertyName("MemberSince")]
    public string MembreDepuis { get; set; } = string.Empty;

    /// <summary>
    /// Total de points du compte.
    /// </summary>
    [JsonPropertyName("TotalPoints")]
    public int PointsTotaux { get; set; }

    /// <summary>
    /// Total de true points du compte.
    /// </summary>
    [JsonPropertyName("TotalTruePoints")]
    public int TruePointsTotaux { get; set; }

    /// <summary>
    /// Rang global de l'utilisateur.
    /// </summary>
    [JsonPropertyName("Rank")]
    public int Rang { get; set; }

    /// <summary>
    /// Nombre total de comptes classés.
    /// </summary>
    [JsonPropertyName("TotalRanked")]
    public int TotalClasses { get; set; }

    /// <summary>
    /// Dernière activité connue.
    /// </summary>
    [JsonPropertyName("LastActivity")]
    public ActiviteUtilisateurRetroAchievements DerniereActivite { get; set; } = new();
}
