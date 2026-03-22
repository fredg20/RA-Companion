namespace RA.Compagnon.Modeles.Local;

/// <summary>
/// Représente les informations minimales de connexion et d'affichage à mémoriser localement.
/// </summary>
public sealed class ConfigurationConnexion
{
    /// <summary>
    /// Pseudo RetroAchievements saisi par l'utilisateur.
    /// </summary>
    public string Pseudo { get; set; } = string.Empty;

    /// <summary>
    /// Clé Web API utilisée pour les futurs appels authentifiés.
    /// </summary>
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
    /// Dernier jeu affiché avec ses informations principales.
    /// </summary>
    public EtatJeuAfficheLocal? DernierJeuAffiche { get; set; }
}
