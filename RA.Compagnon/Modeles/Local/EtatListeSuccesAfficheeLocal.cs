namespace RA.Compagnon.Modeles.Local;

/// <summary>
/// Représente la liste de rétrosuccès affichée dans la grille du jeu courant.
/// </summary>
public sealed class EtatListeSuccesAfficheeLocal
{
    /// <summary>
    /// Identifiant RetroAchievements du jeu auquel la grille est rattachée.
    /// </summary>
    public int IdentifiantJeu { get; set; }

    /// <summary>
    /// Liste des éléments de la grille des rétrosuccès.
    /// </summary>
    public List<ElementListeSuccesAfficheLocal> Succes { get; set; } = [];

    public List<int> SuccesPasses { get; set; } = [];

    public int Id
    {
        get => IdentifiantJeu;
        set => IdentifiantJeu = value;
    }

    public List<ElementListeSuccesAfficheLocal> Achievements
    {
        get => Succes;
        set => Succes = value;
    }
}
