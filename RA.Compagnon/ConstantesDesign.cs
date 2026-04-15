/*
 * Centralise les constantes numériques de design partagées entre le code C#
 * et les vues XAML de l'application.
 */
using System.Windows;

namespace RA.Compagnon;

/*
 * Expose les proportions visuelles communes afin d'éviter les valeurs
 * magiques dispersées dans l'interface.
 */
public static class ConstantesDesign
{
    /*
     * Représente le nombre d'or arrondi à trois décimales pour le projet.
     */
    public const double NombreOr = 1.618;

    /*
     * Représente l'inverse du nombre d'or pour les ratios et opacités
     * secondaires utilisés dans l'interface.
     */
    public const double InverseNombreOr = 1d / NombreOr;

    /*
     * Définit l'unité minimale de la suite de Fibonacci utilisée pour
     * composer les plus petits ajustements visuels.
     */
    public const double UniteMinimale = 1;

    /*
     * Définit l'unité fine de la suite de Fibonacci utilisée pour les
     * contours et corrections très légères.
     */
    public const double UniteFine = 2;

    /*
     * Définit l'unité courte de la suite de Fibonacci utilisée pour les
     * petits décalages et seuils intermédiaires.
     */
    public const double EspaceMinuscule = 3;

    /*
     * Définit l'unité compacte de base utilisée pour les petits espacements
     * et les petits rayons.
     */
    public const double EspaceCompact = 8;

    /*
     * Définit l'unité très compacte issue de la progression de Fibonacci
     * pour les petits ajustements de padding et d'alignement.
     */
    public const double EspaceTresCompact = 5;

    /*
     * Définit l'unité standard dérivée du nombre d'or pour les espacements
     * principaux et les rayons intermédiaires.
     */
    public const double EspaceStandard = 13;

    /*
     * Définit l'unité étendue dérivée du nombre d'or pour les séparations
     * principales entre blocs et colonnes.
     */
    public const double EspaceEtendu = 21;

    /*
     * Définit l'étape supérieure de la progression dorée pour les blocs
     * déjà bien installés visuellement.
     */
    public const double EspaceTresEtendu = 34;

    /*
     * Définit une largeur intermédiaire issue de la progression de
     * Fibonacci pour les zones de contenu denses.
     */
    public const double EspaceMajeur = 55;

    /*
     * Définit une largeur forte issue de la progression de Fibonacci pour
     * les vignettes vedettes et repères principaux.
     */
    public const double EspaceHeroique = 89;

    /*
     * Définit une largeur visuelle large issue de la progression dorée pour
     * les visuels de jeu et colonnes structurantes.
     */
    public const double EspaceVisuelLarge = 144;

    /*
     * Définit une largeur de colonne forte issue de la progression dorée.
     */
    public const double EspaceColonneLarge = 233;

    /*
     * Définit une largeur de fenêtre intermédiaire issue de la progression
     * dorée.
     */
    public const double EspaceFenetreStandard = 377;

    /*
     * Définit une largeur de fenêtre large issue de la progression dorée.
     */
    public const double EspaceFenetreLarge = 610;

    /*
     * Définit une largeur de fenêtre majeure issue de la progression dorée.
     */
    public const double EspaceFenetreTresLarge = 987;

    /*
     * Définit l'épaisseur standard des contours accentués dans l'interface.
     */
    public const double EpaisseurContourAccent = 2;

    /*
     * Définit l'épaisseur plus fine des contours de légende et d'aperçu.
     */
    public const double EpaisseurContourLegende = 1.5;

    /*
     * Définit l'épaisseur WPF nulle commune pour les surfaces sans contour.
     */
    public static readonly Thickness EpaisseurAucune = new(0);

    /*
     * Définit l'épaisseur WPF fine standard de l'interface.
     */
    public static readonly Thickness EpaisseurContourFin = new(UniteMinimale);

    /*
     * Définit le rayon arrondi des petites vignettes de légende.
     */
    public const double RayonBadgeLegendeExterieur = EspaceTresCompact;

    /*
     * Définit le rayon WPF correspondant aux coins extérieurs des badges de
     * légende.
     */
    public static readonly CornerRadius RayonCoinsBadgeLegendeExterieur = new(
        RayonBadgeLegendeExterieur
    );

    /*
     * Définit le rayon intérieur des petites vignettes de légende.
     */
    public const double RayonBadgeLegendeInterieur = 3;

