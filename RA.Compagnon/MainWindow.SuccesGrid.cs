using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RA.Compagnon.Modeles.Api.V2.Game;
using RA.Compagnon.Modeles.Local;
using RA.Compagnon.Modeles.Presentation;
using RA.Compagnon.Services;
using SystemControls = System.Windows.Controls;

namespace RA.Compagnon;

/// <summary>
/// Regroupe la logique de la grille des rétrosuccès affichée dans la carte du jeu courant.
/// </summary>
public partial class MainWindow
{
    /// <summary>
    /// Ouvre le menu de choix de l'ordre d'affichage de la grille.
    /// </summary>
    private void BoutonOrdreSuccesGrille_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { ContextMenu: { } menu })
        {
            return;
        }

        menu.PlacementTarget = sender as FrameworkElement;
        menu.IsOpen = !menu.IsOpen;
    }

    /// <summary>
    /// Applique l'ordre normal correspondant à la page web du jeu.
    /// </summary>
    private async void OrdreSuccesGrilleNormal_Click(object sender, RoutedEventArgs e)
    {
        await ChangerOrdreSuccesGrilleAsync(OrdreSuccesGrille.Normal);
    }

    private async void OrdreSuccesGrilleAleatoire_Click(object sender, RoutedEventArgs e)
    {
        await ChangerOrdreSuccesGrilleAsync(OrdreSuccesGrille.Aleatoire);
    }

    private async void OrdreSuccesGrilleFacile_Click(object sender, RoutedEventArgs e)
    {
        await ChangerOrdreSuccesGrilleAsync(OrdreSuccesGrille.Facile);
    }

    private async void OrdreSuccesGrilleDifficile_Click(object sender, RoutedEventArgs e)
    {
        await ChangerOrdreSuccesGrilleAsync(OrdreSuccesGrille.Difficile);
    }

    /// <summary>
    /// Met à jour l'ordre d'affichage des badges et recharge la grille si nécessaire.
    /// </summary>
    private async Task ChangerOrdreSuccesGrilleAsync(OrdreSuccesGrille nouvelOrdre)
    {
        if (_ordreSuccesGrilleCourant == nouvelOrdre)
        {
            MettreAJourLibelleOrdreSuccesGrilleEtModes();
            return;
        }

        _ordreSuccesGrilleCourant = nouvelOrdre;

        if (nouvelOrdre == OrdreSuccesGrille.Aleatoire)
        {
            InvaliderOrdreAleatoireSuccesGrille();
        }

        MettreAJourLibelleOrdreSuccesGrilleEtModes();

        if (_identifiantJeuSuccesCourant <= 0 || _succesJeuCourant.Count == 0)
        {
            return;
        }

        await MettreAJourGrilleTousSuccesAsync(_identifiantJeuSuccesCourant, _succesJeuCourant);

        if (
            !_identifiantSuccesGrilleTemporaire.HasValue
            && !_identifiantSuccesGrilleEpingle.HasValue
        )
        {
            await MettreAJourPremierSuccesNonDebloqueAsync(
                _identifiantJeuSuccesCourant,
                _succesJeuCourant
            );
        }
    }

    private void InvaliderOrdreAleatoireSuccesGrille()
    {
        _identifiantJeuOrdreAleatoireSuccesGrille = 0;
        _signatureOrdreAleatoireSuccesGrille = string.Empty;
        _positionsAleatoiresSuccesGrille.Clear();
    }

    private void MettreAJourLibelleOrdreSuccesGrilleEtModes()
    {
        string libelle = _ordreSuccesGrilleCourant switch
        {
            OrdreSuccesGrille.Aleatoire => "Aléatoire",
            OrdreSuccesGrille.Facile => "Facile",
            OrdreSuccesGrille.Difficile => "Difficile",
            _ => "Normal",
        };

        if (BoutonOrdreSuccesGrille is not null)
        {
            BoutonOrdreSuccesGrille.Content = libelle;
        }

        Brush contourActif = new SolidColorBrush(Color.FromRgb(120, 200, 255));
        Brush contourInactif = new SolidColorBrush(Color.FromArgb(140, 255, 255, 255));
        Brush centreActif = new SolidColorBrush(Color.FromRgb(120, 200, 255));

        bool modeNormal = _ordreSuccesGrilleCourant == OrdreSuccesGrille.Normal;
        bool modeAleatoire = _ordreSuccesGrilleCourant == OrdreSuccesGrille.Aleatoire;
        bool modeFacile = _ordreSuccesGrilleCourant == OrdreSuccesGrille.Facile;
        bool modeDifficile = _ordreSuccesGrilleCourant == OrdreSuccesGrille.Difficile;

        if (ContourOrdreSuccesNormal is not null)
        {
            ContourOrdreSuccesNormal.Stroke = modeNormal ? contourActif : contourInactif;
            CentreOrdreSuccesNormal.Fill = centreActif;
            CentreOrdreSuccesNormal.Visibility = modeNormal
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        if (ContourOrdreSuccesAleatoire is not null)
        {
            ContourOrdreSuccesAleatoire.Stroke = modeAleatoire ? contourActif : contourInactif;
            CentreOrdreSuccesAleatoire.Fill = centreActif;
            CentreOrdreSuccesAleatoire.Visibility = modeAleatoire
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        if (ContourOrdreSuccesFacile is not null)
        {
            ContourOrdreSuccesFacile.Stroke = modeFacile ? contourActif : contourInactif;
            CentreOrdreSuccesFacile.Fill = centreActif;
            CentreOrdreSuccesFacile.Visibility = modeFacile
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        if (ContourOrdreSuccesDifficile is not null)
        {
            ContourOrdreSuccesDifficile.Stroke = modeDifficile ? contourActif : contourInactif;
            CentreOrdreSuccesDifficile.Fill = centreActif;
            CentreOrdreSuccesDifficile.Visibility = modeDifficile
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
    }

    private List<GameAchievementV2> OrdonnerSuccesPourGrilleSelonMode(
        int identifiantJeu,
        IEnumerable<GameAchievementV2> succes
    )
    {
        return _ordreSuccesGrilleCourant switch
        {
            OrdreSuccesGrille.Aleatoire => OrdonnerSuccesPourGrilleAleatoire(
                identifiantJeu,
                succes
            ),
            OrdreSuccesGrille.Facile => OrdonnerSuccesPourGrilleParTrueRatio(
                succes,
                ordreCroissant: true
            ),
            OrdreSuccesGrille.Difficile => OrdonnerSuccesPourGrilleParTrueRatio(
                succes,
                ordreCroissant: false
            ),
            _ =>
            [
                .. succes
                    .OrderBy(item => SuccesEstDebloque(item) ? 1 : 0)
                    .ThenBy(item => item.DisplayOrder)
                    .ThenBy(item => item.Id),
            ],
        };
    }

    private static List<GameAchievementV2> OrdonnerSuccesPourGrilleParTrueRatio(
        IEnumerable<GameAchievementV2> succes,
        bool ordreCroissant
    )
    {
        IOrderedEnumerable<GameAchievementV2> ordreInitial = ordreCroissant
            ? succes.OrderBy(item => SuccesEstDebloque(item) ? 1 : 0).ThenBy(item => item.TrueRatio)
            : succes
                .OrderBy(item => SuccesEstDebloque(item) ? 1 : 0)
                .ThenByDescending(item => item.TrueRatio);

        return [.. ordreInitial.ThenBy(item => item.DisplayOrder).ThenBy(item => item.Id)];
    }

    private void MettreAJourOrdreAleatoireSuccesGrilleSiNecessaire(
        int identifiantJeu,
        IEnumerable<GameAchievementV2> succes
    )
    {
        List<int> succesNonDebloques =
        [
            .. succes
                .Where(item => !SuccesEstDebloque(item))
                .Select(item => item.Id)
                .OrderBy(item => item),
        ];
        string signature = $"{identifiantJeu}:{string.Join(',', succesNonDebloques)}";

        if (
            _identifiantJeuOrdreAleatoireSuccesGrille == identifiantJeu
            && string.Equals(
                _signatureOrdreAleatoireSuccesGrille,
                signature,
                StringComparison.Ordinal
            )
            && succesNonDebloques.All(item => _positionsAleatoiresSuccesGrille.ContainsKey(item))
        )
        {
            return;
        }

        List<int> succesMelanges = [.. succesNonDebloques];

        for (int index = succesMelanges.Count - 1; index > 0; index--)
        {
            int indexPermutation = _generateurAleatoireSuccesGrille.Next(index + 1);
            (succesMelanges[index], succesMelanges[indexPermutation]) = (
                succesMelanges[indexPermutation],
                succesMelanges[index]
            );
        }

        _positionsAleatoiresSuccesGrille = succesMelanges
            .Select((identifiantSucces, index) => new { identifiantSucces, index })
            .ToDictionary(item => item.identifiantSucces, item => item.index);
        _identifiantJeuOrdreAleatoireSuccesGrille = identifiantJeu;
        _signatureOrdreAleatoireSuccesGrille = signature;
    }

    private List<GameAchievementV2> OrdonnerSuccesPourGrilleAleatoire(
        int identifiantJeu,
        IEnumerable<GameAchievementV2> succes
    )
    {
        List<GameAchievementV2> succesListe = [.. succes];
        MettreAJourOrdreAleatoireSuccesGrilleSiNecessaire(identifiantJeu, succesListe);

        return
        [
            .. succesListe
                .OrderBy(item => SuccesEstDebloque(item) ? 1 : 0)
                .ThenBy(item =>
                    SuccesEstDebloque(item)
                        ? int.MaxValue
                        : _positionsAleatoiresSuccesGrille.GetValueOrDefault(
                            item.Id,
                            int.MaxValue - 1
                        )
                )
                .ThenBy(item => item.DisplayOrder)
                .ThenBy(item => item.Id),
        ];
    }

    /// <summary>
    /// Remplit la grille de tous les succès avec leurs badges.
    /// </summary>
    private async Task MettreAJourGrilleTousSuccesAsync(
        int identifiantJeu,
        List<GameAchievementV2> succes
    )
    {
        if (_identifiantJeuSuccesCourant != identifiantJeu)
        {
            return;
        }

        JournaliserDiagnosticChangementJeu(
            "grille_debut",
            $"jeu={identifiantJeu};succes={succes.Count}"
        );
        GrilleTousSuccesJeuEnCours.Children.Clear();
        List<GameAchievementV2> succesOrdonnes = OrdonnerSuccesPourGrilleSelonMode(
            identifiantJeu,
            succes
        );

        if (succesOrdonnes.Count == 0)
        {
            SauvegarderDerniereListeSuccesAffichee(identifiantJeu, []);
            PlanifierMiseAJourAnimationGrilleTousSucces();
            TerminerDiagnosticChangementJeu("grille_vide");
            return;
        }

        var badgesCharges = await Task.WhenAll(
            succesOrdonnes.Select(async succesJeu =>
            {
                SuccesGrilleAffiche succesAffiche = ServicePresentationSucces.ConstruirePourGrille(
                    succesJeu
                );

                return new
                {
                    Badge = await ConstruireBadgeGrilleSuccesAsync(identifiantJeu, succesAffiche),
                    Etat = new ElementListeSuccesAfficheLocal
                    {
                        IdentifiantSucces = succesJeu.Id,
                        Titre = succesAffiche.Titre,
                        CheminImageBadge = succesAffiche.UrlBadge,
                    },
                };
            })
        );

        if (_identifiantJeuSuccesCourant != identifiantJeu)
        {
            return;
        }

        foreach (var badgeCharge in badgesCharges)
        {
            GrilleTousSuccesJeuEnCours.Children.Add(badgeCharge.Badge);
        }

        SauvegarderDerniereListeSuccesAffichee(
            identifiantJeu,
            [.. badgesCharges.Select(item => item.Etat)]
        );
        RafraichirStyleBadgesGrilleSucces();
        MettreAJourDispositionGrilleTousSucces();
        PlanifierMiseAJourAnimationGrilleTousSucces();
        TerminerDiagnosticChangementJeu("grille_fin", $"badges={badgesCharges.Length}");
    }

    /// <summary>
    /// Construit un badge de la grille des rétrosuccès à partir de son titre et de son visuel.
    /// </summary>
    private async Task<SystemControls.Border> ConstruireBadgeGrilleSuccesAsync(
        int identifiantJeu,
        SuccesGrilleAffiche succesAffiche
    )
    {
        ImageSource? imageBadge = await ChargerImageDistanteAsync(succesAffiche.UrlBadge);
        SystemControls.Border conteneur = new()
        {
            Width = TailleBadgeGrilleSucces,
            Height = TailleBadgeGrilleSucces,
            Margin = new Thickness(0),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(0),
            Cursor = Cursors.Hand,
            ToolTip = succesAffiche.Titre,
            Tag = new BadgeSuccesGrilleContexte(
                identifiantJeu,
                succesAffiche.IdentifiantSucces,
                succesAffiche.UrlBadge
            ),
        };
        conteneur.MouseEnter += BadgeGrilleSucces_EntreeSouris;
        conteneur.MouseLeave += BadgeGrilleSucces_SortieSouris;
        conteneur.MouseLeftButtonUp += BadgeGrilleSucces_ClicGauche;
        conteneur.MouseRightButtonUp += BadgeGrilleSucces_ClicDroit;

        if (imageBadge is null)
        {
            conteneur.Child = new SystemControls.TextBlock
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Opacity = 0.62,
                Text = succesAffiche.Titre,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
            };
            return conteneur;
        }

        SystemControls.Image imageSucces = new()
        {
            Source = succesAffiche.EstDebloque
                ? imageBadge
                : ConvertirImageEnNoirEtBlanc(imageBadge),
            Width = 34,
            Height = 34,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = succesAffiche.EstDebloque ? 1 : 0.58,
            Stretch = Stretch.Uniform,
        };

        imageSucces.Loaded += (_, _) => AppliquerCoinsArrondisImage(imageSucces);
        imageSucces.SizeChanged += (_, _) => AppliquerCoinsArrondisImage(imageSucces);
        conteneur.Child = imageSucces;
        AppliquerStyleBadgeEpingle(conteneur);
        return conteneur;
    }

    /// <summary>
    /// Applique un style visuel discret au badge épinglé ou sélectionné temporairement.
    /// </summary>
    private void AppliquerStyleBadgeEpingle(SystemControls.Border badge)
    {
        if (badge.Tag is not BadgeSuccesGrilleContexte contexte)
        {
            return;
        }

        bool estEpingle =
            _identifiantSuccesGrilleEpingle.HasValue
            && contexte.IdentifiantJeu == _identifiantJeuSuccesCourant
            && contexte.Id == _identifiantSuccesGrilleEpingle.Value;
        bool estTemporaire =
            _identifiantSuccesGrilleTemporaire.HasValue
            && contexte.IdentifiantJeu == _identifiantJeuSuccesCourant
            && contexte.Id == _identifiantSuccesGrilleTemporaire.Value;

        if (estEpingle)
        {
            badge.BorderBrush = new SolidColorBrush(Color.FromRgb(255, 196, 64));
            badge.BorderThickness = new Thickness(2);
            badge.Background = new SolidColorBrush(Color.FromArgb(34, 255, 196, 64));
            return;
        }

        if (estTemporaire)
        {
            badge.BorderBrush = new SolidColorBrush(Color.FromRgb(120, 200, 255));
            badge.BorderThickness = new Thickness(2);
            badge.Background = new SolidColorBrush(Color.FromArgb(32, 120, 200, 255));
            return;
        }

        badge.BorderBrush = Brushes.Transparent;
        badge.BorderThickness = new Thickness(0);
        badge.Background = Brushes.Transparent;
    }

    /// <summary>
    /// Réapplique l'indice visuel d'épingle sur tous les badges visibles.
    /// </summary>
    private void RafraichirStyleBadgesGrilleSucces()
    {
        foreach (object enfant in GrilleTousSuccesJeuEnCours.Children)
        {
            if (enfant is SystemControls.Border badge)
            {
                AppliquerStyleBadgeEpingle(badge);
            }
        }
    }

    /// <summary>
    /// Épingle durablement un succès de la grille dans la carte principale.
    /// </summary>
    private async void BadgeGrilleSucces_ClicGauche(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        await AfficherSuccesGrilleSelectionneAsync(sender, permanent: true);
    }

    /// <summary>
    /// Affiche temporairement un succès de la grille dans la carte principale.
    /// </summary>
    private async void BadgeGrilleSucces_ClicDroit(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        await AfficherSuccesGrilleSelectionneAsync(sender, permanent: false);
    }

    /// <summary>
    /// Affiche un succès de la grille dans la carte principale en mode temporaire ou épinglé.
    /// </summary>
    private async Task AfficherSuccesGrilleSelectionneAsync(object sender, bool permanent)
    {
        if (
            sender is not SystemControls.Border { Tag: BadgeSuccesGrilleContexte contexte }
            || contexte.IdentifiantJeu != _identifiantJeuSuccesCourant
        )
        {
            return;
        }

        GameAchievementV2? succes = _succesJeuCourant.FirstOrDefault(item =>
            item.Id == contexte.Id
        );

        if (succes is null)
        {
            return;
        }

        bool succesDebloque = SuccesEstDebloque(succes);
        bool affichagePermanent = permanent && !succesDebloque;

        if (affichagePermanent)
        {
            _identifiantSuccesGrilleEpingle = succes.Id;
            _identifiantSuccesGrilleTemporaire = null;
            _retourPremierSuccesNonDebloqueApresSelectionTemporaire = false;
            _minuteurAffichageTemporaireSuccesGrille.Stop();
        }
        else
        {
            if (succesDebloque)
            {
                _identifiantSuccesGrilleEpingle = null;
                _retourPremierSuccesNonDebloqueApresSelectionTemporaire = true;
            }
            else
            {
                _retourPremierSuccesNonDebloqueApresSelectionTemporaire = false;
            }

            _identifiantSuccesGrilleTemporaire = succes.Id;
            _minuteurAffichageTemporaireSuccesGrille.Stop();
            _minuteurAffichageTemporaireSuccesGrille.Start();
        }

        RafraichirStyleBadgesGrilleSucces();
        await AppliquerSuccesEnCoursAsync(
            _identifiantJeuSuccesCourant,
            succes,
            affichagePermanent,
            affichagePermanent
        );
    }

    /// <summary>
    /// Termine l'affichage temporaire d'un succès sélectionné dans la grille.
    /// </summary>
    private async void MinuteurAffichageTemporaireSuccesGrille_Tick(object? sender, EventArgs e)
    {
        _minuteurAffichageTemporaireSuccesGrille.Stop();
        _identifiantSuccesGrilleTemporaire = null;

        if (_identifiantJeuSuccesCourant <= 0 || _succesJeuCourant.Count == 0)
        {
            return;
        }

        RafraichirStyleBadgesGrilleSucces();

        if (_retourPremierSuccesNonDebloqueApresSelectionTemporaire)
        {
            _retourPremierSuccesNonDebloqueApresSelectionTemporaire = false;

            GameAchievementV2? premierSuccesNonDebloque = _succesJeuCourant
                .Where(item => !SuccesEstDebloque(item))
                .OrderBy(item => item.DisplayOrder)
                .ThenBy(item => item.Id)
                .FirstOrDefault();

            await AppliquerSuccesEnCoursAsync(
                _identifiantJeuSuccesCourant,
                premierSuccesNonDebloque,
                true,
                false
            );
            return;
        }

        await MettreAJourPremierSuccesNonDebloqueAsync(
            _identifiantJeuSuccesCourant,
            _succesJeuCourant
        );
    }

    /// <summary>
    /// Indique si un succès du jeu a déjà été obtenu par l'utilisateur.
    /// </summary>
    private static bool SuccesEstDebloque(GameAchievementV2 succes)
    {
        return !string.IsNullOrWhiteSpace(succes.DateEarned)
            || !string.IsNullOrWhiteSpace(succes.DateEarnedHardcore);
    }

    /// <summary>
    /// Recalcule le gap de la grille des succès selon la largeur disponible.
    /// </summary>
    private void ConteneurGrilleTousSuccesJeuEnCours_TailleChangee(
        object sender,
        SizeChangedEventArgs e
    )
    {
        MettreAJourDispositionGrilleTousSucces();
        PlanifierMiseAJourAnimationGrilleTousSucces();
    }

    /// <summary>
    /// Répartit les badges sur la largeur disponible avec un espacement adaptatif.
    /// </summary>
    private void MettreAJourDispositionGrilleTousSucces()
    {
        if (
            ConteneurGrilleTousSuccesJeuEnCours is null
            || GrilleTousSuccesJeuEnCours is null
            || GrilleTousSuccesJeuEnCours.Children.Count == 0
        )
        {
            return;
        }

        double largeurDisponible = ConteneurGrilleTousSuccesJeuEnCours.ActualWidth;

        if (largeurDisponible <= 0)
        {
            return;
        }

        int nombreBadges = GrilleTousSuccesJeuEnCours.Children.Count;
        int colonnes = Math.Max(
            1,
            (int)
                Math.Floor(
                    (largeurDisponible + EspaceMinimalGrilleSucces)
                        / (TailleBadgeGrilleSucces + EspaceMinimalGrilleSucces)
                )
        );
        colonnes = Math.Min(colonnes, nombreBadges);

        double gapHorizontal =
            colonnes > 1
                ? Math.Max(
                    EspaceMinimalGrilleSucces,
                    (largeurDisponible - (colonnes * TailleBadgeGrilleSucces)) / (colonnes - 1)
                )
                : 0;

        for (int index = 0; index < nombreBadges; index++)
        {
            if (GrilleTousSuccesJeuEnCours.Children[index] is not SystemControls.Border badge)
            {
                continue;
            }

            int colonne = index % colonnes;
            bool dernierDeLigne = colonne == colonnes - 1;
            badge.Margin = new Thickness(
                0,
                0,
                dernierDeLigne ? 0 : gapHorizontal,
                EspaceMinimalGrilleSucces
            );
        }
    }
}
