using System.Text.Json.Serialization;

namespace RA.Compagnon.Modeles.Api.V2.User;

public sealed class UserPointsV2
{
    [JsonPropertyName("Points")]
    public int Points { get; set; }

    [JsonPropertyName("SoftcorePoints")]
    public int SoftcorePoints { get; set; }
}
