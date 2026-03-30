using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RA.Compagnon.Modeles.Api.V2.Game;
using RA.Compagnon.Modeles.Local;
using RA.Compagnon.Modeles.Presentation;
using RA.Compagnon.Services;

namespace RA.Compagnon;

public partial class MainWindow
{
    /// <summary>
    /// Réinitialise la zone du premier succès non débloqué.
    /// </summary>
    private void ReinitialiserPremierSuccesNonDebloque()
    {
        ImagePremierSuccesNonDebloque.Source = null;
        ImagePremierSuccesNonDebloque.Clip = null;
        ImagePremierSuccesNonDebloque.Visibility = Visibility.Collapsed;
        TexteImagePremierSuccesNonDebloque.Text = string.Empty;
        TexteImagePremierSuccesNonDebloque.Visibility = Visibility.Collapsed;
        TexteTitrePremierSuccesNonDebloque.Text = string.Empty;
        TexteTitrePremierSuccesNonDebloque.Visibility = Visibility.Collapsed;
        TexteDescriptionPremierSuccesNonDebloque.Text = string.Empty;
        TexteDescriptionPremierSuccesNonDebloque.Visibility = Visibility.Collapsed;
        TextePointsPremierSuccesNonDebloque.Text = string.Empty;
        TextePointsPremierSuccesNonDebloque.Visibility = Visibility.Collapsed;
        TexteFaisabilitePremierSuccesNonDebloque.Text = string.Empty;
        TexteFaisabilitePremierSuccesNonDebloque.Visibility = Visibility.Collapsed;
        MettreAJourNavigationSuccesEnCours(null);
    }

    /// <summary>
    /// Réinitialise la grille de tous les rétrosuccès.
    /// </summary>
    private void ReinitialiserGrilleTousSucces()
    {
        _survolBadgeGrilleSuccesActif = false;
        _animationGrilleSuccesVersBas = true;
        _amplitudeAnimationGrilleSucces = 0;
        _minuteurRepriseAnimationGrilleSucces.Stop();
        GrilleTousSuccesJeuEnCours.Children.Clear();
        PlanifierMiseAJourAnimationGrilleTousSucces();
    }

    /// <summary>
    /// Efface les zones de rétrosuccès et leur état persisté pour éviter de garder ceux d'un ancien jeu.
    /// </summary>
    private void ReinitialiserSuccesAffichesEtPersistes()
    {
        _identifiantJeuSuccesCourant = 0;
        _succesJeuCourant = [];
        _succesDebloquesLocauxTemporaires.Clear();
        _succesDetectesRecemment.Clear();
        _succesDebloqueDetecteEnAttente = null;
        _identifiantSuccesGrilleTemporaire = null;
        _identifiantSuccesGrilleEpingle = null;
        _minuteurAffichageTemporaireSuccesGrille.Stop();
        ReinitialiserPremierSuccesNonDebloque();
        ReinitialiserGrilleTousSucces();

        if (_configurationConnexion.DernierSuccesAffiche is not null)
        {
            _configurationConnexion.DernierSuccesAffiche = null;
            _dernierSuccesAfficheModifie = true;
        }

        if (_configurationConnexion.DerniereListeSuccesAffichee is not null)
        {
            _configurationConnexion.DerniereListeSuccesAffichee = null;
            _derniereListeSuccesAfficheeModifiee = true;
        }
    }

    /// <summary>
    /// Met • jour l'affichage des succès du jeu courant.
    /// </summary>
    private async Task MettreAJourSuccesJeuAsync(GameInfoAndUserProgressV2 jeu)
    {
        List<GameAchievementV2> succes = InitialiserContexteSuccesJeu(jeu);
        await MettreAJourPremierSuccesNonDebloqueAsync(jeu.IdentifiantJeu, succes);
        DemarrerMiseAJourGrilleTousSuccesEnArrierePlan(jeu.IdentifiantJeu, succes);

        if (
            _succesDebloqueDetecteEnAttente is not null
            && _succesDebloqueDetecteEnAttente.IdentifiantJeu == jeu.IdentifiantJeu
        )
        {
            _ = await AfficherSuccesDebloqueDetecteAsync(_succesDebloqueDetecteEnAttente);
        }
    }

