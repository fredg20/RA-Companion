using System.Text.Json;
using System.Text.Json.Serialization;

namespace RA.Compagnon.Modeles.Api.V2.Common;

public abstract class ApiDtoV2Base
{
    [JsonExtensionData]
    public Dictionary<string, JsonElement> Extra { get; set; } = [];
}