    /*
     * Définit le rayon WPF correspondant aux coins intérieurs des badges de
     * légende.
     */
    public static readonly CornerRadius RayonCoinsBadgeLegendeInterieur = new(
        RayonBadgeLegendeInterieur
    );

    /*
     * Définit le petit rayon standard partagé par les cartes et capsules.
     */
    public static readonly CornerRadius RayonCoinsPetit = new(EspaceCompact);

    /*
     * Définit le rayon standard partagé par les cartes principales.
     */
    public static readonly CornerRadius RayonCoinsStandard = new(EspaceStandard);

    /*
     * Définit le grand rayon standard partagé par les éléments larges.
     */
    public static readonly CornerRadius RayonCoinsGrand = new(EspaceEtendu);

    /*
     * Définit la taille standard des badges de succès selon l'échelle
     * harmonisée de l'interface.
     */
    public const double TailleBadgeStandard = 34;

    /*
     * Définit la largeur par défaut de la fenêtre en combinant uniquement
     * des unités de la progression dorée.
     */
    public const double LargeurFenetreParDefaut =
        EspaceFenetreTresLarge + EspaceHeroique + EspaceEtendu + EspaceMinuscule;

    /*
     * Définit la hauteur par défaut de la fenêtre en combinant uniquement
     * des unités de la progression dorée.
     */
    public const double HauteurFenetreParDefaut =
        EspaceFenetreLarge
        + EspaceMajeur
        + EspaceEtendu
        + EspaceCompact
        + EspaceTresCompact
        + UniteMinimale;

    /*
     * Définit la largeur minimale de la fenêtre.
     */
    public const double LargeurFenetreMinimale =
        EspaceColonneLarge + EspaceHeroique + EspaceTresEtendu + EspaceMinuscule + UniteMinimale;

    /*
     * Définit la hauteur minimale de la fenêtre.
     */
    public const double HauteurFenetreMinimale =
        EspaceColonneLarge + EspaceMajeur + EspaceEtendu + EspaceCompact + EspaceMinuscule;

    /*
     * Définit la hauteur de la barre de fenêtre.
     */
    public const double HauteurBarreFenetre = EspaceEtendu + EspaceStandard + UniteFine;

    /*
     * Définit la largeur des colonnes latérales de la barre de fenêtre.
     */
    public const double LargeurColonneBarreFenetre =
        EspaceVisuelLarge + EspaceMajeur + EspaceEtendu;

    /*
     * Définit la largeur des colonnes latérales de la barre de fenêtre au
     * format WPF attendu par les définitions de grille.
     */
    public static readonly GridLength LargeurColonneBarreFenetreGrille = new(
        LargeurColonneBarreFenetre
    );

    /*
     * Définit la largeur minimale du contenu de la modale de connexion.
     */
    public const double LargeurContenuModaleConnexion =
        EspaceColonneLarge + EspaceHeroique + EspaceTresEtendu + EspaceMinuscule + UniteMinimale;

    /*
     * Définit la largeur de la carte de connexion.
     */
    public const double LargeurCarteConnexion =
        EspaceFenetreStandard
        + EspaceTresEtendu
        + EspaceStandard
        + EspaceTresCompact
        + UniteMinimale;

    /*
     * Définit la marge intérieure de la modale de connexion.
     */
    public const double MargeInterieureModaleConnexion = EspaceStandard + EspaceMinuscule;

    /*
     * Définit la largeur de détection du survol de barre de défilement.
     */
    public const double LargeurZoneDetectionBarreDefilement = EspaceStandard + EspaceTresCompact;

    /*
     * Définit la largeur minimale de la carte principale en disposition étendue.
     */
    public const double LargeurMinimaleCarteJeuDispositionEtendue =
        EspaceFenetreLarge + EspaceColonneLarge + EspaceHeroique + EspaceCompact;

    /*
     * Définit la hauteur de la zone de titre du jeu.
     */
    public const double HauteurZoneTitreJeu =
        TailleBadgeStandard + EspaceTresCompact + UniteMinimale;

    /*
     * Définit la hauteur de ligne du titre de fenêtre.
     */
    public const double HauteurLigneTitreFenetre = EspaceEtendu;

    /*
     * Définit la hauteur du trait de progression.
     */
    public const double HauteurBarreProgression = EspaceCompact;

    /*
     * Définit la hauteur minimale des lignes d'information sous la progression.
     */
    public const double HauteurLigneInformation = EspaceEtendu + UniteMinimale;

