using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using RA.Compagnon.Modeles.Api;
using RA.Compagnon.Modeles.Local;
using RA.Compagnon.Services;
using SystemControls = System.Windows.Controls;
using UiControls = Wpf.Ui.Controls;

namespace RA.Compagnon;

/// <summary>
/// Fenêtre principale du compagnon RetroAchievements.
/// </summary>
public partial class MainWindow : UiControls.FluentWindow
{
    private sealed record VisuelJeuEnCours(string Libelle, string CheminImage);

    private static readonly TimeSpan IntervalleActualisationApi = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan IntervalleSondeLocale = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan IntervalleMasquageBarreDefilement = TimeSpan.FromSeconds(1.2);
    private static readonly TimeSpan IntervalleRepriseAnimationGrilleSucces = TimeSpan.FromSeconds(
        1.3
    );
    private static readonly HttpClient HttpClientImages = new();
    private const double LargeurMinimaleDispositionDouble = 920;
    private const double LargeurContenuModaleConnexion = 360;
    private const double MargeInterieureModaleConnexion = 16;
    private const double LargeurZoneDetectionBarreDefilement = 18;
    private const double TaillePoliceTitreJeuNormale = 26;
    private const double VitesseDefilementTitreJeuPixelsParSeconde = 18;
    private const double SeuilDeclenchementDefilementTitreJeu = 4;
    private const double TailleBadgeGrilleSucces = 34;
    private const double EspaceMinimalGrilleSucces = 6;
    private const double HauteurMinimaleGrilleSucces = 0;
    private const double VitesseDefilementGrilleSuccesPixelsParSeconde = 22;
    private const double SeuilDeclenchementDefilementGrilleSucces = 4;

    private readonly ServiceConfigurationLocale _serviceConfigurationLocale = new();
    private readonly ClientRetroAchievements _clientRetroAchievements = new();
    private readonly SondeJeuLocal _sondeJeuLocal = new();
    private readonly ServiceHachageJeuLocal _serviceHachageJeuLocal = new();
    private readonly ServiceTraductionTexte _serviceTraductionTexte = new();
    private readonly DispatcherTimer _minuteurActualisationApi = new();
    private readonly DispatcherTimer _minuteurSondeLocale = new();
    private readonly DispatcherTimer _minuteurMasquageBarreDefilement = new();
    private readonly DispatcherTimer _minuteurRepriseAnimationGrilleSucces = new();
    private readonly Dictionary<string, ImageSource> _cacheImagesDistantes = new(
        StringComparer.OrdinalIgnoreCase
    );
    private readonly List<VisuelJeuEnCours> _visuelsJeuEnCours = [];
    private SystemControls.Primitives.ScrollBar? _barreDefilementVerticalePrincipale;
    private bool _carteConnexionRepliee;
    private bool _carteJeuEnCoursRepliee;
    private bool _connexionInitialeAffichee;
    private bool _chargementJeuEnCoursActif;
    private bool _actualisationApiCibleeEnAttente;
    private bool _profilUtilisateurAccessible = true;
    private bool _dernierJeuAfficheModifie;
    private bool _miseAJourAnimationTitreJeuPlanifiee;
    private bool _miseAJourAnimationGrilleSuccesPlanifiee;
    private bool _animationGrilleSuccesVersBas = true;
    private bool _survolBadgeGrilleSuccesActif;
    private int _dernierIdentifiantJeuApi;
    private int _dernierIdentifiantJeuAvecInfos;
    private int _dernierIdentifiantJeuAvecProgression;
    private ProfilUtilisateurRetroAchievements? _dernierProfilUtilisateurCharge;
    private ResumeUtilisateurRetroAchievements? _dernierResumeUtilisateurCharge;
    private string _dernierTitreJeuApi = string.Empty;
    private string _dernierePresenceRiche = string.Empty;
    private string _dernierPseudoCharge = string.Empty;
    private string _signatureDernierJeuLocal = string.Empty;
    private string _signatureAnimationTitreJeu = string.Empty;
    private string _signatureAnimationGrilleSucces = string.Empty;
    private string _etatConnexionCourant = "Non configuré";
    private ConfigurationConnexion _configurationConnexion = new();
    private int _indexVisuelJeuEnCours;
    private double _amplitudeAnimationGrilleSucces;
    private AnimationClock? _horlogeAnimationGrilleSucces;