    private List<GameAchievementV2> InitialiserContexteSuccesJeu(GameInfoAndUserProgressV2 jeu)
    {
        List<GameAchievementV2> succes =
        [
            .. jeu.Succes.Values.OrderBy(item => item.DisplayOrder).ThenBy(item => item.Id),
        ];

        _identifiantJeuSuccesCourant = jeu.IdentifiantJeu;
        FusionnerSuccesDebloquesLocauxTemporaires(jeu.IdentifiantJeu, succes);
        _succesJeuCourant = succes;
        return succes;
    }

    private void DemarrerMiseAJourSuccesJeuEnArrierePlan(GameInfoAndUserProgressV2 jeu)
    {
        _ = MettreAJourSuccesJeuEnArrierePlanAsync(jeu);
    }

    private async Task MettreAJourSuccesJeuEnArrierePlanAsync(GameInfoAndUserProgressV2 jeu)
    {
        try
        {
            await MettreAJourSuccesJeuAsync(jeu);
        }
        catch
        {
            // Les succès enrichissent l'affichage, mais ne doivent pas bloquer la carte principale.
        }
    }

    /// <summary>
    /// Charge la grille complète des succès sans bloquer l'affichage du succès principal.
    /// </summary>
    private void DemarrerMiseAJourGrilleTousSuccesEnArrierePlan(
        int identifiantJeu,
        List<GameAchievementV2> succes
    )
    {
        _ = MettreAJourGrilleTousSuccesEnArrierePlanAsync(identifiantJeu, succes);
    }

    /// <summary>
    /// Exécute le remplissage complet de la grille en arrière-plan et ignore les erreurs non critiques.
    /// </summary>
    private async Task MettreAJourGrilleTousSuccesEnArrierePlanAsync(
        int identifiantJeu,
        List<GameAchievementV2> succes
    )
    {
        try
        {
            await MettreAJourGrilleTousSuccesAsync(identifiantJeu, succes);
        }
        catch
        {
            // La grille complète enrichit l'interface, mais ne doit pas bloquer le rendu principal.
        }
    }

    /// <summary>
    /// Choisit le rétrosuccès en cours en suivant l'ordre réel de la grille quand aucun badge n'est sélectionné.
    /// </summary>
    private (
        GameAchievementV2? Succes,
        bool DoitSauvegarder,
        bool EstEpingleManuellement
    ) SelectionnerSuccesEnCours(List<GameAchievementV2> succes)
    {
        if (_identifiantJeuSuccesCourant > 0)
        {
            if (_identifiantSuccesGrilleTemporaire.HasValue)
            {
                GameAchievementV2? succesTemporaire = succes.FirstOrDefault(item =>
                    item.Id == _identifiantSuccesGrilleTemporaire.Value
                );

                if (succesTemporaire is not null)
                {
                    return (succesTemporaire, false, false);
                }
            }

            if (_identifiantSuccesGrilleEpingle.HasValue)
            {
                GameAchievementV2? succesEpingle = succes.FirstOrDefault(item =>
                    item.Id == _identifiantSuccesGrilleEpingle.Value
                );

                if (succesEpingle is not null)
                {
                    return (succesEpingle, true, true);
                }
            }
        }

        List<GameAchievementV2> succesNonDebloques =
        [
            .. OrdonnerSuccesPourGrilleSelonMode(_identifiantJeuSuccesCourant, succes)
                .Where(item => !SuccesEstDebloquePourAffichage(item)),
        ];
        GameAchievementV2? premierSuccesNonDebloque = succesNonDebloques.FirstOrDefault();

        return (premierSuccesNonDebloque, true, false);
    }

    /// <summary>
    /// Met • jour la carte du premier succès restant • débloquer.
    /// </summary>
    private async Task MettreAJourPremierSuccesNonDebloqueAsync(
        int identifiantJeu,
        List<GameAchievementV2> succes
    )
    {
        (GameAchievementV2? premierSucces, bool doitSauvegarder, bool estEpingleManuellement) =
            SelectionnerSuccesEnCours(succes);

        await AppliquerSuccesEnCoursAsync(
            identifiantJeu,
            premierSucces,
            doitSauvegarder,
            estEpingleManuellement
        );
    }

