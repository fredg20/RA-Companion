using System.Windows;
using System.Windows.Media;
using RA.Compagnon.ViewModels;

/*
 * Regroupe l'initialisation du ViewModel principal et le branchement des
 * actions UI sur la fenêtre principale.
 */
namespace RA.Compagnon;

/*
 * Porte la logique de liaison entre la fenêtre principale et son ViewModel.
 */
public partial class MainWindow
{
    private readonly MainWindowViewModel _vueModele = new();

    /*
     * Initialise le ViewModel principal avec ses valeurs par défaut et
     * branche toutes les actions exposées à l'interface.
     */
    private void InitialiserVueModele()
    {
        DataContext = _vueModele;
        _vueModele.TitreFenetre = "Compagnon RA";
        _vueModele.TitreCarteJeuEnCours = "Dernier jeu joué";
        _vueModele.TitreModuleActif = "Accueil";
        _vueModele.MessageBibliotheque = "Aucun jeu récent disponible pour le moment.";
        _vueModele.EtatConnexion = _etatConnexionCourant;
        _vueModele.VisibiliteContenuPrincipal = Visibility.Hidden;
        _vueModele.VisibiliteBarreModules = Visibility.Collapsed;
        _vueModele.VisibiliteCarteConnexion = Visibility.Collapsed;
        _vueModele.VisibiliteCarteJeuEnCours = Visibility.Visible;
        _vueModele.VisibiliteMiseAJourApplication = Visibility.Collapsed;
        _vueModele.VisibiliteSynchronisationJeu = Visibility.Hidden;
        _vueModele.VisibiliteModuleAccueil = Visibility.Visible;
        _vueModele.VisibiliteModuleBibliotheque = Visibility.Collapsed;
        _vueModele.VisibiliteBibliothequeListe = Visibility.Collapsed;
        _vueModele.VisibiliteBibliothequeVide = Visibility.Visible;
        _vueModele.ModuleAccueilActif = true;
        _vueModele.ModuleBibliothequeActif = false;
        _vueModele.MiseAJourApplicationActivee = false;
        _vueModele.LibelleMiseAJourApplication = "Mise à jour";
        _vueModele.ToolTipMiseAJourApplication = string.Empty;
        _vueModele.EtatSynchronisationJeu = string.Empty;
        _vueModele.LibelleRechargerJeu = "Recharger";
        _vueModele.ToolTipRechargerJeu = string.Empty;
        _vueModele.RechargerJeuActif = false;
        _vueModele.LibelleOrdreSuccesGrille = "Normal";
        _vueModele.OrdreSuccesNormalActif = true;
        _vueModele.OrdreSuccesAleatoireActif = false;
        _vueModele.OrdreSuccesFacileActif = false;
        _vueModele.OrdreSuccesDifficileActif = false;
        _vueModele.ContourOrdreSuccesNormal = ObtenirPinceauTheme(
            "PinceauAccentInformation",
            ConstantesDesign.CouleurRepliAccentInformation
        );
        _vueModele.ContourOrdreSuccesAleatoire = ObtenirPinceauTheme(
            "PinceauContourInactif",
            ConstantesDesign.CouleurRepliContourInactif
        );
        _vueModele.ContourOrdreSuccesFacile = ObtenirPinceauTheme(
            "PinceauContourInactif",
            ConstantesDesign.CouleurRepliContourInactif
        );
        _vueModele.ContourOrdreSuccesDifficile = ObtenirPinceauTheme(
            "PinceauContourInactif",
            ConstantesDesign.CouleurRepliContourInactif
        );
        _vueModele.SuccesEnCours.NavigationVisible = false;
        _vueModele.SuccesEnCours.PrecedentActif = false;
        _vueModele.SuccesEnCours.SuivantActif = false;
        _vueModele.SuccesEnCours.PasserActif = false;
        _vueModele.SuccesEnCours.PrecedentOpacity = 1;
        _vueModele.SuccesEnCours.SuivantOpacity = 1;
        _vueModele.SuccesEnCours.Image = null;
        _vueModele.SuccesEnCours.ImageVisible = false;
        _vueModele.SuccesEnCours.ImageOpacity = 0.58;
        _vueModele.SuccesEnCours.TexteVisuel = string.Empty;
        _vueModele.SuccesEnCours.TexteVisuelVisible = false;
        _vueModele.SuccesEnCours.Titre = string.Empty;
        _vueModele.SuccesEnCours.TitreVisible = false;
        _vueModele.SuccesEnCours.Description = string.Empty;
        _vueModele.SuccesEnCours.DescriptionVisible = false;
        _vueModele.SuccesEnCours.DetailsPoints = string.Empty;
        _vueModele.SuccesEnCours.DetailsPointsVisible = false;
        _vueModele.SuccesEnCours.DetailsFaisabilite = string.Empty;
        _vueModele.SuccesEnCours.DetailsFaisabiliteVisible = false;
        _vueModele.SuccesEnCours.ToolTipDetailsFaisabilite = string.Empty;
        _vueModele.SuccesEnCours.GroupeDetecteType = string.Empty;
        _vueModele.SuccesEnCours.GroupeDetecteAncre = string.Empty;
        _vueModele.SuccesEnCours.GroupeDetecteQuantite = string.Empty;
        _vueModele.SuccesEnCours.ToolTipGroupeDetecte = string.Empty;
        _vueModele.SuccesEnCours.GroupeDetecteVisible = false;
        _vueModele.SuccesEnCours.BadgesGroupeDetecte.Clear();
        _vueModele.JeuCourant.VisuelsSecondairesVisible = false;
        _vueModele.JeuCourant.LibelleVisuelCourant = string.Empty;
        _vueModele.JeuCourant.TexteVisuelPrincipal = string.Empty;
        _vueModele.JeuCourant.TexteVisuelPrincipalVisible = false;
        _vueModele.JeuCourant.ActionVisuelPrecedentActivee = false;
        _vueModele.JeuCourant.ActionVisuelSuivantActivee = false;
        _vueModele.Compte.LibelleBouton = "Connexion";
        _vueModele.ConfigurerActionAfficherCompte(() =>
            LancerTacheNonBloquante(
                ExecuterActionAfficherCompteAsync(),
                "action_afficher_compte"
            )
        );
        _vueModele.ConfigurerActionAfficherAide(() =>
            LancerTacheNonBloquante(ExecuterActionAfficherAideAsync(), "action_afficher_aide")
        );
        _vueModele.ConfigurerActionMiseAJourApplication(() =>
            LancerTacheNonBloquante(
                ExecuterActionMiseAJourApplicationAsync(),
                "action_mise_a_jour_application"
            )
        );
        _vueModele.ConfigurerActionRechargerJeu(() =>
            LancerTacheNonBloquante(
                ExecuterActionRechargerJeuEnCoursAsync(),
                "action_recharger_jeu"
            )
        );
        _vueModele.ConfigurerActionAfficherModuleAccueil(() =>
            AfficherModulePrincipal(ModulePrincipal.Accueil)
        );
        _vueModele.ConfigurerActionAfficherModuleBibliotheque(() =>
            AfficherModulePrincipal(ModulePrincipal.Bibliotheque)
        );
        _vueModele.ConfigurerActionOrdreSuccesNormal(() =>
            LancerTacheNonBloquante(
                ChangerOrdreSuccesGrilleAsync(OrdreSuccesGrille.Normal),
                "action_ordre_succes_normal"
            )
        );
        _vueModele.ConfigurerActionOrdreSuccesAleatoire(() =>
            LancerTacheNonBloquante(
                ChangerOrdreSuccesGrilleAsync(OrdreSuccesGrille.Aleatoire),
                "action_ordre_succes_aleatoire"
            )
        );
        _vueModele.ConfigurerActionOrdreSuccesFacile(() =>
            LancerTacheNonBloquante(
                ChangerOrdreSuccesGrilleAsync(OrdreSuccesGrille.Facile),
                "action_ordre_succes_facile"
            )
        );
        _vueModele.ConfigurerActionOrdreSuccesDifficile(() =>
            LancerTacheNonBloquante(
                ChangerOrdreSuccesGrilleAsync(OrdreSuccesGrille.Difficile),
                "action_ordre_succes_difficile"
            )
        );
        _vueModele.SuccesEnCours.ConfigurerNavigationPrecedente(() =>
            LancerTacheNonBloquante(
                ExecuterNavigationSuccesEnCoursAsync(-1),
                "navigation_succes_precedent"
            )
        );
        _vueModele.SuccesEnCours.ConfigurerNavigationSuivante(() =>
            LancerTacheNonBloquante(
                ExecuterNavigationSuccesEnCoursAsync(1),
                "navigation_succes_suivant"
            )
        );
        _vueModele.SuccesEnCours.ConfigurerActionPasser(() =>
            LancerTacheNonBloquante(
                ExecuterPassageSuccesEnCoursAsync(),
                "action_passer_succes"
            )
        );
        _vueModele.JeuCourant.ConfigurerActionRejouer(ExecuterActionRejouerJeuEnCours);
        _vueModele.JeuCourant.ConfigurerActionDetails(() =>
            LancerTacheNonBloquante(
                ExecuterActionVueDetailleeJeuEnCoursAsync(),
                "action_details_jeu"
            )
        );
        _vueModele.JeuCourant.ConfigurerActionVisuelPrecedent(() =>
            LancerTacheNonBloquante(
                ExecuterActionVisuelJeuPrecedentAsync(),
                "action_visuel_precedent"
            )
        );
        _vueModele.JeuCourant.ConfigurerActionVisuelSuivant(() =>
            LancerTacheNonBloquante(
                ExecuterActionVisuelJeuSuivantAsync(),
                "action_visuel_suivant"
            )
        );
    }
}
