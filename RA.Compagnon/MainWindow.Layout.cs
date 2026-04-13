using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using SystemControls = System.Windows.Controls;

namespace RA.Compagnon;

public partial class MainWindow
{
    private static double CalculerHauteurOccupee(FrameworkElement element)
    {
        return element.ActualHeight + element.Margin.Top + element.Margin.Bottom;
    }

    /// <summary>
    /// Affiche temporairement la barre de défilement après un usage de la molette ou un scroll.
    /// </summary>
    private void AfficherTemporairementBarreDefilementPrincipale()
    {
        if (!ZonePrincipalePeutDefiler())
        {
            DefinirVisibiliteBarreDefilementPrincipale();
            return;
        }

        DefinirVisibiliteBarreDefilementPrincipale();
        _minuteurMasquageBarreDefilement.Stop();
        _minuteurMasquageBarreDefilement.Start();
    }

    /// <summary>
    /// Retourne la barre verticale du ScrollViewer principal.
    /// </summary>
    private SystemControls.Primitives.ScrollBar? ObtenirBarreDefilementVerticalePrincipale()
    {
        if (_barreDefilementVerticalePrincipale is not null)
        {
            return _barreDefilementVerticalePrincipale;
        }

        _barreDefilementVerticalePrincipale =
            TrouverDescendants<SystemControls.Primitives.ScrollBar>(ZonePrincipale)
                .FirstOrDefault(barre => barre.Orientation == SystemControls.Orientation.Vertical);

        return _barreDefilementVerticalePrincipale;
    }

    /// <summary>
    /// Retourne la barre verticale de la liste des succès.
    /// </summary>
    private SystemControls.Primitives.ScrollBar? ObtenirBarreDefilementVerticaleListeSucces()
    {
        if (_etatListeSuccesUi.BarreDefilementVerticale is not null)
        {
            return _etatListeSuccesUi.BarreDefilementVerticale;
        }

        if (ConteneurGrilleTousSuccesJeuEnCours is null)
        {
            return null;
        }

        _etatListeSuccesUi.BarreDefilementVerticale =
            TrouverDescendants<SystemControls.Primitives.ScrollBar>(
                    ConteneurGrilleTousSuccesJeuEnCours
                )
                .FirstOrDefault(barre => barre.Orientation == SystemControls.Orientation.Vertical);

        return _etatListeSuccesUi.BarreDefilementVerticale;
    }

    /// <summary>
    /// Masque la barre verticale principale sans changer la structure du layout.
    /// </summary>
    private void DefinirVisibiliteBarreDefilementPrincipale()
    {
        if (ZonePrincipale is not null)
        {
            ZonePrincipale.VerticalScrollBarVisibility = SystemControls.ScrollBarVisibility.Hidden;
        }

        SystemControls.Primitives.ScrollBar? barre = ObtenirBarreDefilementVerticalePrincipale();

        if (barre is null)
        {
            return;
        }

        barre.Opacity = 0;
        barre.Visibility = Visibility.Hidden;
        barre.IsHitTestVisible = false;
    }

    /// <summary>
    /// Affiche ou masque la barre verticale de la liste des succès selon le survol.
    /// </summary>
    private void DefinirVisibiliteBarreDefilementListeSucces(bool visible)
    {
        if (ConteneurGrilleTousSuccesJeuEnCours is null)
        {
            return;
        }

        bool afficher = visible && ListeSuccesPeutDefiler();
        ConteneurGrilleTousSuccesJeuEnCours.VerticalScrollBarVisibility = afficher
            ? SystemControls.ScrollBarVisibility.Auto
            : SystemControls.ScrollBarVisibility.Hidden;
        ConteneurGrilleTousSuccesJeuEnCours.UpdateLayout();

        SystemControls.Primitives.ScrollBar? barre = ObtenirBarreDefilementVerticaleListeSucces();

        if (barre is null)
        {
            return;
        }

        barre.Opacity = afficher ? 1 : 0;
        barre.Visibility = afficher ? Visibility.Visible : Visibility.Hidden;
        barre.IsHitTestVisible = afficher;
        SystemControls.Panel.SetZIndex(barre, 10);
    }

    /// <summary>
    /// Indique si la souris est placée sur la zone réservée à la barre verticale.
    /// </summary>
    private static bool EstDansZoneBarreDefilement(
        SystemControls.ScrollViewer scrollViewer,
        Point position
    )
    {
        return position.X
            >= Math.Max(0, scrollViewer.ActualWidth - LargeurZoneDetectionBarreDefilement);
    }

    /// <summary>
    /// Indique si le ScrollViewer principal a réellement besoin d'une barre verticale.
    /// </summary>
    private bool ZonePrincipalePeutDefiler()
    {
        return ZonePrincipale is not null && ZonePrincipale.ScrollableHeight > 0;
    }

    /// <summary>
    /// Indique si la liste des succès a réellement besoin d'une barre verticale.
    /// </summary>
    private bool ListeSuccesPeutDefiler()
    {
        return ConteneurGrilleTousSuccesJeuEnCours is not null
            && ConteneurGrilleTousSuccesJeuEnCours.ScrollableHeight > 0;
    }