    /// <summary>
    /// Applique le succès choisi à la carte principale, qu'il provienne du mode automatique ou d'un clic sur la grille.
    /// </summary>
    private async Task AppliquerSuccesEnCoursAsync(
        int identifiantJeu,
        GameAchievementV2? succesSelectionne,
        bool doitSauvegarder,
        bool estEpingleManuellement
    )
    {
        if (succesSelectionne is null)
        {
            ImagePremierSuccesNonDebloque.Source = null;
            ImagePremierSuccesNonDebloque.Visibility = Visibility.Collapsed;
            TexteImagePremierSuccesNonDebloque.Visibility = Visibility.Visible;
            TexteImagePremierSuccesNonDebloque.Text = "Tous les succès sont débloqués";
            TexteTitrePremierSuccesNonDebloque.Text = "Tous les succès sont obtenus";
            TexteTitrePremierSuccesNonDebloque.Visibility = Visibility.Visible;
            TexteDescriptionPremierSuccesNonDebloque.Text =
                "Ce jeu ne contient plus de succès • débloquer.";
            TexteDescriptionPremierSuccesNonDebloque.Visibility = Visibility.Visible;
            TextePointsPremierSuccesNonDebloque.Text = string.Empty;
            TextePointsPremierSuccesNonDebloque.Visibility = Visibility.Collapsed;
            TexteFaisabilitePremierSuccesNonDebloque.Text = string.Empty;
            TexteFaisabilitePremierSuccesNonDebloque.Visibility = Visibility.Collapsed;
            MettreAJourNavigationSuccesEnCours(null);
            SauvegarderDernierSuccesAffiche(
                new EtatSuccesAfficheLocal
                {
                    IdentifiantJeu = identifiantJeu,
                    IdentifiantSucces = 0,
                    Titre = TexteTitrePremierSuccesNonDebloque.Text,
                    Description = TexteDescriptionPremierSuccesNonDebloque.Text,
                    DetailsPoints = string.Empty,
                    DetailsFaisabilite = string.Empty,
                    TexteVisuel = TexteImagePremierSuccesNonDebloque.Text,
                }
            );
            return;
        }

        SuccesAffiche succesAffiche = Services.ServicePresentationSucces.Construire(
            succesSelectionne,
            _succesJeuCourant,
            identifiantJeu
        );
        ImageSource? imageSucces = await ChargerImageDistanteAsync(succesAffiche.UrlBadge);

        if (imageSucces is not null)
        {
            ImagePremierSuccesNonDebloque.Source = succesAffiche.EstDebloque
                ? imageSucces
                : ConvertirImageEnNoirEtBlanc(imageSucces);
            ImagePremierSuccesNonDebloque.Opacity = succesAffiche.EstDebloque ? 1 : 0.58;
            ImagePremierSuccesNonDebloque.Visibility = Visibility.Visible;
            TexteImagePremierSuccesNonDebloque.Visibility = Visibility.Collapsed;
            AppliquerCoinsArrondisImagePremierSuccesNonDebloque();
        }
        else
        {
            ImagePremierSuccesNonDebloque.Source = null;
            ImagePremierSuccesNonDebloque.Clip = null;
            ImagePremierSuccesNonDebloque.Opacity = 0.58;
            ImagePremierSuccesNonDebloque.Visibility = Visibility.Collapsed;
            TexteImagePremierSuccesNonDebloque.Visibility = Visibility.Visible;
            TexteImagePremierSuccesNonDebloque.Text = "Visuel indisponible";
        }

        TexteTitrePremierSuccesNonDebloque.Text = succesAffiche.Titre;
        TexteTitrePremierSuccesNonDebloque.Visibility = Visibility.Visible;
        string descriptionSucces = await _serviceTraductionTexte.TraduireVersFrancaisAsync(
            succesAffiche.Description
        );
        TexteDescriptionPremierSuccesNonDebloque.Text = descriptionSucces.Trim();
        TexteDescriptionPremierSuccesNonDebloque.Visibility = Visibility.Visible;
        TextePointsPremierSuccesNonDebloque.Text = succesAffiche.DetailsPoints;
        TextePointsPremierSuccesNonDebloque.Visibility = string.IsNullOrWhiteSpace(
            succesAffiche.DetailsPoints
        )
            ? Visibility.Collapsed
            : Visibility.Visible;
        TexteFaisabilitePremierSuccesNonDebloque.Text = succesAffiche.DetailsFaisabilite;
        TexteFaisabilitePremierSuccesNonDebloque.Visibility = string.IsNullOrWhiteSpace(
            succesAffiche.DetailsFaisabilite
        )
            ? Visibility.Collapsed
            : Visibility.Visible;
        MettreAJourNavigationSuccesEnCours(succesSelectionne);
        if (doitSauvegarder)
        {
            SauvegarderDernierSuccesAffiche(
                new EtatSuccesAfficheLocal
                {
                    IdentifiantJeu = identifiantJeu,
                    IdentifiantSucces = succesSelectionne.Id,
                    Titre = TexteTitrePremierSuccesNonDebloque.Text,
                    Description = TexteDescriptionPremierSuccesNonDebloque.Text,
                    DetailsPoints = TextePointsPremierSuccesNonDebloque.Text,
                    DetailsFaisabilite = TexteFaisabilitePremierSuccesNonDebloque.Text,
                    EstEpingleManuellement = estEpingleManuellement,
                    CheminImageBadge = succesAffiche.UrlBadge,
                    TexteVisuel = TexteImagePremierSuccesNonDebloque.Text,
                }
            );
        }
    }

