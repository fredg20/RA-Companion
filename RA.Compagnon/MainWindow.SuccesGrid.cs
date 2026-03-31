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
        if (_etatListeSuccesUi.OrdreCourant == nouvelOrdre)
        {
            MettreAJourLibelleOrdreSuccesGrilleEtModes();
            return;
        }

        _etatListeSuccesUi.OrdreCourant = nouvelOrdre;

        if (nouvelOrdre == OrdreSuccesGrille.Aleatoire)
        {
            InvaliderOrdreAleatoireSuccesGrille();
        }

        MettreAJourLibelleOrdreSuccesGrilleEtModes();

        if (_identifiantJeuSuccesCourant <= 0 || _succesJeuCourant.Count == 0)
        {
            return;
        }

        await MettreAJourGrilleTousSuccesAsync(
            _identifiantJeuSuccesCourant,
            _succesJeuCourant,
            _etatListeSuccesUi.VersionChargementGrille
        );

        if (
            !_etatListeSuccesUi.IdentifiantSuccesTemporaire.HasValue
            && !_etatListeSuccesUi.IdentifiantSuccesEpingle.HasValue
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
        _etatListeSuccesUi.IdentifiantJeuOrdreAleatoire = 0;
        _etatListeSuccesUi.SignatureOrdreAleatoire = string.Empty;
        _etatListeSuccesUi.PositionsAleatoires.Clear();
    }

    private void MettreAJourLibelleOrdreSuccesGrilleEtModes()
    {
        string libelle = _etatListeSuccesUi.OrdreCourant switch
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

        bool modeNormal = _etatListeSuccesUi.OrdreCourant == OrdreSuccesGrille.Normal;
        bool modeAleatoire = _etatListeSuccesUi.OrdreCourant == OrdreSuccesGrille.Aleatoire;
        bool modeFacile = _etatListeSuccesUi.OrdreCourant == OrdreSuccesGrille.Facile;
        bool modeDifficile = _etatListeSuccesUi.OrdreCourant == OrdreSuccesGrille.Difficile;

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
        return _etatListeSuccesUi.OrdreCourant switch
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
                    .OrderBy(item => SuccesEstDebloquePourAffichage(item) ? 1 : 0)
                    .ThenBy(item => item.DisplayOrder)
                    .ThenBy(item => item.Id),
            ],
        };
    }

    private List<GameAchievementV2> OrdonnerSuccesPourGrilleParTrueRatio(
        IEnumerable<GameAchievementV2> succes,
        bool ordreCroissant
    )
    {
        IOrderedEnumerable<GameAchievementV2> ordreInitial = ordreCroissant
            ? succes
                .OrderBy(item => SuccesEstDebloquePourAffichage(item) ? 1 : 0)
                .ThenBy(item => item.TrueRatio)
            : succes
                .OrderBy(item => SuccesEstDebloquePourAffichage(item) ? 1 : 0)
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
                .Where(item => !SuccesEstDebloquePourAffichage(item))
                .Select(item => item.Id)
                .OrderBy(item => item),
        ];
        string signature = $"{identifiantJeu}:{string.Join(',', succesNonDebloques)}";

        if (
            _etatListeSuccesUi.IdentifiantJeuOrdreAleatoire == identifiantJeu
            && string.Equals(
                _etatListeSuccesUi.SignatureOrdreAleatoire,
                signature,
                StringComparison.Ordinal
            )
            && succesNonDebloques.All(item =>
                _etatListeSuccesUi.PositionsAleatoires.ContainsKey(item)
            )
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

        _etatListeSuccesUi.PositionsAleatoires.Clear();
        foreach (
            var item in succesMelanges.Select(
                (identifiantSucces, index) => new { identifiantSucces, index }
            )
        )
        {
            _etatListeSuccesUi.PositionsAleatoires[item.identifiantSucces] = item.index;
        }
        _etatListeSuccesUi.IdentifiantJeuOrdreAleatoire = identifiantJeu;
        _etatListeSuccesUi.SignatureOrdreAleatoire = signature;
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
                .OrderBy(item => SuccesEstDebloquePourAffichage(item) ? 1 : 0)
                .ThenBy(item =>
                    SuccesEstDebloquePourAffichage(item)
                        ? int.MaxValue
                        : _etatListeSuccesUi.PositionsAleatoires.GetValueOrDefault(
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
        List<GameAchievementV2> succes,
        int versionGrille
    )
    {
        if (
            _identifiantJeuSuccesCourant != identifiantJeu
            || _etatListeSuccesUi.VersionChargementGrille != versionGrille
        )
        {
            JournaliserDiagnosticListeSucces(
                "grille_ignoree_version",
                $"jeu={identifiantJeu};version={versionGrille};versionCourante={_etatListeSuccesUi.VersionChargementGrille};jeuCourant={_identifiantJeuSuccesCourant};succesAttendus={succes.Count}"
            );
            return;
        }

        JournaliserDiagnosticChangementJeu(
            "grille_debut",
            $"jeu={identifiantJeu};succes={succes.Count}"
        );
        JournaliserDiagnosticListeSucces(
            "grille_debut",
            $"jeu={identifiantJeu};version={versionGrille};succesAttendus={succes.Count}"
        );
        GrilleTousSuccesJeuEnCours.Children.Clear();
        GrilleTousSuccesJeuEnCours.Width = double.NaN;
        GrilleTousSuccesJeuEnCours.Height = double.NaN;
        GrilleTousSuccesJeuEnCours.InvalidateMeasure();
        GrilleTousSuccesJeuEnCours.InvalidateArrange();
        ConteneurGrilleTousSuccesJeuEnCours.InvalidateMeasure();
        ConteneurGrilleTousSuccesJeuEnCours.InvalidateArrange();
        List<GameAchievementV2> succesOrdonnes = OrdonnerSuccesPourGrilleSelonMode(
            identifiantJeu,
            succes
        );

        if (succesOrdonnes.Count == 0)
        {
            SauvegarderDerniereListeSuccesAffichee(identifiantJeu, []);
            JournaliserDiagnosticListeSucces(
                "grille_vide",
                $"jeu={identifiantJeu};version={versionGrille}"
            );
            PlanifierMiseAJourAnimationGrilleTousSucces();
            TerminerDiagnosticChangementJeu("grille_vide");
            return;
        }

        List<ElementListeSuccesAfficheLocal> etatsBadges = [];
        int indexLot = 0;

        foreach (GameAchievementV2[] lotSucces in succesOrdonnes.Chunk(12))
        {
            indexLot++;

            if (
                _identifiantJeuSuccesCourant != identifiantJeu
                || _etatListeSuccesUi.VersionChargementGrille != versionGrille
            )
            {
                JournaliserDiagnosticListeSucces(
                    "grille_abandon_lot",
                    $"jeu={identifiantJeu};version={versionGrille};versionCourante={_etatListeSuccesUi.VersionChargementGrille};lot={indexLot};badgesAjoutes={etatsBadges.Count};succesAttendus={succesOrdonnes.Count}"
                );
                return;
            }

            foreach (GameAchievementV2 succesJeu in lotSucces)
            {
                SuccesGrilleAffiche succesAffiche = ServicePresentationSucces.ConstruirePourGrille(
                    succesJeu
                );

                GrilleTousSuccesJeuEnCours.Children.Add(
                    ConstruireBadgeGrilleSucces(identifiantJeu, succesAffiche)
                );
                etatsBadges.Add(
                    new ElementListeSuccesAfficheLocal
                    {
                        IdentifiantSucces = succesJeu.Id,
                        Titre = succesAffiche.Titre,
                        CheminImageBadge = succesAffiche.UrlBadge,
                    }
                );
            }

            JournaliserDiagnosticListeSucces(
                "grille_lot",
                $"jeu={identifiantJeu};version={versionGrille};lot={indexLot};tailleLot={lotSucces.Length};badgesAjoutes={etatsBadges.Count};succesAttendus={succesOrdonnes.Count}"
            );
            MettreAJourDispositionGrilleTousSucces();
            GrilleTousSuccesJeuEnCours.UpdateLayout();
            ConteneurGrilleTousSuccesJeuEnCours.UpdateLayout();
            PlanifierAjustementHauteurListeSuccesJeuEnCours();
            await Task.Yield();
        }

        if (
            _identifiantJeuSuccesCourant != identifiantJeu
            || _etatListeSuccesUi.VersionChargementGrille != versionGrille
        )
        {
            JournaliserDiagnosticListeSucces(
                "grille_abandon_fin",
                $"jeu={identifiantJeu};version={versionGrille};versionCourante={_etatListeSuccesUi.VersionChargementGrille};badgesAjoutes={etatsBadges.Count};succesAttendus={succesOrdonnes.Count}"
            );
            return;
        }

        ConteneurGrilleTousSuccesJeuEnCours.ScrollToVerticalOffset(0);
        SauvegarderDerniereListeSuccesAffichee(identifiantJeu, etatsBadges);
        MettreAJourDispositionGrilleTousSucces();
        GrilleTousSuccesJeuEnCours.UpdateLayout();
        ConteneurGrilleTousSuccesJeuEnCours.UpdateLayout();
        CarteListeSuccesJeuEnCours?.UpdateLayout();
        CarteJeuEnCours?.UpdateLayout();
        PlanifierAjustementHauteurListeSuccesJeuEnCours();
        RafraichirStyleBadgesGrilleSucces();
        PlanifierMiseAJourAnimationGrilleTousSucces();
        JournaliserDiagnosticListeSucces(
            "grille_fin",
            $"jeu={identifiantJeu};version={versionGrille};badgesAjoutes={etatsBadges.Count};succesAttendus={succesOrdonnes.Count}"
        );
        TerminerDiagnosticChangementJeu("grille_fin", $"badges={etatsBadges.Count}");
    }

    /// <summary>
    /// Construit un badge de la grille des rétrosuccès à partir de son titre et de son visuel.
    /// </summary>
    private SystemControls.Border ConstruireBadgeGrilleSucces(
        int identifiantJeu,
        SuccesGrilleAffiche succesAffiche
    )
    {
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

        SystemControls.Grid grilleVisuel = new();
        SystemControls.TextBlock texteSecours = new()
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = 0.62,
            FontSize = 10,
            FontWeight = FontWeights.SemiBold,
            Text = string.IsNullOrWhiteSpace(succesAffiche.Titre)
                ? "?"
                : succesAffiche.Titre[..1].ToUpperInvariant(),
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.NoWrap,
        };
        SystemControls.Image imageSucces = new()
        {
            Width = 34,
            Height = 34,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = 0,
            Stretch = Stretch.Uniform,
        };

        imageSucces.Loaded += (_, _) => AppliquerCoinsArrondisImage(imageSucces);
        imageSucces.SizeChanged += (_, _) => AppliquerCoinsArrondisImage(imageSucces);
        grilleVisuel.Children.Add(texteSecours);
        grilleVisuel.Children.Add(imageSucces);
        conteneur.Child = grilleVisuel;
        AppliquerStyleBadgeEpingle(conteneur);
        _ = ChargerImageBadgeGrilleEnArrierePlanAsync(imageSucces, texteSecours, succesAffiche);
        return conteneur;
    }

    private async Task ChargerImageBadgeGrilleEnArrierePlanAsync(
        SystemControls.Image imageSucces,
        SystemControls.TextBlock texteSecours,
        SuccesGrilleAffiche succesAffiche
    )
    {
        try
        {
            ImageSource? imageBadge = await ChargerImageDistanteAsync(succesAffiche.UrlBadge);

            if (imageBadge is null)
            {
                return;
            }

            imageSucces.Source = succesAffiche.EstDebloque
                ? imageBadge
                : ConvertirImageEnNoirEtBlanc(imageBadge);
            imageSucces.Opacity = succesAffiche.EstDebloque ? 1 : 0.58;
            texteSecours.Visibility = Visibility.Collapsed;
        }
        catch
        {
            // Le badge texte de secours reste affiché.
        }
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
            _etatListeSuccesUi.IdentifiantSuccesEpingle.HasValue
            && contexte.IdentifiantJeu == _identifiantJeuSuccesCourant
            && contexte.Id == _etatListeSuccesUi.IdentifiantSuccesEpingle.Value;
        bool estTemporaire =
            _etatListeSuccesUi.IdentifiantSuccesTemporaire.HasValue
            && contexte.IdentifiantJeu == _identifiantJeuSuccesCourant
            && contexte.Id == _etatListeSuccesUi.IdentifiantSuccesTemporaire.Value;

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

        bool succesDebloque = SuccesEstDebloquePourAffichage(succes);
        bool affichagePermanent = permanent && !succesDebloque;

        if (affichagePermanent)
        {
            _etatListeSuccesUi.IdentifiantSuccesEpingle = succes.Id;
            _etatListeSuccesUi.IdentifiantSuccesTemporaire = null;
            _etatListeSuccesUi.RetourPremierSuccesApresSelectionTemporaire = false;
            _minuteurAffichageTemporaireSuccesGrille.Stop();
        }
        else
        {
            if (succesDebloque)
            {
                _etatListeSuccesUi.IdentifiantSuccesEpingle = null;
                _etatListeSuccesUi.RetourPremierSuccesApresSelectionTemporaire = true;
            }
            else
            {
                _etatListeSuccesUi.RetourPremierSuccesApresSelectionTemporaire = false;
            }

            _etatListeSuccesUi.IdentifiantSuccesTemporaire = succes.Id;
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
        _etatListeSuccesUi.IdentifiantSuccesTemporaire = null;
        ServiceSurveillanceSuccesLocaux.JournaliserEvenement(
            "succes_ui_fin_temporaire",
            $"jeu={_identifiantJeuSuccesCourant};nbSucces={_succesJeuCourant.Count}"
        );

        if (_identifiantJeuSuccesCourant <= 0 || _succesJeuCourant.Count == 0)
        {
            return;
        }

        RafraichirStyleBadgesGrilleSucces();

        if (_etatListeSuccesUi.RetourPremierSuccesApresSelectionTemporaire)
        {
            _etatListeSuccesUi.RetourPremierSuccesApresSelectionTemporaire = false;

            GameAchievementV2? premierSuccesNonDebloque = _succesJeuCourant
                .Where(item => !SuccesEstDebloquePourAffichage(item))
                .OrderBy(item => item.DisplayOrder)
                .ThenBy(item => item.Id)
                .FirstOrDefault();

            await AppliquerSuccesEnCoursAsync(
                _identifiantJeuSuccesCourant,
                premierSuccesNonDebloque,
                true,
                false
            );
            ServiceSurveillanceSuccesLocaux.JournaliserEvenement(
                "succes_ui_retour_automatique",
                premierSuccesNonDebloque is null
                    ? $"jeu={_identifiantJeuSuccesCourant};succes=0;raison=tous_debloques"
                    : $"jeu={_identifiantJeuSuccesCourant};succes={premierSuccesNonDebloque.Id};titre={premierSuccesNonDebloque.Title}"
            );
            return;
        }

        await MettreAJourPremierSuccesNonDebloqueAsync(
            _identifiantJeuSuccesCourant,
            _succesJeuCourant
        );
        ServiceSurveillanceSuccesLocaux.JournaliserEvenement(
            "succes_ui_retour_normal",
            $"jeu={_identifiantJeuSuccesCourant}"
        );
    }

    /// <summary>
    /// Indique si un succès du jeu a déjà été obtenu par l'utilisateur.
    /// </summary>
    /// <summary>
    /// Recalcule le gap de la grille des succès selon la largeur disponible.
    /// </summary>
    private void ConteneurGrilleTousSuccesJeuEnCours_TailleChangee(
        object sender,
        SizeChangedEventArgs e
    )
    {
        AppliquerEcretageArrondiZoneSucces();
        JournaliserDiagnosticListeSucces(
            "liste_sizechanged",
            $"largeur={e.NewSize.Width:0.##};hauteur={e.NewSize.Height:0.##}"
        );
        MettreAJourDispositionGrilleTousSucces();
        GrilleTousSuccesJeuEnCours.UpdateLayout();
        ConteneurGrilleTousSuccesJeuEnCours.UpdateLayout();
        PlanifierAjustementHauteurListeSuccesJeuEnCours();
        PlanifierMiseAJourAnimationGrilleTousSucces();
    }

    /// <summary>
    /// Applique un masque d'écrêtage arrondi à la zone visible de la liste des succès.
    /// </summary>
    private void AppliquerEcretageArrondiZoneSucces()
    {
        if (ConteneurGrilleTousSuccesJeuEnCours is null)
        {
            return;
        }

        double largeur = ConteneurGrilleTousSuccesJeuEnCours.ActualWidth;
        double hauteur = ConteneurGrilleTousSuccesJeuEnCours.ActualHeight;

        if (largeur <= 0 || hauteur <= 0)
        {
            ConteneurGrilleTousSuccesJeuEnCours.Clip = null;
            return;
        }

        CornerRadius rayon = ObtenirRayonCoins("RayonCoinsPetit", 8);
        double rayonEffectif = Math.Max(
            0,
            Math.Min(Math.Min(rayon.TopLeft, rayon.TopRight), Math.Min(largeur, hauteur) / 2)
        );

        ConteneurGrilleTousSuccesJeuEnCours.Clip = new RectangleGeometry(
            new Rect(0, 0, largeur, hauteur),
            rayonEffectif,
            rayonEffectif
        );
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

        GrilleTousSuccesJeuEnCours.Width = largeurDisponible;

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
        int nombreRangees = (int)Math.Ceiling((double)nombreBadges / colonnes);
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
            int rangee = index / colonnes;
            int indexPremierElementRangee = rangee * colonnes;
            int elementsDansRangee = Math.Min(colonnes, nombreBadges - indexPremierElementRangee);
            bool dernierDeLigne = colonne == elementsDansRangee - 1;
            bool derniereRangee = rangee == nombreRangees - 1;
            badge.Margin = new Thickness(
                0,
                0,
                dernierDeLigne ? 0 : gapHorizontal,
                derniereRangee ? 0 : EspaceMinimalGrilleSucces
            );
        }
    }
}
