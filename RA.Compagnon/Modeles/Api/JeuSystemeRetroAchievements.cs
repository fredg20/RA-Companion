using System.Text.Json.Serialization;

namespace RA.Compagnon.Modeles.Api;

/// <summary>
/// Représente un jeu système ou un jeu issu du catalogue complet.
/// </summary>
public sealed class JeuSystemeRetroAchievements : ModeleApiRetroAchievementsBase
{
    [JsonPropertyName("ID")]
    public int IdentifiantJeu { get; set; }

    [JsonPropertyName("Title")]
    public string Titre { get; set; } = string.Empty;

    [JsonPropertyName("ConsoleID")]
    public int IdentifiantConsole { get; set; }

    [JsonPropertyName("ConsoleName")]
    public string NomConsole { get; set; } = string.Empty;

    [JsonPropertyName("ImageIcon")]
    public string CheminImageIcone { get; set; } = string.Empty;

    [JsonPropertyName("ImageBoxArt")]
    public string CheminImageBoite { get; set; } = string.Empty;

    [JsonPropertyName("Hashes")]
    public List<string> Hashes { get; set; } = [];
}
