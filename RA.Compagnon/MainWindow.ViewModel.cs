using System.Windows;
using System.Windows.Media;
using RA.Compagnon.ViewModels;

namespace RA.Compagnon;

public partial class MainWindow
{
    private readonly MainWindowViewModel _vueModele = new();

    private void InitialiserVueModele()
    {
        DataContext = _vueModele;
        _vueModele.TitreFenetre = "Compagnon RA";
        _vueModele.TitreCarteJeuEnCours = "Dernier jeu joué";
        _vueModele.EtatConnexion = _etatConnexionCourant;
        _vueModele.VisibiliteContenuPrincipal = Visibility.Hidden;
        _vueModele.VisibiliteCarteConnexion = Visibility.Collapsed;
        _vueModele.VisibiliteCarteJeuEnCours = Visibility.Visible;
        _vueModele.VisibiliteMiseAJourApplication = Visibility.Collapsed;
        _vueModele.VisibiliteSynchronisationJeu = Visibility.Hidden;
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
        _vueModele.ContourOrdreSuccesNormal = new SolidColorBrush(Color.FromRgb(120, 200, 255));
        _vueModele.ContourOrdreSuccesAleatoire = new SolidColorBrush(
            Color.FromArgb(140, 255, 255, 255)
        );
        _vueModele.ContourOrdreSuccesFacile = new SolidColorBrush(
            Color.FromArgb(140, 255, 255, 255)
        );
        _vueModele.ContourOrdreSuccesDifficile = new SolidColorBrush(
            Color.FromArgb(140, 255, 255, 255)
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
        _vueModele.JeuCourant.VisuelsSecondairesVisible = false;
        _vueModele.JeuCourant.LibelleVisuelCourant = string.Empty;
        _vueModele.JeuCourant.TexteVisuelPrincipal = string.Empty;
        _vueModele.JeuCourant.TexteVisuelPrincipalVisible = false;
        _vueModele.JeuCourant.ActionVisuelPrecedentActivee = false;
        _vueModele.JeuCourant.ActionVisuelSuivantActivee = false;
        _vueModele.Compte.LibelleBouton = "Connexion";
        _vueModele.ConfigurerActionAfficherCompte(() => _ = ExecuterActionAfficherCompteAsync());
        _vueModele.ConfigurerActionAfficherAide(() => _ = ExecuterActionAfficherAideAsync());
        _vueModele.ConfigurerActionMiseAJourApplication(() =>
            _ = ExecuterActionMiseAJourApplicationAsync()
        );
        _vueModele.ConfigurerActionRechargerJeu(() => _ = ExecuterActionRechargerJeuEnCoursAsync());
        _vueModele.ConfigurerActionOrdreSuccesNormal(() =>
            _ = ChangerOrdreSuccesGrilleAsync(OrdreSuccesGrille.Normal)
        );
        _vueModele.ConfigurerActionOrdreSuccesAleatoire(() =>
            _ = ChangerOrdreSuccesGrilleAsync(OrdreSuccesGrille.Aleatoire)
        );
        _vueModele.ConfigurerActionOrdreSuccesFacile(() =>
            _ = ChangerOrdreSuccesGrilleAsync(OrdreSuccesGrille.Facile)
        );
        _vueModele.ConfigurerActionOrdreSuccesDifficile(() =>
            _ = ChangerOrdreSuccesGrilleAsync(OrdreSuccesGrille.Difficile)
        );
        _vueModele.SuccesEnCours.ConfigurerNavigationPrecedente(() =>
            _ = ExecuterNavigationSuccesEnCoursAsync(-1)
        );
        _vueModele.SuccesEnCours.ConfigurerNavigationSuivante(() =>
            _ = ExecuterNavigationSuccesEnCoursAsync(1)
        );
        _vueModele.SuccesEnCours.ConfigurerActionPasser(() =>
            _ = ExecuterPassageSuccesEnCoursAsync()
        );
        _vueModele.JeuCourant.ConfigurerActionRejouer(ExecuterActionRejouerJeuEnCours);
        _vueModele.JeuCourant.ConfigurerActionDetails(() =>
            _ = ExecuterActionVueDetailleeJeuEnCoursAsync()
        );
        _vueModele.JeuCourant.ConfigurerActionVisuelPrecedent(() =>
            _ = ExecuterActionVisuelJeuPrecedentAsync()
        );
        _vueModele.JeuCourant.ConfigurerActionVisuelSuivant(() =>
            _ = ExecuterActionVisuelJeuSuivantAsync()
        );
    }
}
