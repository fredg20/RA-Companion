namespace RA.Compagnon.Modeles.Etat;

public enum PhaseOrchestrateurJeu
{
    AucunJeu = 0,
    DetectionLocale = 1,
    JeuLocalConfirme = 2,
    ChargementApi = 3,
    JeuAffiche = 4,
    ErreurChargement = 5,
}
