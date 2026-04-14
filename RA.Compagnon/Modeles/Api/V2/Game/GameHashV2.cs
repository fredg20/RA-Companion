using System.Text.Json.Serialization;

/*
 * Représente une empreinte de jeu retournée par l'API RetroAchievements v2.
 */
namespace RA.Compagnon.Modeles.Api.V2.Game;

/*
 * Transporte l'empreinte MD5 d'un jeu dans les réponses de hachage.
 */
public sealed class GameHashV2
{
    [JsonPropertyName("MD5")]
    public string Md5 { get; set; } = string.Empty;

    public string EmpreinteMd5
    {
        get => Md5;
        set => Md5 = value;
    }
}