    /*
     * Définit la hauteur de ligne du texte courant enrichi.
     */
    public const double HauteurLigneTexte = EspaceStandard + EspaceTresCompact;

    /*
     * Définit un espacement intermédiaire composé de la progression dorée.
     */
    public const double EspaceIntermediaire = EspaceCompact + EspaceMinuscule + UniteMinimale;

    /*
     * Définit la largeur standard d'une carte modale secondaire.
     */
    public const double LargeurCarteSecondaire =
        EspaceFenetreStandard + EspaceMajeur + EspaceEtendu + EspaceTresCompact + UniteFine;

    /*
     * Définit la largeur cible de la modale d'aide sur une étape plus large
     * de la progression dorée afin d'aérer davantage son contenu.
     */
    public const double LargeurContenuModaleAide = EspaceFenetreLarge;

    /*
     * Définit le seuil à partir duquel la modale d'aide peut quitter sa
     * disposition compacte et afficher des espacements plus confortables.
     */
    public const double SeuilCompactModaleAide = EspaceFenetreStandard + EspaceMajeur;

    /*
     * Définit la largeur minimale cible d'une capsule d'information du jeu
     * avant de forcer un retour à la ligne.
     */
    public const double LargeurMinimaleCapsuleInformation = EspaceHeroique;

    /*
     * Définit la largeur minimale standard des boutons d'action secondaires.
     */
    public const double LargeurMinimaleBoutonSecondaire =
        EspaceHeroique + EspaceEtendu + EspaceCompact + UniteFine;

    /*
     * Définit la hauteur maximale standard d'une modale de consultation.
     */
    public const double HauteurMaximaleModale =
        EspaceFenetreLarge
        + EspaceVisuelLarge
        + EspaceMajeur
        + EspaceTresEtendu
        + EspaceCompact
        + UniteFine;

    /*
     * Définit l'espacement minimal entre badges de succès.
     */
    public const double EspaceMinimalGrilleSucces = EspaceTresCompact + UniteMinimale;

    /*
     * Définit le seuil de déclenchement du défilement de grille.
     */
    public const double SeuilDeclenchementDefilementGrilleSucces = EspaceMinuscule + UniteMinimale;

    /*
     * Définit la vitesse de défilement de la grille de succès.
     */
    public const double VitesseDefilementGrilleSuccesPixelsParSeconde =
        EspaceEtendu + UniteMinimale;

    /*
     * Définit le rayon de flou de transition des images du jeu courant.
     */
    public const double RayonFlouTransitionImageJeuEnCours = EspaceStandard + UniteMinimale;

    /*
     * Définit l'épaisseur WPF des contours fins utilisés dans la légende.
     */
    public static readonly Thickness EpaisseurContourLegendeThickness = new(
        EpaisseurContourLegende
    );

    /*
     * Définit la hauteur commune des boutons d'action textuels.
     */
    public const double HauteurBoutonAction = EspaceEtendu + EspaceCompact;

    /*
     * Définit la hauteur plus marquée des boutons principaux.
     */
    public const double HauteurBoutonPrincipal = TailleBadgeStandard;

    /*
     * Définit la largeur minimale des boutons principaux textuels.
     */
    public const double LargeurBoutonPrincipal = LargeurMaxVisuelJeu;

    /*
     * Définit la taille des boutons d'icône visibles dans les barres.
     */
    public const double TailleBoutonIcone = EspaceEtendu + EspaceTresCompact;

    /*
     * Définit la taille du pictogramme utilisé dans les zones d'icônes.
     */
    public const double TailleIconeStandard = EspaceEtendu;

    /*
     * Définit la taille compacte des micro-icônes dans les capsules et légendes.
     */
    public const double TailleMicroIcone = EspaceCompact;

    /*
     * Définit la taille des petits boutons de navigation dans les sections.
     */
    public const double TailleBoutonNavigation = HauteurBoutonAction;

    /*
     * Définit la taille du badge mis en avant en s'appuyant sur l'étape
     * supérieure de l'échelle visuelle harmonisée.
     */
    public const double TailleBadgeVedette = 89;

    /*
     * Définit la largeur de colonne réservée au badge mis en avant.
     */
    public static readonly GridLength LargeurColonneBadgeVedette = new(TailleBadgeVedette);

    /*
     * Définit la taille de l'aperçu de légende des badges Softcore et Hardcore.
     */
    public const double TailleBadgeLegende = EspaceEtendu;

