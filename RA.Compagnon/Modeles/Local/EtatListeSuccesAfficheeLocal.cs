/*
 * Représente la version persistée de la liste de succès affichée pour un jeu,
 * afin de pouvoir la restaurer rapidement.
 */
namespace RA.Compagnon.Modeles.Local;

/*
 * Stocke les succès visibles d'un jeu ainsi que l'ordre des succès passés.
 */
public sealed class EtatListeSuccesAfficheeLocal
{
    public int IdentifiantJeu { get; set; }

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
