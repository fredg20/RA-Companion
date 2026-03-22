using System.Text.Json.Serialization;

namespace RA.Compagnon.Modeles.Api;

/// <summary>
/// Représente un commentaire retourné par l'API.
/// </summary>
public sealed class CommentaireRetroAchievements : ModeleApiRetroAchievementsBase
{
    [JsonPropertyName("ID")]
    public int IdentifiantCommentaire { get; set; }

    [JsonPropertyName("User")]
    public string Utilisateur { get; set; } = string.Empty;

    [JsonPropertyName("Submitted")]
    public string DatePublication { get; set; } = string.Empty;

    [JsonPropertyName("CommentText")]
    public string Texte { get; set; } = string.Empty;
}
