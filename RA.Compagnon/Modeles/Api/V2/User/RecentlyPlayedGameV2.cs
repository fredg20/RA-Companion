using System.Text.Json.Serialization;

/*
 * Représente un jeu récemment joué par l'utilisateur dans l'API
 * RetroAchievements v2.
 */
namespace RA.Compagnon.Modeles.Api.V2.User;

/*
 * Transporte l'identité du jeu récent, ses images et quelques statistiques
 * de progression de base.
 */
public sealed class RecentlyPlayedGameV2
{
    [JsonPropertyName("GameID")]
    public int IdentifiantJeu { get; set; }

    [JsonPropertyName("ConsoleID")]
    public int IdentifiantConsole { get; set; }

    [JsonPropertyName("ConsoleName")]
    public string NomConsole { get; set; } = string.Empty;

    [JsonPropertyName("Title")]
    public string Titre { get; set; } = string.Empty;

    [JsonPropertyName("ImageIcon")]
    public string CheminImageIcone { get; set; } = string.Empty;

    [JsonPropertyName("ImageTitle")]
    public string CheminImageTitre { get; set; } = string.Empty;

    [JsonPropertyName("ImageIngame")]
    public string CheminImageEnJeu { get; set; } = string.Empty;

    [JsonPropertyName("ImageBoxArt")]
    public string CheminImageBoite { get; set; } = string.Empty;

    [JsonPropertyName("LastPlayed")]
    public string DernierePartie { get; set; } = string.Empty;

    [JsonPropertyName("AchievementsTotal")]
    public int NombreSuccesTotal { get; set; }
    public int Id
    {
        get => IdentifiantJeu;
        set => IdentifiantJeu = value;
    }

    public string Title
    {
        get => Titre;
        set => Titre = value;
    }
}