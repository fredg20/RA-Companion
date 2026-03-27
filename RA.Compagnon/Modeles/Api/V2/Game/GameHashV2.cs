using System.Text.Json.Serialization;

namespace RA.Compagnon.Modeles.Api.V2.Game;

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
