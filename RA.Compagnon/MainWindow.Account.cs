using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using RA.Compagnon.Modeles.Api.V2.User;
using RA.Compagnon.Modeles.Presentation;
using RA.Compagnon.Services;
using SystemControls = System.Windows.Controls;
using UiControls = Wpf.Ui.Controls;

namespace RA.Compagnon;

public partial class MainWindow
{
    private void DefinirEtatModalesActif(bool actif)
    {
        if (RacineModales is null)
        {
            return;
        }

        RacineModales.Visibility = actif ? Visibility.Visible : Visibility.Collapsed;
        RacineModales.IsHitTestVisible = actif;
    }

    private async Task AfficherModaleConnexionAsync(bool masquerContenuPrincipal = true)
    {
        ArreterActualisationAutomatique();
        if (masquerContenuPrincipal)
        {
            DefinirVisibiliteContenuPrincipal(false);
        }

        while (true)
        {
            bool connexionDejaConfiguree = ConfigurationConnexionEstComplete();
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
                        FontSize = 20,
                        FontWeight = FontWeights.SemiBold,
                        Text = "Connecter ton compte",
                        Margin = new Thickness(0, 0, 0, 8),
                    },
                    new SystemControls.TextBlock
                    {
                        MinWidth = LargeurContenuModaleConnexion,
                        MaxWidth = LargeurContenuModaleConnexion,
                        Opacity = 0.84,
                        Text =
                            "Entre ton pseudo et ta clé Web API pour synchroniser ton dernier jeu joué, ta progression et tes succès récents.",
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(0, 0, 0, 14),
                    },
                    champPseudo,
                    champCleApi,
                    new SystemControls.TextBlock
                    {
                        MinWidth = LargeurContenuModaleConnexion,
                        MaxWidth = LargeurContenuModaleConnexion,
                        Margin = new Thickness(0, 10, 0, 0),
                        Opacity = 0.68,
                        Text =
                            "Tu peux retrouver cette clé depuis ton compte RetroAchievements, dans la section dédiée à l'API Web.",
                        TextWrapping = TextWrapping.Wrap,
                    },
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
                Title = "Connexion RetroAchievements",
                Content = conteneurContenu,
                MinWidth = LargeurContenuModaleConnexion + (MargeInterieureModaleConnexion * 2),
                PrimaryButtonText = "Enregistrer",
                SecondaryButtonText = connexionDejaConfiguree ? "Annuler" : "Fermer",
                DefaultButton = UiControls.ContentDialogButton.Primary,
            };

            dialogueConnexion.Loaded += DialogueConnexion_Chargement;

            UiControls.ContentDialogResult resultat;

            try
            {
                DefinirEtatModalesActif(true);
                resultat = await dialogueConnexion.ShowAsync();
            }
            finally
            {
                DefinirEtatModalesActif(false);
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
                texteErreur.Text = "Renseigne ton pseudo et ta clé Web API pour continuer.";
                texteErreur.Visibility = Visibility.Visible;
                continue;
            }

            try
            {
                await _serviceUtilisateurRetroAchievements.ObtenirProfilAsync(pseudo, cleApi);
            }
            catch (UtilisateurRetroAchievementsInaccessibleException exception)
            {
                texteErreur.Text = exception.Message;
                texteErreur.Visibility = Visibility.Visible;
                continue;
            }
            catch (Exception exception)
            {
                string messageErreur = string.IsNullOrWhiteSpace(exception.Message)
                    ? "Impossible de vérifier ce compte pour le moment. Vérifie ta connexion et réessaie."
                    : $"Impossible de vérifier ce compte pour le moment. {exception.Message}";
                texteErreur.Text = messageErreur;
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

    private async Task AfficherModaleCompteAsync()
    {
        DonneesCompteUtilisateur donnees = await ObtenirDonneesComptePourModaleAsync();
        CompteAffiche compte = _servicePresentationCompte.Construire(
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
            PrimaryButtonText = "Modifier la connexion",
            SecondaryButtonText = "Profil RA",
            CloseButtonText = "Fermer",
            DefaultButton = UiControls.ContentDialogButton.Secondary,
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
            await AfficherModaleConnexionAsync(false);
            return;
        }

        if (resultat == UiControls.ContentDialogResult.Secondary)
        {
            OuvrirProfilRetroAchievements(compte.NomUtilisateur);
        }
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
            await _serviceUtilisateurRetroAchievements.ObtenirDonneesCompteAsync(
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

    private FrameworkElement ConstruireEnTeteAvatarCompte(CompteAffiche compte)
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

        if (EtatLocalEmulateurEstActifPourNotice())
        {
            TexteEtatCompteUtilisateur.Text = "En jeu";
            TexteSousEtatCompteUtilisateur.Text = texteIdentifiantJeu;
            TexteSousEtatCompteUtilisateur.Visibility = string.IsNullOrWhiteSpace(
                texteIdentifiantJeu
            )
                ? Visibility.Collapsed
                : Visibility.Visible;

            (Brush fondLocal, Brush bordureLocale) = ObtenirCouleursNoticeCompteEntete("En jeu");
            BadgeEtatCompteUtilisateur.Background = fondLocal;
            BadgeEtatCompteUtilisateur.BorderBrush = bordureLocale;
            ZoneEtatCompteUtilisateur.Visibility = Visibility.Visible;
            ZoneEtatCompteUtilisateur.ToolTip = string.IsNullOrWhiteSpace(texteIdentifiantJeu)
                ? "En jeu (détection locale)"
                : $"En jeu{Environment.NewLine}{texteIdentifiantJeu}";
            JournaliserNoticeCompteEntete("En jeu", texteIdentifiantJeu, "local");
            return;
        }

        CompteAffiche compte = _servicePresentationCompte.Construire(
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

        bool afficherSousStatut = !string.IsNullOrWhiteSpace(texteIdentifiantJeu);
        TexteSousEtatCompteUtilisateur.Text = texteIdentifiantJeu;
        TexteSousEtatCompteUtilisateur.Visibility = afficherSousStatut
            ? Visibility.Visible
            : Visibility.Collapsed;

        (Brush fond, Brush bordure) = ObtenirCouleursNoticeCompteEntete(compte.Statut);
        BadgeEtatCompteUtilisateur.Background = fond;
        BadgeEtatCompteUtilisateur.BorderBrush = bordure;
        ZoneEtatCompteUtilisateur.Visibility = Visibility.Visible;
        ZoneEtatCompteUtilisateur.ToolTip = afficherSousStatut
            ? $"{compte.Statut}{Environment.NewLine}{texteIdentifiantJeu}"
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
