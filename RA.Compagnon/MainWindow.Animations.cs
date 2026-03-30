using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace RA.Compagnon;

public partial class MainWindow
{
    public static readonly DependencyProperty OffsetVerticalAnimeProperty =
        DependencyProperty.RegisterAttached(
            "OffsetVerticalAnime",
            typeof(double),
            typeof(MainWindow),
            new PropertyMetadata(0d, OffsetVerticalAnimeChange)
        );

    public static double GetOffsetVerticalAnime(DependencyObject objet)
    {
        return (double)objet.GetValue(OffsetVerticalAnimeProperty);
    }

    public static void SetOffsetVerticalAnime(DependencyObject objet, double valeur)
    {
        objet.SetValue(OffsetVerticalAnimeProperty, valeur);
    }

    private static void OffsetVerticalAnimeChange(
        DependencyObject objet,
        DependencyPropertyChangedEventArgs e
    )
    {
        if (objet is not System.Windows.Controls.ScrollViewer scrollViewer)
        {
            return;
        }

        if (e.NewValue is not double offset)
        {
            return;
        }

        if (double.IsNaN(offset) || double.IsInfinity(offset))
        {
            return;
        }

        double offsetBorne = Math.Clamp(offset, 0, scrollViewer.ScrollableHeight);
        scrollViewer.ScrollToVerticalOffset(offsetBorne);
    }

    /// <summary>
    /// Planifie le recalcul de la hauteur visible de la grille des succès.
    /// </summary>
    private void PlanifierMiseAJourAnimationGrilleTousSucces()
    {
        if (_miseAJourAnimationGrilleSuccesPlanifiee)
        {
            return;
        }

        _miseAJourAnimationGrilleSuccesPlanifiee = true;
        _ = Dispatcher.BeginInvoke(
            () =>
            {
                _miseAJourAnimationGrilleSuccesPlanifiee = false;
                MettreAJourAnimationGrilleTousSucces();
            },
            DispatcherPriority.Render
        );
    }

    /// <summary>
    /// Borne la hauteur visible de la grille des succès et anime son contenu en rebond si nécessaire.
    /// </summary>
    private void MettreAJourAnimationGrilleTousSucces()
    {
        if (
            ConteneurGrilleTousSuccesJeuEnCours is null
            || GrilleTousSuccesJeuEnCours is null
            || ZonePrincipale is null
        )
        {
            return;
        }

        if (GrilleTousSuccesJeuEnCours.Children.Count == 0)
        {
            JournaliserDiagnosticListeSucces("animation_maj_vide");
            _signatureAnimationGrilleSucces = string.Empty;
            _amplitudeAnimationGrilleSucces = 0;
            _dernierOffsetInteractionListeSucces = 0;
            ArreterAnimationGrilleSucces();
            GrilleTousSuccesJeuEnCours.Width = double.NaN;
            GrilleTousSuccesJeuEnCours.Height = double.NaN;
            ConteneurGrilleTousSuccesJeuEnCours.MaxHeight = double.PositiveInfinity;
            ConteneurGrilleTousSuccesJeuEnCours.Height = double.NaN;
            ConteneurGrilleTousSuccesJeuEnCours.ScrollToVerticalOffset(0);
            DefinirVisibiliteBarreDefilementListeSucces(visible: false);
            return;
        }
        GrilleTousSuccesJeuEnCours.Measure(
            new Size(ConteneurGrilleTousSuccesJeuEnCours.ActualWidth, double.PositiveInfinity)
        );
        double hauteurContenu = GrilleTousSuccesJeuEnCours.DesiredSize.Height;
        double hauteurVisible = Math.Max(0, ConteneurGrilleTousSuccesJeuEnCours.ViewportHeight);

        if (hauteurVisible <= 0)
        {
            hauteurVisible = Math.Max(0, ConteneurGrilleTousSuccesJeuEnCours.ActualHeight);
        }

        GrilleTousSuccesJeuEnCours.Width = ConteneurGrilleTousSuccesJeuEnCours.ActualWidth;
        GrilleTousSuccesJeuEnCours.Height = hauteurContenu;
        _amplitudeAnimationGrilleSucces = Math.Max(0, hauteurContenu - hauteurVisible);

        string signatureAnimation =
            $"{GrilleTousSuccesJeuEnCours.Children.Count}|{Math.Round(hauteurContenu, 1, MidpointRounding.AwayFromZero)}|{Math.Round(hauteurVisible, 1, MidpointRounding.AwayFromZero)}|{Math.Round(_amplitudeAnimationGrilleSucces, 1, MidpointRounding.AwayFromZero)}";

        if (
            string.Equals(
                _signatureAnimationGrilleSucces,
                signatureAnimation,
                StringComparison.Ordinal
            )
        )
        {
            JournaliserDiagnosticListeSucces("animation_maj_skip_signature");
            return;
        }

        ArreterAnimationGrilleSucces();
        _signatureAnimationGrilleSucces = signatureAnimation;
        double offsetReference = Math.Clamp(
            ConteneurGrilleTousSuccesJeuEnCours.VerticalOffset,
            0,
            _amplitudeAnimationGrilleSucces
        );
        ConteneurGrilleTousSuccesJeuEnCours.ScrollToVerticalOffset(offsetReference);
        JournaliserDiagnosticListeSucces(
            "animation_maj_recalculee",
            $"signature={signatureAnimation};offsetRef={offsetReference:0.##}"
        );

        if (_amplitudeAnimationGrilleSucces > SeuilDeclenchementDefilementGrilleSucces)
        {
            DemarrerAnimationGrilleSuccesDepuisPosition(
                offsetReference,
                _amplitudeAnimationGrilleSucces,
                allerVersBas: _animationGrilleSuccesVersBas
            );
        }

        DefinirVisibiliteBarreDefilementListeSucces(ConteneurGrilleTousSuccesJeuEnCours.IsMouseOver);
    }

