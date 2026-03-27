using System.Text.Json.Serialization;
using RA.Compagnon.Modeles.Api.V2.Common;

namespace RA.Compagnon.Modeles.Api.V2.Game;

public sealed class GameListEntryV2 : ApiDtoV2Base
{
    [JsonPropertyName("ID")]
    public int Id { get; set; }

    [JsonPropertyName("Title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("ConsoleID")]
    public int ConsoleId { get; set; }

    [JsonPropertyName("ConsoleName")]
    public string ConsoleName { get; set; } = string.Empty;

    [JsonPropertyName("ImageIcon")]
    public string ImageIcon { get; set; } = string.Empty;

    [JsonPropertyName("ImageBoxArt")]
    public string ImageBoxArt { get; set; } = string.Empty;

    [JsonPropertyName("Hashes")]
    public List<string> Hashes { get; set; } = [];

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

    public int IdentifiantConsole
    {
        get => ConsoleId;
        set => ConsoleId = value;
    }

    public string NomConsole
    {
        get => ConsoleName;
        set => ConsoleName = value;
    }

    public string CheminImageIcone
    {
        get => ImageIcon;
        set => ImageIcon = value;
    }

    public string CheminImageBoite
    {
        get => ImageBoxArt;
        set => ImageBoxArt = value;
    }
}