    /// <summary>
    /// Indique si la souris survole la zone d'apparition de la barre verticale.
    /// </summary>
    private bool SourisSurvoleZoneBarreDefilement()
    {
        if (ZonePrincipale is null || !ZonePrincipale.IsMouseOver)
        {
            return false;
        }

        Point position = Mouse.GetPosition(ZonePrincipale);
        return EstDansZoneBarreDefilement(ZonePrincipale, position);
    }

    /// <summary>
    /// Affiche temporairement la barre verticale quand la molette est utilisée.
    /// </summary>
    private void ZonePrincipale_ApercuMoletteSouris(object sender, MouseWheelEventArgs e)
    {
        AfficherTemporairementBarreDefilementPrincipale();
    }

    /// <summary>
    /// Révèle la barre seulement si la souris survole sa zone dédiée.
    /// </summary>
    private void ZonePrincipale_DeplacementSouris(object sender, MouseEventArgs e)
    {
        if (sender is not SystemControls.ScrollViewer scrollViewer || !ZonePrincipalePeutDefiler())
        {
            DefinirVisibiliteBarreDefilementPrincipale();
            return;
        }

        bool surZoneBarre = EstDansZoneBarreDefilement(scrollViewer, e.GetPosition(scrollViewer));

        if (surZoneBarre)
        {
            _minuteurMasquageBarreDefilement.Stop();
            DefinirVisibiliteBarreDefilementPrincipale();
            return;
        }

        if (!_minuteurMasquageBarreDefilement.IsEnabled)
        {
            DefinirVisibiliteBarreDefilementPrincipale();
        }
    }

    /// <summary>
    /// Masque la barre en quittant la zone, sauf si un défilement récent la maintient visible.
    /// </summary>
    private void ZonePrincipale_SortieSouris(object sender, MouseEventArgs e)
    {
        if (!_minuteurMasquageBarreDefilement.IsEnabled)
        {
            DefinirVisibiliteBarreDefilementPrincipale();
        }
    }

    /// <summary>
    /// Rend la barre visible pendant un défilement effectif du contenu.
    /// </summary>
    private void ZonePrincipale_DefilementChange(
        object sender,
        SystemControls.ScrollChangedEventArgs e
    )
    {
        if (Math.Abs(e.VerticalChange) > 0.01)
        {
            AfficherTemporairementBarreDefilementPrincipale();
        }
    }

    /// <summary>
    /// Masque la barre après un court délai si la souris n'est plus sur sa zone.
    /// </summary>
    private void MinuteurMasquageBarreDefilement_Tick(object? sender, EventArgs e)
    {
        if (SourisSurvoleZoneBarreDefilement())
        {
            return;
        }

        _minuteurMasquageBarreDefilement.Stop();
        DefinirVisibiliteBarreDefilementPrincipale();
    }

    /// <summary>
    /// Affiche la barre de la liste des succès uniquement pendant le survol.
    /// </summary>
    private void ConteneurGrilleTousSuccesJeuEnCours_EntreeSouris(object sender, MouseEventArgs e)
    {
        DefinirVisibiliteBarreDefilementListeSucces(
            visible: ConteneurGrilleTousSuccesJeuEnCours?.IsMouseOver == true
        );
        JournaliserDiagnosticListeSucces("liste_mouseenter");
    }

    /// <summary>
    /// Masque la barre de la liste dès que la souris quitte la zone.
    /// </summary>
    private void ConteneurGrilleTousSuccesJeuEnCours_SortieSouris(object sender, MouseEventArgs e)
    {
        if (_etatListeSuccesUi.InteractionActive && Mouse.LeftButton == MouseButtonState.Pressed)
        {
            return;
        }

        DefinirVisibiliteBarreDefilementListeSucces(visible: false);
        JournaliserDiagnosticListeSucces("liste_mouseleave");

        if (!_etatListeSuccesUi.SurvolBadgeActif)
        {
            _minuteurRepriseAnimationGrilleSucces.Stop();
            _minuteurRepriseAnimationGrilleSucces.Start();
        }
    }

    /// <summary>
    /// Garde la barre visible et l'autodéfilement en pause pendant l'interaction souris.
    /// </summary>
    private void ConteneurGrilleTousSuccesJeuEnCours_ApercuBoutonGaucheBas(
        object sender,
        MouseButtonEventArgs e
    )
    {
        _etatListeSuccesUi.EtatInteraction = EtatInteractionListeSucces.InteractionManuelle;
        _etatListeSuccesUi.DernierOffsetInteraction =
            ConteneurGrilleTousSuccesJeuEnCours?.VerticalOffset ?? 0;
        _minuteurRepriseAnimationGrilleSucces.Stop();
        ArreterAnimationGrilleSucces();
        DefinirVisibiliteBarreDefilementListeSucces(visible: true);
        JournaliserDiagnosticListeSucces("liste_mouseleftdown");
    }

