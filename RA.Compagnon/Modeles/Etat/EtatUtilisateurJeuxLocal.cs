/*
 * Représente le conteneur local de l'ensemble des jeux utilisateur observés.
 */
namespace RA.Compagnon.Modeles.Etat;

/*
 * Stocke la date de mise à jour et la collection de jeux utilisateur gardés
 * en cache local.
 */
public sealed class EtatUtilisateurJeuxLocal
{
    public DateTimeOffset DateMajUtc { get; set; }

    public List<EtatJeuUtilisateurLocal> Jeux { get; set; } = [];
}