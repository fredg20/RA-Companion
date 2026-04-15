/*
 * Regroupe la logique de connexion, de compte et d'aide utilisateur, ainsi
 * que les modales associÃ©es et les outils de diagnostic reliÃ©s aux Ã©mulateurs
 * et Ã  l'Ã©tat visible du compte dans l'interface principale.
 */
using System.Diagnostics;
using System.Globalization;
using System.IO;
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
 * Porte la partie de la fenÃªtre principale qui gÃ¨re la connexion utilisateur,
 * la modale Compte, la modale Aide et la notice d'Ã©tat visible dans l'entÃªte.
 */
public partial class MainWindow
{
    /*
     * Retourne un pinceau issu du thÃ¨me courant ou une couleur de repli
     * lorsque la ressource demandÃ©e n'est pas disponible.
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
     * Active ou dÃ©sactive la racine visuelle qui porte les modales.
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
     * Affiche ou masque le voile de fond dÃ©diÃ© Ã  la modale de connexion.
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
     * Calcule une largeur de contenu de modale Ã  partir de la largeur courante
     * de la fenÃªtre principale afin d'Ã©viter les largeurs figÃ©es sur petits
     * Ã©crans.
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

        return Math.Clamp(largeurDisponible, largeurMinimale, largeurCible);
    }

    /*
     * Retourne la largeur extÃ©rieure d'une modale Ã  partir de sa largeur de
     * contenu pour faciliter les bornes de dialogue.
     */
    private static double CalculerLargeurExterieureModale(double largeurContenu)
    {
        return largeurContenu + (MargeInterieureModaleConnexion * 2);
    }

