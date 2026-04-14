/*
 * Représente le conteneur global du catalogue local de jeux mis en cache.
 */
namespace RA.Compagnon.Modeles.Catalogue;

/*
 * Stocke la date de mise à jour et la liste complète des jeux du catalogue
 * local.
 */
public sealed class CatalogueJeuxLocal
{
    public DateTimeOffset DateMajUtc { get; set; }

    public List<JeuCatalogueLocal> Jeux { get; set; } = [];
}