    /// <summary>
    /// Arrête l'animation verticale de la grille et réinitialise sa position.
    /// </summary>
    private void ArreterAnimationGrilleSucces()
    {
        if (ConteneurGrilleTousSuccesJeuEnCours is null)
        {
            _horlogeAnimationGrilleSucces?.Controller?.Stop();
            _horlogeAnimationGrilleSucces = null;
            return;
        }

        double offsetCourant = ConteneurGrilleTousSuccesJeuEnCours.VerticalOffset;
        JournaliserDiagnosticListeSucces(
            "animation_arret_net",
            $"offset={offsetCourant:0.##}"
        );
        _horlogeAnimationGrilleSucces?.Controller?.Stop();
        _horlogeAnimationGrilleSucces = null;

        // Conserve la position courante quand on coupe l'autodéfilement,
        // sinon la propriété animée retombe à sa valeur par défaut et la liste
        // saute visuellement tout en haut au survol.
        SetOffsetVerticalAnime(ConteneurGrilleTousSuccesJeuEnCours, offsetCourant);
        ConteneurGrilleTousSuccesJeuEnCours.ApplyAnimationClock(OffsetVerticalAnimeProperty, null);
        ConteneurGrilleTousSuccesJeuEnCours.ScrollToVerticalOffset(offsetCourant);
    }

