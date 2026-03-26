using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using RA.Compagnon.Modeles.Api;
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
            DefinirEtatModalesActif(true);
            resultat = await dialogueCompte.ShowAsync();
        }
        finally
        {
            DefinirEtatModalesActif(false);
            dialogueCompte.Loaded -= DialogueConnexion_Chargement;
        }

        if (resultat == UiControls.ContentDialogResult.Primary)
        {
            await AfficherModaleConnexionAsync(false);
        }
    }

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

    private static SystemControls.Separator ConstruireSeparateurBlocCompte()
    {
        return new SystemControls.Separator { Margin = new Thickness(0, 2, 0, 12), Opacity = 0.45 };
    }

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

    private static string FormaterNombre(int? valeur)
    {
        return valeur.HasValue ? valeur.Value.ToString(CultureInfo.CurrentCulture) : "Indisponible";
    }

    private static string ConstruireResumePositionCompte(ResumeUtilisateurRetroAchievements? resume)
    {
        if (resume is null || resume.Rang <= 0 || resume.TotalClasses <= 0)
        {
            return "Position : indisponible";
        }

        return $"Position : {resume.Rang.ToString(CultureInfo.CurrentCulture)} sur {resume.TotalClasses.ToString(CultureInfo.CurrentCulture)}";
    }

    private static string ConstruireUrlAvatar(ProfilUtilisateurRetroAchievements profil)
    {
        return ConstruireUrlImageRetroAchievements(profil.CheminAvatar);
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

    private string ObtenirTitreModaleCompte(ProfilUtilisateurRetroAchievements? profil)
    {
        if (!string.IsNullOrWhiteSpace(profil?.NomUtilisateur))
        {
            return profil.NomUtilisateur;
        }

        return ObtenirLibelleBoutonCompte();
    }

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
    }

    private string ObtenirLibelleBoutonCompte()
    {
        return string.IsNullOrWhiteSpace(_configurationConnexion.Pseudo)
            ? "Connexion"
            : _configurationConnexion.Pseudo;
    }

    private void DefinirEtatConnexion(string etatConnexion)
    {
        _etatConnexionCourant = etatConnexion;
        MettreAJourResumeConnexion();
    }

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