    private async Task<bool> AfficherSuccesDebloqueDetecteAsync(SuccesDebloqueDetecte succesDetecte)
    {
        if (succesDetecte.IdentifiantJeu <= 0)
        {
            ServiceSurveillanceSuccesLocaux.JournaliserEvenement(
                "succes_ui_ignore",
                $"raison=jeu_invalide;jeu={succesDetecte.IdentifiantJeu};succes={succesDetecte.IdentifiantSucces}"
            );
            return false;
        }

        _succesDebloqueDetecteEnAttente = succesDetecte;

        if (
            _identifiantJeuSuccesCourant != succesDetecte.IdentifiantJeu
            || _succesJeuCourant.Count == 0
        )
        {
            ServiceSurveillanceSuccesLocaux.JournaliserEvenement(
                "succes_ui_en_attente",
                $"raison=contexte_indisponible;jeu={succesDetecte.IdentifiantJeu};succes={succesDetecte.IdentifiantSucces};jeuCourant={_identifiantJeuSuccesCourant};nbSucces={_succesJeuCourant.Count}"
            );
            return false;
        }

        GameAchievementV2? succes = _succesJeuCourant.FirstOrDefault(item =>
            item.Id == succesDetecte.IdentifiantSucces
        );

        if (succes is null)
        {
            ServiceSurveillanceSuccesLocaux.JournaliserEvenement(
                "succes_ui_ignore",
                $"raison=succes_introuvable;jeu={succesDetecte.IdentifiantJeu};succes={succesDetecte.IdentifiantSucces}"
            );
            return false;
        }

        MarquerSuccesCommeDebloqueLocalement(succes, succesDetecte);
        if (!_succesDebloquesLocauxTemporaires.TryGetValue(succesDetecte.IdentifiantJeu, out HashSet<int>? succesTemp))
        {
            succesTemp = [];
            _succesDebloquesLocauxTemporaires[succesDetecte.IdentifiantJeu] = succesTemp;
        }
        succesTemp.Add(succesDetecte.IdentifiantSucces);
        _succesDebloqueDetecteEnAttente = null;
        _identifiantSuccesGrilleTemporaire = succes.Id;
        _retourPremierSuccesNonDebloqueApresSelectionTemporaire = true;
        _minuteurAffichageTemporaireSuccesGrille.Stop();
        _minuteurAffichageTemporaireSuccesGrille.Start();
        RafraichirStyleBadgesGrilleSucces();

        await AppliquerSuccesEnCoursAsync(_identifiantJeuSuccesCourant, succes, false, false);
        ServiceSurveillanceSuccesLocaux.JournaliserEvenement(
            "succes_ui_affiche",
            $"jeu={succesDetecte.IdentifiantJeu};succes={succesDetecte.IdentifiantSucces};titre={succesDetecte.TitreSucces}"
        );
        return true;
    }