    /*
     * Définit la largeur maximale du visuel principal du jeu courant selon une
     * progression cohérente avec l'échelle dorée.
     */
    public const double LargeurMaxVisuelJeu = 144;

    /*
     * Définit la hauteur maximale du visuel principal du jeu courant selon une
     * progression cohérente avec l'échelle dorée.
     */
    public const double HauteurMaxVisuelJeu = 233;

    /*
     * Définit l'opacité secondaire commune utilisée pour les libellés et
     * textes d'accompagnement.
     */
    public const double OpaciteSecondaire = InverseNombreOr;

    /*
     * Définit l'opacité des micro-libellés et aides discrètes.
     */
    public static readonly double OpaciteMicroLibelle = Math.Round(
        1d / Math.Pow(NombreOr, 0.8),
        2,
        MidpointRounding.AwayFromZero
    );

    /*
     * Définit le ratio minimal de largeur autorisé pour la fenêtre, afin
     * d'éviter une interface plus étroite qu'un quart d'écran.
     */
    public const double RatioLargeurMinimaleFenetre = 0.25;

    /*
     * Définit le ratio à partir duquel l'interface passe du mode colonne
     * au mode intermédiaire.
     */
    public const double RatioDispositionIntermediaire = 0.50;

    /*
     * Définit le ratio à partir duquel l'interface passe au mode trois
     * colonnes.
     */
    public const double RatioDispositionTriple = 0.75;

    /*
     * Définit la largeur minimale à partir de laquelle la typographie
     * responsive commence à s'adapter.
     */
    public const double LargeurTypographieMinimale = 360;

    /*
     * Définit la taille de base du texte courant dans l'interface.
     */
    public const double TaillePoliceTexte = EspaceStandard;

    /*
     * Définit la taille des textes secondaires et des informations courtes.
     */
    public static readonly double TaillePoliceSecondaire = Math.Round(
        TaillePoliceTexte / Math.Sqrt(Math.Sqrt(NombreOr)),
        MidpointRounding.AwayFromZero
    );

    /*
     * Définit la taille des micro-libellés de service et de légende.
     */
    public static readonly double TaillePoliceMicro = Math.Round(
        TaillePoliceTexte / Math.Sqrt(NombreOr),
        MidpointRounding.AwayFromZero
    );

    /*
     * Définit la taille des sous-titres de section.
     */
    public static readonly double TaillePoliceSousTitre = Math.Round(
        TaillePoliceTexte * Math.Sqrt(NombreOr),
        MidpointRounding.AwayFromZero
    );

    /*
     * Définit la taille des titres importants de l'interface.
     */
    public static readonly double TaillePoliceTitreSection = Math.Round(
        TaillePoliceTexte * NombreOr,
        MidpointRounding.AwayFromZero
    );

    /*
     * Définit la taille du titre principal du jeu à partir de l'échelle dorée.
     */
    public static readonly double TaillePoliceTitreJeu = Math.Round(
        TaillePoliceTitreSection * Math.Sqrt(NombreOr),
        MidpointRounding.AwayFromZero
    );

    /*
     * Définit la taille normale responsive appliquée au titre principal du jeu.
     */
    public static readonly double TaillePoliceTitreJeuNormale = TaillePoliceTitreJeu;

    /*
     * Définit la taille minimale responsive appliquée au titre principal du jeu.
     */
    public static readonly double TaillePoliceTitreJeuMinimale = TaillePoliceSousTitre;

    /*
     * Définit la taille de police globale de l'interface à partir d'une racine
     * du nombre d'or afin de rester harmonisée avec le texte courant.
     */
    public static readonly double TaillePoliceInterfaceBase = Math.Round(
        TaillePoliceTexte * Math.Pow(NombreOr, 0.25),
        1,
        MidpointRounding.AwayFromZero
    );

    /*
     * Définit la taille de base du titre de fenêtre.
     */
    public const double TaillePoliceTitreFenetre = 14;

    /*
     * Définit la largeur de référence de la typographie à partir d'une
     * progression liée au nombre d'or.
     */
    public static readonly double LargeurTypographieReference = Math.Round(
        LargeurTypographieMinimale * NombreOr * NombreOr,
        MidpointRounding.AwayFromZero
    );

    /*
     * Définit la largeur maximale utile pour l'agrandissement typographique
     * dans la fenêtre.
     */
    public static readonly double LargeurTypographieMaximale = Math.Round(
        LargeurTypographieReference * NombreOr,
        MidpointRounding.AwayFromZero
    );

