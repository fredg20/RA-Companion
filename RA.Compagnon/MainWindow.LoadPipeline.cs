using RA.Compagnon.Modeles.Etat;

namespace RA.Compagnon;

public partial class MainWindow
{
    private void ReinitialiserPipelineChargementJeu()
    {
        _etatPipelineChargementJeu = EtatPipelineChargementJeu.Vide;
    }

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

    private bool PipelineChargementJeuEstActuel(int identifiantJeu, int versionChargement)
    {
        return identifiantJeu > 0
            && versionChargement > 0
            && _etatPipelineChargementJeu.IdentifiantJeu == identifiantJeu
            && _etatPipelineChargementJeu.VersionChargement == versionChargement;
    }
}
