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
    /// Identifiant de la console.
    /// </summary>
    [JsonPropertyName("ConsoleID")]
    public int IdentifiantConsole { get; set; }

    /// <summary>
    /// Nom de la console.
    /// </summary>
    [JsonPropertyName("ConsoleName")]
    public string NomConsole { get; set; } = string.Empty;

    /// <summary>
    /// Titre du jeu si fourni par l'API.
    /// </summary>
    [JsonPropertyName("Title")]
    public string Titre { get; set; } = string.Empty;

    /// <summary>
    /// Icône du jeu.
    /// </summary>
    [JsonPropertyName("ImageIcon")]
    public string CheminImageIcone { get; set; } = string.Empty;

    /// <summary>
    /// Date de dernière activité sur ce jeu.
    /// </summary>
    [JsonPropertyName("LastPlayed")]
    public string DernierePartie { get; set; } = string.Empty;

    /// <summary>
    /// Nombre total de succès du jeu.
    /// </summary>
    [JsonPropertyName("AchievementsTotal")]
    public int NombreSuccesTotal { get; set; }
}
