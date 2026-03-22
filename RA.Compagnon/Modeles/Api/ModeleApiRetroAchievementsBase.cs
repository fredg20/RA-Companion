using System.Text.Json;
using System.Text.Json.Serialization;

namespace RA.Compagnon.Modeles.Api;

/// <summary>
/// Base souple pour les DTOs API RetroAchievements non encore entièrement modélisés.
/// </summary>
public abstract class ModeleApiRetroAchievementsBase
{
    /// <summary>
    /// Capture les propriétés JSON supplémentaires non encore mappées explicitement.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement> ProprietesSupplementaires { get; set; } = [];
}