    /// <summary>
    /// Initialise la fenêtre principale.
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();
        AppliquerEtatSectionsRepliables();
        AppliquerIconeApplication();
        ReinitialiserJeuEnCours();
        ConfigurerActualisationAutomatique();
        Loaded += FenetrePrincipaleChargee;
        Closing += FenetrePrincipale_Fermeture;
    }

    /// <summary>
    /// Charge l'icône applicative depuis le fichier ICO embarqué et l'applique à la fenêtre.
    /// </summary>
    private void AppliquerIconeApplication()
    {
        Uri uriIcone = new("pack://application:,,,/rac.ico", UriKind.Absolute);
        IconBitmapDecoder decodeur = new(
            uriIcone,
            BitmapCreateOptions.PreservePixelFormat,
            BitmapCacheOption.OnLoad
        );

        if (decodeur.Frames.Count == 0)
        {
            return;
        }

        ImageSource imageIcone = decodeur.Frames[0];
        Icon = imageIcone;
        ImageIconeTitre.Source = imageIcone;
    }

    /// <summary>
    /// Prépare les minuteurs qui pilotent la sonde locale rapide et les appels API.
    /// </summary>
    private void ConfigurerActualisationAutomatique()
    {
        _minuteurActualisationApi.Interval = IntervalleActualisationApi;
        _minuteurActualisationApi.Tick += ActualisationApi_Tick;

        _minuteurSondeLocale.Interval = IntervalleSondeLocale;
        _minuteurSondeLocale.Tick += SondeLocale_Tick;

        _minuteurMasquageBarreDefilement.Interval = IntervalleMasquageBarreDefilement;
        _minuteurMasquageBarreDefilement.Tick += MinuteurMasquageBarreDefilement_Tick;

        _minuteurRepriseAnimationGrilleSucces.Interval = IntervalleRepriseAnimationGrilleSucces;
        _minuteurRepriseAnimationGrilleSucces.Tick += MinuteurRepriseAnimationGrilleSucces_Tick;
    }

    /// <summary>
    /// Charge la configuration locale puis affiche la modale de connexion au premier lancement.
    /// </summary>
    private async void FenetrePrincipaleChargee(object sender, RoutedEventArgs e)
    {
        if (_connexionInitialeAffichee)
        {
            return;
        }

        _connexionInitialeAffichee = true;
        _configurationConnexion = await _serviceConfigurationLocale.ChargerAsync();
        AppliquerGeometrieFenetre();
        MettreAJourResumeConnexion();
        AjusterDisposition();
        _ = Dispatcher.BeginInvoke(
            DefinirVisibiliteBarreDefilementPrincipale,
            DispatcherPriority.Loaded
        );
        await AppliquerDernierJeuSauvegardeAsync();

        if (ConfigurationConnexionEstComplete())
        {
            DefinirVisibiliteContenuPrincipal(true);
            await AmorcerEtatJeuLocalAuDemarrageAsync();
            await ChargerJeuEnCoursAsync();
            DemarrerActualisationAutomatique();
            return;
        }

        await AfficherModaleConnexionAsync();
    }

    /// <summary>
    /// Sauvegarde la géométrie de la fenêtre au moment de la fermeture.
    /// </summary>
    private async void FenetrePrincipale_Fermeture(object? sender, CancelEventArgs e)
    {
        ArreterActualisationAutomatique();
        MemoriserGeometrieFenetre();
        await _serviceConfigurationLocale.SauvegarderAsync(_configurationConnexion);
    }

    /// <summary>
    /// Réorganise l'interface quand la fenêtre devient trop étroite.
    /// </summary>
    private void FenetrePrincipale_TailleChangee(object sender, SizeChangedEventArgs e)
    {
        AjusterDisposition();
        AjusterHauteurCarteJeuEnCours();
        PlanifierMiseAJourAnimationGrilleTousSucces();
    }

    /// <summary>
    /// Réagit au changement de taille de la zone de contenu visible.
    /// </summary>
    private void ZonePrincipale_TailleChangee(object sender, SizeChangedEventArgs e)
    {
        AjusterHauteurCarteJeuEnCours();
    }

    /// <summary>
    /// Ouvre une modale de connexion bloquante pour collecter le pseudo et la clé API.
    /// </summary>
    private async Task AfficherModaleConnexionAsync(bool masquerContenuPrincipal = true)
    {
        ArreterActualisationAutomatique();
        if (masquerContenuPrincipal)
        {
            DefinirVisibiliteContenuPrincipal(false);
        }

        while (true)
        {
            UiControls.TextBox champPseudo = new()
            {
                MinWidth = LargeurContenuModaleConnexion,
                MaxWidth = LargeurContenuModaleConnexion,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                PlaceholderText = "Pseudo",
                Text = _configurationConnexion.Pseudo,
            };

            UiControls.PasswordBox champCleApi = new()
            {
                MinWidth = LargeurContenuModaleConnexion,
                MaxWidth = LargeurContenuModaleConnexion,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Password = _configurationConnexion.CleApiWeb,
                PlaceholderText = "Clé API",
            };

            SystemControls.TextBlock texteErreur = new()
            {
                MinWidth = LargeurContenuModaleConnexion,
                MaxWidth = LargeurContenuModaleConnexion,
                Margin = new Thickness(0, 12, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Visibility = Visibility.Collapsed,
                Foreground = Brushes.IndianRed,
                TextWrapping = TextWrapping.Wrap,
            };

            SystemControls.StackPanel contenu = new()
            {
                Width = LargeurContenuModaleConnexion,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0),
                Children =
                {
                    new SystemControls.TextBlock
                    {
                        MinWidth = LargeurContenuModaleConnexion,
                        MaxWidth = LargeurContenuModaleConnexion,
                        Text = "Entrez votre pseudo et clé API RetroAchievements.",
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(0, 0, 0, 14),
                    },
                    champPseudo,
                    champCleApi,
                    texteErreur,
                },
            };
            champPseudo.Margin = new Thickness(0, 0, 0, 14);

            SystemControls.Border conteneurContenu = new()
            {
                Padding = new Thickness(MargeInterieureModaleConnexion),
                HorizontalAlignment = HorizontalAlignment.Center,
                CornerRadius = ObtenirRayonCoins("RayonCoinsStandard", 12),
                Child = contenu,
            };

            UiControls.ContentDialog dialogueConnexion = new(RacineModales)
            {
                Title = "Connexion",
                Content = conteneurContenu,
                MinWidth = LargeurContenuModaleConnexion + (MargeInterieureModaleConnexion * 2),
                PrimaryButtonText = "Enregistrer",
                SecondaryButtonText = "Annuler",
                DefaultButton = UiControls.ContentDialogButton.Primary,
            };

            dialogueConnexion.Loaded += DialogueConnexion_Chargement;

            UiControls.ContentDialogResult resultat;

            try
            {
                resultat = await dialogueConnexion.ShowAsync();
            }
            finally
            {
                dialogueConnexion.Loaded -= DialogueConnexion_Chargement;
            }

            if (resultat != UiControls.ContentDialogResult.Primary)
            {
                if (ConfigurationConnexionEstComplete())
                {
                    if (masquerContenuPrincipal)
                    {
                        DefinirVisibiliteContenuPrincipal(true);
                    }
                    return;
                }

                Close();
                return;
            }

            string pseudo = champPseudo.Text.Trim();
            string cleApi = champCleApi.Password.Trim();

            if (string.IsNullOrWhiteSpace(pseudo) || string.IsNullOrWhiteSpace(cleApi))
            {
                texteErreur.Text = "Le pseudo et la clé Web API sont obligatoires.";
                texteErreur.Visibility = Visibility.Visible;
                continue;
            }

            try
            {
                await _clientRetroAchievements.ObtenirProfilUtilisateurAsync(pseudo, cleApi);
            }
            catch (UtilisateurRetroAchievementsInaccessibleException exception)
            {
                texteErreur.Text = exception.Message;
                texteErreur.Visibility = Visibility.Visible;
                continue;
            }
            catch (Exception exception)
            {
                texteErreur.Text =
                    $"Impossible de vérifier ce compte pour le moment : {exception.Message}";
                texteErreur.Visibility = Visibility.Visible;
                continue;
            }

            if (
                !string.Equals(
                    _configurationConnexion.Pseudo,
                    pseudo,
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                _configurationConnexion.DernierJeuAffiche = null;
            }

            _configurationConnexion.Pseudo = pseudo;
            _configurationConnexion.CleApiWeb = cleApi;

            MemoriserGeometrieFenetre();
            await _serviceConfigurationLocale.SauvegarderAsync(_configurationConnexion);

            MettreAJourResumeConnexion();
            if (masquerContenuPrincipal)
            {
                DefinirVisibiliteContenuPrincipal(true);
            }
            await ChargerJeuEnCoursAsync();
            DemarrerActualisationAutomatique();
            return;
        }
    }

    /// <summary>
    /// Retire le troisième bouton du footer natif et recentre le duo attendu.
    /// </summary>
    /// <summary>
    /// Affiche la modale de gestion du compte courant avec tous les champs du profil utilisateur.
    /// </summary>
    private async Task AfficherModaleCompteAsync()
    {
        ProfilUtilisateurRetroAchievements? profil =
            await ObtenirProfilUtilisateurPourModaleAsync();
        ResumeUtilisateurRetroAchievements? resume =
            await ObtenirResumeUtilisateurPourModaleAsync();

        SystemControls.StackPanel contenu = new()
        {
            Width = 460,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0),
        };
        SystemControls.Border conteneurContenu = new()
        {
            Padding = new Thickness(MargeInterieureModaleConnexion),
            HorizontalAlignment = HorizontalAlignment.Center,
            CornerRadius = ObtenirRayonCoins("RayonCoinsStandard", 12),
            Child = contenu,
        };

        UiControls.ContentDialog dialogueCompte = new(RacineModales)
        {
            Title = ConstruireEnteteModaleCompte(profil, resume),
            Content = conteneurContenu,
            MinWidth = 460 + (MargeInterieureModaleConnexion * 2),
            PrimaryButtonText = "Modifier la connexion",
            SecondaryButtonText = "Fermer",
            DefaultButton = UiControls.ContentDialogButton.Secondary,
        };

        dialogueCompte.Loaded += DialogueConnexion_Chargement;

        UiControls.ContentDialogResult resultat;

        try
        {
            resultat = await dialogueCompte.ShowAsync();
        }
        finally
        {
            dialogueCompte.Loaded -= DialogueConnexion_Chargement;
        }

        if (resultat == UiControls.ContentDialogResult.Primary)
        {
            await AfficherModaleConnexionAsync(false);
        }
    }

    /// <summary>
    /// Retourne le dernier résumé utilisateur connu ou le recharge pour la modale de compte.
    /// </summary>
    private async Task<ResumeUtilisateurRetroAchievements?> ObtenirResumeUtilisateurPourModaleAsync()
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
                await _clientRetroAchievements.ObtenirResumeUtilisateurAsync(
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

    /// <summary>
    /// Retourne le dernier profil utilisateur connu ou le recharge pour la modale de compte.
    /// </summary>
    private async Task<ProfilUtilisateurRetroAchievements?> ObtenirProfilUtilisateurPourModaleAsync()
    {
        if (_dernierProfilUtilisateurCharge is not null)
        {
            return _dernierProfilUtilisateurCharge;
        }

        if (!ConfigurationConnexionEstComplete())
        {
            return null;
        }

        try
        {
            _dernierProfilUtilisateurCharge =
                await _clientRetroAchievements.ObtenirProfilUtilisateurAsync(
                    _configurationConnexion.Pseudo,
                    _configurationConnexion.CleApiWeb
                );
            return _dernierProfilUtilisateurCharge;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Construit un bloc d'informations utilisateur sur deux colonnes.
    /// </summary>
    private SystemControls.Border ConstruireBlocCompte(
        string titre,
        IReadOnlyList<(string Libelle, string Valeur)> lignes
    )
    {
        SystemControls.StackPanel pile = new()
        {
            Margin = new Thickness(0, 0, 0, 14),
            Children =
            {
                new SystemControls.TextBlock
                {
                    Margin = new Thickness(0, 0, 0, 8),
                    FontSize = 16,
                    FontWeight = FontWeights.SemiBold,
                    Text = titre,
                },
            },
        };

        SystemControls.Grid grille = new();
        grille.ColumnDefinitions.Add(
            new SystemControls.ColumnDefinition { Width = new GridLength(180) }
        );
        grille.ColumnDefinitions.Add(
            new SystemControls.ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
        );

        for (int index = 0; index < lignes.Count; index++)
        {
            grille.RowDefinitions.Add(
                new SystemControls.RowDefinition { Height = GridLength.Auto }
            );

            if (index % 2 == 1)
            {
                SystemControls.Border fondLigne = new()
                {
                    Background = new SolidColorBrush(Color.FromArgb(40, 0, 0, 0)),
                    CornerRadius = ObtenirRayonCoins("RayonCoinsPetit", 8),
                    Padding = new Thickness(10, 8, 10, 8),
                };
                SystemControls.Grid.SetRow(fondLigne, index);
                SystemControls.Grid.SetColumnSpan(fondLigne, 2);
                grille.Children.Add(fondLigne);
            }

            SystemControls.TextBlock libelle = new()
            {
                Margin = new Thickness(10, 8, 10, 8),
                FontWeight = FontWeights.SemiBold,
                Text = lignes[index].Libelle,
                TextWrapping = TextWrapping.Wrap,
            };
            SystemControls.Grid.SetRow(libelle, index);
            SystemControls.Grid.SetColumn(libelle, 0);
            grille.Children.Add(libelle);

            SystemControls.TextBlock valeur = new()
            {
                Margin = new Thickness(10, 8, 10, 8),
                Text = lignes[index].Valeur,
                TextWrapping = TextWrapping.Wrap,
            };
            SystemControls.Grid.SetRow(valeur, index);
            SystemControls.Grid.SetColumn(valeur, 1);
            grille.Children.Add(valeur);
        }

        pile.Children.Add(grille);

        return new SystemControls.Border { Padding = new Thickness(0), Child = pile };
    }

    /// <summary>
    /// Construit un séparateur discret entre deux blocs d'informations.
    /// </summary>
    private static SystemControls.Separator ConstruireSeparateurBlocCompte()
    {
        return new SystemControls.Separator { Margin = new Thickness(0, 2, 0, 12), Opacity = 0.45 };
    }

    /// <summary>
    /// Formate la date de profil dans un style français lisible.
    /// </summary>
    private static string FormaterDateProfil(string? dateProfil)
    {
        if (string.IsNullOrWhiteSpace(dateProfil))
        {
            return "Indisponible";
        }

        if (
            DateTime.TryParseExact(
                dateProfil,
                "yyyy-MM-dd HH:mm:ss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out DateTime date
            )
        )
        {
            return date.ToString("d MMMM yyyy", CultureInfo.GetCultureInfo("fr-CA"));
        }

        if (
            DateTime.TryParse(
                dateProfil,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces,
                out date
            )
        )
        {
            return date.ToString("d MMMM yyyy", CultureInfo.GetCultureInfo("fr-CA"));
        }

        return dateProfil;
    }

    /// <summary>
    /// Formate un nombre utilisateur pour l'affichage compact de la modale.
    /// </summary>
    private static string FormaterNombre(int? valeur)
    {
        return valeur.HasValue ? valeur.Value.ToString(CultureInfo.CurrentCulture) : "Indisponible";
    }

    /// <summary>
    /// Construit la ligne de position globale du compte.
    /// </summary>
    private static string ConstruireResumePositionCompte(ResumeUtilisateurRetroAchievements? resume)
    {
        if (resume is null || resume.Rang <= 0 || resume.TotalClasses <= 0)
        {
            return "Position : indisponible";
        }

        return $"Position : {resume.Rang.ToString(CultureInfo.CurrentCulture)} sur {resume.TotalClasses.ToString(CultureInfo.CurrentCulture)}";
    }

    /// <summary>
    /// Construit l'URL absolue de l'avatar utilisateur si disponible.
    /// </summary>
    private static string ConstruireUrlAvatar(ProfilUtilisateurRetroAchievements profil)
    {
        return ConstruireUrlImageRetroAchievements(profil.CheminAvatar);
    }

    /// <summary>
    /// Construit l'URL absolue d'une image RetroAchievements si disponible.
    /// </summary>
    private static string ConstruireUrlImageRetroAchievements(string? cheminImage)
    {
        if (string.IsNullOrWhiteSpace(cheminImage))
        {
            return "Indisponible";
        }

        return cheminImage.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? cheminImage
            : $"https://retroachievements.org{cheminImage}";
    }

    /// <summary>
    /// Met à jour le visuel du jeu courant à partir du box art retourné par l'API.
    /// </summary>
    private void MettreAJourImageJeuEnCours(string? cheminImage)
    {
        string urlImage = ConstruireUrlImageRetroAchievements(cheminImage);

        if (string.IsNullOrWhiteSpace(urlImage) || urlImage == "Indisponible")
        {
            ReinitialiserImageJeuEnCours();
            return;
        }

        try
        {
            BitmapImage imageJeu = new();
            imageJeu.BeginInit();
            imageJeu.UriSource = new Uri(urlImage, UriKind.Absolute);
            imageJeu.CacheOption = BitmapCacheOption.OnLoad;
            imageJeu.EndInit();

            ImageJeuEnCours.Source = imageJeu;
            AppliquerCoinsArrondisImageJeuEnCours();
            ImageJeuEnCours.Visibility = Visibility.Visible;
            TexteImageJeuEnCours.Visibility = Visibility.Collapsed;
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
        ImageJeuEnCours.Source = null;
        ImageJeuEnCours.Clip = null;
        ImageJeuEnCours.Visibility = Visibility.Collapsed;
        TexteImageJeuEnCours.Text = string.Empty;
        TexteImageJeuEnCours.Visibility = Visibility.Collapsed;
    }

    /// <summary>
    /// Réinitialise le carrousel des visuels du jeu courant.
    /// </summary>
    private void ReinitialiserCarrouselVisuelsJeuEnCours()
    {
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
        MettreAJourAffichageVisuelJeuEnCours();
    }

    /// <summary>
    /// Met à jour le grand visuel et l'état du carrousel sous l'image.
    /// </summary>
    private void MettreAJourAffichageVisuelJeuEnCours()
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
        MettreAJourImageJeuEnCours(visuel.CheminImage);
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
    /// Construit les visuels disponibles pour le jeu courant.
    /// </summary>
    private async Task MettreAJourVisuelsJeuEnCoursAsync(JeuUtilisateurRetroAchievements jeu)
    {
        List<VisuelJeuEnCours> visuels = [];

        AjouterVisuelJeu(visuels, "Jaquette", jeu.CheminImageBoite);
        AjouterVisuelJeu(visuels, "Badge", await ObtenirCheminBadgeJeuAsync(jeu));
        DefinirVisuelsJeuEnCours(visuels);
    }

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

        await MettreAJourPremierSuccesNonDebloqueAsync(succes);
        await MettreAJourGrilleTousSuccesAsync(succes);
    }

    /// <summary>
    /// Met à jour la carte du premier succès restant à débloquer.
    /// </summary>
    private async Task MettreAJourPremierSuccesNonDebloqueAsync(
        List<SuccesJeuUtilisateurRetroAchievements> succes
    )
    {
        SuccesJeuUtilisateurRetroAchievements? premierSucces = succes.FirstOrDefault(item =>
            !SuccesEstDebloque(item)
        );

        if (premierSucces is null)
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
            return;
        }

        ImageSource? imageSucces = await ChargerImageDistanteAsync(
            ConstruireUrlBadgeDepuisNom(premierSucces.NomBadge, false)
        );

        if (imageSucces is not null)
        {
            ImagePremierSuccesNonDebloque.Source = ConvertirImageEnNoirEtBlanc(imageSucces);
            ImagePremierSuccesNonDebloque.Visibility = Visibility.Visible;
            TexteImagePremierSuccesNonDebloque.Visibility = Visibility.Collapsed;
            AppliquerCoinsArrondisImagePremierSuccesNonDebloque();
        }
        else
        {
            ImagePremierSuccesNonDebloque.Source = null;
            ImagePremierSuccesNonDebloque.Clip = null;
            ImagePremierSuccesNonDebloque.Visibility = Visibility.Collapsed;
            TexteImagePremierSuccesNonDebloque.Visibility = Visibility.Visible;
            TexteImagePremierSuccesNonDebloque.Text = "Visuel indisponible";
        }

        TexteTitrePremierSuccesNonDebloque.Text = premierSucces.Titre;
        TexteTitrePremierSuccesNonDebloque.Visibility = Visibility.Visible;
        string descriptionSucces = string.IsNullOrWhiteSpace(premierSucces.Description)
            ? "Aucune description disponible."
            : await _serviceTraductionTexte.TraduireVersFrancaisAsync(premierSucces.Description);
        TexteDescriptionPremierSuccesNonDebloque.Text = descriptionSucces.Trim();
        TexteDescriptionPremierSuccesNonDebloque.Visibility = Visibility.Visible;
        string detailsPoints = ConstruireDetailsPointsSucces(premierSucces);
        TextePointsPremierSuccesNonDebloque.Text = detailsPoints;
        TextePointsPremierSuccesNonDebloque.Visibility = string.IsNullOrWhiteSpace(detailsPoints)
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    /// <summary>
    /// Remplit la grille de tous les succès avec leurs badges.
    /// </summary>
    private async Task MettreAJourGrilleTousSuccesAsync(
        List<SuccesJeuUtilisateurRetroAchievements> succes
    )
    {
        GrilleTousSuccesJeuEnCours.Children.Clear();

        if (succes.Count == 0)
        {
            PlanifierMiseAJourAnimationGrilleTousSucces();
            return;
        }

        foreach (SuccesJeuUtilisateurRetroAchievements succesJeu in succes)
        {
            string urlBadge = ConstruireUrlBadgeDepuisNom(
                succesJeu.NomBadge,
                !SuccesEstDebloque(succesJeu)
            );
            ImageSource? imageBadge = await ChargerImageDistanteAsync(urlBadge);
            SystemControls.Border conteneur = new()
            {
                Width = TailleBadgeGrilleSucces,
                Height = TailleBadgeGrilleSucces,
                Margin = new Thickness(0),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0),
                ToolTip = succesJeu.Titre,
            };
            conteneur.MouseEnter += BadgeGrilleSucces_EntreeSouris;
            conteneur.MouseLeave += BadgeGrilleSucces_SortieSouris;

            if (imageBadge is null)
            {
                conteneur.Child = new SystemControls.TextBlock
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Opacity = 0.62,
                    Text = succesJeu.Titre,
                    TextAlignment = TextAlignment.Center,
                    TextWrapping = TextWrapping.Wrap,
                };
            }
            else
            {
                SystemControls.Image imageSucces = new()
                {
                    Source = imageBadge,
                    Width = 34,
                    Height = 34,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Stretch = Stretch.Uniform,
                };

                imageSucces.Loaded += (_, _) => AppliquerCoinsArrondisImage(imageSucces);
                imageSucces.SizeChanged += (_, _) => AppliquerCoinsArrondisImage(imageSucces);
                conteneur.Child = imageSucces;
            }

            GrilleTousSuccesJeuEnCours.Children.Add(conteneur);
        }

        MettreAJourDispositionGrilleTousSucces();
        PlanifierMiseAJourAnimationGrilleTousSucces();
    }

    /// <summary>
    /// Indique si un succès du jeu a déjà été obtenu par l'utilisateur.
    /// </summary>
    private static bool SuccesEstDebloque(SuccesJeuUtilisateurRetroAchievements succes)
    {
        return !string.IsNullOrWhiteSpace(succes.DateObtention)
            || !string.IsNullOrWhiteSpace(succes.DateObtentionHardcore);
    }

    /// <summary>
    /// Recalcule le gap de la grille des succès selon la largeur disponible.
    /// </summary>
    private void ConteneurGrilleTousSuccesJeuEnCours_TailleChangee(
        object sender,
        SizeChangedEventArgs e
    )
    {
        MettreAJourDispositionGrilleTousSucces();
        PlanifierMiseAJourAnimationGrilleTousSucces();
    }

    /// <summary>
    /// Répartit les badges sur la largeur disponible avec un espacement adaptatif.
    /// </summary>
    private void MettreAJourDispositionGrilleTousSucces()
    {
        if (
            ConteneurGrilleTousSuccesJeuEnCours is null
            || GrilleTousSuccesJeuEnCours is null
            || GrilleTousSuccesJeuEnCours.Children.Count == 0
        )
        {
            return;
        }

        double largeurDisponible = ConteneurGrilleTousSuccesJeuEnCours.ActualWidth;

        if (largeurDisponible <= 0)
        {
            return;
        }

        int nombreBadges = GrilleTousSuccesJeuEnCours.Children.Count;
        int colonnes = Math.Max(
            1,
            (int)
                Math.Floor(
                    (largeurDisponible + EspaceMinimalGrilleSucces)
                        / (TailleBadgeGrilleSucces + EspaceMinimalGrilleSucces)
                )
        );
        colonnes = Math.Min(colonnes, nombreBadges);

        double gapHorizontal =
            colonnes > 1
                ? Math.Max(
                    EspaceMinimalGrilleSucces,
                    (largeurDisponible - (colonnes * TailleBadgeGrilleSucces)) / (colonnes - 1)
                )
                : 0;

        for (int index = 0; index < nombreBadges; index++)
        {
            if (GrilleTousSuccesJeuEnCours.Children[index] is not SystemControls.Border badge)
            {
                continue;
            }

            int colonne = index % colonnes;
            bool dernierDeLigne = colonne == colonnes - 1;
            badge.Margin = new Thickness(
                0,
                0,
                dernierDeLigne ? 0 : gapHorizontal,
                EspaceMinimalGrilleSucces
            );
        }
    }

    /// <summary>
    /// Planifie le recalcul de la hauteur visible de la grille des succès.
    /// </summary>
    private void PlanifierMiseAJourAnimationGrilleTousSucces()
    {
        if (_miseAJourAnimationGrilleSuccesPlanifiee)
        {
            return;
        }

        _miseAJourAnimationGrilleSuccesPlanifiee = true;
        _ = Dispatcher.BeginInvoke(
            () =>
            {
                _miseAJourAnimationGrilleSuccesPlanifiee = false;
                MettreAJourAnimationGrilleTousSucces();
            },
            DispatcherPriority.Render
        );
    }

    /// <summary>
    /// Borne la hauteur visible de la grille des succès et anime son contenu en rebond si nécessaire.
    /// </summary>
    private void MettreAJourAnimationGrilleTousSucces()
    {
        if (
            ConteneurGrilleTousSuccesJeuEnCours is null
            || GrilleTousSuccesJeuEnCours is null
            || ZonePrincipale is null
        )
        {
            return;
        }

        if (GrilleTousSuccesJeuEnCours.Children.Count == 0)
        {
            _signatureAnimationGrilleSucces = string.Empty;
            _amplitudeAnimationGrilleSucces = 0;
            TranslateTransform translationVide =
                GrilleTousSuccesJeuEnCours.RenderTransform as TranslateTransform
                ?? new TranslateTransform();
            GrilleTousSuccesJeuEnCours.RenderTransform = translationVide;
            ArreterAnimationGrilleSucces(translationVide);
            GrilleTousSuccesJeuEnCours.Width = double.NaN;
            GrilleTousSuccesJeuEnCours.Height = double.NaN;
            ConteneurGrilleTousSuccesJeuEnCours.MaxHeight = double.PositiveInfinity;
            ConteneurGrilleTousSuccesJeuEnCours.Height = double.NaN;
            return;
        }

        double hauteurDisponible = CalculerHauteurDisponibleGrilleTousSucces();

        if (hauteurDisponible <= 0)
        {
            _signatureAnimationGrilleSucces = string.Empty;
            _amplitudeAnimationGrilleSucces = 0;
            TranslateTransform translationVide =
                GrilleTousSuccesJeuEnCours.RenderTransform as TranslateTransform
                ?? new TranslateTransform();
            GrilleTousSuccesJeuEnCours.RenderTransform = translationVide;
            ArreterAnimationGrilleSucces(translationVide);
            GrilleTousSuccesJeuEnCours.Width = double.NaN;
            GrilleTousSuccesJeuEnCours.Height = double.NaN;
            ConteneurGrilleTousSuccesJeuEnCours.MaxHeight = double.PositiveInfinity;
            ConteneurGrilleTousSuccesJeuEnCours.Height = double.NaN;
            return;
        }

        ConteneurGrilleTousSuccesJeuEnCours.MaxHeight = hauteurDisponible;
        ConteneurGrilleTousSuccesJeuEnCours.Height = hauteurDisponible;
        GrilleTousSuccesJeuEnCours.Measure(
            new Size(ConteneurGrilleTousSuccesJeuEnCours.ActualWidth, double.PositiveInfinity)
        );
        double hauteurContenu = GrilleTousSuccesJeuEnCours.DesiredSize.Height;
        GrilleTousSuccesJeuEnCours.Width = ConteneurGrilleTousSuccesJeuEnCours.ActualWidth;
        GrilleTousSuccesJeuEnCours.Height = hauteurContenu;
        double amplitude = Math.Max(0, hauteurContenu - hauteurDisponible + 8);
        _amplitudeAnimationGrilleSucces = amplitude;

        string signatureAnimation =
            $"{GrilleTousSuccesJeuEnCours.Children.Count}|{Math.Round(hauteurContenu, 1, MidpointRounding.AwayFromZero)}|{Math.Round(hauteurDisponible, 1, MidpointRounding.AwayFromZero)}|{Math.Round(amplitude, 1, MidpointRounding.AwayFromZero)}";

        TranslateTransform translation =
            GrilleTousSuccesJeuEnCours.RenderTransform as TranslateTransform
            ?? new TranslateTransform();
        GrilleTousSuccesJeuEnCours.RenderTransform = translation;

        if (amplitude <= SeuilDeclenchementDefilementGrilleSucces)
        {
            ArreterAnimationGrilleSucces(translation);
            _signatureAnimationGrilleSucces = signatureAnimation;
            return;
        }

        if (
            string.Equals(
                _signatureAnimationGrilleSucces,
                signatureAnimation,
                StringComparison.Ordinal
            )
        )
        {
            return;
        }

        _signatureAnimationGrilleSucces = signatureAnimation;
        ArreterAnimationGrilleSucces(translation);
        DemarrerAnimationGrilleSuccesDepuisPosition(
            translation.Y,
            amplitude,
            _animationGrilleSuccesVersBas
        );
    }

    /// <summary>
    /// Arrête l'animation verticale de la grille et réinitialise sa position.
    /// </summary>
    private void ArreterAnimationGrilleSucces(TranslateTransform translation)
    {
        _horlogeAnimationGrilleSucces?.Controller?.Stop();
        _horlogeAnimationGrilleSucces = null;
        translation.ApplyAnimationClock(TranslateTransform.YProperty, null);
    }

    /// <summary>
    /// Démarre l'animation verticale de la grille depuis une position donnée.
    /// </summary>
    private void DemarrerAnimationGrilleSuccesDepuisPosition(
        double positionInitiale,
        double amplitude,
        bool allerVersBas
    )
    {
        if (GrilleTousSuccesJeuEnCours is null)
        {
            return;
        }

        TranslateTransform translation =
            GrilleTousSuccesJeuEnCours.RenderTransform as TranslateTransform
            ?? new TranslateTransform();
        GrilleTousSuccesJeuEnCours.RenderTransform = translation;

        double position = Math.Clamp(positionInitiale, -amplitude, 0);
        translation.Y = position;
        _animationGrilleSuccesVersBas = allerVersBas;

        if (Math.Abs(position) <= 0.5)
        {
            DemarrerAnimationGrilleSuccesCyclique(translation, amplitude, true);
            return;
        }

        if (Math.Abs(position + amplitude) <= 0.5)
        {
            DemarrerAnimationGrilleSuccesCyclique(translation, amplitude, false);
            return;
        }

        double ciblePremierTrajet = allerVersBas ? -amplitude : 0;
        double distancePremierTrajet = Math.Abs(ciblePremierTrajet - position);
        double dureePremierTrajet = Math.Clamp(
            distancePremierTrajet / VitesseDefilementGrilleSuccesPixelsParSeconde,
            0.8,
            16
        );
        DoubleAnimation animationPremierTrajet = new()
        {
            From = position,
            To = ciblePremierTrajet,
            Duration = TimeSpan.FromSeconds(dureePremierTrajet),
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
            FillBehavior = FillBehavior.Stop,
        };

        AnimationClock horlogePremierTrajet = animationPremierTrajet.CreateClock();
        _horlogeAnimationGrilleSucces = horlogePremierTrajet;
        animationPremierTrajet.Completed += (_, _) =>
        {
            if (!ReferenceEquals(_horlogeAnimationGrilleSucces, horlogePremierTrajet))
            {
                return;
            }

            translation.ApplyAnimationClock(TranslateTransform.YProperty, null);
            translation.Y = ciblePremierTrajet;
            _horlogeAnimationGrilleSucces = null;

            if (_survolBadgeGrilleSuccesActif)
            {
                return;
            }

            DemarrerAnimationGrilleSuccesCyclique(
                translation,
                amplitude,
                departEnHaut: ciblePremierTrajet >= -0.5
            );
        };

        translation.ApplyAnimationClock(TranslateTransform.YProperty, horlogePremierTrajet);

        if (_survolBadgeGrilleSuccesActif)
        {
            horlogePremierTrajet.Controller?.Pause();
        }
    }

    /// <summary>
    /// Démarre un cycle de rebond complet entre le haut et le bas de la grille.
    /// </summary>
    private void DemarrerAnimationGrilleSuccesCyclique(
        TranslateTransform translation,
        double amplitude,
        bool departEnHaut
    )
    {
        double positionDepart = departEnHaut ? 0 : -amplitude;
        double positionArrivee = departEnHaut ? -amplitude : 0;
        double dureeTrajetSecondes = Math.Clamp(
            amplitude / VitesseDefilementGrilleSuccesPixelsParSeconde,
            4,
            16
        );
        TimeSpan pause = TimeSpan.FromSeconds(1.1);
        TimeSpan trajet = TimeSpan.FromSeconds(dureeTrajetSecondes);
        DoubleAnimationUsingKeyFrames animation = new() { RepeatBehavior = RepeatBehavior.Forever };

        translation.Y = positionDepart;
        _animationGrilleSuccesVersBas = departEnHaut;

        animation.KeyFrames.Add(
            new EasingDoubleKeyFrame(positionDepart, KeyTime.FromTimeSpan(TimeSpan.Zero))
        );
        animation.KeyFrames.Add(
            new EasingDoubleKeyFrame(positionDepart, KeyTime.FromTimeSpan(pause))
        );
        animation.KeyFrames.Add(
            new EasingDoubleKeyFrame(
                positionArrivee,
                KeyTime.FromTimeSpan(pause + trajet),
                new SineEase { EasingMode = EasingMode.EaseInOut }
            )
        );
        animation.KeyFrames.Add(
            new EasingDoubleKeyFrame(positionArrivee, KeyTime.FromTimeSpan(pause + trajet + pause))
        );
        animation.KeyFrames.Add(
            new EasingDoubleKeyFrame(
                positionDepart,
                KeyTime.FromTimeSpan(pause + trajet + pause + trajet),
                new SineEase { EasingMode = EasingMode.EaseInOut }
            )
        );
        animation.KeyFrames.Add(
            new EasingDoubleKeyFrame(
                positionDepart,
                KeyTime.FromTimeSpan(pause + trajet + pause + trajet + pause)
            )
        );

        _horlogeAnimationGrilleSucces = animation.CreateClock();
        translation.ApplyAnimationClock(
            TranslateTransform.YProperty,
            _horlogeAnimationGrilleSucces
        );

        if (_survolBadgeGrilleSuccesActif)
        {
            _horlogeAnimationGrilleSucces.Controller?.Pause();
        }
    }

    /// <summary>
    /// Calcule la hauteur visible maximale de la grille des succès dans la fenêtre.
    /// </summary>
    private double CalculerHauteurDisponibleGrilleTousSucces()
    {
        if (
            ConteneurGrilleTousSuccesJeuEnCours is null
            || ZonePrincipale is null
            || !ConteneurGrilleTousSuccesJeuEnCours.IsLoaded
        )
        {
            return 0;
        }

        double hauteurViewport =
            ZonePrincipale.ViewportHeight > 0
                ? ZonePrincipale.ViewportHeight
                : ZonePrincipale.ActualHeight;

        if (hauteurViewport <= 0)
        {
            return 0;
        }

        Point positionDansScrollViewer = ConteneurGrilleTousSuccesJeuEnCours.TranslatePoint(
            new Point(0, 0),
            ZonePrincipale
        );
        double margeBas = 12;
        double hauteurDisponible = hauteurViewport - positionDansScrollViewer.Y - margeBas;

        return Math.Max(HauteurMinimaleGrilleSucces, hauteurDisponible);
    }

    /// <summary>
    /// Met en pause l'animation des succès lors du survol d'un badge.
    /// </summary>
    private void BadgeGrilleSucces_EntreeSouris(object sender, MouseEventArgs e)
    {
        _survolBadgeGrilleSuccesActif = true;
        _minuteurRepriseAnimationGrilleSucces.Stop();
        _horlogeAnimationGrilleSucces?.Controller?.Pause();
    }

    /// <summary>
    /// Reprend l'animation des succès lorsque le survol d'un badge se termine.
    /// </summary>
    private void BadgeGrilleSucces_SortieSouris(object sender, MouseEventArgs e)
    {
        _survolBadgeGrilleSuccesActif = false;
        ReprendreAnimationGrilleSuccesSiPossible();
    }

    /// <summary>
    /// Permet de faire défiler manuellement la grille des succès à la molette.
    /// </summary>
    private void ConteneurGrilleTousSuccesJeuEnCours_ApercuMoletteSouris(
        object sender,
        MouseWheelEventArgs e
    )
    {
        if (
            GrilleTousSuccesJeuEnCours is null
            || _amplitudeAnimationGrilleSucces <= SeuilDeclenchementDefilementGrilleSucces
        )
        {
            return;
        }

        TranslateTransform translation =
            GrilleTousSuccesJeuEnCours.RenderTransform as TranslateTransform
            ?? new TranslateTransform();
        GrilleTousSuccesJeuEnCours.RenderTransform = translation;

        double positionCourante =
            _horlogeAnimationGrilleSucces?.GetCurrentValue(translation.Y, translation.Y) as double?
            ?? translation.Y;

        ArreterAnimationGrilleSucces(translation);

        double pas = Math.Abs(e.Delta) / 120d * 32d;
        double nouvellePosition = Math.Clamp(
            positionCourante + (e.Delta > 0 ? pas : -pas),
            -_amplitudeAnimationGrilleSucces,
            0
        );

        translation.Y = nouvellePosition;
        _animationGrilleSuccesVersBas = e.Delta < 0;
        _minuteurRepriseAnimationGrilleSucces.Stop();
        _minuteurRepriseAnimationGrilleSucces.Start();
        e.Handled = true;
    }

    /// <summary>
    /// Relance l'animation des succès après un défilement manuel.
    /// </summary>
    private void MinuteurRepriseAnimationGrilleSucces_Tick(object? sender, EventArgs e)
    {
        _minuteurRepriseAnimationGrilleSucces.Stop();
        ReprendreAnimationGrilleSuccesSiPossible();
    }

    /// <summary>
    /// Reprend ou recrée l'animation de la grille des succès si les conditions le permettent.
    /// </summary>
    private void ReprendreAnimationGrilleSuccesSiPossible()
    {
        if (
            _survolBadgeGrilleSuccesActif
            || GrilleTousSuccesJeuEnCours is null
            || _amplitudeAnimationGrilleSucces <= SeuilDeclenchementDefilementGrilleSucces
        )
        {
            return;
        }

        TranslateTransform translation =
            GrilleTousSuccesJeuEnCours.RenderTransform as TranslateTransform
            ?? new TranslateTransform();
        GrilleTousSuccesJeuEnCours.RenderTransform = translation;

        if (_horlogeAnimationGrilleSucces is not null)
        {
            _horlogeAnimationGrilleSucces.Controller?.Resume();
            return;
        }

        DemarrerAnimationGrilleSuccesDepuisPosition(
            translation.Y,
            _amplitudeAnimationGrilleSucces,
            _animationGrilleSuccesVersBas
        );
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
                await _clientRetroAchievements.ObtenirJeuxSystemeAvecHashesAsync(
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

    /// <summary>
    /// Réinitialise l'affichage des métadonnées sous le titre du jeu courant.
    /// </summary>
    private void ReinitialiserMetaConsoleJeuEnCours()
    {
        ImageConsoleJeuEnCours.Source = null;
        ImageConsoleJeuEnCours.Visibility = Visibility.Collapsed;
        TexteAnneeJeuEnCours.Text = string.Empty;
        TexteAnneeJeuEnCours.Visibility = Visibility.Collapsed;
        TexteConsoleJeuEnCours.Text = string.Empty;
        TexteConsoleJeuEnCours.Visibility = Visibility.Collapsed;
        TexteTypeJeuEnCours.Text = string.Empty;
        TexteTypeJeuEnCours.Visibility = Visibility.Collapsed;
        TexteDeveloppeurJeuEnCours.Text = string.Empty;
        TexteDeveloppeurJeuEnCours.Visibility = Visibility.Collapsed;
        LigneMetaJeuEnCours.Visibility = Visibility.Collapsed;
    }

    /// <summary>
    /// Met à jour l'année du jeu, sa console, son type, son développeur et l'icône officielle.
    /// </summary>
    private async Task MettreAJourMetaConsoleJeuEnCoursAsync(JeuUtilisateurRetroAchievements jeu)
    {
        ReinitialiserMetaConsoleJeuEnCours();

        string anneeJeu = ExtraireAnneeJeu(jeu.DateSortie);
        DefinirTitreJeuEnCours(jeu.Titre);

        if (!string.IsNullOrWhiteSpace(anneeJeu))
        {
            TexteAnneeJeuEnCours.Text = anneeJeu;
            TexteAnneeJeuEnCours.Visibility = Visibility.Visible;
        }

        if (!string.IsNullOrWhiteSpace(jeu.NomConsole))
        {
            TexteConsoleJeuEnCours.Text = jeu.NomConsole.Trim();
            TexteConsoleJeuEnCours.Visibility = Visibility.Visible;
        }

        if (!string.IsNullOrWhiteSpace(jeu.Genre))
        {
            string genreTraduit = await _serviceTraductionTexte.TraduireVersFrancaisAsync(
                jeu.Genre
            );
            TexteTypeJeuEnCours.Text = genreTraduit.Trim();
            TexteTypeJeuEnCours.Visibility = string.IsNullOrWhiteSpace(TexteTypeJeuEnCours.Text)
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        if (!string.IsNullOrWhiteSpace(jeu.Developpeur))
        {
            TexteDeveloppeurJeuEnCours.Text = jeu.Developpeur.Trim();
            TexteDeveloppeurJeuEnCours.Visibility = Visibility.Visible;
        }

        try
        {
            IReadOnlyList<ConsoleRetroAchievements> consoles =
                await _clientRetroAchievements.ObtenirConsolesAsync(
                    _configurationConnexion.CleApiWeb
                );
            ConsoleRetroAchievements? console = consoles.FirstOrDefault(item =>
                item.IdentifiantConsole == jeu.IdentifiantConsole
            );

            if (console is not null && !string.IsNullOrWhiteSpace(console.UrlIcone))
            {
                ImageSource? imageConsole = await ChargerImageDistanteAsync(console.UrlIcone);

                if (imageConsole is not null)
                {
                    ImageConsoleJeuEnCours.Source = imageConsole;
                    ImageConsoleJeuEnCours.Visibility = Visibility.Visible;
                }
            }
        }
        catch
        {
            // L'icône de console reste facultative. En cas d'échec, on conserve au moins l'année.
        }

        LigneMetaJeuEnCours.Visibility =
            ImageConsoleJeuEnCours.Visibility == Visibility.Visible
            || TexteConsoleJeuEnCours.Visibility == Visibility.Visible
            || TexteAnneeJeuEnCours.Visibility == Visibility.Visible
                ? Visibility.Visible
                : Visibility.Collapsed;
    }

    /// <summary>
    /// Met à jour la ligne de détails sous le type et le développeur du jeu.
    /// </summary>
    private void DefinirDetailsJeuEnCours(string details)
    {
        TexteDetailsJeuEnCours.Text = details;
        TexteDetailsJeuEnCours.Visibility = string.IsNullOrWhiteSpace(details)
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    /// <summary>
    /// Met à jour le temps de jeu affiché sous l'image du jeu.
    /// </summary>
    private void DefinirTempsJeuSousImage(string tempsJeu)
    {
        TexteTempsJeuSousImage.Text = tempsJeu;
        TexteTempsJeuSousImage.Visibility = string.IsNullOrWhiteSpace(tempsJeu)
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    /// <summary>
    /// Met à jour l'état du jeu dans l'en-tête de la carte de progression.
    /// </summary>
    private void DefinirEtatJeuDansProgression(string etat)
    {
        TexteEtatJeuDansProgression.Text = string.IsNullOrWhiteSpace(etat) ? "Progression" : etat;
    }

    /// <summary>
    /// Affiche ou masque les placeholders animés du chargement initial.
    /// </summary>
    private void DefinirChargementVisuel(bool actif)
    {
        Visibility visibilite = actif ? Visibility.Visible : Visibility.Collapsed;
        ShimmerJeuEnCoursImage.Visibility = visibilite;
        ShimmerJeuEnCoursInfos.Visibility = visibilite;
        ShimmerPremierSuccesNonDebloque.Visibility = visibilite;
        ShimmerProgressionJeuEnCours.Visibility = visibilite;
        ShimmerTousSuccesJeuEnCours.Visibility = visibilite;
    }

    /// <summary>
    /// Met à jour le titre du jeu puis relance son éventuel défilement.
    /// </summary>
    private void DefinirTitreJeuEnCours(string titre)
    {
        bool titreInchange = string.Equals(
            TexteTitreJeuEnCours.Text,
            titre,
            StringComparison.Ordinal
        );
        ConteneurTitreJeuEnCours.ToolTip = titre;

        if (titreInchange)
        {
            return;
        }

        _signatureAnimationTitreJeu = string.Empty;
        TexteTitreJeuEnCours.Text = titre;
        TexteTitreJeuEnCours.Width = double.NaN;
        TexteTitreJeuEnCours.FontSize = TaillePoliceTitreJeuNormale;
        PlanifierMiseAJourAnimationTitreJeuEnCours();
    }

    /// <summary>
    /// Recalcule l'animation horizontale du titre de jeu si sa largeur dépasse l'espace disponible.
    /// </summary>
    private void MettreAJourAnimationTitreJeuEnCours()
    {
        if (
            ConteneurTitreJeuEnCours is null
            || TexteTitreJeuEnCours is null
            || ZoneTitreJeuEnCours is null
            || ConteneurTitreJeuEnCours.ActualWidth <= 0
        )
        {
            return;
        }

        TranslateTransform translation =
            TexteTitreJeuEnCours.RenderTransform as TranslateTransform ?? new TranslateTransform();
        TexteTitreJeuEnCours.RenderTransform = translation;
        translation.BeginAnimation(TranslateTransform.XProperty, null);
        translation.X = 0;
        TexteTitreJeuEnCours.Width = double.NaN;
        TexteTitreJeuEnCours.FontSize = TaillePoliceTitreJeuNormale;
        double largeurTitreSouhaitee = MesurerLargeurTitreJeuEnCours();
        double largeurDisponible = ConteneurTitreJeuEnCours.ActualWidth;

        if (largeurTitreSouhaitee <= 0 || largeurDisponible <= 0)
        {
            return;
        }

        double amplitude = Math.Max(0, largeurTitreSouhaitee - largeurDisponible + 12);

        string signatureAnimation =
            $"{TexteTitreJeuEnCours.Text}|{Math.Round(largeurTitreSouhaitee, 1, MidpointRounding.AwayFromZero)}|{Math.Round(largeurDisponible, 1, MidpointRounding.AwayFromZero)}|{Math.Round(amplitude, 1, MidpointRounding.AwayFromZero)}";

        if (amplitude <= SeuilDeclenchementDefilementTitreJeu)
        {
            _signatureAnimationTitreJeu = signatureAnimation;
            return;
        }

        if (
            string.Equals(_signatureAnimationTitreJeu, signatureAnimation, StringComparison.Ordinal)
        )
        {
            return;
        }

        _signatureAnimationTitreJeu = signatureAnimation;

        TexteTitreJeuEnCours.Width = largeurTitreSouhaitee;
        System.Windows.Controls.Canvas.SetLeft(TexteTitreJeuEnCours, 0);

        double dureeTrajetSecondes = Math.Clamp(
            amplitude / VitesseDefilementTitreJeuPixelsParSeconde,
            4,
            16
        );
        TimeSpan pause = TimeSpan.FromSeconds(1.2);
        TimeSpan trajet = TimeSpan.FromSeconds(dureeTrajetSecondes);
        DoubleAnimationUsingKeyFrames animation = new() { RepeatBehavior = RepeatBehavior.Forever };

        animation.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        animation.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(pause)));
        animation.KeyFrames.Add(
            new EasingDoubleKeyFrame(
                -amplitude,
                KeyTime.FromTimeSpan(pause + trajet),
                new SineEase { EasingMode = EasingMode.EaseInOut }
            )
        );
        animation.KeyFrames.Add(
            new EasingDoubleKeyFrame(-amplitude, KeyTime.FromTimeSpan(pause + trajet + pause))
        );
        animation.KeyFrames.Add(
            new EasingDoubleKeyFrame(
                0,
                KeyTime.FromTimeSpan(pause + trajet + pause + trajet),
                new SineEase { EasingMode = EasingMode.EaseInOut }
            )
        );
        animation.KeyFrames.Add(
            new EasingDoubleKeyFrame(
                0,
                KeyTime.FromTimeSpan(pause + trajet + pause + trajet + pause)
            )
        );

        translation.BeginAnimation(TranslateTransform.XProperty, animation);
    }

    /// <summary>
    /// Planifie le recalcul de l'animation du titre à la fin du cycle de mise en page courant.
    /// </summary>
    private void PlanifierMiseAJourAnimationTitreJeuEnCours()
    {
        if (_miseAJourAnimationTitreJeuPlanifiee)
        {
            return;
        }

        _miseAJourAnimationTitreJeuPlanifiee = true;
        _ = Dispatcher.BeginInvoke(
            () =>
            {
                _miseAJourAnimationTitreJeuPlanifiee = false;
                MettreAJourAnimationTitreJeuEnCours();
            },
            DispatcherPriority.Render
        );
    }

    /// <summary>
    /// Mesure la largeur réelle du titre indépendamment du layout WPF courant.
    /// </summary>
    private double MesurerLargeurTitreJeuEnCours()
    {
        string texte = TexteTitreJeuEnCours.Text ?? string.Empty;

        if (string.IsNullOrWhiteSpace(texte))
        {
            return 0;
        }

        double pixelsParDip = VisualTreeHelper.GetDpi(TexteTitreJeuEnCours).PixelsPerDip;
        FormattedText texteMesure = new(
            texte,
            CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            new Typeface(
                TexteTitreJeuEnCours.FontFamily,
                TexteTitreJeuEnCours.FontStyle,
                TexteTitreJeuEnCours.FontWeight,
                TexteTitreJeuEnCours.FontStretch
            ),
            TexteTitreJeuEnCours.FontSize,
            Brushes.Transparent,
            pixelsParDip
        );

        return Math.Ceiling(texteMesure.WidthIncludingTrailingWhitespace);
    }

    /// <summary>
    /// Affiche temporairement la barre de défilement après un usage de la molette ou un scroll.
    /// </summary>
    private void AfficherTemporairementBarreDefilementPrincipale()
    {
        if (!ZonePrincipalePeutDefiler())
        {
            DefinirVisibiliteBarreDefilementPrincipale();
            return;
        }

        DefinirVisibiliteBarreDefilementPrincipale();
        _minuteurMasquageBarreDefilement.Stop();
        _minuteurMasquageBarreDefilement.Start();
    }

    /// <summary>
    /// Retourne la barre verticale du ScrollViewer principal.
    /// </summary>
    private SystemControls.Primitives.ScrollBar? ObtenirBarreDefilementVerticalePrincipale()
    {
        if (_barreDefilementVerticalePrincipale is not null)
        {
            return _barreDefilementVerticalePrincipale;
        }

        _barreDefilementVerticalePrincipale =
            TrouverDescendants<SystemControls.Primitives.ScrollBar>(ZonePrincipale)
                .FirstOrDefault(barre => barre.Orientation == SystemControls.Orientation.Vertical);

        return _barreDefilementVerticalePrincipale;
    }

    /// <summary>
    /// Masque la barre verticale principale sans changer la structure du layout.
    /// </summary>
    private void DefinirVisibiliteBarreDefilementPrincipale()
    {
        if (ZonePrincipale is not null)
        {
            ZonePrincipale.VerticalScrollBarVisibility = SystemControls.ScrollBarVisibility.Hidden;
        }

        SystemControls.Primitives.ScrollBar? barre = ObtenirBarreDefilementVerticalePrincipale();

        if (barre is null)
        {
            return;
        }

        barre.Opacity = 0;
        barre.Visibility = Visibility.Hidden;
        barre.IsHitTestVisible = false;
    }

    /// <summary>
    /// Indique si la souris est placée sur la zone réservée à la barre verticale.
    /// </summary>
    private static bool EstDansZoneBarreDefilement(
        SystemControls.ScrollViewer scrollViewer,
        Point position
    )
    {
        return position.X
            >= Math.Max(0, scrollViewer.ActualWidth - LargeurZoneDetectionBarreDefilement);
    }

    /// <summary>
    /// Indique si le ScrollViewer principal a réellement besoin d'une barre verticale.
    /// </summary>
    private bool ZonePrincipalePeutDefiler()
    {
        return ZonePrincipale is not null && ZonePrincipale.ScrollableHeight > 0;
    }

    /// <summary>
    /// Indique si la souris survole la zone d'apparition de la barre verticale.
    /// </summary>
    private bool SourisSurvoleZoneBarreDefilement()
    {
        if (ZonePrincipale is null || !ZonePrincipale.IsMouseOver)
        {
            return false;
        }

        Point position = Mouse.GetPosition(ZonePrincipale);
        return EstDansZoneBarreDefilement(ZonePrincipale, position);
    }

    /// <summary>
    /// Affiche temporairement la barre verticale quand la molette est utilisée.
    /// </summary>
    private void ZonePrincipale_ApercuMoletteSouris(object sender, MouseWheelEventArgs e)
    {
        AfficherTemporairementBarreDefilementPrincipale();
    }

    /// <summary>
    /// Révèle la barre seulement si la souris survole sa zone dédiée.
    /// </summary>
    private void ZonePrincipale_DeplacementSouris(object sender, MouseEventArgs e)
    {
        if (sender is not SystemControls.ScrollViewer scrollViewer || !ZonePrincipalePeutDefiler())
        {
            DefinirVisibiliteBarreDefilementPrincipale();
            return;
        }

        bool surZoneBarre = EstDansZoneBarreDefilement(scrollViewer, e.GetPosition(scrollViewer));

        if (surZoneBarre)
        {
            _minuteurMasquageBarreDefilement.Stop();
            DefinirVisibiliteBarreDefilementPrincipale();
            return;
        }

        if (!_minuteurMasquageBarreDefilement.IsEnabled)
        {
            DefinirVisibiliteBarreDefilementPrincipale();
        }
    }

    /// <summary>
    /// Masque la barre en quittant la zone, sauf si un défilement récent la maintient visible.
    /// </summary>
    private void ZonePrincipale_SortieSouris(object sender, MouseEventArgs e)
    {
        if (!_minuteurMasquageBarreDefilement.IsEnabled)
        {
            DefinirVisibiliteBarreDefilementPrincipale();
        }
    }

    /// <summary>
    /// Rend la barre visible pendant un défilement effectif du contenu.
    /// </summary>
    private void ZonePrincipale_DefilementChange(
        object sender,
        SystemControls.ScrollChangedEventArgs e
    )
    {
        if (Math.Abs(e.VerticalChange) > 0.01)
        {
            AfficherTemporairementBarreDefilementPrincipale();
        }
    }

    /// <summary>
    /// Masque la barre après un court délai si la souris n'est plus sur sa zone.
    /// </summary>
    private void MinuteurMasquageBarreDefilement_Tick(object? sender, EventArgs e)
    {
        if (SourisSurvoleZoneBarreDefilement())
        {
            return;
        }

        _minuteurMasquageBarreDefilement.Stop();
        DefinirVisibiliteBarreDefilementPrincipale();
    }

    /// <summary>
    /// Réapplique le dernier jeu sauvegardé pour éviter une fenêtre vide au démarrage.
    /// </summary>
    private async Task AppliquerDernierJeuSauvegardeAsync()
    {
        EtatJeuAfficheLocal? jeuSauvegarde = _configurationConnexion.DernierJeuAffiche;

        if (jeuSauvegarde is null || string.IsNullOrWhiteSpace(jeuSauvegarde.Titre))
        {
            return;
        }

        DefinirTitreZoneJeu(jeuSauvegarde.EstJeuEnCours);
        _dernierTitreJeuApi = jeuSauvegarde.Titre;
        _dernierIdentifiantJeuAvecInfos = jeuSauvegarde.IdentifiantJeu;
        _dernierIdentifiantJeuAvecProgression = jeuSauvegarde.IdentifiantJeu;

        DefinirVisuelsJeuEnCours(
            string.IsNullOrWhiteSpace(jeuSauvegarde.CheminImageBoite)
                ? []
                : [new VisuelJeuEnCours("Jaquette", jeuSauvegarde.CheminImageBoite)]
        );
        await MettreAJourMetaConsoleJeuEnCoursAsync(
            ConstruireJeuUtilisateurDepuisEtatLocal(jeuSauvegarde)
        );

        DefinirTitreJeuEnCours(jeuSauvegarde.Titre);
        DefinirDetailsJeuEnCours(jeuSauvegarde.Details);
        DefinirTempsJeuSousImage(jeuSauvegarde.TempsJeuSousImage);
        DefinirEtatJeuDansProgression(jeuSauvegarde.EtatJeu);
        TexteResumeProgressionJeuEnCours.Text = string.IsNullOrWhiteSpace(
            jeuSauvegarde.ResumeProgression
        )
            ? "-- / --"
            : jeuSauvegarde.ResumeProgression;
        TextePourcentageJeuEnCours.Text = string.IsNullOrWhiteSpace(
            jeuSauvegarde.PourcentageProgression
        )
            ? "Progression du jeu indisponible"
            : jeuSauvegarde.PourcentageProgression;
        BarreProgressionJeuEnCours.Value = Math.Clamp(jeuSauvegarde.ValeurProgression, 0, 100);
    }

    /// <summary>
    /// Sauvegarde l'état actuellement affiché du jeu pour le prochain démarrage.
    /// </summary>
    private Task SauvegarderDernierJeuAfficheAsync(
        JeuUtilisateurRetroAchievements jeu,
        string detailsTempsJeu,
        string detailsEtatJeu
    )
    {
        _configurationConnexion.DernierJeuAffiche = new EtatJeuAfficheLocal
        {
            IdentifiantJeu = jeu.IdentifiantJeu,
            EstJeuEnCours = string.Equals(
                TitreZoneJeuEnCours.Text,
                "Jeu en cours",
                StringComparison.Ordinal
            ),
            Titre = jeu.Titre,
            Details = TexteDetailsJeuEnCours.Text,
            ResumeProgression = TexteResumeProgressionJeuEnCours.Text,
            PourcentageProgression = TextePourcentageJeuEnCours.Text,
            ValeurProgression = BarreProgressionJeuEnCours.Value,
            TempsJeuSousImage = detailsTempsJeu,
            EtatJeu = detailsEtatJeu,
            CheminImageBoite = jeu.CheminImageBoite,
            IdentifiantConsole = jeu.IdentifiantConsole,
            DateSortie = jeu.DateSortie,
            Genre = jeu.Genre,
            Developpeur = jeu.Developpeur,
        };
        _dernierJeuAfficheModifie = true;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Sauvegarde un état local du jeu affiché dès la détection de l'émulateur.
    /// </summary>
    private Task SauvegarderJeuLocalAfficheAsync(
        JeuDetecteLocalement jeuLocal,
        string detailsJeuLocal
    )
    {
        string signatureLocale = ConstruireSignatureJeuLocal(jeuLocal);
        EtatJeuAfficheLocal nouvelEtat = ConstruireEtatJeuAfficheLocal(jeuLocal, detailsJeuLocal);
        nouvelEtat.SignatureLocale = signatureLocale;

        if (EtatJeuAfficheEquivalent(_configurationConnexion.DernierJeuAffiche, nouvelEtat))
        {
            return Task.CompletedTask;
        }

        _configurationConnexion.DernierJeuAffiche = nouvelEtat;
        _dernierJeuAfficheModifie = true;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Construit un état local persistant à partir du jeu détecté et des informations visibles.
    /// </summary>
    private EtatJeuAfficheLocal ConstruireEtatJeuAfficheLocal(
        JeuDetecteLocalement jeuLocal,
        string detailsJeuLocal
    )
    {
        EtatJeuAfficheLocal? etatPrecedent = _configurationConnexion.DernierJeuAffiche;
        bool conserverInformationsApi = JeuLocalCorrespondAuJeuAffiche(jeuLocal);

        return new EtatJeuAfficheLocal
        {
            IdentifiantJeu = conserverInformationsApi ? _dernierIdentifiantJeuAvecInfos : 0,
            EstJeuEnCours = true,
            Titre = TexteTitreJeuEnCours.Text,
            Details = detailsJeuLocal,
            ResumeProgression = TexteResumeProgressionJeuEnCours.Text,
            PourcentageProgression = TextePourcentageJeuEnCours.Text,
            ValeurProgression = BarreProgressionJeuEnCours.Value,
            TempsJeuSousImage = TexteTempsJeuSousImage.Text,
            EtatJeu = TexteEtatJeuDansProgression.Text,
            CheminImageBoite = conserverInformationsApi
                ? etatPrecedent?.CheminImageBoite ?? string.Empty
                : string.Empty,
            IdentifiantConsole = conserverInformationsApi
                ? etatPrecedent?.IdentifiantConsole ?? 0
                : 0,
            DateSortie = conserverInformationsApi
                ? etatPrecedent?.DateSortie ?? string.Empty
                : string.Empty,
            Genre = conserverInformationsApi ? etatPrecedent?.Genre ?? string.Empty : string.Empty,
            Developpeur = conserverInformationsApi
                ? etatPrecedent?.Developpeur ?? string.Empty
                : string.Empty,
        };
    }

    /// <summary>
    /// Compare deux états locaux pour éviter une sauvegarde identique.
    /// </summary>
    private static bool EtatJeuAfficheEquivalent(
        EtatJeuAfficheLocal? precedent,
        EtatJeuAfficheLocal courant
    )
    {
        if (precedent is null)
        {
            return false;
        }

        return precedent.SignatureLocale == courant.SignatureLocale
            && precedent.IdentifiantJeu == courant.IdentifiantJeu
            && precedent.EstJeuEnCours == courant.EstJeuEnCours
            && precedent.Titre == courant.Titre
            && precedent.Details == courant.Details
            && precedent.ResumeProgression == courant.ResumeProgression
            && precedent.PourcentageProgression == courant.PourcentageProgression
            && Math.Abs(precedent.ValeurProgression - courant.ValeurProgression) < 0.01
            && precedent.TempsJeuSousImage == courant.TempsJeuSousImage
            && precedent.EtatJeu == courant.EtatJeu
            && precedent.CheminImageBoite == courant.CheminImageBoite
            && precedent.IdentifiantConsole == courant.IdentifiantConsole
            && precedent.DateSortie == courant.DateSortie
            && precedent.Genre == courant.Genre
            && precedent.Developpeur == courant.Developpeur;
    }

    /// <summary>
    /// Reconstruit un modèle de jeu minimal à partir de l'état local sauvegardé.
    /// </summary>
    private static JeuUtilisateurRetroAchievements ConstruireJeuUtilisateurDepuisEtatLocal(
        EtatJeuAfficheLocal jeuSauvegarde
    )
    {
        return new JeuUtilisateurRetroAchievements
        {
            IdentifiantJeu = jeuSauvegarde.IdentifiantJeu,
            Titre = jeuSauvegarde.Titre,
            IdentifiantConsole = jeuSauvegarde.IdentifiantConsole,
            DateSortie = jeuSauvegarde.DateSortie,
            Genre = jeuSauvegarde.Genre,
            Developpeur = jeuSauvegarde.Developpeur,
            CheminImageBoite = jeuSauvegarde.CheminImageBoite,
        };
    }

    /// <summary>
    /// Télécharge une image distante puis la garde en cache mémoire pour les prochains affichages.
    /// </summary>
    private async Task<ImageSource?> ChargerImageDistanteAsync(string urlImage)
    {
        if (string.IsNullOrWhiteSpace(urlImage))
        {
            return null;
        }

        if (_cacheImagesDistantes.TryGetValue(urlImage, out ImageSource? imageCachee))
        {
            return imageCachee;
        }

        using HttpResponseMessage reponse = await HttpClientImages.GetAsync(urlImage);
        reponse.EnsureSuccessStatusCode();

        await using Stream fluxImage = await reponse.Content.ReadAsStreamAsync();
        MemoryStream memoire = new();
        await fluxImage.CopyToAsync(memoire);
        memoire.Position = 0;

        BitmapImage image = new();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = memoire;
        image.EndInit();
        image.Freeze();

        _cacheImagesDistantes[urlImage] = image;
        return image;
    }

    /// <summary>
    /// Met à jour le titre de la zone principale selon la détection locale d'un émulateur.
    /// </summary>
    private void DefinirTitreZoneJeu(bool emulateurLocalDetecte)
    {
        bool basculeVersDernierJeuJoue =
            string.Equals(TitreZoneJeuEnCours.Text, "Jeu en cours", StringComparison.Ordinal)
            && !emulateurLocalDetecte;

        TitreZoneJeuEnCours.Text = emulateurLocalDetecte ? "Jeu en cours" : "Dernier jeu joué";

        if (basculeVersDernierJeuJoue)
        {
            _ = PersisterDernierJeuAfficheSiNecessaireAsync();
        }
    }

    /// <summary>
    /// Écrit la configuration sur disque seulement si le dernier jeu affiché a changé.
    /// </summary>
    private async Task PersisterDernierJeuAfficheSiNecessaireAsync()
    {
        if (!_dernierJeuAfficheModifie)
        {
            return;
        }

        _dernierJeuAfficheModifie = false;

        try
        {
            await _serviceConfigurationLocale.SauvegarderAsync(_configurationConnexion);
        }
        catch
        {
            _dernierJeuAfficheModifie = true;
        }
    }

    /// <summary>
    /// Recalcule la découpe arrondie de l'image du jeu quand sa taille change.
    /// </summary>
    private void ImageJeuEnCours_TailleChangee(object sender, SizeChangedEventArgs e)
    {
        AppliquerCoinsArrondisImageJeuEnCours();
    }

    /// <summary>
    /// Recalcule la découpe arrondie du badge du premier succès quand sa taille change.
    /// </summary>
    private void ImagePremierSuccesNonDebloque_TailleChangee(object sender, SizeChangedEventArgs e)
    {
        AppliquerCoinsArrondisImagePremierSuccesNonDebloque();
    }

    /// <summary>
    /// Affiche le visuel précédent du jeu courant.
    /// </summary>
    private void VisuelJeuPrecedent_Click(object sender, RoutedEventArgs e)
    {
        if (_visuelsJeuEnCours.Count <= 1)
        {
            return;
        }

        _indexVisuelJeuEnCours--;
        MettreAJourAffichageVisuelJeuEnCours();
    }

    /// <summary>
    /// Affiche le visuel suivant du jeu courant.
    /// </summary>
    private void VisuelJeuSuivant_Click(object sender, RoutedEventArgs e)
    {
        if (_visuelsJeuEnCours.Count <= 1)
        {
            return;
        }

        _indexVisuelJeuEnCours++;
        MettreAJourAffichageVisuelJeuEnCours();
    }

    /// <summary>
    /// Recalcule le défilement du titre quand sa taille ou celle de son conteneur change.
    /// </summary>
    private void TitreJeuEnCours_MiseEnPageChangee(object sender, SizeChangedEventArgs e)
    {
        _signatureAnimationTitreJeu = string.Empty;
        PlanifierMiseAJourAnimationTitreJeuEnCours();
    }

    /// <summary>
    /// Applique les coins arrondis à l'image du jeu courant selon sa taille réelle.
    /// </summary>
    private void AppliquerCoinsArrondisImageJeuEnCours()
    {
        AppliquerCoinsArrondisImage(ImageJeuEnCours);
    }

    /// <summary>
    /// Applique les coins arrondis au badge du premier succès selon sa taille réelle.
    /// </summary>
    private void AppliquerCoinsArrondisImagePremierSuccesNonDebloque()
    {
        AppliquerCoinsArrondisImage(ImagePremierSuccesNonDebloque);
    }

    /// <summary>
    /// Applique une découpe arrondie à une image selon sa taille réelle.
    /// </summary>
    private void AppliquerCoinsArrondisImage(SystemControls.Image image)
    {
        if (image.ActualWidth <= 0 || image.ActualHeight <= 0)
        {
            return;
        }

        double rayon = ObtenirRayonCoins("RayonCoinsPetit", 8).TopLeft;
        image.Clip = new RectangleGeometry(
            new Rect(0, 0, image.ActualWidth, image.ActualHeight),
            rayon,
            rayon
        );
    }

    /// <summary>
    /// Retourne le titre à afficher dans la modale utilisateur.
    /// </summary>
    private string ObtenirTitreModaleCompte(ProfilUtilisateurRetroAchievements? profil)
    {
        if (!string.IsNullOrWhiteSpace(profil?.NomUtilisateur))
        {
            return profil.NomUtilisateur;
        }

        return ObtenirLibelleBoutonCompte();
    }

    /// <summary>
    /// Construit l'en-tête de la modale utilisateur avec le pseudo et la devise.
    /// </summary>
    private SystemControls.Grid ConstruireEnteteModaleCompte(
        ProfilUtilisateurRetroAchievements? profil,
        ResumeUtilisateurRetroAchievements? resume
    )
    {
        SystemControls.Grid entete = new()
        {
            Margin = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ColumnDefinitions =
            {
                new SystemControls.ColumnDefinition
                {
                    Width = new GridLength(1, GridUnitType.Star),
                },
                new SystemControls.ColumnDefinition
                {
                    Width = new GridLength(1, GridUnitType.Star),
                },
            },
        };

        SystemControls.Grid blocPrincipal = new()
        {
            Margin = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Top,
            ColumnDefinitions =
            {
                new SystemControls.ColumnDefinition { Width = GridLength.Auto },
                new SystemControls.ColumnDefinition
                {
                    Width = new GridLength(1, GridUnitType.Star),
                },
            },
        };

        SystemControls.StackPanel blocTexte = new()
        {
            Margin = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Top,
            Children =
            {
                new SystemControls.TextBlock
                {
                    FontSize = 24,
                    FontWeight = FontWeights.SemiBold,
                    Text = ObtenirTitreModaleCompte(profil),
                    TextWrapping = TextWrapping.Wrap,
                },
            },
        };

        if (!string.IsNullOrWhiteSpace(profil?.Devise))
        {
            blocTexte.Children.Add(
                new SystemControls.TextBlock
                {
                    Margin = new Thickness(0, 4, 0, 0),
                    Opacity = 0.72,
                    FontSize = 14,
                    FontStyle = FontStyles.Italic,
                    Text = profil.Devise,
                    TextWrapping = TextWrapping.Wrap,
                }
            );
        }

        SystemControls.Image? imageAvatar = ConstruireImageAvatarCompte(profil);
        if (imageAvatar is not null)
        {
            SystemControls.Grid.SetColumn(imageAvatar, 0);
            blocPrincipal.Children.Add(imageAvatar);
        }

        SystemControls.Grid.SetColumn(blocTexte, 1);
        blocPrincipal.Children.Add(blocTexte);

        SystemControls.Grid.SetColumn(blocPrincipal, 0);
        entete.Children.Add(blocPrincipal);

        SystemControls.Grid grilleResume = ConstruireGrilleResumeEnteteCompte(profil, resume);
        SystemControls.Grid.SetColumn(grilleResume, 1);
        grilleResume.HorizontalAlignment = HorizontalAlignment.Stretch;
        grilleResume.VerticalAlignment = VerticalAlignment.Top;
        entete.Children.Add(grilleResume);

        return entete;
    }

    /// <summary>
    /// Construit une petite grille d'informations à droite de l'en-tête utilisateur.
    /// </summary>
    private static SystemControls.Grid ConstruireGrilleResumeEnteteCompte(
        ProfilUtilisateurRetroAchievements? profil,
        ResumeUtilisateurRetroAchievements? resume
    )
    {
        SystemControls.Grid grille = new()
        {
            Margin = new Thickness(16, 0, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Top,
            RowDefinitions =
            {
                new SystemControls.RowDefinition { Height = GridLength.Auto },
                new SystemControls.RowDefinition { Height = GridLength.Auto },
                new SystemControls.RowDefinition { Height = GridLength.Auto },
            },
        };

        grille.Children.Add(
            ConstruireLigneEnteteCompte(
                $"Membre depuis le {FormaterDateProfil(resume?.MembreDepuis ?? profil?.MembreDepuis)}",
                0
            )
        );
        grille.Children.Add(
            ConstruireLigneEnteteCompte(
                $"Points : {FormaterNombre(resume?.PointsTotaux ?? profil?.PointsTotaux)} ({FormaterNombre(resume?.TruePointsTotaux ?? profil?.TruePointsTotaux)})",
                1
            )
        );
        grille.Children.Add(ConstruireLigneEnteteCompte(ConstruireResumePositionCompte(resume), 2));
        return grille;
    }

    /// <summary>
    /// Construit une ligne compacte pour la grille de droite de l'en-tête utilisateur.
    /// </summary>
    private static SystemControls.Border ConstruireLigneEnteteCompte(string texte, int ligne)
    {
        SystemControls.Border conteneur = new()
        {
            Margin = new Thickness(0, ligne == 0 ? 0 : 6, 0, 0),
            Padding = new Thickness(10, 6, 10, 6),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Background =
                ligne % 2 == 0
                    ? new SolidColorBrush(Color.FromArgb(36, 0, 0, 0))
                    : new SolidColorBrush(Color.FromArgb(18, 255, 255, 255)),
            CornerRadius = new CornerRadius(8),
            Child = new SystemControls.TextBlock
            {
                FontSize = 12,
                Opacity = 0.86,
                Text = texte,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
            },
        };

        SystemControls.Grid.SetRow(conteneur, ligne);
        return conteneur;
    }

    /// <summary>
    /// Construit l'image d'avatar à afficher dans l'en-tête de la modale utilisateur.
    /// </summary>
    private static SystemControls.Image? ConstruireImageAvatarCompte(
        ProfilUtilisateurRetroAchievements? profil
    )
    {
        string urlAvatar = profil is null ? string.Empty : ConstruireUrlAvatar(profil);

        if (string.IsNullOrWhiteSpace(urlAvatar) || urlAvatar == "Indisponible")
        {
            return null;
        }

        try
        {
            BitmapImage imageAvatar = new();
            imageAvatar.BeginInit();
            imageAvatar.UriSource = new Uri(urlAvatar, UriKind.Absolute);
            imageAvatar.CacheOption = BitmapCacheOption.OnLoad;
            imageAvatar.EndInit();

            return new SystemControls.Image
            {
                Width = 44,
                Height = 44,
                Margin = new Thickness(0, 0, 16, 0),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Stretch = Stretch.UniformToFill,
                Source = imageAvatar,
            };
        }
        catch
        {
            return null;
        }
    }

    private void DialogueConnexion_Chargement(object sender, RoutedEventArgs e)
    {
        if (sender is not UiControls.ContentDialog dialogueConnexion)
        {
            return;
        }

        dialogueConnexion.Dispatcher.BeginInvoke(
            () => AjusterPiedModaleConnexion(dialogueConnexion),
            DispatcherPriority.Loaded
        );
    }

    /// <summary>
    /// Conserve uniquement les deux boutons explicitement définis pour la modale.
    /// </summary>
    private static void AjusterPiedModaleConnexion(UiControls.ContentDialog dialogueConnexion)
    {
        List<SystemControls.Button> boutons =
        [
            .. TrouverDescendants<SystemControls.Button>(dialogueConnexion),
        ];

        string texteBoutonPrincipal = dialogueConnexion.PrimaryButtonText?.Trim() ?? string.Empty;
        string texteBoutonSecondaire =
            dialogueConnexion.SecondaryButtonText?.Trim() ?? string.Empty;

        SystemControls.Button? boutonSecondaire = boutons.Find(bouton =>
            TexteBouton(bouton).Equals(texteBoutonSecondaire, StringComparison.OrdinalIgnoreCase)
        );
        SystemControls.Button? boutonPrincipal = boutons.Find(bouton =>
            TexteBouton(bouton).Equals(texteBoutonPrincipal, StringComparison.OrdinalIgnoreCase)
        );

        if (boutonSecondaire is null || boutonPrincipal is null)
        {
            return;
        }

        boutonSecondaire.MinWidth = 120;
        boutonPrincipal.MinWidth = 120;

        SystemControls.Panel? conteneurBoutons = TrouverPanneauCommun(
            boutonSecondaire,
            boutonPrincipal
        );

        if (conteneurBoutons is null)
        {
            return;
        }

        foreach (
            SystemControls.Button boutonSupplementaire in TrouverDescendants<SystemControls.Button>(
                conteneurBoutons
            )
        )
        {
            if (
                ReferenceEquals(boutonSupplementaire, boutonSecondaire)
                || ReferenceEquals(boutonSupplementaire, boutonPrincipal)
            )
            {
                continue;
            }

            boutonSupplementaire.Visibility = Visibility.Collapsed;
            boutonSupplementaire.IsEnabled = false;
        }

        conteneurBoutons.HorizontalAlignment = HorizontalAlignment.Center;
    }

    /// <summary>
    /// Déclenche un cycle périodique d'actualisation API.
    /// </summary>
    /// <summary>
    /// Bascule l'affichage de la carte de connexion.
    /// </summary>
    private void BasculerCarteConnexion_Click(object sender, RoutedEventArgs e)
    {
        _carteConnexionRepliee = !_carteConnexionRepliee;
        AppliquerEtatCarteConnexion();
    }

    /// <summary>
    /// Bascule l'affichage de la carte du jeu en cours.
    /// </summary>
    private void BasculerCarteJeuEnCours_Click(object sender, RoutedEventArgs e)
    {
        _carteJeuEnCoursRepliee = !_carteJeuEnCoursRepliee;
        AppliquerEtatCarteJeuEnCours();
    }

    /// <summary>
    /// Applique l'etat visuel initial des sections repliables.
    /// </summary>
    private void AppliquerEtatSectionsRepliables()
    {
        AppliquerEtatCarteConnexion();
        AppliquerEtatCarteJeuEnCours();
    }

    /// <summary>
    /// Replie ou déplie le contenu de la carte de connexion en conservant son titre visible.
    /// </summary>
    private void AppliquerEtatCarteConnexion()
    {
        if (ContenuCarteConnexion is null)
        {
            return;
        }

        for (int index = 1; index < ContenuCarteConnexion.Children.Count; index++)
        {
            ContenuCarteConnexion.Children[index].Visibility = _carteConnexionRepliee
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        IconeRepliCarteConnexion.Text = _carteConnexionRepliee ? "+" : "-";
        BoutonRepliCarteConnexion.ToolTip = _carteConnexionRepliee
            ? "Déplier la section"
            : "Replier la section";
    }

    /// <summary>
    /// Replie ou déplie le contenu de la carte du jeu en cours en conservant son en-tête visible.
    /// </summary>
    private void AppliquerEtatCarteJeuEnCours()
    {
        if (GrilleCarteJeuEnCours is null)
        {
            return;
        }

        for (int index = 1; index < GrilleCarteJeuEnCours.Children.Count; index++)
        {
            if (GrilleCarteJeuEnCours.Children[index] is UIElement element)
            {
                element.Visibility = _carteJeuEnCoursRepliee
                    ? Visibility.Collapsed
                    : Visibility.Visible;
            }
        }

        IconeRepliCarteJeuEnCours.Text = _carteJeuEnCoursRepliee ? "+" : "-";
        BoutonRepliCarteJeuEnCours.ToolTip = _carteJeuEnCoursRepliee
            ? "Déplier la section"
            : "Replier la section";
    }

    private async void ActualisationApi_Tick(object? sender, EventArgs e)
    {
        if (!ConfigurationConnexionEstComplete())
        {
            ArreterActualisationAutomatique();
            return;
        }

        if (_chargementJeuEnCoursActif)
        {
            return;
        }

        await ChargerJeuEnCoursAsync(false, false);
    }

    /// <summary>
    /// Surveille rapidement la détection locale pour réagir presque instantanément aux changements.
    /// </summary>
    private async void SondeLocale_Tick(object? sender, EventArgs e)
    {
        if (!ConfigurationConnexionEstComplete())
        {
            ArreterActualisationAutomatique();
            return;
        }

        JeuDetecteLocalement? jeuLocal = _sondeJeuLocal.DetecterJeu();

        if (jeuLocal is not null)
        {
            jeuLocal.CheminJeuRetenu = DeterminerCheminJeuLocalRetenu(jeuLocal);
        }

        string signatureJeuLocal = ConstruireSignatureJeuLocal(jeuLocal);

        if (string.Equals(signatureJeuLocal, _signatureDernierJeuLocal, StringComparison.Ordinal))
        {
            return;
        }

        _signatureDernierJeuLocal = signatureJeuLocal;

        if (_chargementJeuEnCoursActif)
        {
            if (jeuLocal is not null)
            {
                AppliquerEtatJeuLocal(jeuLocal);
                _ = SauvegarderJeuLocalAfficheAsync(
                    jeuLocal,
                    ConstruireTexteDetailsJeuLocal(jeuLocal)
                );
            }
            else
            {
                DefinirTitreZoneJeu(false);
            }

            _actualisationApiCibleeEnAttente = true;
            return;
        }

        if (jeuLocal is null)
        {
            ReinitialiserContexteSurveillance();
            await ChargerJeuEnCoursAsync(false, true);
            RedemarrerMinuteurActualisationApi();
            return;
        }

        await AppliquerJeuLocalAsync(jeuLocal);
        await ChargerJeuEnCoursAsync(false, true);
        RedemarrerMinuteurActualisationApi();
    }

    /// <summary>
    /// Active les rafraîchissements périodiques.
    /// </summary>
    private void DemarrerActualisationAutomatique()
    {
        if (!ConfigurationConnexionEstComplete())
        {
            return;
        }

        if (!_minuteurSondeLocale.IsEnabled)
        {
            _minuteurSondeLocale.Start();
        }

        if (_profilUtilisateurAccessible && !_minuteurActualisationApi.IsEnabled)
        {
            _minuteurActualisationApi.Start();
        }
    }

    /// <summary>
    /// Redémarre le minuteur API pour repousser le prochain tick après un rafraîchissement ciblé.
    /// </summary>
    private void RedemarrerMinuteurActualisationApi()
    {
        if (!_profilUtilisateurAccessible)
        {
            return;
        }

        _minuteurActualisationApi.Stop();
        _minuteurActualisationApi.Start();
    }

    /// <summary>
    /// Arrête les rafraîchissements périodiques.
    /// </summary>
    private void ArreterActualisationAutomatique()
    {
        _minuteurActualisationApi.Stop();
        _minuteurSondeLocale.Stop();
    }

    /// <summary>
    /// Affiche immédiatement le jeu local détecté au démarrage pour éviter une bascule visuelle tardive.
    /// </summary>
    private async Task AmorcerEtatJeuLocalAuDemarrageAsync()
    {
        JeuDetecteLocalement? jeuLocal = _sondeJeuLocal.DetecterJeu();

        if (jeuLocal is null)
        {
            _signatureDernierJeuLocal = string.Empty;
            return;
        }

        jeuLocal.CheminJeuRetenu = DeterminerCheminJeuLocalRetenu(jeuLocal);
        _signatureDernierJeuLocal = ConstruireSignatureJeuLocal(jeuLocal);
        await AppliquerJeuLocalAsync(jeuLocal);
    }

    /// <summary>
    /// Charge les données réelles du profil, du jeu en cours et des derniers succès.
    /// </summary>
    private async Task ChargerJeuEnCoursAsync(
        bool afficherEtatChargement = true,
        bool forcerChargementJeu = true
    )
    {
        if (!ConfigurationConnexionEstComplete())
        {
            ReinitialiserJeuEnCours();
            return;
        }

        if (_chargementJeuEnCoursActif)
        {
            return;
        }

        _chargementJeuEnCoursActif = true;

        try
        {
            bool afficherShimmer = afficherEtatChargement && _dernierIdentifiantJeuAvecInfos <= 0;
            DefinirChargementVisuel(afficherShimmer);

            if (afficherEtatChargement)
            {
                ReinitialiserPremierSuccesNonDebloque();
                ReinitialiserGrilleTousSucces();
                DefinirTempsJeuSousImage(string.Empty);
                DefinirEtatJeuDansProgression(string.Empty);
                ReinitialiserSuccesRecents();
            }

            ProfilUtilisateurRetroAchievements profil =
                await _clientRetroAchievements.ObtenirProfilUtilisateurAsync(
                    _configurationConnexion.Pseudo,
                    _configurationConnexion.CleApiWeb
                );

            _profilUtilisateurAccessible = true;
            _dernierProfilUtilisateurCharge = profil;
            _dernierResumeUtilisateurCharge = null;
            DefinirEtatConnexion("Connecté");
            await AppliquerProfilUtilisateurAsync(profil, forcerChargementJeu);
            await ChargerSuccesRecentsAsync(profil);
        }
        catch (UtilisateurRetroAchievementsInaccessibleException exception)
        {
            _profilUtilisateurAccessible = false;
            _dernierProfilUtilisateurCharge = null;
            _dernierResumeUtilisateurCharge = null;
            _minuteurActualisationApi.Stop();
            ReinitialiserContexteSurveillance();
            DefinirEtatConnexion("Profil inaccessible");

            JeuDetecteLocalement? jeuLocal = _sondeJeuLocal.DetecterJeu();

            if (jeuLocal is not null)
            {
                await AppliquerJeuLocalAsync(jeuLocal, exception.Message);
            }
            else
            {
                ReinitialiserMetaConsoleJeuEnCours();
                ReinitialiserPremierSuccesNonDebloque();
                ReinitialiserGrilleTousSucces();
                DefinirTempsJeuSousImage(string.Empty);
                DefinirEtatJeuDansProgression(string.Empty);
                DefinirTitreJeuEnCours(string.Empty);
                DefinirDetailsJeuEnCours(string.Empty);
                TexteResumeProgressionJeuEnCours.Text = "-- / --";
                TextePourcentageJeuEnCours.Text = "Progression du jeu indisponible";
                BarreProgressionJeuEnCours.Value = 0;
                ReinitialiserSuccesRecents();
                TexteEtatSuccesRecents.Text =
                    "Les succès récents ne peuvent pas être chargés pour ce compte.";
                TextePourcentageJeuEnCours.Text = "Progression du jeu indisponible";
                TexteEtatSuccesRecents.Text =
                    "Les succès récents ne peuvent pas être chargés pour ce compte.";
            }
        }
        catch (Exception exception)
        {
            _dernierProfilUtilisateurCharge = null;
            _dernierResumeUtilisateurCharge = null;
            DefinirEtatConnexion("Hors ligne ou erreur API");
            JeuDetecteLocalement? jeuLocal = _sondeJeuLocal.DetecterJeu();

            if (jeuLocal is not null)
            {
                await AppliquerJeuLocalAsync(jeuLocal, exception.Message);
            }
            else if (afficherEtatChargement)
            {
                ReinitialiserMetaConsoleJeuEnCours();
                ReinitialiserPremierSuccesNonDebloque();
                ReinitialiserGrilleTousSucces();
                DefinirTempsJeuSousImage(string.Empty);
                DefinirEtatJeuDansProgression(string.Empty);
                DefinirTitreJeuEnCours(string.Empty);
                DefinirDetailsJeuEnCours(string.Empty);
                TexteResumeProgressionJeuEnCours.Text = "-- / --";
                TextePourcentageJeuEnCours.Text = "Progression du jeu indisponible";
                BarreProgressionJeuEnCours.Value = 0;
                ReinitialiserSuccesRecents();
            }
        }
        finally
        {
            _chargementJeuEnCoursActif = false;
            DefinirChargementVisuel(false);

            if (_actualisationApiCibleeEnAttente && ConfigurationConnexionEstComplete())
            {
                _actualisationApiCibleeEnAttente = false;
                _ = Dispatcher.InvokeAsync(async () =>
                {
                    await ChargerJeuEnCoursAsync(false, true);
                    RedemarrerMinuteurActualisationApi();
                });
            }
        }
    }

    /// <summary>
    /// Applique les données du profil utilisateur dans la section "Jeu en cours".
    /// </summary>
    private async Task AppliquerProfilUtilisateurAsync(
        ProfilUtilisateurRetroAchievements profil,
        bool forcerChargementJeu
    )
    {
        JeuDetecteLocalement? jeuLocalDetecte = _sondeJeuLocal.DetecterJeu();
        bool emulateurLocalDetecte = jeuLocalDetecte is not null;
        DefinirTitreZoneJeu(emulateurLocalDetecte);
        string messagePresence = string.IsNullOrWhiteSpace(profil.MessagePresenceRiche)
            ? "Aucune activité en cours."
            : profil.MessagePresenceRiche;
        int identifiantJeuEffectif = profil.IdentifiantDernierJeu;
        JeuRecemmentJoueRetroAchievements? dernierJeuJoue = null;
        JeuSystemeRetroAchievements? jeuResoluParHash = null;
        string titreJeuProvisoire;

        if (jeuLocalDetecte is not null)
        {
            jeuResoluParHash = await ResoudreJeuRetroAchievementsParHashAsync(jeuLocalDetecte);

            if (jeuResoluParHash is not null)
            {
                identifiantJeuEffectif = jeuResoluParHash.IdentifiantJeu;
            }
        }

        if (!emulateurLocalDetecte && identifiantJeuEffectif <= 0)
        {
            dernierJeuJoue = await ObtenirDernierJeuJoueAsync();
            identifiantJeuEffectif = dernierJeuJoue?.IdentifiantJeu ?? 0;
        }

        if (identifiantJeuEffectif <= 0)
        {
            JeuDetecteLocalement? jeuLocal = _sondeJeuLocal.DetecterJeu();

            if (jeuLocal is not null)
            {
                await AppliquerJeuLocalAsync(jeuLocal);
                return;
            }

            ReinitialiserMetaConsoleJeuEnCours();
            ReinitialiserCarrouselVisuelsJeuEnCours();
            ReinitialiserPremierSuccesNonDebloque();
            ReinitialiserGrilleTousSucces();
            DefinirTempsJeuSousImage(string.Empty);
            DefinirEtatJeuDansProgression(string.Empty);
            DefinirTitreJeuEnCours(string.Empty);
            DefinirDetailsJeuEnCours(string.Empty);
            TexteResumeProgressionJeuEnCours.Text = "-- / --";
            TextePourcentageJeuEnCours.Text = "Aucun jeu pour afficher une progression";
            BarreProgressionJeuEnCours.Value = 0;
            return;
        }

        titreJeuProvisoire = !string.IsNullOrWhiteSpace(jeuResoluParHash?.Titre)
            ? jeuResoluParHash.Titre
            : DeterminerTitreJeuApiProvisoire(profil.NomDernierJeu, dernierJeuJoue?.Titre);
        string titreAffichageInitial = string.IsNullOrWhiteSpace(_dernierTitreJeuApi)
            ? titreJeuProvisoire
            : _dernierTitreJeuApi;
        bool infosJeuDejaAfficheesPourCeJeu = PeutConserverInfosJeuAffichees(
            identifiantJeuEffectif
        );

        if (!infosJeuDejaAfficheesPourCeJeu)
        {
            DefinirDetailsJeuEnCours(string.Empty);
            DefinirTempsJeuSousImage(string.Empty);
            DefinirEtatJeuDansProgression(string.Empty);
        }

        bool progressionDejaAfficheePourCeJeu = PeutConserverProgressionAffichee(
            identifiantJeuEffectif
        );

        if (!infosJeuDejaAfficheesPourCeJeu)
        {
            DefinirTitreJeuEnCours(titreAffichageInitial);
        }

        if (!progressionDejaAfficheePourCeJeu)
        {
            TexteResumeProgressionJeuEnCours.Text = "-- / --";
            TextePourcentageJeuEnCours.Text = "Progression du jeu en cours de chargement";
            BarreProgressionJeuEnCours.Value = 0;
        }

        bool contexteApiInchange =
            !forcerChargementJeu
            && _dernierIdentifiantJeuApi == identifiantJeuEffectif
            && string.Equals(_dernierePresenceRiche, messagePresence, StringComparison.Ordinal)
            && string.Equals(_dernierPseudoCharge, profil.NomUtilisateur, StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(_dernierTitreJeuApi);

        _dernierIdentifiantJeuApi = identifiantJeuEffectif;
        _dernierePresenceRiche = messagePresence;
        _dernierPseudoCharge = profil.NomUtilisateur;

        if (contexteApiInchange)
        {
            DefinirTitreJeuEnCours(_dernierTitreJeuApi);
            return;
        }

        try
        {
            JeuUtilisateurRetroAchievements jeu =
                await _clientRetroAchievements.ObtenirJeuEtProgressionUtilisateurAsync(
                    _configurationConnexion.Pseudo,
                    _configurationConnexion.CleApiWeb,
                    identifiantJeuEffectif
                );

            await AppliquerProgressionJeuAsync(jeu);
            await CompleterValidationLocaleJeuAsync(jeu);
        }
        catch
        {
            if (!infosJeuDejaAfficheesPourCeJeu)
            {
                DefinirTitreJeuEnCours(titreJeuProvisoire);
                DefinirDetailsJeuEnCours(string.Empty);
                DefinirTempsJeuSousImage(string.Empty);
                DefinirEtatJeuDansProgression(string.Empty);
            }

            if (!progressionDejaAfficheePourCeJeu)
            {
                TexteResumeProgressionJeuEnCours.Text = "-- / --";
                TextePourcentageJeuEnCours.Text = "Progression du jeu indisponible";
                BarreProgressionJeuEnCours.Value = 0;
            }
        }
    }

    /// <summary>
    /// Charge les derniers succès débloqués et privilégie ceux du jeu en cours si possible.
    /// </summary>
    private async Task ChargerSuccesRecentsAsync(ProfilUtilisateurRetroAchievements profil)
    {
        try
        {
            DateTimeOffset maintenant = DateTimeOffset.UtcNow;
            IReadOnlyList<SuccesRecentRetroAchievements> succesRecents =
                await _clientRetroAchievements.ObtenirSuccesDebloquesEntreAsync(
                    _configurationConnexion.Pseudo,
                    _configurationConnexion.CleApiWeb,
                    maintenant.AddDays(-7),
                    maintenant
                );

            IEnumerable<SuccesRecentRetroAchievements> succesTries =
                succesRecents.OrderByDescending(succes =>
                    ConvertirDateSucces(succes.DateDeblocage)
                );

            if (profil.IdentifiantDernierJeu > 0)
            {
                List<SuccesRecentRetroAchievements> succesJeuEnCours =
                [
                    .. succesTries.Where(succes =>
                        succes.IdentifiantJeu == profil.IdentifiantDernierJeu
                    ),
                ];

                if (succesJeuEnCours.Count > 0)
                {
                    AppliquerSuccesRecents(
                        [.. succesJeuEnCours.Take(3)],
                        $"Affichage des {Math.Min(3, succesJeuEnCours.Count)} derniers succès du jeu en cours."
                    );
                    return;
                }
            }

            List<SuccesRecentRetroAchievements> succesAffiches = [.. succesTries.Take(3)];

            if (succesAffiches.Count == 0)
            {
                ReinitialiserSuccesRecents();
                TexteEtatSuccesRecents.Text =
                    "Aucun succès récent n'a été détecté sur les 7 derniers jours.";
                return;
            }

            AppliquerSuccesRecents(
                succesAffiches,
                $"Affichage des {succesAffiches.Count} derniers succès connus."
            );
        }
        catch
        {
            ReinitialiserSuccesRecents();
            TexteEtatSuccesRecents.Text = "Impossible de charger les succès récents.";
        }
    }

    /// <summary>
    /// Applique les informations détaillées du jeu et sa progression.
    /// </summary>
    private async Task AppliquerProgressionJeuAsync(JeuUtilisateurRetroAchievements jeu)
    {
        _dernierTitreJeuApi = jeu.Titre;
        _dernierIdentifiantJeuAvecInfos = jeu.IdentifiantJeu;
        _dernierIdentifiantJeuAvecProgression = jeu.IdentifiantJeu;
        await MettreAJourVisuelsJeuEnCoursAsync(jeu);
        await MettreAJourMetaConsoleJeuEnCoursAsync(jeu);

        string detailsTempsJeu = string.Empty;
        string detailsRecompense = DeterminerStatutJeu(jeu);

        DefinirTempsJeuSousImage(detailsTempsJeu);
        DefinirEtatJeuDansProgression(detailsRecompense);
        DefinirDetailsJeuEnCours(string.Empty);
        await MettreAJourSuccesJeuAsync(jeu);

        TexteResumeProgressionJeuEnCours.Text = $"{jeu.NombreSuccesObtenus} / {jeu.NombreSucces}";
        TextePourcentageJeuEnCours.Text = NormaliserPourcentage(jeu.CompletionUtilisateur);
        BarreProgressionJeuEnCours.Value = ExtrairePourcentage(jeu.CompletionUtilisateur);

        await SauvegarderDernierJeuAfficheAsync(jeu, detailsTempsJeu, detailsRecompense);
    }

    /// <summary>
    /// Compare le fichier local détecté avec les hashes officiels RetroAchievements du jeu courant.
    /// </summary>
    private async Task CompleterValidationLocaleJeuAsync(JeuUtilisateurRetroAchievements jeu)
    {
        JeuDetecteLocalement? jeuLocal = _sondeJeuLocal.DetecterJeu();

        if (jeuLocal is null)
        {
            return;
        }

        jeuLocal.CheminJeuRetenu = DeterminerCheminJeuLocalRetenu(jeuLocal);
        jeuLocal.EmpreinteLocale = await _serviceHachageJeuLocal.CalculerEmpreinteAsync(
            jeuLocal.CheminJeuRetenu
        );

        if (jeuLocal.EmpreinteLocale is null)
        {
            return;
        }

        try
        {
            IReadOnlyList<HashJeuRetroAchievements> hashesOfficiels =
                await _clientRetroAchievements.ObtenirHashesJeuAsync(
                    _configurationConnexion.CleApiWeb,
                    jeu.IdentifiantJeu
                );

            bool hashReconnu = hashesOfficiels.Any(hash =>
                hash.EmpreinteMd5.Equals(
                    jeuLocal.EmpreinteLocale.EmpreinteMd5,
                    StringComparison.OrdinalIgnoreCase
                )
            );

            string detailsValidation =
                $"\nValidation locale : {(hashReconnu ? "MD5 reconnu" : "MD5 non reconnu")}"
                + $"\nMD5 local : {jeuLocal.EmpreinteLocale.EmpreinteMd5}"
                + $"\nSHA-1 local : {jeuLocal.EmpreinteLocale.EmpreinteSha1}";

            if (!string.IsNullOrWhiteSpace(jeuLocal.CheminJeuRetenu))
            {
                detailsValidation += $"\nFichier local : {jeuLocal.CheminJeuRetenu}";
            }

            if (
                !TexteDetailsJeuEnCours.Text.Contains(
                    "Validation locale :",
                    StringComparison.Ordinal
                )
            )
            {
                TexteDetailsJeuEnCours.Text += detailsValidation;
            }
        }
        catch
        {
            if (
                !TexteDetailsJeuEnCours.Text.Contains(
                    "Validation locale :",
                    StringComparison.Ordinal
                )
            )
            {
                TexteDetailsJeuEnCours.Text +=
                    "\nValidation locale : impossible de comparer avec les hashes officiels.";
            }
        }
    }

    /// <summary>
    /// Récupère un identifiant de dernier jeu joué via la liste des jeux récemment joués.
    /// </summary>
    private async Task<JeuRecemmentJoueRetroAchievements?> ObtenirDernierJeuJoueAsync()
    {
        try
        {
            IReadOnlyList<JeuRecemmentJoueRetroAchievements> jeuxRecents =
                await _clientRetroAchievements.ObtenirJeuxRecemmentJouesAsync(
                    _configurationConnexion.Pseudo,
                    _configurationConnexion.CleApiWeb
                );

            return jeuxRecents.Count > 0 ? jeuxRecents[0] : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Tente de résoudre un jeu RetroAchievements à partir du hash du fichier local détecté.
    /// </summary>
    private async Task<JeuSystemeRetroAchievements?> ResoudreJeuRetroAchievementsParHashAsync(
        JeuDetecteLocalement jeuLocal
    )
    {
        if (!ConfigurationConnexionEstComplete())
        {
            return null;
        }

        jeuLocal.CheminJeuRetenu = DeterminerCheminJeuLocalRetenu(jeuLocal);
        jeuLocal.EmpreinteLocale ??= await _serviceHachageJeuLocal.CalculerEmpreinteAsync(
            jeuLocal.CheminJeuRetenu
        );

        if (
            jeuLocal.EmpreinteLocale is null
            || string.IsNullOrWhiteSpace(jeuLocal.EmpreinteLocale.EmpreinteMd5)
        )
        {
            return null;
        }

        IReadOnlyList<int> consolesCandidates = await DeterminerConsolesCandidatesAsync(jeuLocal);

        foreach (int identifiantConsole in consolesCandidates)
        {
            IReadOnlyList<JeuSystemeRetroAchievements> jeuxSysteme =
                await _clientRetroAchievements.ObtenirJeuxSystemeAvecHashesAsync(
                    _configurationConnexion.CleApiWeb,
                    identifiantConsole
                );

            JeuSystemeRetroAchievements? jeuTrouve = jeuxSysteme.FirstOrDefault(jeu =>
                jeu.Hashes.Any(hash =>
                    hash.Equals(
                        jeuLocal.EmpreinteLocale.EmpreinteMd5,
                        StringComparison.OrdinalIgnoreCase
                    )
                )
            );

            if (jeuTrouve is not null)
            {
                return jeuTrouve;
            }
        }

        return null;
    }

    /// <summary>
    /// Détermine les consoles RetroAchievements les plus plausibles pour le jeu local détecté.
    /// </summary>
    private async Task<IReadOnlyList<int>> DeterminerConsolesCandidatesAsync(
        JeuDetecteLocalement jeuLocal
    )
    {
        IReadOnlyList<ConsoleRetroAchievements> consoles =
            await _clientRetroAchievements.ObtenirConsolesAsync(_configurationConnexion.CleApiWeb);
        List<string> nomsCandidats = DeterminerNomsConsolesCandidates(jeuLocal);
        List<int> identifiants = [];

        foreach (string nomCandidat in nomsCandidats)
        {
            foreach (ConsoleRetroAchievements console in consoles)
            {
                if (
                    !CorrespondConsole(console.Nom, nomCandidat)
                    || identifiants.Contains(console.IdentifiantConsole)
                )
                {
                    continue;
                }

                identifiants.Add(console.IdentifiantConsole);
            }
        }

        return identifiants;
    }

    /// <summary>
    /// Construit une liste ordonnée de consoles candidates à partir de l'émulateur et de l'extension du fichier.
    /// </summary>
    private static List<string> DeterminerNomsConsolesCandidates(JeuDetecteLocalement jeuLocal)
    {
        List<string> noms = [];

        void Ajouter(string nomConsole)
        {
            if (
                string.IsNullOrWhiteSpace(nomConsole)
                || noms.Any(nom =>
                    string.Equals(nom, nomConsole, StringComparison.OrdinalIgnoreCase)
                )
            )
            {
                return;
            }

            noms.Add(nomConsole);
        }

        switch (jeuLocal.NomEmulateur)
        {
            case "Luna's Project64":
                Ajouter("Nintendo 64");
                break;
            case "DuckStation":
                Ajouter("PlayStation");
                break;
            case "PCSX2":
                Ajouter("PlayStation 2");
                break;
            case "PPSSPP":
                Ajouter("PSP");
                break;
            case "Flycast":
                Ajouter("Dreamcast");
                break;
            case "Dolphin":
                Ajouter("GameCube");
                Ajouter("Wii");
                break;
        }

        AjouterConsolesDepuisIndicesCore(jeuLocal, Ajouter);

        string extension = Path.GetExtension(
                !string.IsNullOrWhiteSpace(jeuLocal.CheminJeuRetenu)
                    ? jeuLocal.CheminJeuRetenu
                    : jeuLocal.CheminJeuLigneCommande
            )
            .Trim()
            .ToLowerInvariant();

        switch (extension)
        {
            case ".nes":
            case ".fds":
                Ajouter("Nintendo Entertainment System");
                break;
            case ".sfc":
            case ".smc":
                Ajouter("Super Nintendo Entertainment System");
                break;
            case ".gb":
                Ajouter("Game Boy");
                break;
            case ".gbc":
                Ajouter("Game Boy Color");
                break;
            case ".gba":
                Ajouter("Game Boy Advance");
                break;
            case ".nds":
                Ajouter("Nintendo DS");
                break;
            case ".n64":
            case ".z64":
            case ".v64":
                Ajouter("Nintendo 64");
                break;
            case ".gen":
            case ".md":
                Ajouter("Mega Drive");
                break;
            case ".gg":
                Ajouter("Game Gear");
                break;
            case ".sms":
                Ajouter("Master System");
                break;
            case ".pce":
                Ajouter("PC Engine");
                break;
            case ".sgx":
                Ajouter("SuperGrafx");
                Ajouter("PC Engine");
                break;
            case ".a26":
                Ajouter("Atari 2600");
                break;
            case ".a78":
                Ajouter("Atari 7800");
                break;
            case ".lnx":
                Ajouter("Atari Lynx");
                break;
            case ".ngp":
                Ajouter("Neo Geo Pocket");
                break;
            case ".ngc":
                Ajouter("Neo Geo Pocket Color");
                break;
            case ".vb":
                Ajouter("Virtual Boy");
                break;
            case ".ws":
                Ajouter("WonderSwan");
                break;
            case ".wsc":
                Ajouter("WonderSwan Color");
                break;
            case ".32x":
                Ajouter("Sega 32X");
                break;
            case ".sg":
                Ajouter("SG-1000");
                break;
            case ".col":
                Ajouter("ColecoVision");
                break;
            case ".adf":
            case ".dms":
            case ".ipf":
                Ajouter("Amiga");
                break;
            case ".rom":
                Ajouter("MSX");
                Ajouter("MSX2");
                break;
            case ".vec":
                Ajouter("Vectrex");
                break;
            case ".j64":
            case ".jag":
                Ajouter("Jaguar");
                break;
            case ".gdi":
                Ajouter("Dreamcast");
                break;
            case ".cso":
            case ".pbp":
                Ajouter("PSP");
                break;
            case ".cue":
            case ".bin":
            case ".img":
            case ".iso":
            case ".chd":
                AjouterDepuisSupportOptique(jeuLocal, Ajouter);
                break;
            case ".rvz":
            case ".wbfs":
                Ajouter("GameCube");
                Ajouter("Wii");
                break;
        }

        return noms;
    }

    /// <summary>
    /// Ajoute des consoles candidates à partir d'indices de core ou de moteur dans la ligne de commande.
    /// </summary>
    private static void AjouterConsolesDepuisIndicesCore(
        JeuDetecteLocalement jeuLocal,
        Action<string> ajouter
    )
    {
        string indices = string.Join(
                " ",
                jeuLocal.NomEmulateur,
                jeuLocal.NomProcessus,
                jeuLocal.LigneCommande,
                jeuLocal.CheminExecutable,
                jeuLocal.TitreFenetre
            )
            .ToLowerInvariant();

        AjouterSiContient(indices, ajouter, "mesen", "Nintendo Entertainment System");
        AjouterSiContient(indices, ajouter, "mesen-s", "Super Nintendo Entertainment System");
        AjouterSiContient(indices, ajouter, "snes9x", "Super Nintendo Entertainment System");
        AjouterSiContient(indices, ajouter, "bsnes", "Super Nintendo Entertainment System");
        AjouterSiContient(indices, ajouter, "mupen64plus", "Nintendo 64");
        AjouterSiContient(indices, ajouter, "parallel_n64", "Nintendo 64");
        AjouterSiContient(indices, ajouter, "sameboy", "Game Boy");
        AjouterSiContient(indices, ajouter, "sameboy", "Game Boy Color");
        AjouterSiContient(indices, ajouter, "gambatte", "Game Boy");
        AjouterSiContient(indices, ajouter, "gambatte", "Game Boy Color");
        AjouterSiContient(indices, ajouter, "gearboy", "Game Boy");
        AjouterSiContient(indices, ajouter, "gearboy", "Game Boy Color");
        AjouterSiContient(indices, ajouter, "mgba", "Game Boy Advance");
        AjouterSiContient(indices, ajouter, "melonds", "Nintendo DS");
        AjouterSiContient(indices, ajouter, "desmume", "Nintendo DS");
        AjouterSiContient(indices, ajouter, "genesis_plus_gx", "Mega Drive");
        AjouterSiContient(indices, ajouter, "genesis_plus_gx", "Master System");
        AjouterSiContient(indices, ajouter, "genesis_plus_gx", "Game Gear");
        AjouterSiContient(indices, ajouter, "genesis_plus_gx", "SG-1000");
        AjouterSiContient(indices, ajouter, "picodrive", "Mega Drive");
        AjouterSiContient(indices, ajouter, "picodrive", "Sega 32X");
        AjouterSiContient(indices, ajouter, "flycast", "Dreamcast");
        AjouterSiContient(indices, ajouter, "swanstation", "PlayStation");
        AjouterSiContient(indices, ajouter, "beetle_psx", "PlayStation");
        AjouterSiContient(indices, ajouter, "pcsx_rearmed", "PlayStation");
        AjouterSiContient(indices, ajouter, "duckstation", "PlayStation");
        AjouterSiContient(indices, ajouter, "pcsx2", "PlayStation 2");
        AjouterSiContient(indices, ajouter, "ppsspp", "PSP");
        AjouterSiContient(indices, ajouter, "dolphin", "GameCube");
        AjouterSiContient(indices, ajouter, "dolphin", "Wii");
        AjouterSiContient(indices, ajouter, "pce_fast", "PC Engine");
        AjouterSiContient(indices, ajouter, "mednafen_pce", "PC Engine");
        AjouterSiContient(indices, ajouter, "mednafen_supergrafx", "SuperGrafx");
        AjouterSiContient(indices, ajouter, "mednafen_wswan", "WonderSwan");
        AjouterSiContient(indices, ajouter, "mednafen_wswan", "WonderSwan Color");
        AjouterSiContient(indices, ajouter, "virtualjaguar", "Jaguar");
        AjouterSiContient(indices, ajouter, "prosystem", "Atari 7800");
        AjouterSiContient(indices, ajouter, "stella", "Atari 2600");
        AjouterSiContient(indices, ajouter, "handy", "Atari Lynx");
        AjouterSiContient(indices, ajouter, "vb", "Virtual Boy");
        AjouterSiContient(indices, ajouter, "opera", "3DO Interactive Multiplayer");
        AjouterSiContient(indices, ajouter, "fbneo", "Arcade");
        AjouterSiContient(indices, ajouter, "mame", "Arcade");
        AjouterSiContient(indices, ajouter, "fceumm", "Nintendo Entertainment System");
        AjouterSiContient(indices, ajouter, "nestopia", "Nintendo Entertainment System");
    }

    /// <summary>
    /// Ajoute une console si un indice de chaîne est trouvé.
    /// </summary>
    private static void AjouterSiContient(
        string contenu,
        Action<string> ajouter,
        string indice,
        string nomConsole
    )
    {
        if (contenu.Contains(indice, StringComparison.Ordinal))
        {
            ajouter(nomConsole);
        }
    }

    /// <summary>
    /// Affine les supports optiques selon l'émulateur ou les indices disponibles.
    /// </summary>
    private static void AjouterDepuisSupportOptique(
        JeuDetecteLocalement jeuLocal,
        Action<string> ajouter
    )
    {
        switch (jeuLocal.NomEmulateur)
        {
            case "DuckStation":
                ajouter("PlayStation");
                return;
            case "PCSX2":
                ajouter("PlayStation 2");
                return;
            case "Flycast":
                ajouter("Dreamcast");
                return;
            case "Dolphin":
                ajouter("GameCube");
                ajouter("Wii");
                return;
            case "PPSSPP":
                ajouter("PSP");
                return;
        }

        string indices = string.Join(
                " ",
                jeuLocal.NomEmulateur,
                jeuLocal.NomProcessus,
                jeuLocal.LigneCommande,
                jeuLocal.CheminExecutable
            )
            .ToLowerInvariant();

        if (indices.Contains("saturn", StringComparison.Ordinal))
        {
            ajouter("Sega Saturn");
        }

        if (
            indices.Contains("segacd", StringComparison.Ordinal)
            || indices.Contains("mega cd", StringComparison.Ordinal)
        )
        {
            ajouter("Mega CD");
        }

        if (
            indices.Contains("pce", StringComparison.Ordinal)
            || indices.Contains("pcengine", StringComparison.Ordinal)
        )
        {
            ajouter("PC Engine CD");
            ajouter("PC Engine");
        }

        if (
            indices.Contains("playstation", StringComparison.Ordinal)
            || indices.Contains("psx", StringComparison.Ordinal)
        )
        {
            ajouter("PlayStation");
        }

        if (indices.Contains("ps2", StringComparison.Ordinal))
        {
            ajouter("PlayStation 2");
        }

        if (indices.Contains("dreamcast", StringComparison.Ordinal))
        {
            ajouter("Dreamcast");
        }
    }

    /// <summary>
    /// Compare souplement un nom de console API avec un nom candidat local.
    /// </summary>
    private static bool CorrespondConsole(string nomApi, string nomCandidat)
    {
        if (string.IsNullOrWhiteSpace(nomApi) || string.IsNullOrWhiteSpace(nomCandidat))
        {
            return false;
        }

        string cleApi = NormaliserCleComparaisonJeu(nomApi);
        string cleCandidate = NormaliserCleComparaisonJeu(nomCandidat);

        return string.Equals(cleApi, cleCandidate, StringComparison.Ordinal)
            || cleApi.Contains(cleCandidate, StringComparison.Ordinal)
            || cleCandidate.Contains(cleApi, StringComparison.Ordinal);
    }

    /// <summary>
    /// Applique un jeu estimé localement à partir d'un émulateur détecté sur la machine.
    /// </summary>
    private static string DeterminerTitreJeuApiProvisoire(
        string nomDernierJeuProfil,
        string? titreDernierJeuRecent
    )
    {
        if (!string.IsNullOrWhiteSpace(nomDernierJeuProfil))
        {
            return nomDernierJeuProfil;
        }

        if (!string.IsNullOrWhiteSpace(titreDernierJeuRecent))
        {
            return titreDernierJeuRecent;
        }

        return string.Empty;
    }

    private async Task AppliquerJeuLocalAsync(
        JeuDetecteLocalement jeuLocal,
        string? raisonIndisponibiliteApi = null
    )
    {
        jeuLocal.CheminJeuRetenu = DeterminerCheminJeuLocalRetenu(jeuLocal);
        string signatureJeuLocal = ConstruireSignatureJeuLocal(jeuLocal);
        JeuSystemeRetroAchievements? jeuResoluParHash =
            await ResoudreJeuRetroAchievementsParHashAsync(jeuLocal);

        if (jeuResoluParHash is not null)
        {
            jeuLocal.IdentifiantJeuRetroAchievements = jeuResoluParHash.IdentifiantJeu;
            jeuLocal.TitreJeuRetroAchievements = jeuResoluParHash.Titre;
            jeuLocal.NomConsoleRetroAchievements = jeuResoluParHash.NomConsole;
            jeuLocal.TitreJeuEstime = jeuResoluParHash.Titre;
        }

        AppliquerEtatJeuLocal(jeuLocal, raisonIndisponibiliteApi);
        await SauvegarderJeuLocalAfficheAsync(
            jeuLocal,
            ConstruireTexteDetailsJeuLocal(jeuLocal, raisonIndisponibiliteApi)
        );
        await Dispatcher.Yield(DispatcherPriority.Background);

        jeuLocal.EmpreinteLocale = await _serviceHachageJeuLocal.CalculerEmpreinteAsync(
            jeuLocal.CheminJeuRetenu
        );

        if (
            !string.Equals(_signatureDernierJeuLocal, signatureJeuLocal, StringComparison.Ordinal)
            || jeuLocal.EmpreinteLocale is null
        )
        {
            return;
        }

        string detailsJeuLocal = ConstruireTexteDetailsJeuLocal(jeuLocal, raisonIndisponibiliteApi);
        DefinirDetailsJeuEnCours(detailsJeuLocal);
        await SauvegarderJeuLocalAfficheAsync(jeuLocal, detailsJeuLocal);
    }

    private void AppliquerEtatJeuLocal(
        JeuDetecteLocalement jeuLocal,
        string? raisonIndisponibiliteApi = null
    )
    {
        bool conserverInformationsJeuAffiche = JeuLocalCorrespondAuJeuAffiche(jeuLocal);
        DefinirTitreZoneJeu(true);

        if (!conserverInformationsJeuAffiche)
        {
            ReinitialiserImageJeuEnCours();
            ReinitialiserMetaConsoleJeuEnCours();
            DefinirTempsJeuSousImage(string.Empty);
            DefinirEtatJeuDansProgression(string.Empty);
        }

        DefinirTitreJeuEnCours(
            string.IsNullOrWhiteSpace(jeuLocal.TitreJeuEstime)
                ? string.Empty
                : jeuLocal.TitreJeuEstime
        );
        DefinirDetailsJeuEnCours(
            ConstruireTexteDetailsJeuLocal(jeuLocal, raisonIndisponibiliteApi)
        );
        TexteResumeProgressionJeuEnCours.Text = "-- / --";
        TextePourcentageJeuEnCours.Text = "Progression du jeu indisponible localement";
        BarreProgressionJeuEnCours.Value = 0;
    }

    private bool JeuLocalCorrespondAuJeuAffiche(JeuDetecteLocalement jeuLocal)
    {
        string titreLocal = NormaliserCleComparaisonJeu(jeuLocal.TitreJeuEstime);
        string titreAffiche = NormaliserCleComparaisonJeu(TexteTitreJeuEnCours.Text);
        string titreApi = NormaliserCleComparaisonJeu(_dernierTitreJeuApi);

        if (string.IsNullOrWhiteSpace(titreLocal))
        {
            return false;
        }

        return CorrespondanceSoupleJeu(titreLocal, titreAffiche)
            || CorrespondanceSoupleJeu(titreLocal, titreApi);
    }

    private static bool CorrespondanceSoupleJeu(string titreA, string titreB)
    {
        if (string.IsNullOrWhiteSpace(titreA) || string.IsNullOrWhiteSpace(titreB))
        {
            return false;
        }

        return string.Equals(titreA, titreB, StringComparison.Ordinal)
            || titreA.Contains(titreB, StringComparison.Ordinal)
            || titreB.Contains(titreA, StringComparison.Ordinal);
    }

    private static string NormaliserCleComparaisonJeu(string valeur)
    {
        if (string.IsNullOrWhiteSpace(valeur))
        {
            return string.Empty;
        }

        char[] caracteres = [.. valeur.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant)];

        return new string(caracteres);
    }

    private static string ConstruireTexteDetailsJeuLocal(
        JeuDetecteLocalement jeuLocal,
        string? raisonIndisponibiliteApi = null
    )
    {
        string detailsApi = string.IsNullOrWhiteSpace(raisonIndisponibiliteApi)
            ? "En attente d'une confirmation par l'API RetroAchievements."
            : $"API indisponible : {raisonIndisponibiliteApi}";
        string detailsJeu = string.IsNullOrWhiteSpace(jeuLocal.CheminJeuEstime)
            ? string.Empty
            : $"\nJeu estimé : {jeuLocal.CheminJeuEstime}";
        string detailsJeuLigneCommande = string.IsNullOrWhiteSpace(jeuLocal.CheminJeuLigneCommande)
            ? string.Empty
            : $"\nJeu ligne de commande : {jeuLocal.CheminJeuLigneCommande}";
        string detailsJeuRetenu = string.IsNullOrWhiteSpace(jeuLocal.CheminJeuRetenu)
            ? string.Empty
            : $"\nJeu retenu : {jeuLocal.CheminJeuRetenu}";
        string detailsJeuRetroAchievements =
            jeuLocal.IdentifiantJeuRetroAchievements <= 0
                ? string.Empty
                : $"\nCorrespondance hash : {jeuLocal.TitreJeuRetroAchievements} ({jeuLocal.NomConsoleRetroAchievements})";
        string detailsExecutable = string.IsNullOrWhiteSpace(jeuLocal.CheminExecutable)
            ? string.Empty
            : $"\nExécutable : {jeuLocal.CheminExecutable}";
        string detailsEmpreinte = jeuLocal.EmpreinteLocale is null
            ? string.Empty
            : $"\nMD5 : {jeuLocal.EmpreinteLocale.EmpreinteMd5}\nSHA-1 : {jeuLocal.EmpreinteLocale.EmpreinteSha1}\nTaille : {jeuLocal.EmpreinteLocale.TailleOctets} octets";

        return $"Émulateur : {jeuLocal.NomEmulateur}\nProcessus : {jeuLocal.NomProcessus}{detailsJeu}{detailsJeuLigneCommande}{detailsJeuRetenu}{detailsJeuRetroAchievements}{detailsExecutable}{detailsEmpreinte}\n{detailsApi}";
    }

    /// <summary>
    /// Choisit le meilleur chemin local disponible pour l'analyse du jeu détecté.
    /// </summary>
    private static string DeterminerCheminJeuLocalRetenu(JeuDetecteLocalement jeuLocal)
    {
        if (Path.IsPathRooted(jeuLocal.CheminJeuLigneCommande))
        {
            return jeuLocal.CheminJeuLigneCommande;
        }

        if (Path.IsPathRooted(jeuLocal.CheminJeuEstime))
        {
            return jeuLocal.CheminJeuEstime;
        }

        return string.Empty;
    }

    /// <summary>
    /// Construit une signature légère du jeu local détecté pour repérer rapidement les changements.
    /// </summary>
    private static string ConstruireSignatureJeuLocal(JeuDetecteLocalement? jeuLocal)
    {
        if (jeuLocal is null)
        {
            return string.Empty;
        }

        string signatureFichier = ConstruireSignatureFichierJeu(jeuLocal.CheminJeuRetenu);

        return string.Join(
            "|",
            jeuLocal.NomEmulateur,
            jeuLocal.NomProcessus,
            jeuLocal.TitreFenetre,
            jeuLocal.CheminJeuLigneCommande,
            jeuLocal.CheminJeuEstime,
            jeuLocal.CheminJeuRetenu,
            signatureFichier
        );
    }

    /// <summary>
    /// Construit une signature légère de fichier pour repérer un changement de ROM même si le titre reste proche.
    /// </summary>
    private static string ConstruireSignatureFichierJeu(string cheminJeu)
    {
        if (string.IsNullOrWhiteSpace(cheminJeu))
        {
            return string.Empty;
        }

        try
        {
            if (!Path.IsPathRooted(cheminJeu) || !File.Exists(cheminJeu))
            {
                return cheminJeu;
            }

            FileInfo informationsFichier = new(cheminJeu);
            return $"{informationsFichier.FullName}|{informationsFichier.Length}|{informationsFichier.LastWriteTimeUtc.Ticks}";
        }
        catch
        {
            return cheminJeu;
        }
    }

    /// <summary>
    /// Réinitialise la section des succès récents sur un état neutre.
    /// </summary>
    private void ReinitialiserSuccesRecents()
    {
        TexteEtatSuccesRecents.Text = "Les succès récents apparaîtront ici.";
        TexteSuccesRecent1.Text = "Aucun succès chargé.";
        TexteSuccesRecent2.Text = "Aucun succès chargé.";
        TexteSuccesRecent3.Text = "Aucun succès chargé.";
    }

    /// <summary>
    /// Remplit les lignes de la section des succès récents.
    /// </summary>
    private void AppliquerSuccesRecents(
        List<SuccesRecentRetroAchievements> succesRecents,
        string texteEtat
    )
    {
        TexteEtatSuccesRecents.Text = texteEtat;

        string[] lignes =
        [
            "Aucun autre succès récent.",
            "Aucun autre succès récent.",
            "Aucun autre succès récent.",
        ];

        for (int index = 0; index < Math.Min(3, succesRecents.Count); index++)
        {
            lignes[index] = ConstruireLigneSucces(succesRecents[index]);
        }

        TexteSuccesRecent1.Text = lignes[0];
        TexteSuccesRecent2.Text = lignes[1];
        TexteSuccesRecent3.Text = lignes[2];
    }

    /// <summary>
    /// Construit une ligne d'affichage lisible pour un succès récent.
    /// </summary>
    private static string ConstruireLigneSucces(SuccesRecentRetroAchievements succes)
    {
        string mode = succes.ModeHardcore ? "Hardcore" : "Standard";
        DateTimeOffset dateDeblocage = ConvertirDateSucces(succes.DateDeblocage);
        string dateFormatee =
            dateDeblocage == DateTimeOffset.MinValue
                ? "Date inconnue"
                : dateDeblocage.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);
        string titreJeu = string.IsNullOrWhiteSpace(succes.TitreJeu)
            ? "Jeu inconnu"
            : succes.TitreJeu;
        string description = string.IsNullOrWhiteSpace(succes.Description)
            ? string.Empty
            : $"\n{succes.Description}";

        return $"{succes.Titre} - {succes.Points} pts - {mode}\n{titreJeu} - {dateFormatee}{description}";
    }

    /// <summary>
    /// Convertit la date d'un succès récent en horodatage exploitable pour le tri.
    /// </summary>
    private static DateTimeOffset ConvertirDateSucces(string dateApi)
    {
        if (string.IsNullOrWhiteSpace(dateApi))
        {
            return DateTimeOffset.MinValue;
        }

        if (
            DateTimeOffset.TryParse(
                dateApi,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AllowWhiteSpaces,
                out DateTimeOffset dateParsee
            )
        )
        {
            return dateParsee;
        }

        string[] formatsAcceptes =
        [
            "yyyy-MM-dd HH:mm:ss",
            "yyyy-MM-ddTHH:mm:ssK",
            "yyyy-MM-ddTHH:mm:ss.fffK",
        ];

        if (
            DateTimeOffset.TryParseExact(
                dateApi,
                formatsAcceptes,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AllowWhiteSpaces,
                out dateParsee
            )
        )
        {
            return dateParsee;
        }

        if (
            DateTimeOffset.TryParse(
                dateApi,
                CultureInfo.CurrentCulture,
                DateTimeStyles.AllowWhiteSpaces,
                out dateParsee
            )
        )
        {
            return dateParsee;
        }

        return DateTimeOffset.MinValue;
    }

    /// <summary>
    /// Réinitialise la section "Jeu en cours" sur un état neutre.
    /// </summary>
    private void ReinitialiserJeuEnCours()
    {
        DefinirChargementVisuel(false);
        DefinirTitreZoneJeu(true);
        _dernierIdentifiantJeuAvecInfos = 0;
        _dernierIdentifiantJeuAvecProgression = 0;
        ReinitialiserCarrouselVisuelsJeuEnCours();
        ReinitialiserImageJeuEnCours();
        ReinitialiserPremierSuccesNonDebloque();
        ReinitialiserGrilleTousSucces();
        ReinitialiserMetaConsoleJeuEnCours();
        DefinirTempsJeuSousImage(string.Empty);
        DefinirEtatJeuDansProgression(string.Empty);
        DefinirTitreJeuEnCours(string.Empty);
        DefinirDetailsJeuEnCours(string.Empty);
        TexteResumeProgressionJeuEnCours.Text = "-- / --";
        TextePourcentageJeuEnCours.Text = "Aucun jeu pour afficher une progression";
        BarreProgressionJeuEnCours.Value = 0;
        ReinitialiserSuccesRecents();
    }

    /// <summary>
    /// Réinitialise les derniers marqueurs utilisés pour éviter les rechargements API inutiles.
    /// </summary>
    private void ReinitialiserContexteSurveillance()
    {
        _actualisationApiCibleeEnAttente = false;
        _dernierIdentifiantJeuApi = 0;
        _dernierIdentifiantJeuAvecInfos = 0;
        _dernierIdentifiantJeuAvecProgression = 0;
        _dernierTitreJeuApi = string.Empty;
        _dernierePresenceRiche = string.Empty;
        _dernierPseudoCharge = string.Empty;
        _signatureDernierJeuLocal = string.Empty;
        _dernierProfilUtilisateurCharge = null;
        _dernierResumeUtilisateurCharge = null;
    }

    /// <summary>
    /// Indique si la progression affichée peut être conservée pour le même jeu.
    /// </summary>
    private bool PeutConserverProgressionAffichee(int identifiantJeu)
    {
        return identifiantJeu > 0 && _dernierIdentifiantJeuAvecProgression == identifiantJeu;
    }

    /// <summary>
    /// Indique si les informations visibles du jeu peuvent être conservées pour le même jeu.
    /// </summary>
    private bool PeutConserverInfosJeuAffichees(int identifiantJeu)
    {
        return identifiantJeu > 0 && _dernierIdentifiantJeuAvecInfos == identifiantJeu;
    }

    /// <summary>
    /// Extrait l'année de sortie d'un jeu à partir du champ API de date.
    /// </summary>
    private static string ExtraireAnneeJeu(string dateSortie)
    {
        if (string.IsNullOrWhiteSpace(dateSortie))
        {
            return string.Empty;
        }

        if (
            DateTimeOffset.TryParse(
                dateSortie,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal,
                out DateTimeOffset dateParsee
            )
        )
        {
            return dateParsee.Year.ToString(CultureInfo.InvariantCulture);
        }

        if (
            DateTime.TryParse(
                dateSortie,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces,
                out DateTime dateSimple
            )
        )
        {
            return dateSimple.Year.ToString(CultureInfo.InvariantCulture);
        }

        return dateSortie.Length >= 4 ? dateSortie[..4] : string.Empty;
    }

    /// <summary>
    /// Détermine le statut lisible du jeu selon HighestAwardKind, avec repli sur les compteurs pour completed/mastered.
    /// </summary>
    private static string DeterminerStatutJeu(JeuUtilisateurRetroAchievements jeu)
    {
        string etatApi = jeu.PlusHauteRecompense.Trim().ToLowerInvariant();

        string etatDirect = etatApi switch
        {
            "mastered" => "Jeu maîtrisé",
            "completed" => "Jeu complété",
            "beaten" => "Jeu battu",
            "beaten-hardcore" => "Jeu battu en hardcore",
            "beaten-softcore" => "Jeu battu en softcore",
            _ => string.Empty,
        };

        if (!string.IsNullOrWhiteSpace(etatDirect))
        {
            return etatDirect;
        }

        if (jeu.NombreSucces > 0 && jeu.NombreSuccesObtenusHardcore == jeu.NombreSucces)
        {
            return "Jeu maîtrisé";
        }

        if (
            jeu.NombreSucces > 0
            && jeu.NombreSuccesObtenus == jeu.NombreSucces
            && jeu.NombreSuccesObtenusHardcore < jeu.NombreSucces
        )
        {
            return "Jeu complété";
        }

        return "Progression en cours";
    }

    /// <summary>
    /// Formate une durée exprimée en minutes en texte français lisible.
    /// </summary>
    private static string FormaterTempsJeuTotal(int totalMinutes)
    {
        if (totalMinutes <= 0)
        {
            return "0 min";
        }

        int jours = totalMinutes / (24 * 60);
        int heures = (totalMinutes % (24 * 60)) / 60;
        int minutes = totalMinutes % 60;
        List<string> segments = [];

        if (jours > 0)
        {
            segments.Add(jours == 1 ? "1 j" : $"{jours} j");
        }

        if (heures > 0)
        {
            segments.Add(heures == 1 ? "1 h" : $"{heures} h");
        }

        if (minutes > 0 || segments.Count == 0)
        {
            segments.Add(minutes == 1 ? "1 min" : $"{minutes} min");
        }

        return string.Join(" ", segments);
    }

    /// <summary>
    /// Convertit une chaîne de pourcentage de l'API en valeur numérique exploitable.
    /// </summary>
    private static double ExtrairePourcentage(string pourcentageApi)
    {
        if (string.IsNullOrWhiteSpace(pourcentageApi))
        {
            return 0;
        }

        string valeurNormalisee = pourcentageApi.Replace("%", string.Empty).Trim();

        if (
            double.TryParse(
                valeurNormalisee,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out double pourcentage
            )
        )
        {
            return Math.Clamp(pourcentage, 0, 100);
        }

        if (
            double.TryParse(
                valeurNormalisee,
                NumberStyles.Float,
                CultureInfo.CurrentCulture,
                out pourcentage
            )
        )
        {
            return Math.Clamp(pourcentage, 0, 100);
        }

        return 0;
    }

    /// <summary>
    /// Normalise l'affichage texte du pourcentage de complétion.
    /// </summary>
    private static string NormaliserPourcentage(string pourcentageApi)
    {
        double valeur = ExtrairePourcentage(pourcentageApi);
        return $"{valeur:0.##} % complété";
    }

    /// <summary>
    /// Ouvre la modale de connexion pour modifier le compte sans perdre l'etat visuel courant.
    /// </summary>
    private async void ConfigurerConnexion_Click(object sender, RoutedEventArgs e)
    {
        MemoriserGeometrieFenetre();
        ArreterActualisationAutomatique();
        DefinirEtatConnexion("Modification en cours");

        await AfficherModaleConnexionAsync();
    }

    /// <summary>
    /// Affiche la modale de compte depuis le bouton principal de la carte de gauche.
    /// </summary>
    private async void AfficherCompte_Click(object sender, RoutedEventArgs e)
    {
        if (!ConfigurationConnexionEstComplete())
        {
            await AfficherModaleConnexionAsync();
            return;
        }

        await AfficherModaleCompteAsync();
    }

    /// <summary>
    /// Met à jour le résumé visible de la connexion locale.
    /// </summary>
    private void MettreAJourResumeConnexion()
    {
        if (string.IsNullOrWhiteSpace(_configurationConnexion.Pseudo))
        {
            _etatConnexionCourant = "Non configuré";
            BoutonCompteUtilisateur.Content = "Connexion";
        }
        else
        {
            BoutonCompteUtilisateur.Content = ObtenirLibelleBoutonCompte();
        }
    }

    /// <summary>
    /// Retourne le libellé à afficher sur le bouton d'accès au compte.
    /// </summary>
    private string ObtenirLibelleBoutonCompte()
    {
        return string.IsNullOrWhiteSpace(_configurationConnexion.Pseudo)
            ? "Connexion"
            : _configurationConnexion.Pseudo;
    }

    /// <summary>
    /// Met à jour l'état de connexion visible dans la carte de connexion.
    /// </summary>
    private void DefinirEtatConnexion(string etatConnexion)
    {
        _etatConnexionCourant = etatConnexion;
        MettreAJourResumeConnexion();
    }

    /// <summary>
    /// Masque la majeure partie de la clé API dans l'interface.
    /// </summary>
    private static string MasquerCleApi(string cleApi)
    {
        if (string.IsNullOrWhiteSpace(cleApi))
        {
            return "non renseignée";
        }

        if (cleApi.Length <= 4)
        {
            return new string('*', cleApi.Length);
        }

        return $"{cleApi[..2]}{new string('*', cleApi.Length - 4)}{cleApi[^2..]}";
    }

    /// <summary>
    /// Bascule entre un affichage sur une colonne ou deux colonnes selon la largeur disponible.
    /// </summary>
    private void AjusterDisposition()
    {
        bool dispositionDouble = ActualWidth >= LargeurMinimaleDispositionDouble;
        bool carteConnexionVisible = CarteConnexion?.Visibility == Visibility.Visible;

        GrilleCartes.RowDefinitions.Clear();

        if (dispositionDouble)
        {
            GrilleCartes.ColumnDefinitions[0].Width = carteConnexionVisible
                ? new GridLength(280)
                : new GridLength(1, GridUnitType.Star);
            GrilleCartes.ColumnDefinitions[1].Width = carteConnexionVisible
                ? new GridLength(20)
                : new GridLength(0);
            GrilleCartes.ColumnDefinitions[2].Width = carteConnexionVisible
                ? new GridLength(1, GridUnitType.Star)
                : new GridLength(0);

            GrilleCartes.RowDefinitions.Add(
                new SystemControls.RowDefinition { Height = new GridLength(1, GridUnitType.Star) }
            );

            if (carteConnexionVisible)
            {
                SystemControls.Grid.SetColumn(CarteConnexion, 0);
                SystemControls.Grid.SetRow(CarteConnexion, 0);
            }

            SystemControls.Grid.SetColumn(CarteJeuEnCours, carteConnexionVisible ? 2 : 0);
            SystemControls.Grid.SetRow(CarteJeuEnCours, 0);
        }
        else
        {
            GrilleCartes.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
            GrilleCartes.ColumnDefinitions[1].Width = new GridLength(0);
            GrilleCartes.ColumnDefinitions[2].Width = new GridLength(0);

            if (carteConnexionVisible)
            {
                GrilleCartes.RowDefinitions.Add(
                    new SystemControls.RowDefinition { Height = GridLength.Auto }
                );
                GrilleCartes.RowDefinitions.Add(
                    new SystemControls.RowDefinition { Height = new GridLength(20) }
                );
                GrilleCartes.RowDefinitions.Add(
                    new SystemControls.RowDefinition { Height = GridLength.Auto }
                );
            }
            else
            {
                GrilleCartes.RowDefinitions.Add(
                    new SystemControls.RowDefinition { Height = GridLength.Auto }
                );
            }

            if (carteConnexionVisible)
            {
                SystemControls.Grid.SetColumn(CarteConnexion, 0);
                SystemControls.Grid.SetRow(CarteConnexion, 0);
            }

            SystemControls.Grid.SetColumn(CarteJeuEnCours, 0);
            SystemControls.Grid.SetRow(CarteJeuEnCours, carteConnexionVisible ? 2 : 0);
        }

        AjusterHauteurCarteJeuEnCours();
    }

    /// <summary>
    /// Borne la hauteur de la carte "Jeu en cours" à la hauteur visible de la fenêtre.
    /// </summary>
    private void AjusterHauteurCarteJeuEnCours()
    {
        if (CarteJeuEnCours is null || ZonePrincipale is null)
        {
            return;
        }

        if (ZonePrincipale.Visibility != Visibility.Visible)
        {
            CarteJeuEnCours.Height = double.NaN;
            CarteJeuEnCours.MaxHeight = double.PositiveInfinity;
            return;
        }

        double hauteurVisible =
            ZonePrincipale.ViewportHeight > 0
                ? ZonePrincipale.ViewportHeight
                : ZonePrincipale.ActualHeight;

        if (hauteurVisible <= 0)
        {
            _ = Dispatcher.BeginInvoke(
                (Action)AjusterHauteurCarteJeuEnCours,
                DispatcherPriority.Render
            );
            return;
        }

        double hauteurCible = Math.Max(1, hauteurVisible);
        CarteJeuEnCours.Height = hauteurCible;
        CarteJeuEnCours.MaxHeight = hauteurCible;
    }

    /// <summary>
    /// Affiche ou masque le contenu principal pendant l'ouverture de la modale de connexion.
    /// </summary>
    private void DefinirVisibiliteContenuPrincipal(bool afficher)
    {
        ZonePrincipale.Visibility = afficher ? Visibility.Visible : Visibility.Hidden;
        AjusterHauteurCarteJeuEnCours();
    }

    /// <summary>
    /// Indique si les informations minimales de connexion sont déjà disponibles localement.
    /// </summary>
    private bool ConfigurationConnexionEstComplete()
    {
        return !string.IsNullOrWhiteSpace(_configurationConnexion.Pseudo)
            && !string.IsNullOrWhiteSpace(_configurationConnexion.CleApiWeb);
    }

    /// <summary>
    /// Applique une géométrie sauvegardée si elle reste visible sur l'écran courant.
    /// </summary>
    private void AppliquerGeometrieFenetre()
    {
        Width = Math.Max(MinWidth, _configurationConnexion.LargeurFenetre);
        Height = Math.Max(MinHeight, _configurationConnexion.HauteurFenetre);

        if (
            _configurationConnexion.PositionGaucheFenetre is not double gauche
            || _configurationConnexion.PositionHautFenetre is not double haut
        )
        {
            return;
        }

        Rect zoneVisible = new(
            SystemParameters.VirtualScreenLeft,
            SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth,
            SystemParameters.VirtualScreenHeight
        );

        Rect zoneFenetre = new(gauche, haut, Width, Height);

        if (!zoneVisible.IntersectsWith(zoneFenetre))
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            return;
        }

        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = gauche;
        Top = haut;
    }

    /// <summary>
    /// Mémorise la géométrie courante de la fenêtre pour le prochain lancement.
    /// </summary>
    private void MemoriserGeometrieFenetre()
    {
        Rect geometrie =
            WindowState == WindowState.Normal ? new Rect(Left, Top, Width, Height) : RestoreBounds;

        _configurationConnexion.PositionGaucheFenetre = geometrie.Left;
        _configurationConnexion.PositionHautFenetre = geometrie.Top;
        _configurationConnexion.LargeurFenetre = Math.Max(MinWidth, geometrie.Width);
        _configurationConnexion.HauteurFenetre = Math.Max(MinHeight, geometrie.Height);
    }

    /// <summary>
    /// Retourne le texte d'un bouton du footer.
    /// </summary>
    private static string TexteBouton(SystemControls.Button bouton)
    {
        return bouton.Content?.ToString()?.Trim() ?? string.Empty;
    }

    /// <summary>
    /// Trouve le premier panneau commun qui contient les deux boutons du footer.
    /// </summary>
    private static SystemControls.Panel? TrouverPanneauCommun(
        DependencyObject premierElement,
        DependencyObject secondElement
    )
    {
        HashSet<DependencyObject> ancetresPremier = [];
        DependencyObject? elementCourant = premierElement;

        while (elementCourant is not null)
        {
            ancetresPremier.Add(elementCourant);
            elementCourant = VisualTreeHelper.GetParent(elementCourant);
        }

        elementCourant = secondElement;

        while (elementCourant is not null)
        {
            if (ancetresPremier.Contains(elementCourant))
            {
                DependencyObject? ancetre = elementCourant;

                while (ancetre is not null && ancetre is not SystemControls.Panel)
                {
                    ancetre = VisualTreeHelper.GetParent(ancetre);
                }

                return ancetre as SystemControls.Panel;
            }

            elementCourant = VisualTreeHelper.GetParent(elementCourant);
        }

        return null;
    }

    /// <summary>
    /// Parcourt récursivement l'arbre visuel pour récupérer les descendants d'un type précis.
    /// </summary>
    private static IEnumerable<TElement> TrouverDescendants<TElement>(DependencyObject racine)
        where TElement : DependencyObject
    {
        int nombreEnfants = VisualTreeHelper.GetChildrenCount(racine);

        for (int indexEnfant = 0; indexEnfant < nombreEnfants; indexEnfant++)
        {
            DependencyObject enfant = VisualTreeHelper.GetChild(racine, indexEnfant);

            if (enfant is TElement elementTrouve)
            {
                yield return elementTrouve;
            }

            foreach (TElement descendant in TrouverDescendants<TElement>(enfant))
            {
                yield return descendant;
            }
        }
    }

    /// <summary>
    /// Récupère un rayon de coins partagé depuis les ressources de l'application.
    /// </summary>
    private CornerRadius ObtenirRayonCoins(string cleRessource, double valeurParDefaut)
    {
        if (TryFindResource(cleRessource) is CornerRadius rayon)
        {
            return rayon;
        }

        return new CornerRadius(valeurParDefaut);
    }
}
