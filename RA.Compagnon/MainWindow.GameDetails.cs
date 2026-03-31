using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using RA.Compagnon.Modeles.Api.V2.Game;
using RA.Compagnon.Modeles.Presentation;
using RA.Compagnon.Services;
using SystemControls = System.Windows.Controls;
using UiControls = Wpf.Ui.Controls;

namespace RA.Compagnon;

public partial class MainWindow
{
    private void ReinitialiserVueDetailleeJeuEnCours()
    {
        BoutonVueDetailleeJeuEnCours.Visibility = Visibility.Collapsed;
    }

    private void MettreAJourActionVueDetailleeJeuEnCours(GameInfoAndUserProgressV2 jeu)
    {
        BoutonVueDetailleeJeuEnCours.Visibility =
            jeu.Id > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void BoutonVueDetailleeJeuEnCours_Click(object sender, RoutedEventArgs e)
    {
        if (_dernieresDonneesJeuAffichees?.Jeu is not GameInfoAndUserProgressV2 jeu || jeu.Id <= 0)
        {
            return;
        }

        JeuAffiche jeuAffiche = ServicePresentationJeu.Construire(_dernieresDonneesJeuAffichees);
        await AfficherModaleVueDetailleeJeuAsync(jeu, jeuAffiche);
    }

    private async Task AfficherModaleVueDetailleeJeuAsync(
        GameInfoAndUserProgressV2 jeu,
        JeuAffiche jeuAffiche
    )
    {
        SystemControls.StackPanel contenu = new()
        {
            Width = 460,
            Margin = new Thickness(0),
            Children =
            {
                new SystemControls.TextBlock
                {
                    FontSize = 20,
                    FontWeight = FontWeights.SemiBold,
                    Text = jeu.Title,
                    TextWrapping = TextWrapping.Wrap,
                },
                new SystemControls.TextBlock
                {
                    Margin = new Thickness(0, 6, 0, 0),
                    Opacity = 0.76,
                    Text = jeuAffiche.Statut,
                    TextWrapping = TextWrapping.Wrap,
                },
                ConstruireGrilleVueDetailleeJeu(jeu, jeuAffiche),
            },
        };

        string dernierSucces = ConstruireTexteDernierSuccesJeu(jeu);

        if (!string.IsNullOrWhiteSpace(dernierSucces))
        {
            contenu.Children.Add(
                new SystemControls.TextBlock
                {
                    Margin = new Thickness(0, 14, 0, 0),
                    Opacity = 0.72,
                    Text = "Dernier succès obtenu",
                }
            );
            contenu.Children.Add(
                new SystemControls.TextBox
                {
                    Margin = new Thickness(0, 4, 0, 0),
                    Style = (Style)FindResource("StyleTexteCopiable"),
                    Text = dernierSucces,
                    TextWrapping = TextWrapping.Wrap,
                }
            );
        }

        UiControls.Button boutonPageJeu = new()
        {
            Content = "Ouvrir la page RetroAchievements",
            Appearance = UiControls.ControlAppearance.Secondary,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 14, 0, 0),
            Padding = new Thickness(14, 6, 14, 6),
        };
        boutonPageJeu.Click += (_, _) => OuvrirPageJeuRetroAchievements(jeu.Id);
        contenu.Children.Add(boutonPageJeu);

        SystemControls.Border conteneurContenu = new()
        {
            Padding = new Thickness(MargeInterieureModaleConnexion),
            HorizontalAlignment = HorizontalAlignment.Center,
            CornerRadius = ObtenirRayonCoins("RayonCoinsStandard", 12),
            Child = contenu,
        };

        UiControls.ContentDialog dialogue = new(RacineModales)
        {
            Title = "Détails du jeu",
            Content = conteneurContenu,
            MinWidth = 460 + (MargeInterieureModaleConnexion * 2),
            CloseButtonText = "Fermer",
            DefaultButton = UiControls.ContentDialogButton.Close,
        };

        try
        {
            DefinirEtatModalesActif(true);
            await dialogue.ShowAsync();
        }
        finally
        {
            DefinirEtatModalesActif(false);
        }
    }

