using System.Text.Json.Serialization;

/*
 * Représente une empreinte de jeu retournée par l'API RetroAchievements v2.
 */
namespace RA.Compagnon.Modeles.Api.V2.Game;

/*
 * Transporte les informations d'un fichier compatible connu pour un jeu dans
 * les réponses de hachage.
 */
public sealed class GameHashV2
{
    [JsonPropertyName("MD5")]
    public string Md5 { get; set; } = string.Empty;

    [JsonPropertyName("Name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("Labels")]
    public List<string> Labels { get; set; } = [];

    [JsonPropertyName("PatchUrl")]
    public string? PatchUrl { get; set; }

    public string EmpreinteMd5
    {
        get => Md5;
        set => Md5 = value;
    }

    public string NomFichier
    {
        get => Name;
        set => Name = value;
    }

    public List<string> Etiquettes
    {
        get => Labels;
        set => Labels = value;
    }

    public string? UrlPatch
    {
        get => PatchUrl;
        set => PatchUrl = value;
    }
}
