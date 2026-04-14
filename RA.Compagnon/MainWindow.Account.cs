/*
 * Regroupe la logique de connexion, de compte et d'aide utilisateur, ainsi
 * que les modales associées et les outils de diagnostic reliés aux émulateurs
 * et à l'état visible du compte dans l'interface principale.
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
 * Porte la partie de la fenêtre principale qui gère la connexion utilisateur,
 * la modale Compte, la modale Aide et la notice d'état visible dans l'entête.
 */
public partial class MainWindow
{
    /*
     * Mémorise si la modale d'aide courante est affichée dans une largeur
     * compacte afin d'harmoniser ses sous-sections, même lorsqu'elles sont
     * construites depuis d'autres fichiers partiels.
     */
    private bool _modaleAideCompacteCourante;

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

        return Math.Clamp(largeurDisponible, largeurMinimale, largeurCible);
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
     * Calcule une largeur souple pour la colonne des libellés dans la modale
     * de compte afin de conserver une lecture stable sans imposer une valeur
     * fixe quand la fenêtre rétrécit.
     */
    private static double CalculerLargeurColonneLibellesCompte(double largeurContenuDisponible)
    {
        double largeurProportionnelle = Math.Round(
            largeurContenuDisponible / (ConstantesDesign.NombreOr + 1d),
            0
        );

        return Math.Clamp(
            largeurProportionnelle,
            ConstantesDesign.EspaceHeroique,
            ConstantesDesign.EspaceVisuelLarge
        );
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
        bool modaleCompteCompacte = EstModaleCompacte(
            largeurContenuCompte,
            ConstantesDesign.LargeurCarteSecondaire - ConstantesDesign.EspaceVisuelLarge
        );
        double largeurColonneLibellesCompte = CalculerLargeurColonneLibellesCompte(
            largeurContenuCompte
        );
        SystemControls.StackPanel contenu = new()
        {
            Width = largeurContenuCompte,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = ConstantesDesign.AucuneMarge,
        };
        contenu.Children.Add(ConstruireEnTeteAvatarCompte(compte, modaleCompteCompacte));
        contenu.Children.Add(ConstruireCartePresentationCompte(compte, modaleCompteCompacte));
        for (int indexSection = 0; indexSection < compte.Sections.Count; indexSection++)
        {
            if (indexSection > 0)
            {
                contenu.Children.Add(ConstruireSeparateurBlocCompte());
            }

            contenu.Children.Add(
                ConstruireBlocCompte(
                    compte.Sections[indexSection],
                    modaleCompteCompacte,
                    largeurColonneLibellesCompte
                )
            );
        }

        if (compte.JeuxRecemmentJoues.Count > 0)
        {
            contenu.Children.Add(ConstruireSeparateurBlocCompte());
            contenu.Children.Add(
                ConstruireBlocJeuxRecemmentJoues(compte.JeuxRecemmentJoues, modaleCompteCompacte)
            );
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
            ConstantesDesign.LargeurCarteSecondaire,
            ConstantesDesign.EspaceFenetreStandard
        );
        bool modaleAideCompacte = EstModaleCompacte(
            largeurContenuAide,
            ConstantesDesign.LargeurCarteSecondaire + ConstantesDesign.EspaceHeroique
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
                ConstruireBlocAide(
                    "Démarrage rapide",
                    [
                        "Ouvre Profil puis connecte ton compte RetroAchievements.",
                        "Lance un jeu dans un émulateur compatible et attends quelques secondes.",
                        "Si le jeu affiché n'est pas le bon, utilise Recharger avant de redémarrer l'émulateur.",
                    ],
                    "Les trois premières étapes à suivre.",
                    true,
                    sectionsAide,
                    modaleAideCompacte
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
                    sectionsAide,
                    modaleAideCompacte
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
                    sectionsAide,
                    modaleAideCompacte
                ),
                ConstruireBlocAideMiseAJourApplication(sectionsAide),
                ConstruireBlocAideLogsEmulateurs(sectionsAide, modaleAideCompacte),
            },
        };