    /*
     * Calcule une hauteur maximale de contenu de modale cohÃ©rente avec la
     * hauteur disponible de la fenÃªtre courante.
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
     * sa largeur de contenu calculÃ©e.
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
        SolidColorBrush fondCarte = new(Color.FromRgb(36, 36, 40));
        SolidColorBrush fondChamp = new(Color.FromRgb(24, 24, 27));
        SolidColorBrush bordure = ObtenirPinceauTheme(
            "CardStrokeColorDefaultBrush",
            Color.FromRgb(78, 78, 86)
        );
        SolidColorBrush textePrincipal = ObtenirPinceauTheme(
            "TextFillColorPrimaryBrush",
            Color.FromRgb(243, 244, 246)
        );
        SolidColorBrush texteSecondaire = ObtenirPinceauTheme(
            "TextFillColorSecondaryBrush",
            Color.FromRgb(191, 197, 206)
        );
        SolidColorBrush fondBoutonPrimaire = ObtenirPinceauTheme(
            "SystemAccentColorBrush",
            Color.FromRgb(28, 100, 242)
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
            Foreground = Brushes.IndianRed,
            TextWrapping = TextWrapping.Wrap,
        };

        SystemControls.Button boutonEnregistrer = new()
        {
            Content = "Enregistrer",
            MinWidth = 120,
            IsDefault = true,
            Padding = new Thickness(14, 6, 14, 6),
            Background = fondBoutonPrimaire,
            Foreground = Brushes.White,
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
                                    "Entre ton pseudo et ta clÃ© Web API pour synchroniser ton dernier jeu jouÃ©, ta progression et tes succÃ¨s rÃ©cents.",
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
                                Text = "ClÃ© API",
                            },
                            champCleApi,
                            new SystemControls.TextBlock
                            {
                                Margin = new Thickness(0, 10, 0, 0),
                                Opacity = 0.68,
                                Foreground = texteSecondaire,
                                Text =
                                    "Tu peux retrouver cette clÃ© depuis ton compte RetroAchievements, dans la section dÃ©diÃ©e Ã  l'API Web.",
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
                    texteErreur.Text = "Renseigne ton pseudo et ta clÃ© Web API pour continuer.";
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
                    ? "Impossible de vÃ©rifier ce compte pour le moment. VÃ©rifie ta connexion et rÃ©essaie."
                    : $"Impossible de vÃ©rifier ce compte pour le moment. {exception.Message}";
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
                    ? "Le compte a Ã©tÃ© validÃ©, mais son enregistrement local a Ã©chouÃ©. La session courante peut fonctionner, mais la reconnexion automatique au prochain dÃ©marrage n'est pas garantie."
                    : $"Le compte a Ã©tÃ© validÃ©, mais son enregistrement local a Ã©chouÃ©. {exception.Message}",
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
     * les plus rÃ©centes disponibles.
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
    private async Task AfficherModaleAideAsync()
    {
        _ = VerifierMiseAJourApplicationSiNecessaireAsync();
        double largeurContenuAide = CalculerLargeurContenuModale(
            ConstantesDesign.LargeurCarteSecondaire + ConstantesDesign.EspaceHeroique,
            ConstantesDesign.EspaceFenetreStandard
        );
        double hauteurMaximaleAide = CalculerHauteurMaximaleContenuModale();
        bool modaleAideCompacte = EstModaleCompacte(
            largeurContenuAide,
            ConstantesDesign.LargeurCarteSecondaire + ConstantesDesign.EspaceCompact
        );
        _modaleAideCompacteCourante = modaleAideCompacte;
        List<SystemControls.Expander> sectionsAide = [];

        SystemControls.StackPanel contenu = new()
        {
            Width = largeurContenuAide,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = ConstantesDesign.AucuneMarge,
            Children =
            {
                ConstruireBandeauIntroductionAide(largeurContenuAide, modaleAideCompacte),
                ConstruireBarreActionsAide(sectionsAide),
                ConstruireBlocAide(
                    "DÃ©marrage rapide",
                    [
                        "Ouvre Profil puis connecte ton compte RetroAchievements.",
                        "Lance un jeu dans un Ã©mulateur compatible et attends quelques secondes.",
                        "Si le jeu affichÃ© n'est pas le bon, utilise Recharger avant de redÃ©marrer l'Ã©mulateur.",
                    ],
                    "Les trois premiÃ¨res Ã©tapes Ã  suivre.",
                    true,
                    sectionsAide
                ),
                ConstruireBlocAide(
                    "Comprendre l'Ã©cran",
                    [
                        "Le grand libellÃ© indique si Compagnon voit un Dernier jeu ou une session En jeu.",
                        "La carte principale affiche le jeu dÃ©tectÃ©, ses informations et les actions utiles du moment.",
                        "Les deux sections de rÃ©trosuccÃ¨s montrent le succÃ¨s mis en avant, la grille complÃ¨te et la progression Softcore / Hardcore.",
                    ],
                    "Les repÃ¨res essentiels de l'interface.",
                    false,
                    sectionsAide
                ),
                ConstruireBlocAide(
                    "Si un Ã©lÃ©ment manque",
                    [
                        "VÃ©rifie d'abord que le compte est connectÃ© et qu'aucune synchronisation n'est en cours.",
                        "Ouvre Logs des Ã©mulateurs pour voir le chemin exact attendu sur ce PC.",
                        "Si la dÃ©tection hÃ©site, choisis manuellement le bon exÃ©cutable.",
                        "En dernier recours, relance d'abord l'Ã©mulateur, puis Compagnon.",
                    ],
                    "La procÃ©dure la plus courte pour diagnostiquer un manque.",
                    false,
                    sectionsAide
                ),
                ConstruireBlocAideMiseAJourApplication(),
                ConstruireBlocAideLogsEmulateurs(),
            },
        };

        SystemControls.ScrollViewer defileurAide = new()
        {
            VerticalScrollBarVisibility = SystemControls.ScrollBarVisibility.Hidden,
            HorizontalScrollBarVisibility = SystemControls.ScrollBarVisibility.Disabled,
            MaxHeight = hauteurMaximaleAide,
            Content = contenu,
        };
        defileurAide.MouseEnter += (_, _) =>
            defileurAide.VerticalScrollBarVisibility = SystemControls.ScrollBarVisibility.Auto;
        defileurAide.MouseLeave += (_, _) =>
            defileurAide.VerticalScrollBarVisibility = SystemControls.ScrollBarVisibility.Hidden;

        SystemControls.Border conteneurContenu = new()
        {
            Padding = ConstantesDesign.PaddingCarteSecondaire,
            HorizontalAlignment = HorizontalAlignment.Center,
            CornerRadius = ObtenirRayonCoins("RayonCoinsStandard", ConstantesDesign.EspaceStandard),
            Child = defileurAide,
        };

        UiControls.ContentDialog dialogueAide = new(RacineModales)
        {
            Title = "Aide",
            Content = conteneurContenu,
            MinWidth = CalculerLargeurExterieureModale(largeurContenuAide),
            CloseButtonText = "Fermer",
            DefaultButton = UiControls.ContentDialogButton.Close,
        };
        AppliquerTypographieResponsiveSurObjet(dialogueAide);

        try
        {
            DefinirEtatModalesActif(true);
            await dialogueAide.ShowAsync();
        }
        finally
        {
            DefinirEtatModalesActif(false);
        }
    }

    /*
     * Construit le bandeau d'introduction affichÃ© en haut de la modale d'aide
     * afin de mieux rÃ©sumer son rÃ´le et l'Ã©tat courant de la connexion.
     */
    private SystemControls.Border ConstruireBandeauIntroductionAide(
        double largeurContenuAide,
        bool modaleAideCompacte
    )
    {
        string texteCompte =
            ConfigurationConnexionEstComplete()
            && !string.IsNullOrWhiteSpace(_configurationConnexion.Pseudo)
                ? $"Compte connectÃ© : {_configurationConnexion.Pseudo}"
                : "Compte non connectÃ© : ouvre Profil pour commencer";

        SystemControls.WrapPanel capsules = new()
        {
            Margin = new Thickness(0, 10, 0, 0),
            ItemHeight = double.NaN,
            Orientation = SystemControls.Orientation.Horizontal,
        };
        capsules.Children.Add(ConstruireCapsuleAide("DÃ©marrage"));
        capsules.Children.Add(ConstruireCapsuleAide("Diagnostic"));
        capsules.Children.Add(ConstruireCapsuleAide("Mise Ã  jour"));
        capsules.Children.Add(ConstruireCapsuleAide("Ã‰mulateurs"));

        return new SystemControls.Border
        {
            Margin = new Thickness(0, 0, 0, 13),
            Padding = new Thickness(13, 13, 13, 13),
            CornerRadius = ObtenirRayonCoins("RayonCoinsStandard", ConstantesDesign.EspaceStandard),
            Background = new SolidColorBrush(Color.FromArgb(34, 255, 255, 255)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(48, 255, 255, 255)),
            BorderThickness = ConstantesDesign.EpaisseurContourFin,
            Child = new SystemControls.StackPanel
            {
                Children =
                {
                    new SystemControls.TextBlock
                    {
                        FontSize = ConstantesDesign.TaillePoliceTitreSection,
                        FontWeight = FontWeights.SemiBold,
                        Text = "Aide rapide et diagnostic",
                        TextWrapping = TextWrapping.Wrap,
                    },
                    new SystemControls.TextBlock
                    {
                        Margin = new Thickness(0, 6, 0, 0),
                        Opacity = 0.84,
                        Text =
                            "Cette fenÃªtre t'aide Ã  dÃ©marrer, comprendre l'Ã©cran, vÃ©rifier les mises Ã  jour et diagnostiquer les chemins lus par Compagnon.",
                        TextWrapping = TextWrapping.Wrap,
                    },
                    new SystemControls.TextBlock
                    {
                        Margin = new Thickness(0, 8, 0, 0),
                        FontWeight = FontWeights.SemiBold,
                        Opacity = 0.78,
                        Text = texteCompte,
                        TextWrapping = TextWrapping.Wrap,
                    },
                    capsules,
                    ConstruireGrilleSyntheseAide(largeurContenuAide, modaleAideCompacte),
                },
            },
        };
    }

