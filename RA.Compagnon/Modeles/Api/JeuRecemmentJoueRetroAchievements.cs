using System.Text.Json.Serialization;

namespace RA.Compagnon.Modeles.Api;

/// <summary>
/// Représente un jeu récemment joué retourné par RetroAchievements.
/// </summary>
public sealed class JeuRecemmentJoueRetroAchievements
{
    /// <summary>
    /// Identifiant du jeu.
    /// </summary>
    [JsonPropertyName("GameID")]
    public int IdentifiantJeu { get; set; }

    /// <summary>
    /// Titre du jeu si fourni par l'API.
    /// </summary>
    [JsonPropertyName("Title")]
    public string Titre { get; set; } = string.Empty;
}
