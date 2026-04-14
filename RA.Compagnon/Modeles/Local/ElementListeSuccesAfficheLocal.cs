/*
 * Représente un élément de la liste locale sérialisée des succès affichés
 * pour restaurer rapidement la grille au démarrage.
 */
namespace RA.Compagnon.Modeles.Local;

/*
 * Stocke l'identifiant, le titre et le badge d'un succès déjà affiché.
 */
public sealed class ElementListeSuccesAfficheLocal
{
    public int IdentifiantSucces { get; set; }

    public string Titre { get; set; } = string.Empty;

    public string CheminImageBadge { get; set; } = string.Empty;

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
