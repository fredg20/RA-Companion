using RA.Compagnon.Modeles.Etat;

/*
 * Regroupe le suivi interne du pipeline de chargement d'un jeu dans la
 * fenêtre principale.
 */
namespace RA.Compagnon;

/*
 * Porte les helpers qui suivent les étapes déjà franchies lors d'un
 * chargement de jeu en cours.
 */
public partial class MainWindow
{
    /*
     * Réinitialise l'état du pipeline de chargement courant.
     */
    private void ReinitialiserPipelineChargementJeu()
    {
        _etatPipelineChargementJeu = EtatPipelineChargementJeu.Vide;
    }

    /*
     * Initialise le suivi du pipeline pour un nouveau chargement de jeu.
     */
    private void DemarrerPipelineChargementJeu(
        int identifiantJeu,
        string titreJeu,
        int versionChargement
    )
    {
        _etatPipelineChargementJeu = new EtatPipelineChargementJeu(
            identifiantJeu,
            titreJeu?.Trim() ?? string.Empty,
            versionChargement,
            EtapePipelineChargementJeu.Aucune,
            DateTimeOffset.UtcNow
        );
        JournaliserDiagnosticChangementJeu(
            "pipeline_debut",
            $"jeu={identifiantJeu};version={versionChargement};titre={titreJeu}"
        );
    }

    /*
     * Marque une étape comme franchie dans le pipeline du jeu courant.
     */
    private void MarquerEtapePipelineChargementJeu(
        EtapePipelineChargementJeu etape,
        int identifiantJeu,
        int versionChargement
    )
    {
        if (
            identifiantJeu <= 0
            || _etatPipelineChargementJeu.IdentifiantJeu != identifiantJeu
            || _etatPipelineChargementJeu.VersionChargement != versionChargement
        )
        {
            return;
        }

        EtapePipelineChargementJeu etapesChargees =
            _etatPipelineChargementJeu.EtapesChargees | etape;

        if (etapesChargees == _etatPipelineChargementJeu.EtapesChargees)
        {
            return;
        }

        _etatPipelineChargementJeu = _etatPipelineChargementJeu with
        {
            EtapesChargees = etapesChargees,
            HorodatageDerniereMiseAJourUtc = DateTimeOffset.UtcNow,
        };
        JournaliserDiagnosticChangementJeu(
            "pipeline_etape",
            $"jeu={identifiantJeu};version={versionChargement};etape={etape};etapes={etapesChargees}"
        );
    }

    /*
     * Indique si un couple jeu/version correspond encore au pipeline actif.
     */
    private bool PipelineChargementJeuEstActuel(int identifiantJeu, int versionChargement)
    {
        return identifiantJeu > 0
            && versionChargement > 0
            && _etatPipelineChargementJeu.IdentifiantJeu == identifiantJeu
            && _etatPipelineChargementJeu.VersionChargement == versionChargement;
    }
}