    /*
     * Construit la barre d'actions placÃ©e au-dessus des sections de la modale
     * d'aide pour accÃ©lÃ©rer la navigation et l'accÃ¨s aux diagnostics.
     */
    private SystemControls.Border ConstruireBarreActionsAide(
        IReadOnlyList<SystemControls.Expander> sectionsAide
    )
    {
        UiControls.Button boutonDeplier = new()
        {
            Content = "DÃ©plier tout",
            Padding = new Thickness(12, 4, 12, 4),
            Margin = new Thickness(0, 0, 8, 8),
        };
        UiControls.Button boutonReduire = new()
        {
            Content = "RÃ©duire tout",
            Padding = new Thickness(12, 4, 12, 4),
            Margin = new Thickness(0, 0, 8, 8),
        };
        UiControls.Button boutonJournal = new()
        {
            Content = "Ouvrir le journal local",
            Padding = new Thickness(12, 4, 12, 4),
            Margin = new Thickness(0, 0, 8, 8),
        };
        UiControls.Button boutonNotice = new()
        {
            Content = "Ouvrir le manuel",
            Padding = new Thickness(12, 4, 12, 4),
            Margin = new Thickness(0, 0, 0, 8),
        };

        boutonDeplier.Click += (_, _) => DefinirEtatSectionsAide(sectionsAide, true);
        boutonReduire.Click += (_, _) => DefinirEtatSectionsAide(sectionsAide, false);
        boutonJournal.Click += (_, _) =>
        {
            string cheminJournal = ServiceSondeLocaleEmulateurs.ObtenirCheminJournal();

            if (File.Exists(cheminJournal))
            {
                OuvrirFichierExterne(cheminJournal);
                return;
            }

            OuvrirDossierContenant(cheminJournal);
        };
        boutonNotice.Click += (_, _) =>
        {
            string cheminNotice = TrouverCheminNoticeAide();

            if (File.Exists(cheminNotice))
            {
                OuvrirFichierExterne(cheminNotice);
            }
        };
        boutonNotice.IsEnabled = File.Exists(TrouverCheminNoticeAide());

        return new SystemControls.Border
        {
            Margin = new Thickness(0, 0, 0, 13),
            Padding = new Thickness(13, 8, 13, 5),
            CornerRadius = ObtenirRayonCoins("RayonCoinsPetit", 8),
            Background = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255)),
            Child = new SystemControls.WrapPanel
            {
                Orientation = SystemControls.Orientation.Horizontal,
                Children = { boutonDeplier, boutonReduire, boutonJournal, boutonNotice },
            },
        };
    }

    /*
     * Construit une petite capsule visuelle pour rÃ©sumer les thÃ¨mes abordÃ©s
     * dans la modale d'aide.
     */
    private static SystemControls.Border ConstruireCapsuleAide(string texte)
    {
        return new SystemControls.Border
        {
            Margin = new Thickness(0, 0, 8, 8),
            Padding = new Thickness(10, 4, 10, 4),
            CornerRadius = new CornerRadius(ConstantesDesign.EspaceTresCompact),
            Background = new SolidColorBrush(Color.FromArgb(28, 255, 255, 255)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(38, 255, 255, 255)),
            BorderThickness = ConstantesDesign.EpaisseurContourFin,
            Child = new SystemControls.TextBlock
            {
                FontWeight = FontWeights.SemiBold,
                Opacity = 0.82,
                Text = texte,
            },
        };
    }

    /*
     * Applique rapidement un Ã©tat commun Ã  toutes les sections principales
     * de la modale d'aide.
     */
    private static void DefinirEtatSectionsAide(
        IEnumerable<SystemControls.Expander> sectionsAide,
        bool developpees
    )
    {
        foreach (SystemControls.Expander section in sectionsAide)
        {
            section.IsExpanded = developpees;
        }
    }

    /*
     * Construit une petite grille de synthÃ¨se au sommet de la modale d'aide
     * afin de rÃ©sumer immÃ©diatement l'Ã©tat courant de Compagnon.
     */
    private SystemControls.Grid ConstruireGrilleSyntheseAide(
        double largeurContenuAide,
        bool modaleAideCompacte
    )
    {
        SystemControls.Grid grille = new() { Margin = new Thickness(0, 13, 0, 0) };
        int nombreColonnes =
            modaleAideCompacte ? 1
            : largeurContenuAide
            < (ConstantesDesign.LargeurCarteSecondaire + ConstantesDesign.EspaceTresEtendu)
                ? 2
            : 3;

        for (int indexColonne = 0; indexColonne < nombreColonnes; indexColonne++)
        {
            grille.ColumnDefinitions.Add(
                new SystemControls.ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
            );
        }

        SystemControls.Border carteCompte = ConstruireCarteSyntheseAide(
            "Compte",
            _etatConnexionCourant,
            ConfigurationConnexionEstComplete()
                ? "Le compte local est prÃªt pour les appels Ã  l'API."
                : "La connexion doit Ãªtre configurÃ©e avant toute synchronisation."
        );
        SystemControls.Border carteJeu = ConstruireCarteSyntheseAide(
            "Jeu visible",
            ObtenirTexteSyntheseJeuAide(),
            "Ce rÃ©sumÃ© indique ce que Compagnon voit maintenant ou garde encore en mÃ©moire."
        );
        SystemControls.Border carteInterface = ConstruireCarteSyntheseAide(
            "Interface",
            string.IsNullOrWhiteSpace(_vueModele.EtatSynchronisationJeu)
                ? $"Mode succÃ¨s : {_vueModele.LibelleOrdreSuccesGrille}"
                : _vueModele.EtatSynchronisationJeu,
            string.IsNullOrWhiteSpace(_vueModele.EtatSynchronisationJeu)
                ? "Aucun rafraÃ®chissement visible n'est en cours."
                : "Un rafraÃ®chissement du jeu est en cours."
        );

        SystemControls.Border[] cartes = [carteCompte, carteJeu, carteInterface];

        for (int indexCarte = 0; indexCarte < cartes.Length; indexCarte++)
        {
            int ligne = indexCarte / nombreColonnes;
            int colonne = indexCarte % nombreColonnes;

            while (grille.RowDefinitions.Count <= ligne)
            {
                grille.RowDefinitions.Add(
                    new SystemControls.RowDefinition { Height = GridLength.Auto }
                );
            }

            cartes[indexCarte].Margin = new Thickness(
                0,
                0,
                colonne < nombreColonnes - 1 ? 10 : 0,
                ligne < ((cartes.Length - 1) / nombreColonnes) ? 10 : 0
            );
            SystemControls.Grid.SetRow(cartes[indexCarte], ligne);
            SystemControls.Grid.SetColumn(cartes[indexCarte], colonne);
            grille.Children.Add(cartes[indexCarte]);
        }

        return grille;
    }

    /*
     * Construit une carte compacte de synthÃ¨se rÃ©utilisable pour la modale
     * d'aide.
     */
    private SystemControls.Border ConstruireCarteSyntheseAide(
        string titre,
        string valeur,
        string sousTexte
    )
    {
        return new SystemControls.Border
        {
            Margin = new Thickness(0, 0, 10, 0),
            Padding = new Thickness(10, 9, 10, 9),
            CornerRadius = ObtenirRayonCoins("RayonCoinsPetit", 8),
            Background = new SolidColorBrush(Color.FromArgb(24, 255, 255, 255)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(36, 255, 255, 255)),
            BorderThickness = ConstantesDesign.EpaisseurContourFin,
            Child = new SystemControls.StackPanel
            {
                Children =
                {
                    new SystemControls.TextBlock
                    {
                        FontWeight = FontWeights.SemiBold,
                        Opacity = 0.78,
                        Text = titre,
                        TextWrapping = TextWrapping.Wrap,
                    },
                    new SystemControls.TextBlock
                    {
                        Margin = new Thickness(0, 5, 0, 0),
                        FontSize = 15,
                        FontWeight = FontWeights.SemiBold,
                        Text = valeur,
                        TextWrapping = TextWrapping.Wrap,
                    },
                    new SystemControls.TextBlock
                    {
                        Margin = new Thickness(0, 5, 0, 0),
                        Opacity = 0.66,
                        Text = sousTexte,
                        TextWrapping = TextWrapping.Wrap,
                    },
                },
            },
        };
    }

    /*
     * Retourne un rÃ©sumÃ© court du jeu actuellement visible ou retenu par
     * Compagnon pour le haut de la modale d'aide.
     */
    private string ObtenirTexteSyntheseJeuAide()
    {
        if (!string.IsNullOrWhiteSpace(_titreJeuLocalActif))
        {
            return $"En jeu : {_titreJeuLocalActif}";
        }

        if (
            _dernieresDonneesJeuAffichees is not null
            && !string.IsNullOrWhiteSpace(_dernieresDonneesJeuAffichees.Jeu.Title)
        )
        {
            return $"Dernier affichÃ© : {_dernieresDonneesJeuAffichees.Jeu.Title}";
        }

        if (!string.IsNullOrWhiteSpace(_configurationConnexion.DernierJeuAffiche?.Titre))
        {
            return $"MÃ©moire locale : {_configurationConnexion.DernierJeuAffiche.Titre}";
        }

        return "Aucun jeu visible";
    }

    /*
     * Recherche la notice utilisateur visible pour permettre son ouverture
     * directe depuis la modale d'aide.
     */
    private static string TrouverCheminNoticeAide()
    {
        string[] candidats =
        [
            Path.Combine(AppContext.BaseDirectory, "INSTRUCTION.md"),
            Path.Combine(Directory.GetCurrentDirectory(), "INSTRUCTION.md"),
        ];

        return candidats.FirstOrDefault(File.Exists) ?? candidats[0];
    }

    /*
     * Demande Ã  l'utilisateur de confirmer la dÃ©connexion du compte courant.
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
     * dans un Ã©tat dÃ©connectÃ©.
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

        DefinirEtatConnexion("Non configurÃ©");
        AjusterDisposition();

        await _serviceConfigurationLocale.SauvegarderAsync(_configurationConnexion);
    }

    /*
     * RÃ©cupÃ¨re les donnÃ©es nÃ©cessaires Ã  l'affichage dÃ©taillÃ© du compte
     * dans la modale dÃ©diÃ©e.
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
     * Construit le bloc visuel correspondant Ã  une section de la modale
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
     * Construit l'en-tÃªte du compte avec avatar, pseudo et statut.
     */
    private static SystemControls.Border ConstruireEnTeteAvatarCompte(CompteAffiche compte)
    {
        SystemControls.Border conteneur = new()
        {
            Width = 96,
            Height = 96,
            Padding = new Thickness(8),
            HorizontalAlignment = HorizontalAlignment.Center,
            CornerRadius = new CornerRadius(48),
            Background = new SolidColorBrush(Color.FromArgb(28, 255, 255, 255)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(42, 255, 255, 255)),
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
            Background = new SolidColorBrush(Color.FromArgb(28, 255, 255, 255)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(42, 255, 255, 255)),
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
     * CrÃ©e un sÃ©parateur visuel homogÃ¨ne pour les blocs de la modale de compte.
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
                    Margin = new Thickness(0, 0, 0, 8),
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
            sectionsAide
        );
    }

    /*
     * Construit une section d'aide rabattable rÃ©utilisable dans la modale
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
                    Margin = new Thickness(0, 4, 0, 0),
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
                Padding = new Thickness(0, 10, 0, 2),
                Child = contenu,
            },
        };
        expander.Expanded += (_, _) => AssurerContenuInitialise();
        sectionsAide?.Add(expander);

        if (estOuvertParDefaut)
        {
            AssurerContenuInitialise();
        }

        return new SystemControls.Border
        {
            Padding = new Thickness(13, 11, 13, 11),
            Margin = new Thickness(0, 0, 0, 10),
            CornerRadius = ObtenirRayonCoins("RayonCoinsPetit", 8),
            Background = new SolidColorBrush(Color.FromArgb(26, 255, 255, 255)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(34, 255, 255, 255)),
            BorderThickness = ConstantesDesign.EpaisseurContourFin,
            Child = expander,
        };
    }

    /*
     * Construit la section d'aide consacrÃ©e aux logs, chemins et emplacements
     * des Ã©mulateurs.
     */
    private SystemControls.Border ConstruireBlocAideLogsEmulateurs()
    {
        SystemControls.StackPanel pile = new() { Margin = new Thickness(0) };
        pile.Children.Add(
            new SystemControls.TextBlock
            {
                Margin = new Thickness(0, 0, 0, 10),
                Opacity = 0.78,
                Text =
                    "Cette section montre le fichier, le dossier ou l'exÃ©cutable que Compagnon attend rÃ©ellement pour chaque Ã©mulateur.",
                TextWrapping = TextWrapping.Wrap,
            }
        );
        pile.Children.Add(
            new SystemControls.TextBlock
            {
                Margin = new Thickness(0, 0, 0, 10),
                Opacity = 0.72,
                Text =
                    "Commence par le journal local, puis ouvre la carte de l'Ã©mulateur concernÃ©. Si besoin, dÃ©finis un exÃ©cutable manuel.",
                TextWrapping = TextWrapping.Wrap,
            }
        );
        pile.Children.Add(ConstruireLibelleChampAide("Journal local de Compagnon"));
        pile.Children.Add(
            ConstruireZoneTexteCopiableAide(ServiceSondeLocaleEmulateurs.ObtenirCheminJournal())
        );
        UiControls.Button boutonOuvrirJournal = new()
        {
            Content = "Ouvrir le journal",
            Padding = new Thickness(12, 4, 12, 4),
            Margin = new Thickness(0, 0, 0, 10),
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
                contenu.Children.Add(ConstruireCarteIndicatifLogsEmulateur(definition));
            }

            contenuCharge = true;
        }

        pile.Children.Add(contenu);

        return ConstruireSectionAideRabattable(
            "Logs des Ã©mulateurs",
            pile,
            "Chemins surveillÃ©s, exÃ©cutable visÃ© et actions directes.",
            false,
            ChargerContenu
        );
    }

    /*
     * Construit la carte de diagnostic d'un Ã©mulateur avec ses sources
     * locales et ses chemins utiles.
     */
    private SystemControls.Border ConstruireCarteIndicatifLogsEmulateur(
        DefinitionEmulateurLocal definition
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
            ? "Non trouvÃ© sur ce PC"
            : "DÃ©tectÃ© sur ce PC";
        SystemControls.TextBlock texteStatutEmplacement = new()
        {
            Margin = new Thickness(0, 6, 0, 0),
            FontWeight = FontWeights.SemiBold,
            Opacity = 0.72,
            TextWrapping = TextWrapping.Wrap,
        };
        SystemControls.TextBox texteEmplacement = new()
        {
            Margin = new Thickness(0, 4, 0, 0),
            Style = (Style)FindResource("StyleTexteCopiable"),
            TextWrapping = TextWrapping.Wrap,
        };
        SystemControls.TextBlock texteAideEmplacementManuel = new()
        {
            Margin = new Thickness(0, 6, 0, 0),
            Opacity = 0.66,
            TextWrapping = TextWrapping.Wrap,
        };
        SystemControls.Button boutonChoisirEmplacement = new()
        {
            Margin = new Thickness(0, 8, 8, 0),
            Padding = new Thickness(10, 4, 10, 4),
            MinWidth = 0,
        };
        SystemControls.Button boutonRetirerEmplacement = new()
        {
            Margin = new Thickness(0, 8, 0, 0),
            Padding = new Thickness(10, 4, 10, 4),
            MinWidth = 0,
            Content = "Retirer le choix manuel",
        };
        SystemControls.Button boutonOuvrirEmplacement = new()
        {
            Margin = new Thickness(0, 8, 8, 0),
            Padding = new Thickness(10, 4, 10, 4),
            MinWidth = 0,
            Content = "Ouvrir l'emplacement",
        };
        SystemControls.Button boutonOuvrirSource = new()
        {
            Margin = new Thickness(0, 8, 0, 0),
            Padding = new Thickness(10, 4, 10, 4),
            MinWidth = 0,
            Content = "Ouvrir la source",
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
                !string.IsNullOrWhiteSpace(emplacementManuel) ? "Emplacement manuel dÃ©fini"
                : !string.IsNullOrWhiteSpace(emplacementDetecteMemorise)
                    ? "Emplacement dÃ©tectÃ© et mÃ©morisÃ©"
                : string.IsNullOrWhiteSpace(emplacementDetecte) ? "Emplacement non trouvÃ© sur ce PC"
                : "Emplacement dÃ©tectÃ© sur ce PC";

            texteEmplacement.Text = string.IsNullOrWhiteSpace(emplacementDetecte)
                ? ConstruireCheminIndicatifEmulateur(definition)
                : emplacementDetecte;

            texteAideEmplacementManuel.Text =
                !string.IsNullOrWhiteSpace(emplacementManuel)
                    ? "Ce chemin manuel passe en prioritÃ© si Compagnon hÃ©site entre plusieurs exÃ©cutables."
                : !string.IsNullOrWhiteSpace(emplacementDetecteMemorise)
                    ? "Compagnon a mÃ©morisÃ© cet emplacement aprÃ¨s avoir vu l'Ã©mulateur ouvert sur ce PC."
                : "Si l'exÃ©cutable est renommÃ© ou ambigu, tu peux choisir ici le bon fichier .exe.";

            boutonChoisirEmplacement.Content = !string.IsNullOrWhiteSpace(emplacementManuel)
                ? "Modifier l'emplacement manuel"
                : "Choisir un exÃ©cutable";

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
        }

        SystemControls.StackPanel pile = new()
        {
            Children =
            {
                ConstruireLibelleChampAide("Source locale suivie"),
                ConstruireTexteDetailAide(source, 0.78),
                ConstruireLibelleChampAide("Niveau de confiance"),
                ConstruireTexteDetailAide(
                    ConstruireTexteConfianceDetectionEmulateur(definition),
                    0.72
                ),
                ConstruireTexteDetailAide(
                    ConstruireTexteValidationEmulateur(definition),
                    0.78,
                    FontWeights.SemiBold,
                    string.IsNullOrWhiteSpace(ConstruireTexteValidationEmulateur(definition))
                        ? Visibility.Collapsed
                        : Visibility.Visible
                ),
                ConstruireLibelleChampAide("ExÃ©cutable de l'Ã©mulateur"),
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
                        : Visibility.Visible
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
                ConstruireLibelleChampAide("Fichier ou dossier surveillÃ©"),
                ConstruireTexteDetailAide(statutChemin, 0.72, FontWeights.SemiBold),
                ConstruireZoneTexteCopiableAide(cheminAttendu),
                new SystemControls.WrapPanel
                {
                    Orientation = SystemControls.Orientation.Horizontal,
                    Children = { boutonOuvrirSource },
                },
                ConstruireLibelleChampAide("Ã€ vÃ©rifier dans l'Ã©mulateur"),
                ConstruireTexteDetailAide(ConstruireTexteActivationSourceLocale(definition), 0.66),
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
            false
        );
    }

    /*
     * Construit un libellÃ© de champ homogÃ¨ne pour les cartes de diagnostic
     * affichÃ©es dans la modale d'aide.
     */
    private static SystemControls.TextBlock ConstruireLibelleChampAide(string texte)
    {
        return new SystemControls.TextBlock
        {
            Margin = new Thickness(0, 8, 0, 2),
            FontWeight = FontWeights.SemiBold,
            Opacity = 0.8,
            Text = texte,
            TextWrapping = TextWrapping.Wrap,
        };
    }

    /*
     * Construit un texte de dÃ©tail homogÃ¨ne pour les contenus explicatifs de
     * la modale d'aide.
     */
    private static SystemControls.TextBlock ConstruireTexteDetailAide(
        string texte,
        double opacite,
        FontWeight? graisse = null,
        Visibility visibility = Visibility.Visible
    )
    {
        return new SystemControls.TextBlock
        {
            Margin = new Thickness(0, 0, 0, 0),
            FontWeight = graisse ?? FontWeights.Normal,
            Opacity = opacite,
            Text = texte,
            TextWrapping = TextWrapping.Wrap,
            Visibility = visibility,
        };
    }

    /*
     * Construit une zone de texte copiable homogÃ¨ne pour les chemins et
     * journaux affichÃ©s dans la modale d'aide.
     */
    private SystemControls.TextBox ConstruireZoneTexteCopiableAide(string texte)
    {
        return new SystemControls.TextBox
        {
            Margin = new Thickness(0, 4, 0, 0),
            Style = (Style)FindResource("StyleTexteCopiable"),
            Text = texte,
            TextWrapping = TextWrapping.Wrap,
        };
    }

    /*
     * Ouvre un chemin de diagnostic depuis la modale d'aide en privilÃ©giant le
     * fichier lui-mÃªme lorsqu'il existe, puis son dossier parent.
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
     * RÃ©sume en une ligne la source locale et le niveau de confiance
     * d'un Ã©mulateur.
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
     * Permet de choisir manuellement l'exÃ©cutable d'un Ã©mulateur puis
     * mÃ©morise ce choix.
     */
    private async Task ChoisirEmplacementEmulateurManuelAsync(DefinitionEmulateurLocal definition)
    {
        OpenFileDialog dialogue = new()
        {
            Title = $"Choisir l'exÃ©cutable pour {definition.NomEmulateur}",
            Filter = "ExÃ©cutable Windows (*.exe)|*.exe|Tous les fichiers (*.*)|*.*",
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
     * Retire l'emplacement manuel mÃ©morisÃ© pour un Ã©mulateur.
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
     * Indique si un Ã©mulateur doit apparaÃ®tre dans la section de diagnostic
     * des logs.
     */
    private static bool EstEmulateurValidePourIndicatifLogs(DefinitionEmulateurLocal definition)
    {
        return ServiceCatalogueEmulateursLocaux.EstEmulateurValide(definition);
    }

    /*
     * DÃ©crit la source locale principale utilisÃ©e pour dÃ©tecter le jeu
     * d'un Ã©mulateur.
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
                "Source : flycast.log, avec le chemin du jeu chargÃ© en secours.",
            _ => "Source locale non prÃ©cisÃ©e.",
        };
    }

    /*
     * Fournit les consignes d'activation nÃ©cessaires pour que la source locale
     * soit exploitable.
     */
    private static string ConstruireTexteActivationSourceLocale(DefinitionEmulateurLocal definition)
    {
        return definition.StrategieRenseignementJeu switch
        {
            StrategieRenseignementJeuEmulateurLocal.RetroArchLog =>
                "Dans RetroArch : active `Show Advanced Settings`, puis `Log to File`. DÃ©sactive aussi les journaux horodatÃ©s pour garder un fichier stable `retroarch.log` dans `logs`.",
            StrategieRenseignementJeuEmulateurLocal.BizHawkConfig =>
                "Dans BizHawk : garde `retroachievements-game-log.json` Ã  la racine. Compagnon lit d'abord ce fichier, puis `config.ini` en secours pour retrouver la ROM.",
            StrategieRenseignementJeuEmulateurLocal.DolphinConfig =>
                "Dans Dolphin : ouvre `View -> Show Log Configuration`, coche `Write to File`, garde `RetroAchievements` actif et une verbositÃ© au moins sur `Info`.",
            StrategieRenseignementJeuEmulateurLocal.DuckStationLog =>
                "Dans DuckStation : ouvre `Settings -> Advanced Settings`, rÃ¨gle `Log Level` sur `Debug`, puis active `Log To File`. RedÃ©marre DuckStation si `duckstation.log` n'apparaÃ®t pas.",
            StrategieRenseignementJeuEmulateurLocal.PCSX2Log =>
                "Dans PCSX2 : `emulog.txt` est normalement crÃ©Ã© dans `logs`. S'il n'apparaÃ®t pas, vÃ©rifie les options de console ou de dÃ©bogage de ta version.",
            StrategieRenseignementJeuEmulateurLocal.PPSSPPLog =>
                "Dans PPSSPP : ouvre `Tools -> Developer Tools`, puis active `Enable debug logging`. Si rien n'est Ã©crit sur disque, lance PPSSPP avec une option du type `--log=...`.",
            StrategieRenseignementJeuEmulateurLocal.Project64RACache =>
                "Dans RAP64 : ce n'est pas un journal classique. VÃ©rifie surtout que RetroAchievements est bien actif pour mettre Ã  jour `RACache` et `RALog.txt`.",
            StrategieRenseignementJeuEmulateurLocal.RALibretroRACache =>
                "Dans RALibretro : ce n'est pas un journal classique. VÃ©rifie surtout que RetroAchievements est bien actif pour mettre Ã  jour `RACache` et `RALog.txt`.",
            StrategieRenseignementJeuEmulateurLocal.RANesRACache =>
                "Dans RANes : ce n'est pas un journal classique. VÃ©rifie surtout que RetroAchievements est bien actif pour mettre Ã  jour `RACache` et `RALog.txt`.",
            StrategieRenseignementJeuEmulateurLocal.RAVBARACache =>
                "Dans RAVBA : ce n'est pas un journal classique. VÃ©rifie surtout que RetroAchievements est bien actif pour mettre Ã  jour `RACache` et `RALog.txt`.",
            StrategieRenseignementJeuEmulateurLocal.RASnes9xRACache =>
                "Dans RASnes9x : ce n'est pas un journal classique. VÃ©rifie surtout que RetroAchievements est bien actif pour mettre Ã  jour `RACache` et `RALog.txt`.",
            StrategieRenseignementJeuEmulateurLocal.FlycastConfig =>
                "Dans Flycast : active l'Ã©criture de `flycast.log` Ã  la racine. Compagnon s'appuie d'abord sur ce journal, puis sur le chemin du jeu lancÃ© si besoin.",
            _ => string.Empty,
        };
    }

    /*
     * DÃ©crit le niveau de confiance de la dÃ©tection locale pour un Ã©mulateur.
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
                "Confiance : excellente. Compagnon croise le processus Ã©mulateur avec RACache et RALog.txt.",
            StrategieRenseignementJeuEmulateurLocal.RALibretroRACache =>
                "Confiance : excellente. Compagnon croise le processus Ã©mulateur avec RACache et RALog.txt.",
            StrategieRenseignementJeuEmulateurLocal.RANesRACache =>
                "Confiance : excellente. Compagnon croise le processus Ã©mulateur avec RACache et RALog.txt.",
            StrategieRenseignementJeuEmulateurLocal.RAVBARACache =>
                "Confiance : excellente. Compagnon croise le processus Ã©mulateur avec RACache et RALog.txt.",
            StrategieRenseignementJeuEmulateurLocal.RASnes9xRACache =>
                "Confiance : excellente. Compagnon croise le processus Ã©mulateur avec RACache et RALog.txt.",
            StrategieRenseignementJeuEmulateurLocal.FlycastConfig =>
                "Confiance : bonne. Compagnon s'appuie sur le processus Flycast, sur flycast.log et sur le disque lancÃ© en secours.",
            StrategieRenseignementJeuEmulateurLocal.RetroArchLog =>
                "Confiance : bonne. Compagnon s'appuie sur le processus et sur les journaux locaux de RetroArch.",
            StrategieRenseignementJeuEmulateurLocal.DuckStationLog =>
                "Confiance : bonne. Compagnon s'appuie sur le processus et sur duckstation.log.",
            StrategieRenseignementJeuEmulateurLocal.PCSX2Log =>
                "Confiance : bonne. Compagnon s'appuie sur le processus et sur emulog.txt.",
            StrategieRenseignementJeuEmulateurLocal.PPSSPPLog =>
                "Confiance : bonne. Compagnon s'appuie sur le processus et sur les journaux locaux de PPSSPP.",
            _ => "Confiance : fragile. Une vÃ©rification manuelle peut Ãªtre nÃ©cessaire.",
        };
    }

    /*
     * RÃ©sume le dossier de profil gÃ©nÃ©ralement utilisÃ© par la version
     * installÃ©e d'un Ã©mulateur lorsqu'il n'Ã©crit pas ses donnÃ©es Ã  cÃ´tÃ©
     * de l'exÃ©cutable.
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
                $"Version installÃ©e : DuckStation Ã©crit souvent son profil dans `{Path.Combine(documents, "DuckStation")}`, `{Path.Combine(localAppData, "DuckStation")}` ou `{Path.Combine(appData, "DuckStation")}`.",
            StrategieRenseignementJeuEmulateurLocal.PCSX2Log =>
                $"Version installÃ©e : PCSX2 Ã©crit souvent ses journaux dans `{Path.Combine(documents, "PCSX2", "logs")}`, `{Path.Combine(localAppData, "PCSX2", "logs")}` ou `{Path.Combine(appData, "PCSX2", "logs")}`.",
            StrategieRenseignementJeuEmulateurLocal.PPSSPPLog =>
                $"Version installÃ©e : PPSSPP Ã©crit souvent son profil dans `{Path.Combine(localAppData, "PPSSPP")}` ou `{Path.Combine(appData, "PPSSPP")}`.",
            StrategieRenseignementJeuEmulateurLocal.RetroArchLog =>
                $"Version installÃ©e : RetroArch peut Ã©crire ses journaux dans le dossier `logs` proche de l'exÃ©cutable ou dans un profil local comme `{Path.Combine(appData, "RetroArch", "logs")}`.",
            _ => string.Empty,
        };
    }

    /*
     * Retourne le libellÃ© de validation affichÃ© pour les Ã©mulateurs
     * dÃ©jÃ  testÃ©s.
     */
    private static string ConstruireTexteValidationEmulateur(DefinitionEmulateurLocal definition)
    {
        return definition.StrategieRenseignementJeu switch
        {
            StrategieRenseignementJeuEmulateurLocal.RetroArchLog => "ValidÃ© et testÃ©.",
            StrategieRenseignementJeuEmulateurLocal.DuckStationLog => "ValidÃ© et testÃ©.",
            _ => string.Empty,
        };
    }

    /*
     * Retourne le dossier indicatif oÃ¹ l'utilisateur retrouvera
     * gÃ©nÃ©ralement l'Ã©mulateur.
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
            _ => "Emplacement local non dÃ©fini.",
        };
    }

    /*
     * Retourne le chemin indicatif de la source locale attendue
     * pour un Ã©mulateur.
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
            _ => "Chemin local non dÃ©fini.",
        };
    }

    /*
     * Construit la liste visuelle des jeux rÃ©cemment jouÃ©s affichÃ©e
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
                    Text = "Jeux rÃ©cemment jouÃ©s",
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
                    Background = new SolidColorBrush(Color.FromArgb(24, 255, 255, 255)),
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
     * Retourne l'URL du dÃ©pÃ´t GitHub du projet.
     */
    private static string ConstruireUrlDepotGitHub()
    {
        return "https://github.com/fredg20/RA-Companion";
    }

    /*
     * Ouvre le profil RetroAchievements de l'utilisateur dans le navigateur
     * par dÃ©faut.
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
     * Ouvre le dÃ©pÃ´t GitHub du projet dans le navigateur par dÃ©faut.
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
     * GÃ¨re le clic sur le bouton ouvrant le dÃ©pÃ´t GitHub du projet.
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
     * Ajuste le pied de la modale de connexion une fois son arbre visuel chargÃ©.
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
     * Ajuste le pied de la modale de compte une fois son arbre visuel chargÃ©.
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
     * Ajuste le pied de la modale de confirmation aprÃ¨s son chargement visuel.
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
     * Centre et Ã©pure les boutons affichÃ©s dans le pied de la modale
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
     * Ouvre la modale de connexion depuis le bouton d'entÃªte.
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
     * Affiche la connexion ou le compte selon l'Ã©tat actuel
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
     * visible de Compagnon, puis mÃ©morise immÃ©diatement cet Ã©tat.
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
     * Applique ou retire le halo dorÃ© du bouton Aide afin de guider la
     * premiÃ¨re dÃ©couverte de l'application.
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

        BoutonAide.BorderBrush = new SolidColorBrush(Color.FromRgb(245, 200, 76));
        BoutonAide.BorderThickness = new Thickness(ConstantesDesign.EpaisseurContourAccent);
        BoutonAide.Effect = new DropShadowEffect
        {
            Color = Color.FromRgb(245, 200, 76),
            BlurRadius = ConstantesDesign.FlouHaloHardcore,
            ShadowDepth = 0,
            Opacity = ConstantesDesign.OpaciteHaloHardcore,
        };
    }

    /*
     * Met Ã  jour le rÃ©sumÃ© de connexion visible dans l'entÃªte et le ViewModel.
     */
    private void MettreAJourResumeConnexion()
    {
        if (string.IsNullOrWhiteSpace(_configurationConnexion.Pseudo))
        {
            _etatConnexionCourant = "Non configurÃ©";
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
     * Retourne le libellÃ© Ã  afficher sur le bouton de compte.
     */
    private string ObtenirLibelleBoutonCompte()
    {
        return string.IsNullOrWhiteSpace(_configurationConnexion.Pseudo)
            ? "Connexion"
            : _configurationConnexion.Pseudo;
    }

    /*
     * Met Ã  jour la notice d'Ã©tat du compte et dÃ©clenche une synchronisation
     * ciblÃ©e si l'Ã©tat visible change.
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
                    : "En jeu (dÃ©tection locale)";
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
     * RÃ©initialise l'historique interne du dernier Ã©tat de jeu visible
     * dans l'entÃªte.
     */
    private void ReinitialiserSuiviEtatJeuVisible()
    {
        _suiviEtatJeuVisibleInitialise = false;
        _signatureDernierEtatJeuVisible = string.Empty;
        _horodatageDerniereSynchronisationEtatJeuUtc = DateTimeOffset.MinValue;
    }

    /*
     * Enregistre l'Ã©tat de jeu visible et demande une synchronisation
     * ciblÃ©e si sa valeur change.
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
     * DÃ©clenche ou diffÃ¨re une synchronisation ciblÃ©e de l'Ã©tat du jeu
     * en respectant le debounce dÃ©fini.
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
     * Recharge le jeu courant aprÃ¨s un changement d'Ã©tat dÃ©tectÃ©
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
     * DÃ©termine le Game ID le plus pertinent Ã  afficher dans la notice
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
     * Retourne les couleurs associÃ©es au statut visible dans la notice
     * de compte.
     */
    private static (Brush Fond, Brush Bordure) ObtenirCouleursNoticeCompteEntete(string statut)
    {
        if (statut.Contains("En jeu", StringComparison.OrdinalIgnoreCase))
        {
            return (Brushes.Transparent, new SolidColorBrush(Color.FromArgb(96, 58, 188, 116)));
        }

        if (statut.Contains("Actif", StringComparison.OrdinalIgnoreCase))
        {
            return (Brushes.Transparent, new SolidColorBrush(Color.FromArgb(56, 120, 200, 255)));
        }

        if (statut.Contains("Inactif", StringComparison.OrdinalIgnoreCase))
        {
            return (Brushes.Transparent, new SolidColorBrush(Color.FromArgb(56, 160, 160, 160)));
        }

        return (Brushes.Transparent, new SolidColorBrush(Color.FromArgb(56, 120, 200, 255)));
    }

    /*
     * Journalise les changements utiles de la notice de compte sans
     * dupliquer les mÃªmes entrÃ©es.
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
     * Met Ã  jour l'Ã©tat de connexion courant et le rÃ©percute dans le ViewModel.
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
     * Retourne le texte nettoyÃ© portÃ© par un bouton WPF.
     */
    private static string TexteBouton(SystemControls.Button bouton)
    {
        return bouton.Content?.ToString()?.Trim() ?? string.Empty;
    }

    /*
     * Recherche le premier panneau ancÃªtre commun Ã  deux Ã©lÃ©ments visuels.
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
     * Parcourt rÃ©cursivement l'arbre visuel pour retourner tous les descendants
     * du type demandÃ©.
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
}
