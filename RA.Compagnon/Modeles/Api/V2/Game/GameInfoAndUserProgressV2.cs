using System.Text.Json.Serialization;

namespace RA.Compagnon.Modeles.Api.V2.Game;

public sealed class GameInfoAndUserProgressV2
{
    [JsonPropertyName("ID")]
    public int Id { get; set; }

    [JsonPropertyName("Title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("ConsoleName")]
    public string ConsoleName { get; set; } = string.Empty;

    [JsonPropertyName("ConsoleID")]
    public int ConsoleId { get; set; }

    [JsonPropertyName("Released")]
    public string Released { get; set; } = string.Empty;

    [JsonPropertyName("Developer")]
    public string Developer { get; set; } = string.Empty;

    [JsonPropertyName("Publisher")]
    public string Publisher { get; set; } = string.Empty;

    [JsonPropertyName("Genre")]
    public string Genre { get; set; } = string.Empty;

    [JsonPropertyName("ImageTitle")]
    public string ImageTitle { get; set; } = string.Empty;

    [JsonPropertyName("ImageIngame")]
    public string ImageIngame { get; set; } = string.Empty;

    [JsonPropertyName("ImageBoxArt")]
    public string ImageBoxArt { get; set; } = string.Empty;

    [JsonPropertyName("Achievements")]
    public Dictionary<string, GameAchievementV2> Achievements { get; set; } = [];

    [JsonPropertyName("NumAchievements")]
    public int NumAchievements { get; set; }

    [JsonPropertyName("NumAwardedToUser")]
    public int NumAwardedToUser { get; set; }

    [JsonPropertyName("NumAwardedToUserHardcore")]
    public int NumAwardedToUserHardcore { get; set; }

    [JsonPropertyName("NumDistinctPlayers")]
    public int NumDistinctPlayers { get; set; }

    [JsonPropertyName("UserCompletion")]
    public string UserCompletion { get; set; } = string.Empty;

    [JsonPropertyName("UserTotalPlaytime")]
    public int UserTotalPlaytime { get; set; }

    [JsonPropertyName("HighestAwardKind")]
    public string HighestAwardKind { get; set; } = string.Empty;

    public int IdentifiantJeu
    {
        get => Id;
        set => Id = value;
    }

    public string Titre
    {
        get => Title;
        set => Title = value;
    }

    public string NomConsole
    {
        get => ConsoleName;
        set => ConsoleName = value;
    }

    public int IdentifiantConsole
    {
        get => ConsoleId;
        set => ConsoleId = value;
    }

    public string DateSortie
    {
        get => Released;
        set => Released = value;
    }

    public string Developpeur
    {
        get => Developer;
        set => Developer = value;
    }

    public string Editeur
    {
        get => Publisher;
        set => Publisher = value;
    }

    public string CheminImageBoite
    {
        get => ImageBoxArt;
        set => ImageBoxArt = value;
    }

    public string CheminImageTitre
    {
        get => ImageTitle;
        set => ImageTitle = value;
    }

    public string CheminImageEnJeu
    {
        get => ImageIngame;
        set => ImageIngame = value;
    }

    public Dictionary<string, GameAchievementV2> Succes
    {
        get => Achievements;
        set => Achievements = value;
    }

    public int NombreSucces
    {
        get => NumAchievements;
        set => NumAchievements = value;
    }

    public int NombreSuccesObtenus
    {
        get => NumAwardedToUser;
        set => NumAwardedToUser = value;
    }

    public int NombreSuccesObtenusHardcore
    {
        get => NumAwardedToUserHardcore;
        set => NumAwardedToUserHardcore = value;
    }

    public string CompletionUtilisateur
    {
        get => UserCompletion;
        set => UserCompletion = value;
    }

    public int TempsJeuTotalMinutes
    {
        get => UserTotalPlaytime;
        set => UserTotalPlaytime = value;
    }

    public string PlusHauteRecompense
    {
        get => HighestAwardKind;
        set => HighestAwardKind = value;
    }
}
