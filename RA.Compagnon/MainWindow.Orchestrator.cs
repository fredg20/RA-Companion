using RA.Compagnon.Modeles.Etat;

namespace RA.Compagnon;

public partial class MainWindow
{
    /// <summary>
    /// Applique l'état transitoire centralisé de la carte jeu lorsqu'il est pertinent.
    /// </summary>
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

    /// <summary>
    /// Enregistre une nouvelle phase puis synchronise immédiatement l'état transitoire affiché.
    /// </summary>
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

    /// <summary>
    /// Enregistre une phase de chargement API puis applique le message cohérent associé.
    /// </summary>
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

    /// <summary>
    /// Enregistre le jeu désormais affiché pour fermer les états transitoires.
    /// </summary>
    private bool EnregistrerPhaseJeuAfficheOrchestrateur(
        int identifiantJeu,
        string titreJeu,
        string source
    )
    {
        return _serviceOrchestrateurEtatJeu.EnregistrerJeuAffiche(identifiantJeu, titreJeu, source);
    }

    /// <summary>
    /// Enregistre un état neutre sans jeu et applique le message associé.
    /// </summary>
    private bool EnregistrerPhaseAucunJeuOrchestrateur(string source)
    {
        bool transitionAppliquee = _serviceOrchestrateurEtatJeu.EnregistrerAucunJeu(source);

        if (transitionAppliquee)
        {
            AfficherEtatTransitoireOrchestrateur();
        }

        return transitionAppliquee;
    }

    /// <summary>
    /// Enregistre un état d'erreur de chargement et applique le message associé.
    /// </summary>
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
