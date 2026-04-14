/*
 * Représente un jeu enregistré dans le catalogue local mis en cache.
 */
namespace RA.Compagnon.Modeles.Catalogue;

/*
 * Stocke les métadonnées locales d'un jeu et ses succès pour la résolution
 * rapide hors ligne.
 */
public sealed class JeuCatalogueLocal
{
    public int GameId { get; set; }

    public string Titre { get; set; } = string.Empty;

    public string TitreNormalise { get; set; } = string.Empty;

    public List<string> TitresAlternatifs { get; set; } = [];

    public int ConsoleId { get; set; }

    public string NomConsole { get; set; } = string.Empty;

    public string ImageBoxArt { get; set; } = string.Empty;

    public string ImageTitre { get; set; } = string.Empty;

    public string ImageEnJeu { get; set; } = string.Empty;

    public DateTimeOffset DateMajUtc { get; set; }

    public List<SuccesCatalogueLocal> Succes { get; set; } = [];
}
