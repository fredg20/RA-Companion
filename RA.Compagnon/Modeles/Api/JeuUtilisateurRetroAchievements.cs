using System.Text.Json.Serialization;

namespace RA.Compagnon.Modeles.Api;

/// <summary>
/// Représente les données principales d'un jeu et la progression d'un utilisateur.
/// </summary>
public sealed class JeuUtilisateurRetroAchievements
{
    /// <summary>
    /// Identifiant du jeu.
    /// </summary>
    [JsonPropertyName("ID")]
    public int IdentifiantJeu { get; set; }

    /// <summary>
    /// Titre du jeu.
    /// </summary>
    [JsonPropertyName("Title")]
    public string Titre { get; set; } = string.Empty;

    /// <summary>
    /// Nom de la console.
    /// </summary>
    [JsonPropertyName("ConsoleName")]
    public string NomConsole { get; set; } = string.Empty;

    /// <summary>
    /// Identifiant de la console.
    /// </summary>
    [JsonPropertyName("ConsoleID")]
    public int IdentifiantConsole { get; set; }

    /// <summary>
    /// Date de sortie du jeu.
    /// </summary>
    [JsonPropertyName("Released")]
    public string DateSortie { get; set; } = string.Empty;

    /// <summary>
    /// Nom du développeur du jeu si l'API le fournit.
    /// </summary>
    [JsonPropertyName("Developer")]
    public string Developpeur { get; set; } = string.Empty;

    /// <summary>
    /// Genre principal du jeu si l'API le fournit.
    /// </summary>
    [JsonPropertyName("Genre")]
    public string Genre { get; set; } = string.Empty;

    /// <summary>
    /// Chemin relatif du box art du jeu.
    /// </summary>
    [JsonPropertyName("ImageBoxArt")]
    public string CheminImageBoite { get; set; } = string.Empty;

    /// <summary>
    /// Succès du jeu avec les informations d'obtention pour l'utilisateur ciblé.
    /// </summary>
    [JsonPropertyName("Achievements")]
    public Dictionary<string, SuccesJeuUtilisateurRetroAchievements> Succes { get; set; } = [];

    /// <summary>
    /// Nombre total de succès du jeu.
    /// </summary>
    [JsonPropertyName("NumAchievements")]
    public int NombreSucces { get; set; }

    /// <summary>
    /// Nombre de succès obtenus par l'utilisateur.
    /// </summary>
    [JsonPropertyName("NumAwardedToUser")]
    public int NombreSuccesObtenus { get; set; }

    /// <summary>
    /// Nombre de succès obtenus en mode Hardcore.
    /// </summary>
    [JsonPropertyName("NumAwardedToUserHardcore")]
    public int NombreSuccesObtenusHardcore { get; set; }

    /// <summary>
    /// Pourcentage de complétion utilisateur.
    /// </summary>
    [JsonPropertyName("UserCompletion")]
    public string CompletionUtilisateur { get; set; } = string.Empty;

    /// <summary>
    /// Temps de jeu total retourné par l'API, en minutes.
    /// </summary>
    [JsonPropertyName("UserTotalPlaytime")]
    public int TempsJeuTotalMinutes { get; set; }

    /// <summary>
    /// Plus haute récompense obtenue sur le jeu si disponible.
    /// </summary>
    [JsonPropertyName("HighestAwardKind")]
    public string PlusHauteRecompense { get; set; } = string.Empty;
}