    /// <summary>
    /// Relâche l'interaction souris avec la liste et reprend l'autodéfilement si possible.
    /// </summary>
    private void ConteneurGrilleTousSuccesJeuEnCours_ApercuBoutonGaucheHaut(
        object sender,
        MouseButtonEventArgs e
    )
    {
        FinaliserInteractionListeSucces();
    }

    /// <summary>
    /// Finalise aussi le drag de la barre quand le relâchement se fait hors de la zone.
    /// </summary>
    private void ConteneurGrilleTousSuccesJeuEnCours_PerteCaptureSouris(
        object sender,
        MouseEventArgs e
    )
    {
        if (Mouse.LeftButton == MouseButtonState.Pressed)
        {
            return;
        }

        FinaliserInteractionListeSucces();
    }

    /// <summary>
    /// Laisse le déplacement manuel de la liste devenir la nouvelle référence avant reprise.
    /// </summary>
    private void ConteneurGrilleTousSuccesJeuEnCours_DefilementChange(
        object sender,
        SystemControls.ScrollChangedEventArgs e
    )
    {
        bool viewportModifie =
            Math.Abs(e.ViewportHeightChange) > 0.01
            || Math.Abs(e.ViewportWidthChange) > 0.01
            || Math.Abs(e.ExtentHeightChange) > 0.01
            || Math.Abs(e.ExtentWidthChange) > 0.01;

        if (!viewportModifie)
        {
            return;
        }

        JournaliserDiagnosticListeSucces(
            "liste_viewport_changed",
            $"viewportW={e.ViewportWidthChange:0.##};viewportH={e.ViewportHeightChange:0.##};extentW={e.ExtentWidthChange:0.##};extentH={e.ExtentHeightChange:0.##}"
        );
        PlanifierMiseAJourDispositionGrilleTousSucces();
        PlanifierAjustementHauteurListeSuccesJeuEnCours();
        PlanifierMiseAJourAnimationGrilleTousSucces();
        JournaliserDimensionsListeSucces("viewport_modifie");
        DefinirVisibiliteBarreDefilementListeSucces(visible: true);
    }

    /// <summary>
    /// Valide la dernière position manuelle de la liste puis relance l'autodéfilement depuis cette base.
    /// </summary>
    private void FinaliserInteractionListeSucces()
    {
        if (ConteneurGrilleTousSuccesJeuEnCours is not null)
        {
            _etatListeSuccesUi.DernierOffsetInteraction =
                ConteneurGrilleTousSuccesJeuEnCours.VerticalOffset;
        }

        _etatListeSuccesUi.EtatInteraction = EtatInteractionListeSucces.AutoScroll;
        DefinirVisibiliteBarreDefilementListeSucces(
            ConteneurGrilleTousSuccesJeuEnCours?.IsMouseOver == true
        );
        JournaliserDiagnosticListeSucces("liste_interaction_fin");

        _minuteurRepriseAnimationGrilleSucces.Stop();
        _minuteurRepriseAnimationGrilleSucces.Start();
    }

    /// <summary>
    /// Replanifie la section des rétrosuccès quand sa carte parente change de taille.
    /// </summary>
    private void CadreSuccesJeuEnCours_TailleChangee(object sender, SizeChangedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        if (
            Math.Abs(e.PreviousSize.Width - e.NewSize.Width) <= 0.01
            && Math.Abs(e.PreviousSize.Height - e.NewSize.Height) <= 0.01
        )
        {
            return;
        }

        GrilleTousSuccesJeuEnCours?.InvalidateMeasure();
        GrilleTousSuccesJeuEnCours?.InvalidateArrange();
        ZoneVisibleListeSuccesJeuEnCours?.InvalidateMeasure();
        ZoneVisibleListeSuccesJeuEnCours?.InvalidateArrange();
        ConteneurGrilleTousSuccesJeuEnCours?.InvalidateMeasure();
        ConteneurGrilleTousSuccesJeuEnCours?.InvalidateArrange();
        PlanifierMiseAJourDispositionGrilleTousSucces();
        PlanifierAjustementHauteurListeSuccesJeuEnCours();
        PlanifierMiseAJourAnimationGrilleTousSucces();
        JournaliserDimensionsListeSucces(
            "cadre_succes_sizechanged",
            $"largeur={e.NewSize.Width:0.##};hauteur={e.NewSize.Height:0.##}"
        );
    }

    /// <summary>
    /// Rejoue un recalcul complet de la zone de liste à la fin du layout après redimensionnement.
    /// </summary>
    private void PlanifierRelayoutListeSuccesApresRedimensionnement()
    {
        _minuteurRelayoutApresRedimensionnement.Stop();
        _minuteurRelayoutApresRedimensionnement.Start();
    }