    private SystemControls.Grid ConstruireGrilleVueDetailleeJeu(
        GameInfoAndUserProgressV2 jeu,
        JeuAffiche jeuAffiche
    )
    {
        (string Libelle, string Valeur)[] lignes =
        [
            ("Game ID", jeu.Id.ToString(CultureInfo.CurrentCulture)),
            ("Progression", jeuAffiche.PourcentageTexte),
            (
                "Succès",
                $"{jeu.NumAwardedToUser.ToString(CultureInfo.CurrentCulture)} / {jeu.NumAchievements.ToString(CultureInfo.CurrentCulture)}"
            ),
            (
                "Hardcore",
                $"{jeu.NumAwardedToUserHardcore.ToString(CultureInfo.CurrentCulture)} / {jeu.NumAchievements.ToString(CultureInfo.CurrentCulture)}"
            ),
            ("Points", ConstruireTextePointsDetailJeu(jeu)),
            (
                "Joueurs",
                jeu.NumDistinctPlayers > 0
                    ? jeu.NumDistinctPlayers.ToString(CultureInfo.CurrentCulture)
                    : "Inconnu"
            ),
            ("Récompense", ConstruireTexteRecompenseJeu(jeu)),
            ("Résumé", jeuAffiche.ResumeProgression),
        ];

        SystemControls.Grid grille = new() { Margin = new Thickness(0, 14, 0, 0) };
        grille.ColumnDefinitions.Add(
            new SystemControls.ColumnDefinition { Width = new GridLength(150) }
        );
        grille.ColumnDefinitions.Add(
            new SystemControls.ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
        );

        for (int index = 0; index < lignes.Length; index++)
        {
            grille.RowDefinitions.Add(
                new SystemControls.RowDefinition { Height = GridLength.Auto }
            );

            SystemControls.TextBlock libelle = new()
            {
                Margin = new Thickness(0, 0, 12, 8),
                FontWeight = FontWeights.SemiBold,
                Opacity = 0.72,
                Text = lignes[index].Libelle,
                TextWrapping = TextWrapping.Wrap,
            };
            SystemControls.Grid.SetRow(libelle, index);
            SystemControls.Grid.SetColumn(libelle, 0);
            grille.Children.Add(libelle);

            SystemControls.TextBox valeur = new()
            {
                Margin = new Thickness(0, 0, 0, 8),
                Style = (Style)FindResource("StyleTexteCopiable"),
                Text = lignes[index].Valeur,
                TextWrapping = TextWrapping.Wrap,
            };
            SystemControls.Grid.SetRow(valeur, index);
            SystemControls.Grid.SetColumn(valeur, 1);
            grille.Children.Add(valeur);
        }

        return grille;
    }

    private static string ConstruireTextePointsDetailJeu(GameInfoAndUserProgressV2 jeu)
    {
        int pointsTotaux = jeu.Succes.Values.Sum(item => Math.Max(0, item.Points));
        int pointsObtenus = jeu
            .Succes.Values.Where(SuccesEstDebloquePourDetail)
            .Sum(item => Math.Max(0, item.Points));

        if (pointsTotaux <= 0)
        {
            return "Inconnu";
        }

        return $"{pointsObtenus.ToString(CultureInfo.CurrentCulture)} / {pointsTotaux.ToString(CultureInfo.CurrentCulture)}";
    }

    private static bool SuccesEstDebloquePourDetail(GameAchievementV2 succes)
    {
        return !string.IsNullOrWhiteSpace(succes.DateEarned)
            || !string.IsNullOrWhiteSpace(succes.DateEarnedHardcore);
    }

    private static string ConstruireTexteRecompenseJeu(GameInfoAndUserProgressV2 jeu)
    {
        string recompense = jeu.HighestAwardKind.Trim().ToLowerInvariant();

        return recompense switch
        {
            "mastered" => "Jeu maîtrisé",
            "completed" => "Jeu complété",
            "beaten" => "Jeu battu",
            "beaten-hardcore" => "Jeu battu en hardcore",
            "beaten-softcore" => "Jeu battu en softcore",
            _ => "Aucune récompense spéciale",
        };
    }

    private static string ConstruireTexteDernierSuccesJeu(GameInfoAndUserProgressV2 jeu)
    {
        GameAchievementV2? dernierSucces = jeu
            .Succes.Values.Select(item => new
            {
                Succes = item,
                Date = ExtraireDateDernierDeblocage(item),
            })
            .Where(item => item.Date is not null)
            .OrderByDescending(item => item.Date)
            .Select(item => item.Succes)
            .FirstOrDefault();

        if (dernierSucces is null)
        {
            return string.Empty;
        }

        string mode = !string.IsNullOrWhiteSpace(dernierSucces.DateEarnedHardcore)
            ? "Hardcore"
            : "Softcore";
        DateTimeOffset? date = ExtraireDateDernierDeblocage(dernierSucces);
        string dateTexte = date is null
            ? string.Empty
            : date.Value.ToLocalTime().ToString("d MMM yyyy HH:mm", CultureInfo.CurrentCulture);

        return string.IsNullOrWhiteSpace(dateTexte)
            ? $"{dernierSucces.Title} - {mode}"
            : $"{dernierSucces.Title} - {mode} - {dateTexte}";
    }

    private static DateTimeOffset? ExtraireDateDernierDeblocage(GameAchievementV2 succes)
    {
        string valeur = !string.IsNullOrWhiteSpace(succes.DateEarnedHardcore)
            ? succes.DateEarnedHardcore
            : succes.DateEarned;

        if (string.IsNullOrWhiteSpace(valeur))
        {
            return null;
        }

        if (
            DateTimeOffset.TryParse(
                valeur,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal,
                out DateTimeOffset dateUtc
            )
        )
        {
            return dateUtc;
        }

        if (
            DateTimeOffset.TryParse(
                valeur,
                CultureInfo.CurrentCulture,
                DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal,
                out dateUtc
            )
        )
        {
            return dateUtc;
        }

        return null;
    }

    private static string ConstruireUrlJeuRetroAchievements(int identifiantJeu)
    {
        return $"https://retroachievements.org/game/{identifiantJeu.ToString(CultureInfo.InvariantCulture)}";
    }

    private static void OuvrirPageJeuRetroAchievements(int identifiantJeu)
    {
        if (identifiantJeu <= 0)
        {
            return;
        }

        try
        {
            Process.Start(
                new ProcessStartInfo
                {
                    FileName = ConstruireUrlJeuRetroAchievements(identifiantJeu),
                    UseShellExecute = true,
                }
            );
        }
        catch
        {
            // L'ouverture du navigateur reste optionnelle.
        }
    }
}
