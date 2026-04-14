using System.Text.Json.Serialization;

/*
 * Représente un déblocage de succès renvoyé par l'API RetroAchievements v2.
 */
namespace RA.Compagnon.Modeles.Api.V2.Achievement;

/*
 * Transporte les informations d'obtention d'un succès, y compris le mode
 * hardcore et les métadonnées du jeu concerné.
 */
public sealed class AchievementUnlockV2
{
    [JsonPropertyName("Date")]
    public string Date { get; set; } = string.Empty;

    [JsonPropertyName("HardcoreMode")]
    public bool HardcoreMode { get; set; }

    [JsonPropertyName("AchievementID")]
    public int AchievementId { get; set; }

    [JsonPropertyName("Title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("Description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("Points")]
    public int Points { get; set; }

    [JsonPropertyName("GameID")]
    public int GameId { get; set; }

    [JsonPropertyName("GameTitle")]
    public string GameTitle { get; set; } = string.Empty;

    public string DateDeblocage
    {
        get => Date;
        set => Date = value;
    }

    public bool ModeHardcore
    {
        get => HardcoreMode;
        set => HardcoreMode = value;
    }

    public int IdentifiantSucces
    {
        get => AchievementId;
        set => AchievementId = value;
    }

    public string Titre
    {
        get => Title;
        set => Title = value;
    }

    public int IdentifiantJeu
    {
        get => GameId;
        set => GameId = value;
    }

    public string TitreJeu
    {
        get => GameTitle;
        set => GameTitle = value;
    }
    public string TitleJeu
    {
        get => GameTitle;
        set => GameTitle = value;
    }
}