    /// <summary>
    /// Rejoue le relayout une fois que le redimensionnement de fenêtre s'est stabilisé.
    /// </summary>
    private void MinuteurRelayoutApresRedimensionnement_Tick(object? sender, EventArgs e)
    {
        _minuteurRelayoutApresRedimensionnement.Stop();
        _etatListeSuccesUi.RedimensionnementFenetreActif = false;
        CarteJeuEnCours?.InvalidateMeasure();
        CarteJeuEnCours?.InvalidateArrange();
        CarteListeSuccesJeuEnCours?.InvalidateMeasure();
        CarteListeSuccesJeuEnCours?.InvalidateArrange();
        ZoneVisibleListeSuccesJeuEnCours?.InvalidateMeasure();
        ZoneVisibleListeSuccesJeuEnCours?.InvalidateArrange();
        ConteneurGrilleTousSuccesJeuEnCours?.InvalidateMeasure();
        ConteneurGrilleTousSuccesJeuEnCours?.InvalidateArrange();
        AjusterDisposition();
        AjusterHauteurCarteJeuEnCours();
        PlanifierMiseAJourDispositionGrilleTousSucces();
        PlanifierAjustementHauteurListeSuccesJeuEnCours();
        PlanifierMiseAJourAnimationGrilleTousSucces();
        JournaliserDimensionsListeSucces("relayout_apres_redimensionnement");
    }

    /// <summary>
    /// Bascule entre un affichage sur une colonne ou deux colonnes selon la largeur disponible.
    /// </summary>
    private void AjusterDisposition()
    {
        bool dispositionDouble = ActualWidth >= LargeurMinimaleDispositionDouble;
        bool carteConnexionVisible = _vueModele.VisibiliteCarteConnexion == Visibility.Visible;

        GrilleCartes.RowDefinitions.Clear();

        if (dispositionDouble)
        {
            GrilleCartes.ColumnDefinitions[0].Width = carteConnexionVisible
                ? new GridLength(280)
                : new GridLength(1, GridUnitType.Star);
            GrilleCartes.ColumnDefinitions[1].Width = carteConnexionVisible
                ? new GridLength(20)
                : new GridLength(0);
            GrilleCartes.ColumnDefinitions[2].Width = carteConnexionVisible
                ? new GridLength(1, GridUnitType.Star)
                : new GridLength(0);

            GrilleCartes.RowDefinitions.Add(
                new SystemControls.RowDefinition { Height = new GridLength(1, GridUnitType.Star) }
            );

            if (carteConnexionVisible)
            {
                SystemControls.Grid.SetColumn(CarteConnexion, 0);
                SystemControls.Grid.SetRow(CarteConnexion, 0);
            }

            SystemControls.Grid.SetColumn(CarteJeuEnCours, carteConnexionVisible ? 2 : 0);
            SystemControls.Grid.SetRow(CarteJeuEnCours, 0);
        }
        else
        {
            GrilleCartes.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
            GrilleCartes.ColumnDefinitions[1].Width = new GridLength(0);
            GrilleCartes.ColumnDefinitions[2].Width = new GridLength(0);

            if (carteConnexionVisible)
            {
                GrilleCartes.RowDefinitions.Add(
                    new SystemControls.RowDefinition { Height = GridLength.Auto }
                );
                GrilleCartes.RowDefinitions.Add(
                    new SystemControls.RowDefinition { Height = new GridLength(20) }
                );
                GrilleCartes.RowDefinitions.Add(
                    new SystemControls.RowDefinition { Height = GridLength.Auto }
                );
            }
            else
            {
                GrilleCartes.RowDefinitions.Add(
                    new SystemControls.RowDefinition
                    {
                        Height = new GridLength(1, GridUnitType.Star),
                    }
                );
            }

            if (carteConnexionVisible)
            {
                SystemControls.Grid.SetColumn(CarteConnexion, 0);
                SystemControls.Grid.SetRow(CarteConnexion, 0);
            }

            SystemControls.Grid.SetColumn(CarteJeuEnCours, 0);
            SystemControls.Grid.SetRow(CarteJeuEnCours, carteConnexionVisible ? 2 : 0);
        }

        AjusterDispositionSectionsJeuEnCours();
        AjusterHauteurCarteJeuEnCours();
    }

