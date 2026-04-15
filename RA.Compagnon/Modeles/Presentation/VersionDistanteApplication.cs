/*
 * Représente le manifeste distant minimal utilisé pour vérifier les mises à
 * jour de l'application.
 */
namespace RA.Compagnon.Modeles.Presentation;

/*
 * Transporte la version, l'URL, les notes et la date de publication d'une
 * mise à jour distante.
 */
public sealed class VersionDistanteApplication
{
    public string Version { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
    public string Notes { get; init; } = string.Empty;
    public string PublishedAt { get; init; } = string.Empty;
}