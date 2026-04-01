using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using RA.Compagnon.Modeles.Api.V2.User;
using RA.Compagnon.Modeles.Local;
using RA.Compagnon.Modeles.Presentation;
using RA.Compagnon.Services;
using SystemControls = System.Windows.Controls;
using UiControls = Wpf.Ui.Controls;

namespace RA.Compagnon;

public partial class MainWindow
{
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

    private void DefinirEtatModalesActif(bool actif)
    {
        if (RacineModales is null)
        {
            return;
        }

        RacineModales.Visibility = actif ? Visibility.Visible : Visibility.Collapsed;
        RacineModales.IsHitTestVisible = actif;
    }

    private void DefinirVoileConnexionActif(bool actif)
    {
        if (VoileFenetreConnexion is null)
        {
            return;
        }

        VoileFenetreConnexion.Visibility = actif ? Visibility.Visible : Visibility.Collapsed;
    }

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
        CornerRadius rayonCoins = ObtenirRayonCoins("RayonCoinsStandard", 12);

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
                Padding = new Thickness(MargeInterieureModaleConnexion),
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
                            FontSize = 20,
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

    private async Task AfficherModaleCompteAsync()
    {
        DonneesCompteUtilisateur donnees = await ObtenirDonneesComptePourModaleAsync();
        CompteAffiche compte = ServicePresentationCompte.Construire(
            donnees,
            _configurationConnexion.Pseudo
        );
        SystemControls.StackPanel contenu = new()
        {
            Width = 460,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0),
        };
        contenu.Children.Add(ConstruireEnTeteAvatarCompte(compte));
        contenu.Children.Add(
            new SystemControls.TextBlock
            {
                Margin = new Thickness(0, 12, 0, 0),
                FontSize = 22,
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
                    FontSize = 14,
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
                Padding = new Thickness(14, 6, 14, 6),
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
            Padding = new Thickness(MargeInterieureModaleConnexion),
            HorizontalAlignment = HorizontalAlignment.Center,
            CornerRadius = ObtenirRayonCoins("RayonCoinsStandard", 12),
            Child = contenu,
        };

        UiControls.ContentDialog dialogueCompte = new(RacineModales)
        {
            Title = string.Empty,
            Content = conteneurContenu,
            MinWidth = 460 + (MargeInterieureModaleConnexion * 2),
            PrimaryButtonText = "Déconnexion",
            DefaultButton = UiControls.ContentDialogButton.Primary,
        };

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

    private async Task AfficherModaleAideAsync()
    {
        await VerifierMiseAJourApplicationSiNecessaireAsync();

        SystemControls.StackPanel contenu = new()
        {
            Width = 460,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0),
            Children =
            {
                new SystemControls.TextBlock
                {
                    FontSize = 20,
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
                    ]
                ),
                ConstruireBlocAide(
                    "Pendant le jeu",
                    [
                        "La notice en haut t'indique si une partie est en cours.",
                        "La carte principale affiche le jeu courant, les informations utiles et la liste des succès.",
                        "Lorsqu'un succès est obtenu, il peut être mis en avant temporairement dans la carte.",
                    ]
                ),
                ConstruireBlocAide(
                    "En cas de problème",
                    [
                        "Attends quelques secondes après un changement de jeu.",
                        "Vérifie que ton compte est toujours connecté.",
                        "Si besoin, relance d'abord l'émulateur, puis Compagnon.",
                    ]
                ),
                ConstruireBlocAideMiseAJourApplication(),
                ConstruireBlocAideLogsEmulateurs(),
            },
        };

        SystemControls.Border conteneurContenu = new()
        {
            Padding = new Thickness(MargeInterieureModaleConnexion),
            HorizontalAlignment = HorizontalAlignment.Center,
            CornerRadius = ObtenirRayonCoins("RayonCoinsStandard", 12),
            Child = contenu,
        };

        UiControls.ContentDialog dialogueAide = new(RacineModales)
        {
            Title = "Aide",
            Content = conteneurContenu,
            MinWidth = 460 + (MargeInterieureModaleConnexion * 2),
            CloseButtonText = "Fermer",
            DefaultButton = UiControls.ContentDialogButton.Close,
        };

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

    private static SystemControls.Separator ConstruireSeparateurBlocCompte()
    {
        return new SystemControls.Separator { Margin = new Thickness(0, 2, 0, 12), Opacity = 0.45 };
    }

