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
        DefinirVisibiliteBarreDefilementListeSucces(visible: true);
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
        if (Math.Abs(e.VerticalChange) <= 0.01)
        {
            return;
        }

        // Mémorise le sens réel du mouvement observé pour que l'autodéfilement
        // reparte du bon côté après une interruption ou un déplacement manuel.
        _etatListeSuccesUi.AnimationVersBas = e.VerticalChange > 0;

        _etatListeSuccesUi.DernierOffsetInteraction =
            ConteneurGrilleTousSuccesJeuEnCours?.VerticalOffset
            ?? _etatListeSuccesUi.DernierOffsetInteraction;
        JournaliserDiagnosticListeSucces("liste_scrollchanged", $"delta={e.VerticalChange:0.##}");

        DefinirVisibiliteBarreDefilementListeSucces(
            ConteneurGrilleTousSuccesJeuEnCours?.IsMouseOver == true
        );

        if (!_etatListeSuccesUi.InteractionActive)
        {
            return;
        }

        _minuteurRepriseAnimationGrilleSucces.Stop();
        _minuteurRepriseAnimationGrilleSucces.Start();
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
    /// Bascule entre un affichage sur une colonne ou deux colonnes selon la largeur disponible.
    /// </summary>
    private void AjusterDisposition()
    {
        bool dispositionDouble = ActualWidth >= LargeurMinimaleDispositionDouble;
        bool carteConnexionVisible = CarteConnexion?.Visibility == Visibility.Visible;

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

        AjusterHauteurCarteJeuEnCours();
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
            return;
        }

        double hauteurTitre = 36;
        double hauteurBandeauCompte =
            Math.Max(BoutonCompteUtilisateur?.ActualHeight ?? 32, BoutonAide?.ActualHeight ?? 32)
            + 8
            + 6;

        double hauteurVisible =
            ActualHeight
            - hauteurTitre
            - hauteurBandeauCompte
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
        if (
            CarteJeuEnCours is null
            || GrilleTousSuccesJeuEnCours is null
            || CarteListeSuccesJeuEnCours is null
            || ConteneurGrilleTousSuccesJeuEnCours is null
            || !CarteListeSuccesJeuEnCours.IsLoaded
        )
        {
            return;
        }

        double hauteurCarteJeu = CarteJeuEnCours.ActualHeight;

        if (hauteurCarteJeu <= 0)
        {
            return;
        }

        CarteJeuEnCours.UpdateLayout();
        CarteListeSuccesJeuEnCours.UpdateLayout();
        ConteneurGrilleTousSuccesJeuEnCours.UpdateLayout();

        Point positionSectionDansCarteJeu = CarteListeSuccesJeuEnCours.TranslatePoint(
            new Point(0, 0),
            CarteJeuEnCours
        );
        double hauteurMaxSection = hauteurCarteJeu - positionSectionDansCarteJeu.Y;

        if (hauteurMaxSection <= 0)
        {
            return;
        }

        Point positionDansCarte = ConteneurGrilleTousSuccesJeuEnCours.TranslatePoint(
            new Point(0, 0),
            CarteListeSuccesJeuEnCours
        );
        double hauteurDisponible = hauteurMaxSection - positionDansCarte.Y;

        if (hauteurDisponible <= 0)
        {
            return;
        }

        CarteListeSuccesJeuEnCours.MinHeight = 0;
        CarteListeSuccesJeuEnCours.Height = hauteurMaxSection;
        CarteListeSuccesJeuEnCours.MaxHeight = hauteurMaxSection;
        ConteneurGrilleTousSuccesJeuEnCours.MinHeight = 0;
        ConteneurGrilleTousSuccesJeuEnCours.Height = double.NaN;
        ConteneurGrilleTousSuccesJeuEnCours.MaxHeight = hauteurDisponible;
        ConteneurGrilleTousSuccesJeuEnCours.UpdateLayout();
        DefinirVisibiliteBarreDefilementListeSucces(
            ConteneurGrilleTousSuccesJeuEnCours.IsMouseOver
        );
    }

    /// <summary>
    /// Affiche ou masque le contenu principal pendant l'ouverture de la modale de connexion.
    /// </summary>
    private void DefinirVisibiliteContenuPrincipal(bool afficher)
    {
        ZonePrincipale.Visibility = afficher ? Visibility.Visible : Visibility.Hidden;
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
