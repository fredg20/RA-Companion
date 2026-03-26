using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RA.Compagnon.Modeles.Api;
using RA.Compagnon.Modeles.Local;

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
    /// Met à jour l'affichage des succès du jeu courant.
    /// </summary>
    private async Task MettreAJourSuccesJeuAsync(JeuUtilisateurRetroAchievements jeu)
    {
        List<SuccesJeuUtilisateurRetroAchievements> succes =
        [
            .. jeu
                .Succes.Values.OrderBy(item => item.OrdreAffichage)
                .ThenBy(item => item.IdentifiantSucces),
        ];

        _identifiantJeuSuccesCourant = jeu.IdentifiantJeu;
        _succesJeuCourant = succes;
        await MettreAJourPremierSuccesNonDebloqueAsync(jeu.IdentifiantJeu, succes);
        DemarrerMiseAJourGrilleTousSuccesEnArrierePlan(jeu.IdentifiantJeu, succes);
    }

    /// <summary>
    /// Charge la grille complète des succès sans bloquer l'affichage du succès principal.
    /// </summary>
    private void DemarrerMiseAJourGrilleTousSuccesEnArrierePlan(
        int identifiantJeu,
        List<SuccesJeuUtilisateurRetroAchievements> succes
    )
    {
        _ = MettreAJourGrilleTousSuccesEnArrierePlanAsync(identifiantJeu, succes);
    }

    /// <summary>
    /// Exécute le remplissage complet de la grille en arrière-plan et ignore les erreurs non critiques.
    /// </summary>
    private async Task MettreAJourGrilleTousSuccesEnArrierePlanAsync(
        int identifiantJeu,
        List<SuccesJeuUtilisateurRetroAchievements> succes
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
        SuccesJeuUtilisateurRetroAchievements? Succes,
        bool DoitSauvegarder,
        bool EstEpingleManuellement
    ) SelectionnerSuccesEnCours(List<SuccesJeuUtilisateurRetroAchievements> succes)
    {
        if (_identifiantJeuSuccesCourant > 0)
        {
            if (_identifiantSuccesGrilleTemporaire.HasValue)
            {
                SuccesJeuUtilisateurRetroAchievements? succesTemporaire = succes.FirstOrDefault(
                    item => item.IdentifiantSucces == _identifiantSuccesGrilleTemporaire.Value
                );

                if (succesTemporaire is not null)
                {
                    return (
                        succesTemporaire,
                        false,
                        false
                    );
                }
            }

            if (_identifiantSuccesGrilleEpingle.HasValue)
            {
                SuccesJeuUtilisateurRetroAchievements? succesEpingle = succes.FirstOrDefault(item =>
                    item.IdentifiantSucces == _identifiantSuccesGrilleEpingle.Value
                );

                if (succesEpingle is not null)
                {
                    return (
                        succesEpingle,
                        true,
                        true
                    );
                }
            }
        }

        List<SuccesJeuUtilisateurRetroAchievements> succesNonDebloques =
        [
            .. OrdonnerSuccesPourGrilleSelonMode(_identifiantJeuSuccesCourant, succes)
                .Where(item => !SuccesEstDebloque(item)),
        ];
        SuccesJeuUtilisateurRetroAchievements? premierSuccesNonDebloque =
            succesNonDebloques.FirstOrDefault();

        return (
            premierSuccesNonDebloque,
            true,
            false
        );
    }

    /// <summary>
    /// Met à jour la carte du premier succès restant à débloquer.
    /// </summary>
    private async Task MettreAJourPremierSuccesNonDebloqueAsync(
        int identifiantJeu,
        List<SuccesJeuUtilisateurRetroAchievements> succes
    )
    {
        (
            SuccesJeuUtilisateurRetroAchievements? premierSucces,
            bool doitSauvegarder,
            bool estEpingleManuellement
        ) = SelectionnerSuccesEnCours(succes);

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
        SuccesJeuUtilisateurRetroAchievements? succesSelectionne,
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
            TexteTitrePremierSuccesNonDebloque.Text = "Jeu complété";
            TexteTitrePremierSuccesNonDebloque.Visibility = Visibility.Visible;
            TexteDescriptionPremierSuccesNonDebloque.Text =
                "Aucun succès restant à afficher pour ce jeu.";
            TexteDescriptionPremierSuccesNonDebloque.Visibility = Visibility.Visible;
            TextePointsPremierSuccesNonDebloque.Text = string.Empty;
            TextePointsPremierSuccesNonDebloque.Visibility = Visibility.Collapsed;
            SauvegarderDernierSuccesAffiche(
                new EtatSuccesAfficheLocal
                {
                    IdentifiantJeu = identifiantJeu,
                    IdentifiantSucces = 0,
                    Titre = TexteTitrePremierSuccesNonDebloque.Text,
                    Description = TexteDescriptionPremierSuccesNonDebloque.Text,
                    DetailsPoints = string.Empty,
                    TexteVisuel = TexteImagePremierSuccesNonDebloque.Text,
                }
            );
            return;
        }

        bool succesDebloque = SuccesEstDebloque(succesSelectionne);
        string urlBadge = ConstruireUrlBadgeDepuisNom(succesSelectionne.NomBadge, !succesDebloque);
        ImageSource? imageSucces = await ChargerImageDistanteAsync(urlBadge);

        if (imageSucces is not null)
        {
            ImagePremierSuccesNonDebloque.Source = succesDebloque
                ? imageSucces
                : ConvertirImageEnNoirEtBlanc(imageSucces);
            ImagePremierSuccesNonDebloque.Opacity = succesDebloque ? 1 : 0.58;
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

        TexteTitrePremierSuccesNonDebloque.Text = succesSelectionne.Titre;
        TexteTitrePremierSuccesNonDebloque.Visibility = Visibility.Visible;
        string descriptionSucces = string.IsNullOrWhiteSpace(succesSelectionne.Description)
            ? "Aucune description disponible."
            : await _serviceTraductionTexte.TraduireVersFrancaisAsync(
                succesSelectionne.Description
            );
        TexteDescriptionPremierSuccesNonDebloque.Text = descriptionSucces.Trim();
        TexteDescriptionPremierSuccesNonDebloque.Visibility = Visibility.Visible;
        string detailsPoints = ConstruireDetailsPointsSucces(succesSelectionne);
        TextePointsPremierSuccesNonDebloque.Text = detailsPoints;
        TextePointsPremierSuccesNonDebloque.Visibility = string.IsNullOrWhiteSpace(detailsPoints)
            ? Visibility.Collapsed
            : Visibility.Visible;
        if (doitSauvegarder)
        {
            SauvegarderDernierSuccesAffiche(
                new EtatSuccesAfficheLocal
                {
                    IdentifiantJeu = identifiantJeu,
                    IdentifiantSucces = succesSelectionne.IdentifiantSucces,
                    Titre = TexteTitrePremierSuccesNonDebloque.Text,
                    Description = TexteDescriptionPremierSuccesNonDebloque.Text,
                    DetailsPoints = TextePointsPremierSuccesNonDebloque.Text,
                    EstEpingleManuellement = estEpingleManuellement,
                    CheminImageBadge = urlBadge,
                    TexteVisuel = TexteImagePremierSuccesNonDebloque.Text,
                }
            );
        }
    }

    /// <summary>
    /// Construit l'URL publique d'un badge de succès.
    /// </summary>
    private static string ConstruireUrlBadgeDepuisNom(string nomBadge, bool versionVerrouillee)
    {
        if (string.IsNullOrWhiteSpace(nomBadge))
        {
            return string.Empty;
        }

        string badgeNettoye = nomBadge.Trim();

        if (badgeNettoye.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            return badgeNettoye;
        }

        if (badgeNettoye.StartsWith('/'))
        {
            return ConstruireUrlImageRetroAchievements(badgeNettoye);
        }

        string suffixe = versionVerrouillee ? "_lock" : string.Empty;
        return $"https://i.retroachievements.org/Badge/{badgeNettoye}{suffixe}.png";
    }

    /// <summary>
    /// Traduit le type technique d'un succès en libellé français.
    /// </summary>
    private static string TraduireTypeSucces(string type)
    {
        return (type ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "progression" => "Succès de progression",
            "win_condition" => "Succès de victoire",
            "missable" => "Succès manquable",
            _ => string.Empty,
        };
    }

    /// <summary>
    /// Construit la ligne de points affichée pour un succès.
    /// </summary>
    private static string ConstruireDetailsPointsSucces(
        SuccesJeuUtilisateurRetroAchievements succes
    )
    {
        List<string> segments = [];
        string typeSucces = TraduireTypeSucces(succes.Type);

        if (!string.IsNullOrWhiteSpace(typeSucces))
        {
            segments.Add(typeSucces);
        }

        if (succes.Points > 0)
        {
            segments.Add($"{succes.Points.ToString(CultureInfo.CurrentCulture)} points");
        }

        if (succes.Retropoints > 0)
        {
            segments.Add($"{succes.Retropoints.ToString(CultureInfo.CurrentCulture)} rétropoints");
        }

        return string.Join(" • ", segments);
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
