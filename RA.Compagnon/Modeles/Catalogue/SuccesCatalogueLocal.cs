/*
 * Représente un succès enregistré dans le catalogue local d'un jeu.
 */
namespace RA.Compagnon.Modeles.Catalogue;

/*
 * Stocke les informations locales essentielles d'un succès dans le cache
 * catalogue.
 */
public sealed class SuccesCatalogueLocal
{
    public int AchievementId { get; set; }

    public string Titre { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public int Points { get; set; }

    public string BadgeName { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public DateTimeOffset DateMajUtc { get; set; }
}
