using System.Text.Json.Serialization;

namespace RA.Compagnon.Modeles.Api;

/// <summary>
/// Représente une console retournée par l'API RetroAchievements.
/// </summary>
public sealed class ConsoleRetroAchievements
{
    /// <summary>
    /// Identifiant unique de la console.
    /// </summary>
    [JsonPropertyName("ID")]
    public int IdentifiantConsole { get; set; }

    /// <summary>
    /// Nom lisible de la console.
    /// </summary>
    [JsonPropertyName("Name")]
    public string Nom { get; set; } = string.Empty;

    /// <summary>
    /// URL de l'icône officielle de la console.
    /// </summary>
    [JsonPropertyName("IconURL")]
    public string UrlIcone { get; set; } = string.Empty;
}
