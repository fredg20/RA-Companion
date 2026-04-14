using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;

/*
 * Regroupe la logique de typographie responsive appliquée à la fenêtre
 * principale et à ses éléments visuels.
 */
namespace RA.Compagnon;

/*
 * Porte les calculs de facteur d'échelle et l'application récursive
 * des tailles de police responsives.
 */
public partial class MainWindow
{
    /*
     * Calcule le facteur de typographie responsive à partir de la largeur courante.
     */
    private double CalculerFacteurTypographieResponsive(double largeurFenetre)
    {
        double largeurNormalisee = Math.Clamp(
            largeurFenetre,
            LargeurMinimaleTypographieResponsive,
            LargeurMaximaleTypographieResponsive
        );

        if (largeurNormalisee <= LargeurReferenceTypographieResponsive)
        {
            double progressionReduction =
                (largeurNormalisee - LargeurMinimaleTypographieResponsive)
                / (LargeurReferenceTypographieResponsive - LargeurMinimaleTypographieResponsive);

            return FacteurTypographieResponsiveMinimal
                + ((1 - FacteurTypographieResponsiveMinimal) * progressionReduction);
        }

        double progressionAgrandissement =
            (largeurNormalisee - LargeurReferenceTypographieResponsive)
            / (LargeurMaximaleTypographieResponsive - LargeurReferenceTypographieResponsive);

        return 1 + ((FacteurTypographieResponsiveMaximal - 1) * progressionAgrandissement);
    }

    /*
     * Calcule une taille de police responsive à partir d'une taille de base.
     */
    private double CalculerTaillePoliceResponsive(double tailleBase)
    {
        return Math.Round(
            tailleBase * _facteurTypographieResponsive,
            2,
            MidpointRounding.AwayFromZero
        );
    }

    /*
     * Retourne la taille responsive normale du titre de jeu.
     */
    private double ObtenirTaillePoliceTitreJeuNormaleResponsive()
    {
        return CalculerTaillePoliceResponsive(TaillePoliceTitreJeuNormale);
    }

    /*
     * Ajuste la typographie globale de la fenêtre lorsque le facteur change.
     */
    private void AjusterTypographieResponsive(bool forcer = false)
    {
        double nouveauFacteur = CalculerFacteurTypographieResponsive(ActualWidth);

        if (!forcer && Math.Abs(nouveauFacteur - _facteurTypographieResponsive) < 0.01)
        {
            return;
        }

        _facteurTypographieResponsive = nouveauFacteur;
        FontSize = CalculerTaillePoliceResponsive(TaillePoliceInterfaceBase);
        AjusterTitreFenetreResponsive();
        AppliquerTypographieResponsiveSurObjet(this);
    }

    /*
     * Ajuste spécifiquement le titre de la fenêtre selon la largeur disponible.
     */
    private void AjusterTitreFenetreResponsive()
    {
        if (TexteTitreFenetre is null || ConteneurTitreFenetre is null)
        {
            return;
        }

        double facteurReductionTitre =
            ActualWidth < 760 ? Math.Max(0.8, 1 - ((760 - ActualWidth) / 900)) : 1;
        double taillePoliceTitre = CalculerTaillePoliceResponsive(16) * facteurReductionTitre;
        double hauteurLigne = CalculerTaillePoliceResponsive(20) * facteurReductionTitre;
        double hauteurConteneur = Math.Clamp(hauteurLigne, 20, 28);

        TexteTitreFenetre.FontSize = taillePoliceTitre;
        TexteTitreFenetre.LineHeight = hauteurLigne;
        ConteneurTitreFenetre.Height = hauteurConteneur;
        Grid.SetColumn(ConteneurTitreFenetre, 0);
        Grid.SetColumnSpan(ConteneurTitreFenetre, 2);
        ConteneurTitreFenetre.HorizontalAlignment = HorizontalAlignment.Left;
        ConteneurTitreFenetre.Margin = new Thickness(44, 0, 8, 0);
        TexteTitreFenetre.TextAlignment = TextAlignment.Left;
    }

    /*
     * Applique la typographie responsive à la racine visuelle donnée.
     */
    private void AppliquerTypographieResponsiveSurObjet(DependencyObject? racine)
    {
        if (racine is null)
        {
            return;
        }

        if (racine is Window fenetre)
        {
            fenetre.FontSize = CalculerTaillePoliceResponsive(TaillePoliceInterfaceBase);
        }
        else if (
            racine is Control controleRacine
            && controleRacine.ReadLocalValue(Control.FontSizeProperty)
                == DependencyProperty.UnsetValue
        )
        {
            controleRacine.FontSize = CalculerTaillePoliceResponsive(TaillePoliceInterfaceBase);
        }

        HashSet<DependencyObject> elementsVisites = new(ReferenceEqualityComparer.Instance);
        AppliquerTypographieResponsiveSurObjetRecursif(racine, elementsVisites);
    }

    /*
     * Parcourt récursivement l'arbre visuel et logique pour appliquer
     * la typographie responsive.
     */
    private void AppliquerTypographieResponsiveSurObjetRecursif(
        DependencyObject element,
        HashSet<DependencyObject> elementsVisites
    )
    {
        if (!elementsVisites.Add(element))
        {
            return;
        }

        switch (element)
        {
            case TextBlock texte:
                AppliquerTaillePoliceLocaleResponsive(texte, TextBlock.FontSizeProperty);
                break;
            case Control controle:
                AppliquerTaillePoliceLocaleResponsive(controle, Control.FontSizeProperty);
                break;
        }

        if (element is Visual || element is Visual3D)
        {
            int nombreEnfantsVisuels = VisualTreeHelper.GetChildrenCount(element);

            for (int index = 0; index < nombreEnfantsVisuels; index++)
            {
                DependencyObject enfant = VisualTreeHelper.GetChild(element, index);
                AppliquerTypographieResponsiveSurObjetRecursif(enfant, elementsVisites);
            }
        }

        foreach (
            DependencyObject enfantLogique in LogicalTreeHelper
                .GetChildren(element)
                .OfType<DependencyObject>()
        )
        {
            AppliquerTypographieResponsiveSurObjetRecursif(enfantLogique, elementsVisites);
        }
    }

    /*
     * Applique une taille de police responsive à partir de la valeur locale de base.
     */
    private void AppliquerTaillePoliceLocaleResponsive(
        DependencyObject element,
        DependencyProperty proprieteTaillePolice
    )
    {
        object valeurLocale = element.ReadLocalValue(proprieteTaillePolice);

        if (valeurLocale is not double taillePoliceLocale)
        {
            _taillesPoliceLocalesResponsive.Remove(element);
            return;
        }

        if (!_taillesPoliceLocalesResponsive.TryGetValue(element, out double taillePoliceBase))
        {
            taillePoliceBase = taillePoliceLocale;
            _taillesPoliceLocalesResponsive[element] = taillePoliceBase;
        }

        element.SetValue(proprieteTaillePolice, CalculerTaillePoliceResponsive(taillePoliceBase));
    }
}