    private static void MarquerSuccesCommeDebloqueLocalement(
        GameAchievementV2 succes,
        SuccesDebloqueDetecte succesDetecte
    )
    {
        string dateObtention = string.IsNullOrWhiteSpace(succesDetecte.DateObtention)
            ? DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
            : succesDetecte.DateObtention.Trim();

        succes.DateEarned = dateObtention;

        if (succesDetecte.Hardcore)
        {
            succes.DateEarnedHardcore = dateObtention;
        }
    }

    private void FusionnerSuccesDebloquesLocauxTemporaires(
        int identifiantJeu,
        List<GameAchievementV2> succes
    )
    {
        if (
            identifiantJeu <= 0
            || !_succesDebloquesLocauxTemporaires.TryGetValue(identifiantJeu, out HashSet<int>? ids)
            || ids.Count == 0
        )
        {
            return;
        }

        string dateSession = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

        foreach (GameAchievementV2 succesJeu in succes.Where(item => ids.Contains(item.Id)))
        {
            if (string.IsNullOrWhiteSpace(succesJeu.DateEarned))
            {
                succesJeu.DateEarned = dateSession;
            }
        }
    }

    private bool SuccesEstDebloquePourAffichage(GameAchievementV2 succes)
    {
        if (
            _identifiantJeuSuccesCourant > 0
            && _succesDebloquesLocauxTemporaires.TryGetValue(
                _identifiantJeuSuccesCourant,
                out HashSet<int>? ids
            )
            && ids.Contains(succes.Id)
        )
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(succes.DateEarned)
            || !string.IsNullOrWhiteSpace(succes.DateEarnedHardcore);
    }

    private bool SuccesDejaTraiteRecemment(SuccesDebloqueDetecte succes)
    {
        string signature = ConstruireSignatureSuccesTraite(succes);

        if (
            _succesDetectesRecemment.TryGetValue(signature, out DateTimeOffset horodatage)
            && DateTimeOffset.UtcNow - horodatage < TimeSpan.FromMinutes(5)
        )
        {
            return true;
        }

        return false;
    }

    private void MarquerSuccesCommeTraite(SuccesDebloqueDetecte succes)
    {
        string signature = ConstruireSignatureSuccesTraite(succes);
        _succesDetectesRecemment[signature] = DateTimeOffset.UtcNow;
    }

