using System.Linq;
using System.Windows;
using System.Windows.Threading;
using RA.Compagnon.Modeles.Presentation;
using RA.Compagnon.Services;
using RA.Compagnon.ViewModels;

/*
 * Regroupe la navigation entre les modules principaux affichés dans la zone
 * centrale de la fenêtre.
 */
namespace RA.Compagnon;

/*
 * Porte la logique de changement de module principal dans l'interface.
 */
public partial class MainWindow
{
    /*
     * Représente les modules principaux actuellement prévus dans la fenêtre.
     */
    private enum ModulePrincipal
    {
        Accueil,
        Bibliotheque,
    }

    /*
     * Bascule l'interface vers le module demandé et met à jour les états
     * visuels associés à la barre d'onglets.
     */
    private void AfficherModulePrincipal(ModulePrincipal module)
    {
        _vueModele.ModuleAccueilActif = module == ModulePrincipal.Accueil;
        _vueModele.ModuleBibliothequeActif = module == ModulePrincipal.Bibliotheque;

        _vueModele.VisibiliteModuleAccueil =
            module == ModulePrincipal.Accueil ? Visibility.Visible : Visibility.Collapsed;
        _vueModele.VisibiliteModuleBibliotheque =
            module == ModulePrincipal.Bibliotheque ? Visibility.Visible : Visibility.Collapsed;

        _vueModele.TitreModuleActif = module switch
        {
            ModulePrincipal.Accueil => "Accueil",
            ModulePrincipal.Bibliotheque => "Bibliothèque",
            _ => "Accueil",
        };

        if (module == ModulePrincipal.Accueil)
        {
            _ = Dispatcher.BeginInvoke(AjusterDisposition, DispatcherPriority.Loaded);
        }
    }

    /*
     * Recharge le contenu du module Bibliothèque à partir des jeux récents du
     * compte ou, à défaut, du dernier jeu mémorisé localement.
     */
    private void RafraichirModuleBibliotheque()
    {
        List<BibliothequeJeuViewModel> jeux = [];

        if (_dernierResumeUtilisateurCharge is not null)
        {
            CompteAffiche compte = ServicePresentationCompte.Construire(
                new DonneesCompteUtilisateur
                {
                    Profil = _dernierProfilUtilisateurCharge,
                    Resume = _dernierResumeUtilisateurCharge,
                },
                _configurationConnexion.Pseudo
            );

            jeux.AddRange(
                compte.JeuxRecemmentJoues.Select(jeu => new BibliothequeJeuViewModel
                {
                    Titre = jeu.Titre,
                    SousTitre = jeu.SousTitre,
                })
            );
        }

        if (
            jeux.Count == 0
            && _configurationConnexion.DernierJeuAffiche is { } dernierJeu
            && !string.IsNullOrWhiteSpace(dernierJeu.Titre)
        )
        {
            string sousTitre =
                string.IsNullOrWhiteSpace(dernierJeu.ResumeProgression) ? dernierJeu.EtatJeu
                : string.IsNullOrWhiteSpace(dernierJeu.EtatJeu) ? dernierJeu.ResumeProgression
                : $"{dernierJeu.EtatJeu} - {dernierJeu.ResumeProgression}";

            jeux.Add(
                new BibliothequeJeuViewModel
                {
                    Titre = dernierJeu.Titre,
                    SousTitre = string.IsNullOrWhiteSpace(sousTitre)
                        ? "Dernier jeu mémorisé localement"
                        : sousTitre,
                }
            );
        }

        _vueModele.RemplacerJeuxBibliotheque(jeux, "Aucun jeu récent disponible pour le moment.");
    }
}
