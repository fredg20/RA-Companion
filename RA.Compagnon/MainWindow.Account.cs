/*
 * Regroupe la logique de connexion, de compte et d'aide utilisateur, ainsi
 * que les modales associées et les outils de diagnostic reliés aux émulateurs
 * et à l'état visible du compte dans l'interface principale.
 */
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using RA.Compagnon.Modeles.Api.V2.User;
using RA.Compagnon.Modeles.Local;
using RA.Compagnon.Modeles.Presentation;
using RA.Compagnon.Services;
using SystemControls = System.Windows.Controls;
using UiControls = Wpf.Ui.Controls;

namespace RA.Compagnon;

/*
 * Porte la partie de la fenêtre principale qui gère la connexion utilisateur,
 * la modale Compte, la modale Aide et la notice d'état visible dans l'entête.
 */
public partial class MainWindow
{
    /*
     * Retourne un pinceau issu du thème courant ou une couleur de repli
     * lorsque la ressource demandée n'est pas disponible.
     */
    private SolidColorBrush ObtenirPinceauTheme(string cleRessource, Color couleurParDefaut)
    {
        if (TryFindResource(cleRessource) is SolidColorBrush pinceauLocal)
        {
            return pinceauLocal;
        }

        if (Application.Current.TryFindResource(cleRessource) is SolidColorBrush pinceauApp)
        {
            return pinceauApp;
        }

        return new SolidColorBrush(couleurParDefaut);
    }

    /*
     * Retourne une couleur issue du thème courant ou une couleur de repli
     * pour les effets WPF qui attendent directement un Color.
     */
    private Color ObtenirCouleurTheme(string cleRessource, Color couleurParDefaut)
    {
        if (TryFindResource(cleRessource) is Color couleurLocale)
        {
            return couleurLocale;
        }

        if (Application.Current.TryFindResource(cleRessource) is Color couleurApp)
        {
            return couleurApp;
        }

        if (TryFindResource(cleRessource) is SolidColorBrush pinceauLocal)
        {
            return pinceauLocal.Color;
        }

        if (Application.Current.TryFindResource(cleRessource) is SolidColorBrush pinceauApp)
        {
            return pinceauApp.Color;
        }

        return couleurParDefaut;
    }

    /*
     * Active ou désactive la racine visuelle qui porte les modales.
     */
    private void DefinirEtatModalesActif(bool actif)
    {
        if (RacineModales is null)
        {
            return;
        }

        RacineModales.Visibility = actif ? Visibility.Visible : Visibility.Collapsed;
        RacineModales.IsHitTestVisible = actif;
    }

    /*
     * Affiche ou masque le voile de fond dédié à la modale de connexion.
     */
    private void DefinirVoileConnexionActif(bool actif)
    {
        if (VoileFenetreConnexion is null)
        {
            return;
        }

        VoileFenetreConnexion.Visibility = actif ? Visibility.Visible : Visibility.Collapsed;
    }

    /*
     * Calcule une largeur de contenu de modale à partir de la largeur courante
     * de la fenêtre principale afin d'éviter les largeurs figées sur petits
     * écrans.
     */
    private double CalculerLargeurContenuModale(double largeurCible, double largeurMinimale)
    {
        double largeurFenetreReference =
            ActualWidth > 0 ? ActualWidth : ConstantesDesign.LargeurFenetreParDefaut;
        double largeurDisponible =
            largeurFenetreReference
            - (MargeInterieureModaleConnexion * 2)
            - ConstantesDesign.EspaceTresEtendu;

        if (double.IsNaN(largeurDisponible) || double.IsInfinity(largeurDisponible))
        {
            largeurDisponible = largeurCible;
        }

        largeurDisponible = Math.Max(ConstantesDesign.EspaceColonneLarge, largeurDisponible);

        double largeurMinimaleEffective = Math.Min(largeurMinimale, largeurDisponible);

        return Math.Clamp(largeurDisponible, largeurMinimaleEffective, largeurCible);
    }

    /*
     * Retourne la largeur extérieure d'une modale à partir de sa largeur de
     * contenu pour faciliter les bornes de dialogue.
     */
    private static double CalculerLargeurExterieureModale(double largeurContenu)
    {
        return largeurContenu + (MargeInterieureModaleConnexion * 2);
    }

    /*
     * Indique si une dimension mesurée peut servir de référence visuelle
     * fiable pour aligner une modale sur un élément déjà rendu.
     */
    private static bool DimensionVisuelleEstUtilisable(double dimension)
    {
        return dimension > 0 && !double.IsNaN(dimension) && !double.IsInfinity(dimension);
    }

    /*
     * Retourne le rectangle visible de la carte principale afin que la modale
     * d'aide puisse s'aligner sur sa hauteur et sur son axe vertical.
     */
    private Rect ObtenirRectangleCartePrincipalePourModaleAide()
    {
        FrameworkElement? elementReference = null;

        if (
            CadreCarteJeuEnCours.IsLoaded
            && DimensionVisuelleEstUtilisable(CadreCarteJeuEnCours.ActualHeight)
        )
        {
            elementReference = CadreCarteJeuEnCours;
        }
        else if (
            CarteJeuEnCours.IsLoaded && DimensionVisuelleEstUtilisable(CarteJeuEnCours.ActualHeight)
        )
        {
            elementReference = CarteJeuEnCours;
        }
        else if (
            GrilleCarteJeuEnCours.IsLoaded
            && DimensionVisuelleEstUtilisable(GrilleCarteJeuEnCours.ActualHeight)
        )
        {
            elementReference = GrilleCarteJeuEnCours;
        }

        if (elementReference is null)
        {
            return Rect.Empty;
        }

        Point origineCarte = ConvertirPointElementEnEcranWpf(elementReference, new Point(0, 0));
        return new Rect(
            origineCarte.X,
            origineCarte.Y,
            elementReference.ActualWidth,
            elementReference.ActualHeight
        );
    }

    /*
     * Calcule une hauteur maximale de contenu de modale cohérente avec la
     * hauteur disponible de la fenêtre courante.
     */
    private double CalculerHauteurMaximaleContenuModale()
    {
        double hauteurFenetreReference =
            ActualHeight > 0 ? ActualHeight : ConstantesDesign.HauteurFenetreParDefaut;
        double hauteurDisponible = hauteurFenetreReference - ConstantesDesign.EspaceTresEtendu;

        if (double.IsNaN(hauteurDisponible) || double.IsInfinity(hauteurDisponible))
        {
            hauteurDisponible = ConstantesDesign.HauteurMaximaleModale;
        }

        return Math.Clamp(
            hauteurDisponible,
            ConstantesDesign.EspaceFenetreStandard,
            ConstantesDesign.HauteurMaximaleModale
        );
    }

    /*
     * Indique si une modale doit passer en disposition compacte en fonction de
     * sa largeur de contenu calculée.
     */
    private static bool EstModaleCompacte(double largeurContenu, double seuilCompact)
    {
        return largeurContenu < seuilCompact;
    }

    /*
     * Ouvre la modale de connexion et orchestre la validation puis la
     * sauvegarde locale des informations de compte.
     */
    private async Task AfficherModaleConnexionAsync(
        bool masquerContenuPrincipal = true,
        bool fermerApplicationSiAnnuleSansConfiguration = true
    )
    {
        ArreterActualisationAutomatique();
        bool connexionDejaConfiguree = ConfigurationConnexionEstComplete();
        string pseudoValide = string.Empty;
        string cleApiValide = string.Empty;
        SolidColorBrush fondFenetre = Brushes.Transparent;
        SolidColorBrush fondCarte = ObtenirPinceauTheme(
            "PinceauCartePrincipale",
            ConstantesDesign.CouleurRepliCartePrincipale
        );
        SolidColorBrush fondChamp = ObtenirPinceauTheme(
            "ControlFillColorInputActiveBrush",
            ConstantesDesign.CouleurRepliChamp
        );
        SolidColorBrush bordure = ObtenirPinceauTheme(
            "CardStrokeColorDefaultBrush",
            ConstantesDesign.CouleurRepliBordure
        );
        SolidColorBrush textePrincipal = ObtenirPinceauTheme(
            "TextFillColorPrimaryBrush",
            ConstantesDesign.CouleurRepliTextePrincipal
        );
        SolidColorBrush texteSecondaire = ObtenirPinceauTheme(
            "TextFillColorSecondaryBrush",
            ConstantesDesign.CouleurRepliTexteSecondaire
        );
        SolidColorBrush fondBoutonPrimaire = ObtenirPinceauTheme(
            "SystemAccentColorBrush",
            ConstantesDesign.CouleurRepliAccentPrimaire
        );
        CornerRadius rayonCoins = ObtenirRayonCoins(
            "RayonCoinsStandard",
            ConstantesDesign.EspaceStandard
        );
        double largeurContenuConnexion = CalculerLargeurContenuModale(
            LargeurContenuModaleConnexion,
            ConstantesDesign.EspaceFenetreStandard - ConstantesDesign.EspaceEtendu
        );
        double hauteurMaximaleConnexion = CalculerHauteurMaximaleContenuModale();

        SystemControls.TextBox champPseudo = new()
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(10, 6, 10, 6),
            Margin = new Thickness(0, 0, 0, 14),
            Text = _configurationConnexion.Pseudo,
            Foreground = textePrincipal,
            Background = fondChamp,
            BorderBrush = bordure,
            BorderThickness = new Thickness(1),
            CaretBrush = textePrincipal,
        };

