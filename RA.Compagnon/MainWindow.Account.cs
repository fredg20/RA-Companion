using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using RA.Compagnon.Modeles.Api;
using RA.Compagnon.Modeles.Api.V2.User;
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
                PlaceholderText = "Cl� API",
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
                            "Entre ton pseudo et ta cl� Web API pour synchroniser ton dernier jeu jou�, ta progression et tes succ�s r�cents.",
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
                            "Tu peux retrouver cette cl� depuis ton compte RetroAchievements, dans la section d�di�e � l'API Web.",
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
                texteErreur.Text = "Renseigne ton pseudo et ta cl� Web API pour continuer.";
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
                string messageErreur = string.IsNullOrWhiteSpace(exception.Message)
                    ? "Impossible de v�rifier ce compte pour le moment. V�rifie ta connexion et r�essaie."
                    : $"Impossible de v�rifier ce compte pour le moment. {exception.Message}";
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
        UserProfileV2? profil = await ObtenirProfilUtilisateurPourModaleAsync();
        UserSummaryV2? resume = await ObtenirResumeUtilisateurPourModaleAsync();
        SystemControls.StackPanel contenu = new()
        {
            Width = 460,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0),
        };
        contenu.Children.Add(ConstruireEnTeteAvatarCompte(profil));
        contenu.Children.Add(
            new SystemControls.TextBlock
            {
                Margin = new Thickness(0, 12, 0, 0),
                FontSize = 22,
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                Text = ObtenirTitreCompteAffiche(profil),
                TextWrapping = TextWrapping.Wrap,
            }
        );
        if (!string.IsNullOrWhiteSpace(profil?.Motto))
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
                    Text = profil.Motto,
                    TextWrapping = TextWrapping.Wrap,
                }
            );
        }
        contenu.Children.Add(
            new SystemControls.TextBlock
            {
                Margin = new Thickness(0, 12, 0, 12),
                Opacity = 0.82,
                Text =
                    "Retrouve ici les informations principales de ton compte et g�re ta connexion en toute simplicit�.",
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
            }
        );
        contenu.Children.Add(
            ConstruireBlocCompte("Compte", ConstruireLignesCompte(profil, resume))
        );
        contenu.Children.Add(ConstruireSeparateurBlocCompte());
        contenu.Children.Add(
            ConstruireBlocCompte("Progression", ConstruireLignesProgressionCompte(profil, resume))
        );
        IReadOnlyList<(string Libelle, string Valeur)> lignesActivite =
            ConstruireLignesActiviteCompte(profil, resume);

        if (lignesActivite.Count > 0)
        {
            contenu.Children.Add(ConstruireSeparateurBlocCompte());
            contenu.Children.Add(ConstruireBlocCompte("Activit�", lignesActivite));
        }

        if (resume is not null && resume.RecentlyPlayed.Count > 0)
        {
            contenu.Children.Add(ConstruireSeparateurBlocCompte());
            contenu.Children.Add(
                ConstruireBlocJeuxRecemmentJoues(resume.RecentlyPlayed.Take(3).ToList())
            );
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
            OuvrirProfilRetroAchievements(profil?.User ?? _configurationConnexion.Pseudo);
        }
    }

    private async Task<UserSummaryV2?> ObtenirResumeUtilisateurPourModaleAsync()
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

    private async Task<UserProfileV2?> ObtenirProfilUtilisateurPourModaleAsync()
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

    private FrameworkElement ConstruireEnTeteAvatarCompte(UserProfileV2? profil)
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
            profil,
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
                Text = string.IsNullOrWhiteSpace(_configurationConnexion.Pseudo)
                    ? "?"
                    : _configurationConnexion.Pseudo[..1].ToUpperInvariant(),
            },
        };
    }

    private IReadOnlyList<(string Libelle, string Valeur)> ConstruireLignesCompte(
        UserProfileV2? profil,
        UserSummaryV2? resume
    )
    {
        List<(string Libelle, string Valeur)> lignes = [];

        AjouterLigneSiValeurUtile(lignes, "Membre depuis", FormaterDateProfil(profil?.MemberSince));
        AjouterLigneSiValeurUtile(
            lignes,
            "Dernier jeu jou�",
            DeterminerDernierJeuAffiche(profil, resume)
        );

        return lignes;
    }

    private static IReadOnlyList<(string Libelle, string Valeur)> ConstruireLignesActiviteCompte(
        UserProfileV2? profil,
        UserSummaryV2? resume
    )
    {
        List<(string Libelle, string Valeur)> lignes = [];

        AjouterLigneSiValeurUtile(lignes, "Rich Presence", profil?.RichPresenceMsg);
        AjouterLigneSiValeurUtile(lignes, "Succ�s r�cents", FormaterResumeSuccesRecents(resume));

        return lignes;
    }

    private static IReadOnlyList<(string Libelle, string Valeur)> ConstruireLignesProgressionCompte(
        UserProfileV2? profil,
        UserSummaryV2? resume
    )
    {
        return
        [
            ("Points", FormaterNombre(resume?.TotalPoints ?? profil?.TotalPoints)),
            ("TruePoints", FormaterNombre(resume?.TotalTruePoints ?? profil?.TotalTruePoints)),
            (
                "Classement",
                ConstruireResumePositionCompte(resume).Replace("Position : ", string.Empty)
            ),
        ];
    }

    private static SystemControls.Separator ConstruireSeparateurBlocCompte()
    {
        return new SystemControls.Separator { Margin = new Thickness(0, 2, 0, 12), Opacity = 0.45 };
    }

    private SystemControls.Border ConstruireBlocJeuxRecemmentJoues(
        IReadOnlyList<RecentlyPlayedGameV2> jeux
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
                    Text = "Jeux r�cemment jou�s",
                },
            },
        };

        foreach (RecentlyPlayedGameV2 jeu in jeux)
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
                                Text = ConstruireSousTitreJeuRecent(jeu),
                                TextWrapping = TextWrapping.Wrap,
                            },
                        },
                    },
                }
            );
        }

        return new SystemControls.Border { Padding = new Thickness(0), Child = pile };
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

    private static string ConstruireResumePositionCompte(UserSummaryV2? resume)
    {
        if (resume is null || resume.Rank <= 0 || resume.TotalRanked <= 0)
        {
            return "Position : indisponible";
        }

        return $"Position : {resume.Rank.ToString(CultureInfo.CurrentCulture)} sur {resume.TotalRanked.ToString(CultureInfo.CurrentCulture)}";
    }

    private string ObtenirTitreCompteAffiche(UserProfileV2? profil)
    {
        if (!string.IsNullOrWhiteSpace(profil?.User))
        {
            return profil.User;
        }

        return ObtenirLibelleBoutonCompte();
    }

    private static string FormaterDerniereActivite(UserSummaryV2? resume)
    {
        UserLastActivityV2? activite = resume?.LastActivity;

        if (activite is null)
        {
            return "Indisponible";
        }

        if (activite.Horodatage is long horodatage && horodatage > 0)
        {
            return DateTimeOffset
                .FromUnixTimeSeconds(horodatage)
                .ToLocalTime()
                .ToString("g", CultureInfo.GetCultureInfo("fr-CA"));
        }

        if (!string.IsNullOrWhiteSpace(activite.DerniereMiseAJour))
        {
            if (
                DateTimeOffset.TryParse(
                    activite.DerniereMiseAJour,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal,
                    out DateTimeOffset date
                )
            )
            {
                return date.ToLocalTime().ToString("g", CultureInfo.GetCultureInfo("fr-CA"));
            }

            return activite.DerniereMiseAJour;
        }

        return "Indisponible";
    }

    private static string FormaterResumeSuccesRecents(UserSummaryV2? resume)
    {
        if (resume is null)
        {
            return string.Empty;
        }

        int nombreSucces = resume.RecentAchievements.Sum(item => item.Value?.Count ?? 0);

        if (nombreSucces <= 0)
        {
            return string.Empty;
        }

        if (nombreSucces == 1)
        {
            return "1 succ�s r�cent";
        }

        return $"{nombreSucces.ToString(CultureInfo.CurrentCulture)} succ�s r�cents";
    }

    private static string DeterminerDernierJeuAffiche(UserProfileV2? profil, UserSummaryV2? resume)
    {
        if (!string.IsNullOrWhiteSpace(resume?.LastGame?.Titre))
        {
            return resume.LastGame.Titre;
        }

        if (!string.IsNullOrWhiteSpace(profil?.LastGame))
        {
            return profil.LastGame;
        }

        return string.Empty;
    }

    private static string ConstruireSousTitreJeuRecent(RecentlyPlayedGameV2 jeu)
    {
        List<string> segments = [];

        if (!string.IsNullOrWhiteSpace(jeu.NomConsole))
        {
            segments.Add(jeu.NomConsole);
        }

        if (!string.IsNullOrWhiteSpace(jeu.DernierePartie))
        {
            segments.Add(jeu.DernierePartie);
        }

        return segments.Count == 0 ? "Activit� r�cente" : string.Join(" � ", segments);
    }

    private static void AjouterLigneSiValeurUtile(
        ICollection<(string Libelle, string Valeur)> lignes,
        string libelle,
        string? valeur
    )
    {
        if (string.IsNullOrWhiteSpace(valeur))
        {
            return;
        }

        string valeurNettoyee = valeur.Trim();

        if (
            valeurNettoyee.Equals("Indisponible", StringComparison.OrdinalIgnoreCase)
            || valeurNettoyee.Equals(
                "Informations indisponibles",
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            return;
        }

        lignes.Add((libelle, valeurNettoyee));
    }

    private static string ConstruireUrlAvatar(UserProfileV2 profil)
    {
        return ConstruireUrlImageRetroAchievements(profil.UserPic);
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
        UserProfileV2? profil,
        double largeur = 44,
        double hauteur = 44,
        Thickness? marge = null
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
            _etatConnexionCourant = "Non configur�";
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
