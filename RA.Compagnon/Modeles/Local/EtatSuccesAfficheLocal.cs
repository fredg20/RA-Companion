/*
 * Représente la version persistée du succès actuellement affiché dans la
 * section centrale de l'application.
 */
namespace RA.Compagnon.Modeles.Local;

/*
 * Stocke les informations essentielles d'un succès affiché pour pouvoir le
 * restaurer au démarrage.
 */
public sealed class EtatSuccesAfficheLocal
{
    public int IdentifiantJeu { get; set; }

    public int IdentifiantSucces { get; set; }

    public string Titre { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string DetailsPoints { get; set; } = string.Empty;

    public string DetailsFaisabilite { get; set; } = string.Empty;

    public string ExplicationFaisabilite { get; set; } = string.Empty;

    public bool EstEpingleManuellement { get; set; }

    public string CheminImageBadge { get; set; } = string.Empty;

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
