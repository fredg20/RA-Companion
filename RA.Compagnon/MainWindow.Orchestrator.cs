using RA.Compagnon.Modeles.Etat;

/*
 * Regroupe les interactions entre la fenêtre principale et l'orchestrateur
 * d'état de jeu affiché.
 */
namespace RA.Compagnon;

/*
 * Porte la logique d'enregistrement des phases transitoires du jeu courant
 * dans l'orchestrateur local.
 */
public partial class MainWindow
{
    /*
     * Affiche dans l'interface l'état transitoire actuellement porté par
     * l'orchestrateur de jeu.
     */
    private void AfficherEtatTransitoireOrchestrateur()
    {
        if (!_serviceOrchestrateurEtatJeu.EtatTransitoireEstAffichable())
        {
            return;
        }

        _vueModele.JeuCourant.Progression = "-- / --";
        _vueModele.JeuCourant.Pourcentage =
            _serviceOrchestrateurEtatJeu.ObtenirTexteEtatAffichable();
        _vueModele.JeuCourant.ProgressionValeur = 0;
    }

    /*
     * Enregistre une phase de détection locale dans l'orchestrateur puis
     * applique éventuellement son état transitoire.
     */
    private bool EnregistrerPhaseDetectionLocaleOrchestrateur(
        int identifiantJeu,
        string titreJeu,
        string source,
        bool appliquerAffichage = true
    )
    {
        bool transitionAppliquee = _serviceOrchestrateurEtatJeu.EnregistrerDetectionLocale(
            identifiantJeu,
            titreJeu,
            source
        );

        if (transitionAppliquee && appliquerAffichage)
        {
            AfficherEtatTransitoireOrchestrateur();
        }

        return transitionAppliquee;
    }

    /*
     * Enregistre une phase de chargement API dans l'orchestrateur puis
     * applique éventuellement son état transitoire.
     */
    private bool EnregistrerPhaseChargementApiOrchestrateur(
        int identifiantJeu,
        string titreJeu,
        string source,
        bool appliquerAffichage = true
    )
    {
        bool transitionAppliquee = _serviceOrchestrateurEtatJeu.EnregistrerChargement(
            identifiantJeu,
            titreJeu,
            source
        );

        if (transitionAppliquee && appliquerAffichage)
        {
            AfficherEtatTransitoireOrchestrateur();
        }

        return transitionAppliquee;
    }

    /*
     * Enregistre la phase où un jeu est pleinement affiché à l'écran.
     */
    private bool EnregistrerPhaseJeuAfficheOrchestrateur(
        int identifiantJeu,
        string titreJeu,
        string source
    )
    {
        return _serviceOrchestrateurEtatJeu.EnregistrerJeuAffiche(identifiantJeu, titreJeu, source);
    }

    /*
     * Enregistre la phase où aucun jeu n'est actuellement détecté.
     */
    private bool EnregistrerPhaseAucunJeuOrchestrateur(string source)
    {
        bool transitionAppliquee = _serviceOrchestrateurEtatJeu.EnregistrerAucunJeu(source);

        if (transitionAppliquee)
        {
            AfficherEtatTransitoireOrchestrateur();
        }

        return transitionAppliquee;
    }

    /*
     * Enregistre une phase d'erreur de chargement pour le jeu courant.
     */
    private bool EnregistrerPhaseErreurChargementOrchestrateur(
        int identifiantJeu,
        string titreJeu,
        string source
    )
    {
        bool transitionAppliquee = _serviceOrchestrateurEtatJeu.EnregistrerErreurChargement(
            identifiantJeu,
            titreJeu,
            source
        );

        if (transitionAppliquee)
        {
            AfficherEtatTransitoireOrchestrateur();
        }

        return transitionAppliquee;
    }
}