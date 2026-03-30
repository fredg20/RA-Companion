using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using RA.Compagnon.Modeles.Api.V2.Game;
using RA.Compagnon.Modeles.Api.V2.User;
using RA.Compagnon.Modeles.Etat;
using RA.Compagnon.Services;
using SystemControls = System.Windows.Controls;

namespace RA.Compagnon;

public partial class MainWindow
{
    private static readonly string CheminJournalVisuelsJeu = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "RA-Compagnon",
        "journal-visuels-jeu.log"
    );

    /// <summary>
    /// Met ï¿½ jour le visuel du jeu courant ï¿½ partir du box art retournï¿½ par l'API.
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
    /// Rï¿½initialise le visuel du jeu courant sur un ï¿½tat neutre.
    /// </summary>
    private void ReinitialiserImageJeuEnCours()
    {
        _largeurMaxVisuelJeuEnCours = 0;
        _hauteurMaxVisuelJeuEnCours = 0;
        ConteneurImageJeuEnCours.MinWidth = 0;
        ConteneurImageJeuEnCours.MinHeight = 0;
        ColonneImageJeuEnCours.MinWidth = 0;
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
            ImageJeuEnCours.Effect = null;
            AppliquerCoinsArrondisImageJeuEnCours();
            TexteImageJeuEnCours.Visibility = Visibility.Collapsed;

            DoubleAnimation animationFonduEntreeInitiale = new()
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(320),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
            };
            ImageJeuEnCours.BeginAnimation(UIElement.OpacityProperty, animationFonduEntreeInitiale);
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
        ImageJeuEnCours.Effect = null;
        ImageJeuEnCoursTransition.Source = imageJeu;
        ImageJeuEnCoursTransition.Visibility = Visibility.Visible;
        ImageJeuEnCoursTransition.Opacity = 0;
        ImageJeuEnCoursTransition.Effect = null;
        AppliquerCoinsArrondisImageJeuEnCours();
        TexteImageJeuEnCours.Visibility = Visibility.Collapsed;

        DoubleAnimation animationFonduSortie = new()
        {
            From = 1,
            To = 0,
            Duration = TimeSpan.FromMilliseconds(320),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut },
        };

        DoubleAnimation animationFonduEntree = new()
        {
            From = 0,
            To = 1,
            Duration = TimeSpan.FromMilliseconds(320),
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
            ImageJeuEnCoursTransition.Source = null;
            ImageJeuEnCoursTransition.Clip = null;
            ImageJeuEnCoursTransition.Visibility = Visibility.Collapsed;
            ImageJeuEnCoursTransition.Opacity = 1;
            ImageJeuEnCoursTransition.Effect = null;
            AppliquerCoinsArrondisImageJeuEnCours();
        };

        ImageJeuEnCours.BeginAnimation(UIElement.OpacityProperty, animationFonduSortie);
        ImageJeuEnCoursTransition.BeginAnimation(UIElement.OpacityProperty, animationFonduEntree);
    }

    /// <summary>
    /// Rï¿½initialise le carrousel des visuels du jeu courant.
    /// </summary>
    private void ReinitialiserCarrouselVisuelsJeuEnCours()
    {
        _minuteurRotationVisuelsJeuEnCours.Stop();
        _visuelsJeuEnCours.Clear();
        _indexVisuelJeuEnCours = 0;
        TexteVisuelJeuEnCours.Text = string.Empty;
        ZoneSousImageJeuEnCours.Visibility = Visibility.Collapsed;
        BoutonVisuelJeuPrecedent.Visibility = Visibility.Collapsed;
        BoutonVisuelJeuSuivant.Visibility = Visibility.Collapsed;
    }

    /// <summary>
    /// Applique les visuels disponibles du jeu courant au carrousel situï¿½ sous l'image.
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
    /// Met ï¿½ jour le grand visuel et l'ï¿½tat du carrousel sous l'image.
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
        MarquerEtapePipelineChargementJeu(
            EtapePipelineChargementJeu.ImagesChargees,
            _dernierIdentifiantJeuAvecInfos,
            _versionChargementContenuJeu
        );
        TexteVisuelJeuEnCours.Text =
            $"{visuel.Libelle} {_indexVisuelJeuEnCours + 1}/{_visuelsJeuEnCours.Count}";
        ZoneSousImageJeuEnCours.Visibility = Visibility.Collapsed;
        BoutonVisuelJeuPrecedent.Visibility = Visibility.Collapsed;
        BoutonVisuelJeuSuivant.Visibility = Visibility.Collapsed;
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
    /// Fait dï¿½filer automatiquement les autres visuels du jeu avec un fondu doux.
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
    /// Affiche immï¿½diatement les visuels essentiels du jeu courant.
    /// </summary>
    private void AppliquerVisuelsJeuEnCoursInitiaux(GameInfoAndUserProgressV2 jeu)
    {
        List<VisuelJeuEnCours> visuels = [];
        AjouterVisuelJeu(visuels, "Jaquette", jeu.ImageBoxArt);
        DefinirVisuelsJeuEnCours(visuels);
    }

    /// <summary>
    /// Enrichit ensuite les visuels du jeu avec des ï¿½lï¿½ments secondaires comme le badge.
    /// </summary>
    private void DemarrerEnrichissementVisuelsJeuEnCours(GameInfoAndUserProgressV2 jeu)
    {
        _ = EnrichirVisuelsJeuEnCoursAsync(jeu);
    }

    /// <summary>
    /// Charge le badge du jeu sans bloquer l'affichage initial.
    /// </summary>
    private async Task EnrichirVisuelsJeuEnCoursAsync(GameInfoAndUserProgressV2 jeu)
    {
        try
        {
            UserSummaryV2? resume = await ObtenirResumeUtilisateurPourVisuelsAsync();
            string cheminBadge = await ObtenirCheminBadgeJeuAsync(jeu);

            if (_dernierIdentifiantJeuAvecInfos != jeu.Id)
            {
                return;
            }

            List<VisuelJeuEnCours> visuels = [];
            AjouterVisuelJeu(visuels, "Jaquette", jeu.ImageBoxArt);
            AjouterVisuelsSecondairesJeu(visuels, jeu, resume);
            AjouterVisuelJeu(visuels, "Badge", cheminBadge);
            JournaliserVisuelsJeu(jeu, resume, cheminBadge, visuels);
            DefinirVisuelsJeuEnCours(visuels);
        }
        catch
        {
            // Le badge reste un enrichissement facultatif.
        }
    }

    /// <summary>
    /// Ajoute un visuel de jeu s'il est exploitable et non dï¿½jï¿½ prï¿½sent.
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

    private static void AjouterVisuelsSecondairesJeu(
        List<VisuelJeuEnCours> visuels,
        GameInfoAndUserProgressV2 jeu,
        UserSummaryV2? resume
    )
    {
        AjouterVisuelJeu(visuels, "Titre", jeu.CheminImageTitre);
        AjouterVisuelJeu(visuels, "En jeu", jeu.CheminImageEnJeu);

        if (resume?.LastGame?.IdentifiantJeu == jeu.Id)
        {
            AjouterVisuelJeu(visuels, "Titre", resume.LastGame.CheminImageTitre);
            AjouterVisuelJeu(visuels, "En jeu", resume.LastGame.CheminImageEnJeu);
        }

        RecentlyPlayedGameV2? jeuRecent = resume?.RecentlyPlayed.FirstOrDefault(item =>
            item.IdentifiantJeu == jeu.Id
        );

        if (jeuRecent is null)
        {
            return;
        }

        AjouterVisuelJeu(visuels, "Titre", jeuRecent.CheminImageTitre);
        AjouterVisuelJeu(visuels, "En jeu", jeuRecent.CheminImageEnJeu);
    }

    /// <summary>
    /// Rï¿½cupï¿½re le badge du jeu via le catalogue systï¿½me si disponible.
    /// </summary>
    private static void AjouterVisuelJeu(List<VisuelJeuEnCours> visuels, VisuelJeuEnCours? visuel)
    {
        if (visuel is null)
        {
            return;
        }

        AjouterVisuelJeu(visuels, visuel.Libelle, visuel.CheminImage);
    }

    private async Task<VisuelJeuEnCours?> ObtenirVisuelSecondaireJeuAsync(
        GameInfoAndUserProgressV2 jeu
    )
    {
        UserSummaryV2? resume = await ObtenirResumeUtilisateurPourVisuelsAsync();

        if (resume?.LastGame?.IdentifiantJeu == jeu.Id)
        {
            return ConstruireVisuelSecondaire(
                resume.LastGame.CheminImageTitre,
                resume.LastGame.CheminImageEnJeu
            );
        }

        RecentlyPlayedGameV2? jeuRecent = resume?.RecentlyPlayed.FirstOrDefault(item =>
            item.IdentifiantJeu == jeu.Id
        );

        return jeuRecent is null
            ? null
            : ConstruireVisuelSecondaire(jeuRecent.CheminImageTitre, jeuRecent.CheminImageEnJeu);
    }

    private async Task<UserSummaryV2?> ObtenirResumeUtilisateurPourVisuelsAsync()
    {
        if (_dernierResumeUtilisateurCharge is not null)
        {
            return _dernierResumeUtilisateurCharge;
        }

        if (!ConfigurationConnexionEstComplete())
        {
            return null;
        }

        try
        {
            _dernierResumeUtilisateurCharge =
                await ServiceUtilisateurRetroAchievements.ObtenirResumeAsync(
                    _configurationConnexion.Pseudo,
                    _configurationConnexion.CleApiWeb
                );
            return _dernierResumeUtilisateurCharge;
        }
        catch
        {
            return null;
        }
    }

    private static VisuelJeuEnCours? ConstruireVisuelSecondaire(
        string? cheminImageTitre,
        string? cheminImageEnJeu
    )
    {
        if (!string.IsNullOrWhiteSpace(cheminImageTitre))
        {
            return new VisuelJeuEnCours("Titre", cheminImageTitre.Trim());
        }

        if (!string.IsNullOrWhiteSpace(cheminImageEnJeu))
        {
            return new VisuelJeuEnCours("En jeu", cheminImageEnJeu.Trim());
        }

        return null;
    }

    private async Task<string> ObtenirCheminBadgeJeuAsync(GameInfoAndUserProgressV2 jeu)
    {
        if (!ConfigurationConnexionEstComplete() || jeu.Id <= 0 || jeu.ConsoleId <= 0)
        {
            return string.Empty;
        }

        try
        {
            IReadOnlyList<GameListEntryV2> jeuxSysteme =
                await _serviceCatalogueRetroAchievements.ObtenirJeuxSystemeAvecHashesAsync(
                    _configurationConnexion.CleApiWeb,
                    jeu.ConsoleId
                );

            return jeuxSysteme.FirstOrDefault(item => item.Id == jeu.Id)?.ImageIcon ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static void JournaliserVisuelsJeu(
        GameInfoAndUserProgressV2 jeu,
        UserSummaryV2? resume,
        string cheminBadge,
        IReadOnlyList<VisuelJeuEnCours> visuels
    )
    {
        LastGameV2? dernierJeu = resume?.LastGame;
        RecentlyPlayedGameV2? jeuRecent = resume?.RecentlyPlayed.FirstOrDefault(item =>
            item.IdentifiantJeu == jeu.Id
        );

        string details =
            $"jeu={jeu.Id};titre={jeu.Title};"
            + $"box={jeu.ImageBoxArt};"
            + $"lastGameId={dernierJeu?.IdentifiantJeu ?? 0};"
            + $"lastTitle={dernierJeu?.CheminImageTitre ?? string.Empty};"
            + $"lastIngame={dernierJeu?.CheminImageEnJeu ?? string.Empty};"
            + $"recentTitle={jeuRecent?.CheminImageTitre ?? string.Empty};"
            + $"recentIngame={jeuRecent?.CheminImageEnJeu ?? string.Empty};"
            + $"badge={cheminBadge};"
            + $"visuels={string.Join(" | ", visuels.Select(visuel => $"{visuel.Libelle}:{visuel.CheminImage}"))}";

        _ = ServiceModeDiagnostic.JournaliserLigne(
            CheminJournalVisuelsJeu,
            $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {details}{Environment.NewLine}"
        );
    }
}
