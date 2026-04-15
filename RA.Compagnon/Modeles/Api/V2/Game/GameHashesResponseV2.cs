using System.Text.Json.Serialization;

/*
 * Représente la réponse contenant une collection d'empreintes de jeu dans
 * l'API RetroAchievements v2.
 */
namespace RA.Compagnon.Modeles.Api.V2.Game;

/*
 * Transporte la liste des empreintes renvoyées par une recherche de hachages.
 */
public sealed class GameHashesResponseV2
{
    [JsonPropertyName("Results")]
    public List<GameHashV2> Results { get; set; } = [];

    public List<GameHashV2> Resultats
    {
        get => Results;
        set => Results = value;
    }
}