namespace RA.Compagnon.Modeles.Catalogue;

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
