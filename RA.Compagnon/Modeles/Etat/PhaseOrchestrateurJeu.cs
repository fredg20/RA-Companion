namespace RA.Compagnon.Modeles.Etat;

/// <summary>
/// Représente la phase principale du flux de changement de jeu.
/// </summary>
public enum PhaseOrchestrateurJeu
{
    AucunJeu = 0,
    DetectionLocale = 1,
    JeuLocalConfirme = 2,
    ChargementApi = 3,
    JeuAffiche = 4,
    ErreurChargement = 5,
}
