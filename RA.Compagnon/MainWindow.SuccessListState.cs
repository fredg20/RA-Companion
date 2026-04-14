using System.Windows.Media.Animation;
using SystemControls = System.Windows.Controls;

/*
 * Regroupe les types internes qui décrivent l'état de la liste animée
 * des succès affichés dans la fenêtre principale.
 */
namespace RA.Compagnon;

/*
 * Porte les structures internes utilisées pour suivre l'interaction
 * et l'animation de la liste des succès.
 */
public partial class MainWindow
{
    /*
     * Décrit les différents états d'interaction possibles avec la liste des succès.
     */
    private enum EtatInteractionListeSucces
    {
        AutoScroll,
        PauseSurvol,
        InteractionManuelle,
    }

    /*
     * Regroupe l'ensemble de l'état UI nécessaire au pilotage de la grille
     * et du défilement des succès.
     */
    private sealed class EtatListeSuccesUi
    {
        public SystemControls.Primitives.ScrollBar? BarreDefilementVerticale { get; set; }

        public bool MiseAJourAnimationPlanifiee { get; set; }

        public bool AjustementHauteurPlanifie { get; set; }

        public bool MiseAJourDispositionPlanifiee { get; set; }

        public bool RelayoutApresRedimensionnementPlanifie { get; set; }

        public bool RedimensionnementFenetreActif { get; set; }

        public bool AnimationVersBas { get; set; } = true;

        public EtatInteractionListeSucces EtatInteraction { get; set; } =
            EtatInteractionListeSucces.AutoScroll;

        public bool SurvolBadgeActif => EtatInteraction == EtatInteractionListeSucces.PauseSurvol;

        public bool InteractionActive =>
            EtatInteraction == EtatInteractionListeSucces.InteractionManuelle;

        public double DernierOffsetInteraction { get; set; }

        public double DerniereHauteurSectionStable { get; set; }

        public double DerniereHauteurVisibleStable { get; set; }

        public int VersionChargementGrille { get; set; }

        public string SignatureAnimation { get; set; } = string.Empty;

        public string SignatureOrdreAleatoire { get; set; } = string.Empty;

        public int IdentifiantJeuOrdreAleatoire { get; set; }

        public int? IdentifiantSuccesTemporaire { get; set; }

        public int? IdentifiantSuccesEpingle { get; set; }

        public bool RetourPremierSuccesApresSelectionTemporaire { get; set; }

        public double AmplitudeAnimation { get; set; }

        public AnimationClock? HorlogeAnimation { get; set; }

        public Dictionary<int, int> PositionsAleatoires { get; } = [];

        public List<int> SuccesPasses { get; } = [];

        public OrdreSuccesGrille OrdreCourant { get; set; } = OrdreSuccesGrille.Normal;
    }
}
