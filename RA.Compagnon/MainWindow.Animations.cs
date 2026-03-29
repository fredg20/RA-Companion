using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace RA.Compagnon;

public partial class MainWindow
{
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
            _signatureAnimationGrilleSucces = string.Empty;
            _amplitudeAnimationGrilleSucces = 0;
            TranslateTransform translationVide =
                GrilleTousSuccesJeuEnCours.RenderTransform as TranslateTransform
                ?? new TranslateTransform();
            GrilleTousSuccesJeuEnCours.RenderTransform = translationVide;
            ArreterAnimationGrilleSucces(translationVide);
            GrilleTousSuccesJeuEnCours.Width = double.NaN;
            GrilleTousSuccesJeuEnCours.Height = double.NaN;
            ConteneurGrilleTousSuccesJeuEnCours.MaxHeight = double.PositiveInfinity;
            ConteneurGrilleTousSuccesJeuEnCours.Height = double.NaN;
            return;
        }

        double hauteurDisponible = CalculerHauteurDisponibleGrilleTousSucces();

        if (hauteurDisponible <= 0)
        {
            _signatureAnimationGrilleSucces = string.Empty;
            _amplitudeAnimationGrilleSucces = 0;
            TranslateTransform translationVide =
                GrilleTousSuccesJeuEnCours.RenderTransform as TranslateTransform
                ?? new TranslateTransform();
            GrilleTousSuccesJeuEnCours.RenderTransform = translationVide;
            ArreterAnimationGrilleSucces(translationVide);
            GrilleTousSuccesJeuEnCours.Width = double.NaN;
            GrilleTousSuccesJeuEnCours.Height = double.NaN;
            ConteneurGrilleTousSuccesJeuEnCours.MaxHeight = double.PositiveInfinity;
            ConteneurGrilleTousSuccesJeuEnCours.Height = double.NaN;
            return;
        }

        ConteneurGrilleTousSuccesJeuEnCours.MaxHeight = hauteurDisponible;
        ConteneurGrilleTousSuccesJeuEnCours.Height = hauteurDisponible;
        GrilleTousSuccesJeuEnCours.Measure(
            new Size(ConteneurGrilleTousSuccesJeuEnCours.ActualWidth, double.PositiveInfinity)
        );
        double hauteurContenu = GrilleTousSuccesJeuEnCours.DesiredSize.Height;
        GrilleTousSuccesJeuEnCours.Width = ConteneurGrilleTousSuccesJeuEnCours.ActualWidth;
        GrilleTousSuccesJeuEnCours.Height = hauteurContenu;
        double amplitude = Math.Max(0, hauteurContenu - hauteurDisponible + 8);
        _amplitudeAnimationGrilleSucces = amplitude;

        string signatureAnimation =
            $"{GrilleTousSuccesJeuEnCours.Children.Count}|{Math.Round(hauteurContenu, 1, MidpointRounding.AwayFromZero)}|{Math.Round(hauteurDisponible, 1, MidpointRounding.AwayFromZero)}|{Math.Round(amplitude, 1, MidpointRounding.AwayFromZero)}";

        TranslateTransform translation =
            GrilleTousSuccesJeuEnCours.RenderTransform as TranslateTransform
            ?? new TranslateTransform();
        GrilleTousSuccesJeuEnCours.RenderTransform = translation;

        if (amplitude <= SeuilDeclenchementDefilementGrilleSucces)
        {
            ArreterAnimationGrilleSucces(translation);
            _signatureAnimationGrilleSucces = signatureAnimation;
            return;
        }

        if (
            string.Equals(
                _signatureAnimationGrilleSucces,
                signatureAnimation,
                StringComparison.Ordinal
            )
        )
        {
            return;
        }

        _signatureAnimationGrilleSucces = signatureAnimation;
        ArreterAnimationGrilleSucces(translation);
        DemarrerAnimationGrilleSuccesDepuisPosition(
            translation.Y,
            amplitude,
            _animationGrilleSuccesVersBas
        );
    }

    /// <summary>
    /// Arrête l'animation verticale de la grille et réinitialise sa position.
    /// </summary>
    private void ArreterAnimationGrilleSucces(TranslateTransform translation)
    {
        _horlogeAnimationGrilleSucces?.Controller?.Stop();
        _horlogeAnimationGrilleSucces = null;
        translation.ApplyAnimationClock(TranslateTransform.YProperty, null);
    }

    /// <summary>
    /// Démarre l'animation verticale de la grille depuis une position donnée.
    /// </summary>
    private void DemarrerAnimationGrilleSuccesDepuisPosition(
        double positionInitiale,
        double amplitude,
        bool allerVersBas
    )
    {
        if (GrilleTousSuccesJeuEnCours is null)
        {
            return;
        }

        TranslateTransform translation =
            GrilleTousSuccesJeuEnCours.RenderTransform as TranslateTransform
            ?? new TranslateTransform();
        GrilleTousSuccesJeuEnCours.RenderTransform = translation;

        double position = Math.Clamp(positionInitiale, -amplitude, 0);
        translation.Y = position;
        _animationGrilleSuccesVersBas = allerVersBas;

        if (Math.Abs(position) <= 0.5)
        {
            DemarrerAnimationGrilleSuccesCyclique(translation, amplitude, true);
            return;
        }

        if (Math.Abs(position + amplitude) <= 0.5)
        {
            DemarrerAnimationGrilleSuccesCyclique(translation, amplitude, false);
            return;
        }

        double ciblePremierTrajet = allerVersBas ? -amplitude : 0;
        double distancePremierTrajet = Math.Abs(ciblePremierTrajet - position);
        double dureePremierTrajet = Math.Clamp(
            distancePremierTrajet / VitesseDefilementGrilleSuccesPixelsParSeconde,
            0.8,
            16
        );
        DoubleAnimation animationPremierTrajet = new()
        {
            From = position,
            To = ciblePremierTrajet,
            Duration = TimeSpan.FromSeconds(dureePremierTrajet),
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
            FillBehavior = FillBehavior.Stop,
        };

        AnimationClock horlogePremierTrajet = animationPremierTrajet.CreateClock();
        _horlogeAnimationGrilleSucces = horlogePremierTrajet;
        animationPremierTrajet.Completed += (_, _) =>
        {
            if (!ReferenceEquals(_horlogeAnimationGrilleSucces, horlogePremierTrajet))
            {
                return;
            }

            translation.ApplyAnimationClock(TranslateTransform.YProperty, null);
            translation.Y = ciblePremierTrajet;
            _horlogeAnimationGrilleSucces = null;

            if (_survolBadgeGrilleSuccesActif)
            {
                return;
            }

            DemarrerAnimationGrilleSuccesCyclique(
                translation,
                amplitude,
                departEnHaut: ciblePremierTrajet >= -0.5
            );
        };

        translation.ApplyAnimationClock(TranslateTransform.YProperty, horlogePremierTrajet);

        if (_survolBadgeGrilleSuccesActif)
        {
            horlogePremierTrajet.Controller?.Pause();
        }
    }

    /// <summary>
    /// Démarre un cycle de rebond complet entre le haut et le bas de la grille.
    /// </summary>
    private void DemarrerAnimationGrilleSuccesCyclique(
        TranslateTransform translation,
        double amplitude,
        bool departEnHaut
    )
    {
        double positionDepart = departEnHaut ? 0 : -amplitude;
        double positionArrivee = departEnHaut ? -amplitude : 0;
        double dureeTrajetSecondes = Math.Clamp(
            amplitude / VitesseDefilementGrilleSuccesPixelsParSeconde,
            4,
            16
        );
        TimeSpan pause = TimeSpan.FromSeconds(1.1);
        TimeSpan trajet = TimeSpan.FromSeconds(dureeTrajetSecondes);
        DoubleAnimationUsingKeyFrames animation = new() { RepeatBehavior = RepeatBehavior.Forever };

        translation.Y = positionDepart;
        _animationGrilleSuccesVersBas = departEnHaut;

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
        animation.KeyFrames.Add(
            new EasingDoubleKeyFrame(
                positionDepart,
                KeyTime.FromTimeSpan(pause + trajet + pause + trajet),
                new SineEase { EasingMode = EasingMode.EaseInOut }
            )
        );
        animation.KeyFrames.Add(
            new EasingDoubleKeyFrame(
                positionDepart,
                KeyTime.FromTimeSpan(pause + trajet + pause + trajet + pause)
            )
        );

        _horlogeAnimationGrilleSucces = animation.CreateClock();
        translation.ApplyAnimationClock(
            TranslateTransform.YProperty,
            _horlogeAnimationGrilleSucces
        );

        if (_survolBadgeGrilleSuccesActif)
        {
            _horlogeAnimationGrilleSucces.Controller?.Pause();
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
        _horlogeAnimationGrilleSucces?.Controller?.Pause();
    }

    /// <summary>
    /// Reprend l'animation des succès lorsque le survol d'un badge se termine.
    /// </summary>
    private void BadgeGrilleSucces_SortieSouris(object sender, MouseEventArgs e)
    {
        _survolBadgeGrilleSuccesActif = false;
        ReprendreAnimationGrilleSuccesSiPossible();
    }

    /// <summary>
    /// Permet de faire défiler manuellement la grille des succès à la molette.
    /// </summary>
    private void ConteneurGrilleTousSuccesJeuEnCours_ApercuMoletteSouris(
        object sender,
        MouseWheelEventArgs e
    )
    {
        if (
            GrilleTousSuccesJeuEnCours is null
            || _amplitudeAnimationGrilleSucces <= SeuilDeclenchementDefilementGrilleSucces
        )
        {
            return;
        }

        TranslateTransform translation =
            GrilleTousSuccesJeuEnCours.RenderTransform as TranslateTransform
            ?? new TranslateTransform();
        GrilleTousSuccesJeuEnCours.RenderTransform = translation;

        double positionCourante =
            _horlogeAnimationGrilleSucces?.GetCurrentValue(translation.Y, translation.Y) as double?
            ?? translation.Y;

        ArreterAnimationGrilleSucces(translation);

        double pas = Math.Abs(e.Delta) / 120d * 32d;
        double nouvellePosition = Math.Clamp(
            positionCourante + (e.Delta > 0 ? pas : -pas),
            -_amplitudeAnimationGrilleSucces,
            0
        );

        translation.Y = nouvellePosition;
        _animationGrilleSuccesVersBas = e.Delta < 0;
        _minuteurRepriseAnimationGrilleSucces.Stop();
        _minuteurRepriseAnimationGrilleSucces.Start();
        e.Handled = true;
    }

    /// <summary>
    /// Relance l'animation des succès après un défilement manuel.
    /// </summary>
    private void MinuteurRepriseAnimationGrilleSucces_Tick(object? sender, EventArgs e)
    {
        _minuteurRepriseAnimationGrilleSucces.Stop();
        ReprendreAnimationGrilleSuccesSiPossible();
    }

    /// <summary>
    /// Reprend ou recrée l'animation de la grille des succès si les conditions le permettent.
    /// </summary>
    private void ReprendreAnimationGrilleSuccesSiPossible()
    {
        if (
            _survolBadgeGrilleSuccesActif
            || GrilleTousSuccesJeuEnCours is null
            || _amplitudeAnimationGrilleSucces <= SeuilDeclenchementDefilementGrilleSucces
        )
        {
            return;
        }

        TranslateTransform translation =
            GrilleTousSuccesJeuEnCours.RenderTransform as TranslateTransform
            ?? new TranslateTransform();
        GrilleTousSuccesJeuEnCours.RenderTransform = translation;

        if (_horlogeAnimationGrilleSucces is not null)
        {
            _horlogeAnimationGrilleSucces.Controller?.Resume();
            return;
        }

        DemarrerAnimationGrilleSuccesDepuisPosition(
            translation.Y,
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
        const double vitessePixelsParSeconde = 42;
        TimeSpan pause = TimeSpan.FromSeconds(1.1);
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