    /// <summary>
    /// Ralentit puis fige l'autodéfilement au lieu de l'arrêter net.
    /// </summary>
    private void ArreterAnimationGrilleSuccesEnDouceur()
    {
        if (ConteneurGrilleTousSuccesJeuEnCours is null || _horlogeAnimationGrilleSucces is null)
        {
            ArreterAnimationGrilleSucces();
            return;
        }

        double offsetCourant = Math.Clamp(
            ConteneurGrilleTousSuccesJeuEnCours.VerticalOffset,
            0,
            Math.Max(_amplitudeAnimationGrilleSucces, ConteneurGrilleTousSuccesJeuEnCours.ScrollableHeight)
        );
        double distanceRestante = _animationGrilleSuccesVersBas
            ? Math.Max(0, _amplitudeAnimationGrilleSucces - offsetCourant)
            : Math.Max(0, offsetCourant);

        if (distanceRestante <= 0.5)
        {
            ArreterAnimationGrilleSucces();
            return;
        }

        double distanceRalentissement = Math.Min(18, distanceRestante);
        double cible = _animationGrilleSuccesVersBas
            ? offsetCourant + distanceRalentissement
            : offsetCourant - distanceRalentissement;
        JournaliserDiagnosticListeSucces(
            "animation_arret_doux_debut",
            $"offset={offsetCourant:0.##};cible={cible:0.##};reste={distanceRestante:0.##}"
        );

        _horlogeAnimationGrilleSucces.Controller?.Stop();
        _horlogeAnimationGrilleSucces = null;
        SetOffsetVerticalAnime(ConteneurGrilleTousSuccesJeuEnCours, offsetCourant);
        ConteneurGrilleTousSuccesJeuEnCours.ApplyAnimationClock(OffsetVerticalAnimeProperty, null);
        ConteneurGrilleTousSuccesJeuEnCours.ScrollToVerticalOffset(offsetCourant);

        DoubleAnimation animationRalentissement = new()
        {
            From = offsetCourant,
            To = Math.Clamp(cible, 0, _amplitudeAnimationGrilleSucces),
            Duration = TimeSpan.FromMilliseconds(240),
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseOut },
            FillBehavior = FillBehavior.Stop,
        };

        AnimationClock horlogeRalentissement = animationRalentissement.CreateClock();
        _horlogeAnimationGrilleSucces = horlogeRalentissement;
        horlogeRalentissement.Completed += (_, _) =>
        {
            if (!ReferenceEquals(_horlogeAnimationGrilleSucces, horlogeRalentissement))
            {
                return;
            }

            double offsetFinal = Math.Clamp(
                animationRalentissement.To ?? offsetCourant,
                0,
                _amplitudeAnimationGrilleSucces
            );
            _dernierOffsetInteractionListeSucces = offsetFinal;
            SetOffsetVerticalAnime(ConteneurGrilleTousSuccesJeuEnCours, offsetFinal);
            ConteneurGrilleTousSuccesJeuEnCours.ApplyAnimationClock(
                OffsetVerticalAnimeProperty,
                null
            );
            ConteneurGrilleTousSuccesJeuEnCours.ScrollToVerticalOffset(offsetFinal);
            _horlogeAnimationGrilleSucces = null;
            JournaliserDiagnosticListeSucces(
                "animation_arret_doux_fin",
                $"offsetFinal={offsetFinal:0.##}"
            );
        };

