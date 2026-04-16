using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using RA.Compagnon.Modeles.Api.V2.Game;
using RA.Compagnon.Modeles.Local;
using RA.Compagnon.Modeles.Presentation;
using RA.Compagnon.Services;
using SystemControls = System.Windows.Controls;

/*
 * Regroupe la logique d'ordonnancement, de rendu et d'interaction de la
 * grille affichant tous les succès du jeu courant.
 */
namespace RA.Compagnon;

/*
 * Porte la gestion complète de la grille de succès, depuis son mode
 * d'affichage jusqu'aux interactions de sélection et de style.
 */
public partial class MainWindow
{
    /*
     * Restaure le mode d'affichage des succès depuis la configuration locale.
     */
    private void AppliquerModeAffichageSuccesDepuisConfiguration()
    {
        _etatListeSuccesUi.OrdreCourant = ConvertirModeAffichageSuccesDepuisConfiguration(
            _configurationConnexion.ModeAffichageSucces
        );
        MettreAJourLibelleOrdreSuccesGrilleEtModes();
    }

    /*
     * Convertit la chaîne stockée en configuration vers l'énumération interne
     * utilisée par la grille de succès.
     */
    private static OrdreSuccesGrille ConvertirModeAffichageSuccesDepuisConfiguration(
        string? modeAffichageSucces
    )
    {
        return modeAffichageSucces switch
        {
            "Aléatoire" => OrdreSuccesGrille.Aleatoire,
            "Facile" => OrdreSuccesGrille.Facile,
            "Difficile" => OrdreSuccesGrille.Difficile,
            _ => OrdreSuccesGrille.Normal,
        };
    }

    /*
     * Sauvegarde le mode d'affichage courant lorsque l'utilisateur l'a modifié.
     */
    private void MemoriserModeAffichageSucces()
    {
        string modeAffichage = _etatListeSuccesUi.OrdreCourant switch
        {
            OrdreSuccesGrille.Aleatoire => "Aléatoire",
            OrdreSuccesGrille.Facile => "Facile",
            OrdreSuccesGrille.Difficile => "Difficile",
            _ => "Normal",
        };

        if (
            string.Equals(
                _configurationConnexion.ModeAffichageSucces,
                modeAffichage,
                StringComparison.Ordinal
            )
        )
        {
            return;
        }

        _configurationConnexion.ModeAffichageSucces = modeAffichage;
        _modeAffichageSuccesModifie = true;
    }

