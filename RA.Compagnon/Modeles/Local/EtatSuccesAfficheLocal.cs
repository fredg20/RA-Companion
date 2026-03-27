namespace RA.Compagnon.Modeles.Local;

/// <summary>
/// Représente le rétrosuccès actuellement affiché dans la fenêtre principale.
/// </summary>
public sealed class EtatSuccesAfficheLocal
{
    /// <summary>
    /// Identifiant RetroAchievements du jeu auquel le succès est rattaché.
    /// </summary>
    public int IdentifiantJeu { get; set; }

    /// <summary>
    /// Identifiant RetroAchievements du succès affiché.
    /// </summary>
    public int IdentifiantSucces { get; set; }

    /// <summary>
    /// Titre affiché pour le succès en cours.
    /// </summary>
    public string Titre { get; set; } = string.Empty;

    /// <summary>
    /// Description affichée pour le succès en cours.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Ligne de détails affichée pour le succès en cours.
    /// </summary>
    public string DetailsPoints { get; set; } = string.Empty;

    /// <summary>
    /// Indique si ce succès a été épinglé manuellement depuis la grille.
    /// </summary>
    public bool EstEpingleManuellement { get; set; }

    /// <summary>
    /// URL ou chemin du badge affiché.
    /// </summary>
    public string CheminImageBadge { get; set; } = string.Empty;

    /// <summary>
    /// Texte de remplacement affiché quand aucun visuel n'est disponible.
    /// </summary>
    public string TexteVisuel { get; set; } = string.Empty;

    public int Id
    {
        get => IdentifiantJeu;
        set => IdentifiantJeu = value;
    }

    public int AchievementId
    {
        get => IdentifiantSucces;
        set => IdentifiantSucces = value;
    }

    public string Title
    {
        get => Titre;
        set => Titre = value;
    }
}