        ConteneurGrilleTousSuccesJeuEnCours.ApplyAnimationClock(
            OffsetVerticalAnimeProperty,
            horlogeRalentissement
        );
    }

    /// <summary>
    /// Replace la grille des succès à sa position d'origine après un changement de jeu.
    /// </summary>
    private void ReinitialiserPositionGrilleTousSucces()
    {
        if (ConteneurGrilleTousSuccesJeuEnCours is null)
        {
            return;
        }

        ArreterAnimationGrilleSucces();
        _dernierOffsetInteractionListeSucces = 0;
        ConteneurGrilleTousSuccesJeuEnCours.ScrollToVerticalOffset(0);
    }

    /// <summary>
    /// Démarre l'animation verticale de la grille depuis un offset donné.
    /// </summary>
    private void DemarrerAnimationGrilleSuccesDepuisPosition(
        double offsetInitial,
        double amplitude,
        bool allerVersBas
    )
    {
        if (ConteneurGrilleTousSuccesJeuEnCours is null)
        {
            return;
        }

        double offsetDepart = Math.Clamp(offsetInitial, 0, amplitude);
        SetOffsetVerticalAnime(ConteneurGrilleTousSuccesJeuEnCours, offsetDepart);
        ConteneurGrilleTousSuccesJeuEnCours.ScrollToVerticalOffset(offsetDepart);
        _animationGrilleSuccesVersBas = allerVersBas;

        if (offsetDepart <= 0.5)
        {
            DemarrerAnimationGrilleSuccesCyclique(amplitude, true);
            return;
        }

        if (Math.Abs(offsetDepart - amplitude) <= 0.5)
        {
            DemarrerAnimationGrilleSuccesCyclique(amplitude, false);
            return;
        }

        double ciblePremierTrajet = allerVersBas ? amplitude : 0;
        double distancePremierTrajet = Math.Abs(ciblePremierTrajet - offsetDepart);
        double dureePremierTrajet = Math.Clamp(
            distancePremierTrajet / VitesseDefilementGrilleSuccesPixelsParSeconde,
            0.8,
            16
        );
        DoubleAnimation animationPremierTrajet = new()
        {
            From = offsetDepart,
            To = ciblePremierTrajet,
            Duration = TimeSpan.FromSeconds(dureePremierTrajet),
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
            FillBehavior = FillBehavior.Stop,
        };

        AnimationClock horlogePremierTrajet = animationPremierTrajet.CreateClock();
        _horlogeAnimationGrilleSucces = horlogePremierTrajet;
        horlogePremierTrajet.Completed += (_, _) =>
        {
            if (!ReferenceEquals(_horlogeAnimationGrilleSucces, horlogePremierTrajet))
            {
                return;
            }

            SetOffsetVerticalAnime(
                ConteneurGrilleTousSuccesJeuEnCours,
                Math.Clamp(ciblePremierTrajet, 0, amplitude)
            );
            ConteneurGrilleTousSuccesJeuEnCours.ApplyAnimationClock(
                OffsetVerticalAnimeProperty,
                null
            );
            ConteneurGrilleTousSuccesJeuEnCours.ScrollToVerticalOffset(
                Math.Clamp(ciblePremierTrajet, 0, amplitude)
            );
            _horlogeAnimationGrilleSucces = null;

            if (_survolBadgeGrilleSuccesActif)
            {
                return;
            }

            DemarrerAnimationGrilleSuccesCyclique(amplitude, departEnHaut: ciblePremierTrajet <= 0.5);
        };

        ConteneurGrilleTousSuccesJeuEnCours.ApplyAnimationClock(
            OffsetVerticalAnimeProperty,
            horlogePremierTrajet
        );

        if (_survolBadgeGrilleSuccesActif || _interactionListeSuccesActive)
        {
            horlogePremierTrajet.Controller?.Pause();
        }
    }

    /// <summary>
    /// Démarre un cycle de rebond complet entre le haut et le bas de la grille.
    /// </summary>
    private void DemarrerAnimationGrilleSuccesCyclique(double amplitude, bool departEnHaut)
    {
        if (ConteneurGrilleTousSuccesJeuEnCours is null)
        {
            return;
        }

        double positionDepart = departEnHaut ? 0 : amplitude;
        double positionArrivee = departEnHaut ? amplitude : 0;
        double dureeTrajetSecondes = Math.Clamp(
            amplitude / VitesseDefilementGrilleSuccesPixelsParSeconde,
            4,
            16
        );
        TimeSpan pause = TimeSpan.FromSeconds(1.1);
        TimeSpan trajet = TimeSpan.FromSeconds(dureeTrajetSecondes);
        DoubleAnimationUsingKeyFrames animation = new();

        SetOffsetVerticalAnime(ConteneurGrilleTousSuccesJeuEnCours, positionDepart);
        ConteneurGrilleTousSuccesJeuEnCours.ScrollToVerticalOffset(positionDepart);
        _animationGrilleSuccesVersBas = departEnHaut;
        JournaliserDiagnosticListeSucces(
            "animation_cycle_debut",
            $"depart={positionDepart:0.##};arrivee={positionArrivee:0.##}"
        );

        animation.KeyFrames.Add(
            new EasingDoubleKeyFrame(positionDepart, KeyTime.FromTimeSpan(TimeSpan.Zero))
        );
        animation.KeyFrames.Add(
            new EasingDoubleKeyFrame(positionDepart, KeyTime.FromTimeSpan(pause))
        );
        animation.KeyFrames.Add(
            new EasingDoubleKeyFrame(
                positionArrivee,
                KeyTime.FromTimeSpan(pause + trajet),
                new SineEase { EasingMode = EasingMode.EaseInOut }
            )
        );
        animation.KeyFrames.Add(
            new EasingDoubleKeyFrame(positionArrivee, KeyTime.FromTimeSpan(pause + trajet + pause))
        );

        AnimationClock horlogeCycle = animation.CreateClock();
        _horlogeAnimationGrilleSucces = horlogeCycle;
        horlogeCycle.Completed += (_, _) =>
        {
            if (!ReferenceEquals(_horlogeAnimationGrilleSucces, horlogeCycle))
            {
                return;
            }

            SetOffsetVerticalAnime(ConteneurGrilleTousSuccesJeuEnCours, positionArrivee);
            ConteneurGrilleTousSuccesJeuEnCours.ApplyAnimationClock(
                OffsetVerticalAnimeProperty,
                null
            );
            ConteneurGrilleTousSuccesJeuEnCours.ScrollToVerticalOffset(positionArrivee);
            _horlogeAnimationGrilleSucces = null;
            _dernierOffsetInteractionListeSucces = 0;
            _animationGrilleSuccesVersBas = !departEnHaut;
            JournaliserDiagnosticListeSucces(
                "animation_cycle_fin",
                $"offset={positionArrivee:0.##};prochainSens={(_animationGrilleSuccesVersBas ? "bas" : "haut")}"
            );

            if (_survolBadgeGrilleSuccesActif || _interactionListeSuccesActive)
            {
                return;
            }

            DemarrerAnimationGrilleSuccesCyclique(amplitude, departEnHaut: !departEnHaut);
        };

        ConteneurGrilleTousSuccesJeuEnCours.ApplyAnimationClock(
            OffsetVerticalAnimeProperty,
            horlogeCycle
        );

        if (_survolBadgeGrilleSuccesActif || _interactionListeSuccesActive)
        {
            horlogeCycle.Controller?.Pause();
        }
    }

    /// <summary>
    /// Calcule la hauteur visible maximale de la grille des succès dans la fenêtre.
    /// </summary>
    private double CalculerHauteurDisponibleGrilleTousSucces()
    {
        if (
            ConteneurGrilleTousSuccesJeuEnCours is null
            || ZonePrincipale is null
            || !ConteneurGrilleTousSuccesJeuEnCours.IsLoaded
        )
        {
            return 0;
        }

        double hauteurViewport =
            ZonePrincipale.ViewportHeight > 0
                ? ZonePrincipale.ViewportHeight
                : ZonePrincipale.ActualHeight;

        if (hauteurViewport <= 0)
        {
            return 0;
        }

        Point positionDansScrollViewer = ConteneurGrilleTousSuccesJeuEnCours.TranslatePoint(
            new Point(0, 0),
            ZonePrincipale
        );
        double margeBas = 12;
        double hauteurDisponible = hauteurViewport - positionDansScrollViewer.Y - margeBas;

        return Math.Max(HauteurMinimaleGrilleSucces, hauteurDisponible);
    }

    /// <summary>
    /// Met en pause l'animation des succès lors du survol d'un badge.
    /// </summary>
    private void BadgeGrilleSucces_EntreeSouris(object sender, MouseEventArgs e)
    {
        _survolBadgeGrilleSuccesActif = true;
        _minuteurRepriseAnimationGrilleSucces.Stop();
        JournaliserDiagnosticListeSucces("badge_mouseenter");
        ArreterAnimationGrilleSucces();
    }

    /// <summary>
    /// Reprend l'animation des succès lorsque le survol d'un badge se termine.
    /// </summary>
    private void BadgeGrilleSucces_SortieSouris(object sender, MouseEventArgs e)
    {
        _survolBadgeGrilleSuccesActif = false;
        JournaliserDiagnosticListeSucces("badge_mouseleave");
        _minuteurRepriseAnimationGrilleSucces.Stop();
        _minuteurRepriseAnimationGrilleSucces.Start();
    }

    /// <summary>
    /// Relance l'animation des succès après un défilement manuel.
    /// </summary>
    private void MinuteurRepriseAnimationGrilleSucces_Tick(object? sender, EventArgs e)
    {
        _minuteurRepriseAnimationGrilleSucces.Stop();
        JournaliserDiagnosticListeSucces("animation_reprise_timer");
        ReprendreAnimationGrilleSuccesSiPossible();
    }

    /// <summary>
    /// Reprend ou recrée l'animation de la grille des succès si les conditions le permettent.
    /// </summary>
    private void ReprendreAnimationGrilleSuccesSiPossible()
    {
        if (
            _survolBadgeGrilleSuccesActif
            || _interactionListeSuccesActive
            || GrilleTousSuccesJeuEnCours is null
            || _amplitudeAnimationGrilleSucces <= SeuilDeclenchementDefilementGrilleSucces
        )
        {
            JournaliserDiagnosticListeSucces("animation_reprise_bloquee");
            return;
        }

        if (_horlogeAnimationGrilleSucces is not null)
        {
            JournaliserDiagnosticListeSucces("animation_reprise_resume");
            _horlogeAnimationGrilleSucces.Controller?.Resume();
            return;
        }

        JournaliserDiagnosticListeSucces("animation_reprise_restart");
        double offsetReprise = _dernierOffsetInteractionListeSucces > 0
            ? _dernierOffsetInteractionListeSucces
            : ConteneurGrilleTousSuccesJeuEnCours?.VerticalOffset ?? 0;
        _dernierOffsetInteractionListeSucces = 0;
        DemarrerAnimationGrilleSuccesDepuisPosition(
            offsetReprise,
            _amplitudeAnimationGrilleSucces,
            _animationGrilleSuccesVersBas
        );
    }

    /// <summary>
    /// Met à jour le titre du jeu puis relance son éventuel défilement.
    /// </summary>
    private void DefinirTitreJeuEnCours(string titre)
    {
        bool titreInchange = string.Equals(
            TexteTitreJeuEnCours.Text,
            titre,
            StringComparison.Ordinal
        );
        ConteneurTitreJeuEnCours.ToolTip = titre;

        if (titreInchange)
        {
            return;
        }

        _signatureAnimationTitreJeu = string.Empty;
        if (TexteTitreJeuEnCours.RenderTransform is TranslateTransform translation)
        {
            translation.BeginAnimation(TranslateTransform.XProperty, null);
            translation.X = 0;
        }
        TexteTitreJeuEnCours.Text = titre;
        TexteTitreJeuEnCours.Width = double.NaN;
        TexteTitreJeuEnCours.FontSize = TaillePoliceTitreJeuNormale;
        PlanifierMiseAJourAnimationTitreJeuEnCours();
    }

    /// <summary>
    /// Anime horizontalement le titre quand il dépasse de son conteneur.
    /// </summary>
    private void MettreAJourAnimationTitreJeuEnCours()
    {
        if (
            ConteneurTitreJeuEnCours is null
            || TexteTitreJeuEnCours is null
            || ZoneTitreJeuEnCours is null
            || ConteneurTitreJeuEnCours.ActualWidth <= 0
        )
        {
            return;
        }

        TranslateTransform translation =
            TexteTitreJeuEnCours.RenderTransform as TranslateTransform ?? new TranslateTransform();
        TexteTitreJeuEnCours.RenderTransform = translation;
        translation.BeginAnimation(TranslateTransform.XProperty, null);
        translation.X = 0;
        TexteTitreJeuEnCours.FontSize = TaillePoliceTitreJeuNormale;
        double largeurTitreSouhaitee = MesurerLargeurTitreJeuEnCours(TaillePoliceTitreJeuNormale);
        double largeurDisponible = ConteneurTitreJeuEnCours.ActualWidth;

        if (largeurTitreSouhaitee <= 0 || largeurDisponible <= 0)
        {
            return;
        }

        const double seuilDeclenchement = 6;
        const double vitessePixelsParSeconde = 30;
        TimeSpan pause = TimeSpan.FromSeconds(1.4);
        double debordement = Math.Max(0, largeurTitreSouhaitee - largeurDisponible);
        string signatureAnimation =
            $"{TexteTitreJeuEnCours.Text}|{Math.Round(largeurTitreSouhaitee, 1, MidpointRounding.AwayFromZero)}|{Math.Round(largeurDisponible, 1, MidpointRounding.AwayFromZero)}|{Math.Round(debordement, 1, MidpointRounding.AwayFromZero)}";

        if (
            string.Equals(_signatureAnimationTitreJeu, signatureAnimation, StringComparison.Ordinal)
        )
        {
            return;
        }

        _signatureAnimationTitreJeu = signatureAnimation;

        TexteTitreJeuEnCours.Width = largeurTitreSouhaitee;
        System.Windows.Controls.Canvas.SetLeft(TexteTitreJeuEnCours, 0);

        if (debordement <= seuilDeclenchement)
        {
            return;
        }

        double dureeTrajetSecondes = Math.Clamp(debordement / vitessePixelsParSeconde, 2.4, 14);
        TimeSpan trajet = TimeSpan.FromSeconds(dureeTrajetSecondes);
        DoubleAnimationUsingKeyFrames animation = new() { RepeatBehavior = RepeatBehavior.Forever };

        animation.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        animation.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(pause)));
        animation.KeyFrames.Add(
            new EasingDoubleKeyFrame(
                -debordement,
                KeyTime.FromTimeSpan(pause + trajet),
                new SineEase { EasingMode = EasingMode.EaseInOut }
            )
        );
        animation.KeyFrames.Add(
            new EasingDoubleKeyFrame(-debordement, KeyTime.FromTimeSpan(pause + trajet + pause))
        );
        animation.KeyFrames.Add(
            new EasingDoubleKeyFrame(
                0,
                KeyTime.FromTimeSpan(pause + trajet + pause + trajet),
                new SineEase { EasingMode = EasingMode.EaseInOut }
            )
        );

        translation.BeginAnimation(TranslateTransform.XProperty, animation);
    }

    /// <summary>
    /// Planifie le recalcul de l'animation du titre à la fin du cycle de mise en page courant.
    /// </summary>
    private void PlanifierMiseAJourAnimationTitreJeuEnCours()
    {
        if (_miseAJourAnimationTitreJeuPlanifiee)
        {
            return;
        }

        _miseAJourAnimationTitreJeuPlanifiee = true;
        _ = Dispatcher.BeginInvoke(
            () =>
            {
                _miseAJourAnimationTitreJeuPlanifiee = false;
                MettreAJourAnimationTitreJeuEnCours();
            },
            DispatcherPriority.Render
        );
    }

    /// <summary>
    /// Mesure la largeur réelle du titre indépendamment du layout WPF courant.
    /// </summary>
    private double MesurerLargeurTitreJeuEnCours(double taillePolice)
    {
        string texte = TexteTitreJeuEnCours.Text ?? string.Empty;

        if (string.IsNullOrWhiteSpace(texte))
        {
            return 0;
        }

        double pixelsParDip = VisualTreeHelper.GetDpi(TexteTitreJeuEnCours).PixelsPerDip;
        FormattedText texteMesure = new(
            texte,
            CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            new Typeface(
                TexteTitreJeuEnCours.FontFamily,
                TexteTitreJeuEnCours.FontStyle,
                TexteTitreJeuEnCours.FontWeight,
                TexteTitreJeuEnCours.FontStretch
            ),
            taillePolice,
            Brushes.Transparent,
            pixelsParDip
        );

        return Math.Ceiling(texteMesure.WidthIncludingTrailingWhitespace);
    }
}
