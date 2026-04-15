using System.Text.Json.Serialization;

/*
 * Représente une console ou un système connu de l'API RetroAchievements v2.
 */
namespace RA.Compagnon.Modeles.Api.V2.Game;

/*
 * Transporte l'identité, le nom et l'icône d'une console.
 */
public sealed class ConsoleV2
{
    [JsonPropertyName("ID")]
    public int Id { get; set; }

    [JsonPropertyName("Name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("IconURL")]
    public string IconUrl { get; set; } = string.Empty;

    public int IdentifiantConsole
    {
        get => Id;
        set => Id = value;
    }

    public int ConsoleId
    {
        get => Id;
        set => Id = value;
    }

    public string Nom
    {
        get => Name;
        set => Name = value;
    }

    public string UrlIcone
    {
        get => IconUrl;
        set => IconUrl = value;
    }
}