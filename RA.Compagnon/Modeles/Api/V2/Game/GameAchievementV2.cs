using System.Text.Json.Serialization;

/*
 * Représente un succès de jeu tel qu'il est retourné par l'API
 * RetroAchievements v2.
 */
namespace RA.Compagnon.Modeles.Api.V2.Game;

/*
 * Transporte les données détaillées d'un succès, ses points, ses ratios et
 * son état d'obtention utilisateur.
 */
public sealed class GameAchievementV2
{
    [JsonPropertyName("ID")]
    public int Id { get; set; }

    [JsonPropertyName("Title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("Description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("Points")]
    public int Points { get; set; }

    [JsonPropertyName("TrueRatio")]
    public int TrueRatio { get; set; }

    [JsonPropertyName("NumAwarded")]
    public int NumAwarded { get; set; }

    [JsonPropertyName("NumAwardedHardcore")]
    public int NumAwardedHardcore { get; set; }

    [JsonPropertyName("BadgeName")]
    public string BadgeName { get; set; } = string.Empty;

    [JsonPropertyName("DisplayOrder")]
    public int DisplayOrder { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("DateEarned")]
    public string DateEarned { get; set; } = string.Empty;

    [JsonPropertyName("DateEarnedHardcore")]
    public string DateEarnedHardcore { get; set; } = string.Empty;

    [JsonPropertyName("MemAddr")]
    public string MemAddr { get; set; } = string.Empty;

    public int IdentifiantSucces
    {
        get => Id;
        set => Id = value;
    }

    public string Titre
    {
        get => Title;
        set => Title = value;
    }

    public int Retropoints
    {
        get => TrueRatio;
        set => TrueRatio = value;
    }

    public string NomBadge
    {
        get => BadgeName;
        set => BadgeName = value;
    }

    public int OrdreAffichage
    {
        get => DisplayOrder;
        set => DisplayOrder = value;
    }

    public string DateObtention
    {
        get => DateEarned;
        set => DateEarned = value;
    }

    public string DateObtentionHardcore
    {
        get => DateEarnedHardcore;
        set => DateEarnedHardcore = value;
    }

    public string DefinitionMemoire
    {
        get => MemAddr;
        set => MemAddr = value;
    }
}