    /*
     * Ouvre ou ferme le menu contextuel de choix du mode d'affichage.
     */
    private void BoutonOrdreSuccesGrille_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { ContextMenu: { } menu })
        {
            return;
        }

        menu.PlacementTarget = sender as FrameworkElement;
        menu.IsOpen = !menu.IsOpen;
    }

    /*
     * Change l'ordre de la grille, réinitialise la sélection visible puis
     * recharge la grille si un jeu est déjà présent.
     */
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

        ReinitialiserSelectionSuccesGrille();
        MemoriserModeAffichageSucces();
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

    /*
     * Efface toute sélection temporaire ou épinglée dans la grille de succès.
     */
    private void ReinitialiserSelectionSuccesGrille()
    {
        _etatListeSuccesUi.IdentifiantSuccesTemporaire = null;
        _etatListeSuccesUi.IdentifiantSuccesEpingle = null;
        _etatListeSuccesUi.RetourPremierSuccesApresSelectionTemporaire = false;
        _minuteurAffichageTemporaireSuccesGrille.Stop();
    }

    /*
     * Invalide l'ordre aléatoire mémorisé pour forcer un nouveau mélange.
     */
    private void InvaliderOrdreAleatoireSuccesGrille()
    {
        _etatListeSuccesUi.IdentifiantJeuOrdreAleatoire = 0;
        _etatListeSuccesUi.SignatureOrdreAleatoire = string.Empty;
        _etatListeSuccesUi.PositionsAleatoires.Clear();
    }

    /*
     * Met à jour le libellé et l'état visuel des différents modes
     * d'affichage de la grille.
     */
    private void MettreAJourLibelleOrdreSuccesGrilleEtModes()
    {
        string libelle = _etatListeSuccesUi.OrdreCourant switch
        {
            OrdreSuccesGrille.Aleatoire => "Aléatoire",
            OrdreSuccesGrille.Facile => "Facile",
            OrdreSuccesGrille.Difficile => "Difficile",
            _ => "Normal",
        };

        _vueModele.LibelleOrdreSuccesGrille = libelle;

        Brush contourActif = new SolidColorBrush(Color.FromRgb(120, 200, 255));
        Brush contourInactif = new SolidColorBrush(Color.FromArgb(140, 255, 255, 255));
        Brush centreActif = new SolidColorBrush(Color.FromRgb(120, 200, 255));

        bool modeNormal = _etatListeSuccesUi.OrdreCourant == OrdreSuccesGrille.Normal;
        bool modeAleatoire = _etatListeSuccesUi.OrdreCourant == OrdreSuccesGrille.Aleatoire;
        bool modeFacile = _etatListeSuccesUi.OrdreCourant == OrdreSuccesGrille.Facile;
        bool modeDifficile = _etatListeSuccesUi.OrdreCourant == OrdreSuccesGrille.Difficile;
        _vueModele.OrdreSuccesNormalActif = modeNormal;
        _vueModele.OrdreSuccesAleatoireActif = modeAleatoire;
        _vueModele.OrdreSuccesFacileActif = modeFacile;
        _vueModele.OrdreSuccesDifficileActif = modeDifficile;
        _vueModele.ContourOrdreSuccesNormal = modeNormal ? contourActif : contourInactif;
        _vueModele.ContourOrdreSuccesAleatoire = modeAleatoire ? contourActif : contourInactif;
        _vueModele.ContourOrdreSuccesFacile = modeFacile ? contourActif : contourInactif;
        _vueModele.ContourOrdreSuccesDifficile = modeDifficile ? contourActif : contourInactif;

        if (ContourOrdreSuccesNormal is not null)
        {
            CentreOrdreSuccesNormal.Fill = centreActif;
        }

        if (ContourOrdreSuccesAleatoire is not null)
        {
            CentreOrdreSuccesAleatoire.Fill = centreActif;
        }

        if (ContourOrdreSuccesFacile is not null)
        {
            CentreOrdreSuccesFacile.Fill = centreActif;
        }

        if (ContourOrdreSuccesDifficile is not null)
        {
            CentreOrdreSuccesDifficile.Fill = centreActif;
        }
    }

    /*
     * Ordonne les succès selon le mode courant puis applique la règle qui
     * repousse les succès passés en fin de liste non débloquée.
     */
    private List<GameAchievementV2> OrdonnerSuccesPourGrilleSelonMode(
        int identifiantJeu,
        IEnumerable<GameAchievementV2> succes
    )
    {
        List<GameAchievementV2> succesOrdonnes = _etatListeSuccesUi.OrdreCourant switch
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

        return AppliquerOrdreSuccesPasses(succesOrdonnes);
    }

    /*
     * Déplace les succès marqués comme passés après les autres succès non
     * débloqués, tout en conservant les succès déjà obtenus à la fin.
     */
    private List<GameAchievementV2> AppliquerOrdreSuccesPasses(
        List<GameAchievementV2> succesOrdonnes
    )
    {
        if (_etatListeSuccesUi.SuccesPasses.Count == 0)
        {
            return succesOrdonnes;
        }

        Dictionary<int, int> positionsPassage = _etatListeSuccesUi
            .SuccesPasses.Select((id, index) => new { id, index })
            .ToDictionary(item => item.id, item => item.index);

        List<GameAchievementV2> succesNonDebloquesNormaux =
        [
            .. succesOrdonnes.Where(item =>
                !SuccesEstDebloquePourAffichage(item) && !positionsPassage.ContainsKey(item.Id)
            ),
        ];
        List<GameAchievementV2> succesNonDebloquesPasses =
        [
            .. succesOrdonnes
                .Where(item =>
                    !SuccesEstDebloquePourAffichage(item) && positionsPassage.ContainsKey(item.Id)
                )
                .OrderBy(item => positionsPassage[item.Id]),
        ];
        List<GameAchievementV2> succesDebloques =
        [
            .. succesOrdonnes.Where(SuccesEstDebloquePourAffichage),
        ];

        return [.. succesNonDebloquesNormaux, .. succesNonDebloquesPasses, .. succesDebloques];
    }

    /*
     * Ordonne les succès par TrueRatio en conservant le regroupement entre
     * succès obtenus et non obtenus.
     */
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

    /*
     * Recalcule l'ordre aléatoire mémorisé lorsqu'il n'est plus compatible
     * avec la liste actuelle des succès non débloqués.
     */
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

    /*
     * Ordonne les succès selon le mélange aléatoire mémorisé pour le jeu.
     */
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

    /*
     * Reconstruit progressivement la grille de tous les succès pour le jeu
     * courant, en respectant la version de chargement active.
     */
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
        ServiceDiagnosticFaisabiliteSucces.JournaliserJeu(
            identifiantJeu,
            _dernieresDonneesJeuAffichees?.Jeu.Title ?? _dernierTitreJeuApi,
            succesOrdonnes,
            _dernieresDonneesJeuAffichees?.Jeu.NumDistinctPlayers ?? 0
        );
        JournaliserDiagnosticListeSucces(
            "grille_fin",
            $"jeu={identifiantJeu};version={versionGrille};badgesAjoutes={etatsBadges.Count};succesAttendus={succesOrdonnes.Count}"
        );
        TerminerDiagnosticChangementJeu("grille_fin", $"badges={etatsBadges.Count}");
    }

    /*
     * Construit le badge visuel cliquable utilisé pour représenter un succès
     * dans la grille complète.
     */
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
            ToolTip = succesAffiche.ToolTip,
            Tag = new BadgeSuccesGrilleContexte(
                identifiantJeu,
                succesAffiche.IdentifiantSucces,
                succesAffiche.UrlBadge,
                succesAffiche.EstHardcore
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
            Opacity = ConstantesDesign.OpaciteSecondaire,
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
            Width = TailleBadgeGrilleSucces,
            Height = TailleBadgeGrilleSucces,
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

    /*
     * Charge en arrière-plan l'image du badge et applique le rendu adapté
     * selon l'état débloqué ou non du succès.
     */
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
        catch { }
    }

    /*
     * Applique le style d'un badge selon son état épinglé, temporaire ou
     * hardcore dans la grille.
     */
    private void AppliquerStyleBadgeEpingle(SystemControls.Border badge)
    {
        if (badge.Tag is not BadgeSuccesGrilleContexte contexte)
        {
            return;
        }

        badge.Effect = null;

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
            badge.BorderThickness = new Thickness(ConstantesDesign.EpaisseurContourAccent);
            badge.Background = new SolidColorBrush(Color.FromArgb(34, 255, 196, 64));
            AppliquerStyleBadgeHardcore(badge, contexte, prioritaire: true);
            return;
        }

        if (estTemporaire)
        {
            badge.BorderBrush = new SolidColorBrush(Color.FromRgb(120, 200, 255));
            badge.BorderThickness = new Thickness(ConstantesDesign.EpaisseurContourAccent);
            badge.Background = new SolidColorBrush(Color.FromArgb(32, 120, 200, 255));
            AppliquerStyleBadgeHardcore(badge, contexte, prioritaire: true);
            return;
        }

        AppliquerStyleBadgeHardcore(badge, contexte, prioritaire: false);
    }

    /*
     * Applique le halo et la bordure du mode hardcore, avec une intensité
     * plus forte lorsqu'un autre état prioritaire est actif.
     */
    private static void AppliquerStyleBadgeHardcore(
        SystemControls.Border badge,
        BadgeSuccesGrilleContexte contexte,
        bool prioritaire
    )
    {
        if (!contexte.EstHardcore)
        {
            badge.BorderBrush = Brushes.Transparent;
            badge.BorderThickness = new Thickness(0);
            badge.Background = Brushes.Transparent;
            return;
        }

        if (!prioritaire)
        {
            badge.BorderBrush = new SolidColorBrush(Color.FromRgb(245, 200, 76));
            badge.BorderThickness = new Thickness(ConstantesDesign.EpaisseurContourAccent);
            badge.Background = new SolidColorBrush(Color.FromArgb(28, 245, 200, 76));
        }

        badge.Effect = new DropShadowEffect
        {
            Color = Color.FromRgb(245, 200, 76),
            BlurRadius = prioritaire
                ? ConstantesDesign.FlouHaloHardcorePrioritaire
                : ConstantesDesign.FlouHaloHardcore,
            ShadowDepth = 0,
            Opacity = prioritaire
                ? ConstantesDesign.OpaciteHaloHardcorePrioritaire
                : ConstantesDesign.OpaciteHaloHardcore,
        };
    }

    /*
     * Réapplique le style de tous les badges déjà présents dans la grille.
     */
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

    /*
     * Ouvre durablement le succès sélectionné via un clic gauche.
     */
    private async void BadgeGrilleSucces_ClicGauche(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        await AfficherSuccesGrilleSelectionneAsync(sender, permanent: true);
    }

    /*
     * Affiche temporairement le succès sélectionné via un clic droit.
     */
    private async void BadgeGrilleSucces_ClicDroit(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        await AfficherSuccesGrilleSelectionneAsync(sender, permanent: false);
    }

    /*
     * Détermine le type de sélection demandé pour un badge puis affiche le
     * succès correspondant dans la carte de détail.
     */
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

    /*
     * Terminé l'affichage temporaire d'un succès puis revient soit au premier
     * succès non débloqué, soit à l'état normal de la grille.
     */
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

    /*
     * Réagit aux changements de taille du conteneur de la grille pour
     * recalculer la disposition et l'écrêtage.
     */
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
        JournaliserDimensionsListeSucces(
            "conteneur_sizechanged",
            $"largeur={e.NewSize.Width:0.##};hauteur={e.NewSize.Height:0.##}"
        );
        PlanifierMiseAJourDispositionGrilleTousSucces();
        GrilleTousSuccesJeuEnCours.UpdateLayout();
        ConteneurGrilleTousSuccesJeuEnCours.UpdateLayout();
        PlanifierAjustementHauteurListeSuccesJeuEnCours();
        PlanifierMiseAJourAnimationGrilleTousSucces();
    }

    /*
     * Programme un recalcul différé de la disposition de la grille de succès.
     */
    private void PlanifierMiseAJourDispositionGrilleTousSucces()
    {
        if (_etatListeSuccesUi.MiseAJourDispositionPlanifiee)
        {
            return;
        }

        _etatListeSuccesUi.MiseAJourDispositionPlanifiee = true;
        _ = Dispatcher.BeginInvoke(
            () =>
            {
                _etatListeSuccesUi.MiseAJourDispositionPlanifiee = false;
                MettreAJourDispositionGrilleTousSucces();
                AppliquerEcretageArrondiZoneSucces();
                JournaliserDimensionsListeSucces("disposition_grille_recalculee");
            },
            DispatcherPriority.Render
        );
    }

    /*
     * Retourne la largeur visible réellement disponible pour la grille des
     * succès, en préférant la zone dédiée lorsque présente.
     */
    private double ObtenirLargeurVisibleListeSucces()
    {
        if (ZoneVisibleListeSuccesJeuEnCours is not null)
        {
            double largeurZone = Math.Max(0, ZoneVisibleListeSuccesJeuEnCours.ActualWidth);

            if (largeurZone > 0)
            {
                return largeurZone;
            }

            double largeurViewport = Math.Max(
                0,
                ConteneurGrilleTousSuccesJeuEnCours?.ViewportWidth ?? 0
            );

            if (largeurViewport > 0)
            {
                return largeurViewport;
            }

            return largeurZone;
        }

        if (ConteneurGrilleTousSuccesJeuEnCours is null)
        {
            return 0;
        }

        double largeurVisible = Math.Max(0, ConteneurGrilleTousSuccesJeuEnCours.ViewportWidth);

        if (largeurVisible > 0)
        {
            return largeurVisible;
        }

        return Math.Max(0, ConteneurGrilleTousSuccesJeuEnCours.ActualWidth);
    }

    /*
     * Retourne la hauteur visible réellement disponible pour la grille des
     * succès dans son conteneur courant.
     */
    private double ObtenirHauteurVisibleListeSucces()
    {
        if (ZoneVisibleListeSuccesJeuEnCours is not null)
        {
            return Math.Max(0, ZoneVisibleListeSuccesJeuEnCours.ActualHeight);
        }

        if (ConteneurGrilleTousSuccesJeuEnCours is null)
        {
            return 0;
        }

        double hauteurViewport = Math.Max(0, ConteneurGrilleTousSuccesJeuEnCours.ViewportHeight);

        if (hauteurViewport > 0)
        {
            return hauteurViewport;
        }

        return Math.Max(0, ConteneurGrilleTousSuccesJeuEnCours.ActualHeight);
    }

    /*
     * Applique un écrêtage à coins arrondis à la zone visible de la liste
     * complète des succès.
     */
    private void AppliquerEcretageArrondiZoneSucces()
    {
        if (ConteneurGrilleTousSuccesJeuEnCours is null || ZoneVisibleListeSuccesJeuEnCours is null)
        {
            return;
        }

        double largeur = ObtenirLargeurVisibleListeSucces();
        double hauteur = ObtenirHauteurVisibleListeSucces();

        if (largeur <= 0 || hauteur <= 0)
        {
            ZoneVisibleListeSuccesJeuEnCours.Clip = null;
            return;
        }

        CornerRadius rayon = ObtenirRayonCoins("RayonCoinsPetit", 8);
        double rayonEffectif = Math.Max(
            0,
            Math.Min(Math.Min(rayon.TopLeft, rayon.TopRight), Math.Min(largeur, hauteur) / 2)
        );

        RectangleGeometry geometrie = new(
            new Rect(0, 0, largeur, hauteur),
            rayonEffectif,
            rayonEffectif
        );
        ZoneVisibleListeSuccesJeuEnCours.Clip = geometrie.Clone();
        ConteneurGrilleTousSuccesJeuEnCours.Clip = null;
    }

    /*
     * Recalcule le nombre de colonnes et les marges des badges pour centrer
     * la grille dans l'espace horizontal disponible.
     */
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

        double largeurDisponible = ObtenirLargeurVisibleListeSucces();

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
        int nombreRangees = (int)Math.Ceiling((double)nombreBadges / colonnes);
        double gapHorizontal = EspaceMinimalGrilleSucces;
        double largeurGrille = colonnes * TailleBadgeGrilleSucces;

        if (colonnes > 1)
        {
            largeurGrille += (colonnes - 1) * gapHorizontal;
        }

        GrilleTousSuccesJeuEnCours.Width = Math.Min(largeurDisponible, largeurGrille);

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
