using System.Text.Json.Serialization;

/*
 * Représente la réponse de récompenses utilisateur de l'API
 * RetroAchievements v2.
 */
namespace RA.Compagnon.Modeles.Api.V2.User;

/*
 * Transporte les compteurs de récompenses et la liste visible des badges
 * utilisateur.
 */
public sealed class UserAwardsResponseV2
{
    [JsonPropertyName("TotalAwardsCount")]
    public int TotalAwardsCount { get; set; }

    [JsonPropertyName("HiddenAwardsCount")]
    public int HiddenAwardsCount { get; set; }

    [JsonPropertyName("MasteryAwardsCount")]
    public int MasteryAwardsCount { get; set; }

    [JsonPropertyName("CompletionAwardsCount")]
    public int CompletionAwardsCount { get; set; }

    [JsonPropertyName("BeatenHardcoreAwardsCount")]
    public int BeatenHardcoreAwardsCount { get; set; }

    [JsonPropertyName("BeatenSoftcoreAwardsCount")]
    public int BeatenSoftcoreAwardsCount { get; set; }

    [JsonPropertyName("EventAwardsCount")]
    public int EventAwardsCount { get; set; }

    [JsonPropertyName("SiteAwardsCount")]
    public int SiteAwardsCount { get; set; }

    [JsonPropertyName("VisibleUserAwards")]
    public List<VisibleUserAwardV2> VisibleUserAwards { get; set; } = [];
}