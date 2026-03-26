using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using RA.Compagnon.Modeles.Api;
using RA.Compagnon.Services;
using SystemControls = System.Windows.Controls;

namespace RA.Compagnon;

public partial class MainWindow
{
    /// <summary>
    /// Met à jour le visuel du jeu courant à partir du box art retourné par l'API.
    /// </summary>
    private async Task MettreAJourImageJeuEnCoursAsync(string? cheminImage)
    {
        JournaliserDiagnosticChangementJeu("image_debut", cheminImage ?? string.Empty);
        string urlImage = ConstruireUrlImageRetroAchievements(cheminImage);
        _cheminImageJeuEnCoursDemande = urlImage;

        if (string.IsNullOrWhiteSpace(urlImage) || urlImage == "Indisponible")
        {
            ReinitialiserImageJeuEnCours();
            return;
        }

        try
        {
            ImageSource? imageJeu = await ChargerImageDistanteAsync(urlImage);

            if (
                imageJeu is null
                || !string.Equals(
                    _cheminImageJeuEnCoursDemande,
                    urlImage,
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                return;
            }

            AppliquerImageJeuEnCoursAvecFondu(imageJeu, urlImage);
            JournaliserDiagnosticChangementJeu("image_appliquee");
        }
        catch
        {
            ReinitialiserImageJeuEnCours();
        }
    }

    /// <summary>
    /// Réinitialise le visuel du jeu courant sur un état neutre.
    /// </summary>
    private void ReinitialiserImageJeuEnCours()
    {
        ImageJeuEnCours.BeginAnimation(UIElement.OpacityProperty, null);
        if (ImageJeuEnCours.Effect is BlurEffect effetImageJeu)
        {
            effetImageJeu.BeginAnimation(BlurEffect.RadiusProperty, null);
        }
        ImageJeuEnCoursTransition.BeginAnimation(UIElement.OpacityProperty, null);
        if (ImageJeuEnCoursTransition.Effect is BlurEffect effetTransition)
        {
            effetTransition.BeginAnimation(BlurEffect.RadiusProperty, null);
        }
        ImageJeuEnCours.Opacity = 1;
        ImageJeuEnCours.Effect = null;
        ImageJeuEnCours.Source = null;
        ImageJeuEnCours.Clip = null;
        ImageJeuEnCours.Visibility = Visibility.Collapsed;
        ImageJeuEnCoursTransition.Opacity = 1;
        ImageJeuEnCoursTransition.Effect = null;
        ImageJeuEnCoursTransition.Source = null;
        ImageJeuEnCoursTransition.Clip = null;
        ImageJeuEnCoursTransition.Visibility = Visibility.Collapsed;
        _cheminImageJeuEnCoursAffiche = string.Empty;
        TexteImageJeuEnCours.Text = string.Empty;
        TexteImageJeuEnCours.Visibility = Visibility.Collapsed;
    }

    /// <summary>
    /// Applique la jaquette avec une transition douce par flou entre deux visuels.
    /// </summary>
    private void AppliquerImageJeuEnCoursAvecFondu(ImageSource imageJeu, string urlImage)
    {
        if (ImageJeuEnCours.Source is null || ImageJeuEnCours.Visibility != Visibility.Visible)
        {
            ImageJeuEnCours.Source = imageJeu;
            ImageJeuEnCours.Visibility = Visibility.Visible;
            ImageJeuEnCours.Opacity = 0;
            BlurEffect effetEntreeInitiale = new() { Radius = RayonFlouTransitionImageJeuEnCours };
            ImageJeuEnCours.Effect = effetEntreeInitiale;
            AppliquerCoinsArrondisImageJeuEnCours();
            TexteImageJeuEnCours.Visibility = Visibility.Collapsed;

            DoubleAnimation animationFonduEntreeInitiale = new()
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(DureeFonduImageJeuEnCoursMillisecondes),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
            };

            DoubleAnimation animationFlouEntreeInitiale = new()
            {
                From = RayonFlouTransitionImageJeuEnCours,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(DureeFonduImageJeuEnCoursMillisecondes),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
            };

            animationFlouEntreeInitiale.Completed += (_, _) => ImageJeuEnCours.Effect = null;
            ImageJeuEnCours.BeginAnimation(UIElement.OpacityProperty, animationFonduEntreeInitiale);
            effetEntreeInitiale.BeginAnimation(
                BlurEffect.RadiusProperty,
                animationFlouEntreeInitiale
            );
            _cheminImageJeuEnCoursAffiche = urlImage;
            return;
        }

        if (
            string.Equals(
                _cheminImageJeuEnCoursAffiche,
                urlImage,
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            return;
        }

        ImageJeuEnCours.BeginAnimation(UIElement.OpacityProperty, null);
        if (ImageJeuEnCours.Effect is BlurEffect effetImageJeuActuelle)
        {
            effetImageJeuActuelle.BeginAnimation(BlurEffect.RadiusProperty, null);
        }
        ImageJeuEnCoursTransition.BeginAnimation(UIElement.OpacityProperty, null);
        if (ImageJeuEnCoursTransition.Effect is BlurEffect effetImageTransition)
        {
            effetImageTransition.BeginAnimation(BlurEffect.RadiusProperty, null);
        }

        ImageJeuEnCours.Visibility = Visibility.Visible;
        BlurEffect effetSortie = new() { Radius = 0 };
        ImageJeuEnCours.Effect = effetSortie;
        ImageJeuEnCoursTransition.Source = imageJeu;
        ImageJeuEnCoursTransition.Visibility = Visibility.Visible;
        ImageJeuEnCoursTransition.Opacity = 0;
        BlurEffect effetEntree = new() { Radius = RayonFlouTransitionImageJeuEnCours };
        ImageJeuEnCoursTransition.Effect = effetEntree;
        AppliquerCoinsArrondisImageJeuEnCours();
        TexteImageJeuEnCours.Visibility = Visibility.Collapsed;

        DoubleAnimation animationFonduSortie = new()
        {
            From = 1,
            To = 0,
            Duration = TimeSpan.FromMilliseconds(DureeFonduImageJeuEnCoursMillisecondes),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut },
        };

        DoubleAnimation animationFlouSortie = new()
        {
            From = 0,
            To = RayonFlouTransitionImageJeuEnCours,
            Duration = TimeSpan.FromMilliseconds(DureeFonduImageJeuEnCoursMillisecondes),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut },
        };

