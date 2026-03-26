using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
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

    private sealed record BadgeSuccesGrilleContexte(
        int IdentifiantJeu,
        int IdentifiantSucces,
        string UrlBadge
    );

    private enum OrdreSuccesGrille
    {
        Normal,
        Aleatoire,
        Facile,
        Difficile,
    }

    private static readonly TimeSpan IntervalleActualisationApi = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan IntervalleSondeLocale = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan IntervalleRafraichissementIndicateurRcheevos =
        TimeSpan.FromSeconds(1);
    private static readonly TimeSpan IntervalleMasquageBarreDefilement = TimeSpan.FromSeconds(1.2);
    private static readonly TimeSpan IntervalleRepriseAnimationGrilleSucces = TimeSpan.FromSeconds(
        1.3
    );
    private static readonly TimeSpan DureeAffichageTemporaireSuccesGrille = TimeSpan.FromSeconds(
        10
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
    private const double DureeFonduImageJeuEnCoursMillisecondes = 1000;
    private const double RayonFlouTransitionImageJeuEnCours = 14;
    private static readonly TimeSpan IntervalleRotationVisuelsJeuEnCours = TimeSpan.FromSeconds(4);

    private readonly ServiceConfigurationLocale _serviceConfigurationLocale = new();
    private readonly SondeJeuLocal _sondeJeuLocal = new();
    private readonly ServiceHachageJeuLocal _serviceHachageJeuLocal = new();
    private readonly ServiceHachageRcheevos _serviceHachageRcheevos = new();
    private readonly ServiceTraductionTexte _serviceTraductionTexte = new();
    private readonly ServiceRcheevos _serviceRcheevos = new();
    private readonly DispatcherTimer _minuteurActualisationApi = new();
    private readonly DispatcherTimer _minuteurSondeLocale = new();
    private readonly DispatcherTimer _minuteurMasquageBarreDefilement = new();
    private readonly DispatcherTimer _minuteurRepriseAnimationGrilleSucces = new();
    private readonly DispatcherTimer _minuteurAffichageTemporaireSuccesGrille = new();
    private readonly DispatcherTimer _minuteurRotationVisuelsJeuEnCours = new();
    private readonly Random _generateurAleatoireSuccesGrille = new();
    private readonly Dictionary<string, ImageSource> _cacheImagesDistantes = new(
        StringComparer.OrdinalIgnoreCase
    );
    private readonly Dictionary<string, Task<ImageSource?>> _chargementsImagesDistantesEnCours =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly List<VisuelJeuEnCours> _visuelsJeuEnCours = [];
    private SystemControls.Primitives.ScrollBar? _barreDefilementVerticalePrincipale;
    private bool _connexionInitialeAffichee;
    private bool _chargementJeuEnCoursActif;
    private bool _actualisationApiCibleeEnAttente;
    private bool _profilUtilisateurAccessible = true;
    private bool _dernierJeuAfficheModifie;
    private bool _dernierSuccesAfficheModifie;
    private bool _derniereListeSuccesAfficheeModifiee;
    private bool _miseAJourAnimationTitreJeuPlanifiee;
    private bool _miseAJourAnimationGrilleSuccesPlanifiee;
    private bool _animationGrilleSuccesVersBas = true;
    private bool _survolBadgeGrilleSuccesActif;
    private int _dernierIdentifiantJeuApi;
    private int _dernierIdentifiantJeuAvecInfos;
    private int _dernierIdentifiantJeuAvecProgression;
    private int _versionChargementContenuJeu;
    private ProfilUtilisateurRetroAchievements? _dernierProfilUtilisateurCharge;
    private ResumeUtilisateurRetroAchievements? _dernierResumeUtilisateurCharge;
    private string _dernierTitreJeuApi = string.Empty;
    private string _dernierePresenceRiche = string.Empty;
    private string _dernierPseudoCharge = string.Empty;
    private DateTime _dernierRafraichissementIndicateurRcheevosUtc = DateTime.MinValue;
    private string _signatureDernierJeuLocal = string.Empty;
    private string _signatureAnimationTitreJeu = string.Empty;
    private string _signatureAnimationGrilleSucces = string.Empty;
    private string _signatureOrdreAleatoireSuccesGrille = string.Empty;
    private string _etatConnexionCourant = "Non configuré";
    private string _cheminImageJeuEnCoursDemande = string.Empty;
    private string _cheminImageJeuEnCoursAffiche = string.Empty;
    private ConfigurationConnexion _configurationConnexion = new();
    private JeuDetecteLocalement? _dernierJeuLocalDetecte;
    private int _indexVisuelJeuEnCours;
    private int _identifiantJeuSuccesCourant;
    private int _identifiantJeuOrdreAleatoireSuccesGrille;
    private int? _identifiantSuccesGrilleTemporaire;
    private int? _identifiantSuccesGrilleEpingle;
    private bool _retourPremierSuccesNonDebloqueApresSelectionTemporaire;
    private double _amplitudeAnimationGrilleSucces;
    private AnimationClock? _horlogeAnimationGrilleSucces;
    private Dictionary<int, int> _positionsAleatoiresSuccesGrille = [];
    private List<SuccesJeuUtilisateurRetroAchievements> _succesJeuCourant = [];
    private OrdreSuccesGrille _ordreSuccesGrilleCourant = OrdreSuccesGrille.Normal;

    /// <summary>
    /// Initialise la fenêtre principale.
    /// </summary>
    public MainWindow()
    {
        App.JournaliserDemarrage("MainWindow ctor début");
        InitializeComponent();
        App.JournaliserDemarrage("MainWindow ctor fin");
        MettreAJourLibelleOrdreSuccesGrilleEtModes();
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

        _minuteurAffichageTemporaireSuccesGrille.Interval = DureeAffichageTemporaireSuccesGrille;
        _minuteurAffichageTemporaireSuccesGrille.Tick +=
            MinuteurAffichageTemporaireSuccesGrille_Tick;

        _minuteurRotationVisuelsJeuEnCours.Interval = IntervalleRotationVisuelsJeuEnCours;
        _minuteurRotationVisuelsJeuEnCours.Tick += MinuteurRotationVisuelsJeuEnCours_Tick;
    }

    /// <summary>
    /// Charge la configuration locale puis affiche la modale de connexion au premier lancement.
    /// </summary>
    private async void FenetrePrincipaleChargee(object sender, RoutedEventArgs e)
    {
        App.JournaliserDemarrage("FenetrePrincipaleChargee début");
        if (_connexionInitialeAffichee)
        {
            return;
        }

        _connexionInitialeAffichee = true;
        DefinirVisibiliteContenuPrincipal(true);
        AjusterDisposition();
        App.JournaliserDemarrage("FenetrePrincipaleChargee avant ChargerConfig");
        _configurationConnexion = await _serviceConfigurationLocale.ChargerAsync();
        App.JournaliserDemarrage("FenetrePrincipaleChargee apres ChargerConfig");
        AppliquerGeometrieFenetre();
        MettreAJourResumeConnexion();
        AjusterDisposition();
        _ = Dispatcher.BeginInvoke(
            DefinirVisibiliteBarreDefilementPrincipale,
            DispatcherPriority.Loaded
        );
        App.JournaliserDemarrage("FenetrePrincipaleChargee avant DernierJeuSauvegarde");
        await AppliquerDernierJeuSauvegardeAsync();
        App.JournaliserDemarrage("FenetrePrincipaleChargee apres DernierJeuSauvegarde");
        bool conserverEtatSauvegardeAuPremierChargement =
            _configurationConnexion.DernierJeuAffiche is not null;

        if (ConfigurationConnexionEstComplete())
        {
            App.JournaliserDemarrage("FenetrePrincipaleChargee avant ChargerJeuEnCours");
            await ChargerJeuEnCoursAsync(
                !conserverEtatSauvegardeAuPremierChargement,
                true,
                sonderJeuLocal: false
            );
            App.JournaliserDemarrage("FenetrePrincipaleChargee apres ChargerJeuEnCours");
            _ = Dispatcher.BeginInvoke(
                () => DemarrerActualisationAutomatique(),
                DispatcherPriority.ApplicationIdle
            );
            App.JournaliserDemarrage("FenetrePrincipaleChargee fin");
            return;
        }

        await AfficherModaleConnexionAsync();
        App.JournaliserDemarrage("FenetrePrincipaleChargee fin");
    }

    /// <summary>
    /// Sauvegarde la géométrie de la fenêtre au moment de la fermeture.
    /// </summary>
    private void FenetrePrincipale_Fermeture(object? sender, CancelEventArgs e)
    {
        ArreterActualisationAutomatique();
        MemoriserGeometrieFenetre();

        try
        {
            Task.Run(() =>
                    _serviceConfigurationLocale.SauvegarderEtatApplicationAsync(
                        _configurationConnexion
                    )
                )
                .GetAwaiter()
                .GetResult();
        }
        catch
        {
            // Évite de bloquer la fermeture si la persistance locale échoue au tout dernier moment.
        }
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
                await ClientRetroAchievements.ObtenirProfilUtilisateurAsync(pseudo, cleApi);
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
                _configurationConnexion.DernierSuccesAffiche = null;
                _configurationConnexion.DerniereListeSuccesAffichee = null;
            }

            _configurationConnexion.Pseudo = pseudo;
            _configurationConnexion.CleApiWeb = cleApi;

            MemoriserGeometrieFenetre();
            await _serviceConfigurationLocale.SauvegarderUtilisateurAsync(_configurationConnexion);
            await _serviceConfigurationLocale.SauvegarderEtatApplicationAsync(
                _configurationConnexion
            );

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
                await ClientRetroAchievements.ObtenirResumeUtilisateurAsync(
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
                await ClientRetroAchievements.ObtenirProfilUtilisateurAsync(
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
        TexteIndicateurRcheevosPremierSuccesNonDebloque.Text = string.Empty;
        TexteIndicateurRcheevosPremierSuccesNonDebloque.Visibility = Visibility.Collapsed;
    }

    /// <summary>
    /// Met à jour la ligne d'indicateur rcheevos du succès en cours.
    /// </summary>
    private void DefinirIndicateurRcheevosPremierSucces(string texte)
    {
        TexteIndicateurRcheevosPremierSuccesNonDebloque.Text = texte;
        TexteIndicateurRcheevosPremierSuccesNonDebloque.Visibility = string.IsNullOrWhiteSpace(
            texte
        )
            ? Visibility.Collapsed
            : Visibility.Visible;
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
        _serviceRcheevos.DefinirDefinitionsSucces(jeu.IdentifiantJeu, succes);
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
        string IndicateurProgression,
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
                        ConstruireTexteRcheevosPremierSucces(succesTemporaire.IdentifiantSucces),
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
                        ConstruireTexteRcheevosPremierSucces(succesEpingle.IdentifiantSucces),
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
            premierSuccesNonDebloque is null
                ? string.Empty
                : ConstruireTexteRcheevosPremierSucces(premierSuccesNonDebloque.IdentifiantSucces),
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
            string indicateurProgression,
            bool doitSauvegarder,
            bool estEpingleManuellement
        ) = SelectionnerSuccesEnCours(succes);

        await AppliquerSuccesEnCoursAsync(
            identifiantJeu,
            premierSucces,
            indicateurProgression,
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
        string indicateurProgression,
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
            DefinirIndicateurRcheevosPremierSucces(string.Empty);
            SauvegarderDernierSuccesAffiche(
                new EtatSuccesAfficheLocal
                {
                    IdentifiantJeu = identifiantJeu,
                    IdentifiantSucces = 0,
                    Titre = TexteTitrePremierSuccesNonDebloque.Text,
                    Description = TexteDescriptionPremierSuccesNonDebloque.Text,
                    DetailsPoints = string.Empty,
                    IndicateurProgression = string.Empty,
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
        DefinirIndicateurRcheevosPremierSucces(indicateurProgression);
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
                    IndicateurProgression = indicateurProgression,
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
    /// Formate le Progress Indicator rcheevos du succès courant pour l'affichage.
    /// </summary>
    private static string ConstruireIndicateurRcheevosPremierSucces(
        IndicateurProgressionRcheevos? indicateur
    )
    {
        if (indicateur is null)
        {
            return string.Empty;
        }

        List<string> segments = [];

        if (!string.IsNullOrWhiteSpace(indicateur.Texte))
        {
            segments.Add($"Indicateur : {indicateur.Texte.Trim()}");
        }

        return string.Join(" • ", segments);
    }

    /// <summary>
    /// Retourne uniquement l'indicateur rcheevos mesuré quand il existe réellement.
    /// </summary>
    private string ConstruireTexteRcheevosPremierSucces(int identifiantSucces)
    {
        return ConstruireIndicateurRcheevosPremierSucces(
            _serviceRcheevos.ObtenirIndicateurProgression(identifiantSucces)
        );
    }

    /// <summary>
    /// Retourne l'identifiant du succès actuellement montré dans la carte principale.
    /// </summary>
    private int ObtenirIdentifiantSuccesAffichePourRafraichissementRcheevos()
    {
        if (_identifiantJeuSuccesCourant <= 0)
        {
            return 0;
        }

        if (_identifiantSuccesGrilleTemporaire.HasValue)
        {
            return _identifiantSuccesGrilleTemporaire.Value;
        }

        if (_identifiantSuccesGrilleEpingle.HasValue)
        {
            return _identifiantSuccesGrilleEpingle.Value;
        }

        return
            _configurationConnexion.DernierSuccesAffiche?.IdentifiantJeu
            == _identifiantJeuSuccesCourant
            ? _configurationConnexion.DernierSuccesAffiche.IdentifiantSucces
            : 0;
    }

    /// <summary>
    /// Rafraîchit périodiquement l'indicateur rcheevos du succès affiché sans attendre un rechargement complet.
    /// </summary>
    private void ActualiserIndicateurRcheevosAfficheSiPossible(bool forcer = false)
    {
        int identifiantSucces = ObtenirIdentifiantSuccesAffichePourRafraichissementRcheevos();

        if (identifiantSucces <= 0)
        {
            return;
        }

        DateTime maintenantUtc = DateTime.UtcNow;
        if (
            !forcer
            && _dernierRafraichissementIndicateurRcheevosUtc != DateTime.MinValue
            && maintenantUtc - _dernierRafraichissementIndicateurRcheevosUtc
                < IntervalleRafraichissementIndicateurRcheevos
        )
        {
            return;
        }

        _dernierRafraichissementIndicateurRcheevosUtc = maintenantUtc;
        RafraichirIndicateurRcheevosPremierSuccesSiPossible(identifiantSucces);
    }

    /// <summary>
    /// Tente de recalculer l'indicateur rcheevos du succès actuellement affiché quand on connaît son identifiant.
    /// </summary>
    private void RafraichirIndicateurRcheevosPremierSuccesSiPossible(int identifiantSucces)
    {
        if (identifiantSucces <= 0)
        {
            return;
        }

        string indicateurProgression = ConstruireTexteRcheevosPremierSucces(identifiantSucces);

        DefinirIndicateurRcheevosPremierSucces(indicateurProgression);

        EtatSuccesAfficheLocal? succesSauvegarde = _configurationConnexion.DernierSuccesAffiche;
        if (
            succesSauvegarde is null
            || succesSauvegarde.IdentifiantSucces != identifiantSucces
            || string.Equals(
                succesSauvegarde.IndicateurProgression,
                indicateurProgression,
                StringComparison.Ordinal
            )
        )
        {
            return;
        }

        succesSauvegarde.IndicateurProgression = indicateurProgression;
        _dernierSuccesAfficheModifie = true;
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
                await ClientRetroAchievements.ObtenirConsolesAsync(
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
    /// Affiche immédiatement les métadonnées déjà connues du jeu sans attendre les enrichissements lents.
    /// </summary>
    private void AppliquerMetaConsoleJeuEnCoursInitiale(JeuUtilisateurRetroAchievements jeu)
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
            TexteTypeJeuEnCours.Text = jeu.Genre.Trim();
            TexteTypeJeuEnCours.Visibility = Visibility.Visible;
        }

        if (!string.IsNullOrWhiteSpace(jeu.Developpeur))
        {
            TexteDeveloppeurJeuEnCours.Text = jeu.Developpeur.Trim();
            TexteDeveloppeurJeuEnCours.Visibility = Visibility.Visible;
        }

        LigneMetaJeuEnCours.Visibility =
            TexteConsoleJeuEnCours.Visibility == Visibility.Visible
            || TexteAnneeJeuEnCours.Visibility == Visibility.Visible
            || TexteTypeJeuEnCours.Visibility == Visibility.Visible
            || TexteDeveloppeurJeuEnCours.Visibility == Visibility.Visible
                ? Visibility.Visible
                : Visibility.Collapsed;
    }

    /// <summary>
    /// Lance les enrichissements secondaires des métadonnées sans bloquer le rendu initial.
    /// </summary>
    private void DemarrerEnrichissementMetaConsoleJeuEnCours(JeuUtilisateurRetroAchievements jeu)
    {
        _ = EnrichirMetaConsoleJeuEnCoursAsync(jeu);
    }

    /// <summary>
    /// Traduit le genre et charge l'icône de console après l'affichage initial.
    /// </summary>
    private async Task EnrichirMetaConsoleJeuEnCoursAsync(JeuUtilisateurRetroAchievements jeu)
    {
        try
        {
            string genreAffiche = jeu.Genre?.Trim() ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(jeu.Genre))
            {
                genreAffiche = (
                    await _serviceTraductionTexte.TraduireVersFrancaisAsync(jeu.Genre)
                ).Trim();
            }

            ImageSource? imageConsole = null;

            try
            {
                IReadOnlyList<ConsoleRetroAchievements> consoles =
                    await ClientRetroAchievements.ObtenirConsolesAsync(
                        _configurationConnexion.CleApiWeb
                    );
                ConsoleRetroAchievements? console = consoles.FirstOrDefault(item =>
                    item.IdentifiantConsole == jeu.IdentifiantConsole
                );

                if (console is not null && !string.IsNullOrWhiteSpace(console.UrlIcone))
                {
                    imageConsole = await ChargerImageDistanteAsync(console.UrlIcone);
                }
            }
            catch
            {
                // L'icône de console reste facultative.
            }

            if (_dernierIdentifiantJeuAvecInfos != jeu.IdentifiantJeu)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(genreAffiche))
            {
                TexteTypeJeuEnCours.Text = genreAffiche;
                TexteTypeJeuEnCours.Visibility = Visibility.Visible;
            }

            if (imageConsole is not null)
            {
                ImageConsoleJeuEnCours.Source = imageConsole;
                ImageConsoleJeuEnCours.Visibility = Visibility.Visible;
            }

            LigneMetaJeuEnCours.Visibility =
                ImageConsoleJeuEnCours.Visibility == Visibility.Visible
                || TexteConsoleJeuEnCours.Visibility == Visibility.Visible
                || TexteAnneeJeuEnCours.Visibility == Visibility.Visible
                || TexteTypeJeuEnCours.Visibility == Visibility.Visible
                || TexteDeveloppeurJeuEnCours.Visibility == Visibility.Visible
                    ? Visibility.Visible
                    : Visibility.Collapsed;
        }
        catch
        {
            // Les enrichissements restent facultatifs.
        }
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
    private Task AppliquerDernierJeuSauvegardeAsync()
    {
        EtatJeuAfficheLocal? jeuSauvegarde = _configurationConnexion.DernierJeuAffiche;

        if (jeuSauvegarde is null || string.IsNullOrWhiteSpace(jeuSauvegarde.Titre))
        {
            return Task.CompletedTask;
        }

        DefinirTitreZoneJeu(jeuSauvegarde.EstJeuEnCours);
        _dernierTitreJeuApi = jeuSauvegarde.Titre;
        _dernierIdentifiantJeuAvecInfos = jeuSauvegarde.IdentifiantJeu;
        _dernierIdentifiantJeuAvecProgression = jeuSauvegarde.IdentifiantJeu;
        _serviceRcheevos.DefinirJeuActif(
            jeuSauvegarde.IdentifiantJeu,
            jeuSauvegarde.IdentifiantConsole,
            jeuSauvegarde.Titre
        );
        _serviceRcheevos.ReinitialiserSourceMemoire();

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

        DefinirVisuelsJeuEnCours(
            string.IsNullOrWhiteSpace(jeuSauvegarde.CheminImageBoite)
                ? []
                : [new VisuelJeuEnCours("Jaquette", jeuSauvegarde.CheminImageBoite)]
        );

        JeuUtilisateurRetroAchievements jeuLocalReconstruit =
            ConstruireJeuUtilisateurDepuisEtatLocal(jeuSauvegarde);
        _identifiantSuccesGrilleTemporaire = null;
        _identifiantSuccesGrilleEpingle = null;
        AppliquerMetaConsoleJeuEnCoursInitiale(jeuLocalReconstruit);
        DemarrerEnrichissementMetaConsoleJeuEnCours(jeuLocalReconstruit);
        DemarrerRestaurationSuccesSauvegardesEnArrierePlan(jeuSauvegarde.IdentifiantJeu);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Réapplique les succès sauvegardés sans bloquer l'affichage initial du dernier jeu.
    /// </summary>
    private void DemarrerRestaurationSuccesSauvegardesEnArrierePlan(int identifiantJeu)
    {
        _ = RestaurerSuccesSauvegardesEnArrierePlanAsync(identifiantJeu);
    }

    /// <summary>
    /// Recharge la carte du succès courant et la grille sauvegardée après le rendu initial.
    /// </summary>
    private async Task RestaurerSuccesSauvegardesEnArrierePlanAsync(int identifiantJeu)
    {
        try
        {
            await AppliquerDernierSuccesSauvegardeAsync(identifiantJeu);
            await AppliquerDerniereListeSuccesSauvegardeeAsync(identifiantJeu);
        }
        catch
        {
            // Les données sauvegardées enrichissent l'écran de démarrage, mais ne doivent pas le ralentir.
        }
    }

    /// <summary>
    /// Réapplique le dernier rétrosuccès sauvegardé pour éviter une zone vide au démarrage.
    /// </summary>
    private async Task AppliquerDernierSuccesSauvegardeAsync(int identifiantJeu)
    {
        EtatSuccesAfficheLocal? succesSauvegarde = _configurationConnexion.DernierSuccesAffiche;

        if (
            succesSauvegarde is null
            || succesSauvegarde.IdentifiantJeu != identifiantJeu
            || string.IsNullOrWhiteSpace(succesSauvegarde.Titre)
        )
        {
            return;
        }

        _identifiantSuccesGrilleEpingle = succesSauvegarde.EstEpingleManuellement
            ? succesSauvegarde.IdentifiantSucces
            : null;
        TexteTitrePremierSuccesNonDebloque.Text = succesSauvegarde.Titre;
        TexteTitrePremierSuccesNonDebloque.Visibility = Visibility.Visible;
        TexteDescriptionPremierSuccesNonDebloque.Text = succesSauvegarde.Description;
        TexteDescriptionPremierSuccesNonDebloque.Visibility = string.IsNullOrWhiteSpace(
            succesSauvegarde.Description
        )
            ? Visibility.Collapsed
            : Visibility.Visible;
        TextePointsPremierSuccesNonDebloque.Text = succesSauvegarde.DetailsPoints;
        TextePointsPremierSuccesNonDebloque.Visibility = string.IsNullOrWhiteSpace(
            succesSauvegarde.DetailsPoints
        )
            ? Visibility.Collapsed
            : Visibility.Visible;
        DefinirIndicateurRcheevosPremierSucces(succesSauvegarde.IndicateurProgression);
        RafraichirIndicateurRcheevosPremierSuccesSiPossible(succesSauvegarde.IdentifiantSucces);

        if (!string.IsNullOrWhiteSpace(succesSauvegarde.CheminImageBadge))
        {
            ImageSource? imageSucces = await ChargerImageDistanteAsync(
                succesSauvegarde.CheminImageBadge
            );

            if (imageSucces is not null)
            {
                ImagePremierSuccesNonDebloque.Source = ConvertirImageEnNoirEtBlanc(imageSucces);
                ImagePremierSuccesNonDebloque.Visibility = Visibility.Visible;
                TexteImagePremierSuccesNonDebloque.Visibility = Visibility.Collapsed;
                AppliquerCoinsArrondisImagePremierSuccesNonDebloque();
                return;
            }
        }

        ImagePremierSuccesNonDebloque.Source = null;
        ImagePremierSuccesNonDebloque.Clip = null;
        ImagePremierSuccesNonDebloque.Visibility = Visibility.Collapsed;
        TexteImagePremierSuccesNonDebloque.Text = succesSauvegarde.TexteVisuel;
        TexteImagePremierSuccesNonDebloque.Visibility = string.IsNullOrWhiteSpace(
            succesSauvegarde.TexteVisuel
        )
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    /// <summary>
    /// Réapplique la dernière grille de rétrosuccès sauvegardée pour éviter une zone vide au démarrage.
    /// </summary>
    private async Task AppliquerDerniereListeSuccesSauvegardeeAsync(int identifiantJeu)
    {
        EtatListeSuccesAfficheeLocal? listeSauvegardee =
            _configurationConnexion.DerniereListeSuccesAffichee;

        if (listeSauvegardee is null || listeSauvegardee.IdentifiantJeu != identifiantJeu)
        {
            return;
        }

        GrilleTousSuccesJeuEnCours.Children.Clear();

        SystemControls.Border[] badges = await Task.WhenAll(
            listeSauvegardee.Succes.Select(succesSauvegarde =>
                ConstruireBadgeGrilleSuccesAsync(
                    identifiantJeu,
                    new SuccesJeuUtilisateurRetroAchievements
                    {
                        IdentifiantSucces = succesSauvegarde.IdentifiantSucces,
                        Titre = succesSauvegarde.Titre,
                    },
                    succesSauvegarde.CheminImageBadge
                )
            )
        );

        foreach (SystemControls.Border badge in badges)
        {
            GrilleTousSuccesJeuEnCours.Children.Add(badge);
        }

        RafraichirStyleBadgesGrilleSucces();
        MettreAJourDispositionGrilleTousSucces();
        PlanifierMiseAJourAnimationGrilleTousSucces();
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
    /// Sauvegarde l'état du rétrosuccès actuellement affiché.
    /// </summary>
    private void SauvegarderDernierSuccesAffiche(EtatSuccesAfficheLocal nouvelEtat)
    {
        if (EtatSuccesAfficheEquivalent(_configurationConnexion.DernierSuccesAffiche, nouvelEtat))
        {
            return;
        }

        _configurationConnexion.DernierSuccesAffiche = nouvelEtat;
        _dernierSuccesAfficheModifie = true;
    }

    /// <summary>
    /// Sauvegarde l'état de la grille des rétrosuccès actuellement affichée.
    /// </summary>
    private void SauvegarderDerniereListeSuccesAffichee(
        int identifiantJeu,
        List<ElementListeSuccesAfficheLocal> succes
    )
    {
        EtatListeSuccesAfficheeLocal nouvelEtat = new()
        {
            IdentifiantJeu = identifiantJeu,
            Succes = succes,
        };

        if (
            EtatListeSuccesAfficheeEquivalent(
                _configurationConnexion.DerniereListeSuccesAffichee,
                nouvelEtat
            )
        )
        {
            return;
        }

        _configurationConnexion.DerniereListeSuccesAffichee = nouvelEtat;
        _derniereListeSuccesAfficheeModifiee = true;
        _ = PersisterDernierJeuAfficheSiNecessaireAsync();
    }

    /// <summary>
    /// Compare deux états de rétrosuccès affiché pour éviter les écritures inutiles.
    /// </summary>
    private static bool EtatSuccesAfficheEquivalent(
        EtatSuccesAfficheLocal? precedent,
        EtatSuccesAfficheLocal? courant
    )
    {
        if (precedent is null || courant is null)
        {
            return precedent is null && courant is null;
        }

        return precedent.IdentifiantJeu == courant.IdentifiantJeu
            && precedent.IdentifiantSucces == courant.IdentifiantSucces
            && string.Equals(precedent.Titre, courant.Titre, StringComparison.Ordinal)
            && string.Equals(precedent.Description, courant.Description, StringComparison.Ordinal)
            && string.Equals(
                precedent.DetailsPoints,
                courant.DetailsPoints,
                StringComparison.Ordinal
            )
            && string.Equals(
                precedent.IndicateurProgression,
                courant.IndicateurProgression,
                StringComparison.Ordinal
            )
            && precedent.EstEpingleManuellement == courant.EstEpingleManuellement
            && string.Equals(
                precedent.CheminImageBadge,
                courant.CheminImageBadge,
                StringComparison.Ordinal
            )
            && string.Equals(precedent.TexteVisuel, courant.TexteVisuel, StringComparison.Ordinal);
    }

    /// <summary>
    /// Compare deux états de grille de rétrosuccès pour éviter les écritures inutiles.
    /// </summary>
    private static bool EtatListeSuccesAfficheeEquivalent(
        EtatListeSuccesAfficheeLocal? precedent,
        EtatListeSuccesAfficheeLocal? courant
    )
    {
        if (precedent is null || courant is null)
        {
            return precedent is null && courant is null;
        }

        if (
            precedent.IdentifiantJeu != courant.IdentifiantJeu
            || precedent.Succes.Count != courant.Succes.Count
        )
        {
            return false;
        }

        for (int index = 0; index < precedent.Succes.Count; index++)
        {
            ElementListeSuccesAfficheLocal succesPrecedent = precedent.Succes[index];
            ElementListeSuccesAfficheLocal succesCourant = courant.Succes[index];

            if (
                !string.Equals(succesPrecedent.Titre, succesCourant.Titre, StringComparison.Ordinal)
                || !string.Equals(
                    succesPrecedent.CheminImageBadge,
                    succesCourant.CheminImageBadge,
                    StringComparison.Ordinal
                )
            )
            {
                return false;
            }
        }

        return true;
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
        bool jeuLocalResoluParHash = jeuLocal.IdentifiantJeuRetroAchievements > 0;
        bool conserverInformationsApi =
            jeuLocalResoluParHash && JeuLocalCorrespondAuJeuAffiche(jeuLocal);
        int identifiantJeu = jeuLocalResoluParHash ? jeuLocal.IdentifiantJeuRetroAchievements : 0;

        return new EtatJeuAfficheLocal
        {
            IdentifiantJeu = identifiantJeu,
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
        if (
            !_dernierJeuAfficheModifie
            && !_dernierSuccesAfficheModifie
            && !_derniereListeSuccesAfficheeModifiee
        )
        {
            return;
        }

        _dernierJeuAfficheModifie = false;
        _dernierSuccesAfficheModifie = false;
        _derniereListeSuccesAfficheeModifiee = false;

        try
        {
            await _serviceConfigurationLocale.SauvegarderEtatApplicationAsync(
                _configurationConnexion
            );
        }
        catch
        {
            _dernierJeuAfficheModifie = true;
            _dernierSuccesAfficheModifie = true;
            _derniereListeSuccesAfficheeModifiee = true;
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
    private async void VisuelJeuPrecedent_Click(object sender, RoutedEventArgs e)
    {
        if (_visuelsJeuEnCours.Count <= 1)
        {
            return;
        }

        _indexVisuelJeuEnCours--;
        await MettreAJourAffichageVisuelJeuEnCoursAsync();
    }

    /// <summary>
    /// Affiche le visuel suivant du jeu courant.
    /// </summary>
    private async void VisuelJeuSuivant_Click(object sender, RoutedEventArgs e)
    {
        if (_visuelsJeuEnCours.Count <= 1)
        {
            return;
        }

        _indexVisuelJeuEnCours++;
        await MettreAJourAffichageVisuelJeuEnCoursAsync();
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
        AppliquerCoinsArrondisImage(ImageJeuEnCoursTransition);
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
        ActualiserIndicateurRcheevosAfficheSiPossible();
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
            ActualiserIndicateurRcheevosAfficheSiPossible();
            return;
        }

        _signatureDernierJeuLocal = signatureJeuLocal;
        DemarrerDiagnosticChangementJeu(signatureJeuLocal, jeuLocal);

        if (jeuLocal is not null && !JeuLocalEstFiable(jeuLocal))
        {
            if (_chargementJeuEnCoursActif)
            {
                JournaliserDiagnosticChangementJeu(
                    "sonde_pendant_chargement",
                    "emulateur_en_attente"
                );
                AppliquerEtatEmulateurEnAttente(jeuLocal);
                _actualisationApiCibleeEnAttente = false;
                return;
            }

            JournaliserDiagnosticChangementJeu("sonde_emulateur_en_attente");
            AppliquerEtatEmulateurEnAttente(jeuLocal);
            RedemarrerMinuteurActualisationApi();
            return;
        }

        if (_chargementJeuEnCoursActif)
        {
            if (jeuLocal is not null)
            {
                JournaliserDiagnosticChangementJeu("sonde_pendant_chargement", "etat_local");
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
            JournaliserDiagnosticChangementJeu("sonde_aucun_jeu");
            ReinitialiserContexteSurveillance();
            await ChargerJeuEnCoursAsync(false, true);
            RedemarrerMinuteurActualisationApi();
            return;
        }

        JournaliserDiagnosticChangementJeu("sonde_jeu_detecte", $"titre={jeuLocal.TitreJeuEstime}");
        await AppliquerJeuLocalAsync(jeuLocal);
        JournaliserDiagnosticChangementJeu("sonde_apres_etat_local");
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
        _minuteurRotationVisuelsJeuEnCours.Stop();
    }

    /// <summary>
    /// Affiche immédiatement le jeu local détecté au démarrage pour éviter une bascule visuelle tardive.
    /// </summary>
    private async Task AmorcerEtatJeuLocalAuDemarrageAsync()
    {
        JeuDetecteLocalement? jeuLocal = _sondeJeuLocal.DetecterJeu();

        if (jeuLocal is null)
        {
            _dernierJeuLocalDetecte = null;
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
        bool forcerChargementJeu = true,
        bool sonderJeuLocal = true
    )
    {
        App.JournaliserDemarrage("ChargerJeuEnCours début");
        JournaliserDiagnosticChangementJeu(
            "charger_jeu_debut",
            $"forcer={forcerChargementJeu};chargement={afficherEtatChargement}"
        );

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
            if (afficherEtatChargement)
            {
                ReinitialiserPremierSuccesNonDebloque();
                ReinitialiserGrilleTousSucces();
                DefinirTempsJeuSousImage(string.Empty);
                DefinirEtatJeuDansProgression(string.Empty);
                ReinitialiserSuccesRecents();
            }

            ProfilUtilisateurRetroAchievements profil =
                await ClientRetroAchievements.ObtenirProfilUtilisateurAsync(
                    _configurationConnexion.Pseudo,
                    _configurationConnexion.CleApiWeb
                );

            App.JournaliserDemarrage("ChargerJeuEnCours apres Profil");
            JournaliserDiagnosticChangementJeu("charger_jeu_profil_charge");
            _profilUtilisateurAccessible = true;
            _dernierProfilUtilisateurCharge = profil;
            _dernierResumeUtilisateurCharge = null;
            DefinirEtatConnexion("Connecté");
            await AppliquerProfilUtilisateurAsync(profil, forcerChargementJeu, sonderJeuLocal);
            App.JournaliserDemarrage("ChargerJeuEnCours apres AppliquerProfil");
            DemarrerChargementSuccesRecentsEnArrierePlan(
                profil,
                _versionChargementContenuJeu,
                profil.IdentifiantDernierJeu
            );
            App.JournaliserDemarrage("ChargerJeuEnCours apres SuccesRecents");
            JournaliserDiagnosticChangementJeu("charger_jeu_fin");
        }
        catch (UtilisateurRetroAchievementsInaccessibleException exception)
        {
            _profilUtilisateurAccessible = false;
            _dernierProfilUtilisateurCharge = null;
            _dernierResumeUtilisateurCharge = null;
            _minuteurActualisationApi.Stop();
            ReinitialiserContexteSurveillance();
            DefinirEtatConnexion("Profil inaccessible");

            JeuDetecteLocalement? jeuLocal = sonderJeuLocal ? _sondeJeuLocal.DetecterJeu() : null;

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
            JeuDetecteLocalement? jeuLocal = sonderJeuLocal ? _sondeJeuLocal.DetecterJeu() : null;

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
            App.JournaliserDemarrage("ChargerJeuEnCours fin");

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
        bool forcerChargementJeu,
        bool sonderJeuLocal
    )
    {
        JeuDetecteLocalement? jeuLocalDetecte = sonderJeuLocal
            ? _sondeJeuLocal.DetecterJeu()
            : null;
        await _serviceRcheevos.DefinirSourceLocaleAsync(jeuLocalDetecte);
        bool emulateurLocalDetecte = sonderJeuLocal && jeuLocalDetecte is not null;
        DefinirTitreZoneJeu(emulateurLocalDetecte);

        if (
            emulateurLocalDetecte
            && jeuLocalDetecte is not null
            && !JeuLocalEstFiable(jeuLocalDetecte)
        )
        {
            AppliquerEtatEmulateurEnAttente(jeuLocalDetecte);
            return;
        }

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

        if (
            _configurationConnexion.DernierSuccesAffiche?.IdentifiantJeu == identifiantJeuEffectif
            && identifiantJeuEffectif > 0
        )
        {
            RafraichirIndicateurRcheevosPremierSuccesSiPossible(
                _configurationConnexion.DernierSuccesAffiche.IdentifiantSucces
            );
        }

        if (identifiantJeuEffectif <= 0)
        {
            JeuDetecteLocalement? jeuLocal = sonderJeuLocal ? _sondeJeuLocal.DetecterJeu() : null;

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

        int versionChargement = ++_versionChargementContenuJeu;
        DemarrerChargementJeuUtilisateurEnArrierePlan(
            identifiantJeuEffectif,
            titreJeuProvisoire,
            infosJeuDejaAfficheesPourCeJeu,
            progressionDejaAfficheePourCeJeu,
            versionChargement
        );
    }

    /// <summary>
    /// Lance le chargement détaillé du jeu sans bloquer la fenêtre principale.
    /// </summary>
    private void DemarrerChargementJeuUtilisateurEnArrierePlan(
        int identifiantJeuEffectif,
        string titreJeuProvisoire,
        bool infosJeuDejaAfficheesPourCeJeu,
        bool progressionDejaAfficheePourCeJeu,
        int versionChargement
    )
    {
        _ = ChargerJeuUtilisateurEnArrierePlanAsync(
            identifiantJeuEffectif,
            titreJeuProvisoire,
            infosJeuDejaAfficheesPourCeJeu,
            progressionDejaAfficheePourCeJeu,
            versionChargement
        );
    }

    /// <summary>
    /// Charge les détails complets du jeu puis les applique seulement s'ils sont encore d'actualité.
    /// </summary>
    private async Task ChargerJeuUtilisateurEnArrierePlanAsync(
        int identifiantJeuEffectif,
        string titreJeuProvisoire,
        bool infosJeuDejaAfficheesPourCeJeu,
        bool progressionDejaAfficheePourCeJeu,
        int versionChargement
    )
    {
        try
        {
            JeuUtilisateurRetroAchievements jeu =
                await ClientRetroAchievements.ObtenirJeuEtProgressionUtilisateurAsync(
                    _configurationConnexion.Pseudo,
                    _configurationConnexion.CleApiWeb,
                    identifiantJeuEffectif
                );

            if (!ChargementContenuJeuEstToujoursActuel(versionChargement, identifiantJeuEffectif))
            {
                return;
            }

            await AppliquerProgressionJeuAsync(jeu);
            DemarrerValidationLocaleJeuEnArrierePlan(jeu);
        }
        catch
        {
            if (!ChargementContenuJeuEstToujoursActuel(versionChargement, identifiantJeuEffectif))
            {
                return;
            }

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
    /// Vérifie qu'un chargement différé correspond encore au jeu actuellement attendu.
    /// </summary>
    private bool ChargementContenuJeuEstToujoursActuel(
        int versionChargement,
        int identifiantJeuEffectif
    )
    {
        return versionChargement == _versionChargementContenuJeu
            && identifiantJeuEffectif > 0
            && _dernierIdentifiantJeuApi == identifiantJeuEffectif;
    }

    /// <summary>
    /// Lance la validation locale du jeu sans bloquer le rendu initial de la carte principale.
    /// </summary>
    private void DemarrerValidationLocaleJeuEnArrierePlan(JeuUtilisateurRetroAchievements jeu)
    {
        _ = ValiderJeuLocalEnArrierePlanAsync(jeu);
    }

    /// <summary>
    /// Exécute la validation locale en arrière-plan et ignore toute erreur pour ne pas pénaliser l'UI.
    /// </summary>
    private async Task ValiderJeuLocalEnArrierePlanAsync(JeuUtilisateurRetroAchievements jeu)
    {
        try
        {
            await CompleterValidationLocaleJeuAsync(jeu);
        }
        catch
        {
            // La validation locale enrichit l'affichage, mais ne doit jamais bloquer l'UX.
        }
    }

    /// <summary>
    /// Charge les derniers succès débloqués et privilégie ceux du jeu en cours si possible.
    /// </summary>
    private async Task ChargerSuccesRecentsAsync(
        ProfilUtilisateurRetroAchievements profil,
        int versionChargement,
        int identifiantJeuProfil
    )
    {
        try
        {
            DateTimeOffset maintenant = DateTimeOffset.UtcNow;
            IReadOnlyList<SuccesRecentRetroAchievements> succesRecents =
                await ClientRetroAchievements.ObtenirSuccesDebloquesEntreAsync(
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
                    if (
                        !ChargementSuccesRecentsEstToujoursActuel(
                            versionChargement,
                            identifiantJeuProfil
                        )
                    )
                    {
                        return;
                    }

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
                if (
                    !ChargementSuccesRecentsEstToujoursActuel(
                        versionChargement,
                        identifiantJeuProfil
                    )
                )
                {
                    return;
                }

                ReinitialiserSuccesRecents();
                TexteEtatSuccesRecents.Text =
                    "Aucun succès récent n'a été détecté sur les 7 derniers jours.";
                return;
            }

            if (!ChargementSuccesRecentsEstToujoursActuel(versionChargement, identifiantJeuProfil))
            {
                return;
            }

            AppliquerSuccesRecents(
                succesAffiches,
                $"Affichage des {succesAffiches.Count} derniers succès connus."
            );
        }
        catch
        {
            if (!ChargementSuccesRecentsEstToujoursActuel(versionChargement, identifiantJeuProfil))
            {
                return;
            }

            ReinitialiserSuccesRecents();
            TexteEtatSuccesRecents.Text = "Impossible de charger les succès récents.";
        }
    }

    /// <summary>
    /// Charge les succès récents sans bloquer le chargement principal.
    /// </summary>
    private void DemarrerChargementSuccesRecentsEnArrierePlan(
        ProfilUtilisateurRetroAchievements profil,
        int versionChargement,
        int identifiantJeuProfil
    )
    {
        _ = ChargerSuccesRecentsEnArrierePlanAsync(profil, versionChargement, identifiantJeuProfil);
    }

    /// <summary>
    /// Ignore les résultats de succès récents si un chargement plus récent a déjà pris la main.
    /// </summary>
    private async Task ChargerSuccesRecentsEnArrierePlanAsync(
        ProfilUtilisateurRetroAchievements profil,
        int versionChargement,
        int identifiantJeuProfil
    )
    {
        try
        {
            await ChargerSuccesRecentsAsync(profil, versionChargement, identifiantJeuProfil);
        }
        catch
        {
            // Les succès récents restent annexes au rendu initial.
        }
    }

    /// <summary>
    /// Vérifie que les succès récents chargés correspondent encore au contexte de jeu courant.
    /// </summary>
    private bool ChargementSuccesRecentsEstToujoursActuel(
        int versionChargement,
        int identifiantJeuProfil
    )
    {
        return versionChargement == _versionChargementContenuJeu
            && (
                identifiantJeuProfil <= 0
                || _dernierIdentifiantJeuApi <= 0
                || _dernierIdentifiantJeuApi == identifiantJeuProfil
            );
    }

    /// <summary>
    /// Applique les informations détaillées du jeu et sa progression.
    /// </summary>
    private async Task AppliquerProgressionJeuAsync(JeuUtilisateurRetroAchievements jeu)
    {
        JournaliserDiagnosticChangementJeu("progression_debut", $"jeu={jeu.IdentifiantJeu}");
        _dernierTitreJeuApi = jeu.Titre;
        _dernierIdentifiantJeuAvecInfos = jeu.IdentifiantJeu;
        _dernierIdentifiantJeuAvecProgression = jeu.IdentifiantJeu;
        _serviceRcheevos.DefinirJeuActif(jeu.IdentifiantJeu, jeu.IdentifiantConsole, jeu.Titre);
        AppliquerVisuelsJeuEnCoursInitiaux(jeu);
        DemarrerEnrichissementVisuelsJeuEnCours(jeu);
        JournaliserDiagnosticChangementJeu("progression_visuels_ok");
        AppliquerMetaConsoleJeuEnCoursInitiale(jeu);
        DemarrerEnrichissementMetaConsoleJeuEnCours(jeu);

        string detailsTempsJeu = string.Empty;
        string detailsRecompense = DeterminerStatutJeu(jeu);

        DefinirTempsJeuSousImage(detailsTempsJeu);
        DefinirEtatJeuDansProgression(detailsRecompense);
        DefinirDetailsJeuEnCours(string.Empty);
        await MettreAJourSuccesJeuAsync(jeu);
        JournaliserDiagnosticChangementJeu("progression_succes_ok");

        TexteResumeProgressionJeuEnCours.Text = $"{jeu.NombreSuccesObtenus} / {jeu.NombreSucces}";
        TextePourcentageJeuEnCours.Text = NormaliserPourcentage(jeu.CompletionUtilisateur);
        BarreProgressionJeuEnCours.Value = ExtrairePourcentage(jeu.CompletionUtilisateur);

        await SauvegarderDernierJeuAfficheAsync(jeu, detailsTempsJeu, detailsRecompense);
        JournaliserDiagnosticChangementJeu("progression_fin");
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
                await ClientRetroAchievements.ObtenirHashesJeuAsync(
                    _configurationConnexion.CleApiWeb,
                    jeu.IdentifiantJeu
                );
            ResultatHashRcheevos hashRcheevos = _serviceHachageRcheevos.CalculerHash(
                jeuLocal.CheminJeuRetenu,
                jeu.IdentifiantConsole
            );

            bool hashReconnu = hashesOfficiels.Any(hash =>
                hash.EmpreinteMd5.Equals(
                    jeuLocal.EmpreinteLocale.EmpreinteMd5,
                    StringComparison.OrdinalIgnoreCase
                )
            );
            bool hashRcheevosReconnu =
                !string.IsNullOrWhiteSpace(hashRcheevos.Hash)
                && hashesOfficiels.Any(hash =>
                    hash.EmpreinteMd5.Equals(hashRcheevos.Hash, StringComparison.OrdinalIgnoreCase)
                );

            string detailsValidation =
                $"\nValidation locale : {(hashReconnu ? "MD5 reconnu" : "MD5 non reconnu")}"
                + $"\nMD5 local : {jeuLocal.EmpreinteLocale.EmpreinteMd5}"
                + $"\nSHA-1 local : {jeuLocal.EmpreinteLocale.EmpreinteSha1}";

            if (!string.IsNullOrWhiteSpace(hashRcheevos.Hash))
            {
                detailsValidation +=
                    $"\nHash rcheevos : {hashRcheevos.Hash}"
                    + $"\nValidation rcheevos : {(hashRcheevosReconnu ? "hash reconnu" : "hash non reconnu")}";
            }
            else if (!string.IsNullOrWhiteSpace(hashRcheevos.Message))
            {
                detailsValidation += $"\nHash rcheevos : {hashRcheevos.Message}";
            }

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
                await ClientRetroAchievements.ObtenirJeuxRecemmentJouesAsync(
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
            ResultatHashRcheevos hashRcheevos = _serviceHachageRcheevos.CalculerHash(
                jeuLocal.CheminJeuRetenu,
                identifiantConsole
            );
            IReadOnlyList<JeuSystemeRetroAchievements> jeuxSysteme =
                await ClientRetroAchievements.ObtenirJeuxSystemeAvecHashesAsync(
                    _configurationConnexion.CleApiWeb,
                    identifiantConsole
                );

            JeuSystemeRetroAchievements? jeuTrouve = jeuxSysteme.FirstOrDefault(jeu =>
                jeu.Hashes.Any(hash =>
                    hash.Equals(
                        jeuLocal.EmpreinteLocale.EmpreinteMd5,
                        StringComparison.OrdinalIgnoreCase
                    )
                    || (
                        !string.IsNullOrWhiteSpace(hashRcheevos.Hash)
                        && hash.Equals(hashRcheevos.Hash, StringComparison.OrdinalIgnoreCase)
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
            await ClientRetroAchievements.ObtenirConsolesAsync(_configurationConnexion.CleApiWeb);
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
        JournaliserDiagnosticChangementJeu("etat_local_debut", $"titre={jeuLocal.TitreJeuEstime}");
        _dernierJeuLocalDetecte = jeuLocal;
        jeuLocal.CheminJeuRetenu = DeterminerCheminJeuLocalRetenu(jeuLocal);
        string signatureJeuLocal = ConstruireSignatureJeuLocal(jeuLocal);
        AppliquerEtatJeuLocal(jeuLocal, raisonIndisponibiliteApi);
        JournaliserDiagnosticChangementJeu("etat_local_applique");

        Task definitionSourceLocaleTask = _serviceRcheevos.DefinirSourceLocaleAsync(jeuLocal);
        Task<JeuSystemeRetroAchievements?> resolutionHashTask =
            ResoudreJeuRetroAchievementsParHashAsync(jeuLocal);

        await definitionSourceLocaleTask;
        JeuSystemeRetroAchievements? jeuResoluParHash = await resolutionHashTask;

        if (jeuResoluParHash is not null)
        {
            jeuLocal.IdentifiantJeuRetroAchievements = jeuResoluParHash.IdentifiantJeu;
            jeuLocal.TitreJeuRetroAchievements = jeuResoluParHash.Titre;
            jeuLocal.NomConsoleRetroAchievements = jeuResoluParHash.NomConsole;
            jeuLocal.TitreJeuEstime = jeuResoluParHash.Titre;

            if (
                string.Equals(
                    _signatureDernierJeuLocal,
                    signatureJeuLocal,
                    StringComparison.Ordinal
                )
            )
            {
                DefinirTitreJeuEnCours(jeuLocal.TitreJeuEstime);
            }
        }

        JournaliserDiagnosticChangementJeu(
            "etat_local_hash_resolu",
            jeuResoluParHash is null ? "non" : $"jeu={jeuResoluParHash.IdentifiantJeu}"
        );

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
            _serviceRcheevos.ReinitialiserJeuActif();
            _serviceRcheevos.ReinitialiserDefinitionsSucces();
            _dernierIdentifiantJeuApi = 0;
            _dernierIdentifiantJeuAvecInfos = 0;
            _dernierIdentifiantJeuAvecProgression = 0;
            _dernierTitreJeuApi = string.Empty;
            ReinitialiserImageJeuEnCours();
            ReinitialiserMetaConsoleJeuEnCours();
            ReinitialiserSuccesAffichesEtPersistes();
            DefinirTempsJeuSousImage(string.Empty);
            DefinirEtatJeuDansProgression(string.Empty);
        }

        DefinirTitreJeuEnCours(
            string.IsNullOrWhiteSpace(jeuLocal.TitreJeuEstime)
                ? string.Empty
                : jeuLocal.TitreJeuEstime
        );
        JournaliserDiagnosticChangementJeu("ui_titre_local");
        DefinirDetailsJeuEnCours(
            ConstruireTexteDetailsJeuLocal(jeuLocal, raisonIndisponibiliteApi)
        );
        TexteResumeProgressionJeuEnCours.Text = "-- / --";
        TextePourcentageJeuEnCours.Text = "Progression du jeu indisponible localement";
        BarreProgressionJeuEnCours.Value = 0;
    }

    /// <summary>
    /// Retourne vrai seulement si la sonde locale a identifié un jeu exploitable, pas seulement l'émulateur.
    /// </summary>
    private static bool JeuLocalEstFiable(JeuDetecteLocalement jeuLocal)
    {
        return jeuLocal.IdentifiantJeuRetroAchievements > 0
            || !string.IsNullOrWhiteSpace(jeuLocal.CheminJeuRetenu)
            || !string.IsNullOrWhiteSpace(jeuLocal.CheminJeuLigneCommande)
            || !string.IsNullOrWhiteSpace(jeuLocal.CheminJeuEstime)
            || !string.IsNullOrWhiteSpace(jeuLocal.TitreJeuEstime);
    }

    /// <summary>
    /// Affiche un état d'attente quand l'émulateur est lancé mais qu'aucun jeu fiable n'est encore détecté.
    /// </summary>
    private void AppliquerEtatEmulateurEnAttente(JeuDetecteLocalement jeuLocal)
    {
        _dernierJeuLocalDetecte = jeuLocal;
        _serviceRcheevos.ReinitialiserJeuActif();
        _serviceRcheevos.ReinitialiserDefinitionsSucces();
        _dernierIdentifiantJeuApi = 0;
        _dernierIdentifiantJeuAvecInfos = 0;
        _dernierIdentifiantJeuAvecProgression = 0;
        _dernierTitreJeuApi = string.Empty;

        TitreZoneJeuEnCours.Text = "Émulateur démarré (en attente d'un jeu)";
        ReinitialiserImageJeuEnCours();
        ReinitialiserCarrouselVisuelsJeuEnCours();
        ReinitialiserMetaConsoleJeuEnCours();
        ReinitialiserSuccesAffichesEtPersistes();
        ReinitialiserGrilleTousSucces();
        DefinirTempsJeuSousImage(string.Empty);
        DefinirEtatJeuDansProgression("Progression");
        DefinirTitreJeuEnCours(string.Empty);
        DefinirDetailsJeuEnCours($"Émulateur : {jeuLocal.NomEmulateur}");
        TexteResumeProgressionJeuEnCours.Text = "-- / --";
        TextePourcentageJeuEnCours.Text = "En attente d'un jeu dans l'émulateur";
        BarreProgressionJeuEnCours.Value = 0;
    }

    private bool JeuLocalCorrespondAuJeuAffiche(JeuDetecteLocalement jeuLocal)
    {
        if (jeuLocal.IdentifiantJeuRetroAchievements > 0)
        {
            int identifiantJeuAffiche =
                _dernierIdentifiantJeuAvecInfos > 0
                    ? _dernierIdentifiantJeuAvecInfos
                    : _configurationConnexion.DernierJeuAffiche?.IdentifiantJeu ?? 0;

            if (
                identifiantJeuAffiche > 0
                && identifiantJeuAffiche != jeuLocal.IdentifiantJeuRetroAchievements
            )
            {
                return false;
            }
        }

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
        DefinirTitreZoneJeu(true);
        _dernierJeuLocalDetecte = null;
        _serviceRcheevos.ReinitialiserJeuActif();
        _serviceRcheevos.ReinitialiserSourceMemoire();
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
        _dernierJeuLocalDetecte = null;
        _serviceRcheevos.ReinitialiserJeuActif();
        _serviceRcheevos.ReinitialiserSourceMemoire();
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
    /// Ouvre la modale de connexion pour modifier le compte sans perdre l'état visuel courant.
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
