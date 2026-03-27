using System.Text.Json.Serialization;

namespace RA.Compagnon.Modeles.Api.V2.User;

public sealed class UserSetRequestsResponseV2
{
    [JsonPropertyName("RequestedSets")]
    public List<UserSetRequestV2> RequestedSets { get; set; } = [];
}
