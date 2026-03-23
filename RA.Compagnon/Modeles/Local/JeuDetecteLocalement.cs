namespace RA.Compagnon.Modeles.Local;

/// <summary>
/// Représente un jeu estimé localement à partir d'un émulateur en cours d'exécution.
/// </summary>
public sealed class JeuDetecteLocalement
{
    /// <summary>
    /// Identifiant du processus détecté.
    /// </summary>
    public int IdentifiantProcessus { get; set; }

    /// <summary>
    /// Nom lisible de l'émulateur détecté.
    /// </summary>
    public string NomEmulateur { get; set; } = string.Empty;

    /// <summary>
    /// Nom du processus détecté.
    /// </summary>
    public string NomProcessus { get; set; } = string.Empty;

    /// <summary>
    /// Titre brut de la fenêtre principale de l'émulateur.
    /// </summary>
    public string TitreFenetre { get; set; } = string.Empty;

    /// <summary>
    /// Titre du jeu estimé à partir du titre de fenêtre.
    /// </summary>
    public string TitreJeuEstime { get; set; } = string.Empty;

    /// <summary>
    /// Chemin ou nom de fichier du jeu estimé depuis le titre de fenêtre si disponible.
    /// </summary>
    public string CheminJeuEstime { get; set; } = string.Empty;

    /// <summary>
    /// Chemin ou nom de fichier du jeu estimé depuis la ligne de commande si disponible.
    /// </summary>
    public string CheminJeuLigneCommande { get; set; } = string.Empty;

    /// <summary>
    /// Chemin de jeu finalement retenu pour l'analyse locale.
    /// </summary>
    public string CheminJeuRetenu { get; set; } = string.Empty;

    /// <summary>
    /// Empreinte locale calculée si un vrai fichier a été trouvé.
    /// </summary>
    public EmpreinteJeuLocal? EmpreinteLocale { get; set; }

    /// <summary>
    /// Identifiant RetroAchievements résolu par hash si disponible.
    /// </summary>
    public int IdentifiantJeuRetroAchievements { get; set; }

    /// <summary>
    /// Titre RetroAchievements résolu par hash si disponible.
    /// </summary>
    public string TitreJeuRetroAchievements { get; set; } = string.Empty;

    /// <summary>
    /// Nom de console RetroAchievements résolu par hash si disponible.
    /// </summary>
    public string NomConsoleRetroAchievements { get; set; } = string.Empty;

    /// <summary>
    /// Ligne de commande brute du processus si elle a pu être lue.
    /// </summary>
    public string LigneCommande { get; set; } = string.Empty;

    /// <summary>
    /// Chemin de l'exécutable de l'émulateur si accessible.
    /// </summary>
    public string CheminExecutable { get; set; } = string.Empty;

    /// <summary>
    /// Score simple de confiance calculé par la sonde locale.
    /// </summary>
    public int ScoreConfiance { get; set; }
}