    /*
     * Définit le facteur minimal de typographie responsive à partir d'une
     * racine du nombre d'or, afin de conserver une réduction douce.
     */
    public static readonly double FacteurTypographieMinimal = Math.Round(
        1d / Math.Sqrt(Math.Sqrt(NombreOr)),
        3,
        MidpointRounding.AwayFromZero
    );

    /*
     * Définit le facteur maximal de typographie responsive à partir d'une
     * racine du nombre d'or, afin de conserver une montée modérée.
     */
    public static readonly double FacteurTypographieMaximal = Math.Round(
        Math.Sqrt(Math.Sqrt(NombreOr)),
        3,
        MidpointRounding.AwayFromZero
    );

    /*
     * Définit la largeur à partir de laquelle le titre de fenêtre commence
     * à se compacter.
     */
    public static readonly double SeuilReductionTitreFenetre = Math.Round(
        LargeurTypographieMinimale * NombreOr,
        MidpointRounding.AwayFromZero
    );

    /*
     * Définit la plage de réduction progressive du titre de fenêtre.
     */
    public static readonly double PlageReductionTitreFenetre = Math.Round(
        SeuilReductionTitreFenetre * NombreOr,
        MidpointRounding.AwayFromZero
    );

    /*
     * Définit le seuil de largeur à partir duquel le layout passe en mode
     * intermédiaire plus aéré.
     */
    public static readonly double LargeurDispositionIntermediaire = Math.Round(
        LargeurTypographieReference * InverseNombreOr,
        MidpointRounding.AwayFromZero
    );

    /*
     * Définit le seuil de largeur à partir duquel le layout peut accueillir
     * trois zones de même importance.
     */
    public static readonly double LargeurDispositionLarge = Math.Round(
        LargeurTypographieReference * NombreOr,
        MidpointRounding.AwayFromZero
    );

    /*
     * Définit la durée de fondu principale des visuels.
     */
    public static readonly TimeSpan DureeFonduVisuel = TimeSpan.FromMilliseconds(377);

    /*
     * Définit la durée de pause du titre défilant.
     */
    public static readonly TimeSpan PauseAnimationTitre = TimeSpan.FromMilliseconds(987);

    /*
     * Définit la vitesse de défilement horizontale des titres trop longs.
     */
    public const double VitesseAnimationTitrePixelsParSeconde = TailleBadgeStandard;

    /*
     * Définit le seuil à partir duquel le défilement du titre devient utile.
     */
    public const double SeuilDeclenchementAnimationTitre = EspaceTresCompact;

    /*
     * Définit le rayon du halo hardcore sur un badge standard.
     */
    public const double FlouHaloHardcore = EspaceEtendu;

    /*
     * Définit le rayon du halo hardcore lorsqu'un état prioritaire est actif.
     */
    public const double FlouHaloHardcorePrioritaire = EspaceStandard;

    /*
     * Définit l'opacité normale du halo hardcore.
     */
    public static readonly double OpaciteHaloHardcore = Math.Round(
        1d / Math.Pow(NombreOr, 0.4),
        2,
        MidpointRounding.AwayFromZero
    );

    /*
     * Définit l'opacité réduite du halo hardcore prioritaire.
     */
    public static readonly double OpaciteHaloHardcorePrioritaire = Math.Round(
        1d / Math.Pow(NombreOr, 1.25),
        2,
        MidpointRounding.AwayFromZero
    );

    /*
     * Définit le padding commun des boutons textuels principaux.
     */
    public static readonly Thickness PaddingBoutonAction = new(
        EspaceStandard,
        EspaceTresCompact,
        EspaceStandard,
        EspaceTresCompact
    );

    /*
     * Définit le padding des boutons textuels plus compacts.
     */
    public static readonly Thickness PaddingBoutonActionCompact = new(
        EspaceCompact,
        EspaceTresCompact,
        EspaceCompact,
        EspaceTresCompact
    );

    /*
     * Définit le padding des boutons principaux.
     */
    public static readonly Thickness PaddingBoutonPrincipal = new(
        EspaceStandard,
        EspaceCompact,
        EspaceStandard,
        EspaceCompact
    );

    /*
     * Définit le padding des capsules d'information du jeu.
     */
    public static readonly Thickness PaddingCapsule = new(
        EspaceCompact,
        EspaceTresCompact,
        EspaceCompact,
        EspaceTresCompact
    );