        DoubleAnimation animationFonduEntree = new()
        {
            From = 0,
            To = 1,
            Duration = TimeSpan.FromMilliseconds(DureeFonduImageJeuEnCoursMillisecondes),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut },
        };

        DoubleAnimation animationFlouEntree = new()
        {
            From = RayonFlouTransitionImageJeuEnCours,
            To = 0,
            Duration = TimeSpan.FromMilliseconds(DureeFonduImageJeuEnCoursMillisecondes),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut },
        };

        animationFonduEntree.Completed += (_, _) =>
        {
            if (
                !string.Equals(
                    _cheminImageJeuEnCoursDemande,
                    urlImage,
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                return;
            }

            ImageJeuEnCours.BeginAnimation(UIElement.OpacityProperty, null);
            ((TranslateTransform)ImageJeuEnCours.RenderTransform).BeginAnimation(
                TranslateTransform.XProperty,
                null
            );
            ImageJeuEnCours.Source = imageJeu;
            ImageJeuEnCours.Visibility = Visibility.Visible;
            ImageJeuEnCours.Opacity = 1;
            ImageJeuEnCours.Effect = null;
            _cheminImageJeuEnCoursAffiche = urlImage;

            ImageJeuEnCoursTransition.BeginAnimation(UIElement.OpacityProperty, null);
            effetEntree.BeginAnimation(BlurEffect.RadiusProperty, null);
            ImageJeuEnCoursTransition.Source = null;
            ImageJeuEnCoursTransition.Clip = null;
            ImageJeuEnCoursTransition.Visibility = Visibility.Collapsed;
            ImageJeuEnCoursTransition.Opacity = 1;
            ImageJeuEnCoursTransition.Effect = null;
            AppliquerCoinsArrondisImageJeuEnCours();
        };

        ImageJeuEnCours.BeginAnimation(UIElement.OpacityProperty, animationFonduSortie);
        effetSortie.BeginAnimation(BlurEffect.RadiusProperty, animationFlouSortie);
        ImageJeuEnCoursTransition.BeginAnimation(UIElement.OpacityProperty, animationFonduEntree);
        effetEntree.BeginAnimation(BlurEffect.RadiusProperty, animationFlouEntree);
    }

    /// <summary>
    /// Réinitialise le carrousel des visuels du jeu courant.
    /// </summary>
    private void ReinitialiserCarrouselVisuelsJeuEnCours()
    {
        _minuteurRotationVisuelsJeuEnCours.Stop();
        _visuelsJeuEnCours.Clear();
        _indexVisuelJeuEnCours = 0;
        TexteVisuelJeuEnCours.Text = string.Empty;
        CarrouselVisuelsJeuEnCours.Visibility = Visibility.Collapsed;
        BoutonVisuelJeuPrecedent.Visibility = Visibility.Collapsed;
        BoutonVisuelJeuSuivant.Visibility = Visibility.Collapsed;
    }

    /// <summary>
    /// Applique les visuels disponibles du jeu courant au carrousel situé sous l'image.
    /// </summary>
    private void DefinirVisuelsJeuEnCours(IReadOnlyList<VisuelJeuEnCours> visuels)
    {
        string? cheminActuel =
            _visuelsJeuEnCours.Count > 0 && _indexVisuelJeuEnCours < _visuelsJeuEnCours.Count
                ? _visuelsJeuEnCours[_indexVisuelJeuEnCours].CheminImage
                : null;

        _visuelsJeuEnCours.Clear();
        _visuelsJeuEnCours.AddRange(visuels);

        if (_visuelsJeuEnCours.Count == 0)
        {
            ReinitialiserCarrouselVisuelsJeuEnCours();
            ReinitialiserImageJeuEnCours();
            return;
        }

        int indexConserve = !string.IsNullOrWhiteSpace(cheminActuel)
            ? _visuelsJeuEnCours.FindIndex(visuel =>
                visuel.CheminImage.Equals(cheminActuel, StringComparison.OrdinalIgnoreCase)
            )
            : -1;

        _indexVisuelJeuEnCours = indexConserve >= 0 ? indexConserve : 0;
        MettreAJourRotationVisuelsJeuEnCours();
        _ = MettreAJourAffichageVisuelJeuEnCoursAsync();
    }

    /// <summary>
    /// Met à jour le grand visuel et l'état du carrousel sous l'image.
    /// </summary>
    private async Task MettreAJourAffichageVisuelJeuEnCoursAsync()
    {
        if (_visuelsJeuEnCours.Count == 0)
        {
            ReinitialiserCarrouselVisuelsJeuEnCours();
            ReinitialiserImageJeuEnCours();
            return;
        }

        _indexVisuelJeuEnCours =
            (_indexVisuelJeuEnCours + _visuelsJeuEnCours.Count) % _visuelsJeuEnCours.Count;

        VisuelJeuEnCours visuel = _visuelsJeuEnCours[_indexVisuelJeuEnCours];
        await MettreAJourImageJeuEnCoursAsync(visuel.CheminImage);
        TexteVisuelJeuEnCours.Text =
            $"{visuel.Libelle} {_indexVisuelJeuEnCours + 1}/{_visuelsJeuEnCours.Count}";
        CarrouselVisuelsJeuEnCours.Visibility =
            _visuelsJeuEnCours.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
        BoutonVisuelJeuPrecedent.Visibility =
            _visuelsJeuEnCours.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
        BoutonVisuelJeuSuivant.Visibility =
            _visuelsJeuEnCours.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>
    /// Active ou coupe la rotation automatique des visuels selon le nombre d'images disponibles.
    /// </summary>
    private void MettreAJourRotationVisuelsJeuEnCours()
    {
        if (_visuelsJeuEnCours.Count > 1)
        {
            _minuteurRotationVisuelsJeuEnCours.Stop();
            _minuteurRotationVisuelsJeuEnCours.Start();
            return;
        }

        _minuteurRotationVisuelsJeuEnCours.Stop();
    }

    /// <summary>
    /// Fait défiler automatiquement les autres visuels du jeu avec un fondu doux.
    /// </summary>
    private async void MinuteurRotationVisuelsJeuEnCours_Tick(object? sender, EventArgs e)
    {
        if (_visuelsJeuEnCours.Count <= 1)
        {
            _minuteurRotationVisuelsJeuEnCours.Stop();
            return;
        }

        _indexVisuelJeuEnCours++;
        await MettreAJourAffichageVisuelJeuEnCoursAsync();
    }

    /// <summary>
    /// Affiche immédiatement les visuels essentiels du jeu courant.
    /// </summary>
    private void AppliquerVisuelsJeuEnCoursInitiaux(JeuUtilisateurRetroAchievements jeu)
    {
        List<VisuelJeuEnCours> visuels = [];
        AjouterVisuelJeu(visuels, "Jaquette", jeu.CheminImageBoite);
        DefinirVisuelsJeuEnCours(visuels);
    }

    /// <summary>
    /// Enrichit ensuite les visuels du jeu avec des éléments secondaires comme le badge.
    /// </summary>
    private void DemarrerEnrichissementVisuelsJeuEnCours(JeuUtilisateurRetroAchievements jeu)
    {
        _ = EnrichirVisuelsJeuEnCoursAsync(jeu);
    }

    /// <summary>
    /// Charge le badge du jeu sans bloquer l'affichage initial.
    /// </summary>
    private async Task EnrichirVisuelsJeuEnCoursAsync(JeuUtilisateurRetroAchievements jeu)
    {
        try
        {
            string cheminBadge = await ObtenirCheminBadgeJeuAsync(jeu);

            if (_dernierIdentifiantJeuAvecInfos != jeu.IdentifiantJeu)
            {
                return;
            }

            List<VisuelJeuEnCours> visuels = [];
            AjouterVisuelJeu(visuels, "Jaquette", jeu.CheminImageBoite);
            AjouterVisuelJeu(visuels, "Badge", cheminBadge);
            DefinirVisuelsJeuEnCours(visuels);
        }
        catch
        {
            // Le badge reste un enrichissement facultatif.
        }
    }

    /// <summary>
    /// Ajoute un visuel de jeu s'il est exploitable et non déjà présent.
    /// </summary>
    private static void AjouterVisuelJeu(
        List<VisuelJeuEnCours> visuels,
        string libelle,
        string? cheminImage
    )
    {
        if (string.IsNullOrWhiteSpace(cheminImage))
        {
            return;
        }

        if (
            visuels.Any(visuel =>
                visuel.CheminImage.Equals(cheminImage, StringComparison.OrdinalIgnoreCase)
            )
        )
        {
            return;
        }

        visuels.Add(new VisuelJeuEnCours(libelle, cheminImage.Trim()));
    }

    /// <summary>
    /// Récupère le badge du jeu via le catalogue système si disponible.
    /// </summary>
    private async Task<string> ObtenirCheminBadgeJeuAsync(JeuUtilisateurRetroAchievements jeu)
    {
        if (
            !ConfigurationConnexionEstComplete()
            || jeu.IdentifiantJeu <= 0
            || jeu.IdentifiantConsole <= 0
        )
        {
            return string.Empty;
        }

        try
        {
            IReadOnlyList<JeuSystemeRetroAchievements> jeuxSysteme =
                await ClientRetroAchievements.ObtenirJeuxSystemeAvecHashesAsync(
                    _configurationConnexion.CleApiWeb,
                    jeu.IdentifiantConsole
                );

            return jeuxSysteme
                    .FirstOrDefault(item => item.IdentifiantJeu == jeu.IdentifiantJeu)
                    ?.CheminImageIcone
                ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }
}