        SystemControls.Border conteneurContenu = new()
        {
            Padding = modaleAideCompacte
                ? new Thickness(10, 10, 10, 10)
                : ConstantesDesign.PaddingCarteSecondaire,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            CornerRadius = ObtenirRayonCoins("RayonCoinsStandard", ConstantesDesign.EspaceStandard),
            Child = contenu,
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
        dialogueAide.Loaded += DialogueAide_Chargement;

        try
        {
            DefinirEtatModalesActif(true);
            await dialogueAide.ShowAsync();
        }
        finally
        {
            DefinirEtatModalesActif(false);
            dialogueAide.Loaded -= DialogueAide_Chargement;
        }
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
    private SystemControls.Border ConstruireBlocCompte(
        SectionInformationsAffichee section,
        bool dispositionCompacte = false,
        double largeurColonneLibelle = 0
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
                    FontSize = dispositionCompacte
                        ? ConstantesDesign.TaillePoliceInterfaceBase
                        : 16,
                    FontWeight = FontWeights.SemiBold,
                    Text = section.Titre,
                },
            },
        };

        if (dispositionCompacte)
        {
            SystemControls.StackPanel lignesCompactes = new()
            {
                Margin = new Thickness(0, 2, 0, 0),
            };

            for (int index = 0; index < section.Lignes.Count; index++)
            {
                lignesCompactes.Children.Add(
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
                                    Text = section.Lignes[index].Libelle,
                                    TextWrapping = TextWrapping.Wrap,
                                },
                                new SystemControls.TextBlock
                                {
                                    Margin = new Thickness(0, 4, 0, 0),
                                    Opacity = 0.84,
                                    Text = section.Lignes[index].Valeur,
                                    TextWrapping = TextWrapping.Wrap,
                                },
                            },
                        },
                    }
                );
            }

            pile.Children.Add(lignesCompactes);
            return new SystemControls.Border
            {
                Padding = new Thickness(10, 9, 10, 9),
                CornerRadius = ObtenirRayonCoins("RayonCoinsPetit", 8),
                Background = new SolidColorBrush(Color.FromArgb(18, 255, 255, 255)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(28, 255, 255, 255)),
                BorderThickness = ConstantesDesign.EpaisseurContourFin,
                Child = pile,
            };
        }

        SystemControls.Grid grille = new();
        double largeurLibelle =
            largeurColonneLibelle > 0 ? largeurColonneLibelle : ConstantesDesign.EspaceVisuelLarge;
        grille.ColumnDefinitions.Add(
            new SystemControls.ColumnDefinition { Width = new GridLength(largeurLibelle) }
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

        return new SystemControls.Border
        {
            Padding = new Thickness(12, 10, 12, 10),
            CornerRadius = ObtenirRayonCoins("RayonCoinsPetit", 8),
            Background = new SolidColorBrush(Color.FromArgb(18, 255, 255, 255)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(28, 255, 255, 255)),
            BorderThickness = ConstantesDesign.EpaisseurContourFin,
            Child = pile,
        };
    }

    /*
     * Construit l'en-tête du compte avec avatar, pseudo et statut.
     */
    private static SystemControls.Border ConstruireEnTeteAvatarCompte(
        CompteAffiche compte,
        bool dispositionCompacte = false
    )
    {
        double tailleConteneur = dispositionCompacte ? ConstantesDesign.EspaceHeroique : 96;
        double tailleImage = dispositionCompacte ? ConstantesDesign.EspaceMajeur : 80;
        CornerRadius rayon = new(tailleConteneur / 2);

        SystemControls.Border conteneur = new()
        {
            Width = tailleConteneur,
            Height = tailleConteneur,
            Padding = new Thickness(8),
            HorizontalAlignment = HorizontalAlignment.Center,
            CornerRadius = rayon,
            Background = new SolidColorBrush(Color.FromArgb(28, 255, 255, 255)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(42, 255, 255, 255)),
            BorderThickness = new Thickness(1),
        };

        SystemControls.Image? imageAvatar = ConstruireImageAvatarCompte(
            compte.UrlAvatar,
            tailleImage,
            tailleImage,
            new Thickness(0)
        );

        if (imageAvatar is not null)
        {
            conteneur.Child = imageAvatar;
            return conteneur;
        }

        return new SystemControls.Border
        {
            Width = tailleConteneur,
            Height = tailleConteneur,
            HorizontalAlignment = HorizontalAlignment.Center,
            CornerRadius = rayon,
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
     * Construit la carte de présentation affichée sous l'avatar dans la
     * modale de compte afin de mieux regrouper le titre, l'introduction et
     * l'accès direct au profil public.
     */
    private SystemControls.Border ConstruireCartePresentationCompte(
        CompteAffiche compte,
        bool dispositionCompacte
    )
    {
        SystemControls.StackPanel pile = new() { Margin = new Thickness(0, 12, 0, 12) };

        if (!string.IsNullOrWhiteSpace(compte.NomUtilisateur))
        {
            pile.Children.Add(
                new SystemControls.Border
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Padding = new Thickness(9, 4, 9, 4),
                    CornerRadius = ObtenirRayonCoins("RayonCoinsPetit", 8),
                    Background = new SolidColorBrush(Color.FromArgb(26, 255, 255, 255)),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(36, 255, 255, 255)),
                    BorderThickness = ConstantesDesign.EpaisseurContourFin,
                    Child = new SystemControls.TextBlock
                    {
                        Opacity = 0.84,
                        FontWeight = FontWeights.SemiBold,
                        Text = $"@{compte.NomUtilisateur}",
                    },
                }
            );
        }

        pile.Children.Add(
            new SystemControls.TextBlock
            {
                Margin = new Thickness(0, 10, 0, 0),
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
            pile.Children.Add(
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

        if (!string.IsNullOrWhiteSpace(compte.Introduction))
        {
            pile.Children.Add(
                new SystemControls.TextBlock
                {
                    Margin = new Thickness(0, 12, 0, 0),
                    Opacity = 0.82,
                    Text = compte.Introduction,
                    TextAlignment = TextAlignment.Center,
                    TextWrapping = TextWrapping.Wrap,
                }
            );
        }

        if (!string.IsNullOrWhiteSpace(compte.NomUtilisateur))
        {
            UiControls.Button boutonProfilRetroAchievements = new()
            {
                Content = "Ouvrir le profil RetroAchievements",
                Appearance = Wpf.Ui.Controls.ControlAppearance.Secondary,
                HorizontalAlignment = HorizontalAlignment.Center,
                Padding = dispositionCompacte
                    ? ConstantesDesign.PaddingBoutonActionCompact
                    : ConstantesDesign.PaddingBoutonAction,
                Margin = new Thickness(0, 12, 0, 0),
            };
            boutonProfilRetroAchievements.Click += (_, _) =>
                OuvrirProfilRetroAchievements(compte.NomUtilisateur);
            pile.Children.Add(boutonProfilRetroAchievements);
        }

        return new SystemControls.Border
        {
            Padding = dispositionCompacte
                ? new Thickness(10, 10, 10, 10)
                : new Thickness(13, 13, 13, 13),
            CornerRadius = ObtenirRayonCoins("RayonCoinsStandard", ConstantesDesign.EspaceStandard),
            Background = new SolidColorBrush(Color.FromArgb(24, 255, 255, 255)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(34, 255, 255, 255)),
            BorderThickness = ConstantesDesign.EpaisseurContourFin,
            Child = pile,
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
     * Construit une ligne d'étape homogène pour les sections d'aide afin de
     * mieux guider la lecture sur format large comme sur format compact.
     */
    private static SystemControls.Border ConstruireLigneEtapeAide(
        string texte,
        int index,
        bool dispositionCompacte
    )
    {
        double taillePastille = dispositionCompacte ? 18 : 22;
        SystemControls.Grid grille = new();
        grille.ColumnDefinitions.Add(
            new SystemControls.ColumnDefinition { Width = new GridLength(taillePastille) }
        );
        grille.ColumnDefinitions.Add(
            new SystemControls.ColumnDefinition { Width = new GridLength(8) }
        );
        grille.ColumnDefinitions.Add(
            new SystemControls.ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
        );

        SystemControls.Border pastille = new()
        {
            Width = taillePastille,
            Height = taillePastille,
            CornerRadius = new CornerRadius(taillePastille / 2),
            Background = new SolidColorBrush(Color.FromArgb(36, 255, 255, 255)),
            Child = new SystemControls.TextBlock
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = FontWeights.SemiBold,
                FontSize = dispositionCompacte
                    ? ConstantesDesign.TaillePoliceMicro
                    : ConstantesDesign.TaillePoliceSecondaire,
                Text = (index + 1).ToString(CultureInfo.InvariantCulture),
            },
        };
        SystemControls.Grid.SetColumn(pastille, 0);
        grille.Children.Add(pastille);

        SystemControls.TextBlock texteLigne = new()
        {
            Opacity = 0.86,
            Text = texte,
            TextWrapping = TextWrapping.Wrap,
        };
        SystemControls.Grid.SetColumn(texteLigne, 2);
        grille.Children.Add(texteLigne);

        return new SystemControls.Border
        {
            Margin = new Thickness(0, 0, 0, 8),
            Padding = dispositionCompacte ? new Thickness(7, 6, 7, 6) : new Thickness(10, 8, 10, 8),
            CornerRadius = ConstantesDesign.RayonCoinsPetit,
            Background = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255)),
            Child = grille,
        };
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
        IList<SystemControls.Expander>? sectionsAide = null,
        bool dispositionCompacte = false
    )
    {
        SystemControls.StackPanel pile = new() { Margin = new Thickness(0) };

        for (int index = 0; index < lignes.Count; index++)
        {
            pile.Children.Add(ConstruireLigneEtapeAide(lignes[index], index, dispositionCompacte));
        }

        return ConstruireSectionAideRabattable(
            titre,
            pile,
            resume,
            estOuvertParDefaut,
            null,
            sectionsAide,
            dispositionCompacte
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
                    FontSize = dispositionCompacte ? 16 : 17,
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
                Padding = dispositionCompacte
                    ? new Thickness(0, 6, 0, 0)
                    : new Thickness(0, 10, 0, 2),
                Child = contenu,
            },
        };

        if (contenu is FrameworkElement elementMesurable)
        {
            elementMesurable.SizeChanged += (_, e) =>
            {
                if (!expander.IsExpanded)
                {
                    return;
                }

                if (
                    Math.Abs(e.NewSize.Height - e.PreviousSize.Height) < 0.5
                    && Math.Abs(e.NewSize.Width - e.PreviousSize.Width) < 0.5
                )
                {
                    return;
                }

                PlanifierActualisationDispositionSectionAide(expander);
            };
        }

        expander.Expanded += (_, _) =>
        {
            AssurerContenuInitialise();
            RefermerAutresSectionsAide(expander, sectionsAide);
            PlanifierActualisationDispositionSectionAide(expander);
        };
        expander.Collapsed += (_, _) => PlanifierActualisationDispositionSectionAide(expander);
        sectionsAide?.Add(expander);

        if (estOuvertParDefaut)
        {
            AssurerContenuInitialise();
        }

        return new SystemControls.Border
        {
            Padding = dispositionCompacte
                ? new Thickness(8, 8, 8, 8)
                : new Thickness(13, 11, 13, 11),
            Margin = new Thickness(0, 0, 0, 10),
            CornerRadius = ObtenirRayonCoins("RayonCoinsPetit", 8),
            Background = new SolidColorBrush(Color.FromArgb(26, 255, 255, 255)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(34, 255, 255, 255)),
            BorderThickness = ConstantesDesign.EpaisseurContourFin,
            Child = expander,
        };
    }

    /*
     * Referme les autres sections de la modale d'aide lorsqu'une nouvelle
     * section vient d'être ouverte afin de conserver un comportement accordéon.
     */
    private static void RefermerAutresSectionsAide(
        SystemControls.Expander sectionActive,
        IEnumerable<SystemControls.Expander>? sectionsAide
    )
    {
        if (sectionsAide is null)
        {
            return;
        }

        foreach (SystemControls.Expander section in sectionsAide)
        {
            if (ReferenceEquals(section, sectionActive) || !section.IsExpanded)
            {
                continue;
            }

            section.IsExpanded = false;
        }
    }

    /*
     * Planifie une remise en page après ouverture ou fermeture d'une section
     * d'aide afin que le défilement de la modale suive bien la nouvelle hauteur.
     */
    private void PlanifierActualisationDispositionSectionAide(SystemControls.Expander expander)
    {
        _ = expander.Dispatcher.BeginInvoke(
            () => ActualiserDispositionSectionAide(expander),
            DispatcherPriority.Render
        );
    }

    /*
     * Force la remesure de la section d'aide, de la modale et du défilement
     * interne porté par le ContentDialog.
     */
    private void ActualiserDispositionSectionAide(SystemControls.Expander expander)
    {
        expander.InvalidateMeasure();
        expander.InvalidateArrange();
        expander.UpdateLayout();

        if (expander.Content is UIElement contenuExpander)
        {
            contenuExpander.InvalidateMeasure();
            contenuExpander.InvalidateArrange();
        }

        UiControls.ContentDialog? dialogue = TrouverPremierAncetre<UiControls.ContentDialog>(
            expander
        );
        dialogue?.InvalidateMeasure();
        dialogue?.InvalidateArrange();
        dialogue?.UpdateLayout();

        if (dialogue is not null)
        {
            AjusterDefilementDialogueAide(dialogue);
        }
    }

    /*
     * Ajuste l'unique ScrollViewer interne de la modale d'aide pour lui laisser
     * une hauteur automatique tout en bornant sa hauteur maximale disponible.
     */
    private void AjusterDefilementDialogueAide(UiControls.ContentDialog dialogueAide)
    {
        SystemControls.ScrollViewer? defileurAide = TrouverDescendants<SystemControls.ScrollViewer>(
                dialogueAide
            )
            .FirstOrDefault();

        if (defileurAide is null)
        {
            return;
        }

        double offsetCourant = defileurAide.VerticalOffset;

        defileurAide.Height = double.NaN;
        defileurAide.MaxHeight = CalculerHauteurMaximaleContenuModale();
        defileurAide.VerticalScrollBarVisibility = SystemControls.ScrollBarVisibility.Auto;
        defileurAide.HorizontalScrollBarVisibility = SystemControls.ScrollBarVisibility.Disabled;
        defileurAide.InvalidateMeasure();
        defileurAide.InvalidateArrange();
        defileurAide.UpdateLayout();
        defileurAide.ScrollToVerticalOffset(offsetCourant);
    }

    /*
     * Construit la section d'aide consacrée aux logs, chemins et emplacements
     * des émulateurs.
     */
    private SystemControls.Border ConstruireBlocAideLogsEmulateurs(
        IList<SystemControls.Expander>? sectionsAide = null,
        bool dispositionCompacte = false
    )
    {
        SystemControls.StackPanel pile = new() { Margin = new Thickness(0) };
        pile.Children.Add(
            new SystemControls.TextBlock
            {
                Margin = new Thickness(0, 0, 0, dispositionCompacte ? 8 : 10),
                Opacity = 0.78,
                Text =
                    "Cette section montre le fichier, le dossier ou l'exécutable que Compagnon attend réellement pour chaque émulateur.",
                TextWrapping = TextWrapping.Wrap,
            }
        );
        pile.Children.Add(
            new SystemControls.TextBlock
            {
                Margin = new Thickness(0, 0, 0, dispositionCompacte ? 8 : 10),
                Opacity = 0.72,
                Text =
                    "Commence par le journal local, puis ouvre la carte de l'émulateur concerné. Si besoin, définis un exécutable manuel.",
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
            Padding = dispositionCompacte
                ? ConstantesDesign.PaddingBoutonActionCompact
                : new Thickness(12, 4, 12, 4),
            Margin = new Thickness(0, 0, 0, dispositionCompacte ? 8 : 10),
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
        List<SystemControls.Expander> sectionsLogsEmulateurs = [];
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
                        sectionsLogsEmulateurs,
                        dispositionCompacte
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
            dispositionCompacte
        );
    }

    /*
     * Construit une petite carte d'information réutilisable pour mieux
     * structurer les contenus de diagnostic dans la modale d'aide.
     */
    private SystemControls.Border ConstruireCarteBlocDiagnosticAide(
        string titre,
        UIElement contenu,
        bool dispositionCompacte,
        string? sousTexte = null
    )
    {
        SystemControls.StackPanel pile = new()
        {
            Children =
            {
                new SystemControls.TextBlock
                {
                    FontWeight = FontWeights.SemiBold,
                    Opacity = 0.82,
                    Text = titre,
                    TextWrapping = TextWrapping.Wrap,
                },
            },
        };

        if (!string.IsNullOrWhiteSpace(sousTexte))
        {
            pile.Children.Add(
                new SystemControls.TextBlock
                {
                    Margin = new Thickness(0, 4, 0, 0),
                    Opacity = 0.68,
                    Text = sousTexte,
                    TextWrapping = TextWrapping.Wrap,
                }
            );
        }

        pile.Children.Add(
            new SystemControls.Border { Margin = new Thickness(0, 8, 0, 0), Child = contenu }
        );

        return new SystemControls.Border
        {
            Margin = new Thickness(0, 0, 0, 8),
            Padding = dispositionCompacte ? new Thickness(9, 8, 9, 8) : new Thickness(10, 9, 10, 9),
            CornerRadius = ObtenirRayonCoins("RayonCoinsPetit", 8),
            Background = new SolidColorBrush(Color.FromArgb(22, 255, 255, 255)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)),
            BorderThickness = ConstantesDesign.EpaisseurContourFin,
            Child = pile,
        };
    }

    /*
     * Construit une petite grille de synthèse à une ou deux colonnes pour les
     * cartes de diagnostic de la modale d'aide.
     */
    private static SystemControls.Grid ConstruireGrilleSyntheseDiagnosticAide(
        bool dispositionCompacte,
        params SystemControls.Border[] cartes
    )
    {
        int nombreColonnes = dispositionCompacte ? 1 : 2;
        SystemControls.Grid grille = new() { Margin = new Thickness(0, 0, 0, 8) };

        for (int indexColonne = 0; indexColonne < nombreColonnes; indexColonne++)
        {
            grille.ColumnDefinitions.Add(
                new SystemControls.ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
            );
        }

        for (int index = 0; index < cartes.Length; index++)
        {
            while (grille.RowDefinitions.Count <= (index / nombreColonnes))
            {
                grille.RowDefinitions.Add(
                    new SystemControls.RowDefinition { Height = GridLength.Auto }
                );
            }

            cartes[index].Margin = new Thickness(
                0,
                0,
                index % nombreColonnes < nombreColonnes - 1 ? 8 : 0,
                8
            );
            SystemControls.Grid.SetRow(cartes[index], index / nombreColonnes);
            SystemControls.Grid.SetColumn(cartes[index], index % nombreColonnes);
            grille.Children.Add(cartes[index]);
        }

        return grille;
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
            Margin = new Thickness(0, 6, 0, 0),
            FontWeight = FontWeights.SemiBold,
            Opacity = 0.72,
            TextWrapping = TextWrapping.Wrap,
        };
        SystemControls.TextBlock texteEmplacement = new() { TextWrapping = TextWrapping.Wrap };
        SystemControls.Border zoneTexteEmplacement = ConstruireZoneTexteCopiableAide(
            texteEmplacement
        );
        SystemControls.TextBlock texteAideEmplacementManuel = new()
        {
            Margin = new Thickness(0, 6, 0, 0),
            Opacity = 0.66,
            TextWrapping = TextWrapping.Wrap,
        };
        SystemControls.Button boutonChoisirEmplacement = new()
        {
            Margin = new Thickness(0, 8, 8, 0),
            Padding = dispositionCompacte
                ? ConstantesDesign.PaddingBoutonActionCompact
                : new Thickness(10, 4, 10, 4),
            MinWidth = 0,
        };
        SystemControls.Button boutonRetirerEmplacement = new()
        {
            Margin = new Thickness(0, 8, 0, 0),
            Padding = dispositionCompacte
                ? ConstantesDesign.PaddingBoutonActionCompact
                : new Thickness(10, 4, 10, 4),
            MinWidth = 0,
            Content = "Retirer le choix manuel",
        };
        SystemControls.Button boutonOuvrirEmplacement = new()
        {
            Margin = new Thickness(0, 8, 8, 0),
            Padding = dispositionCompacte
                ? ConstantesDesign.PaddingBoutonActionCompact
                : new Thickness(10, 4, 10, 4),
            MinWidth = 0,
            Content = "Ouvrir l'emplacement",
        };
        SystemControls.Button boutonOuvrirSource = new()
        {
            Margin = new Thickness(0, 8, 0, 0),
            Padding = dispositionCompacte
                ? ConstantesDesign.PaddingBoutonActionCompact
                : new Thickness(10, 4, 10, 4),
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
        }

        SystemControls.StackPanel pile = new()
        {
            Margin = new Thickness(0, dispositionCompacte ? 1 : 2, 0, 0),
            Children =
            {
                ConstruireLibelleChampAide("Exécutable de l'émulateur"),
                texteStatutEmplacement,
                zoneTexteEmplacement,
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
                ConstruireLibelleChampAide("Fichier ou dossier surveillé"),
                ConstruireTexteDetailAide(statutChemin, 0.72, FontWeights.SemiBold),
                ConstruireZoneTexteCopiableAide(cheminAttendu),
                new SystemControls.WrapPanel
                {
                    Orientation = SystemControls.Orientation.Horizontal,
                    Children = { boutonOuvrirSource },
                },
                ConstruireLibelleChampAide("À vérifier dans l'émulateur"),
                ConstruireTexteDetailAide(ConstruireTexteActivationSourceLocale(definition), 0.66),
            },
        };

        SystemControls.StackPanel pileSyntheseSource = new();
        pileSyntheseSource.Children.Add(ConstruireTexteDetailAide(source, 0.78));

        SystemControls.StackPanel pileSyntheseConfiance = new();
        pileSyntheseConfiance.Children.Add(
            ConstruireTexteDetailAide(ConstruireTexteConfianceDetectionEmulateur(definition), 0.72)
        );
        pileSyntheseConfiance.Children.Add(
            ConstruireTexteDetailAide(
                ConstruireTexteValidationEmulateur(definition),
                0.78,
                FontWeights.SemiBold,
                string.IsNullOrWhiteSpace(ConstruireTexteValidationEmulateur(definition))
                    ? Visibility.Collapsed
                    : Visibility.Visible
            )
        );

        pile.Children.Insert(
            0,
            ConstruireGrilleSyntheseDiagnosticAide(
                dispositionCompacte,
                ConstruireCarteBlocDiagnosticAide(
                    "Source locale suivie",
                    pileSyntheseSource,
                    dispositionCompacte
                ),
                ConstruireCarteBlocDiagnosticAide(
                    "Niveau de confiance",
                    pileSyntheseConfiance,
                    dispositionCompacte
                )
            )
        );

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
     * Construit un texte de détail homogène pour les contenus explicatifs de
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
     * Construit une zone de texte copiable homogène pour les chemins et
     * journaux affichés dans la modale d'aide.
     */
    private SystemControls.Border ConstruireZoneTexteCopiableAide(string texte)
    {
        SystemControls.TextBlock texteBloc = new()
        {
            Text = texte,
            TextWrapping = TextWrapping.Wrap,
        };
        return ConstruireZoneTexteCopiableAide(texteBloc);
    }

    /*
     * Construit une zone de texte d'aide sans défilement interne, avec une
     * action de copie explicite pour éviter les barres de défilement imbriquées.
     */
    private SystemControls.Border ConstruireZoneTexteCopiableAide(
        SystemControls.TextBlock texteBloc
    )
    {
        UiControls.Button boutonCopier = new()
        {
            Content = "Copier",
            Padding = _modaleAideCompacteCourante
                ? ConstantesDesign.PaddingBoutonActionCompact
                : new Thickness(10, 4, 10, 4),
            Margin = new Thickness(8, 0, 0, 0),
            MinWidth = 0,
        };

        boutonCopier.Click += (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(texteBloc.Text))
            {
                Clipboard.SetText(texteBloc.Text);
            }
        };

        SystemControls.Grid grille = new();
        grille.ColumnDefinitions.Add(
            new SystemControls.ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
        );
        grille.ColumnDefinitions.Add(
            new SystemControls.ColumnDefinition { Width = GridLength.Auto }
        );

        texteBloc.Margin = new Thickness(0);
        texteBloc.VerticalAlignment = VerticalAlignment.Center;
        SystemControls.Grid.SetColumn(texteBloc, 0);
        grille.Children.Add(texteBloc);

        SystemControls.Grid.SetColumn(boutonCopier, 1);
        grille.Children.Add(boutonCopier);

        return new SystemControls.Border
        {
            Margin = new Thickness(0, 4, 0, 0),
            Padding = new Thickness(8, 6, 8, 6),
            CornerRadius = ConstantesDesign.RayonCoinsPetit,
            Background = new SolidColorBrush(Color.FromArgb(18, 255, 255, 255)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(28, 255, 255, 255)),
            BorderThickness = ConstantesDesign.EpaisseurContourFin,
            Child = grille,
        };
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
        IReadOnlyList<JeuRecentAffiche> jeux,
        bool dispositionCompacte = false
    )
    {
        SystemControls.StackPanel pile = new()
        {
            Margin = new Thickness(0, 0, 0, dispositionCompacte ? 10 : 14),
            Children =
            {
                new SystemControls.TextBlock
                {
                    Margin = new Thickness(0, 0, 0, 8),
                    FontSize = dispositionCompacte
                        ? ConstantesDesign.TaillePoliceInterfaceBase
                        : 16,
                    FontWeight = FontWeights.SemiBold,
                    Text = "Jeux récemment joués",
                },
            },
        };

        SystemControls.Grid grille = new() { Margin = new Thickness(0, 2, 0, 0) };
        int nombreColonnes = dispositionCompacte ? 1 : 2;

        for (int indexColonne = 0; indexColonne < nombreColonnes; indexColonne++)
        {
            grille.ColumnDefinitions.Add(
                new SystemControls.ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
            );
        }

        for (int index = 0; index < jeux.Count; index++)
        {
            while (grille.RowDefinitions.Count <= (index / nombreColonnes))
            {
                grille.RowDefinitions.Add(
                    new SystemControls.RowDefinition { Height = GridLength.Auto }
                );
            }

            SystemControls.Border carteJeu = new()
            {
                Margin = new Thickness(
                    0,
                    0,
                    index % nombreColonnes < nombreColonnes - 1 ? 8 : 0,
                    8
                ),
                Padding = dispositionCompacte
                    ? new Thickness(10, 8, 10, 8)
                    : new Thickness(12, 10, 12, 10),
                CornerRadius = ObtenirRayonCoins("RayonCoinsPetit", 8),
                Background = new SolidColorBrush(Color.FromArgb(24, 255, 255, 255)),
                Child = new SystemControls.StackPanel
                {
                    Children =
                    {
                        new SystemControls.TextBlock
                        {
                            FontWeight = FontWeights.SemiBold,
                            Text = jeux[index].Titre,
                            TextWrapping = TextWrapping.Wrap,
                        },
                        new SystemControls.TextBlock
                        {
                            Margin = new Thickness(0, 4, 0, 0),
                            Opacity = 0.72,
                            Text = jeux[index].SousTitre,
                            TextWrapping = TextWrapping.Wrap,
                        },
                    },
                },
            };
            SystemControls.Grid.SetRow(carteJeu, index / nombreColonnes);
            SystemControls.Grid.SetColumn(carteJeu, index % nombreColonnes);
            grille.Children.Add(carteJeu);
        }

        pile.Children.Add(grille);

        return new SystemControls.Border
        {
            Padding = new Thickness(10, 9, 10, 9),
            CornerRadius = ObtenirRayonCoins("RayonCoinsPetit", 8),
            Background = new SolidColorBrush(Color.FromArgb(18, 255, 255, 255)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(28, 255, 255, 255)),
            BorderThickness = ConstantesDesign.EpaisseurContourFin,
            Child = pile,
        };
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
     * Ajuste la zone de défilement de la modale d'aide une fois son arbre
     * visuel chargé.
     */
    private void DialogueAide_Chargement(object sender, RoutedEventArgs e)
    {
        if (sender is not UiControls.ContentDialog dialogueAide)
        {
            return;
        }

        dialogueAide.Dispatcher.BeginInvoke(
            () => AjusterDefilementDialogueAide(dialogueAide),
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
     * Retourne une largeur minimale de bouton plus compacte quand une modale
     * est affichée sur une faible largeur.
     */
    private static double CalculerLargeurMinimaleBoutonPiedModale(UiControls.ContentDialog dialogue)
    {
        return dialogue.ActualWidth < ConstantesDesign.EspaceFenetreLarge
            ? ConstantesDesign.EspaceHeroique
            : 120;
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
        double largeurMinimaleBouton = CalculerLargeurMinimaleBoutonPiedModale(dialogueConnexion);

        boutonSecondaire.MinWidth = largeurMinimaleBouton;
        boutonPrincipal.MinWidth = largeurMinimaleBouton;

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
                bouton.MinWidth = CalculerLargeurMinimaleBoutonPiedModale(dialogueCompte);
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
     * Remonte l'arbre visuel pour retrouver le premier ancêtre du type demandé.
     */
    private static TElement? TrouverPremierAncetre<TElement>(DependencyObject elementDepart)
        where TElement : DependencyObject
    {
        DependencyObject? elementCourant = VisualTreeHelper.GetParent(elementDepart);

        while (elementCourant is not null)
        {
            if (elementCourant is TElement ancetreTrouve)
            {
                return ancetreTrouve;
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
}
