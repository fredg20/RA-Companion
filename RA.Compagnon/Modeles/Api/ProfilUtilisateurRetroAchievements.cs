using System.Text.Json.Serialization;

namespace RA.Compagnon.Modeles.Api;

/// <summary>
/// Représente les champs du endpoint User Profile de RetroAchievements.
/// </summary>
public sealed class ProfilUtilisateurRetroAchievements
{
    /// <summary>
    /// Nom d'utilisateur retourné par l'API.
    /// </summary>
    [JsonPropertyName("User")]
    public string NomUtilisateur { get; set; } = string.Empty;

    /// <summary>
    /// ULID stable du compte utilisateur.
    /// </summary>
    [JsonPropertyName("ULID")]
    public string Ulid { get; set; } = string.Empty;

    /// <summary>
    /// Chemin relatif de l'avatar utilisateur.
    /// </summary>
    [JsonPropertyName("UserPic")]
    public string CheminAvatar { get; set; } = string.Empty;

    /// <summary>
    /// Date d'inscription du compte.
    /// </summary>
    [JsonPropertyName("MemberSince")]
    public string MembreDepuis { get; set; } = string.Empty;

    /// <summary>
    /// Message de Rich Presence courant si disponible.
    /// </summary>
    [JsonPropertyName("RichPresenceMsg")]
    public string MessagePresenceRiche { get; set; } = string.Empty;

    /// <summary>
    /// Identifiant du dernier jeu détecté pour l'utilisateur.
    /// </summary>
    [JsonPropertyName("LastGameID")]
    public int IdentifiantDernierJeu { get; set; }

    /// <summary>
    /// Nom du dernier jeu détecté si l'API ou le projet le fournit.
    /// </summary>
    [JsonPropertyName("LastGame")]
    public string NomDernierJeu { get; set; } = string.Empty;

    /// <summary>
    /// Nombre de contributions du compte.
    /// </summary>
    [JsonPropertyName("ContribCount")]
    public int NombreContributions { get; set; }

    /// <summary>
    /// Rendement de contribution du compte.
    /// </summary>
    [JsonPropertyName("ContribYield")]
    public int RendementContributions { get; set; }

    /// <summary>
    /// Total des points du compte.
    /// </summary>
    [JsonPropertyName("TotalPoints")]
    public int PointsTotaux { get; set; }

    /// <summary>
    /// Total des points softcore du compte.
    /// </summary>
    [JsonPropertyName("TotalSoftcorePoints")]
    public int PointsSoftcoreTotaux { get; set; }

    /// <summary>
    /// Total des true points du compte.
    /// </summary>
    [JsonPropertyName("TotalTruePoints")]
    public int TruePointsTotaux { get; set; }

    /// <summary>
    /// Niveau de permission du compte.
    /// </summary>
    [JsonPropertyName("Permissions")]
    public int NiveauPermissions { get; set; }

    /// <summary>
    /// Indique si le compte est non suivi.
    /// </summary>
    [JsonPropertyName("Untracked")]
    public int EstNonSuivi { get; set; }

    /// <summary>
    /// Identifiant numérique interne du compte.
    /// </summary>
    [JsonPropertyName("ID")]
    public int IdentifiantUtilisateur { get; set; }

    /// <summary>
    /// Indique si le mur utilisateur est actif.
    /// </summary>
    [JsonPropertyName("UserWallActive")]
    public int MurUtilisateurActif { get; set; }

    /// <summary>
    /// Devise ou message de profil.
    /// </summary>
    [JsonPropertyName("Motto")]
    public string Devise { get; set; } = string.Empty;
}