    /// <summary>
    /// Répartit les sous-sections de la carte jeu selon l'espace réellement occupé par la fenêtre.
    /// </summary>
    private void AjusterDispositionSectionsJeuEnCours()
    {
        if (
            GrilleCarteJeuEnCours is null
            || EnTeteCarteJeuEnCours is null
            || EnTeteSectionSuccesEnCours is null
            || EnTeteSectionListeSuccesJeuEnCours is null
            || EnTeteInterneSectionSuccesEnCours is null
            || EnTeteInterneSectionListeSuccesJeuEnCours is null
            || LigneEspacementSectionSuccesEnCours is null
            || LigneEspacementSectionListeSuccesJeuEnCours is null
            || SectionResumeJeuEnCours is null
            || SectionSuccesEnCours is null
            || SectionListeSuccesJeuEnCours is null
            || GrilleCarteJeuEnCours.ColumnDefinitions.Count < 5
            || GrilleCarteJeuEnCours.RowDefinitions.Count < 7
        )
        {
            return;
        }

        bool dispositionTriple = FenetreCouvreEcranPourDispositionTriple();
        bool dispositionEtendue = !dispositionTriple && FenetreCouvreDeuxTiersEcran();
        bool afficherEntetesExternes = dispositionTriple;

        EnTeteSectionSuccesEnCours.Visibility = afficherEntetesExternes
            ? Visibility.Visible
            : Visibility.Collapsed;
        EnTeteSectionListeSuccesJeuEnCours.Visibility = afficherEntetesExternes
            ? Visibility.Visible
            : Visibility.Collapsed;
        EnTeteInterneSectionSuccesEnCours.Visibility = afficherEntetesExternes
            ? Visibility.Collapsed
            : Visibility.Visible;
        EnTeteInterneSectionListeSuccesJeuEnCours.Visibility = afficherEntetesExternes
            ? Visibility.Collapsed
            : Visibility.Visible;
        LigneEspacementSectionSuccesEnCours.Height = afficherEntetesExternes
            ? new GridLength(0)
            : new GridLength(6);
        LigneEspacementSectionListeSuccesJeuEnCours.Height = afficherEntetesExternes
            ? new GridLength(0)
            : new GridLength(6);

        if (dispositionTriple)
        {
            GrilleCarteJeuEnCours.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
            GrilleCarteJeuEnCours.ColumnDefinitions[1].Width = new GridLength(16);
            GrilleCarteJeuEnCours.ColumnDefinitions[2].Width = new GridLength(1, GridUnitType.Star);
            GrilleCarteJeuEnCours.ColumnDefinitions[3].Width = new GridLength(16);
            GrilleCarteJeuEnCours.ColumnDefinitions[4].Width = new GridLength(1, GridUnitType.Star);

            GrilleCarteJeuEnCours.RowDefinitions[0].Height = GridLength.Auto;
            GrilleCarteJeuEnCours.RowDefinitions[1].Height = new GridLength(6);
            GrilleCarteJeuEnCours.RowDefinitions[2].Height = new GridLength(1, GridUnitType.Star);
            GrilleCarteJeuEnCours.RowDefinitions[3].Height = new GridLength(0);
            GrilleCarteJeuEnCours.RowDefinitions[4].Height = new GridLength(0);
            GrilleCarteJeuEnCours.RowDefinitions[5].Height = new GridLength(0);
            GrilleCarteJeuEnCours.RowDefinitions[6].Height = new GridLength(0);

            SystemControls.Grid.SetColumn(EnTeteCarteJeuEnCours, 0);
            SystemControls.Grid.SetColumnSpan(EnTeteCarteJeuEnCours, 1);
            SystemControls.Grid.SetColumn(EnTeteSectionSuccesEnCours, 2);
            SystemControls.Grid.SetColumnSpan(EnTeteSectionSuccesEnCours, 1);
            SystemControls.Grid.SetColumn(EnTeteSectionListeSuccesJeuEnCours, 4);
            SystemControls.Grid.SetColumnSpan(EnTeteSectionListeSuccesJeuEnCours, 1);

            SystemControls.Grid.SetRow(SectionResumeJeuEnCours, 2);
            SystemControls.Grid.SetColumn(SectionResumeJeuEnCours, 0);

            SystemControls.Grid.SetRow(SectionSuccesEnCours, 2);
            SystemControls.Grid.SetColumn(SectionSuccesEnCours, 2);

            SystemControls.Grid.SetRow(SectionListeSuccesJeuEnCours, 2);
            SystemControls.Grid.SetColumn(SectionListeSuccesJeuEnCours, 4);
            SystemControls.Grid.SetColumnSpan(SectionListeSuccesJeuEnCours, 1);
        }
        else if (dispositionEtendue)
        {
            GrilleCarteJeuEnCours.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
            GrilleCarteJeuEnCours.ColumnDefinitions[1].Width = new GridLength(16);
            GrilleCarteJeuEnCours.ColumnDefinitions[2].Width = new GridLength(1, GridUnitType.Star);
            GrilleCarteJeuEnCours.ColumnDefinitions[3].Width = new GridLength(0);
            GrilleCarteJeuEnCours.ColumnDefinitions[4].Width = new GridLength(0);

            GrilleCarteJeuEnCours.RowDefinitions[0].Height = GridLength.Auto;
            GrilleCarteJeuEnCours.RowDefinitions[1].Height = new GridLength(6);
            GrilleCarteJeuEnCours.RowDefinitions[2].Height = GridLength.Auto;
            GrilleCarteJeuEnCours.RowDefinitions[3].Height = new GridLength(16);
            GrilleCarteJeuEnCours.RowDefinitions[4].Height = new GridLength(1, GridUnitType.Star);
            GrilleCarteJeuEnCours.RowDefinitions[5].Height = new GridLength(0);
            GrilleCarteJeuEnCours.RowDefinitions[6].Height = new GridLength(0);

            SystemControls.Grid.SetColumn(EnTeteCarteJeuEnCours, 0);
            SystemControls.Grid.SetColumnSpan(EnTeteCarteJeuEnCours, 1);

            SystemControls.Grid.SetRow(SectionResumeJeuEnCours, 2);
            SystemControls.Grid.SetColumn(SectionResumeJeuEnCours, 0);
            SystemControls.Grid.SetColumnSpan(SectionResumeJeuEnCours, 1);

            SystemControls.Grid.SetRow(SectionSuccesEnCours, 2);
            SystemControls.Grid.SetColumn(SectionSuccesEnCours, 2);
            SystemControls.Grid.SetColumnSpan(SectionSuccesEnCours, 1);

            SystemControls.Grid.SetRow(SectionListeSuccesJeuEnCours, 4);
            SystemControls.Grid.SetColumn(SectionListeSuccesJeuEnCours, 0);
            SystemControls.Grid.SetColumnSpan(SectionListeSuccesJeuEnCours, 3);
        }
        else
        {
            GrilleCarteJeuEnCours.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
            GrilleCarteJeuEnCours.ColumnDefinitions[1].Width = new GridLength(0);
            GrilleCarteJeuEnCours.ColumnDefinitions[2].Width = new GridLength(0);
            GrilleCarteJeuEnCours.ColumnDefinitions[3].Width = new GridLength(0);
            GrilleCarteJeuEnCours.ColumnDefinitions[4].Width = new GridLength(0);

            GrilleCarteJeuEnCours.RowDefinitions[0].Height = GridLength.Auto;
            GrilleCarteJeuEnCours.RowDefinitions[1].Height = new GridLength(6);
            GrilleCarteJeuEnCours.RowDefinitions[2].Height = GridLength.Auto;
            GrilleCarteJeuEnCours.RowDefinitions[3].Height = new GridLength(16);
            GrilleCarteJeuEnCours.RowDefinitions[4].Height = GridLength.Auto;
            GrilleCarteJeuEnCours.RowDefinitions[5].Height = new GridLength(16);
            GrilleCarteJeuEnCours.RowDefinitions[6].Height = new GridLength(1, GridUnitType.Star);

            SystemControls.Grid.SetColumn(EnTeteCarteJeuEnCours, 0);
            SystemControls.Grid.SetColumnSpan(EnTeteCarteJeuEnCours, 5);

            SystemControls.Grid.SetRow(SectionResumeJeuEnCours, 2);
            SystemControls.Grid.SetColumn(SectionResumeJeuEnCours, 0);
            SystemControls.Grid.SetColumnSpan(SectionResumeJeuEnCours, 1);

            SystemControls.Grid.SetRow(SectionSuccesEnCours, 4);
            SystemControls.Grid.SetColumn(SectionSuccesEnCours, 0);
            SystemControls.Grid.SetColumnSpan(SectionSuccesEnCours, 1);

            SystemControls.Grid.SetRow(SectionListeSuccesJeuEnCours, 6);
            SystemControls.Grid.SetColumn(SectionListeSuccesJeuEnCours, 0);
            SystemControls.Grid.SetColumnSpan(SectionListeSuccesJeuEnCours, 1);
        }
    }