    private SystemControls.Border ConstruireBlocAide(string titre, IReadOnlyList<string> lignes)
    {
        SystemControls.StackPanel pile = new()
        {
            Margin = new Thickness(0, 0, 0, 12),
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

        return new SystemControls.Border
        {
            Padding = new Thickness(10, 10, 10, 6),
            Margin = new Thickness(0, 0, 0, 8),
            CornerRadius = ObtenirRayonCoins("RayonCoinsPetit", 8),
            Background = new SolidColorBrush(Color.FromArgb(24, 255, 255, 255)),
            Child = pile,
        };
    }

    private SystemControls.Border ConstruireBlocAideLogsEmulateurs()
    {
        SystemControls.StackPanel pile = new() { Margin = new Thickness(0, 0, 0, 12) };

        pile.Children.Add(
            new SystemControls.TextBlock
            {
                Margin = new Thickness(0, 0, 0, 8),
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Text = "Logs des émulateurs",
            }
        );
        pile.Children.Add(
            new SystemControls.TextBlock
            {
                Margin = new Thickness(0, 0, 0, 10),
                Opacity = 0.78,
                Text =
                    "Compagnon peut t'indiquer quelle source locale il lit pour chaque émulateur validé. Ouvre cette section si tu dois vérifier un journal ou un dossier RACache.",
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

        SystemControls.Expander expander = new()
        {
            Header = "Voir les chemins et les sources locales",
            IsExpanded = false,
        };

        SystemControls.StackPanel contenu = new();

        foreach (
            DefinitionEmulateurLocal definition in ServiceCatalogueEmulateursLocaux.Definitions.Where(
                EstEmulateurValidePourIndicatifLogs
            )
        )
        {
            contenu.Children.Add(ConstruireCarteIndicatifLogsEmulateur(definition));
        }

        expander.Content = contenu;
        pile.Children.Add(expander);

        return new SystemControls.Border
        {
            Padding = new Thickness(10, 10, 10, 8),
            Margin = new Thickness(0, 0, 0, 8),
            CornerRadius = ObtenirRayonCoins("RayonCoinsPetit", 8),
            Background = new SolidColorBrush(Color.FromArgb(24, 255, 255, 255)),
            Child = pile,
        };
    }

    private SystemControls.Border ConstruireCarteIndicatifLogsEmulateur(
        DefinitionEmulateurLocal definition
    )
    {
        string source = ConstruireLibelleSourceLocaleEmulateur(definition);
        string cheminDetecte = ServiceSourcesLocalesEmulateurs.TrouverCheminJournalSuccesLocal(
            definition.NomEmulateur
        );
        string cheminAttendu = string.IsNullOrWhiteSpace(cheminDetecte)
            ? ConstruireCheminIndicatifSourceLocale(definition)
            : cheminDetecte;
        string statutChemin = string.IsNullOrWhiteSpace(cheminDetecte)
            ? "Non trouvé sur ce PC"
            : "Détecté sur ce PC";

        SystemControls.StackPanel pile = new()
        {
            Children =
            {
                new SystemControls.TextBlock
                {
                    FontWeight = FontWeights.SemiBold,
                    Text = definition.NomEmulateur,
                    TextWrapping = TextWrapping.Wrap,
                },
                new SystemControls.TextBlock
                {
                    Margin = new Thickness(0, 4, 0, 0),
                    Opacity = 0.78,
                    Text = source,
                    TextWrapping = TextWrapping.Wrap,
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

        return new SystemControls.Border
        {
            Margin = new Thickness(0, 0, 0, 8),
            Padding = new Thickness(10, 8, 10, 8),
            CornerRadius = ObtenirRayonCoins("RayonCoinsPetit", 8),
            Background = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255)),
            Child = pile,
        };
    }

    private static bool EstEmulateurValidePourIndicatifLogs(DefinitionEmulateurLocal definition)
    {
        return definition.StrategieRenseignementJeu
                != StrategieRenseignementJeuEmulateurLocal.Aucune
            || definition.StrategieSurveillanceSucces != StrategieSurveillanceSuccesLocale.Aucune;
    }

    private static string ConstruireLibelleSourceLocaleEmulateur(
        DefinitionEmulateurLocal definition
    )
    {
        return definition.StrategieRenseignementJeu switch
        {
            StrategieRenseignementJeuEmulateurLocal.RetroArchLog =>
                "Source suivie : journal local de RetroArch.",
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
            _ => "Source locale non précisée.",
        };
    }

    private static string ConstruireTexteActivationSourceLocale(DefinitionEmulateurLocal definition)
    {
        return definition.StrategieRenseignementJeu switch
        {
            StrategieRenseignementJeuEmulateurLocal.RetroArchLog =>
                "Où l'activer : Settings -> User Interface -> Show Advanced Settings, puis Settings -> Logging -> Log to File. Le journal doit ensuite être écrit dans le dossier logs pendant la session. Garde RetroArch à jour.",
            StrategieRenseignementJeuEmulateurLocal.DuckStationLog =>
                "Où l'activer : Settings -> Advanced Settings. Mets Log Level sur Debug, puis coche Log To File. Redémarre DuckStation si besoin pour forcer l'écriture de duckstation.log. Garde aussi DuckStation à jour.",
            StrategieRenseignementJeuEmulateurLocal.PCSX2Log =>
                "Où l'activer : en général, rien de plus n'est nécessaire. PCSX2 génère normalement emulog.txt dans son dossier logs. Si ce fichier n'apparaît pas, vérifie les options de console ou de débogage de ta version. Garde aussi PCSX2 à jour.",
            StrategieRenseignementJeuEmulateurLocal.PPSSPPLog =>
                "Où l'activer : Tools -> Developer Tools -> Enable debug logging. Si ta version n'écrit toujours pas de fichier, lance PPSSPP avec l'option --log=... pour forcer un log sur disque. Garde aussi PPSSPP à jour.",
            StrategieRenseignementJeuEmulateurLocal.Project64RACache =>
                "Où l'activer : ce n'est pas un log classique. Il faut surtout que RetroAchievements soit actif dans Project64 pour que RACache et RALog.txt se mettent à jour pendant la session. Garde aussi l'émulateur à jour.",
            StrategieRenseignementJeuEmulateurLocal.RALibretroRACache =>
                "Où l'activer : ce n'est pas un log classique. Il faut surtout que RetroAchievements soit actif dans RALibretro pour que RACache et RALog.txt se mettent à jour pendant la session. Garde aussi l'émulateur à jour.",
            StrategieRenseignementJeuEmulateurLocal.RANesRACache =>
                "Où l'activer : ce n'est pas un log classique. Il faut surtout que RetroAchievements soit actif dans RANes pour que RACache et RALog.txt se mettent à jour pendant la session. Garde aussi l'émulateur à jour.",
            StrategieRenseignementJeuEmulateurLocal.RAVBARACache =>
                "Où l'activer : ce n'est pas un log classique. Il faut surtout que RetroAchievements soit actif dans RAVBA pour que RACache et RALog.txt se mettent à jour pendant la session. Garde aussi l'émulateur à jour.",
            StrategieRenseignementJeuEmulateurLocal.RASnes9xRACache =>
                "Où l'activer : ce n'est pas un log classique. Il faut surtout que RetroAchievements soit actif dans RASnes9x pour que RACache et RALog.txt se mettent à jour pendant la session. Garde aussi l'émulateur à jour.",
            _ => string.Empty,
        };
    }

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
                "Luna_Project64",
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
            _ => "Chemin local non défini.",
        };
    }

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

    private static string ConstruireUrlProfilRetroAchievements(string nomUtilisateur)
    {
        return $"https://retroachievements.org/user/{Uri.EscapeDataString(nomUtilisateur)}";
    }

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
        catch
        {
            // L'ouverture du navigateur reste facultative.
        }
    }

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

    private async void ConfigurerConnexion_Click(object sender, RoutedEventArgs e)
    {
        MemoriserGeometrieFenetre();
        ArreterActualisationAutomatique();
        DefinirEtatConnexion("Modification en cours");

        await AfficherModaleConnexionAsync();
    }

    private async void AfficherCompte_Click(object sender, RoutedEventArgs e)
    {
        if (!ConfigurationConnexionEstComplete())
        {
            await AfficherModaleConnexionAsync();
            return;
        }

        await AfficherModaleCompteAsync();
    }

    private async void AfficherAide_Click(object sender, RoutedEventArgs e)
    {
        await AfficherModaleAideAsync();
    }

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

        MettreAJourNoticeCompteEntete();
    }

