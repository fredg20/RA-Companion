using System.Text.Json.Serialization;

/*
 * Représente le dernier jeu connu d'un utilisateur dans l'API
 * RetroAchievements v2.
 */
namespace RA.Compagnon.Modeles.Api.V2.User;

/*
 * Transporte les métadonnées principales du dernier jeu associé à un profil
 * utilisateur.
 */
public sealed class LastGameV2
{
    [JsonPropertyName("ID")]
    public int IdentifiantJeu { get; set; }

    [JsonPropertyName("Title")]
    public string Titre { get; set; } = string.Empty;

    [JsonPropertyName("ConsoleID")]
    public int IdentifiantConsole { get; set; }

    [JsonPropertyName("ConsoleName")]
    public string NomConsole { get; set; } = string.Empty;

    [JsonPropertyName("ForumTopicID")]
    public int IdentifiantForum { get; set; }

    [JsonPropertyName("Flags")]
    public int Flags { get; set; }

    [JsonPropertyName("ImageIcon")]
    public string CheminImageIcone { get; set; } = string.Empty;

    [JsonPropertyName("ImageTitle")]
    public string CheminImageTitre { get; set; } = string.Empty;

    [JsonPropertyName("ImageIngame")]
    public string CheminImageEnJeu { get; set; } = string.Empty;

    [JsonPropertyName("ImageBoxArt")]
    public string CheminImageBoite { get; set; } = string.Empty;

    [JsonPropertyName("Publisher")]
    public string Editeur { get; set; } = string.Empty;

    [JsonPropertyName("Developer")]
    public string Developpeur { get; set; } = string.Empty;

    [JsonPropertyName("Genre")]
    public string Genre { get; set; } = string.Empty;

    [JsonPropertyName("Released")]
    public string DateSortie { get; set; } = string.Empty;

    [JsonPropertyName("ReleasedAtGranularity")]
    public string GranulariteSortie { get; set; } = string.Empty;

    [JsonPropertyName("IsFinal")]
    public int EstFinal { get; set; }
}