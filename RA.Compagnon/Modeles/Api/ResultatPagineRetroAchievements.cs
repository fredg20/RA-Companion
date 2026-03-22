using System.Text.Json.Serialization;

namespace RA.Compagnon.Modeles.Api;

/// <summary>
/// Représente une réponse paginée standard de l'API RetroAchievements.
/// </summary>
public sealed class ResultatPagineRetroAchievements<T>
{
    [JsonPropertyName("Count")]
    public int Nombre { get; set; }

    [JsonPropertyName("Total")]
    public int Total { get; set; }

    [JsonPropertyName("Results")]
    public List<T> Resultats { get; set; } = [];
}