    /// <summary>
    /// Indique si la fenêtre occupe pratiquement tout l'écran et peut passer en trois colonnes.
    /// </summary>
    private bool FenetreCouvreEcranPourDispositionTriple()
    {
        if (WindowState == WindowState.Maximized)
        {
            return true;
        }

        Rect zoneTravail = SystemParameters.WorkArea;
        return ActualWidth >= LargeurMinimaleDispositionTriple
            && ActualWidth >= zoneTravail.Width - 48
            && ActualHeight >= zoneTravail.Height - 48;
    }

    /// <summary>
    /// Indique si la fenêtre couvre environ les deux tiers de la largeur de l'écran.
    /// </summary>
    private bool FenetreCouvreDeuxTiersEcran()
    {
        Rect zoneTravail = SystemParameters.WorkArea;
        double largeurCarteJeu = CarteJeuEnCours?.ActualWidth ?? 0;
        return ActualWidth >= zoneTravail.Width * RatioLargeurDispositionEtendue
            || largeurCarteJeu >= LargeurMinimaleCarteJeuDispositionEtendue;
    }

    /// <summary>
    /// Borne la hauteur de la carte "Jeu en cours" à la hauteur visible de la fenêtre.
    /// </summary>
    private void AjusterHauteurCarteJeuEnCours()
    {
        if (CarteJeuEnCours is null || ZonePrincipale is null || GrilleCartes is null)
        {
            return;
        }

        if (ZonePrincipale.Visibility != Visibility.Visible)
        {
            if (ConteneurZonePrincipale is not null)
            {
                ConteneurZonePrincipale.MinHeight = 0;
                ConteneurZonePrincipale.Height = double.NaN;
            }

            GrilleCartes.MinHeight = 0;
            GrilleCartes.Height = double.NaN;
            CarteJeuEnCours.Height = double.NaN;
            CarteJeuEnCours.MaxHeight = double.PositiveInfinity;
            CarteJeuEnCours.MinHeight = 0;
            if (CarteListeSuccesJeuEnCours is not null)
            {
                CarteListeSuccesJeuEnCours.Height = double.NaN;
                CarteListeSuccesJeuEnCours.MaxHeight = double.PositiveInfinity;
            }

            if (ConteneurGrilleTousSuccesJeuEnCours is not null)
            {
                ConteneurGrilleTousSuccesJeuEnCours.Height = double.NaN;
                ConteneurGrilleTousSuccesJeuEnCours.MaxHeight = double.PositiveInfinity;
            }

            if (ZoneVisibleListeSuccesJeuEnCours is not null)
            {
                ZoneVisibleListeSuccesJeuEnCours.Height = double.NaN;
                ZoneVisibleListeSuccesJeuEnCours.MaxHeight = double.PositiveInfinity;
            }
            return;
        }

        double hauteurVisible =
            (CadreZonePrincipale?.ActualHeight ?? 0)
            - (CadreZonePrincipale?.Padding.Top ?? 0)
            - (CadreZonePrincipale?.Padding.Bottom ?? 0);

        if (hauteurVisible <= 0)
        {
            _ = Dispatcher.BeginInvoke(
                (Action)AjusterHauteurCarteJeuEnCours,
                DispatcherPriority.Render
            );
            return;
        }

        Thickness margeConteneur = ConteneurZonePrincipale?.Margin ?? default;
        double hauteurCible = Math.Max(
            1,
            hauteurVisible - margeConteneur.Top - margeConteneur.Bottom
        );

        if (ConteneurZonePrincipale is not null)
        {
            ConteneurZonePrincipale.MinHeight = hauteurCible;
            ConteneurZonePrincipale.Height = double.NaN;
        }

        GrilleCartes.MinHeight = hauteurCible;
        GrilleCartes.Height = double.NaN;
        CarteJeuEnCours.MinHeight = hauteurCible;
        CarteJeuEnCours.Height = hauteurCible;
        CarteJeuEnCours.MaxHeight = hauteurCible;
        PlanifierAjustementHauteurListeSuccesJeuEnCours();
    }