    private string ObtenirLibelleBoutonCompte()
    {
        return string.IsNullOrWhiteSpace(_configurationConnexion.Pseudo)
            ? "Connexion"
            : _configurationConnexion.Pseudo;
    }

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
            ZoneEtatCompteUtilisateur.Visibility = Visibility.Collapsed;
            TexteSousEtatCompteUtilisateur.Visibility = Visibility.Collapsed;
            TexteEtatCompteUtilisateur.Text = string.Empty;
            TexteSousEtatCompteUtilisateur.Text = string.Empty;
            ZoneEtatCompteUtilisateur.ToolTip = null;
            _signatureDerniereNoticeCompteJournalisee = string.Empty;
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
            TexteEtatCompteUtilisateur.Text = "En jeu";
            TexteSousEtatCompteUtilisateur.Text = texteIdentifiantJeuAffiche;
            TexteSousEtatCompteUtilisateur.Visibility = string.IsNullOrWhiteSpace(
                texteIdentifiantJeuAffiche
            )
                ? Visibility.Collapsed
                : Visibility.Visible;

            BadgeEtatCompteUtilisateur.Background = Brushes.Transparent;
            BadgeEtatCompteUtilisateur.BorderBrush = Brushes.Transparent;
            ZoneEtatCompteUtilisateur.Visibility = Visibility.Visible;
            ZoneEtatCompteUtilisateur.ToolTip =
                identifiantJeuAffiche > 0
                    ? $"En jeu{Environment.NewLine}Game ID {identifiantJeuAffiche.ToString(CultureInfo.CurrentCulture)}"
                    : "En jeu (détection locale)";
            JournaliserNoticeCompteEntete("En jeu", texteIdentifiantJeu, "local");
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
            ZoneEtatCompteUtilisateur.Visibility = Visibility.Collapsed;
            TexteSousEtatCompteUtilisateur.Visibility = Visibility.Collapsed;
            TexteEtatCompteUtilisateur.Text = string.Empty;
            TexteSousEtatCompteUtilisateur.Text = string.Empty;
            ZoneEtatCompteUtilisateur.ToolTip = null;
            return;
        }

        TexteEtatCompteUtilisateur.Text = compte.Statut.Trim();

        bool afficherSousStatut = !string.IsNullOrWhiteSpace(texteIdentifiantJeuAffiche);
        TexteSousEtatCompteUtilisateur.Text = texteIdentifiantJeuAffiche;
        TexteSousEtatCompteUtilisateur.Visibility = afficherSousStatut
            ? Visibility.Visible
            : Visibility.Collapsed;

        BadgeEtatCompteUtilisateur.Background = Brushes.Transparent;
        BadgeEtatCompteUtilisateur.BorderBrush = Brushes.Transparent;
        ZoneEtatCompteUtilisateur.Visibility = Visibility.Visible;
        ZoneEtatCompteUtilisateur.ToolTip =
            identifiantJeuAffiche > 0
                ? $"{compte.Statut}{Environment.NewLine}Game ID {identifiantJeuAffiche.ToString(CultureInfo.CurrentCulture)}"
                : compte.Statut;
        JournaliserNoticeCompteEntete(compte.Statut, texteIdentifiantJeu, "api");
    }

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

    private static (Brush Fond, Brush Bordure) ObtenirCouleursNoticeCompteEntete(string statut)
    {
        if (statut.Contains("En jeu", StringComparison.OrdinalIgnoreCase))
        {
            return (
                new SolidColorBrush(Color.FromArgb(36, 58, 188, 116)),
                new SolidColorBrush(Color.FromArgb(96, 58, 188, 116))
            );
        }

        if (statut.Contains("Actif", StringComparison.OrdinalIgnoreCase))
        {
            return (
                new SolidColorBrush(Color.FromArgb(24, 120, 200, 255)),
                new SolidColorBrush(Color.FromArgb(56, 120, 200, 255))
            );
        }

        if (statut.Contains("Inactif", StringComparison.OrdinalIgnoreCase))
        {
            return (
                new SolidColorBrush(Color.FromArgb(22, 160, 160, 160)),
                new SolidColorBrush(Color.FromArgb(56, 160, 160, 160))
            );
        }

        return (
            new SolidColorBrush(Color.FromArgb(24, 120, 200, 255)),
            new SolidColorBrush(Color.FromArgb(56, 120, 200, 255))
        );
    }

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

    private void DefinirEtatConnexion(string etatConnexion)
    {
        _etatConnexionCourant = etatConnexion;
        MettreAJourResumeConnexion();
    }

    private static void OuvrirDocumentProjet(string nomFichier)
    {
        if (string.IsNullOrWhiteSpace(nomFichier))
        {
            return;
        }

        string? dossierCourant = string.IsNullOrWhiteSpace(AppContext.BaseDirectory)
            ? null
            : System.IO.Path.GetFullPath(AppContext.BaseDirectory);

        while (!string.IsNullOrWhiteSpace(dossierCourant))
        {
            string cheminCandidat = System.IO.Path.Combine(dossierCourant, nomFichier);

            if (System.IO.File.Exists(cheminCandidat))
            {
                try
                {
                    Process.Start(
                        new ProcessStartInfo { FileName = cheminCandidat, UseShellExecute = true }
                    );
                }
                catch
                {
                    // L'ouverture d'un document local reste facultative.
                }

                return;
            }

            System.IO.DirectoryInfo? parent = System.IO.Directory.GetParent(dossierCourant);
            dossierCourant = parent?.FullName;
        }
    }

    private bool ConfigurationConnexionEstComplete()
    {
        return !string.IsNullOrWhiteSpace(_configurationConnexion.Pseudo)
            && !string.IsNullOrWhiteSpace(_configurationConnexion.CleApiWeb);
    }

    private static string TexteBouton(SystemControls.Button bouton)
    {
        return bouton.Content?.ToString()?.Trim() ?? string.Empty;
    }

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
