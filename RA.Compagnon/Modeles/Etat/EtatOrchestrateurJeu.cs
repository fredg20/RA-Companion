/*
 * Décrit l'état courant du jeu tel qu'orchestré entre les sources locales
 * et distantes de l'application.
 */
namespace RA.Compagnon.Modeles.Etat;

/*
 * Transporte la phase visible, le jeu concerné et la source ayant produit
 * l'état courant de l'orchestrateur.
 */
public sealed record EtatOrchestrateurJeu(
    PhaseOrchestrateurJeu Phase,
    int IdentifiantJeu,
    string TitreJeu,
    string Source,
    DateTimeOffset HorodatageUtc
)
{
    /*
     * Retourne l'état initial neutre de l'orchestrateur.
     */
    public static EtatOrchestrateurJeu Initial =>
        new(PhaseOrchestrateurJeu.AucunJeu, 0, string.Empty, string.Empty, DateTimeOffset.MinValue);

    /*
     * Indique si l'état courant concerne l'identifiant de jeu fourni.
     */
    public bool ConcerneJeu(int identifiantJeu)
    {
        return identifiantJeu > 0 && IdentifiantJeu == identifiantJeu;
    }
}