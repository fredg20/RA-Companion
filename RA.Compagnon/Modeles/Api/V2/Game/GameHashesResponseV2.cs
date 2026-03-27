using System.Text.Json.Serialization;

namespace RA.Compagnon.Modeles.Api.V2.Game;

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
