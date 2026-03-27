namespace RA.Compagnon.Modeles.Local;

/// <summary>
/// Représente le dernier jeu affiché dans la fenêtre principale.
/// </summary>
public sealed class EtatJeuAfficheLocal
{
    /// <summary>
    /// Signature locale légère du jeu détecté, utile pour éviter les sauvegardes répétées.
    /// </summary>
    public string SignatureLocale { get; set; } = string.Empty;

    /// <summary>
    /// Identifiant RetroAchievements du jeu.
    /// </summary>
    public int IdentifiantJeu { get; set; }

    /// <summary>
    /// Indique si le jeu était présenté comme un jeu en cours.
    /// </summary>
    public bool EstJeuEnCours { get; set; }

    /// <summary>
    /// Titre du jeu affiché.
    /// </summary>
    public string Titre { get; set; } = string.Empty;

    /// <summary>
    /// Détails textuels affichés dans la zone du jeu.
    /// </summary>
    public string Details { get; set; } = string.Empty;

    /// <summary>
    /// Résumé de progression affiché.
    /// </summary>
    public string ResumeProgression { get; set; } = "-- / --";

    /// <summary>
    /// Pourcentage de progression affiché.
    /// </summary>
    public string PourcentageProgression { get; set; } = string.Empty;

    /// <summary>
    /// Valeur numérique de progression.
    /// </summary>
    public double ValeurProgression { get; set; }

    /// <summary>
    /// Temps de jeu affiché sous l'image.
    /// </summary>
    public string TempsJeuSousImage { get; set; } = string.Empty;

    /// <summary>
    /// État du jeu affiché dans la carte de progression.
    /// </summary>
    public string EtatJeu { get; set; } = string.Empty;

    /// <summary>
    /// Chemin relatif du box art RetroAchievements.
    /// </summary>
    public string CheminImageBoite { get; set; } = string.Empty;

    /// <summary>
    /// Identifiant de la console du jeu.
    /// </summary>
    public int IdentifiantConsole { get; set; }

    /// <summary>
    /// Date de sortie brute du jeu.
    /// </summary>
    public string DateSortie { get; set; } = string.Empty;

    /// <summary>
    /// Genre du jeu affiché.
    /// </summary>
    public string Genre { get; set; } = string.Empty;

    /// <summary>
    /// Développeur affiché sous l'année.
    /// </summary>
    public string Developpeur { get; set; } = string.Empty;

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

    public string ImageBoxArt
    {
        get => CheminImageBoite;
        set => CheminImageBoite = value;
    }

    public int ConsoleId
    {
        get => IdentifiantConsole;
        set => IdentifiantConsole = value;
    }

    public string Released
    {
        get => DateSortie;
        set => DateSortie = value;
    }

    public string Developer
    {
        get => Developpeur;
        set => Developpeur = value;
    }
}
