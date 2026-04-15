/*
 * Déclare les phases possibles de l'orchestrateur d'état du jeu visible.
 */
namespace RA.Compagnon.Modeles.Etat;

/*
 * Représente les grandes phases traversées par un jeu entre détection,
 * chargement et affichage.
 */
public enum PhaseOrchestrateurJeu
{
    AucunJeu = 0,
    DetectionLocale = 1,
    JeuLocalConfirme = 2,
    ChargementApi = 3,
    JeuAffiche = 4,
    ErreurChargement = 5,
}