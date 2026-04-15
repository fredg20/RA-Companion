using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using SystemControls = System.Windows.Controls;

/*
 * Regroupe les calculs de disposition, de hauteur et de défilement qui
 * maintiennent la fenêtre principale lisible selon la taille disponible.
 */
namespace RA.Compagnon;

/*
 * Porte les ajustements de layout dynamique de la fenêtre principale,
 * notamment pour les sections du jeu courant et la grille des succès.
 */
public partial class MainWindow
{
    private const uint MonitorDefaultToNearest = 2;

    [StructLayout(LayoutKind.Sequential)]
    private struct RectangleEcran
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct InformationsMoniteur
    {
        public uint cbSize;
        public RectangleEcran rcMonitor;
        public RectangleEcran rcWork;
        public uint dwFlags;
    }

    [LibraryImport("user32.dll")]
    private static partial nint MonitorFromWindow(nint hwnd, uint dwFlags);

    [LibraryImport("user32.dll", EntryPoint = "GetMonitorInfoW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetMonitorInfo(nint hMonitor, ref InformationsMoniteur lpmi);

    /*
     * Calcule la hauteur réellement occupée par un élément, marges comprises.
     */
    private static double CalculerHauteurOccupee(FrameworkElement element)
    {
        return element.ActualHeight + element.Margin.Top + element.Margin.Bottom;
    }

    /*
     * Retourne la zone de travail du moniteur qui contient réellement la
     * fenêtre courante, convertie dans les unités WPF.
     */
    private Rect ObtenirZoneTravailFenetreCourante()
    {
        nint handle = new WindowInteropHelper(this).Handle;

        if (handle == nint.Zero)
        {
            return SystemParameters.WorkArea;
        }

        nint moniteur = MonitorFromWindow(handle, MonitorDefaultToNearest);

        if (moniteur == nint.Zero)
        {
            return SystemParameters.WorkArea;
        }

        InformationsMoniteur informationsMoniteur = new()
        {
            cbSize = (uint)Marshal.SizeOf<InformationsMoniteur>(),
        };

        if (!GetMonitorInfo(moniteur, ref informationsMoniteur))
        {
            return SystemParameters.WorkArea;
        }

        PresentationSource? sourcePresentation = PresentationSource.FromVisual(this);

        if (sourcePresentation?.CompositionTarget is null)
        {
            return SystemParameters.WorkArea;
        }

        Point coinSuperieurGauche =
            sourcePresentation.CompositionTarget.TransformFromDevice.Transform(
                new Point(informationsMoniteur.rcWork.Left, informationsMoniteur.rcWork.Top)
            );
        Point coinInferieurDroit =
            sourcePresentation.CompositionTarget.TransformFromDevice.Transform(
                new Point(informationsMoniteur.rcWork.Right, informationsMoniteur.rcWork.Bottom)
            );

        return new Rect(coinSuperieurGauche, coinInferieurDroit);
    }

    /*
     * Calcule le ratio de largeur occupé par la fenêtre sur la zone utile
     * de son écran courant.
     */
    private double ObtenirRatioLargeurFenetre(Rect zoneTravail)
    {
        return zoneTravail.Width <= 0 ? 1 : ActualWidth / zoneTravail.Width;
    }

    /*
     * Met à jour la largeur minimale de la fenêtre pour empêcher un rendu
     * plus étroit qu'un quart d'écran.
     */
    private void MettreAJourLargeurMinimaleFenetre(Rect zoneTravail)
    {
        if (zoneTravail.Width <= 0)
        {
            return;
        }

        double largeurMinimale = Math.Round(
            zoneTravail.Width * ConstantesDesign.RatioLargeurMinimaleFenetre,
            2,
            MidpointRounding.AwayFromZero
        );

        if (Math.Abs(MinWidth - largeurMinimale) > 0.01)
        {
            MinWidth = largeurMinimale;
        }
    }

    /*
     * Affiche brièvement la barre de défilement principale lorsqu'une
     * interaction utilisateur suggère qu'elle peut être utile.
     */
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

    /*
     * Recherche puis mémorise la barre de défilement verticale de la zone
     * principale pour éviter de rescanner l'arbre visuel à chaque appel.
     */
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

    /*
     * Recherche puis conserve la barre de défilement de la liste de succès
     * afin de piloter sa visibilité de manière explicite.
     */
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

    /*
     * Masque complètement la barre de défilement principale quand elle ne
     * doit pas être visible ni interactive.
     */
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

    /*
     * Active ou masque la barre de défilement de la grille de succès selon
     * l'état de survol et la possibilité réelle de défiler.
     */
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

    /*
     * Détermine si la souris se trouve dans la bande latérale réservée à la
     * détection de la barre de défilement.
     */
    private static bool EstDansZoneBarreDefilement(
        SystemControls.ScrollViewer scrollViewer,
        Point position
    )
    {
        return position.X
            >= Math.Max(0, scrollViewer.ActualWidth - LargeurZoneDetectionBarreDefilement);
    }

    /*
     * Indique si la zone principale possède réellement un contenu défilable.
     */
    private bool ZonePrincipalePeutDefiler()
    {
        return ZonePrincipale is not null && ZonePrincipale.ScrollableHeight > 0;
    }

    /*
     * Indique si la liste complète des succès dépasse la hauteur visible.
     */
    private bool ListeSuccesPeutDefiler()
    {
        return ConteneurGrilleTousSuccesJeuEnCours is not null
            && ConteneurGrilleTousSuccesJeuEnCours.ScrollableHeight > 0;
    }

    /*
     * Vérifie si la souris survole actuellement la zone sensible de la barre
     * de défilement principale.
     */
    private bool SourisSurvoleZoneBarreDefilement()
    {
        if (ZonePrincipale is null || !ZonePrincipale.IsMouseOver)
        {
            return false;
        }

        Point position = Mouse.GetPosition(ZonePrincipale);
        return EstDansZoneBarreDefilement(ZonePrincipale, position);
    }

    /*
     * Réagit à la molette sur la zone principale en révélant temporairement
     * la barre de défilement.
     */
    private void ZonePrincipale_ApercuMoletteSouris(object sender, MouseWheelEventArgs e)
    {
        AfficherTemporairementBarreDefilementPrincipale();
    }

    /*
     * Ajuste la visibilité de la barre principale lorsque la souris circule
     * sur la zone de défilement.
     */
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

    /*
     * Masque la barre principale à la sortie de la souris si aucun minuteur
     * de délai n'est encore actif.
     */
    private void ZonePrincipale_SortieSouris(object sender, MouseEventArgs e)
    {
        if (!_minuteurMasquageBarreDefilement.IsEnabled)
        {
            DefinirVisibiliteBarreDefilementPrincipale();
        }
    }

    /*
     * Réaffiche temporairement la barre principale lorsqu'un défilement
     * vertical effectif est détecté.
     */
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

    /*
     * Termine la fenêtre de visibilité temporaire de la barre principale
     * lorsque la souris n'est plus sur la zone sensible.
     */
    private void MinuteurMasquageBarreDefilement_Tick(object? sender, EventArgs e)
    {
        if (SourisSurvoleZoneBarreDefilement())
        {
            return;
        }

        _minuteurMasquageBarreDefilement.Stop();
        DefinirVisibiliteBarreDefilementPrincipale();
    }

    /*
     * Affiche la barre de défilement de la liste de succès à l'entrée de la
     * souris pour rendre le défilement explicite.
     */
    private void ConteneurGrilleTousSuccesJeuEnCours_EntreeSouris(object sender, MouseEventArgs e)
    {
        DefinirVisibiliteBarreDefilementListeSucces(
            visible: ConteneurGrilleTousSuccesJeuEnCours?.IsMouseOver == true
        );
        JournaliserDiagnosticListeSucces("liste_mouseenter");
    }

    /*
     * Masque la barre de la liste à la sortie de la souris et relance au
     * besoin le minuteur de reprise d'animation.
     */
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

    /*
     * Bascule la liste de succès en interaction manuelle au début d'un
     * glisser ou d'un clic maintenu.
     */
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

    /*
     * Finalise l'interaction de liste lorsque le bouton gauche est relâché.
     */
    private void ConteneurGrilleTousSuccesJeuEnCours_ApercuBoutonGaucheHaut(
        object sender,
        MouseButtonEventArgs e
    )
    {
        FinaliserInteractionListeSucces();
    }

    /*
     * Finalise l'interaction de liste quand la capture souris est perdue et
     * qu'aucun glisser n'est encore en cours.
     */
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

    /*
     * Déclenche les recalculs de layout de la liste lorsqu'un changement de
     * viewport ou d'étendue vient modifier l'espace disponible.
     */
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

    /*
     * Réinitialise l'état d'interaction manuel de la liste de succès et
     * prépare la reprise éventuelle de son animation automatique.
     */
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

    /*
     * Réagit aux changements de taille du cadre des succès pour invalider les
     * mesures et reprogrammer les ajustements différés nécessaires.
     */
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

    /*
     * Planifie un relayout complet de la liste après une phase de
     * redimensionnement de fenêtre.
     */
    private void PlanifierRelayoutListeSuccesApresRedimensionnement()
    {
        _minuteurRelayoutApresRedimensionnement.Stop();
        _minuteurRelayoutApresRedimensionnement.Start();
    }

    /*
     * Exécute le relayout différé après redimensionnement pour restaurer des
     * dimensions stables sur les cartes et la grille de succès.
     */
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

    /*
     * Réorganise la grille principale entre une disposition simple ou double
     * selon la largeur courante de la fenêtre.
     */
    private void AjusterDisposition()
    {
        Rect zoneTravail = ObtenirZoneTravailFenetreCourante();
        double ratioLargeur = ObtenirRatioLargeurFenetre(zoneTravail);
        bool dispositionDouble = ratioLargeur >= ConstantesDesign.RatioDispositionIntermediaire;
        bool carteConnexionVisible = _vueModele.VisibiliteCarteConnexion == Visibility.Visible;

        MettreAJourLargeurMinimaleFenetre(zoneTravail);

        GrilleCartes.RowDefinitions.Clear();

        if (dispositionDouble)
        {
            GrilleCartes.ColumnDefinitions[0].Width = carteConnexionVisible
                ? new GridLength(280)
                : new GridLength(1, GridUnitType.Star);
            GrilleCartes.ColumnDefinitions[1].Width = carteConnexionVisible
                ? new GridLength(ConstantesDesign.EspaceEtendu)
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
                    new SystemControls.RowDefinition
                    {
                        Height = new GridLength(ConstantesDesign.EspaceEtendu),
                    }
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
        AjusterDispositionCapsulesJeuEnCours();
        AjusterHauteurCarteJeuEnCours();
    }

    /*
     * Répartit les trois sections du jeu courant selon les seuils de largeur
     * pour passer en pile, en deux colonnes ou en trois colonnes.
     */
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

        Rect zoneTravail = ObtenirZoneTravailFenetreCourante();
        double ratioLargeur = ObtenirRatioLargeurFenetre(zoneTravail);
        bool dispositionTriple = FenetreCouvreEcranPourDispositionTriple(ratioLargeur);
        bool dispositionEtendue = !dispositionTriple && FenetreCouvreDeuxTiersEcran(ratioLargeur);
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
            GrilleCarteJeuEnCours.ColumnDefinitions[1].Width = new GridLength(
                ConstantesDesign.EspaceEtendu
            );
            GrilleCarteJeuEnCours.ColumnDefinitions[2].Width = new GridLength(1, GridUnitType.Star);
            GrilleCarteJeuEnCours.ColumnDefinitions[3].Width = new GridLength(
                ConstantesDesign.EspaceEtendu
            );
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
            GrilleCarteJeuEnCours.ColumnDefinitions[1].Width = new GridLength(
                ConstantesDesign.EspaceEtendu
            );
            GrilleCarteJeuEnCours.ColumnDefinitions[2].Width = new GridLength(1, GridUnitType.Star);
            GrilleCarteJeuEnCours.ColumnDefinitions[3].Width = new GridLength(0);
            GrilleCarteJeuEnCours.ColumnDefinitions[4].Width = new GridLength(0);

            GrilleCarteJeuEnCours.RowDefinitions[0].Height = GridLength.Auto;
            GrilleCarteJeuEnCours.RowDefinitions[1].Height = new GridLength(6);
            GrilleCarteJeuEnCours.RowDefinitions[2].Height = GridLength.Auto;
            GrilleCarteJeuEnCours.RowDefinitions[3].Height = new GridLength(
                ConstantesDesign.EspaceStandard
            );
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
            GrilleCarteJeuEnCours.RowDefinitions[3].Height = new GridLength(
                ConstantesDesign.EspaceStandard
            );
            GrilleCarteJeuEnCours.RowDefinitions[4].Height = GridLength.Auto;
            GrilleCarteJeuEnCours.RowDefinitions[5].Height = new GridLength(
                ConstantesDesign.EspaceStandard
            );
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

    /*
     * Réagit au changement de taille du bloc des capsules d'information du
     * jeu pour recalculer leur nombre de colonnes.
     */
    private void GrilleInformationsJeuEnCours_TailleChangee(object sender, SizeChangedEventArgs e)
    {
        AjusterDispositionCapsulesJeuEnCours();
    }

    /*
     * Réorganise les capsules d'information du jeu en 1, 2 ou 3 colonnes
     * selon la largeur réellement disponible dans la carte.
     */
    private void AjusterDispositionCapsulesJeuEnCours()
    {
        if (
            GrilleInformationsJeuEnCours is null
            || ZoneConsoleJeuEnCours is null
            || EtiquetteTypeJeuEnCours is null
            || EtiquetteDateSortieJeuEnCours is null
            || EtiquetteCreditsJeuEnCours is null
        )
        {
            return;
        }

        FrameworkElement[] capsulesVisibles =
        [
            ZoneConsoleJeuEnCours,
            EtiquetteTypeJeuEnCours,
            EtiquetteDateSortieJeuEnCours,
            EtiquetteCreditsJeuEnCours,
        ];

        capsulesVisibles = capsulesVisibles
            .Where(capsule => capsule.Visibility == Visibility.Visible)
            .ToArray();

        GrilleInformationsJeuEnCours.ColumnDefinitions.Clear();
        GrilleInformationsJeuEnCours.RowDefinitions.Clear();

        if (capsulesVisibles.Length == 0)
        {
            return;
        }

        double largeurDisponible = Math.Max(0, GrilleInformationsJeuEnCours.ActualWidth);
        double largeurMinimaleCapsule = ConstantesDesign.LargeurMinimaleCapsuleInformation;
        double espacement = ConstantesDesign.EspaceCompact;

        int nombreColonnes = 1;

        if (largeurDisponible >= (largeurMinimaleCapsule * 3) + (espacement * 2))
        {
            nombreColonnes = 3;
        }
        else if (largeurDisponible >= (largeurMinimaleCapsule * 2) + espacement)
        {
            nombreColonnes = 2;
        }

        for (int indexColonne = 0; indexColonne < nombreColonnes; indexColonne++)
        {
            GrilleInformationsJeuEnCours.ColumnDefinitions.Add(
                new SystemControls.ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
            );
        }

        int nombreRangees = (int)Math.Ceiling(capsulesVisibles.Length / (double)nombreColonnes);

        for (int indexRangee = 0; indexRangee < nombreRangees; indexRangee++)
        {
            GrilleInformationsJeuEnCours.RowDefinitions.Add(
                new SystemControls.RowDefinition { Height = GridLength.Auto }
            );
        }

        for (int indexCapsule = 0; indexCapsule < capsulesVisibles.Length; indexCapsule++)
        {
            FrameworkElement capsule = capsulesVisibles[indexCapsule];
            int colonne = indexCapsule % nombreColonnes;
            int rangee = indexCapsule / nombreColonnes;
            bool derniereColonne = colonne == nombreColonnes - 1;
            bool derniereRangee = rangee == nombreRangees - 1;

            SystemControls.Grid.SetColumn(capsule, colonne);
            SystemControls.Grid.SetRow(capsule, rangee);
            capsule.HorizontalAlignment = HorizontalAlignment.Stretch;
            capsule.Margin = new Thickness(
                0,
                0,
                derniereColonne ? 0 : espacement,
                derniereRangee ? 0 : espacement
            );
        }
    }

    /*
     * Détermine si la fenêtre atteint le seuil de trois colonnes, fixé à
     * partir de trois quarts de la largeur de l'écran courant.
     */
    private bool FenetreCouvreEcranPourDispositionTriple(double ratioLargeur)
    {
        if (WindowState == WindowState.Maximized)
        {
            return true;
        }

        return ratioLargeur >= ConstantesDesign.RatioDispositionTriple;
    }

    /*
     * Détermine si la fenêtre atteint le seuil intermédiaire permettant la
     * disposition en deux temps à partir de la moitié de l'écran.
     */
    private bool FenetreCouvreDeuxTiersEcran(double ratioLargeur)
    {
        return ratioLargeur >= ConstantesDesign.RatioDispositionIntermediaire;
    }

    /*
     * Ajuste la hauteur globale de la carte principale du jeu courant pour
     * qu'elle suive exactement la hauteur visible de la zone centrale.
     */
    private void AjusterHauteurCarteJeuEnCours()
    {
        if (CarteJeuEnCours is null || ZonePrincipale is null || GrilleCartes is null)
        {
            return;
        }

        bool accueilVisible =
            _vueModele.VisibiliteModuleAccueil == Visibility.Visible
            && CadreZonePrincipale?.Visibility == Visibility.Visible
            && ZonePrincipale.Visibility == Visibility.Visible
            && ZonePrincipale.IsVisible;

        if (!accueilVisible)
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

    /*
     * Programme l'ajustement différé de la hauteur de la liste complète des
     * succès afin d'éviter les recalculs synchrones répétés.
     */
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

    /*
     * Recalcule la hauteur visible allouée à la liste des succès en tenant
     * compte de la place réellement disponible dans sa section.
     */
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

    /*
     * Masque ou affiche le contenu principal tout en réappliquant les
     * contraintes de hauteur de la carte du jeu courant.
     */
    private void DefinirVisibiliteContenuPrincipal(bool afficher)
    {
        _vueModele.VisibiliteContenuPrincipal = afficher ? Visibility.Visible : Visibility.Hidden;
        _vueModele.VisibiliteBarreModules = afficher ? Visibility.Visible : Visibility.Collapsed;
        AjusterHauteurCarteJeuEnCours();
    }

    /*
     * Restaure la géométrie sauvegardée de la fenêtre lorsqu'elle reste
     * visible sur l'un des écrans disponibles.
     */
    private void AppliquerGeometrieFenetre()
    {
        MettreAJourLargeurMinimaleFenetre(ObtenirZoneTravailFenetreCourante());
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

    /*
     * Sauvegarde la position et la taille courantes de la fenêtre pour la
     * prochaine ouverture de l'application.
     */
    private void MemoriserGeometrieFenetre()
    {
        Rect geometrie =
            WindowState == WindowState.Normal ? new Rect(Left, Top, Width, Height) : RestoreBounds;

        _configurationConnexion.PositionGaucheFenetre = geometrie.Left;
        _configurationConnexion.PositionHautFenetre = geometrie.Top;
        _configurationConnexion.LargeurFenetre = Math.Max(MinWidth, geometrie.Width);
        _configurationConnexion.HauteurFenetre = Math.Max(MinHeight, geometrie.Height);
    }

    /*
     * Lit un rayon de coins dans les ressources WPF et fournit une valeur de
     * repli lorsque la ressource n'existe pas.
     */
    private CornerRadius ObtenirRayonCoins(string cleRessource, double valeurParDefaut)
    {
        if (TryFindResource(cleRessource) is CornerRadius rayon)
        {
            return rayon;
        }

        return new CornerRadius(valeurParDefaut);
    }
}