    /// <summary>
    /// Planifie un seul recalcul de hauteur de la liste des succès à la fin du cycle de layout.
    /// </summary>
    private void PlanifierAjustementHauteurListeSuccesJeuEnCours()
    {
        if (_etatListeSuccesUi.AjustementHauteurPlanifie)
        {
            return;
        }

        _etatListeSuccesUi.AjustementHauteurPlanifie = true;
        _ = Dispatcher.BeginInvoke(
            () =>
            {
                _etatListeSuccesUi.AjustementHauteurPlanifie = false;
                AjusterHauteurListeSuccesJeuEnCours();
            },
            DispatcherPriority.Render
        );
    }

    /// <summary>
    /// Ajuste explicitement la hauteur de la liste des rétrosuccès à l'espace restant dans sa carte.
    /// </summary>
    private void AjusterHauteurListeSuccesJeuEnCours()
    {
        const double SeuilHauteurSectionAberrante = 80;
        const double SeuilHauteurVisibleAberrante = 48;

        if (
            CarteJeuEnCours is null
            || GrilleCarteJeuEnCours is null
            || GrilleTousSuccesJeuEnCours is null
            || CarteListeSuccesJeuEnCours is null
            || ZonePrincipaleListeSuccesJeuEnCours is null
            || ZoneVisibleListeSuccesJeuEnCours is null
            || ConteneurGrilleTousSuccesJeuEnCours is null
            || !CarteListeSuccesJeuEnCours.IsLoaded
        )
        {
            return;
        }

        double hauteurCarteJeu = GrilleCarteJeuEnCours.ActualHeight;

        if (hauteurCarteJeu <= 0)
        {
            return;
        }

        CarteJeuEnCours.UpdateLayout();
        GrilleCarteJeuEnCours.UpdateLayout();
        CarteListeSuccesJeuEnCours.UpdateLayout();
        ZonePrincipaleListeSuccesJeuEnCours.UpdateLayout();
        ZoneVisibleListeSuccesJeuEnCours.UpdateLayout();
        ConteneurGrilleTousSuccesJeuEnCours.UpdateLayout();

        double hauteurMaxSection = Math.Max(0, ZonePrincipaleListeSuccesJeuEnCours.ActualHeight);

        if (hauteurMaxSection <= 0)
        {
            return;
        }

        double largeurMaxSection = Math.Max(0, ZonePrincipaleListeSuccesJeuEnCours.ActualWidth);

        if (largeurMaxSection <= 0)
        {
            return;
        }

        Point positionDansCarte = ZoneVisibleListeSuccesJeuEnCours.TranslatePoint(
            new Point(0, 0),
            ZonePrincipaleListeSuccesJeuEnCours
        );
        double hauteurDisponible = hauteurMaxSection - positionDansCarte.Y;

        if (hauteurDisponible <= 0)
        {
            return;
        }

        double largeurDisponible = largeurMaxSection - positionDansCarte.X;

        if (largeurDisponible <= 0)
        {
            largeurDisponible = Math.Max(0, ZoneVisibleListeSuccesJeuEnCours.ActualWidth);
        }

        bool grilleChargee = GrilleTousSuccesJeuEnCours.Children.Count > 0;
        bool hauteurAberrante =
            hauteurMaxSection < SeuilHauteurSectionAberrante
            || hauteurDisponible < SeuilHauteurVisibleAberrante;

        if (grilleChargee && _etatListeSuccesUi.RedimensionnementFenetreActif)
        {
            JournaliserDimensionsListeSucces(
                "hauteur_liste_reportee",
                $"hauteurDisponible={hauteurDisponible:0.##};hauteurSection={hauteurMaxSection:0.##}"
            );
            return;
        }

        if (
            grilleChargee
            && hauteurAberrante
            && _etatListeSuccesUi.DerniereHauteurSectionStable > 0
            && _etatListeSuccesUi.DerniereHauteurVisibleStable > 0
        )
        {
            JournaliserDimensionsListeSucces(
                "hauteur_liste_ignoree",
                $"hauteurDisponible={hauteurDisponible:0.##};hauteurSection={hauteurMaxSection:0.##};stableVisible={_etatListeSuccesUi.DerniereHauteurVisibleStable:0.##};stableSection={_etatListeSuccesUi.DerniereHauteurSectionStable:0.##}"
            );
            return;
        }

        ZoneVisibleListeSuccesJeuEnCours.MinHeight = 0;
        ZoneVisibleListeSuccesJeuEnCours.Height = double.NaN;
        ZoneVisibleListeSuccesJeuEnCours.MaxHeight = hauteurDisponible;
        ConteneurGrilleTousSuccesJeuEnCours.MinHeight = 0;
        ConteneurGrilleTousSuccesJeuEnCours.Width = double.NaN;
        ConteneurGrilleTousSuccesJeuEnCours.Height = double.NaN;
        ConteneurGrilleTousSuccesJeuEnCours.MaxWidth = double.PositiveInfinity;
        ConteneurGrilleTousSuccesJeuEnCours.MaxHeight = double.PositiveInfinity;
        _etatListeSuccesUi.DerniereHauteurSectionStable = hauteurMaxSection;
        _etatListeSuccesUi.DerniereHauteurVisibleStable = hauteurDisponible;
        ZoneVisibleListeSuccesJeuEnCours.UpdateLayout();
        ConteneurGrilleTousSuccesJeuEnCours.UpdateLayout();
        JournaliserDimensionsListeSucces(
            "hauteur_liste_ajustee",
            $"largeurDisponible={largeurDisponible:0.##};hauteurDisponible={hauteurDisponible:0.##};largeurZone={largeurMaxSection:0.##};hauteurZone={hauteurMaxSection:0.##}"
        );
        AppliquerEcretageArrondiZoneSucces();
        DefinirVisibiliteBarreDefilementListeSucces(
            ConteneurGrilleTousSuccesJeuEnCours.IsMouseOver
        );
    }