    private static string ConstruireSignatureSuccesTraite(SuccesDebloqueDetecte succes)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{succes.IdentifiantJeu}|{succes.IdentifiantSucces}"
        );
    }

    private async void BoutonSuccesEnCoursPrecedent_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        await NaviguerSuccesEnCoursAsync(-1);
    }

    private async void BoutonSuccesEnCoursSuivant_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        await NaviguerSuccesEnCoursAsync(1);
    }

    private async Task NaviguerSuccesEnCoursAsync(int direction)
    {
        if (_identifiantJeuSuccesCourant <= 0 || _succesJeuCourant.Count <= 1)
        {
            return;
        }

        List<GameAchievementV2> succesOrdonnes =
        [
            .. OrdonnerSuccesPourGrilleSelonMode(_identifiantJeuSuccesCourant, _succesJeuCourant),
        ];

        GameAchievementV2? succesCourant = ObtenirSuccesEnCoursSelectionne(succesOrdonnes);

        if (succesCourant is null)
        {
            return;
        }

        int indexCourant = succesOrdonnes.FindIndex(item => item.Id == succesCourant.Id);

        if (indexCourant < 0)
        {
            return;
        }

        int indexCible = Math.Clamp(indexCourant + direction, 0, succesOrdonnes.Count - 1);

        if (indexCible == indexCourant)
        {
            return;
        }

        GameAchievementV2 succesCible = succesOrdonnes[indexCible];
        _identifiantSuccesGrilleEpingle = succesCible.Id;
        _identifiantSuccesGrilleTemporaire = null;
        _retourPremierSuccesNonDebloqueApresSelectionTemporaire = false;
        _minuteurAffichageTemporaireSuccesGrille.Stop();
        RafraichirStyleBadgesGrilleSucces();

        await AppliquerSuccesEnCoursAsync(_identifiantJeuSuccesCourant, succesCible, true, true);
    }

    private GameAchievementV2? ObtenirSuccesEnCoursSelectionne(
        List<GameAchievementV2> succesOrdonnes
    )
    {
        if (_identifiantSuccesGrilleTemporaire.HasValue)
        {
            GameAchievementV2? succesTemporaire = succesOrdonnes.FirstOrDefault(item =>
                item.Id == _identifiantSuccesGrilleTemporaire.Value
            );

            if (succesTemporaire is not null)
            {
                return succesTemporaire;
            }
        }

        if (_identifiantSuccesGrilleEpingle.HasValue)
        {
            GameAchievementV2? succesEpingle = succesOrdonnes.FirstOrDefault(item =>
                item.Id == _identifiantSuccesGrilleEpingle.Value
            );

            if (succesEpingle is not null)
            {
                return succesEpingle;
            }
        }

        return SelectionnerSuccesEnCours(succesOrdonnes).Succes;
    }

    private void MettreAJourNavigationSuccesEnCours(GameAchievementV2? succesSelectionne)
    {
        if (BoutonSuccesEnCoursPrecedent is null || BoutonSuccesEnCoursSuivant is null)
        {
            return;
        }

        if (
            _identifiantJeuSuccesCourant <= 0
            || _succesJeuCourant.Count <= 1
            || succesSelectionne is null
        )
        {
            BoutonSuccesEnCoursPrecedent.Visibility = Visibility.Collapsed;
            BoutonSuccesEnCoursSuivant.Visibility = Visibility.Collapsed;
            BoutonSuccesEnCoursPrecedent.IsEnabled = false;
            BoutonSuccesEnCoursSuivant.IsEnabled = false;
            TexteChevronSuccesEnCoursPrecedent.Opacity = 1;
            TexteChevronSuccesEnCoursSuivant.Opacity = 1;
            return;
        }

        List<GameAchievementV2> succesOrdonnes =
        [
            .. OrdonnerSuccesPourGrilleSelonMode(_identifiantJeuSuccesCourant, _succesJeuCourant),
        ];
        int indexCourant = succesOrdonnes.FindIndex(item => item.Id == succesSelectionne.Id);

        if (indexCourant < 0)
        {
            BoutonSuccesEnCoursPrecedent.Visibility = Visibility.Collapsed;
            BoutonSuccesEnCoursSuivant.Visibility = Visibility.Collapsed;
            BoutonSuccesEnCoursPrecedent.IsEnabled = false;
            BoutonSuccesEnCoursSuivant.IsEnabled = false;
            TexteChevronSuccesEnCoursPrecedent.Opacity = 1;
            TexteChevronSuccesEnCoursSuivant.Opacity = 1;
            return;
        }

        BoutonSuccesEnCoursPrecedent.Visibility = Visibility.Visible;
        BoutonSuccesEnCoursSuivant.Visibility = Visibility.Visible;
        BoutonSuccesEnCoursPrecedent.IsEnabled = indexCourant > 0;
        BoutonSuccesEnCoursSuivant.IsEnabled = indexCourant < succesOrdonnes.Count - 1;
        TexteChevronSuccesEnCoursPrecedent.Opacity = BoutonSuccesEnCoursPrecedent.IsEnabled
            ? 1
            : 0.38;
        TexteChevronSuccesEnCoursSuivant.Opacity = BoutonSuccesEnCoursSuivant.IsEnabled ? 1 : 0.38;
    }

    /// <summary>
    /// Convertit une image en niveaux de gris pour l'affichage des succès verrouillés.
    /// </summary>
    private static ImageSource ConvertirImageEnNoirEtBlanc(ImageSource image)
    {
        if (image is not BitmapSource bitmapSource)
        {
            return image;
        }

        FormatConvertedBitmap bitmapConverti = new();
        bitmapConverti.BeginInit();
        bitmapConverti.Source = bitmapSource;
        bitmapConverti.DestinationFormat = PixelFormats.Gray32Float;
        bitmapConverti.EndInit();
        bitmapConverti.Freeze();
        return bitmapConverti;
    }
}
