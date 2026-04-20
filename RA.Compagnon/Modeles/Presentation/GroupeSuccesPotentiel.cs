/*
 * Transporte un regroupement potentiel de succès autour d'une ancre commune
 * détectée dans leurs descriptions.
 */
namespace RA.Compagnon.Modeles.Presentation;

/*
 * Représente une cohérence candidate exploitable par l'interface ou les
 * diagnostics pour rapprocher plusieurs succès entre eux.
 */
public sealed class GroupeSuccesPotentiel
{
    public TypeGroupeSuccesPotentiel TypeGroupe { get; init; } = TypeGroupeSuccesPotentiel.Inconnu;

    public string Ancre { get; init; } = string.Empty;

    public string RegleSource { get; init; } = string.Empty;

    public int ScoreConfiance { get; init; }

    public int ScoreSelection { get; init; }

    public int BonusSelectionType { get; init; }

    public int BonusAlignementZone { get; init; }

    public string LibelleConfiance { get; init; } = string.Empty;

    public List<int> IdentifiantsSucces { get; init; } = [];
}
