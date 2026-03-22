using System.Text.Json;
using System.Text.Json.Serialization;

namespace RA.Compagnon.Modeles.Api;

/// <summary>
/// Représente les détails étendus d'un jeu.
/// </summary>
public sealed class JeuEtenduRetroAchievements : ModeleApiRetroAchievementsBase
{
    [JsonPropertyName("ID")]
    public int IdentifiantJeu { get; set; }

    [JsonPropertyName("Title")]
    public string Titre { get; set; } = string.Empty;

    [JsonPropertyName("ConsoleName")]
    public string NomConsole { get; set; } = string.Empty;

    [JsonPropertyName("Released")]
    public string DateSortie { get; set; } = string.Empty;

    [JsonPropertyName("Developer")]
    public string Developpeur { get; set; } = string.Empty;

    [JsonPropertyName("Publisher")]
    public string Editeur { get; set; } = string.Empty;

    [JsonPropertyName("Genre")]
    public string Genre { get; set; } = string.Empty;

    [JsonPropertyName("Achievements")]
    public Dictionary<string, JsonElement> Succes { get; set; } = [];
}
