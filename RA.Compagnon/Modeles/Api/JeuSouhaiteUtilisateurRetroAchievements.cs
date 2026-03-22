using System.Text.Json.Serialization;

namespace RA.Compagnon.Modeles.Api;

/// <summary>
/// Représente un jeu de la liste "want to play" d'un utilisateur.
/// </summary>
public sealed class JeuSouhaiteUtilisateurRetroAchievements : ModeleApiRetroAchievementsBase
{
    [JsonPropertyName("GameID")]
    public int IdentifiantJeu { get; set; }

    [JsonPropertyName("Title")]
    public string TitreJeu { get; set; } = string.Empty;

    [JsonPropertyName("ConsoleName")]
    public string NomConsole { get; set; } = string.Empty;

    [JsonPropertyName("ImageIcon")]
    public string CheminImageIcone { get; set; } = string.Empty;
}
