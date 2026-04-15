/*
 * Déclare les étapes unitaires du pipeline de chargement d'un jeu.
 */
namespace RA.Compagnon.Modeles.Etat;

/*
 * Représente les différentes étapes combinables d'un chargement de jeu.
 */
[Flags]
public enum EtapePipelineChargementJeu
{
    Aucune = 0,
    DonneesMinimales = 1 << 0,
    MetadonneesEnrichies = 1 << 1,
    SuccesCharges = 1 << 2,
    ImagesChargees = 1 << 3,
}