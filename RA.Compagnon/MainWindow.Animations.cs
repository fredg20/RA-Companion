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
        if (_etatListeSuccesUi.MiseAJourAnimationPlanifiee)
        {
            return;
        }

        _etatListeSuccesUi.MiseAJourAnimationPlanifiee = true;
        _ = Dispatcher.BeginInvoke(
            () =>
            {
                _etatListeSuccesUi.MiseAJourAnimationPlanifiee = false;
                MettreAJourAnimationGrilleTousSucces();
            },
            DispatcherPriority.Render
        );
    }

    /// <summary>
    /// Réactive l'autodéfilement quand la liste déborde réellement de la zone visible.
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
            _etatListeSuccesUi.SignatureAnimation = string.Empty;
            _etatListeSuccesUi.AmplitudeAnimation = 0;
            DesactiverAutodefilementListeSucces();
            GrilleTousSuccesJeuEnCours.Width = double.NaN;
            GrilleTousSuccesJeuEnCours.Height = double.NaN;
            if (ZoneVisibleListeSuccesJeuEnCours is not null)
            {
                ZoneVisibleListeSuccesJeuEnCours.MaxHeight = double.PositiveInfinity;
                ZoneVisibleListeSuccesJeuEnCours.Height = double.NaN;
            }
            ConteneurGrilleTousSuccesJeuEnCours.MaxHeight = double.PositiveInfinity;
            ConteneurGrilleTousSuccesJeuEnCours.Height = double.NaN;
            return;
        }

        if (_etatListeSuccesUi.RedimensionnementFenetreActif)
        {
            ArreterAnimationGrilleSucces();
            _etatListeSuccesUi.SignatureAnimation = "redimensionnement_actif";
            _etatListeSuccesUi.AmplitudeAnimation = 0;
            DefinirVisibiliteBarreDefilementListeSucces(
                visible: ConteneurGrilleTousSuccesJeuEnCours.IsMouseOver
            );
            return;
        }

        double hauteurVisible = ObtenirHauteurVisibleListeSucces();
        double hauteurContenu = Math.Max(
            GrilleTousSuccesJeuEnCours.ActualHeight,
            Math.Max(
                GrilleTousSuccesJeuEnCours.DesiredSize.Height,
                ConteneurGrilleTousSuccesJeuEnCours.ExtentHeight
            )
        );
        double amplitude = Math.Max(
            ConteneurGrilleTousSuccesJeuEnCours.ScrollableHeight,
            hauteurContenu - hauteurVisible
        );

        if (
            hauteurVisible <= 0
            || amplitude <= SeuilDeclenchementDefilementGrilleSucces
            || !ListeSuccesPeutDefiler()
        )
        {
            _etatListeSuccesUi.SignatureAnimation = string.Empty;
            _etatListeSuccesUi.AmplitudeAnimation = 0;
            DesactiverAutodefilementListeSucces();
            return;
        }

        string signature = string.Format(
            CultureInfo.InvariantCulture,
            "{0}:{1:0.##}:{2:0.##}:{3:0.##}",
            GrilleTousSuccesJeuEnCours.Children.Count,
            hauteurVisible,
            hauteurContenu,
            amplitude
        );

        _etatListeSuccesUi.AmplitudeAnimation = amplitude;
        DefinirVisibiliteBarreDefilementListeSucces(
            visible: ConteneurGrilleTousSuccesJeuEnCours.IsMouseOver
        );

        if (_etatListeSuccesUi.InteractionActive || _etatListeSuccesUi.SurvolBadgeActif)
        {
            ArreterAnimationGrilleSucces();
            _etatListeSuccesUi.SignatureAnimation = signature;
            return;
        }

        if (
            _etatListeSuccesUi.HorlogeAnimation is not null
            && string.Equals(
                _etatListeSuccesUi.SignatureAnimation,
                signature,
                StringComparison.Ordinal
            )
        )
        {
            return;
        }

        double offsetCourant = Math.Clamp(
            ConteneurGrilleTousSuccesJeuEnCours.VerticalOffset,
            0,
            amplitude
        );
        ArreterAnimationGrilleSucces();
        _etatListeSuccesUi.SignatureAnimation = signature;
        DemarrerAnimationGrilleSuccesDepuisPosition(
            offsetCourant,
            amplitude,
            _etatListeSuccesUi.AnimationVersBas
        );
    }

    /// <summary>
    /// Coupe tout autodéfilement quand la liste tient entièrement dans la zone visible.
    /// </summary>
    private void DesactiverAutodefilementListeSucces()
    {
        if (ConteneurGrilleTousSuccesJeuEnCours is null)
        {
            return;
        }

        _minuteurRepriseAnimationGrilleSucces.Stop();
        _etatListeSuccesUi.EtatInteraction = EtatInteractionListeSucces.AutoScroll;
        _etatListeSuccesUi.AmplitudeAnimation = 0;
        _etatListeSuccesUi.DernierOffsetInteraction = 0;
        ArreterAnimationGrilleSucces();
        ConteneurGrilleTousSuccesJeuEnCours.ScrollToVerticalOffset(0);
        DefinirVisibiliteBarreDefilementListeSucces(visible: false);
    }

    /// <summary>
    /// Arrête l'animation verticale de la grille et réinitialise sa position.
    /// </summary>
    private void ArreterAnimationGrilleSucces()
    {
        if (ConteneurGrilleTousSuccesJeuEnCours is null)
        {
            _etatListeSuccesUi.HorlogeAnimation?.Controller?.Stop();
            _etatListeSuccesUi.HorlogeAnimation = null;
            return;
        }

        double offsetCourant = ConteneurGrilleTousSuccesJeuEnCours.VerticalOffset;
        JournaliserDiagnosticListeSucces("animation_arret_net", $"offset={offsetCourant:0.##}");
        _etatListeSuccesUi.HorlogeAnimation?.Controller?.Stop();
        _etatListeSuccesUi.HorlogeAnimation = null;

        // Conserve la position courante quand on coupe l'autodéfilement,
        // sinon la propriété animée retombe à sa valeur par défaut et la liste
        // saute visuellement tout en haut au survol.
        SetOffsetVerticalAnime(ConteneurGrilleTousSuccesJeuEnCours, offsetCourant);
        ConteneurGrilleTousSuccesJeuEnCours.ApplyAnimationClock(OffsetVerticalAnimeProperty, null);
        ConteneurGrilleTousSuccesJeuEnCours.ScrollToVerticalOffset(offsetCourant);
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
        _etatListeSuccesUi.DernierOffsetInteraction = 0;
        ConteneurGrilleTousSuccesJeuEnCours.ScrollToVerticalOffset(0);
    }

    /// <summary>
    /// Replace la liste en haut avant un recalcul complet après redimensionnement de la fenêtre.
    /// </summary>
    private void ReinitialiserListeSuccesPourRedimensionnement()
    {
        if (ConteneurGrilleTousSuccesJeuEnCours is null)
        {
            return;
        }

        ReinitialiserPositionGrilleTousSucces();
        _etatListeSuccesUi.AnimationVersBas = true;
        _etatListeSuccesUi.SignatureAnimation = string.Empty;
        _etatListeSuccesUi.AmplitudeAnimation = 0;
        DefinirVisibiliteBarreDefilementListeSucces(
            visible: ConteneurGrilleTousSuccesJeuEnCours.IsMouseOver
        );
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
        _etatListeSuccesUi.AnimationVersBas = allerVersBas;

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
        _etatListeSuccesUi.HorlogeAnimation = horlogePremierTrajet;
        horlogePremierTrajet.Completed += (_, _) =>
        {
            if (!ReferenceEquals(_etatListeSuccesUi.HorlogeAnimation, horlogePremierTrajet))
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
            _etatListeSuccesUi.HorlogeAnimation = null;

            if (_etatListeSuccesUi.SurvolBadgeActif)
            {
                return;
            }

            DemarrerAnimationGrilleSuccesCyclique(
                amplitude,
                departEnHaut: ciblePremierTrajet <= 0.5
            );
        };

        ConteneurGrilleTousSuccesJeuEnCours.ApplyAnimationClock(
            OffsetVerticalAnimeProperty,
            horlogePremierTrajet
        );
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
        _etatListeSuccesUi.AnimationVersBas = departEnHaut;
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
        _etatListeSuccesUi.HorlogeAnimation = horlogeCycle;
        horlogeCycle.Completed += (_, _) =>
        {
            if (!ReferenceEquals(_etatListeSuccesUi.HorlogeAnimation, horlogeCycle))
            {
                return;
            }

            SetOffsetVerticalAnime(ConteneurGrilleTousSuccesJeuEnCours, positionArrivee);
            ConteneurGrilleTousSuccesJeuEnCours.ApplyAnimationClock(
                OffsetVerticalAnimeProperty,
                null
            );
            ConteneurGrilleTousSuccesJeuEnCours.ScrollToVerticalOffset(positionArrivee);
            _etatListeSuccesUi.HorlogeAnimation = null;
            _etatListeSuccesUi.DernierOffsetInteraction = 0;
            _etatListeSuccesUi.AnimationVersBas = !departEnHaut;
            JournaliserDiagnosticListeSucces(
                "animation_cycle_fin",
                $"offset={positionArrivee:0.##};prochainSens={(_etatListeSuccesUi.AnimationVersBas ? "bas" : "haut")}"
            );

            if (_etatListeSuccesUi.SurvolBadgeActif || _etatListeSuccesUi.InteractionActive)
            {
                return;
            }

            DemarrerAnimationGrilleSuccesCyclique(amplitude, departEnHaut: !departEnHaut);
        };

        ConteneurGrilleTousSuccesJeuEnCours.ApplyAnimationClock(
            OffsetVerticalAnimeProperty,
            horlogeCycle
        );
    }

    /// <summary>
    /// Met en pause l'animation des succès lors du survol d'un badge.
    /// </summary>
    private void BadgeGrilleSucces_EntreeSouris(object sender, MouseEventArgs e)
    {
        _etatListeSuccesUi.EtatInteraction = EtatInteractionListeSucces.PauseSurvol;
        _minuteurRepriseAnimationGrilleSucces.Stop();
        JournaliserDiagnosticListeSucces("badge_mouseenter");
        ArreterAnimationGrilleSucces();
    }

    /// <summary>
    /// Reprend l'animation des succès lorsque le survol d'un badge se termine.
    /// </summary>
    private void BadgeGrilleSucces_SortieSouris(object sender, MouseEventArgs e)
    {
        if (_etatListeSuccesUi.EtatInteraction == EtatInteractionListeSucces.PauseSurvol)
        {
            _etatListeSuccesUi.EtatInteraction = EtatInteractionListeSucces.AutoScroll;
        }
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
            _etatListeSuccesUi.SurvolBadgeActif
            || _etatListeSuccesUi.InteractionActive
            || GrilleTousSuccesJeuEnCours is null
            || _etatListeSuccesUi.AmplitudeAnimation <= SeuilDeclenchementDefilementGrilleSucces
        )
        {
            JournaliserDiagnosticListeSucces("animation_reprise_bloquee");
            return;
        }

        JournaliserDiagnosticListeSucces("animation_reprise_restart");
        double offsetReprise =
            _etatListeSuccesUi.DernierOffsetInteraction > 0
                ? _etatListeSuccesUi.DernierOffsetInteraction
                : ConteneurGrilleTousSuccesJeuEnCours?.VerticalOffset ?? 0;
        _etatListeSuccesUi.DernierOffsetInteraction = 0;
        DemarrerAnimationGrilleSuccesDepuisPosition(
            offsetReprise,
            _etatListeSuccesUi.AmplitudeAnimation,
            _etatListeSuccesUi.AnimationVersBas
        );
    }

    /// <summary>
    /// Met à jour le titre du jeu puis relance son éventuel défilement.
    /// </summary>
    private void DefinirTitreJeuEnCours(string titre)
    {
        bool titreInchange = string.Equals(
            _vueModele.JeuCourant.Titre,
            titre,
            StringComparison.Ordinal
        );
        _vueModele.JeuCourant.Titre = titre;
        ConteneurTitreJeuEnCours.ToolTip = null;

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
        TexteTitreJeuEnCours.Width = double.NaN;
        TexteTitreJeuEnCours.FontSize = ObtenirTaillePoliceTitreJeuNormaleResponsive();
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
        double taillePoliceTitre = ObtenirTaillePoliceTitreJeuNormaleResponsive();
        TexteTitreJeuEnCours.FontSize = taillePoliceTitre;
        double largeurTitreSouhaitee = MesurerLargeurTitreJeuEnCours(taillePoliceTitre);
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
