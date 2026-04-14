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

        SystemControls.TextBox champPseudo = new()
        {
            MinWidth = LargeurContenuModaleConnexion,
            MaxWidth = LargeurContenuModaleConnexion,
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
            MinWidth = LargeurContenuModaleConnexion,
            MaxWidth = LargeurContenuModaleConnexion,
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
            MinWidth = LargeurContenuModaleConnexion,
            MaxWidth = LargeurContenuModaleConnexion,
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
            MinWidth = LargeurContenuModaleConnexion + (MargeInterieureModaleConnexion * 2) + 36,
            Content = new SystemControls.Border
            {
                Padding = ConstantesDesign.PaddingCarteSecondaire,
                Background = fondCarte,
                BorderBrush = bordure,
                BorderThickness = new Thickness(1),
                CornerRadius = rayonCoins,
                SnapsToDevicePixels = true,
                Child = new SystemControls.StackPanel
                {
                    Width = LargeurContenuModaleConnexion,
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
                        new SystemControls.StackPanel
                        {
                            Orientation = SystemControls.Orientation.Horizontal,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            Margin = new Thickness(0, 18, 0, 0),
                            Children = { boutonEnregistrer, boutonAnnuler },
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

        MemoriserGeometrieFenetre();
        await _serviceConfigurationLocale.SauvegarderUtilisateurAsync(_configurationConnexion);
        await _serviceConfigurationLocale.SauvegarderEtatApplicationAsync(_configurationConnexion);

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
        SystemControls.StackPanel contenu = new()
        {
            Width = ConstantesDesign.LargeurCarteSecondaire,
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
            Child = contenu,
        };

        UiControls.ContentDialog dialogueCompte = new(RacineModales)
        {
            Title = string.Empty,
            Content = conteneurContenu,
            MinWidth =
                ConstantesDesign.LargeurCarteSecondaire + (MargeInterieureModaleConnexion * 2),
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

        SystemControls.StackPanel contenu = new()
        {
            Width = ConstantesDesign.LargeurCarteSecondaire,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = ConstantesDesign.AucuneMarge,
            Children =
            {
                new SystemControls.TextBlock
                {
                    FontSize = ConstantesDesign.TaillePoliceTitreSection,
                    FontWeight = FontWeights.SemiBold,
                    Text = "Aide rapide",
                    Margin = new Thickness(0, 0, 0, 8),
                },
                new SystemControls.TextBlock
                {
                    Opacity = 0.84,
                    Text =
                        "Compagnon t'aide à suivre le jeu en cours, ta progression et les succès obtenus sur RetroAchievements.",
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 0, 0, 14),
                },
                ConstruireBlocAide(
                    "Pour commencer",
                    [
                        "Clique sur Profil pour connecter ton compte RetroAchievements.",
                        "Lance ensuite un jeu dans un émulateur compatible.",
                        "Compagnon affichera automatiquement le jeu détecté et ta progression.",
                    ],
                    "Connexion, lancement du jeu et détection automatique.",
                    true
                ),
                ConstruireBlocAide(
                    "Pendant le jeu",
                    [
                        "La notice en haut t'indique si une partie est en cours.",
                        "La carte principale affiche le jeu courant, les informations utiles et la liste des succès.",
                        "Lorsqu'un succès est obtenu, il peut être mis en avant temporairement dans la carte.",
                    ],
                    "Ce que Compagnon affiche pendant la session."
                ),
                ConstruireBlocAide(
                    "En cas de problème",
                    [
                        "Attends quelques secondes après un changement de jeu.",
                        "Vérifie que ton compte est toujours connecté.",
                        "Si besoin, relance d'abord l'émulateur, puis Compagnon.",
                    ],
                    "Les premiers réflexes si quelque chose manque."
                ),
                ConstruireBlocAideMiseAJourApplication(),
                ConstruireBlocAideLogsEmulateurs(),
            },
        };

        SystemControls.ScrollViewer defileurAide = new()
        {
            VerticalScrollBarVisibility = SystemControls.ScrollBarVisibility.Hidden,
            HorizontalScrollBarVisibility = SystemControls.ScrollBarVisibility.Disabled,
            MaxHeight = ConstantesDesign.HauteurMaximaleModale,
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
            MinWidth =
                ConstantesDesign.LargeurCarteSecondaire + (MargeInterieureModaleConnexion * 2),
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
     * Construit l'en-tête du compte avec avatar, pseudo et statut.
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
        bool estOuvertParDefaut = false
    )
    {
        SystemControls.StackPanel pile = new() { Margin = new Thickness(0) };

        foreach (string ligne in lignes)
        {
            pile.Children.Add(
                new SystemControls.TextBlock
                {
                    Margin = new Thickness(0, 0, 0, 6),
                    Opacity = 0.84,
                    Text = $"• {ligne}",
                    TextWrapping = TextWrapping.Wrap,
                }
            );
        }

        return ConstruireSectionAideRabattable(titre, pile, resume, estOuvertParDefaut);
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
        Action? auPremierDeploiement = null
    )
    {
        SystemControls.StackPanel entete = new()
        {
            Margin = new Thickness(0),
            Children =
            {
                new SystemControls.TextBlock
                {
                    FontSize = 16,
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
                    Margin = new Thickness(0, 3, 0, 0),
                    Opacity = 0.68,
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

        if (estOuvertParDefaut)
        {
            AssurerContenuInitialise();
        }

        return new SystemControls.Border
        {
            Padding = new Thickness(12, 10, 12, 10),
            Margin = new Thickness(0, 0, 0, 8),
            CornerRadius = ObtenirRayonCoins("RayonCoinsPetit", 8),
            Background = new SolidColorBrush(Color.FromArgb(24, 255, 255, 255)),
            Child = expander,
        };
    }

    /*
     * Construit la section d'aide consacrée aux logs, chemins et emplacements
     * des émulateurs.
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
                    "Compagnon peut t'indiquer quelle source locale il lit et où il retrouve chaque émulateur validé. Ouvre cette section si tu dois vérifier un journal, un dossier RACache ou l'emplacement d'un émulateur.",
                TextWrapping = TextWrapping.Wrap,
            }
        );
        pile.Children.Add(
            new SystemControls.TextBlock
            {
                Margin = new Thickness(0, 0, 0, 10),
                Opacity = 0.72,
                Text =
                    "Conseil : garde aussi tes émulateurs à jour. Les options de logs, les chemins et la qualité de détection peuvent changer selon la version.",
                TextWrapping = TextWrapping.Wrap,
            }
        );
        pile.Children.Add(
            new SystemControls.TextBox
            {
                Margin = new Thickness(0, 0, 0, 10),
                Style = (Style)FindResource("StyleTexteCopiable"),
                Text = ServiceSondeLocaleEmulateurs.ObtenirCheminJournal(),
                TextWrapping = TextWrapping.Wrap,
            }
        );

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
            "Logs des émulateurs",
            pile,
            "Chemins surveillés, emplacements détectés et sources locales utilisées.",
            false,
            ChargerContenu
        );
    }

    /*
     * Construit la carte de diagnostic d'un émulateur avec ses sources
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
            ? "Non trouvé sur ce PC"
            : "Détecté sur ce PC";
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
                    ? "Ce chemin manuel est prioritaire si Compagnon hésite entre plusieurs signatures d'émulateur."
                : !string.IsNullOrWhiteSpace(emplacementDetecteMemorise)
                    ? "Compagnon a mémorisé cet emplacement après avoir vu l'émulateur ouvert sur ce PC."
                : "Si un exécutable est renommé ou ambigu, tu peux choisir manuellement le bon fichier .exe ici.";

            boutonChoisirEmplacement.Content = !string.IsNullOrWhiteSpace(emplacementManuel)
                ? "Modifier l'emplacement manuel"
                : "Choisir un exécutable";

            boutonRetirerEmplacement.Visibility = !string.IsNullOrWhiteSpace(emplacementManuel)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        SystemControls.StackPanel pile = new()
        {
            Children =
            {
                new SystemControls.TextBlock
                {
                    Margin = new Thickness(0, 0, 0, 0),
                    Opacity = 0.78,
                    Text = source,
                    TextWrapping = TextWrapping.Wrap,
                },
                new SystemControls.TextBlock
                {
                    Margin = new Thickness(0, 6, 0, 0),
                    FontWeight = FontWeights.SemiBold,
                    Opacity = 0.78,
                    Text = ConstruireTexteValidationEmulateur(definition),
                    TextWrapping = TextWrapping.Wrap,
                    Visibility = string.IsNullOrWhiteSpace(
                        ConstruireTexteValidationEmulateur(definition)
                    )
                        ? Visibility.Collapsed
                        : Visibility.Visible,
                },
                new SystemControls.TextBlock
                {
                    Margin = new Thickness(0, 6, 0, 0),
                    Opacity = 0.72,
                    Text = ConstruireTexteConfianceDetectionEmulateur(definition),
                    TextWrapping = TextWrapping.Wrap,
                },
                texteStatutEmplacement,
                texteEmplacement,
                texteAideEmplacementManuel,
                new SystemControls.StackPanel
                {
                    Orientation = SystemControls.Orientation.Horizontal,
                    Children = { boutonChoisirEmplacement, boutonRetirerEmplacement },
                },
                new SystemControls.TextBlock
                {
                    Margin = new Thickness(0, 6, 0, 0),
                    FontWeight = FontWeights.SemiBold,
                    Opacity = 0.72,
                    Text = statutChemin,
                    TextWrapping = TextWrapping.Wrap,
                },
                new SystemControls.TextBox
                {
                    Margin = new Thickness(0, 4, 0, 0),
                    Style = (Style)FindResource("StyleTexteCopiable"),
                    Text = cheminAttendu,
                    TextWrapping = TextWrapping.Wrap,
                },
                new SystemControls.TextBlock
                {
                    Margin = new Thickness(0, 6, 0, 0),
                    Opacity = 0.66,
                    Text = ConstruireTexteActivationSourceLocale(definition),
                    TextWrapping = TextWrapping.Wrap,
                },
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
                "Source suivie : journal local de RetroArch.",
            StrategieRenseignementJeuEmulateurLocal.BizHawkConfig =>
                "Source suivie : fichier retroachievements-game-log.json de BizHawk.",
            StrategieRenseignementJeuEmulateurLocal.DolphinConfig =>
                "Source suivie : fichier dolphin.log de Dolphin, avec le processus en secours.",
            StrategieRenseignementJeuEmulateurLocal.DuckStationLog =>
                "Source suivie : fichier duckstation.log.",
            StrategieRenseignementJeuEmulateurLocal.PCSX2Log =>
                "Source suivie : fichier emulog.txt de PCSX2.",
            StrategieRenseignementJeuEmulateurLocal.PPSSPPLog =>
                "Source suivie : journal local de PPSSPP.",
            StrategieRenseignementJeuEmulateurLocal.Project64RACache =>
                "Source suivie : RACache et journal RALog.txt.",
            StrategieRenseignementJeuEmulateurLocal.RALibretroRACache =>
                "Source suivie : RACache et journal RALog.txt.",
            StrategieRenseignementJeuEmulateurLocal.RANesRACache =>
                "Source suivie : RACache et journal RALog.txt.",
            StrategieRenseignementJeuEmulateurLocal.RAVBARACache =>
                "Source suivie : RACache et journal RALog.txt.",
            StrategieRenseignementJeuEmulateurLocal.RASnes9xRACache =>
                "Source suivie : RACache et journal RALog.txt.",
            StrategieRenseignementJeuEmulateurLocal.FlycastConfig =>
                "Source suivie : fichier flycast.log à la racine et chemin du jeu chargé en secours.",
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
                "Dans RetroArch : active d'abord `Settings -> User Interface -> Show Advanced Settings`, puis `Settings -> Logging -> Log to File`. Désactive aussi l'option de fichiers de journalisation horodatés pour conserver un fichier stable `retroarch.log`. Le journal doit ensuite être écrit dans le dossier `logs` pendant la session. Garde aussi RetroArch à jour.",
            StrategieRenseignementJeuEmulateurLocal.BizHawkConfig =>
                "Dans BizHawk : garde `retroachievements-game-log.json` à la racine du dossier de l'émulateur. Compagnon lit d'abord ce fichier pour le Game ID et le titre, puis garde `config.ini` seulement en secours pour retrouver le chemin de la ROM.",
            StrategieRenseignementJeuEmulateurLocal.DolphinConfig =>
                "Dans Dolphin : ouvre `View -> Show Log Configuration`, coche `Write to File`, garde le type `RetroAchievements` activé et une verbosité au moins sur `Info`. Compagnon lit ensuite `dolphin.log` pour le jeu et les succès.",
            StrategieRenseignementJeuEmulateurLocal.DuckStationLog =>
                "Dans DuckStation : ouvre `Settings -> Advanced Settings`, règle `Log Level` sur `Debug`, puis active `Log To File`. Si `duckstation.log` n'apparaît pas tout de suite, redémarre DuckStation pour forcer son écriture. Garde aussi DuckStation à jour.",
            StrategieRenseignementJeuEmulateurLocal.PCSX2Log =>
                "Dans PCSX2 : en général, rien de plus n'est nécessaire. L'émulateur génère normalement `emulog.txt` dans son dossier `logs`. Si ce fichier n'apparaît pas, vérifie les options de console ou de débogage propres à ta version. Garde aussi PCSX2 à jour.",
            StrategieRenseignementJeuEmulateurLocal.PPSSPPLog =>
                "Dans PPSSPP : ouvre `Tools -> Developer Tools`, puis active `Enable debug logging`. Si aucun fichier n'est encore écrit sur disque, lance PPSSPP avec une option du type `--log=...` pour forcer la création du journal. Garde aussi PPSSPP à jour.",
            StrategieRenseignementJeuEmulateurLocal.Project64RACache =>
                "Dans RAP64 : ce n'est pas un journal classique. Il faut surtout que RetroAchievements soit bien activé pour que `RACache` et `RALog.txt` se mettent à jour pendant la session. Garde aussi l'émulateur à jour.",
            StrategieRenseignementJeuEmulateurLocal.RALibretroRACache =>
                "Dans RALibretro : ce n'est pas un journal classique. Il faut surtout que RetroAchievements soit bien activé pour que `RACache` et `RALog.txt` se mettent à jour pendant la session. Garde aussi l'émulateur à jour.",
            StrategieRenseignementJeuEmulateurLocal.RANesRACache =>
                "Dans RANes : ce n'est pas un journal classique. Il faut surtout que RetroAchievements soit bien activé pour que `RACache` et `RALog.txt` se mettent à jour pendant la session. Garde aussi l'émulateur à jour.",
            StrategieRenseignementJeuEmulateurLocal.RAVBARACache =>
                "Dans RAVBA : ce n'est pas un journal classique. Il faut surtout que RetroAchievements soit bien activé pour que `RACache` et `RALog.txt` se mettent à jour pendant la session. Garde aussi l'émulateur à jour.",
            StrategieRenseignementJeuEmulateurLocal.RASnes9xRACache =>
                "Dans RASnes9x : ce n'est pas un journal classique. Il faut surtout que RetroAchievements soit bien activé pour que `RACache` et `RALog.txt` se mettent à jour pendant la session. Garde aussi l'émulateur à jour.",
            StrategieRenseignementJeuEmulateurLocal.FlycastConfig =>
                "Dans Flycast : active l'écriture du fichier `flycast.log` à la racine de l'émulateur. Compagnon s'appuie d'abord sur ce journal, puis sur le chemin du jeu lancé si besoin. Garde aussi Flycast à jour.",
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
                "Confiance de détection : bonne. Compagnon s'appuie sur EmuHawk et sur retroachievements-game-log.json, avec config.ini en secours pour la ROM.",
            StrategieRenseignementJeuEmulateurLocal.DolphinConfig =>
                "Confiance de détection : bonne. Compagnon s'appuie d'abord sur dolphin.log, avec le processus Dolphin en secours.",
            StrategieRenseignementJeuEmulateurLocal.Project64RACache =>
                "Confiance de détection : excellente. Compagnon croise le processus émulateur avec RACache et RALog.txt.",
            StrategieRenseignementJeuEmulateurLocal.RALibretroRACache =>
                "Confiance de détection : excellente. Compagnon croise le processus émulateur avec RACache et RALog.txt.",
            StrategieRenseignementJeuEmulateurLocal.RANesRACache =>
                "Confiance de détection : excellente. Compagnon croise le processus émulateur avec RACache et RALog.txt.",
            StrategieRenseignementJeuEmulateurLocal.RAVBARACache =>
                "Confiance de détection : excellente. Compagnon croise le processus émulateur avec RACache et RALog.txt.",
            StrategieRenseignementJeuEmulateurLocal.RASnes9xRACache =>
                "Confiance de détection : excellente. Compagnon croise le processus émulateur avec RACache et RALog.txt.",
            StrategieRenseignementJeuEmulateurLocal.FlycastConfig =>
                "Confiance de détection : bonne. Compagnon s'appuie sur le processus Flycast, sur flycast.log et sur le disque lancé en secours.",
            StrategieRenseignementJeuEmulateurLocal.RetroArchLog =>
                "Confiance de détection : bonne. Compagnon s'appuie sur le processus et sur les journaux locaux de RetroArch.",
            StrategieRenseignementJeuEmulateurLocal.DuckStationLog =>
                "Confiance de détection : bonne. Compagnon s'appuie sur le processus et sur duckstation.log.",
            StrategieRenseignementJeuEmulateurLocal.PCSX2Log =>
                "Confiance de détection : bonne. Compagnon s'appuie sur le processus et sur emulog.txt.",
            StrategieRenseignementJeuEmulateurLocal.PPSSPPLog =>
                "Confiance de détection : bonne. Compagnon s'appuie sur le processus et sur les journaux locaux de PPSSPP.",
            _ =>
                "Confiance de détection : fragile. L'identification peut demander une vérification manuelle.",
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
