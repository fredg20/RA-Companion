using System.Text.Json.Serialization;

namespace RA.Compagnon.Modeles.Local;

/// <summary>
/// Représente les informations minimales d'affichage à mémoriser localement.
/// </summary>
public sealed class ConfigurationConnexion
{
    /// <summary>
    /// Pseudo RetroAchievements saisi par l'utilisateur.
    /// </summary>
    [JsonIgnore]
    public string Pseudo { get; set; } = string.Empty;

    /// <summary>
    /// Clé Web API utilisée pour les futurs appels authentifiés.
    /// </summary>
    [JsonIgnore]
    public string CleApiWeb { get; set; } = string.Empty;

    /// <summary>
    /// Position horizontale de la fenêtre principale.
    /// </summary>
    public double? PositionGaucheFenetre { get; set; }

    /// <summary>
    /// Position verticale de la fenêtre principale.
    /// </summary>
    public double? PositionHautFenetre { get; set; }

    /// <summary>
    /// Largeur mémorisée de la fenêtre principale.
    /// </summary>
    public double LargeurFenetre { get; set; } = 1100;

    /// <summary>
    /// Hauteur mémorisée de la fenêtre principale.
    /// </summary>
    public double HauteurFenetre { get; set; } = 700;

    /// <summary>
    /// Mode d'affichage des rétrosuccès choisi par l'utilisateur.
    /// </summary>
    public string ModeAffichageSucces { get; set; } = "Normal";

    /// <summary>
    /// Dernier jeu affiché avec ses informations principales.
    /// </summary>
    [JsonIgnore]
    public EtatJeuAfficheLocal? DernierJeuAffiche { get; set; }

    /// <summary>
    /// Dernier rétrosuccès affiché dans la section dédiée.
    /// </summary>
    [JsonIgnore]
    public EtatSuccesAfficheLocal? DernierSuccesAffiche { get; set; }

    /// <summary>
    /// Dernière liste de rétrosuccès affichée dans la grille du jeu.
    /// </summary>
    [JsonIgnore]
    public EtatListeSuccesAfficheeLocal? DerniereListeSuccesAffichee { get; set; }

    /// <summary>
    /// Emplacements d'émulateurs définis manuellement par l'utilisateur.
    /// </summary>
    public Dictionary<string, string> EmplacementsEmulateursManuels { get; set; } = [];

    /// <summary>
    /// Derniers emplacements d'émulateurs détectés automatiquement par Compagnon.
    /// </summary>
    public Dictionary<string, string> EmplacementsEmulateursDetectes { get; set; } = [];
}
