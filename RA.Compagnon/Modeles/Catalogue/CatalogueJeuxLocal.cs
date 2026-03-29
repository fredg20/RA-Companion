namespace RA.Compagnon.Modeles.Catalogue;

public sealed class CatalogueJeuxLocal
{
    public DateTimeOffset DateMajUtc { get; set; }

    public List<JeuCatalogueLocal> Jeux { get; set; } = [];
}