    /// <summary>
    /// Affiche ou masque le contenu principal pendant l'ouverture de la modale de connexion.
    /// </summary>
    private void DefinirVisibiliteContenuPrincipal(bool afficher)
    {
        _vueModele.VisibiliteContenuPrincipal = afficher ? Visibility.Visible : Visibility.Hidden;
        AjusterHauteurCarteJeuEnCours();
    }

    /// <summary>
    /// Applique une géométrie sauvegardée si elle reste visible sur l'écran courant.
    /// </summary>
    private void AppliquerGeometrieFenetre()
    {
        Width = Math.Max(MinWidth, _configurationConnexion.LargeurFenetre);
        Height = Math.Max(MinHeight, _configurationConnexion.HauteurFenetre);

        if (
            _configurationConnexion.PositionGaucheFenetre is not double gauche
            || _configurationConnexion.PositionHautFenetre is not double haut
        )
        {
            return;
        }

        Rect zoneVisible = new(
            SystemParameters.VirtualScreenLeft,
            SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth,
            SystemParameters.VirtualScreenHeight
        );

        Rect zoneFenetre = new(gauche, haut, Width, Height);

        if (!zoneVisible.IntersectsWith(zoneFenetre))
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            return;
        }

        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = gauche;
        Top = haut;
    }

    /// <summary>
    /// Mémorise la géométrie courante de la fenêtre pour le prochain lancement.
    /// </summary>
    private void MemoriserGeometrieFenetre()
    {
        Rect geometrie =
            WindowState == WindowState.Normal ? new Rect(Left, Top, Width, Height) : RestoreBounds;

        _configurationConnexion.PositionGaucheFenetre = geometrie.Left;
        _configurationConnexion.PositionHautFenetre = geometrie.Top;
        _configurationConnexion.LargeurFenetre = Math.Max(MinWidth, geometrie.Width);
        _configurationConnexion.HauteurFenetre = Math.Max(MinHeight, geometrie.Height);
    }

    /// <summary>
    /// Récupère un rayon de coins partagé depuis les ressources de l'application.
    /// </summary>
    private CornerRadius ObtenirRayonCoins(string cleRessource, double valeurParDefaut)
    {
        if (TryFindResource(cleRessource) is CornerRadius rayon)
        {
            return rayon;
        }

        return new CornerRadius(valeurParDefaut);
    }
}