    /*
     * Définit le padding des champs de saisie de la modale de connexion.
     */
    public static readonly Thickness PaddingChampSaisie = new(
        EspaceTresCompact + EspaceMinuscule + UniteFine,
        EspaceTresCompact + UniteMinimale,
        EspaceTresCompact + EspaceMinuscule + UniteFine,
        EspaceTresCompact + UniteMinimale
    );

    /*
     * Définit la marge de l'icône de fenêtre.
     */
    public static readonly Thickness MargeIconeTitre = new(EspaceStandard + UniteMinimale, 0, 0, 0);

    /*
     * Définit la marge du titre de fenêtre collé à l'icône.
     */
    public static readonly Thickness MargeTitreFenetre = new(
        TailleBadgeStandard + EspaceStandard,
        0,
        EspaceCompact,
        0
    );

    /*
     * Définit la marge du bandeau d'en-tête sous la barre de titre.
     */
    public static readonly Thickness MargeBandeauEnTete = new(
        EspaceStandard,
        EspaceCompact,
        EspaceStandard,
        EspaceTresCompact
    );

    /*
     * Définit la marge compacte entre un titre et son bloc d'informations.
     */
    public static readonly Thickness MargeSousTitreCompacte = new(
        0,
        EspaceMinuscule + UniteMinimale,
        0,
        0
    );

    /*
     * Définit la marge verticale compacte sous un groupe.
     */
    public static readonly Thickness MargeBasCompacte = new(0, 0, 0, EspaceCompact);

    /*
     * Définit la marge horizontale entre capsules adjacentes.
     */
    public static readonly Thickness MargeCapsuleDroite = new(0, 0, EspaceCompact, 0);

    /*
     * Définit la marge droite intermédiaire utilisée dans les lignes
     * d'information et de progression.
     */
    public static readonly Thickness MargeDroiteIntermediaire = new(0, 0, EspaceIntermediaire, 0);

    /*
     * Définit la marge de séparation commune entre deux actions adjacentes.
     */
    public static readonly Thickness MargeActionAdjacente = new(0, 0, EspaceCompact, 0);

    /*
     * Définit un espacement nul réutilisable dans le XAML.
     */
    public static readonly Thickness AucuneMarge = new(0);

    /*
     * Définit le padding principal de la fenêtre.
     */
    public static readonly Thickness PaddingFenetrePrincipale = new(EspaceStandard);

    /*
     * Définit le padding des cartes modales secondaires.
     */
    public static readonly Thickness PaddingCarteSecondaire = new(MargeInterieureModaleConnexion);

    /*
     * Définit la marge externe des sous-cartes de jeu.
     */
    public static readonly Thickness MargeSousCarte = new(UniteFine);

    /*
     * Définit la marge standard entre deux blocs verticaux.
     */
    public static readonly Thickness MargeBlocStandard = new(0, EspaceStandard, 0, 0);

    /*
     * Définit la marge compacte entre deux blocs verticaux.
     */
    public static readonly Thickness MargeBlocCompacte = new(0, EspaceCompact, 0, 0);

    /*
     * Définit la marge intérieure droite standard.
     */
    public static readonly Thickness MargeInterneDroite = new(EspaceCompact, 0, 0, 0);

    /*
     * Définit la marge intérieure dédiée aux icônes.
     */
    public static readonly Thickness MargeInterneIcone = new(0, 0, EspaceCompact, 0);

    /*
     * Définit la marge de grille dense utilisée pour les zones resserrées.
     */
    public static readonly Thickness MargeGrilleDense = new(0, EspaceEtendu, 0, 0);

    /*
     * Définit la marge de fenêtre neutre utilisée pour les ressources globales.
     */
    public static readonly Thickness MargeFenetreNulle = new(0);

    /*
     * Définit l'espacement standard des colonnes principales.
     */
    public static readonly GridLength EspaceColonnesPrincipal = new(EspaceEtendu);

    /*
     * Définit l'espacement standard des rangées principales.
     */
    public static readonly GridLength EspaceRangeesPrincipal = new(EspaceStandard);

    /*
     * Définit le rayon des boutons d'icône circulaires.
     */
    public static readonly CornerRadius RayonBoutonIcone = new(TailleBoutonIcone / 2);

    /*
     * Définit le rayon des boutons d'action standards.
     */
    public static readonly CornerRadius RayonBoutonAction = new(EspaceCompact);
}
