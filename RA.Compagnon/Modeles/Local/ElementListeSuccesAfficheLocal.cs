namespace RA.Compagnon.Modeles.Local;

/// <summary>
/// Représente un élément affiché dans la grille des rétrosuccès.
/// </summary>
public sealed class ElementListeSuccesAfficheLocal
{
    /// <summary>
    /// Identifiant RetroAchievements du succès affiché dans la grille.
    /// </summary>
    public int IdentifiantSucces { get; set; }

    /// <summary>
    /// Titre du rétrosuccès, utilisé notamment pour l'infobulle et le repli texte.
    /// </summary>
    public string Titre { get; set; } = string.Empty;

    /// <summary>
    /// URL du badge affiché dans la grille.
    /// </summary>
    public string CheminImageBadge { get; set; } = string.Empty;
}
