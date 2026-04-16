/*
 * Transporte le résultat structuré de l'analyse d'un message de Rich Presence
 * afin de déterminer si une zone courante exploitable a été détectée.
 */
namespace RA.Compagnon.Modeles.Presentation;

/*
 * Regroupe la zone détectée, son type et le niveau de confiance associé.
 */
public sealed class AnalyseZoneRichPresence
{
    public string TexteSource { get; init; } = string.Empty;

    public string ResumeCourt { get; init; } = string.Empty;

    public string ZoneDetectee { get; init; } = string.Empty;

    public string LibelleType { get; init; } = string.Empty;

    public TypeZoneRichPresence TypeZone { get; init; }

    public int ScoreConfiance { get; init; }

    public bool EstFiable { get; init; }
}
