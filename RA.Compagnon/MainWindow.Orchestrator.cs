using RA.Compagnon.Modeles.Etat;

namespace RA.Compagnon;

public partial class MainWindow
{
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

    private bool EnregistrerPhaseJeuAfficheOrchestrateur(
        int identifiantJeu,
        string titreJeu,
        string source
    )
    {
        return _serviceOrchestrateurEtatJeu.EnregistrerJeuAffiche(identifiantJeu, titreJeu, source);
    }

    private bool EnregistrerPhaseAucunJeuOrchestrateur(string source)
    {
        bool transitionAppliquee = _serviceOrchestrateurEtatJeu.EnregistrerAucunJeu(source);

        if (transitionAppliquee)
        {
            AfficherEtatTransitoireOrchestrateur();
        }

        return transitionAppliquee;
    }

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