        SystemControls.PasswordBox champCleApi = new()
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Password = _configurationConnexion.CleApiWeb,
            Padding = new Thickness(10, 6, 10, 6),
            Foreground = textePrincipal,
            Background = fondChamp,
            BorderBrush = bordure,
            BorderThickness = new Thickness(1),
            CaretBrush = textePrincipal,
        };

        SystemControls.TextBlock texteErreur = new()
        {
            Margin = new Thickness(0, 12, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Visibility = Visibility.Collapsed,
            Foreground = ObtenirPinceauTheme("SystemFillColorCriticalBrush", Colors.IndianRed),
            TextWrapping = TextWrapping.Wrap,
        };

        SystemControls.Button boutonEnregistrer = new()
        {
            Content = "Enregistrer",
            MinWidth = 120,
            IsDefault = true,
            Padding = new Thickness(14, 6, 14, 6),
            Background = fondBoutonPrimaire,
            Foreground = ObtenirPinceauTheme("TextOnAccentFillColorPrimaryBrush", Colors.White),
            BorderBrush = fondBoutonPrimaire,
        };

        SystemControls.Button boutonAnnuler = new()
        {
            Content = connexionDejaConfiguree ? "Annuler" : "Fermer",
            MinWidth = 120,
            IsCancel = true,
            Padding = new Thickness(14, 6, 14, 6),
            Margin = new Thickness(8, 0, 0, 0),
            Background = fondCarte,
            Foreground = textePrincipal,
            BorderBrush = bordure,
        };

        Window fenetreConnexion = new()
        {
            Owner = this,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.NoResize,
            SizeToContent = SizeToContent.WidthAndHeight,
            ShowInTaskbar = false,
            ShowActivated = true,
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = fondFenetre,
            Foreground = textePrincipal,
            MaxWidth = CalculerLargeurExterieureModale(largeurContenuConnexion) + 36,
            Content = new SystemControls.Border
            {
                Padding = ConstantesDesign.PaddingCarteSecondaire,
                Background = fondCarte,
                BorderBrush = bordure,
                BorderThickness = new Thickness(1),
                CornerRadius = rayonCoins,
                SnapsToDevicePixels = true,
                Child = new SystemControls.ScrollViewer
                {
                    VerticalScrollBarVisibility = SystemControls.ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = SystemControls.ScrollBarVisibility.Disabled,
                    MaxHeight = hauteurMaximaleConnexion,
                    Content = new SystemControls.StackPanel
                    {
                        Width = largeurContenuConnexion,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Children =
                        {
                            new SystemControls.TextBlock
                            {
                                FontSize = ConstantesDesign.TaillePoliceTitreSection,
                                FontWeight = FontWeights.SemiBold,
                                Foreground = textePrincipal,
                                Text = "Connexion",
                                Margin = new Thickness(0, 0, 0, 8),
                            },
                            new SystemControls.TextBlock
                            {
                                Opacity = 0.84,
                                Foreground = texteSecondaire,
                                Text =
                                    "Entre ton pseudo et ta clé Web API pour synchroniser ton dernier jeu joué, ta progression et tes succès récents.",
                                TextWrapping = TextWrapping.Wrap,
                                Margin = new Thickness(0, 0, 0, 14),
                            },
                            new SystemControls.TextBlock
                            {
                                Margin = new Thickness(0, 0, 0, 6),
                                FontWeight = FontWeights.SemiBold,
                                Foreground = textePrincipal,
                                Text = "Pseudo",
                            },
                            champPseudo,
                            new SystemControls.TextBlock
                            {
                                Margin = new Thickness(0, 0, 0, 6),
                                FontWeight = FontWeights.SemiBold,
                                Foreground = textePrincipal,
                                Text = "Clé API",
                            },
                            champCleApi,
                            new SystemControls.TextBlock
                            {
                                Margin = new Thickness(0, 10, 0, 0),
                                Opacity = 0.68,
                                Foreground = texteSecondaire,
                                Text =
                                    "Tu peux retrouver cette clé depuis ton compte RetroAchievements, dans la section dédiée à l'API Web.",
                                TextWrapping = TextWrapping.Wrap,
                            },
                            texteErreur,
                            new SystemControls.WrapPanel
                            {
                                Orientation = SystemControls.Orientation.Horizontal,
                                HorizontalAlignment = HorizontalAlignment.Center,
                                Margin = new Thickness(0, 18, 0, 0),
                                Children = { boutonEnregistrer, boutonAnnuler },
                            },
                        },
                    },
                },
            },
        };
        AppliquerTypographieResponsiveSurObjet(fenetreConnexion);

        fenetreConnexion.Loaded += (_, _) =>
        {
            fenetreConnexion.Dispatcher.BeginInvoke(
                () =>
                {
                    fenetreConnexion.Activate();

                    if (!string.IsNullOrWhiteSpace(champPseudo.Text))
                    {
                        champCleApi.Focus();
                        return;
                    }

                    champPseudo.Focus();
                    champPseudo.SelectAll();
                },
                DispatcherPriority.ApplicationIdle
            );
        };

        boutonAnnuler.Click += (_, _) =>
        {
            fenetreConnexion.DialogResult = false;
            fenetreConnexion.Close();
        };

        boutonEnregistrer.Click += async (_, _) =>
        {
            string pseudo = champPseudo.Text.Trim();
            string cleApi = champCleApi.Password.Trim();

            texteErreur.Visibility = Visibility.Collapsed;
            boutonEnregistrer.IsEnabled = false;
            boutonAnnuler.IsEnabled = false;

            try
            {
                if (string.IsNullOrWhiteSpace(pseudo) || string.IsNullOrWhiteSpace(cleApi))
                {
                    texteErreur.Text = "Renseigne ton pseudo et ta clé Web API pour continuer.";
                    texteErreur.Visibility = Visibility.Visible;
                    return;
                }

                await ServiceUtilisateurRetroAchievements.ObtenirProfilAsync(pseudo, cleApi);
                pseudoValide = pseudo;
                cleApiValide = cleApi;
                fenetreConnexion.DialogResult = true;
                fenetreConnexion.Close();
            }
            catch (UtilisateurRetroAchievementsInaccessibleException exception)
            {
                texteErreur.Text = exception.Message;
                texteErreur.Visibility = Visibility.Visible;
            }
            catch (Exception exception)
            {
                texteErreur.Text = string.IsNullOrWhiteSpace(exception.Message)
                    ? "Impossible de vérifier ce compte pour le moment. Vérifie ta connexion et réessaie."
                    : $"Impossible de vérifier ce compte pour le moment. {exception.Message}";
                texteErreur.Visibility = Visibility.Visible;
            }
            finally
            {
                if (fenetreConnexion.IsVisible)
                {
                    boutonEnregistrer.IsEnabled = true;
                    boutonAnnuler.IsEnabled = true;
                }
            }
        };

        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        bool? resultat;

        try
        {
            DefinirVoileConnexionActif(true);
            resultat = fenetreConnexion.ShowDialog();
        }
        finally
        {
            DefinirVoileConnexionActif(false);
            Activate();
        }

        if (resultat != true)
        {
            if (ConfigurationConnexionEstComplete())
            {
                return;
            }

            if (fermerApplicationSiAnnuleSansConfiguration)
            {
                Close();
            }
            return;
        }

        if (
            !string.Equals(
                _configurationConnexion.Pseudo,
                pseudoValide,
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            _configurationConnexion.DernierJeuAffiche = null;
            _configurationConnexion.DernierSuccesAffiche = null;
            _configurationConnexion.DerniereListeSuccesAffichee = null;
        }

        _configurationConnexion.Pseudo = pseudoValide;
        _configurationConnexion.CleApiWeb = cleApiValide;

        try
        {
            MemoriserGeometrieFenetre();
            await _serviceConfigurationLocale.SauvegarderAsync(_configurationConnexion);
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                string.IsNullOrWhiteSpace(exception.Message)
                    ? "Le compte a été validé, mais son enregistrement local a échoué. La session courante peut fonctionner, mais la reconnexion automatique au prochain démarrage n'est pas garantie."
                    : $"Le compte a été validé, mais son enregistrement local a échoué. {exception.Message}",
                "Enregistrement du compte impossible",
                MessageBoxButton.OK,
                MessageBoxImage.Warning
            );
        }

        MettreAJourResumeConnexion();
        await ChargerJeuEnCoursAsync();
        DemarrerActualisationAutomatique();
    }

    /*
     * Ouvre la modale de compte avec les informations utilisateur
     * les plus récentes disponibles.
     */
    private async Task AfficherModaleCompteAsync()
    {
        DonneesCompteUtilisateur donnees = await ObtenirDonneesComptePourModaleAsync();
        CompteAffiche compte = ServicePresentationCompte.Construire(
            donnees,
            _configurationConnexion.Pseudo
        );
        double largeurContenuCompte = CalculerLargeurContenuModale(
            ConstantesDesign.LargeurCarteSecondaire,
            ConstantesDesign.EspaceFenetreStandard
        );
        double hauteurMaximaleCompte = CalculerHauteurMaximaleContenuModale();
        SystemControls.StackPanel contenu = new()
        {
            Width = largeurContenuCompte,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = ConstantesDesign.AucuneMarge,
        };
        contenu.Children.Add(ConstruireEnTeteAvatarCompte(compte));
        contenu.Children.Add(
            new SystemControls.TextBlock
            {
                Margin = new Thickness(0, 12, 0, 0),
                FontSize = ConstantesDesign.TaillePoliceTitreSection,
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                Text = compte.Titre,
                TextWrapping = TextWrapping.Wrap,
            }
        );
        if (!string.IsNullOrWhiteSpace(compte.Devise))
        {
            contenu.Children.Add(
                new SystemControls.TextBlock
                {
                    Margin = new Thickness(0, 6, 0, 0),
                    Opacity = 0.72,
                    FontSize = ConstantesDesign.TaillePoliceInterfaceBase,
                    FontStyle = FontStyles.Italic,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    TextAlignment = TextAlignment.Center,
                    Text = compte.Devise,
                    TextWrapping = TextWrapping.Wrap,
                }
            );
        }
        contenu.Children.Add(
            new SystemControls.TextBlock
            {
                Margin = new Thickness(0, 12, 0, 12),
                Opacity = 0.82,
                Text = compte.Introduction,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
            }
        );
        if (!string.IsNullOrWhiteSpace(compte.NomUtilisateur))
        {
            UiControls.Button boutonProfilRetroAchievements = new()
            {
                Content = "Ouvrir le profil RetroAchievements",
                Appearance = Wpf.Ui.Controls.ControlAppearance.Secondary,
                HorizontalAlignment = HorizontalAlignment.Center,
                Padding = ConstantesDesign.PaddingBoutonAction,
                Margin = new Thickness(0, 0, 0, 12),
            };
            boutonProfilRetroAchievements.Click += (_, _) =>
                OuvrirProfilRetroAchievements(compte.NomUtilisateur);
            contenu.Children.Add(boutonProfilRetroAchievements);
        }
        for (int indexSection = 0; indexSection < compte.Sections.Count; indexSection++)
        {
            if (indexSection > 0)
            {
                contenu.Children.Add(ConstruireSeparateurBlocCompte());
            }

            contenu.Children.Add(ConstruireBlocCompte(compte.Sections[indexSection]));
        }

        if (compte.JeuxRecemmentJoues.Count > 0)
        {
            contenu.Children.Add(ConstruireSeparateurBlocCompte());
            contenu.Children.Add(ConstruireBlocJeuxRecemmentJoues(compte.JeuxRecemmentJoues));
        }
        SystemControls.Border conteneurContenu = new()
        {
            Padding = ConstantesDesign.PaddingCarteSecondaire,
            HorizontalAlignment = HorizontalAlignment.Center,
            CornerRadius = ObtenirRayonCoins("RayonCoinsStandard", ConstantesDesign.EspaceStandard),
            Child = new SystemControls.ScrollViewer
            {
                VerticalScrollBarVisibility = SystemControls.ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = SystemControls.ScrollBarVisibility.Disabled,
                MaxHeight = hauteurMaximaleCompte,
                Content = contenu,
            },
        };

        UiControls.ContentDialog dialogueCompte = new(RacineModales)
        {
            Title = string.Empty,
            Content = conteneurContenu,
            MinWidth = CalculerLargeurExterieureModale(largeurContenuCompte),
            CloseButtonText = "Fermer",
            PrimaryButtonText = "Déconnexion",
            DefaultButton = UiControls.ContentDialogButton.Primary,
        };
        AppliquerTypographieResponsiveSurObjet(dialogueCompte);

        dialogueCompte.Loaded += DialogueCompte_Chargement;

        UiControls.ContentDialogResult resultat;

        try
        {
            DefinirEtatModalesActif(true);
            resultat = await dialogueCompte.ShowAsync();
        }
        finally
        {
            DefinirEtatModalesActif(false);
            dialogueCompte.Loaded -= DialogueCompte_Chargement;
        }

        if (resultat == UiControls.ContentDialogResult.Primary)
        {
            if (!await ConfirmerDeconnexionAsync())
            {
                return;
            }

            await DeconnecterCompteAsync();
            await AfficherModaleConnexionAsync(false, false);
            return;
        }
    }

    /*
     * Ouvre la modale d'aide et de diagnostic local de l'application.
     */
    private Task AfficherModaleAideAsync()
    {
        _ = VerifierMiseAJourApplicationSiNecessaireAsync();
        double hauteurMaximaleAide = CalculerHauteurMaximaleContenuModale();
        Rect rectangleCartePrincipale = ObtenirRectangleCartePrincipalePourModaleAide();
        Rect zoneTravail = ObtenirZoneTravailFenetreCourante();
        Point origineFenetre = ConvertirPointElementEnEcranWpf(this, new Point(0, 0));
        Rect zoneFenetreVisible =
            ActualWidth > 0 && ActualHeight > 0
                ? new Rect(origineFenetre.X, origineFenetre.Y, ActualWidth, ActualHeight)
                : zoneTravail;
        double largeurContenuAide = CalculerLargeurContenuModale(
            ConstantesDesign.LargeurContenuModaleAide,
            ConstantesDesign.EspaceFenetreStandard
        );
        double largeurFenetreAideCible = CalculerLargeurExterieureModale(largeurContenuAide);
        double largeurFenetreAide = Math.Min(
            largeurFenetreAideCible,
            Math.Min(zoneTravail.Width - 32, zoneFenetreVisible.Width - 24)
        );
        largeurContenuAide = Math.Max(
            ConstantesDesign.EspaceColonneLarge,
            largeurFenetreAide - (MargeInterieureModaleConnexion * 2)
        );
        bool modaleAideCompacte = EstModaleCompacte(
            largeurContenuAide,
            ConstantesDesign.SeuilCompactModaleAide
        );
        double hauteurEnteteEtPiedAide = modaleAideCompacte ? 132 : 148;
        double hauteurFenetreAideCible = DimensionVisuelleEstUtilisable(
            rectangleCartePrincipale.Height
        )
            ? rectangleCartePrincipale.Height
            : hauteurMaximaleAide + (MargeInterieureModaleConnexion * 2) + hauteurEnteteEtPiedAide;
        double hauteurFenetreAide = Math.Min(
            hauteurFenetreAideCible,
            Math.Min(zoneTravail.Height - 32, zoneFenetreVisible.Height - 24)
        );
        double positionGaucheFenetreAide =
            zoneFenetreVisible.Left
            + Math.Max(0, (zoneFenetreVisible.Width - largeurFenetreAide) / 2);
        double positionHautFenetreAide = DimensionVisuelleEstUtilisable(
            rectangleCartePrincipale.Height
        )
            ? rectangleCartePrincipale.Top
            : zoneFenetreVisible.Top
                + Math.Max(0, (zoneFenetreVisible.Height - hauteurFenetreAide) / 2);

        if (
            !DimensionVisuelleEstUtilisable(rectangleCartePrincipale.Height)
            && CadreZonePrincipale.IsLoaded
            && BarreEtatApplication.IsLoaded
            && CadreZonePrincipale.ActualHeight > 0
        )
        {
            Point origineZoneContenu = ConvertirPointElementEnEcranWpf(
                CadreZonePrincipale,
                new Point(0, 0)
            );
            Point origineBarreEtat = ConvertirPointElementEnEcranWpf(
                BarreEtatApplication,
                new Point(0, 0)
            );
            double hauteurDisponibleDansFenetre = origineBarreEtat.Y - origineZoneContenu.Y - 12;

            if (hauteurDisponibleDansFenetre > 0)
            {
                double hauteurDisponibleEcran = Math.Min(
                    hauteurDisponibleDansFenetre,
                    zoneTravail.Height - 32
                );
                hauteurFenetreAide = Math.Min(hauteurFenetreAide, hauteurDisponibleEcran);
                positionHautFenetreAide = origineZoneContenu.Y;
            }
        }

        positionGaucheFenetreAide = Math.Clamp(
            positionGaucheFenetreAide,
            Math.Max(zoneTravail.Left, zoneFenetreVisible.Left),
            Math.Min(zoneTravail.Right, zoneFenetreVisible.Right) - largeurFenetreAide
        );
        positionHautFenetreAide = Math.Clamp(
            positionHautFenetreAide,
            Math.Max(zoneTravail.Top, zoneFenetreVisible.Top),
            Math.Min(zoneTravail.Bottom, zoneFenetreVisible.Bottom) - hauteurFenetreAide
        );

        double hauteurZoneDefilementAide = Math.Max(
            ConstantesDesign.EspaceFenetreStandard,
            hauteurFenetreAide - (MargeInterieureModaleConnexion * 2) - hauteurEnteteEtPiedAide
        );
        _modaleAideCompacteCourante = modaleAideCompacte;
        SolidColorBrush fondFenetre = Brushes.Transparent;
        SolidColorBrush fondCarte = ObtenirPinceauTheme(
            "PinceauCartePrincipale",
            ConstantesDesign.CouleurRepliCartePrincipale
        );
        SolidColorBrush bordure = ObtenirPinceauTheme(
            "CardStrokeColorDefaultBrush",
            ConstantesDesign.CouleurRepliBordure
        );
        SolidColorBrush textePrincipal = ObtenirPinceauTheme(
            "TextFillColorPrimaryBrush",
            ConstantesDesign.CouleurRepliTextePrincipal
        );
        SolidColorBrush texteSecondaire = ObtenirPinceauTheme(
            "TextFillColorSecondaryBrush",
            ConstantesDesign.CouleurRepliTexteSecondaire
        );
        CornerRadius rayonCoins = ObtenirRayonCoins(
            "RayonCoinsStandard",
            ConstantesDesign.EspaceStandard
        );
        List<SystemControls.Expander> sectionsAide = [];

        SystemControls.StackPanel contenu = new()
        {
            Width = largeurContenuAide,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = ConstantesDesign.AucuneMarge,
            Children =
            {
                ConstruireBlocAide(
                    "Démarrage rapide",
                    [
                        "Ouvre Profil puis connecte ton compte RetroAchievements.",
                        "Lance un jeu dans un émulateur compatible et attends quelques secondes.",
                        "Si le jeu affiché n'est pas le bon, utilise Recharger avant de redémarrer l'émulateur.",
                    ],
                    "Les trois premières étapes à suivre.",
                    true,
                    sectionsAide
                ),
                ConstruireBlocAide(
                    "Comprendre l'écran",
                    [
                        "Le grand libellé indique si Compagnon voit un Dernier jeu ou une session En jeu.",
                        "La carte principale affiche le jeu détecté, ses informations et les actions utiles du moment.",
                        "Les deux sections de rétrosuccès montrent le succès mis en avant, la grille complète et la progression Softcore / Hardcore.",
                    ],
                    "Les repères essentiels de l'interface.",
                    false,
                    sectionsAide
                ),
                ConstruireBlocAide(
                    "Si un élément manque",
                    [
                        "Vérifie d'abord que le compte est connecté et qu'aucune synchronisation n'est en cours.",
                        "Ouvre Logs des émulateurs pour voir le chemin exact attendu sur ce PC.",
                        "Si la détection hésite, choisis manuellement le bon exécutable.",
                        "En dernier recours, relance d'abord l'émulateur, puis Compagnon.",
                    ],
                    "La procédure la plus courte pour diagnostiquer un manque.",
                    false,
                    sectionsAide
                ),
                ConstruireBlocAideObs(sectionsAide),
                ConstruireBlocAideMiseAJourApplication(sectionsAide),
                ConstruireBlocAideLogsEmulateurs(sectionsAide),
            },
        };

        SystemControls.Button boutonFermer = new()
        {
            Content = "Fermer",
            MinWidth = 120,
            IsCancel = true,
            Padding = new Thickness(14, 6, 14, 6),
            Background = fondCarte,
            Foreground = textePrincipal,
            BorderBrush = bordure,
        };

        Window fenetreAide = new()
        {
            Owner = this,
            WindowStartupLocation = WindowStartupLocation.Manual,
            ResizeMode = ResizeMode.NoResize,
            SizeToContent = SizeToContent.Manual,
            ShowInTaskbar = false,
            ShowActivated = true,
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = fondFenetre,
            Foreground = textePrincipal,
            Left = positionGaucheFenetreAide,
            Top = positionHautFenetreAide,
            Width = largeurFenetreAide,
            Height = hauteurFenetreAide,
            MinWidth = largeurFenetreAide,
            MaxWidth = largeurFenetreAide,
            MinHeight = hauteurFenetreAide,
            MaxHeight = hauteurFenetreAide,
            Content = new SystemControls.Border
            {
                Padding = ConstantesDesign.PaddingCarteSecondaire,
                Background = fondCarte,
                BorderBrush = bordure,
                BorderThickness = new Thickness(1),
                CornerRadius = rayonCoins,
                SnapsToDevicePixels = true,
                Child = new SystemControls.Grid
                {
                    RowDefinitions =
                    {
                        new SystemControls.RowDefinition { Height = GridLength.Auto },
                        new SystemControls.RowDefinition
                        {
                            Height = new GridLength(hauteurZoneDefilementAide),
                        },
                        new SystemControls.RowDefinition { Height = GridLength.Auto },
                    },
                },
            },
        };
        SystemControls.Grid grilleFenetreAide = (SystemControls.Grid)
            ((SystemControls.Border)fenetreAide.Content).Child;

        grilleFenetreAide.Children.Add(
            new SystemControls.StackPanel
            {
                Width = largeurContenuAide,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, modaleAideCompacte ? 10 : 14),
                Children =
                {
                    new SystemControls.TextBlock
                    {
                        FontSize = ConstantesDesign.TaillePoliceTitreSection,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = textePrincipal,
                        Text = "Aide",
                        Margin = new Thickness(0, 0, 0, modaleAideCompacte ? 6 : 8),
                    },
                    new SystemControls.TextBlock
                    {
                        Opacity = 0.82,
                        Foreground = texteSecondaire,
                        Text =
                            "Retrouve ici les repères essentiels, les actions utiles et les chemins surveillés par Compagnon.",
                        TextWrapping = TextWrapping.Wrap,
                    },
                },
            }
        );

        SystemControls.ScrollViewer scrollViewerAide = new()
        {
            VerticalScrollBarVisibility = SystemControls.ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = SystemControls.ScrollBarVisibility.Disabled,
            Height = hauteurZoneDefilementAide,
            Content = contenu,
        };
        SystemControls.Grid.SetRow(scrollViewerAide, 1);
        grilleFenetreAide.Children.Add(scrollViewerAide);

        SystemControls.WrapPanel piedFenetreAide = new()
        {
            Orientation = SystemControls.Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, modaleAideCompacte ? 12 : 18, 0, 0),
            Children = { boutonFermer },
        };
        SystemControls.Grid.SetRow(piedFenetreAide, 2);
        grilleFenetreAide.Children.Add(piedFenetreAide);
        AppliquerTypographieResponsiveSurObjet(fenetreAide);

        boutonFermer.Click += (_, _) =>
        {
            fenetreAide.DialogResult = false;
            fenetreAide.Close();
        };

        try
        {
            DefinirEtatModalesActif(true);
            fenetreAide.ShowDialog();
        }
        finally
        {
            DefinirEtatModalesActif(false);
        }

        return Task.CompletedTask;
    }

    /*
     * Demande à l'utilisateur de confirmer la déconnexion du compte courant.
     */
    private async Task<bool> ConfirmerDeconnexionAsync()
    {
        UiControls.ContentDialog dialogueConfirmation = new(RacineModales)
        {
            Title = "Déconnexion",
            Content =
                "Veux-tu vraiment te déconnecter de Compagnon ? Le compte sera retiré localement, mais tes données RetroAchievements ne seront pas supprimées.",
            PrimaryButtonText = "Déconnexion",
            SecondaryButtonText = "Annuler",
            CloseButtonText = string.Empty,
            DefaultButton = UiControls.ContentDialogButton.Secondary,
        };
        AppliquerTypographieResponsiveSurObjet(dialogueConfirmation);

        dialogueConfirmation.Loaded += DialogueConfirmation_Chargement;

        UiControls.ContentDialogResult resultat;

        try
        {
            DefinirEtatModalesActif(true);
            resultat = await dialogueConfirmation.ShowAsync();
        }
        finally
        {
            DefinirEtatModalesActif(false);
            dialogueConfirmation.Loaded -= DialogueConfirmation_Chargement;
        }

        return resultat == UiControls.ContentDialogResult.Primary;
    }

    /*
     * Supprime la configuration locale du compte et remet l'interface
     * dans un état déconnecté.
     */
    private async Task DeconnecterCompteAsync()
    {
        ArreterActualisationAutomatique();
        MemoriserGeometrieFenetre();
        ReinitialiserContexteSurveillance();
        ReinitialiserSuccesAffichesEtPersistes();
        ReinitialiserJeuEnCours();

        _profilUtilisateurAccessible = true;
        _dernieresDonneesJeuAffichees = null;
        _configurationConnexion.Pseudo = string.Empty;
        _configurationConnexion.CleApiWeb = string.Empty;
        _configurationConnexion.DernierJeuAffiche = null;
        _configurationConnexion.DernierSuccesAffiche = null;
        _configurationConnexion.DerniereListeSuccesAffichee = null;
        _dernierJeuAfficheModifie = false;
        _dernierSuccesAfficheModifie = false;
        _derniereListeSuccesAfficheeModifiee = false;

        DefinirEtatConnexion("Non configuré");
        AjusterDisposition();

        await _serviceConfigurationLocale.SauvegarderAsync(_configurationConnexion);
    }

    /*
     * Récupère les données nécessaires à l'affichage détaillé du compte
     * dans la modale dédiée.
     */
    private async Task<DonneesCompteUtilisateur> ObtenirDonneesComptePourModaleAsync()
    {
        if (!ConfigurationConnexionEstComplete())
        {
            return new DonneesCompteUtilisateur
            {
                Profil = _dernierProfilUtilisateurCharge,
                Resume = _dernierResumeUtilisateurCharge,
            };
        }

        DonneesCompteUtilisateur donnees =
            await ServiceUtilisateurRetroAchievements.ObtenirDonneesCompteAsync(
                _configurationConnexion.Pseudo,
                _configurationConnexion.CleApiWeb
            );

        UserProfileV2? profil = donnees.Profil ?? _dernierProfilUtilisateurCharge;
        UserSummaryV2? resume = donnees.Resume ?? _dernierResumeUtilisateurCharge;

        if (profil is not null)
        {
            _dernierProfilUtilisateurCharge = profil;
        }

        if (resume is not null)
        {
            _dernierResumeUtilisateurCharge = resume;
        }

        MettreAJourNoticeCompteEntete();

        return new DonneesCompteUtilisateur
        {
            Profil = profil,
            Resume = resume,
            Points = donnees.Points,
            Recompenses = donnees.Recompenses,
            Progression = donnees.Progression,
        };
    }

    /*
     * Construit le bloc visuel correspondant à une section de la modale
     * de compte.
     */
    private SystemControls.Border ConstruireBlocCompte(SectionInformationsAffichee section)
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
                    Text = section.Titre,
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

        for (int index = 0; index < section.Lignes.Count; index++)
        {
            grille.RowDefinitions.Add(
                new SystemControls.RowDefinition { Height = GridLength.Auto }
            );

            if (index % 2 == 1)
            {
                SystemControls.Border fondLigne = new()
                {
                    Background = ObtenirPinceauTheme(
                        "PinceauFondLigneAlternee",
                        ConstantesDesign.CouleurRepliLigneAlternee
                    ),
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
                Text = section.Lignes[index].Libelle,
                TextWrapping = TextWrapping.Wrap,
            };
            SystemControls.Grid.SetRow(libelle, index);
            SystemControls.Grid.SetColumn(libelle, 0);
            grille.Children.Add(libelle);

            SystemControls.TextBlock valeur = new()
            {
                Margin = new Thickness(10, 8, 10, 8),
                Text = section.Lignes[index].Valeur,
                TextWrapping = TextWrapping.Wrap,
            };
            SystemControls.Grid.SetRow(valeur, index);
            SystemControls.Grid.SetColumn(valeur, 1);
            grille.Children.Add(valeur);
        }

        pile.Children.Add(grille);

        return new SystemControls.Border { Padding = new Thickness(0), Child = pile };
    }

    /*
     * Construit l'en-tête du compte avec avatar, pseudo et statut.
     */
    private SystemControls.Border ConstruireEnTeteAvatarCompte(CompteAffiche compte)
    {
        SystemControls.Border conteneur = new()
        {
            Width = 96,
            Height = 96,
            Padding = new Thickness(8),
            HorizontalAlignment = HorizontalAlignment.Center,
            CornerRadius = new CornerRadius(48),
            Background = ObtenirPinceauTheme(
                "PinceauFondSurfaceTranslucide",
                ConstantesDesign.CouleurRepliSurfaceTranslucide
            ),
            BorderBrush = ObtenirPinceauTheme(
                "PinceauBordureSurfaceTranslucide",
                ConstantesDesign.CouleurRepliBordureSurfaceTranslucide
            ),
            BorderThickness = new Thickness(1),
        };

        SystemControls.Image? imageAvatar = ConstruireImageAvatarCompte(
            compte.UrlAvatar,
            80,
            80,
            new Thickness(0)
        );

        if (imageAvatar is not null)
        {
            conteneur.Child = imageAvatar;
            return conteneur;
        }

        return new SystemControls.Border
        {
            Width = 96,
            Height = 96,
            HorizontalAlignment = HorizontalAlignment.Center,
            CornerRadius = new CornerRadius(48),
            Background = ObtenirPinceauTheme(
                "PinceauFondSurfaceTranslucide",
                ConstantesDesign.CouleurRepliSurfaceTranslucide
            ),
            BorderBrush = ObtenirPinceauTheme(
                "PinceauBordureSurfaceTranslucide",
                ConstantesDesign.CouleurRepliBordureSurfaceTranslucide
            ),
            BorderThickness = new Thickness(1),
            Child = new SystemControls.TextBlock
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 28,
                FontWeight = FontWeights.SemiBold,
                Opacity = 0.82,
                Text = string.IsNullOrWhiteSpace(compte.Titre)
                    ? "?"
                    : compte.Titre[..1].ToUpperInvariant(),
            },
        };
    }

    /*
     * Crée un séparateur visuel homogène pour les blocs de la modale de compte.
     */
    private static SystemControls.Separator ConstruireSeparateurBlocCompte()
    {
        return new SystemControls.Separator { Margin = new Thickness(0, 2, 0, 12), Opacity = 0.45 };
    }

    /*
     * Construit un bloc d'aide standard avec un titre, une description
     * et un contenu libre.
     */
    private SystemControls.Border ConstruireBlocAide(
        string titre,
        IReadOnlyList<string> lignes,
        string? resume = null,
        bool estOuvertParDefaut = false,
        IList<SystemControls.Expander>? sectionsAide = null
    )
    {
        SystemControls.StackPanel pile = new() { Margin = new Thickness(0) };

        foreach (string ligne in lignes)
        {
            pile.Children.Add(
                new SystemControls.TextBlock
                {
                    Margin = _modaleAideCompacteCourante
                        ? new Thickness(0, 0, 0, 6)
                        : new Thickness(0, 0, 0, 8),
                    Opacity = 0.84,
                    Text = $"- {ligne}",
                    TextWrapping = TextWrapping.Wrap,
                }
            );
        }

        return ConstruireSectionAideRabattable(
            titre,
            pile,
            resume,
            estOuvertParDefaut,
            null,
            sectionsAide,
            _modaleAideCompacteCourante
        );
    }

    /*
     * Construit la section d'aide qui expose les premiers outils d'intégration
     * OBS : chemins de sources, ouverture du dossier et état de test.
     */
    private SystemControls.Border ConstruireBlocAideObs(
        IList<SystemControls.Expander>? sectionsAide = null
    )
    {
        SystemControls.StackPanel pile = new() { Margin = new Thickness(0) };

        SystemControls.TextBlock texteEtat = ConstruireTexteDetailAide(
            "Les fichiers OBS sont mis à jour automatiquement quand Compagnon change d'état.",
            0.78,
            compact: _modaleAideCompacteCourante
        );
        SystemControls.CheckBox caseExportActif = new()
        {
            Content = "Activer l'export OBS automatique",
            IsChecked = _configurationConnexion.ExportObsActif,
            Margin = new Thickness(0, 0, 0, _modaleAideCompacteCourante ? 8 : 10),
        };

        UiControls.Button ConstruireBoutonActionObs(string libelle)
        {
            return new UiControls.Button
            {
                Content = libelle,
                Padding = _modaleAideCompacteCourante
                    ? ConstantesDesign.PaddingBoutonActionCompact
                    : new Thickness(12, 4, 12, 4),
                Margin = new Thickness(0, 0, 8, _modaleAideCompacteCourante ? 6 : 8),
            };
        }

        caseExportActif.Checked += async (_, _) =>
        {
            await DefinirExportObsActifAsync(true);
            texteEtat.Text = "Export OBS automatique activé.";
        };
        caseExportActif.Unchecked += async (_, _) =>
        {
            await DefinirExportObsActifAsync(false);
            texteEtat.Text = "Export OBS automatique désactivé.";
        };

        UiControls.Button boutonOuvrirDossier = ConstruireBoutonActionObs("Ouvrir le dossier OBS");
        UiControls.Button boutonCopierEtat = ConstruireBoutonActionObs("Copier state.json");
        UiControls.Button boutonCopierOverlay = ConstruireBoutonActionObs("Copier l'URL overlay");
        UiControls.Button boutonEtatTest = ConstruireBoutonActionObs("Générer un test OBS");

        boutonOuvrirDossier.Click += (_, _) =>
        {
            Directory.CreateDirectory(ServiceExportObs.DossierExportObs);
            OuvrirCheminDiagnosticAide(ServiceExportObs.DossierExportObs);
        };

        boutonCopierEtat.Click += (_, _) =>
        {
            CopierTexteDansPressePapiers(ServiceExportObs.CheminEtatJson);
            texteEtat.Text = "Chemin state.json copié.";
        };

        boutonCopierOverlay.Click += (_, _) =>
        {
            _serviceServeurObsLocal.Demarrer();
            CopierTexteDansPressePapiers(_serviceServeurObsLocal.UrlOverlay);
            texteEtat.Text = "URL overlay copiée.";
        };

        boutonEtatTest.Click += async (_, _) =>
        {
            boutonEtatTest.IsEnabled = false;
            texteEtat.Text = "Génération du test OBS...";

            try
            {
                await ExporterEtatObsTestAsync();
                texteEtat.Text = "État de test OBS généré.";
            }
            catch
            {
                texteEtat.Text = "Impossible de générer l'état de test OBS pour le moment.";
            }
            finally
            {
                boutonEtatTest.IsEnabled = true;
            }
        };

        pile.Children.Add(
            ConstruireTexteDetailAide(
                "OBS peut utiliser une source navigateur pointant vers l'URL locale de l'overlay, ou des sources texte pointant vers les fichiers .txt. L'overlay lit directement state.json.",
                0.82,
                compact: _modaleAideCompacteCourante
            )
        );
        pile.Children.Add(
            ConstruireTexteDetailAide(
                "Taille recommandée pour la source navigateur : 760 x 260. Le bloc HTML s'ajuste ensuite à son contenu dans cette zone.",
                0.72,
                compact: _modaleAideCompacteCourante
            )
        );
        pile.Children.Add(caseExportActif);
        pile.Children.Add(
            ConstruireLibelleChampAide("Dossier d'export", _modaleAideCompacteCourante)
        );
        pile.Children.Add(
            ConstruireZoneTexteCopiableAide(
                ServiceExportObs.DossierExportObs,
                _modaleAideCompacteCourante
            )
        );
        pile.Children.Add(
            ConstruireLibelleChampAide("URL source navigateur", _modaleAideCompacteCourante)
        );
        pile.Children.Add(
            ConstruireZoneTexteCopiableAide(
                _serviceServeurObsLocal.UrlOverlay,
                _modaleAideCompacteCourante
            )
        );
        pile.Children.Add(
            ConstruireLibelleChampAide("Fichier overlay", _modaleAideCompacteCourante)
        );
        pile.Children.Add(
            ConstruireZoneTexteCopiableAide(
                ServiceExportObs.CheminOverlayHtml,
                _modaleAideCompacteCourante
            )
        );
        pile.Children.Add(ConstruireLibelleChampAide("État JSON", _modaleAideCompacteCourante));
        pile.Children.Add(
            ConstruireZoneTexteCopiableAide(
                ServiceExportObs.CheminEtatJson,
                _modaleAideCompacteCourante
            )
        );
        pile.Children.Add(
            new SystemControls.WrapPanel
            {
                Orientation = SystemControls.Orientation.Horizontal,
                Margin = new Thickness(0, _modaleAideCompacteCourante ? 8 : 10, 0, 0),
                Children =
                {
                    boutonOuvrirDossier,
                    boutonCopierEtat,
                    boutonCopierOverlay,
                    boutonEtatTest,
                },
            }
        );
        pile.Children.Add(texteEtat);

        return ConstruireSectionAideRabattable(
            "OBS",
            pile,
            "Chemins locaux, source navigateur et génération d'un état de test.",
            false,
            null,
            sectionsAide,
            _modaleAideCompacteCourante
        );
    }

    /*
     * Construit une section d'aide rabattable réutilisable dans la modale
     * d'assistance.
     */
    private SystemControls.Border ConstruireSectionAideRabattable(
        string titre,
        UIElement contenu,
        string? resume = null,
        bool estOuvertParDefaut = false,
        Action? auPremierDeploiement = null,
        IList<SystemControls.Expander>? sectionsAide = null,
        bool dispositionCompacte = false
    )
    {
        SystemControls.StackPanel entete = new()
        {
            Margin = new Thickness(0),
            Children =
            {
                new SystemControls.TextBlock
                {
                    FontSize = 17,
                    FontWeight = FontWeights.SemiBold,
                    Text = titre,
                    TextWrapping = TextWrapping.Wrap,
                },
            },
        };

        if (!string.IsNullOrWhiteSpace(resume))
        {
            entete.Children.Add(
                new SystemControls.TextBlock
                {
                    Margin = dispositionCompacte
                        ? new Thickness(0, 3, 0, 0)
                        : new Thickness(0, 4, 0, 0),
                    Opacity = 0.7,
                    Text = resume,
                    TextWrapping = TextWrapping.Wrap,
                }
            );
        }

        bool contenuInitialise = auPremierDeploiement is null;

        void AssurerContenuInitialise()
        {
            if (contenuInitialise)
            {
                return;
            }

            auPremierDeploiement?.Invoke();
            contenuInitialise = true;
        }

        SystemControls.Expander expander = new()
        {
            Header = entete,
            IsExpanded = estOuvertParDefaut,
            Content = new SystemControls.Border
            {
                Padding = dispositionCompacte
                    ? new Thickness(0, 8, 0, 0)
                    : new Thickness(0, 10, 0, 2),
                Child = contenu,
            },
        };
        expander.Expanded += (_, _) =>
        {
            AssurerContenuInitialise();

            if (sectionsAide is null)
            {
                RafraichirDefilementModaleAide(expander);
                return;
            }

            foreach (SystemControls.Expander autreSection in sectionsAide)
            {
                if (ReferenceEquals(autreSection, expander) || !autreSection.IsExpanded)
                {
                    continue;
                }

                autreSection.IsExpanded = false;
            }

            RafraichirDefilementModaleAide(expander);
        };
        expander.Collapsed += (_, _) => RafraichirDefilementModaleAide(expander);
        sectionsAide?.Add(expander);

        if (estOuvertParDefaut)
        {
            AssurerContenuInitialise();
            RafraichirDefilementModaleAide(expander);
        }

        return new SystemControls.Border
        {
            Padding = dispositionCompacte
                ? new Thickness(10, 8, 10, 8)
                : new Thickness(13, 11, 13, 11),
            Margin = dispositionCompacte ? new Thickness(0, 0, 0, 8) : new Thickness(0, 0, 0, 10),
            CornerRadius = ObtenirRayonCoins("RayonCoinsPetit", 8),
            Background = ObtenirPinceauTheme(
                "PinceauFondSurfaceLegere",
                ConstantesDesign.CouleurRepliSurfaceLegere
            ),
            BorderBrush = ObtenirPinceauTheme(
                "PinceauBordureSurfaceLegere",
                ConstantesDesign.CouleurRepliBordureSurfaceLegere
            ),
            BorderThickness = ConstantesDesign.EpaisseurContourFin,
            Child = expander,
        };
    }

    /*
     * Construit la section d'aide consacrée aux logs, chemins et emplacements
     * des émulateurs.
     */
    private SystemControls.Border ConstruireBlocAideLogsEmulateurs(
        IList<SystemControls.Expander>? sectionsAide = null
    )
    {
        SystemControls.StackPanel pile = new() { Margin = new Thickness(0) };
        List<SystemControls.Expander> sectionsEmulateurs = [];
        pile.Children.Add(
            new SystemControls.TextBlock
            {
                Margin = new Thickness(0, 0, 0, _modaleAideCompacteCourante ? 8 : 10),
                Opacity = 0.78,
                Text =
                    "Cette section montre le fichier, le dossier ou l'exécutable que Compagnon attend réellement pour chaque émulateur.",
                TextWrapping = TextWrapping.Wrap,
            }
        );
        pile.Children.Add(
            new SystemControls.TextBlock
            {
                Margin = new Thickness(0, 0, 0, _modaleAideCompacteCourante ? 8 : 10),
                Opacity = 0.72,
                Text =
                    "Commence par le journal local, puis ouvre la carte de l'émulateur concerné. Si besoin, définis un exécutable manuel.",
                TextWrapping = TextWrapping.Wrap,
            }
        );
        pile.Children.Add(
            ConstruireLibelleChampAide("Journal local de Compagnon", _modaleAideCompacteCourante)
        );
        pile.Children.Add(
            ConstruireZoneTexteCopiableAide(
                ServiceSondeLocaleEmulateurs.ObtenirCheminJournal(),
                _modaleAideCompacteCourante
            )
        );
        UiControls.Button boutonOuvrirJournal = new()
        {
            Content = "Ouvrir le journal",
            Padding = _modaleAideCompacteCourante
                ? ConstantesDesign.PaddingBoutonActionCompact
                : new Thickness(12, 4, 12, 4),
            Margin = new Thickness(0, 0, 0, _modaleAideCompacteCourante ? 8 : 10),
        };
        boutonOuvrirJournal.Click += (_, _) =>
        {
            string cheminJournal = ServiceSondeLocaleEmulateurs.ObtenirCheminJournal();

            if (File.Exists(cheminJournal))
            {
                OuvrirFichierExterne(cheminJournal);
                return;
            }

            OuvrirDossierContenant(cheminJournal);
        };
        pile.Children.Add(boutonOuvrirJournal);

        SystemControls.StackPanel contenu = new() { Margin = new Thickness(0, 2, 0, 0) };
        bool contenuCharge = false;

        void ChargerContenu()
        {
            if (contenuCharge)
            {
                return;
            }

            foreach (
                DefinitionEmulateurLocal definition in ServiceCatalogueEmulateursLocaux.Definitions.Where(
                    EstEmulateurValidePourIndicatifLogs
                )
            )
            {
                contenu.Children.Add(
                    ConstruireCarteIndicatifLogsEmulateur(
                        definition,
                        sectionsEmulateurs,
                        _modaleAideCompacteCourante
                    )
                );
            }

            contenuCharge = true;
        }

        pile.Children.Add(contenu);

        return ConstruireSectionAideRabattable(
            "Logs des émulateurs",
            pile,
            "Chemins surveillés, exécutable visé et actions directes.",
            false,
            ChargerContenu,
            sectionsAide,
            _modaleAideCompacteCourante
        );
    }

    /*
     * Construit la carte de diagnostic d'un émulateur avec ses sources
     * locales et ses chemins utiles.
     */
    private SystemControls.Border ConstruireCarteIndicatifLogsEmulateur(
        DefinitionEmulateurLocal definition,
        IList<SystemControls.Expander>? sectionsAide = null,
        bool dispositionCompacte = false
    )
    {
        string source = ConstruireLibelleSourceLocaleEmulateur(definition);
        string cheminDetecte = ServiceSourcesLocalesEmulateurs.TrouverCheminJournalSuccesLocal(
            definition.NomEmulateur
        );
        if (
            definition.StrategieRenseignementJeu
                == StrategieRenseignementJeuEmulateurLocal.RetroArchLog
            && !string.IsNullOrWhiteSpace(cheminDetecte)
        )
        {
            string? repertoireDetecte = Path.GetDirectoryName(cheminDetecte);

            if (!string.IsNullOrWhiteSpace(repertoireDetecte))
            {
                cheminDetecte = Path.Combine(repertoireDetecte, "retroarch.log");
            }
        }

        string cheminAttendu = string.IsNullOrWhiteSpace(cheminDetecte)
            ? ConstruireCheminIndicatifSourceLocale(definition)
            : cheminDetecte;
        string statutChemin = string.IsNullOrWhiteSpace(cheminDetecte)
            ? "Non trouvé sur ce PC"
            : "Détecté sur ce PC";
        SystemControls.TextBlock texteStatutEmplacement = new()
        {
            Margin = new Thickness(0, dispositionCompacte ? 4 : 6, 0, 0),
            FontWeight = FontWeights.SemiBold,
            Opacity = 0.72,
            TextWrapping = TextWrapping.Wrap,
        };
        SystemControls.TextBox texteEmplacement = new()
        {
            Margin = new Thickness(0, dispositionCompacte ? 3 : 4, 0, 0),
            Style = (Style)FindResource("StyleTexteCopiable"),
            TextWrapping = TextWrapping.Wrap,
        };
        SystemControls.TextBlock texteAideEmplacementManuel = new()
        {
            Margin = new Thickness(0, dispositionCompacte ? 4 : 6, 0, 0),
            Opacity = 0.66,
            TextWrapping = TextWrapping.Wrap,
        };
        SystemControls.Button boutonChoisirEmplacement = new()
        {
            Margin = new Thickness(0, dispositionCompacte ? 6 : 8, 8, 0),
            Padding = dispositionCompacte
                ? ConstantesDesign.PaddingBoutonActionCompact
                : new Thickness(10, 4, 10, 4),
            MinWidth = 0,
        };
        SystemControls.Button boutonRetirerEmplacement = new()
        {
            Margin = new Thickness(0, dispositionCompacte ? 6 : 8, 0, 0),
            Padding = dispositionCompacte
                ? ConstantesDesign.PaddingBoutonActionCompact
                : new Thickness(10, 4, 10, 4),
            MinWidth = 0,
            Content = "Retirer le choix manuel",
        };
        SystemControls.Button boutonOuvrirEmplacement = new()
        {
            Margin = new Thickness(0, dispositionCompacte ? 6 : 8, 8, 0),
            Padding = dispositionCompacte
                ? ConstantesDesign.PaddingBoutonActionCompact
                : new Thickness(10, 4, 10, 4),
            MinWidth = 0,
            Content = "Ouvrir l'emplacement",
        };
        SystemControls.Button boutonOuvrirSource = new()
        {
            Margin = new Thickness(0, dispositionCompacte ? 6 : 8, 0, 0),
            Padding = dispositionCompacte
                ? ConstantesDesign.PaddingBoutonActionCompact
                : new Thickness(10, 4, 10, 4),
            MinWidth = 0,
            Content = "Ouvrir la source",
        };
        SystemControls.TextBlock texteStatutDetectionCourante = new()
        {
            Margin = new Thickness(0, dispositionCompacte ? 4 : 6, 0, 0),
            FontWeight = FontWeights.SemiBold,
            Opacity = 0.72,
            TextWrapping = TextWrapping.Wrap,
        };
        SystemControls.TextBlock texteDetailDetectionCourante = new()
        {
            Opacity = 0.72,
            TextWrapping = TextWrapping.Wrap,
        };
        SystemControls.TextBlock texteConfigurationHttpSkyEmu = new()
        {
            Opacity = 0.66,
            TextWrapping = TextWrapping.Wrap,
            Visibility = Visibility.Collapsed,
        };
        SystemControls.Button boutonTesterHttpSkyEmu = new()
        {
            Margin = new Thickness(0, dispositionCompacte ? 6 : 8, 0, 0),
            Padding = dispositionCompacte
                ? ConstantesDesign.PaddingBoutonActionCompact
                : new Thickness(10, 4, 10, 4),
            MinWidth = 0,
            Content = "Tester HTTP",
            Visibility = Visibility.Collapsed,
        };
        SystemControls.TextBlock texteResultatHttpSkyEmu = new()
        {
            Margin = new Thickness(0, dispositionCompacte ? 4 : 6, 0, 0),
            Opacity = 0.72,
            TextWrapping = TextWrapping.Wrap,
            Visibility = Visibility.Collapsed,
        };
        SystemControls.TextBox texteDiagnosticBrut = new()
        {
            Margin = new Thickness(0, dispositionCompacte ? 3 : 4, 0, 0),
            Style = (Style)FindResource("StyleTexteCopiable"),
            TextWrapping = TextWrapping.Wrap,
            Visibility = Visibility.Collapsed,
        };

        void RafraichirBlocEmplacement()
        {
            string emplacementManuel =
                ServiceSourcesLocalesEmulateurs.ObtenirEmplacementEmulateurManuel(
                    definition.NomEmulateur
                );
            string emplacementDetecteMemorise =
                ServiceSourcesLocalesEmulateurs.ObtenirEmplacementEmulateurDetecte(
                    definition.NomEmulateur
                );
            string emplacementDetecte = ServiceSourcesLocalesEmulateurs.TrouverEmplacementEmulateur(
                definition.NomEmulateur
            );

            texteStatutEmplacement.Text =
                !string.IsNullOrWhiteSpace(emplacementManuel) ? "Emplacement manuel défini"
                : !string.IsNullOrWhiteSpace(emplacementDetecteMemorise)
                    ? "Emplacement détecté et mémorisé"
                : string.IsNullOrWhiteSpace(emplacementDetecte) ? "Emplacement non trouvé sur ce PC"
                : "Emplacement détecté sur ce PC";

            texteEmplacement.Text = string.IsNullOrWhiteSpace(emplacementDetecte)
                ? ConstruireCheminIndicatifEmulateur(definition)
                : emplacementDetecte;

            texteAideEmplacementManuel.Text =
                !string.IsNullOrWhiteSpace(emplacementManuel)
                    ? "Ce chemin manuel passe en priorité si Compagnon hésite entre plusieurs exécutables."
                : !string.IsNullOrWhiteSpace(emplacementDetecteMemorise)
                    ? "Compagnon a mémorisé cet emplacement après avoir vu l'émulateur ouvert sur ce PC."
                : "Si l'exécutable est renommé ou ambigu, tu peux choisir ici le bon fichier .exe.";

            boutonChoisirEmplacement.Content = !string.IsNullOrWhiteSpace(emplacementManuel)
                ? "Modifier l'emplacement manuel"
                : "Choisir un exécutable";

            boutonRetirerEmplacement.Visibility = !string.IsNullOrWhiteSpace(emplacementManuel)
                ? Visibility.Visible
                : Visibility.Collapsed;

            boutonOuvrirEmplacement.IsEnabled =
                !string.IsNullOrWhiteSpace(texteEmplacement.Text)
                && (
                    File.Exists(texteEmplacement.Text)
                    || Directory.Exists(texteEmplacement.Text)
                    || !string.IsNullOrWhiteSpace(Path.GetDirectoryName(texteEmplacement.Text))
                );

            boutonOuvrirSource.IsEnabled =
                !string.IsNullOrWhiteSpace(cheminAttendu)
                && (
                    File.Exists(cheminAttendu)
                    || Directory.Exists(cheminAttendu)
                    || !string.IsNullOrWhiteSpace(Path.GetDirectoryName(cheminAttendu))
                );

            EtatSondeLocaleEmulateur? etatCourant =
                _dernierEtatSondeLocaleEmulateurs is { EmulateurDetecte: true } etatDetecte
                && string.Equals(
                    etatDetecte.NomEmulateur,
                    definition.NomEmulateur,
                    StringComparison.Ordinal
                )
                    ? etatDetecte
                    : null;

            if (etatCourant is null)
            {
                texteStatutDetectionCourante.Text = "Aucune détection active en ce moment";
                texteDetailDetectionCourante.Text =
                    "Ouvre cet émulateur pour voir ici le processus, le jeu probable et la source réellement utilisée par Compagnon.";
                texteDiagnosticBrut.Text = string.Empty;
                texteDiagnosticBrut.Visibility = Visibility.Collapsed;
            }
            else
            {
                texteStatutDetectionCourante.Text = "Détection active en ce moment";
                texteDetailDetectionCourante.Text = ConstruireTexteResumeDetectionCourante(
                    etatCourant
                );
                texteDiagnosticBrut.Text = string.IsNullOrWhiteSpace(
                    etatCourant.InformationsDiagnostic
                )
                    ? "Aucun diagnostic détaillé."
                    : etatCourant.InformationsDiagnostic;
                texteDiagnosticBrut.Visibility = Visibility.Visible;
            }

            if (string.Equals(definition.NomEmulateur, "SkyEmu", StringComparison.Ordinal))
            {
                if (
                    ServiceSourcesLocalesEmulateurs.EssayerLireConfigurationHttpSkyEmu(
                        out bool serveurActif,
                        out int port
                    )
                )
                {
                    texteConfigurationHttpSkyEmu.Text = serveurActif
                        ? $"Configuration HTTP SkyEmu : activée sur le port {port}."
                        : "Configuration HTTP SkyEmu : désactivée dans user_settings.bin.";
                }
                else
                {
                    texteConfigurationHttpSkyEmu.Text =
                        "Configuration HTTP SkyEmu : fichier user_settings.bin introuvable sur ce PC.";
                }

                texteConfigurationHttpSkyEmu.Visibility = Visibility.Visible;
                boutonTesterHttpSkyEmu.Visibility = Visibility.Visible;
                boutonTesterHttpSkyEmu.IsEnabled = ObtenirPortSkyEmuPourTestHttp() > 0;
                boutonTesterHttpSkyEmu.ToolTip = boutonTesterHttpSkyEmu.IsEnabled
                    ? "Interroger le point /status de SkyEmu"
                    : "Active le serveur HTTP de SkyEmu pour tester /status";
                texteResultatHttpSkyEmu.Visibility = string.IsNullOrWhiteSpace(
                    texteResultatHttpSkyEmu.Text
                )
                    ? Visibility.Collapsed
                    : Visibility.Visible;
            }
            else
            {
                texteConfigurationHttpSkyEmu.Text = string.Empty;
                texteConfigurationHttpSkyEmu.Visibility = Visibility.Collapsed;
                boutonTesterHttpSkyEmu.Visibility = Visibility.Collapsed;
                texteResultatHttpSkyEmu.Text = string.Empty;
                texteResultatHttpSkyEmu.Visibility = Visibility.Collapsed;
            }
        }

        SystemControls.StackPanel pile = new()
        {
            Children =
            {
                ConstruireLibelleChampAide("Source locale suivie", dispositionCompacte),
                ConstruireTexteDetailAide(source, 0.78, compact: dispositionCompacte),
                ConstruireLibelleChampAide("Niveau de confiance", dispositionCompacte),
                ConstruireTexteDetailAide(
                    ConstruireTexteConfianceDetectionEmulateur(definition),
                    0.72,
                    compact: dispositionCompacte
                ),
                ConstruireTexteDetailAide(
                    ConstruireTexteValidationEmulateur(definition),
                    0.78,
                    FontWeights.SemiBold,
                    string.IsNullOrWhiteSpace(ConstruireTexteValidationEmulateur(definition))
                        ? Visibility.Collapsed
                        : Visibility.Visible,
                    dispositionCompacte
                ),
                ConstruireLibelleChampAide("Diagnostic en direct", dispositionCompacte),
                texteStatutDetectionCourante,
                texteDetailDetectionCourante,
                texteConfigurationHttpSkyEmu,
                boutonTesterHttpSkyEmu,
                texteResultatHttpSkyEmu,
                texteDiagnosticBrut,
                ConstruireLibelleChampAide("Exécutable de l'émulateur", dispositionCompacte),
                texteStatutEmplacement,
                texteEmplacement,
                texteAideEmplacementManuel,
                ConstruireTexteDetailAide(
                    ConstruireTexteProfilInstallationEmulateur(definition),
                    0.66,
                    visibility: string.IsNullOrWhiteSpace(
                        ConstruireTexteProfilInstallationEmulateur(definition)
                    )
                        ? Visibility.Collapsed
                        : Visibility.Visible,
                    compact: dispositionCompacte
                ),
                new SystemControls.WrapPanel
                {
                    Orientation = SystemControls.Orientation.Horizontal,
                    Children =
                    {
                        boutonChoisirEmplacement,
                        boutonRetirerEmplacement,
                        boutonOuvrirEmplacement,
                    },
                },
                ConstruireLibelleChampAide("Fichier ou dossier surveillé", dispositionCompacte),
                ConstruireTexteDetailAide(
                    statutChemin,
                    0.72,
                    FontWeights.SemiBold,
                    compact: dispositionCompacte
                ),
                ConstruireZoneTexteCopiableAide(cheminAttendu, dispositionCompacte),
                new SystemControls.WrapPanel
                {
                    Orientation = SystemControls.Orientation.Horizontal,
                    Children = { boutonOuvrirSource },
                },
                ConstruireLibelleChampAide("À vérifier dans l'émulateur", dispositionCompacte),
                ConstruireTexteDetailAide(
                    ConstruireTexteActivationSourceLocale(definition),
                    0.66,
                    compact: dispositionCompacte
                ),
            },
        };

        boutonChoisirEmplacement.Click += async (_, _) =>
        {
            await ChoisirEmplacementEmulateurManuelAsync(definition);
            RafraichirBlocEmplacement();
        };

        boutonRetirerEmplacement.Click += async (_, _) =>
        {
            await RetirerEmplacementEmulateurManuelAsync(definition);
            RafraichirBlocEmplacement();
        };
        boutonTesterHttpSkyEmu.Click += async (_, _) =>
            await TesterHttpSkyEmuDepuisCarteAsync(texteResultatHttpSkyEmu);
        boutonOuvrirEmplacement.Click += (_, _) =>
            OuvrirCheminDiagnosticAide(texteEmplacement.Text);
        boutonOuvrirSource.Click += (_, _) => OuvrirCheminDiagnosticAide(cheminAttendu);

        DispatcherTimer minuteurRafraichissementEmplacement = new()
        {
            Interval = TimeSpan.FromSeconds(1),
        };
        minuteurRafraichissementEmplacement.Tick += (_, _) => RafraichirBlocEmplacement();
        pile.Loaded += (_, _) =>
        {
            RafraichirBlocEmplacement();
            minuteurRafraichissementEmplacement.Start();
        };
        pile.Unloaded += (_, _) => minuteurRafraichissementEmplacement.Stop();

        RafraichirBlocEmplacement();
        return ConstruireSectionAideRabattable(
            definition.NomEmulateur,
            pile,
            ConstruireResumeSectionLogsEmulateur(definition, source),
            false,
            null,
            sectionsAide,
            dispositionCompacte
        );
    }

    /*
     * Construit un libellé de champ homogène pour les cartes de diagnostic
     * affichées dans la modale d'aide.
     */
    private static SystemControls.TextBlock ConstruireLibelleChampAide(
        string texte,
        bool compact = false
    )
    {
        return new SystemControls.TextBlock
        {
            Margin = compact ? new Thickness(0, 6, 0, 1) : new Thickness(0, 8, 0, 2),
            FontWeight = FontWeights.SemiBold,
            Opacity = 0.8,
            Text = texte,
            TextWrapping = TextWrapping.Wrap,
        };
    }

    /*
     * Construit un texte de détail homogène pour les contenus explicatifs de
     * la modale d'aide.
     */
    private static SystemControls.TextBlock ConstruireTexteDetailAide(
        string texte,
        double opacite,
        FontWeight? graisse = null,
        Visibility visibility = Visibility.Visible,
        bool compact = false
    )
    {
        return new SystemControls.TextBlock
        {
            Margin = compact ? new Thickness(0, 0, 0, 1) : new Thickness(0),
            FontWeight = graisse ?? FontWeights.Normal,
            Opacity = opacite,
            Text = texte,
            TextWrapping = TextWrapping.Wrap,
            Visibility = visibility,
        };
    }

    /*
     * Construit une zone de texte copiable homogène pour les chemins et
     * journaux affichés dans la modale d'aide.
     */
    private SystemControls.TextBox ConstruireZoneTexteCopiableAide(
        string texte,
        bool compact = false
    )
    {
        return new SystemControls.TextBox
        {
            Margin = compact ? new Thickness(0, 3, 0, 0) : new Thickness(0, 4, 0, 0),
            Style = (Style)FindResource("StyleTexteCopiable"),
            Text = texte,
            TextWrapping = TextWrapping.Wrap,
        };
    }

    /*
     * Copie un texte dans le presse-papiers en ignorant les indisponibilités
     * temporaires du système.
     */
    private static void CopierTexteDansPressePapiers(string texte)
    {
        if (string.IsNullOrWhiteSpace(texte))
        {
            return;
        }

        try
        {
            Clipboard.SetText(texte);
        }
        catch { }
    }

    /*
     * Ouvre un chemin de diagnostic depuis la modale d'aide en privilégiant le
     * fichier lui-même lorsqu'il existe, puis son dossier parent.
     */
    private static void OuvrirCheminDiagnosticAide(string? chemin)
    {
        if (string.IsNullOrWhiteSpace(chemin))
        {
            return;
        }

        if (File.Exists(chemin))
        {
            OuvrirFichierExterne(chemin);
            return;
        }

        if (Directory.Exists(chemin))
        {
            try
            {
                Process.Start(new ProcessStartInfo { FileName = chemin, UseShellExecute = true });
                return;
            }
            catch { }

            OuvrirDossierContenant(Path.Combine(chemin, "_"));
            return;
        }

        OuvrirDossierContenant(chemin);
    }

    /*
     * Résume en une ligne la source locale et le niveau de confiance
     * d'un émulateur.
     */
    private static string ConstruireResumeSectionLogsEmulateur(
        DefinitionEmulateurLocal definition,
        string source
    )
    {
        string confiance = ConstruireTexteConfianceDetectionEmulateur(definition);
        int separateur = confiance.IndexOf(':');
        string confianceCourte =
            separateur >= 0 ? confiance[(separateur + 1)..].Trim() : confiance.Trim();

        return $"{source} {confianceCourte}";
    }

    /*
     * Résume en texte lisible la dernière détection live observée pour un
     * émulateur donné.
     */
    private static string ConstruireTexteResumeDetectionCourante(EtatSondeLocaleEmulateur etat)
    {
        List<string> morceaux = [];

        string sourceCourte = ConstruireLibelleCourtSourceDiagnostic(etat.InformationsDiagnostic);

        if (!string.IsNullOrWhiteSpace(sourceCourte))
        {
            morceaux.Add($"Source active : {sourceCourte}.");
        }

        if (!string.IsNullOrWhiteSpace(etat.NomProcessus))
        {
            morceaux.Add($"Processus : {etat.NomProcessus}.");
        }

        if (!string.IsNullOrWhiteSpace(etat.TitreJeuProbable))
        {
            morceaux.Add($"Jeu probable : {etat.TitreJeuProbable}.");
        }

        if (etat.IdentifiantJeuProbable > 0)
        {
            morceaux.Add($"Game ID détecté : {etat.IdentifiantJeuProbable}.");
        }

        if (!string.IsNullOrWhiteSpace(etat.CheminJeuProbable))
        {
            morceaux.Add($"Chemin détecté : {etat.CheminJeuProbable}.");
        }

        return morceaux.Count > 0
            ? string.Join(" ", morceaux)
            : "Compagnon voit le processus, mais sans détail complémentaire exploitable.";
    }

    /*
     * Transforme une source de diagnostic brute en libellé plus lisible
     * pour la modale d'aide.
     */
    private static string ConstruireLibelleCourtSourceDiagnostic(string diagnostic)
    {
        string source = ExtraireValeurDiagnostic(diagnostic, "source");

        if (string.IsNullOrWhiteSpace(source))
        {
            return string.Empty;
        }

        return source switch
        {
            "skyemu_http_status" => string.IsNullOrWhiteSpace(
                ExtraireValeurDiagnostic(diagnostic, "port")
            )
                ? "serveur HTTP /status"
                : $"serveur HTTP /status sur le port {ExtraireValeurDiagnostic(diagnostic, "port")}",
            "skyemu_recent_games" => "recent_games.txt",
            "retroarch_log" => "retroarch.log",
            "duckstation_log" => "duckstation.log",
            "pcsx2_log" => "emulog.txt",
            "ppsspp_log" => "journal local de PPSSPP",
            "flycast_config" => "flycast.log",
            "project64_racache" => "RACache et RALog.txt",
            "ralibretro_racache" => "RACache et RALog.txt",
            "ranes_racache" => "RACache et RALog.txt",
            "ravba_racache" => "RACache et RALog.txt",
            "rasnes9x_racache" => "RACache et RALog.txt",
            "dolphin_process" => "processus Dolphin",
            "bizhawk_json" => "retroachievements-game-log.json",
            _ => source,
        };
    }

    /*
     * Extrait une valeur simple de type clé=valeur depuis un diagnostic brut.
     */
    private static string ExtraireValeurDiagnostic(string diagnostic, string cle)
    {
        if (string.IsNullOrWhiteSpace(diagnostic) || string.IsNullOrWhiteSpace(cle))
        {
            return string.Empty;
        }

        foreach (string morceau in diagnostic.Split(';', StringSplitOptions.TrimEntries))
        {
            if (string.IsNullOrWhiteSpace(morceau))
            {
                continue;
            }

            int separateur = morceau.IndexOf('=');

            if (separateur <= 0)
            {
                continue;
            }

            string cleCourante = morceau[..separateur].Trim();

            if (!string.Equals(cleCourante, cle, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return morceau[(separateur + 1)..].Trim();
        }

        return string.Empty;
    }

    /*
     * Détermine le port HTTP le plus pertinent à utiliser pour tester SkyEmu
     * depuis la modale d'aide.
     */
    private int ObtenirPortSkyEmuPourTestHttp()
    {
        if (
            _dernierEtatSondeLocaleEmulateurs is { EmulateurDetecte: true } etatDetecte
            && string.Equals(etatDetecte.NomEmulateur, "SkyEmu", StringComparison.Ordinal)
        )
        {
            string portDiagnostic = ExtraireValeurDiagnostic(
                etatDetecte.InformationsDiagnostic,
                "port"
            );

            if (
                int.TryParse(
                    portDiagnostic,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int portActif
                )
                && portActif > 0
                && portActif <= 65535
            )
            {
                return portActif;
            }
        }

        if (
            ServiceSourcesLocalesEmulateurs.EssayerLireConfigurationHttpSkyEmu(
                out bool serveurActif,
                out int portConfiguration
            )
            && serveurActif
            && portConfiguration > 0
            && portConfiguration <= 65535
        )
        {
            return portConfiguration;
        }

        return 0;
    }

    /*
     * Interroge le point /status de SkyEmu et affiche un résultat lisible
     * directement dans la carte de diagnostic.
     */
    private async Task TesterHttpSkyEmuDepuisCarteAsync(SystemControls.TextBlock texteResultat)
    {
        texteResultat.Visibility = Visibility.Visible;
        texteResultat.Text = "Test HTTP en cours...";

        int port = ObtenirPortSkyEmuPourTestHttp();

        if (port <= 0)
        {
            texteResultat.Text = "Aucun port HTTP SkyEmu exploitable n'a été trouvé sur ce PC.";
            return;
        }

        try
        {
            using HttpClient client = new() { Timeout = TimeSpan.FromMilliseconds(800) };
            string contenu = await client.GetStringAsync(
                $"http://127.0.0.1:{port.ToString(CultureInfo.InvariantCulture)}/status"
            );

            if (string.IsNullOrWhiteSpace(contenu))
            {
                texteResultat.Text =
                    $"Test HTTP réussi sur le port {port}, mais la réponse /status est vide.";
                return;
            }

            using JsonDocument document = JsonDocument.Parse(contenu);
            bool romChargee =
                document.RootElement.TryGetProperty("rom-loaded", out JsonElement romChargeeElement)
                && romChargeeElement.ValueKind == JsonValueKind.True;
            string cheminRom =
                document.RootElement.TryGetProperty("rom-path", out JsonElement cheminRomElement)
                && cheminRomElement.ValueKind == JsonValueKind.String
                    ? cheminRomElement.GetString() ?? string.Empty
                    : string.Empty;

            texteResultat.Text =
                romChargee && !string.IsNullOrWhiteSpace(cheminRom)
                    ? $"Test HTTP réussi sur le port {port}. ROM remontée : {cheminRom}."
                    : $"Test HTTP réussi sur le port {port}. Réponse /status valide, mais aucune ROM n'est chargée.";
        }
        catch (Exception exception)
        {
            string messageErreur = exception.Message.Trim();

            if (string.IsNullOrWhiteSpace(messageErreur) && exception.InnerException is not null)
            {
                messageErreur = exception.InnerException.Message.Trim();
            }

            texteResultat.Text = string.IsNullOrWhiteSpace(messageErreur)
                ? $"Échec du test HTTP sur le port {port}."
                : $"Échec du test HTTP sur le port {port} : {messageErreur}";
        }
    }

    /*
     * Permet de choisir manuellement l'exécutable d'un émulateur puis
     * mémorise ce choix.
     */
    private async Task ChoisirEmplacementEmulateurManuelAsync(DefinitionEmulateurLocal definition)
    {
        OpenFileDialog dialogue = new()
        {
            Title = $"Choisir l'exécutable pour {definition.NomEmulateur}",
            Filter = "Exécutable Windows (*.exe)|*.exe|Tous les fichiers (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false,
        };
        string cheminInitial = ServiceSourcesLocalesEmulateurs.ObtenirEmplacementEmulateurManuel(
            definition.NomEmulateur
        );

        if (string.IsNullOrWhiteSpace(cheminInitial))
        {
            cheminInitial = ServiceSourcesLocalesEmulateurs.TrouverEmplacementEmulateur(
                definition.NomEmulateur
            );
        }

        try
        {
            if (File.Exists(cheminInitial))
            {
                dialogue.InitialDirectory = Path.GetDirectoryName(cheminInitial);
                dialogue.FileName = Path.GetFileName(cheminInitial);
            }
            else if (Directory.Exists(cheminInitial))
            {
                dialogue.InitialDirectory = cheminInitial;
            }
        }
        catch { }

        bool? resultat = dialogue.ShowDialog(this);

        if (resultat != true || string.IsNullOrWhiteSpace(dialogue.FileName))
        {
            return;
        }

        _configurationConnexion.EmplacementsEmulateursManuels[definition.NomEmulateur] =
            dialogue.FileName.Trim();
        ServiceSourcesLocalesEmulateurs.ConfigurerEmplacementsEmulateursManuels(
            _configurationConnexion.EmplacementsEmulateursManuels
        );
        await _serviceConfigurationLocale.SauvegarderEtatApplicationAsync(_configurationConnexion);
    }

    /*
     * Retire l'emplacement manuel mémorisé pour un émulateur.
     */
    private async Task RetirerEmplacementEmulateurManuelAsync(DefinitionEmulateurLocal definition)
    {
        if (_configurationConnexion.EmplacementsEmulateursManuels.Remove(definition.NomEmulateur))
        {
            ServiceSourcesLocalesEmulateurs.ConfigurerEmplacementsEmulateursManuels(
                _configurationConnexion.EmplacementsEmulateursManuels
            );
            await _serviceConfigurationLocale.SauvegarderEtatApplicationAsync(
                _configurationConnexion
            );
        }
    }

    /*
     * Indique si un émulateur doit apparaître dans la section de diagnostic
     * des logs.
     */
    private static bool EstEmulateurValidePourIndicatifLogs(DefinitionEmulateurLocal definition)
    {
        return ServiceCatalogueEmulateursLocaux.EstEmulateurValide(definition);
    }

    /*
     * Décrit la source locale principale utilisée pour détecter le jeu
     * d'un émulateur.
     */
    private static string ConstruireLibelleSourceLocaleEmulateur(
        DefinitionEmulateurLocal definition
    )
    {
        return definition.StrategieRenseignementJeu switch
        {
            StrategieRenseignementJeuEmulateurLocal.RetroArchLog =>
                "Source : journal local de RetroArch.",
            StrategieRenseignementJeuEmulateurLocal.BizHawkConfig =>
                "Source : retroachievements-game-log.json de BizHawk.",
            StrategieRenseignementJeuEmulateurLocal.DolphinConfig =>
                "Source : dolphin.log de Dolphin, avec le processus en secours.",
            StrategieRenseignementJeuEmulateurLocal.DuckStationLog => "Source : duckstation.log.",
            StrategieRenseignementJeuEmulateurLocal.PCSX2Log => "Source : emulog.txt de PCSX2.",
            StrategieRenseignementJeuEmulateurLocal.PPSSPPLog =>
                "Source : journal local de PPSSPP.",
            StrategieRenseignementJeuEmulateurLocal.SkyEmuRecentGames =>
                "Source : ligne de commande ou /status de SkyEmu, avec recent_games.txt en secours pour Rejouer.",
            StrategieRenseignementJeuEmulateurLocal.Project64RACache =>
                "Source : RACache et RALog.txt.",
            StrategieRenseignementJeuEmulateurLocal.RALibretroRACache =>
                "Source : RACache et RALog.txt.",
            StrategieRenseignementJeuEmulateurLocal.RANesRACache =>
                "Source : RACache et RALog.txt.",
            StrategieRenseignementJeuEmulateurLocal.RAVBARACache =>
                "Source : RACache et RALog.txt.",
            StrategieRenseignementJeuEmulateurLocal.RASnes9xRACache =>
                "Source : RACache et RALog.txt.",
            StrategieRenseignementJeuEmulateurLocal.FlycastConfig =>
                "Source : flycast.log, avec le chemin du jeu chargé en secours.",
            _ => "Source locale non précisée.",
        };
    }

    /*
     * Fournit les consignes d'activation nécessaires pour que la source locale
     * soit exploitable.
     */
    private static string ConstruireTexteActivationSourceLocale(DefinitionEmulateurLocal definition)
    {
        return definition.StrategieRenseignementJeu switch
        {
            StrategieRenseignementJeuEmulateurLocal.RetroArchLog =>
                "Dans RetroArch : active `Show Advanced Settings`, puis `Log to File`. Désactive aussi les journaux horodatés pour garder un fichier stable `retroarch.log` dans `logs`.",
            StrategieRenseignementJeuEmulateurLocal.BizHawkConfig =>
                "Dans BizHawk : garde `retroachievements-game-log.json` à la racine. Compagnon lit d'abord ce fichier, puis `config.ini` en secours pour retrouver la ROM.",
            StrategieRenseignementJeuEmulateurLocal.DolphinConfig =>
                "Dans Dolphin : ouvre `View -> Show Log Configuration`, coche `Write to File`, garde `RetroAchievements` actif et une verbosité au moins sur `Info`.",
            StrategieRenseignementJeuEmulateurLocal.DuckStationLog =>
                "Dans DuckStation : ouvre `Settings -> Advanced Settings`, règle `Log Level` sur `Debug`, puis active `Log To File`. Redémarre DuckStation si `duckstation.log` n'apparaît pas.",
            StrategieRenseignementJeuEmulateurLocal.PCSX2Log =>
                "Dans PCSX2 : `emulog.txt` est normalement créé dans `logs`. S'il n'apparaît pas, vérifie les options de console ou de débogage de ta version.",
            StrategieRenseignementJeuEmulateurLocal.PPSSPPLog =>
                "Dans PPSSPP : ouvre `Tools -> Developer Tools`, puis active `Enable debug logging`. Si rien n'est écrit sur disque, lance PPSSPP avec une option du type `--log=...`.",
            StrategieRenseignementJeuEmulateurLocal.SkyEmuRecentGames =>
                "Dans SkyEmu : aucun réglage spécial n'est requis pour la phase 1. Pour une détection plus robuste, active `Enable HTTP Control Server` dans les paramètres avancés. Compagnon lit alors `/status`, puis garde `recent_games.txt` dans le profil SDL de SkyEmu comme secours pour `Rejouer`.",
            StrategieRenseignementJeuEmulateurLocal.Project64RACache =>
                "Dans RAP64 : ce n'est pas un journal classique. Vérifie surtout que RetroAchievements est bien actif pour mettre à jour `RACache` et `RALog.txt`.",
            StrategieRenseignementJeuEmulateurLocal.RALibretroRACache =>
                "Dans RALibretro : ce n'est pas un journal classique. Vérifie surtout que RetroAchievements est bien actif pour mettre à jour `RACache` et `RALog.txt`.",
            StrategieRenseignementJeuEmulateurLocal.RANesRACache =>
                "Dans RANes : ce n'est pas un journal classique. Vérifie surtout que RetroAchievements est bien actif pour mettre à jour `RACache` et `RALog.txt`.",
            StrategieRenseignementJeuEmulateurLocal.RAVBARACache =>
                "Dans RAVBA : ce n'est pas un journal classique. Vérifie surtout que RetroAchievements est bien actif pour mettre à jour `RACache` et `RALog.txt`.",
            StrategieRenseignementJeuEmulateurLocal.RASnes9xRACache =>
                "Dans RASnes9x : ce n'est pas un journal classique. Vérifie surtout que RetroAchievements est bien actif pour mettre à jour `RACache` et `RALog.txt`.",
            StrategieRenseignementJeuEmulateurLocal.FlycastConfig =>
                "Dans Flycast : active l'écriture de `flycast.log` à la racine. Compagnon s'appuie d'abord sur ce journal, puis sur le chemin du jeu lancé si besoin.",
            _ => string.Empty,
        };
    }

    /*
     * Décrit le niveau de confiance de la détection locale pour un émulateur.
     */
    private static string ConstruireTexteConfianceDetectionEmulateur(
        DefinitionEmulateurLocal definition
    )
    {
        return definition.StrategieRenseignementJeu switch
        {
            StrategieRenseignementJeuEmulateurLocal.BizHawkConfig =>
                "Confiance : bonne. Compagnon s'appuie sur EmuHawk et sur retroachievements-game-log.json, avec config.ini en secours pour la ROM.",
            StrategieRenseignementJeuEmulateurLocal.DolphinConfig =>
                "Confiance : bonne. Compagnon s'appuie d'abord sur dolphin.log, avec le processus Dolphin en secours.",
            StrategieRenseignementJeuEmulateurLocal.Project64RACache =>
                "Confiance : excellente. Compagnon croise le processus émulateur avec RACache et RALog.txt.",
            StrategieRenseignementJeuEmulateurLocal.RALibretroRACache =>
                "Confiance : excellente. Compagnon croise le processus émulateur avec RACache et RALog.txt.",
            StrategieRenseignementJeuEmulateurLocal.RANesRACache =>
                "Confiance : excellente. Compagnon croise le processus émulateur avec RACache et RALog.txt.",
            StrategieRenseignementJeuEmulateurLocal.RAVBARACache =>
                "Confiance : excellente. Compagnon croise le processus émulateur avec RACache et RALog.txt.",
            StrategieRenseignementJeuEmulateurLocal.RASnes9xRACache =>
                "Confiance : excellente. Compagnon croise le processus émulateur avec RACache et RALog.txt.",
            StrategieRenseignementJeuEmulateurLocal.FlycastConfig =>
                "Confiance : bonne. Compagnon s'appuie sur le processus Flycast, sur flycast.log et sur le disque lancé en secours.",
            StrategieRenseignementJeuEmulateurLocal.RetroArchLog =>
                "Confiance : bonne. Compagnon s'appuie sur le processus et sur les journaux locaux de RetroArch.",
            StrategieRenseignementJeuEmulateurLocal.DuckStationLog =>
                "Confiance : bonne. Compagnon s'appuie sur le processus et sur duckstation.log.",
            StrategieRenseignementJeuEmulateurLocal.PCSX2Log =>
                "Confiance : bonne. Compagnon s'appuie sur le processus et sur emulog.txt.",
            StrategieRenseignementJeuEmulateurLocal.PPSSPPLog =>
                "Confiance : bonne. Compagnon s'appuie sur le processus et sur les journaux locaux de PPSSPP.",
            StrategieRenseignementJeuEmulateurLocal.SkyEmuRecentGames =>
                "Confiance : bonne si le serveur HTTP de SkyEmu est activé. Sinon, Compagnon s'appuie sur le processus, la ROM passée en ligne de commande et `recent_games.txt` pour rejouer le dernier titre connu.",
            _ => "Confiance : fragile. Une vérification manuelle peut être nécessaire.",
        };
    }

    /*
     * Résume le dossier de profil généralement utilisé par la version
     * installée d'un émulateur lorsqu'il n'écrit pas ses données à côté
     * de l'exécutable.
     */
    private static string ConstruireTexteProfilInstallationEmulateur(
        DefinitionEmulateurLocal definition
    )
    {
        string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        string localAppData = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData
        );
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        return definition.StrategieRenseignementJeu switch
        {
            StrategieRenseignementJeuEmulateurLocal.DuckStationLog =>
                $"Version installée : DuckStation écrit souvent son profil dans `{Path.Combine(documents, "DuckStation")}`, `{Path.Combine(localAppData, "DuckStation")}` ou `{Path.Combine(appData, "DuckStation")}`.",
            StrategieRenseignementJeuEmulateurLocal.PCSX2Log =>
                $"Version installée : PCSX2 écrit souvent ses journaux dans `{Path.Combine(documents, "PCSX2", "logs")}`, `{Path.Combine(localAppData, "PCSX2", "logs")}` ou `{Path.Combine(appData, "PCSX2", "logs")}`.",
            StrategieRenseignementJeuEmulateurLocal.PPSSPPLog =>
                $"Version installée : PPSSPP écrit souvent son profil dans `{Path.Combine(localAppData, "PPSSPP")}` ou `{Path.Combine(appData, "PPSSPP")}`.",
            StrategieRenseignementJeuEmulateurLocal.SkyEmuRecentGames =>
                $"Version installée : SkyEmu conserve en général ses préférences dans `{Path.Combine(appData, "Sky", "SkyEmu")}` via `SDL_GetPrefPath`.",
            StrategieRenseignementJeuEmulateurLocal.RetroArchLog =>
                $"Version installée : RetroArch peut écrire ses journaux dans le dossier `logs` proche de l'exécutable ou dans un profil local comme `{Path.Combine(appData, "RetroArch", "logs")}`.",
            _ => string.Empty,
        };
    }

    /*
     * Retourne le libellé de validation affiché pour les émulateurs
     * déjà testés.
     */
    private static string ConstruireTexteValidationEmulateur(DefinitionEmulateurLocal definition)
    {
        return definition.StrategieRenseignementJeu switch
        {
            StrategieRenseignementJeuEmulateurLocal.RetroArchLog => "Validé et testé.",
            StrategieRenseignementJeuEmulateurLocal.DuckStationLog => "Validé et testé.",
            _ => string.Empty,
        };
    }

    /*
     * Retourne le dossier indicatif où l'utilisateur retrouvera
     * généralement l'émulateur.
     */
    private static string ConstruireCheminIndicatifEmulateur(DefinitionEmulateurLocal definition)
    {
        string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

        return definition.StrategieRenseignementJeu switch
        {
            StrategieRenseignementJeuEmulateurLocal.RetroArchLog => Path.Combine(
                documents,
                "emulation",
                "RetroArch"
            ),
            StrategieRenseignementJeuEmulateurLocal.BizHawkConfig => Path.Combine(
                documents,
                "emulation",
                "BizHawk"
            ),
            StrategieRenseignementJeuEmulateurLocal.DolphinConfig => Path.Combine(
                documents,
                "emulation",
                "Gamecube"
            ),
            StrategieRenseignementJeuEmulateurLocal.DuckStationLog => Path.Combine(
                documents,
                "DuckStation"
            ),
            StrategieRenseignementJeuEmulateurLocal.PCSX2Log => Path.Combine(documents, "PCSX2"),
            StrategieRenseignementJeuEmulateurLocal.PPSSPPLog => Path.Combine(
                documents,
                "emulation",
                "Playstation Portable"
            ),
            StrategieRenseignementJeuEmulateurLocal.SkyEmuRecentGames => Path.Combine(
                documents,
                "emulation",
                "SkyEmu"
            ),
            StrategieRenseignementJeuEmulateurLocal.Project64RACache => Path.Combine(
                documents,
                "emulation",
                "RAP64"
            ),
            StrategieRenseignementJeuEmulateurLocal.RALibretroRACache => Path.Combine(
                documents,
                "emulation",
                "RALibretro"
            ),
            StrategieRenseignementJeuEmulateurLocal.RANesRACache => Path.Combine(
                documents,
                "emulation",
                "RANes-x64"
            ),
            StrategieRenseignementJeuEmulateurLocal.RAVBARACache => Path.Combine(
                documents,
                "emulation",
                "RAVBA-x64"
            ),
            StrategieRenseignementJeuEmulateurLocal.RASnes9xRACache => Path.Combine(
                documents,
                "emulation",
                "RASnes9x-x64"
            ),
            StrategieRenseignementJeuEmulateurLocal.FlycastConfig => Path.Combine(
                documents,
                "emulation",
                "Dreamcast"
            ),
            _ => "Emplacement local non défini.",
        };
    }

    /*
     * Retourne le chemin indicatif de la source locale attendue
     * pour un émulateur.
     */
    private static string ConstruireCheminIndicatifSourceLocale(DefinitionEmulateurLocal definition)
    {
        string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

        return definition.StrategieRenseignementJeu switch
        {
            StrategieRenseignementJeuEmulateurLocal.RetroArchLog => Path.Combine(
                documents,
                "emulation",
                "RetroArch",
                "logs"
            ),
            StrategieRenseignementJeuEmulateurLocal.BizHawkConfig => Path.Combine(
                documents,
                "emulation",
                "BizHawk",
                "retroachievements-game-log.json"
            ),
            StrategieRenseignementJeuEmulateurLocal.DolphinConfig => Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Dolphin Emulator",
                "Logs",
                "dolphin.log"
            ),
            StrategieRenseignementJeuEmulateurLocal.DuckStationLog => Path.Combine(
                documents,
                "DuckStation",
                "duckstation.log"
            ),
            StrategieRenseignementJeuEmulateurLocal.PCSX2Log => Path.Combine(
                documents,
                "PCSX2",
                "logs",
                "emulog.txt"
            ),
            StrategieRenseignementJeuEmulateurLocal.PPSSPPLog => Path.Combine(
                documents,
                "emulation",
                "Playstation Portable",
                "memstick",
                "PSP",
                "SYSTEM",
                "DUMP",
                "log.txt"
            ),
            StrategieRenseignementJeuEmulateurLocal.SkyEmuRecentGames => Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Sky",
                "SkyEmu",
                "recent_games.txt"
            ),
            StrategieRenseignementJeuEmulateurLocal.Project64RACache => Path.Combine(
                documents,
                "emulation",
                "RAP64",
                "RACache",
                "RALog.txt"
            ),
            StrategieRenseignementJeuEmulateurLocal.RALibretroRACache => Path.Combine(
                documents,
                "emulation",
                "RALibretro",
                "RACache",
                "RALog.txt"
            ),
            StrategieRenseignementJeuEmulateurLocal.RANesRACache => Path.Combine(
                documents,
                "emulation",
                "RANes-x64",
                "RACache",
                "RALog.txt"
            ),
            StrategieRenseignementJeuEmulateurLocal.RAVBARACache => Path.Combine(
                documents,
                "emulation",
                "RAVBA-x64",
                "RACache",
                "RALog.txt"
            ),
            StrategieRenseignementJeuEmulateurLocal.RASnes9xRACache => Path.Combine(
                documents,
                "emulation",
                "RASnes9x-x64",
                "RACache",
                "RALog.txt"
            ),
            StrategieRenseignementJeuEmulateurLocal.FlycastConfig => Path.Combine(
                documents,
                "emulation",
                "Dreamcast",
                "flycast.log"
            ),
            _ => "Chemin local non défini.",
        };
    }

    /*
     * Construit la liste visuelle des jeux récemment joués affichée
     * dans la modale de compte.
     */
    private SystemControls.Border ConstruireBlocJeuxRecemmentJoues(
        IReadOnlyList<JeuRecentAffiche> jeux
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
                    Text = "Jeux récemment joués",
                },
            },
        };

        foreach (JeuRecentAffiche jeu in jeux)
        {
            pile.Children.Add(
                new SystemControls.Border
                {
                    Margin = new Thickness(0, 0, 0, 8),
                    Padding = new Thickness(10, 8, 10, 8),
                    CornerRadius = ObtenirRayonCoins("RayonCoinsPetit", 8),
                    Background = ObtenirPinceauTheme(
                        "PinceauFondSurfaceLegere",
                        ConstantesDesign.CouleurRepliSurfaceTresLegere
                    ),
                    Child = new SystemControls.StackPanel
                    {
                        Children =
                        {
                            new SystemControls.TextBlock
                            {
                                FontWeight = FontWeights.SemiBold,
                                Text = jeu.Titre,
                                TextWrapping = TextWrapping.Wrap,
                            },
                            new SystemControls.TextBlock
                            {
                                Margin = new Thickness(0, 4, 0, 0),
                                Opacity = 0.72,
                                Text = jeu.SousTitre,
                                TextWrapping = TextWrapping.Wrap,
                            },
                        },
                    },
                }
            );
        }

        return new SystemControls.Border { Padding = new Thickness(0), Child = pile };
    }

    /*
     * Construit l'URL publique du profil RetroAchievements de l'utilisateur.
     */
    private static string ConstruireUrlProfilRetroAchievements(string nomUtilisateur)
    {
        return $"https://retroachievements.org/user/{Uri.EscapeDataString(nomUtilisateur)}";
    }

    /*
     * Retourne l'URL du dépôt GitHub du projet.
     */
    private static string ConstruireUrlDepotGitHub()
    {
        return "https://github.com/fredg20/RA-Companion";
    }

    /*
     * Ouvre le profil RetroAchievements de l'utilisateur dans le navigateur
     * par défaut.
     */
    private static void OuvrirProfilRetroAchievements(string nomUtilisateur)
    {
        if (string.IsNullOrWhiteSpace(nomUtilisateur))
        {
            return;
        }

        try
        {
            Process.Start(
                new ProcessStartInfo
                {
                    FileName = ConstruireUrlProfilRetroAchievements(nomUtilisateur),
                    UseShellExecute = true,
                }
            );
        }
        catch { }
    }

    /*
     * Ouvre le dépôt GitHub du projet dans le navigateur par défaut.
     */
    private static void OuvrirDepotGitHub()
    {
        try
        {
            Process.Start(
                new ProcessStartInfo
                {
                    FileName = ConstruireUrlDepotGitHub(),
                    UseShellExecute = true,
                }
            );
        }
        catch { }
    }

    /*
     * Gère le clic sur le bouton ouvrant le dépôt GitHub du projet.
     */
    private void BoutonDepotGitHub_Click(object sender, RoutedEventArgs e)
    {
        OuvrirDepotGitHub();
    }

    /*
     * Transforme un chemin d'image RetroAchievements en URL absolue exploitable
     * par l'interface.
     */
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

    /*
     * Construit l'image d'avatar du compte lorsqu'une URL valide est disponible.
     */
    private static SystemControls.Image? ConstruireImageAvatarCompte(
        string? urlAvatar,
        double largeur = 44,
        double hauteur = 44,
        Thickness? marge = null
    )
    {
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
                Width = largeur,
                Height = hauteur,
                Margin = marge ?? new Thickness(0, 0, 16, 0),
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

    /*
     * Ajuste le pied de la modale de connexion une fois son arbre visuel chargé.
     */
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

    /*
     * Ajuste le pied de la modale de compte une fois son arbre visuel chargé.
     */
    private void DialogueCompte_Chargement(object sender, RoutedEventArgs e)
    {
        if (sender is not UiControls.ContentDialog dialogueCompte)
        {
            return;
        }

        dialogueCompte.Dispatcher.BeginInvoke(
            () => AjusterPiedModaleCompte(dialogueCompte),
            DispatcherPriority.Loaded
        );
    }

    /*
     * Ajuste le pied de la modale de confirmation après son chargement visuel.
     */
    private void DialogueConfirmation_Chargement(object sender, RoutedEventArgs e)
    {
        if (sender is not UiControls.ContentDialog dialogueConfirmation)
        {
            return;
        }

        dialogueConfirmation.Dispatcher.BeginInvoke(
            () => AjusterPiedModaleConnexion(dialogueConfirmation),
            DispatcherPriority.Loaded
        );
    }

    /*
     * Centre et épure les boutons affichés dans le pied de la modale
     * de connexion.
     */
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

        string[] textesAutorises = [texteBoutonPrincipal, texteBoutonSecondaire];

        boutonSecondaire.MinWidth = 120;
        boutonPrincipal.MinWidth = 120;

        foreach (SystemControls.Button bouton in boutons)
        {
            string texte = TexteBouton(bouton);

            if (
                textesAutorises.Any(texteAutorise =>
                    !string.IsNullOrWhiteSpace(texteAutorise)
                    && texte.Equals(texteAutorise, StringComparison.OrdinalIgnoreCase)
                )
            )
            {
                bouton.Visibility = Visibility.Visible;
                bouton.IsEnabled = true;
                continue;
            }

            bouton.Visibility = Visibility.Collapsed;
            bouton.IsEnabled = false;
        }

        SystemControls.Panel? conteneurBoutons = TrouverPanneauCommun(
            boutonSecondaire,
            boutonPrincipal
        );

        if (conteneurBoutons is null)
        {
            return;
        }

        conteneurBoutons.HorizontalAlignment = HorizontalAlignment.Center;
    }

    /*
     * Centre les actions visibles dans le pied de la modale de compte.
     */
    private static void AjusterPiedModaleCompte(UiControls.ContentDialog dialogueCompte)
    {
        List<SystemControls.Button> boutons =
        [
            .. TrouverDescendants<SystemControls.Button>(dialogueCompte),
        ];

        string texteBoutonPrincipal = dialogueCompte.PrimaryButtonText?.Trim() ?? string.Empty;
        string texteBoutonSecondaire = dialogueCompte.SecondaryButtonText?.Trim() ?? string.Empty;
        string texteBoutonFermeture = dialogueCompte.CloseButtonText?.Trim() ?? string.Empty;

        foreach (SystemControls.Button bouton in boutons)
        {
            string texte = TexteBouton(bouton);

            if (
                texte.Equals(texteBoutonPrincipal, StringComparison.OrdinalIgnoreCase)
                || texte.Equals(texteBoutonSecondaire, StringComparison.OrdinalIgnoreCase)
                || texte.Equals(texteBoutonFermeture, StringComparison.OrdinalIgnoreCase)
            )
            {
                bouton.MinWidth = 120;
                bouton.Visibility = Visibility.Visible;
                bouton.IsEnabled = true;
            }
        }

        SystemControls.Button? boutonPrincipal = boutons.Find(bouton =>
            TexteBouton(bouton).Equals(texteBoutonPrincipal, StringComparison.OrdinalIgnoreCase)
        );
        SystemControls.Button? boutonSecondaire = boutons.Find(bouton =>
            TexteBouton(bouton).Equals(texteBoutonSecondaire, StringComparison.OrdinalIgnoreCase)
        );

        if (boutonPrincipal is null || boutonSecondaire is null)
        {
            return;
        }

        SystemControls.Panel? conteneurBoutons = TrouverPanneauCommun(
            boutonPrincipal,
            boutonSecondaire
        );

        if (conteneurBoutons is not null)
        {
            conteneurBoutons.HorizontalAlignment = HorizontalAlignment.Center;
        }
    }

    /*
     * Ouvre la modale de connexion depuis le bouton d'entête.
     */
    private async void ConfigurerConnexion_Click(object sender, RoutedEventArgs e)
    {
        MemoriserGeometrieFenetre();
        ArreterActualisationAutomatique();
        DefinirEtatConnexion("Modification en cours");

        await AfficherModaleConnexionAsync();
    }

    /*
     * Ouvre la modale de compte depuis le bouton utilisateur.
     */
    private async void AfficherCompte_Click(object sender, RoutedEventArgs e)
    {
        await ExecuterActionAfficherCompteAsync();
    }

    /*
     * Ouvre la modale d'aide depuis le bouton correspondant.
     */
    private async void AfficherAide_Click(object sender, RoutedEventArgs e)
    {
        await ExecuterActionAfficherAideAsync();
    }

    /*
     * Affiche la connexion ou le compte selon l'état actuel
     * de la configuration.
     */
    private async Task ExecuterActionAfficherCompteAsync()
    {
        if (!ConfigurationConnexionEstComplete())
        {
            await AfficherModaleConnexionAsync();
            return;
        }

        await AfficherModaleCompteAsync();
    }

    /*
     * Centralise l'ouverture de la modale d'aide.
     */
    private async Task ExecuterActionAfficherAideAsync()
    {
        DefinirMiseEnAvantBoutonAide(active: false);
        await AfficherModaleAideAsync();
    }

    /*
     * Active une seule fois la mise en avant du bouton Aide au premier lancement
     * visible de Compagnon, puis mémorise immédiatement cet état.
     */
    private async Task InitialiserMiseEnAvantBoutonAidePremiereUtilisationAsync()
    {
        bool afficherHalo = !_configurationConnexion.HaloBoutonAidePremiereUtilisationDejaAffiche;
        DefinirMiseEnAvantBoutonAide(afficherHalo);

        if (!afficherHalo)
        {
            return;
        }

        _configurationConnexion.HaloBoutonAidePremiereUtilisationDejaAffiche = true;

        try
        {
            await _serviceConfigurationLocale.SauvegarderEtatApplicationAsync(
                _configurationConnexion
            );
        }
        catch { }
    }

    /*
     * Applique ou retire le halo doré du bouton Aide afin de guider la
     * première découverte de l'application.
     */
    private void DefinirMiseEnAvantBoutonAide(bool active)
    {
        if (BoutonAide is null)
        {
            return;
        }

        if (!active)
        {
            BoutonAide.Effect = null;
            BoutonAide.ClearValue(SystemControls.Control.BorderBrushProperty);
            BoutonAide.ClearValue(SystemControls.Control.BorderThicknessProperty);
            return;
        }

        BoutonAide.BorderBrush = ObtenirPinceauTheme(
            "PinceauAccentHardcore",
            ConstantesDesign.CouleurRepliAccentHardcore
        );
        BoutonAide.BorderThickness = new Thickness(ConstantesDesign.EpaisseurContourAccent);
        BoutonAide.Effect = new DropShadowEffect
        {
            Color = ObtenirCouleurTheme(
                "CouleurAccentHardcore",
                ConstantesDesign.CouleurRepliAccentHardcore
            ),
            BlurRadius = ConstantesDesign.FlouHaloHardcore,
            ShadowDepth = 0,
            Opacity = ConstantesDesign.OpaciteHaloHardcore,
        };
    }

    /*
     * Met à jour le résumé de connexion visible dans l'entête et le ViewModel.
     */
    private void MettreAJourResumeConnexion()
    {
        if (string.IsNullOrWhiteSpace(_configurationConnexion.Pseudo))
        {
            _etatConnexionCourant = "Non configuré";
            _vueModele.Compte.LibelleBouton = "Connexion";
        }
        else
        {
            _vueModele.Compte.LibelleBouton = ObtenirLibelleBoutonCompte();
        }

        RafraichirModuleBibliotheque();
        MettreAJourNoticeCompteEntete();
    }

    /*
     * Retourne le libellé à afficher sur le bouton de compte.
     */
    private string ObtenirLibelleBoutonCompte()
    {
        return string.IsNullOrWhiteSpace(_configurationConnexion.Pseudo)
            ? "Connexion"
            : _configurationConnexion.Pseudo;
    }

    /*
     * Met à jour la notice d'état du compte et déclenche une synchronisation
     * ciblée si l'état visible change.
     */
    private void MettreAJourNoticeCompteEntete()
    {
        if (
            ZoneEtatCompteUtilisateur is null
            || BadgeEtatCompteUtilisateur is null
            || TexteEtatCompteUtilisateur is null
            || TexteSousEtatCompteUtilisateur is null
        )
        {
            return;
        }

        if (!ConfigurationConnexionEstComplete())
        {
            _vueModele.Compte.NoticeVisible = false;
            _vueModele.Compte.SousEtatVisible = false;
            _vueModele.Compte.EtatNotice = string.Empty;
            _vueModele.Compte.SousEtatNotice = string.Empty;
            _vueModele.Compte.ToolTipNotice = string.Empty;
            _vueModele.Compte.FondNotice = Brushes.Transparent;
            _vueModele.Compte.BordureNotice = Brushes.Transparent;
            _signatureDerniereNoticeCompteJournalisee = string.Empty;
            ReinitialiserSuiviEtatJeuVisible();
            MettreAJourActionRejouerJeuEnCours(_configurationConnexion.DernierJeuAffiche);
            return;
        }

        int identifiantJeuAffiche = DeterminerIdentifiantJeuNoticeCompte();
        string texteIdentifiantJeu =
            identifiantJeuAffiche > 0
                ? identifiantJeuAffiche.ToString(CultureInfo.CurrentCulture)
                : string.Empty;
        string texteIdentifiantJeuAffiche = string.Empty;

        if (EtatLocalEmulateurEstActifPourNotice())
        {
            _vueModele.Compte.EtatNotice = "En jeu";
            _vueModele.Compte.SousEtatNotice = texteIdentifiantJeuAffiche;
            _vueModele.Compte.SousEtatVisible = !string.IsNullOrWhiteSpace(
                texteIdentifiantJeuAffiche
            );
            (Brush fondNoticeLocal, Brush bordureNoticeLocale) = ObtenirCouleursNoticeCompteEntete(
                "En jeu"
            );
            _vueModele.Compte.FondNotice = fondNoticeLocal;
            _vueModele.Compte.BordureNotice = bordureNoticeLocale;

            _vueModele.Compte.NoticeVisible = true;
            _vueModele.Compte.ToolTipNotice =
                identifiantJeuAffiche > 0
                    ? $"En jeu{Environment.NewLine}Game ID {identifiantJeuAffiche.ToString(CultureInfo.CurrentCulture)}"
                    : "En jeu (détection locale)";
            JournaliserNoticeCompteEntete("En jeu", texteIdentifiantJeu, "local");
            EnregistrerEtatJeuVisibleEtSynchroniserSiNecessaire("En jeu");
            MettreAJourActionRejouerJeuEnCours(_configurationConnexion.DernierJeuAffiche);
            return;
        }

        CompteAffiche compte = ServicePresentationCompte.Construire(
            new DonneesCompteUtilisateur
            {
                Profil = _dernierProfilUtilisateurCharge,
                Resume = _dernierResumeUtilisateurCharge,
            },
            _configurationConnexion.Pseudo
        );

        if (string.IsNullOrWhiteSpace(compte.Statut))
        {
            _vueModele.Compte.NoticeVisible = false;
            _vueModele.Compte.SousEtatVisible = false;
            _vueModele.Compte.EtatNotice = string.Empty;
            _vueModele.Compte.SousEtatNotice = string.Empty;
            _vueModele.Compte.ToolTipNotice = string.Empty;
            _vueModele.Compte.FondNotice = Brushes.Transparent;
            _vueModele.Compte.BordureNotice = Brushes.Transparent;
            ReinitialiserSuiviEtatJeuVisible();
            MettreAJourActionRejouerJeuEnCours(_configurationConnexion.DernierJeuAffiche);
            return;
        }

        _vueModele.Compte.EtatNotice = compte.Statut.Trim();

        bool afficherSousStatut = !string.IsNullOrWhiteSpace(texteIdentifiantJeuAffiche);
        _vueModele.Compte.SousEtatNotice = texteIdentifiantJeuAffiche;
        _vueModele.Compte.SousEtatVisible = afficherSousStatut;
        (Brush fondNoticeCompte, Brush bordureNoticeCompte) = ObtenirCouleursNoticeCompteEntete(
            compte.Statut
        );
        _vueModele.Compte.FondNotice = fondNoticeCompte;
        _vueModele.Compte.BordureNotice = bordureNoticeCompte;

        _vueModele.Compte.NoticeVisible = true;
        _vueModele.Compte.ToolTipNotice =
            identifiantJeuAffiche > 0
                ? $"{compte.Statut}{Environment.NewLine}Game ID {identifiantJeuAffiche.ToString(CultureInfo.CurrentCulture)}"
                : compte.Statut;
        JournaliserNoticeCompteEntete(compte.Statut, texteIdentifiantJeu, "api");
        EnregistrerEtatJeuVisibleEtSynchroniserSiNecessaire(compte.Statut);
        MettreAJourActionRejouerJeuEnCours(_configurationConnexion.DernierJeuAffiche);
    }

    /*
     * Réinitialise l'historique interne du dernier état de jeu visible
     * dans l'entête.
     */
    private void ReinitialiserSuiviEtatJeuVisible()
    {
        _suiviEtatJeuVisibleInitialise = false;
        _signatureDernierEtatJeuVisible = string.Empty;
        _horodatageDerniereSynchronisationEtatJeuUtc = DateTimeOffset.MinValue;
    }

    /*
     * Enregistre l'état de jeu visible et demande une synchronisation
     * ciblée si sa valeur change.
     */
    private void EnregistrerEtatJeuVisibleEtSynchroniserSiNecessaire(string etatVisible)
    {
        string signatureEtat = string.IsNullOrWhiteSpace(etatVisible)
            ? string.Empty
            : etatVisible.Trim();

        if (!_suiviEtatJeuVisibleInitialise)
        {
            _suiviEtatJeuVisibleInitialise = true;
            _signatureDernierEtatJeuVisible = signatureEtat;
            return;
        }

        if (
            string.Equals(
                _signatureDernierEtatJeuVisible,
                signatureEtat,
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            return;
        }

        _signatureDernierEtatJeuVisible = signatureEtat;
        DemanderSynchronisationCibleeEtatJeu($"etat_visible={signatureEtat}");
    }

    /*
     * Déclenche ou diffère une synchronisation ciblée de l'état du jeu
     * en respectant le debounce défini.
     */
    private void DemanderSynchronisationCibleeEtatJeu(string raison)
    {
        if (!ConfigurationConnexionEstComplete())
        {
            return;
        }

        DateTimeOffset maintenant = DateTimeOffset.UtcNow;
        if (
            _horodatageDerniereSynchronisationEtatJeuUtc != DateTimeOffset.MinValue
            && maintenant - _horodatageDerniereSynchronisationEtatJeuUtc
                < IntervalleDebounceSynchronisationEtatJeu
        )
        {
            JournaliserDiagnosticAffichageJeu("synchronisation_etat_jeu_ignoree", raison);
            return;
        }

        _horodatageDerniereSynchronisationEtatJeuUtc = maintenant;

        if (_chargementJeuEnCoursActif)
        {
            _actualisationApiCibleeEnAttente = true;
            JournaliserDiagnosticAffichageJeu("synchronisation_etat_jeu_differee", raison);
            return;
        }

        JournaliserDiagnosticAffichageJeu("synchronisation_etat_jeu", raison);
        _ = SynchroniserEtatJeuApresChangementAsync();
    }

    /*
     * Recharge le jeu courant après un changement d'état détecté
     * dans l'interface.
     */
    private async Task SynchroniserEtatJeuApresChangementAsync()
    {
        try
        {
            await ChargerJeuEnCoursAsync(false, true);
            RedemarrerMinuteurActualisationApi();
        }
        catch { }
    }

    /*
     * Détermine le Game ID le plus pertinent à afficher dans la notice
     * de compte.
     */
    private int DeterminerIdentifiantJeuNoticeCompte()
    {
        if (EtatLocalEmulateurEstActifPourNotice())
        {
            return _identifiantJeuLocalActif > 0 ? _identifiantJeuLocalActif : 0;
        }

        if (_dernierIdentifiantJeuApi > 0)
        {
            return _dernierIdentifiantJeuApi;
        }

        if (_dernierResumeUtilisateurCharge?.LastGameId > 0)
        {
            return _dernierResumeUtilisateurCharge.LastGameId;
        }

        if (_dernierProfilUtilisateurCharge?.LastGameId > 0)
        {
            return _dernierProfilUtilisateurCharge.LastGameId;
        }

        return 0;
    }

    /*
     * Retourne les couleurs associées au statut visible dans la notice
     * de compte.
     */
    private (Brush Fond, Brush Bordure) ObtenirCouleursNoticeCompteEntete(string statut)
    {
        if (statut.Contains("En jeu", StringComparison.OrdinalIgnoreCase))
        {
            return (
                Brushes.Transparent,
                ObtenirPinceauTheme(
                    "PinceauFondNoticeSucces",
                    ConstantesDesign.CouleurRepliFondNoticeSucces
                )
            );
        }

        if (statut.Contains("Actif", StringComparison.OrdinalIgnoreCase))
        {
            return (
                Brushes.Transparent,
                ObtenirPinceauTheme(
                    "PinceauFondNoticeInformation",
                    ConstantesDesign.CouleurRepliFondNoticeInformation
                )
            );
        }

        if (statut.Contains("Inactif", StringComparison.OrdinalIgnoreCase))
        {
            return (
                Brushes.Transparent,
                ObtenirPinceauTheme(
                    "PinceauFondNoticeNeutre",
                    ConstantesDesign.CouleurRepliFondNoticeNeutre
                )
            );
        }

        return (
            Brushes.Transparent,
            ObtenirPinceauTheme(
                "PinceauFondNoticeInformation",
                ConstantesDesign.CouleurRepliFondNoticeInformation
            )
        );
    }

    /*
     * Journalise les changements utiles de la notice de compte sans
     * dupliquer les mêmes entrées.
     */
    private void JournaliserNoticeCompteEntete(string statut, string identifiantJeu, string source)
    {
        string signature = $"{source}|{statut}|{identifiantJeu}";

        if (
            string.Equals(
                _signatureDerniereNoticeCompteJournalisee,
                signature,
                StringComparison.Ordinal
            )
        )
        {
            return;
        }

        _signatureDerniereNoticeCompteJournalisee = signature;
        ServiceResolutionJeuLocal.JournaliserEvenementInterface(
            "notice_compte",
            $"source={source};statut={statut};gameId={identifiantJeu}"
        );
    }

    /*
     * Met à jour l'état de connexion courant et le répercute dans le ViewModel.
     */
    private void DefinirEtatConnexion(string etatConnexion)
    {
        _etatConnexionCourant = etatConnexion;
        _vueModele.EtatConnexion = etatConnexion;
        MettreAJourResumeConnexion();
    }

    /*
     * Indique si les informations minimales de connexion sont disponibles localement.
     */
    private bool ConfigurationConnexionEstComplete()
    {
        return !string.IsNullOrWhiteSpace(_configurationConnexion.Pseudo)
            && !string.IsNullOrWhiteSpace(_configurationConnexion.CleApiWeb);
    }

    /*
     * Retourne le texte nettoyé porté par un bouton WPF.
     */
    private static string TexteBouton(SystemControls.Button bouton)
    {
        return bouton.Content?.ToString()?.Trim() ?? string.Empty;
    }

    /*
     * Recherche le premier panneau ancêtre commun à deux éléments visuels.
     */
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

    /*
     * Parcourt récursivement l'arbre visuel pour retourner tous les descendants
     * du type demandé.
     */
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

    /*
     * Recherche le premier ancêtre visuel ou logique correspondant au type
     * demandé à partir d'un élément de l'arbre WPF.
     */
    private Point ConvertirPointElementEnEcranWpf(Visual element, Point pointLocal)
    {
        Point pointEcranDevice = element.PointToScreen(pointLocal);
        PresentationSource? sourcePresentation = PresentationSource.FromVisual(this);

        if (sourcePresentation?.CompositionTarget is null)
        {
            return pointEcranDevice;
        }

        return sourcePresentation.CompositionTarget.TransformFromDevice.Transform(pointEcranDevice);
    }

    private static TElement? TrouverAncetre<TElement>(DependencyObject? element)
        where TElement : DependencyObject
    {
        DependencyObject? courant = element;

        while (courant is not null)
        {
            if (courant is TElement ancetre)
            {
                return ancetre;
            }

            courant =
                VisualTreeHelper.GetParent(courant)
                ?? (courant as FrameworkElement)?.Parent
                ?? LogicalTreeHelper.GetParent(courant);
        }

        return null;
    }

    /*
     * Force la modale d'aide à recalculer son hôte de défilement après
     * l'ouverture ou la fermeture d'une section rabattable.
     */
    private static void RafraichirDefilementModaleAide(DependencyObject? source)
    {
        if (source is not DispatcherObject objetDistribue || objetDistribue.Dispatcher is null)
        {
            return;
        }

        void Rafraichir()
        {
            if (source is not UIElement elementSource)
            {
                return;
            }

            UiControls.ContentDialog? dialogue = TrouverAncetre<UiControls.ContentDialog>(
                elementSource
            );

            elementSource.InvalidateMeasure();
            elementSource.InvalidateArrange();
            elementSource.UpdateLayout();

            if (dialogue is not null)
            {
                dialogue.InvalidateMeasure();
                dialogue.InvalidateArrange();
                dialogue.UpdateLayout();

                foreach (
                    SystemControls.ScrollViewer scrollViewer in TrouverDescendants<SystemControls.ScrollViewer>(
                        dialogue
                    )
                )
                {
                    scrollViewer.InvalidateMeasure();
                    scrollViewer.InvalidateArrange();
                    scrollViewer.UpdateLayout();
                    scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset);
                }
            }
        }

        objetDistribue.Dispatcher.BeginInvoke((Action)Rafraichir, DispatcherPriority.Loaded);
        objetDistribue.Dispatcher.BeginInvoke(
            () =>
            {
                Rafraichir();
            },
            DispatcherPriority.ContextIdle
        );
    }
}
