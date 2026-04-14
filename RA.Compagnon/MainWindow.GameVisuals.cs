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

/*
 * Regroupe la gestion des visuels du jeu courant, de leur carrousel et des
 * journaux de diagnostic associés aux changements d'image.
 */
namespace RA.Compagnon;

/*
 * Porte la logique de chargement, d'animation et d'enrichissement des
 * visuels affichés pour le jeu courant.
 */
public partial class MainWindow
{
    private static readonly string CheminJournalVisuelsJeu = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "RA-Compagnon",
        "journal-visuels-jeu.log"
    );

    /*
     * Télécharge puis applique l'image principale du jeu courant à partir
     * d'un chemin relatif fourni par l'API.
     */
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

    /*
     * Réinitialise complètement l'image principale, l'image de transition
     * et les contraintes de taille du conteneur visuel.
     */
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
        _vueModele.JeuCourant.TexteVisuelPrincipal = string.Empty;
        _vueModele.JeuCourant.TexteVisuelPrincipalVisible = false;
    }

    /*
     * Applique un nouveau visuel du jeu en utilisant un fondu pour éviter
     * un changement abrupt entre deux images.
     */
    private void AppliquerImageJeuEnCoursAvecFondu(ImageSource imageJeu, string urlImage)
    {
        if (ImageJeuEnCours.Source is null || ImageJeuEnCours.Visibility != Visibility.Visible)
        {
            ImageJeuEnCours.Source = imageJeu;
            ImageJeuEnCours.Visibility = Visibility.Visible;
            ImageJeuEnCours.Opacity = 0;
            ImageJeuEnCours.Effect = null;
            AppliquerCoinsArrondisImageJeuEnCours();
            _vueModele.JeuCourant.TexteVisuelPrincipalVisible = false;

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
        _vueModele.JeuCourant.TexteVisuelPrincipalVisible = false;

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

    /*
     * Réinitialise le carrousel des visuels secondaires et l'état de
     * navigation associé dans le ViewModel.
     */
    private void ReinitialiserCarrouselVisuelsJeuEnCours()
    {
        _minuteurRotationVisuelsJeuEnCours.Stop();
        _visuelsJeuEnCours.Clear();
        _indexVisuelJeuEnCours = 0;
        _vueModele.JeuCourant.LibelleVisuelCourant = string.Empty;
        _vueModele.JeuCourant.VisuelsSecondairesVisible = false;
        _vueModele.JeuCourant.ActionVisuelPrecedentActivee = false;
        _vueModele.JeuCourant.ActionVisuelSuivantActivee = false;
    }

    /*
     * Remplace la liste des visuels disponibles en conservant si possible
     * le visuel actuellement affiché.
     */
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

    /*
     * Synchronise l'image affichée, l'étape du pipeline et les commandes
     * de navigation avec le visuel courant du carrousel.
     */
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
        _vueModele.JeuCourant.LibelleVisuelCourant =
            $"{visuel.Libelle} {_indexVisuelJeuEnCours + 1}/{_visuelsJeuEnCours.Count}";
        bool navigationDisponible = _visuelsJeuEnCours.Count > 1;
        _vueModele.JeuCourant.VisuelsSecondairesVisible = navigationDisponible;
        _vueModele.JeuCourant.ActionVisuelPrecedentActivee = navigationDisponible;
        _vueModele.JeuCourant.ActionVisuelSuivantActivee = navigationDisponible;
    }

    /*
     * Active ou coupe la rotation automatique selon le nombre de visuels
     * disponibles pour le jeu courant.
     */
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

    /*
     * Fait avancer automatiquement le carrousel à chaque impulsion du
     * minuteur de rotation.
     */
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

    /*
     * Applique la première version minimale des visuels dès que le jeu est
     * connu, avant l'enrichissement complet.
     */
    private void AppliquerVisuelsJeuEnCoursInitiaux(GameInfoAndUserProgressV2 jeu)
    {
        List<VisuelJeuEnCours> visuels = [];
        AjouterVisuelJeu(visuels, "Jaquette", jeu.ImageBoxArt);
        DefinirVisuelsJeuEnCours(visuels);
    }

    /*
     * Lance en arrière-plan l'enrichissement complet du carrousel de visuels.
     */
    private void DemarrerEnrichissementVisuelsJeuEnCours(GameInfoAndUserProgressV2 jeu)
    {
        _ = EnrichirVisuelsJeuEnCoursAsync(jeu);
    }

    /*
     * Complète la liste des visuels avec les variantes disponibles dans le
     * résumé utilisateur et le badge du jeu.
     */
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
        catch { }
    }

    /*
     * Ajoute un visuel à la collection cible lorsqu'il possède un chemin
     * valide et qu'il n'est pas déjà présent.
     */
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

    /*
     * Ajoute les visuels secondaires connus pour le jeu à partir des données
     * détaillées et du résumé utilisateur.
     */
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

    /*
     * Ajoute un visuel déjà construit lorsqu'il est effectivement présent.
     */
    private static void AjouterVisuelJeu(List<VisuelJeuEnCours> visuels, VisuelJeuEnCours? visuel)
    {
        if (visuel is null)
        {
            return;
        }

        AjouterVisuelJeu(visuels, visuel.Libelle, visuel.CheminImage);
    }

    /*
     * Retourne le résumé utilisateur nécessaire à l'enrichissement des
     * visuels, en profitant du cache local si possible.
     */
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

    /*
     * Construit un visuel secondaire à partir d'une image de titre ou d'une
     * image en jeu quand l'une des deux existe.
     */
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

    /*
     * Recherche le chemin du badge du jeu dans le catalogue de son système.
     */
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

    /*
     * Journalise le détail des visuels retenus pour faciliter les diagnostics
     * lors des changements de jeu.
     */
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
