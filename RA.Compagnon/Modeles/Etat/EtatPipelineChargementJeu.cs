/*
 * Décrit l'état courant du pipeline de chargement d'un jeu dans la fenêtre
 * principale.
 */
namespace RA.Compagnon.Modeles.Etat;

/*
 * Transporte l'identité du jeu en cours de chargement, sa version de
 * chargement et les étapes déjà franchies.
 */
public sealed record EtatPipelineChargementJeu(
    int IdentifiantJeu,
    string TitreJeu,
    int VersionChargement,
    EtapePipelineChargementJeu EtapesChargees,
    DateTimeOffset HorodatageDerniereMiseAJourUtc
)
{
    /*
     * Retourne un état de pipeline vide, utilisé comme valeur neutre.
     */
    public static EtatPipelineChargementJeu Vide { get; } =
        new(0, string.Empty, 0, EtapePipelineChargementJeu.Aucune, DateTimeOffset.MinValue);
}