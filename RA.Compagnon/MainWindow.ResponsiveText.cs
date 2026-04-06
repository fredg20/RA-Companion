using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace RA.Compagnon;

public partial class MainWindow
{
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

    private double CalculerTaillePoliceResponsive(double tailleBase)
    {
        return Math.Round(
            tailleBase * _facteurTypographieResponsive,
            2,
            MidpointRounding.AwayFromZero
        );
    }

    private double ObtenirTaillePoliceTitreJeuNormaleResponsive()
    {
        return CalculerTaillePoliceResponsive(TaillePoliceTitreJeuNormale);
    }

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

    private void AjusterTitreFenetreResponsive()
    {
        if (TexteTitreFenetre is null || ConteneurTitreFenetre is null)
        {
            return;
        }

        double facteurReductionTitre =
            ActualWidth < 760 ? Math.Max(0.8, 1 - ((760 - ActualWidth) / 900)) : 1;
        double taillePoliceTitre = CalculerTaillePoliceResponsive(18) * facteurReductionTitre;
        double hauteurLigne = CalculerTaillePoliceResponsive(22) * facteurReductionTitre;
        double hauteurConteneur = Math.Clamp(hauteurLigne, 20, 28);

        TexteTitreFenetre.FontSize = taillePoliceTitre;
        TexteTitreFenetre.LineHeight = hauteurLigne;
        ConteneurTitreFenetre.Height = hauteurConteneur;
    }

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
