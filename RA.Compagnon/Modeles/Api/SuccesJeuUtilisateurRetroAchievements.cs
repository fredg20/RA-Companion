using System.Text.Json.Serialization;

namespace RA.Compagnon.Modeles.Api;

/// <summary>
/// Représente un succès du jeu retourné avec la progression utilisateur.
/// </summary>
public sealed class SuccesJeuUtilisateurRetroAchievements
{
    [JsonPropertyName("ID")]
    public int IdentifiantSucces { get; set; }

    [JsonPropertyName("Title")]
    public string Titre { get; set; } = string.Empty;

    [JsonPropertyName("Description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("Points")]
    public int Points { get; set; }

    [JsonPropertyName("TrueRatio")]
    public int Retropoints { get; set; }

    [JsonPropertyName("BadgeName")]
    public string NomBadge { get; set; } = string.Empty;

    [JsonPropertyName("DisplayOrder")]
    public int OrdreAffichage { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("DateEarned")]
    public string DateObtention { get; set; } = string.Empty;

    [JsonPropertyName("DateEarnedHardcore")]
    public string DateObtentionHardcore { get; set; } = string.Empty;
}
