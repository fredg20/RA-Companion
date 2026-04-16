/*
 * Regroupe le résultat complet de l'analyse hybride exécutée sur un succès de
 * référence et sur l'ensemble des descriptions du jeu.
 */
namespace RA.Compagnon.Modeles.Presentation;

/*
 * Expose les groupes candidats triés par confiance pour un succès donné.
 */
public sealed class ResultatAnalyseDescriptionsSucces
{
    public int IdentifiantSuccesReference { get; init; }

    public string DescriptionReference { get; init; } = string.Empty;

    public GroupeSuccesPotentiel? GroupePrincipal { get; init; }

    public List<GroupeSuccesPotentiel> Groupes { get; init; } = [];
}
