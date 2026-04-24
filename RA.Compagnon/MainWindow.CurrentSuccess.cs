/*
 * Regroupe la logique d'affichage du succès courant, de la grille des succès
 * et des mises à jour locales qui surviennent quand un succès est débloqué
 * ou quand l'utilisateur navigue dans la liste.
 */
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using RA.Compagnon.Modeles.Api.V2.Game;
using RA.Compagnon.Modeles.Etat;
using RA.Compagnon.Modeles.Local;
using RA.Compagnon.Modeles.Presentation;
using RA.Compagnon.Services;
using RA.Compagnon.ViewModels;

namespace RA.Compagnon;

/*
 * Porte la partie de la fenêtre principale qui gère le succès mis en avant,
 * la grille des succès et leurs rafraîchissements locaux.
 */
public partial class MainWindow
{
    private const int SeuilAffichageGroupeSucces = 68;
    private const int SeuilAmbiguiteGroupeSucces = 6;

    /*
     * Réinitialise totalement la carte du succès mis en avant afin de repartir
     * d'un état propre lors d'un changement de jeu ou de contexte.
     */
    private void ReinitialiserPremierSuccesNonDebloque()
    {
        _analyseSuccesEnCours = null;
        _vueModele.SuccesEnCours.Image = null;
        ImagePremierSuccesNonDebloque.Clip = null;
        AppliquerStyleBadgeSuccesEnCours(false);
        _vueModele.SuccesEnCours.ImageVisible = false;
        _vueModele.SuccesEnCours.ImageOpacity = 0.58;
        _vueModele.SuccesEnCours.TexteVisuel = string.Empty;
        _vueModele.SuccesEnCours.TexteVisuelVisible = false;
        _vueModele.SuccesEnCours.Titre = string.Empty;
        _vueModele.SuccesEnCours.TitreVisible = false;
        _vueModele.SuccesEnCours.Description = string.Empty;
        _vueModele.SuccesEnCours.DescriptionVisible = false;
        _vueModele.SuccesEnCours.DetailsPoints = string.Empty;
        _vueModele.SuccesEnCours.DetailsPointsVisible = false;
        _vueModele.SuccesEnCours.DetailsFaisabilite = string.Empty;
        _vueModele.SuccesEnCours.DetailsFaisabiliteVisible = false;
        _vueModele.SuccesEnCours.ToolTipDetailsFaisabilite = string.Empty;
        _vueModele.SuccesEnCours.GroupeDetecteType = string.Empty;
        _vueModele.SuccesEnCours.GroupeDetecteAncre = string.Empty;
        _vueModele.SuccesEnCours.GroupeDetecteQuantite = string.Empty;
        _vueModele.SuccesEnCours.ToolTipGroupeDetecte = string.Empty;
        _vueModele.SuccesEnCours.GroupeDetecteVisible = false;
        _vueModele.SuccesEnCours.BadgesGroupeDetecte.Clear();
        MettreAJourNavigationSuccesEnCours(null);
    }

    /*
     * Détecte si la carte du succès courant doit être reconstruite, même
     * quand la synchronisation considère encore le jeu comme identique.
     */
    private bool SuccesEnCoursDoitEtreRafraichi(int identifiantJeu, int nombreSuccesJeu)
    {
        if (identifiantJeu <= 0)
        {
            return false;
        }

        if (_identifiantJeuSuccesCourant != identifiantJeu)
        {
            return true;
        }

        if (nombreSuccesJeu > 0 && _succesJeuCourant.Count == 0)
        {
            return true;
        }

        if (
            !_vueModele.SuccesEnCours.TitreVisible
            || string.IsNullOrWhiteSpace(_vueModele.SuccesEnCours.Titre)
        )
        {
            return true;
        }

        if (
            !_vueModele.SuccesEnCours.DescriptionVisible
            || string.IsNullOrWhiteSpace(_vueModele.SuccesEnCours.Description)
        )
        {
            return true;
        }

        if (nombreSuccesJeu > 0 && _analyseSuccesEnCours is null)
        {
            return true;
        }

        return false;
    }

    /*
     * Applique un style visuel différent sur le badge principal selon que
     * le succès a été obtenu en hardcore ou non.
     */
    private void AppliquerStyleBadgeSuccesEnCours(bool estHardcore)
    {
        if (!estHardcore)
        {
            CartePremierSuccesNonDebloqueVisuel.BorderBrush = Brushes.Transparent;
            CartePremierSuccesNonDebloqueVisuel.BorderThickness = new Thickness(0);
            CartePremierSuccesNonDebloqueVisuel.Background = Brushes.Transparent;
            CartePremierSuccesNonDebloqueVisuel.Effect = null;
            return;
        }

        CartePremierSuccesNonDebloqueVisuel.BorderBrush = ObtenirPinceauTheme(
            "PinceauAccentHardcore",
            ConstantesDesign.CouleurRepliAccentHardcore
        );
        CartePremierSuccesNonDebloqueVisuel.BorderThickness = new Thickness(
            ConstantesDesign.EpaisseurContourAccent
        );
        CartePremierSuccesNonDebloqueVisuel.Background = ObtenirPinceauTheme(
            "PinceauFondHardcoreTransparent",
            ConstantesDesign.CouleurRepliAccentHardcoreTransparent
        );
        CartePremierSuccesNonDebloqueVisuel.Effect = new DropShadowEffect
        {
            Color = ObtenirCouleurTheme(
                "CouleurAccentHardcore",
                ConstantesDesign.CouleurRepliAccentHardcore
            ),
            BlurRadius = ConstantesDesign.FlouHaloHardcore,
            ShadowDepth = 0,
            Opacity = ConstantesDesign.OpaciteHaloHardcore,
        };
    }

    /*
     * Construit le libellé de points du succès courant en y ajoutant
     * le mode d'obtention lorsque celui-ci est connu.
     */
    private static string ConstruireDetailsPointsSuccesCourant(
        GameAchievementV2 succesSelectionne,
        SuccesAffiche succesAffiche
    )
    {
        string detailsPoints = succesAffiche.DetailsPoints;
        string modeObtention = DeterminerModeObtentionSucces(succesSelectionne);

        if (string.IsNullOrWhiteSpace(modeObtention))
        {
            return detailsPoints;
        }

        return string.IsNullOrWhiteSpace(detailsPoints)
            ? modeObtention
            : $"{detailsPoints} • {modeObtention}";
    }

    /*
     * Déduit le mode d'obtention visible d'un succès à partir des dates
     * softcore et hardcore fournies par l'API.
     */
    private static string DeterminerModeObtentionSucces(GameAchievementV2 succes)
    {
        if (!string.IsNullOrWhiteSpace(succes.DateEarnedHardcore))
        {
            return "Hardcore";
        }

        if (!string.IsNullOrWhiteSpace(succes.DateEarned))
        {
            return "Softcore";
        }

        return string.Empty;
    }

    /*
     * Indique si le succès sélectionné doit être considéré comme hardcore
     * pour l'affichage principal.
     */
    private static bool SuccesEstHardcore(GameAchievementV2 succesSelectionne)
    {
        return !string.IsNullOrWhiteSpace(succesSelectionne.DateEarnedHardcore);
    }

    /*
     * Vérifie si le texte de détails de points mentionne déjà le mode hardcore.
     */
    private static bool DetailsPointsIndiquentHardcore(string detailsPoints)
    {
        return detailsPoints.Contains("Hardcore", StringComparison.OrdinalIgnoreCase);
    }

    /*
     * Réinitialise complètement la grille des succès avant un nouveau chargement
     * pour éviter de conserver un état visuel obsolète.
     */
    private void ReinitialiserGrilleTousSucces()
    {
        _etatListeSuccesUi.VersionChargementGrille++;
        _etatListeSuccesUi.EtatInteraction = EtatInteractionListeSucces.AutoScroll;
        _etatListeSuccesUi.AnimationVersBas = true;
        _etatListeSuccesUi.AmplitudeAnimation = 0;
        _etatListeSuccesUi.SignatureAnimation = string.Empty;
        _minuteurRepriseAnimationGrilleSucces.Stop();
        ConteneurGrilleTousSuccesJeuEnCours?.ScrollToVerticalOffset(0);
        GrilleTousSuccesJeuEnCours.Children.Clear();
        GrilleTousSuccesJeuEnCours.Width = double.NaN;
        GrilleTousSuccesJeuEnCours.Height = double.NaN;
        GrilleTousSuccesJeuEnCours.InvalidateMeasure();
        GrilleTousSuccesJeuEnCours.InvalidateArrange();
        ConteneurGrilleTousSuccesJeuEnCours?.InvalidateMeasure();
        ConteneurGrilleTousSuccesJeuEnCours?.InvalidateArrange();
        ReinitialiserPositionGrilleTousSucces();
        PlanifierMiseAJourAnimationGrilleTousSucces();
    }

    /*
     * Efface les succès affichés et leurs traces persistantes afin de ne pas
     * mélanger les états de deux jeux différents.
     */
    private void ReinitialiserSuccesAffichesEtPersistes()
    {
        _identifiantJeuSuccesCourant = 0;
        _succesJeuCourant = [];
        ReinitialiserEtatSuccesTemporairesSession();
        _etatListeSuccesUi.IdentifiantSuccesEpingle = null;
        _etatListeSuccesUi.SuccesPasses.Clear();
        ReinitialiserPremierSuccesNonDebloque();
        ReinitialiserGrilleTousSucces();

        if (_configurationConnexion.DernierSuccesAffiche is not null)
        {
            _configurationConnexion.DernierSuccesAffiche = null;
            _dernierSuccesAfficheModifie = true;
        }

        if (_configurationConnexion.DerniereListeSuccesAffichee is not null)
        {
            _configurationConnexion.DerniereListeSuccesAffichee = null;
            _derniereListeSuccesAfficheeModifiee = true;
        }
    }

    /*
     * Réinitialise les succès temporaires détectés pendant la session courante
     * et annule les états transitoires associés.
     */
    private void ReinitialiserEtatSuccesTemporairesSession()
    {
        _succesDebloquesLocauxTemporaires.Clear();
        _succesDetectesRecemment.Clear();
        _succesDebloqueDetecteEnAttente = null;
        _etatListeSuccesUi.IdentifiantSuccesTemporaire = null;
        _etatListeSuccesUi.RetourPremierSuccesApresSelectionTemporaire = false;
        _minuteurAffichageTemporaireSuccesGrille.Stop();
    }

    /*
     * Met à jour l'ensemble de l'affichage des succès pour le jeu courant,
     * du succès principal jusqu'à la grille complète.
     */
    private async Task MettreAJourSuccesJeuAsync(GameInfoAndUserProgressV2 jeu)
    {
        List<GameAchievementV2> succes = InitialiserContexteSuccesJeu(jeu, out int versionGrille);
        await MettreAJourPremierSuccesNonDebloqueAsync(jeu.IdentifiantJeu, succes);
        DemarrerMiseAJourGrilleTousSuccesEnArrierePlan(jeu.IdentifiantJeu, succes, versionGrille);
        MarquerEtapePipelineChargementJeu(
            EtapePipelineChargementJeu.SuccesCharges,
            jeu.IdentifiantJeu,
            _versionChargementContenuJeu
        );

        if (
            _succesDebloqueDetecteEnAttente is not null
            && _succesDebloqueDetecteEnAttente.IdentifiantJeu == jeu.IdentifiantJeu
        )
        {
            _ = await AfficherSuccesDebloqueDetecteAsync(_succesDebloqueDetecteEnAttente);
        }
    }

    /*
     * Prépare la collection de succès à afficher et met en place le contexte
     * local nécessaire à la suite du rendu.
     */
    private List<GameAchievementV2> InitialiserContexteSuccesJeu(
        GameInfoAndUserProgressV2 jeu,
        out int versionGrille
    )
    {
        List<GameAchievementV2> succes =
        [
            .. jeu.Succes.Values.OrderBy(item => item.DisplayOrder).ThenBy(item => item.Id),
        ];

        versionGrille = ++_etatListeSuccesUi.VersionChargementGrille;
        _identifiantJeuSuccesCourant = jeu.IdentifiantJeu;
        ChargerSuccesPassesPersistes(jeu.IdentifiantJeu);
        FusionnerSuccesDebloquesLocauxTemporaires(jeu.IdentifiantJeu, succes);
        _succesJeuCourant = succes;
        return succes;
    }

    /*
     * Recharge l'ordre des succès déjà passés lorsqu'il a été mémorisé
     * pour le jeu courant.
     */
    private void ChargerSuccesPassesPersistes(int identifiantJeu)
    {
        _etatListeSuccesUi.SuccesPasses.Clear();

        if (
            identifiantJeu <= 0
            || _configurationConnexion.DerniereListeSuccesAffichee is not { } etat
            || etat.Id != identifiantJeu
            || etat.SuccesPasses.Count == 0
        )
        {
            return;
        }

        _etatListeSuccesUi.SuccesPasses.AddRange(etat.SuccesPasses);
    }

    /*
     * Démarre la mise à jour complète des succès sans bloquer le reste
     * du pipeline de chargement du jeu.
     */
    private void DemarrerMiseAJourSuccesJeuEnArrierePlan(GameInfoAndUserProgressV2 jeu)
    {
        _ = MettreAJourSuccesJeuEnArrierePlanAsync(jeu);
    }

    /*
     * Exécute la mise à jour des succès en arrière-plan et protège
     * l'affichage principal contre les erreurs non critiques.
     */
    private async Task MettreAJourSuccesJeuEnArrierePlanAsync(GameInfoAndUserProgressV2 jeu)
    {
        try
        {
            await MettreAJourSuccesJeuAsync(jeu);
        }
        catch (Exception exception)
        {
            JournaliserExceptionNonBloquante("enrichissement_succes_en_cours", exception);
        }
    }

    /*
     * Lance le remplissage complet de la grille en arrière-plan afin que
     * le succès principal puisse s'afficher sans attendre.
     */
    private void DemarrerMiseAJourGrilleTousSuccesEnArrierePlan(
        int identifiantJeu,
        List<GameAchievementV2> succes,
        int versionGrille
    )
    {
        _ = MettreAJourGrilleTousSuccesEnArrierePlanAsync(identifiantJeu, succes, versionGrille);
    }

    /*
     * Exécute le rendu complet de la grille et ignore les échecs qui ne doivent
     * pas bloquer l'affichage principal du jeu.
     */
    private async Task MettreAJourGrilleTousSuccesEnArrierePlanAsync(
        int identifiantJeu,
        List<GameAchievementV2> succes,
        int versionGrille
    )
    {
        try
        {
            await MettreAJourGrilleTousSuccesAsync(identifiantJeu, succes, versionGrille);
        }
        catch { }
    }

    /*
     * Sélectionne le succès à mettre en avant en respectant les priorités
     * entre succès temporaire, succès épinglé et premier succès non débloqué.
     */
    private (
        GameAchievementV2? Succes,
        bool DoitSauvegarder,
        bool EstEpingleManuellement
    ) SelectionnerSuccesEnCours(List<GameAchievementV2> succes)
    {
        if (_identifiantJeuSuccesCourant > 0)
        {
            if (_etatListeSuccesUi.IdentifiantSuccesTemporaire.HasValue)
            {
                GameAchievementV2? succesTemporaire = succes.FirstOrDefault(item =>
                    item.Id == _etatListeSuccesUi.IdentifiantSuccesTemporaire.Value
                );

                if (succesTemporaire is not null)
                {
                    return (succesTemporaire, false, false);
                }
            }

            if (_etatListeSuccesUi.IdentifiantSuccesEpingle.HasValue)
            {
                GameAchievementV2? succesEpingle = succes.FirstOrDefault(item =>
                    item.Id == _etatListeSuccesUi.IdentifiantSuccesEpingle.Value
                );

                if (succesEpingle is not null)
                {
                    return (succesEpingle, true, true);
                }
            }
        }

        List<GameAchievementV2> succesNonDebloques =
        [
            .. OrdonnerSuccesPourGrilleSelonMode(_identifiantJeuSuccesCourant, succes)
                .Where(item => !SuccesEstDebloquePourAffichage(item)),
        ];
        GameAchievementV2? premierSuccesNonDebloque = succesNonDebloques.FirstOrDefault();

        return (premierSuccesNonDebloque, true, false);
    }

    /*
     * Met à jour la carte principale du succès en appliquant la sélection
     * déterminée pour le jeu courant.
     */
    private async Task MettreAJourPremierSuccesNonDebloqueAsync(
        int identifiantJeu,
        List<GameAchievementV2> succes
    )
    {
        (GameAchievementV2? premierSucces, bool doitSauvegarder, bool estEpingleManuellement) =
            SelectionnerSuccesEnCours(succes);

        await AppliquerSuccesEnCoursAsync(
            identifiantJeu,
            premierSucces,
            doitSauvegarder,
            estEpingleManuellement
        );
    }

    /*
     * Applique un succès choisi à la carte principale, qu'il provienne du
     * mode automatique ou d'une sélection utilisateur.
     */
    private Task AppliquerSuccesEnCoursAsync(
        int identifiantJeu,
        GameAchievementV2? succesSelectionne,
        bool doitSauvegarder,
        bool estEpingleManuellement
    )
    {
        if (succesSelectionne is null)
        {
            _analyseSuccesEnCours = null;
            _versionAffichageSuccesEnCours++;
            _vueModele.SuccesEnCours.Image = null;
            _vueModele.SuccesEnCours.ImageVisible = false;
            AppliquerStyleBadgeSuccesEnCours(false);
            _vueModele.SuccesEnCours.ImageOpacity = 0.58;
            _vueModele.SuccesEnCours.TexteVisuel = "Tous les succès sont débloqués";
            _vueModele.SuccesEnCours.TexteVisuelVisible = true;
            _vueModele.SuccesEnCours.Titre = "Tous les succès sont obtenus";
            _vueModele.SuccesEnCours.TitreVisible = true;
            _vueModele.SuccesEnCours.Description = "Ce jeu ne contient plus de succès à débloquer.";
            _vueModele.SuccesEnCours.DescriptionVisible = true;
            _vueModele.SuccesEnCours.DetailsPoints = string.Empty;
            _vueModele.SuccesEnCours.DetailsPointsVisible = false;
            _vueModele.SuccesEnCours.DetailsFaisabilite = string.Empty;
            _vueModele.SuccesEnCours.DetailsFaisabiliteVisible = false;
            _vueModele.SuccesEnCours.ToolTipDetailsFaisabilite = string.Empty;
            _vueModele.SuccesEnCours.GroupeDetecteType = string.Empty;
            _vueModele.SuccesEnCours.GroupeDetecteAncre = string.Empty;
            _vueModele.SuccesEnCours.GroupeDetecteQuantite = string.Empty;
            _vueModele.SuccesEnCours.ToolTipGroupeDetecte = string.Empty;
            _vueModele.SuccesEnCours.GroupeDetecteVisible = false;
            _vueModele.SuccesEnCours.BadgesGroupeDetecte.Clear();
            MettreAJourNavigationSuccesEnCours(null);
            SauvegarderDernierSuccesAffiche(
                new EtatSuccesAfficheLocal
                {
                    IdentifiantJeu = identifiantJeu,
                    IdentifiantSucces = 0,
                    Titre = _vueModele.SuccesEnCours.Titre,
                    Description = _vueModele.SuccesEnCours.Description,
                    DetailsPoints = string.Empty,
                    DetailsFaisabilite = string.Empty,
                    ExplicationFaisabilite = string.Empty,
                    TexteVisuel = _vueModele.SuccesEnCours.TexteVisuel,
                }
            );
            DemanderExportObs();
            return Task.CompletedTask;
        }

        SuccesAffiche succesAffiche = Services.ServicePresentationSucces.Construire(
            succesSelectionne,
            identifiantJeu,
            _dernieresDonneesJeuAffichees?.Jeu.NumDistinctPlayers ?? 0
        );
        int versionAffichage = ++_versionAffichageSuccesEnCours;

        _vueModele.SuccesEnCours.Image = null;
        ImagePremierSuccesNonDebloque.Clip = null;
        AppliquerStyleBadgeSuccesEnCours(SuccesEstHardcore(succesSelectionne));
        _vueModele.SuccesEnCours.ImageOpacity = succesAffiche.EstDebloque ? 1 : 0.58;
        _vueModele.SuccesEnCours.ImageVisible = false;
        _vueModele.SuccesEnCours.TexteVisuel = string.Empty;
        _vueModele.SuccesEnCours.TexteVisuelVisible = false;

        _vueModele.SuccesEnCours.Titre = succesAffiche.Titre;
        _vueModele.SuccesEnCours.TitreVisible = true;
        _vueModele.SuccesEnCours.Description = succesAffiche.Description.Trim();
        _vueModele.SuccesEnCours.DescriptionVisible = true;
        _vueModele.SuccesEnCours.DetailsPoints = ConstruireDetailsPointsSuccesCourant(
            succesSelectionne,
            succesAffiche
        );
        _vueModele.SuccesEnCours.DetailsPointsVisible = !string.IsNullOrWhiteSpace(
            _vueModele.SuccesEnCours.DetailsPoints
        );
        _vueModele.SuccesEnCours.DetailsFaisabilite = succesAffiche.DetailsFaisabilite;
        _vueModele.SuccesEnCours.DetailsFaisabiliteVisible = !string.IsNullOrWhiteSpace(
            succesAffiche.DetailsFaisabilite
        );
        _vueModele.SuccesEnCours.ToolTipDetailsFaisabilite = succesAffiche.ExplicationFaisabilite;
        MettreAJourNavigationSuccesEnCours(succesSelectionne);
        MettreAJourAnalyseSuccesEnCours(succesSelectionne);
        _ = ChargerBadgesGroupeSuccesEnCoursAsync(identifiantJeu, versionAffichage);

        if (doitSauvegarder)
        {
            SauvegarderDernierSuccesAffiche(
                new EtatSuccesAfficheLocal
                {
                    IdentifiantJeu = identifiantJeu,
                    IdentifiantSucces = succesSelectionne.Id,
                    Titre = _vueModele.SuccesEnCours.Titre,
                    Description = _vueModele.SuccesEnCours.Description,
                    DetailsPoints = _vueModele.SuccesEnCours.DetailsPoints,
                    DetailsFaisabilite = _vueModele.SuccesEnCours.DetailsFaisabilite,
                    ExplicationFaisabilite = _vueModele.SuccesEnCours.ToolTipDetailsFaisabilite,
                    EstEpingleManuellement = estEpingleManuellement,
                    CheminImageBadge = succesAffiche.UrlBadge,
                    TexteVisuel = _vueModele.SuccesEnCours.TexteVisuel,
                }
            );
        }

        DemanderExportObs();

        _ = EnrichirSuccesEnCoursAffichageAsync(
            identifiantJeu,
            succesSelectionne.Id,
            succesAffiche,
            versionAffichage,
            doitSauvegarder,
            estEpingleManuellement
        );
        return Task.CompletedTask;
    }

    /*
     * Recalcule l'analyse hybride du succès courant afin de préparer un futur
     * affichage de groupes cohérents sans bloquer l'interface.
     */
    private void MettreAJourAnalyseSuccesEnCours(GameAchievementV2 succesSelectionne)
    {
        AnalyseZoneRichPresence analyseZoneCourante = ServiceSondeRichPresence
            .Sonder(
                new DonneesCompteUtilisateur
                {
                    Profil = _dernierProfilUtilisateurCharge,
                    Resume = _dernierResumeUtilisateurCharge,
                },
                journaliser: false
            )
            .AnalyseZone;

        _analyseSuccesEnCours = ServiceAnalyseDescriptionsSucces.Analyser(
            succesSelectionne,
            _succesJeuCourant,
            analyseZoneCourante
        );

        AppliquerPresentationGroupeSuccesEnCours();

        if (_analyseSuccesEnCours.GroupePrincipal is null)
        {
            JournaliserDiagnosticAffichageJeu(
                "analyse_succes",
                $"succes={succesSelectionne.Id};groupes=0"
            );
            return;
        }

        string details = string.Join(" | ", _analyseSuccesEnCours.DiagnosticsGroupes);
        DiagnosticDecisionAffichageGroupeSucces diagnosticAffichage = EvaluerAffichageGroupeSucces(
            _analyseSuccesEnCours.GroupePrincipal
        );

        JournaliserDiagnosticAffichageJeu(
            "analyse_succes",
            $"succes={succesSelectionne.Id};groupes={_analyseSuccesEnCours.Groupes.Count};top={details};decision={ConstruireResumeDiagnosticAffichageGroupe(diagnosticAffichage)}"
        );
    }

    /*
     * Projette le meilleur groupe détecté dans le ViewModel du succès courant
     * lorsqu'il atteint un seuil de confiance suffisamment fiable.
     */
    private void AppliquerPresentationGroupeSuccesEnCours()
    {
        GroupeSuccesPotentiel? groupe = _analyseSuccesEnCours?.GroupePrincipal;

        if (!GroupeSuccesDoitEtreAffiche(groupe))
        {
            _vueModele.SuccesEnCours.GroupeDetecteType = string.Empty;
            _vueModele.SuccesEnCours.GroupeDetecteAncre = string.Empty;
            _vueModele.SuccesEnCours.GroupeDetecteQuantite = string.Empty;
            _vueModele.SuccesEnCours.ToolTipGroupeDetecte = string.Empty;
            _vueModele.SuccesEnCours.GroupeDetecteVisible = false;
            _vueModele.SuccesEnCours.BadgesGroupeDetecte.Clear();
            return;
        }

        string type = TraduireTypeGroupeSucces(groupe!.TypeGroupe);
        string libelleSucces = groupe.IdentifiantsSucces.Count > 1 ? "succès liés" : "succès lié";

        _vueModele.SuccesEnCours.GroupeDetecteType = type;
        _vueModele.SuccesEnCours.GroupeDetecteAncre = groupe.Ancre;
        _vueModele.SuccesEnCours.GroupeDetecteQuantite =
            $"{groupe.IdentifiantsSucces.Count} {libelleSucces}";
        _vueModele.SuccesEnCours.ToolTipGroupeDetecte =
            $"Cohérence détectée : {type}{Environment.NewLine}Ancre : {groupe.Ancre}{Environment.NewLine}Confiance : {groupe.LibelleConfiance} ({groupe.ScoreConfiance}){Environment.NewLine}Règle : {groupe.RegleSource}{Environment.NewLine}Succès reliés : {groupe.IdentifiantsSucces.Count}";
        _vueModele.SuccesEnCours.GroupeDetecteVisible = true;
    }

    /*
     * Recharge les badges du groupe détecté afin de redonner un repère visuel
     * immédiat autour du succès actuellement affiché.
     */
    private bool GroupeSuccesDoitEtreAffiche(GroupeSuccesPotentiel? groupe)
    {
        return EvaluerAffichageGroupeSucces(groupe).DoitAfficher;
    }

    /*
     * Décrit précisément la décision d'affichage du groupe principal afin de
     * rendre les réglages empiriques beaucoup plus lisibles dans les journaux.
     */
    private DiagnosticDecisionAffichageGroupeSucces EvaluerAffichageGroupeSucces(
        GroupeSuccesPotentiel? groupe
    )
    {
        if (groupe is null)
        {
            return new DiagnosticDecisionAffichageGroupeSucces(
                false,
                "aucun_groupe",
                0,
                0,
                0,
                string.Empty,
                0,
                0
            );
        }

        if (groupe.IdentifiantsSucces.Count < 2)
        {
            return new DiagnosticDecisionAffichageGroupeSucces(
                false,
                "groupe_trop_petit",
                0,
                0,
                0,
                string.Empty,
                0,
                0
            );
        }

        int seuilAffichage = ObtenirSeuilAffichageGroupe(
            groupe.TypeGroupe,
            groupe.IdentifiantsSucces.Count
        );

        if (groupe.ScoreSelection < seuilAffichage)
        {
            return new DiagnosticDecisionAffichageGroupeSucces(
                false,
                "score_selection_insuffisant",
                seuilAffichage,
                0,
                0,
                string.Empty,
                0,
                0
            );
        }

        GroupeSuccesPotentiel? secondGroupe = _analyseSuccesEnCours
            ?.Groupes.Skip(1)
            .FirstOrDefault();

        if (secondGroupe is null)
        {
            return new DiagnosticDecisionAffichageGroupeSucces(
                true,
                "aucun_second_candidat",
                seuilAffichage,
                0,
                0,
                string.Empty,
                0,
                0
            );
        }

        int ecartSelection = groupe.ScoreSelection - secondGroupe.ScoreSelection;
        int seuilAmbiguite = ObtenirSeuilAmbiguiteGroupe(
            groupe.IdentifiantsSucces.Count,
            groupe,
            groupe.TypeGroupe,
            secondGroupe
        );
        (double tauxRecouvrement, double tauxUnion) = CalculerRecouvrementGroupes(
            groupe,
            secondGroupe
        );
        string secondResume =
            $"{secondGroupe.TypeGroupe}:{secondGroupe.Ancre}:{secondGroupe.ScoreSelection}";

        if (ecartSelection >= seuilAmbiguite)
        {
            return new DiagnosticDecisionAffichageGroupeSucces(
                true,
                "ecart_selection_suffisant",
                seuilAffichage,
                ecartSelection,
                seuilAmbiguite,
                secondResume,
                tauxRecouvrement,
                tauxUnion
            );
        }

        return new DiagnosticDecisionAffichageGroupeSucces(
            false,
            "ambiguite_trop_forte",
            seuilAffichage,
            ecartSelection,
            seuilAmbiguite,
            secondResume,
            tauxRecouvrement,
            tauxUnion
        );
    }

    /*
     * Calcule le seuil d'affichage final à partir du type de groupe puis de la
     * taille réelle du regroupement détecté.
     */
    private static int ObtenirSeuilAffichageGroupe(
        TypeGroupeSuccesPotentiel typeGroupe,
        int tailleGroupe
    )
    {
        int seuilBase = typeGroupe switch
        {
            TypeGroupeSuccesPotentiel.Niveau => SeuilAffichageGroupeSucces,
            TypeGroupeSuccesPotentiel.Boss => SeuilAffichageGroupeSucces + 1,
            TypeGroupeSuccesPotentiel.Monde => SeuilAffichageGroupeSucces + 1,
            TypeGroupeSuccesPotentiel.Collection => SeuilAffichageGroupeSucces + 3,
            TypeGroupeSuccesPotentiel.Mode => SeuilAffichageGroupeSucces + 4,
            TypeGroupeSuccesPotentiel.Objet => SeuilAffichageGroupeSucces + 5,
            TypeGroupeSuccesPotentiel.NonRelie => SeuilAffichageGroupeSucces + 7,
            TypeGroupeSuccesPotentiel.Lexical => SeuilAffichageGroupeSucces + 8,
            _ => SeuilAffichageGroupeSucces + 8,
        };

        int ajustementTaille = tailleGroupe switch
        {
            <= 2 => 2,
            3 => 1,
            4 => 0,
            5 => -1,
            >= 6 => -2,
        };

        return Math.Clamp(seuilBase + ajustementTaille, SeuilAffichageGroupeSucces - 1, 92);
    }

    /*
     * Exige un écart plus net avec le second candidat quand le groupe détecté
     * est petit ou quand le second groupe ressemble à un vrai concurrent
     * structurel plutôt qu'à un simple repli lexical.
     */
    private static int ObtenirSeuilAmbiguiteGroupe(
        int tailleGroupe,
        GroupeSuccesPotentiel groupePrincipal,
        TypeGroupeSuccesPotentiel typeGroupePrincipal,
        GroupeSuccesPotentiel secondGroupe
    )
    {
        int ajustementTaille = tailleGroupe switch
        {
            <= 2 => 3,
            3 => 2,
            4 => 1,
            5 => 0,
            >= 6 => -1,
        };

        int ajustementSecond = ObtenirAjustementAmbiguiteSecondGroupe(
            typeGroupePrincipal,
            secondGroupe
        );
        int ajustementRecouvrement = ObtenirAjustementAmbiguiteRecouvrement(
            groupePrincipal,
            secondGroupe
        );

        return Math.Clamp(
            SeuilAmbiguiteGroupeSucces
                + ajustementTaille
                + ajustementSecond
                + ajustementRecouvrement,
            2,
            16
        );
    }

    /*
     * Donne plus de poids à un second candidat structurel et moins à un
     * second candidat de repli, afin de mieux refléter sa dangerosité réelle.
     */
    private static int ObtenirAjustementAmbiguiteSecondGroupe(
        TypeGroupeSuccesPotentiel typeGroupePrincipal,
        GroupeSuccesPotentiel secondGroupe
    )
    {
        int ajustement = secondGroupe.TypeGroupe switch
        {
            TypeGroupeSuccesPotentiel.NonRelie => -4,
            TypeGroupeSuccesPotentiel.Lexical => -3,
            TypeGroupeSuccesPotentiel.Objet => 0,
            TypeGroupeSuccesPotentiel.DefiTechnique => 0,
            TypeGroupeSuccesPotentiel.Collection => 1,
            TypeGroupeSuccesPotentiel.Mode => 1,
            TypeGroupeSuccesPotentiel.Monde => 2,
            TypeGroupeSuccesPotentiel.Boss => 3,
            TypeGroupeSuccesPotentiel.Niveau => 3,
            _ => 1,
        };

        if (secondGroupe.IdentifiantsSucces.Count < 2)
        {
            ajustement -= 2;
        }

        if (secondGroupe.ScoreConfiance < SeuilAffichageGroupeSucces)
        {
            ajustement -= 1;
        }

        if (secondGroupe.TypeGroupe == typeGroupePrincipal)
        {
            ajustement += 2;
        }

        if (
            typeGroupePrincipal == TypeGroupeSuccesPotentiel.Niveau
            && secondGroupe.TypeGroupe == TypeGroupeSuccesPotentiel.Mode
        )
        {
            ajustement -= 2;
        }

        if (
            typeGroupePrincipal == TypeGroupeSuccesPotentiel.Monde
            && secondGroupe.TypeGroupe == TypeGroupeSuccesPotentiel.Mode
        )
        {
            ajustement -= 1;
        }

        if (
            typeGroupePrincipal == TypeGroupeSuccesPotentiel.Niveau
            && secondGroupe.TypeGroupe == TypeGroupeSuccesPotentiel.Collection
        )
        {
            ajustement -= 2;
        }

        if (
            typeGroupePrincipal == TypeGroupeSuccesPotentiel.Monde
            && secondGroupe.TypeGroupe == TypeGroupeSuccesPotentiel.Collection
        )
        {
            ajustement -= 1;
        }

        return ajustement;
    }

    /*
     * Ajuste l'ambiguïté selon le recouvrement réel entre le premier et le
     * second groupe, afin de distinguer un quasi-duplicate d'un vrai rival.
     */
    private static int ObtenirAjustementAmbiguiteRecouvrement(
        GroupeSuccesPotentiel? groupePrincipal,
        GroupeSuccesPotentiel secondGroupe
    )
    {
        if (groupePrincipal is null)
        {
            return 0;
        }

        (double tauxRecouvrement, double tauxUnion) = CalculerRecouvrementGroupes(
            groupePrincipal,
            secondGroupe
        );

        if (tauxRecouvrement >= 0.85 || tauxUnion >= 0.7)
        {
            return -4;
        }

        if (tauxRecouvrement >= 0.6 || tauxUnion >= 0.5)
        {
            return -2;
        }

        if (tauxRecouvrement <= 0.2 && tauxUnion <= 0.15)
        {
            return 3;
        }

        if (tauxRecouvrement <= 0.35 && tauxUnion <= 0.25)
        {
            return 1;
        }

        return 0;
    }

    /*
     * Calcule deux visions complémentaires du recouvrement: la part commune
     * rapportée au plus petit groupe, puis la part commune sur l'union totale.
     */
    private static (double TauxRecouvrement, double TauxUnion) CalculerRecouvrementGroupes(
        GroupeSuccesPotentiel groupePrincipal,
        GroupeSuccesPotentiel secondGroupe
    )
    {
        HashSet<int> identifiantsPrincipal = [.. groupePrincipal.IdentifiantsSucces];
        HashSet<int> identifiantsSecond = [.. secondGroupe.IdentifiantsSucces];

        if (identifiantsPrincipal.Count == 0 || identifiantsSecond.Count == 0)
        {
            return (0, 0);
        }

        int communs = identifiantsPrincipal.Intersect(identifiantsSecond).Count();

        if (communs == 0)
        {
            return (0, 0);
        }

        int plusPetit = Math.Min(identifiantsPrincipal.Count, identifiantsSecond.Count);

        identifiantsPrincipal.UnionWith(identifiantsSecond);
        int union = identifiantsPrincipal.Count;

        return ((double)communs / plusPetit, (double)communs / union);
    }

    /*
     * Formate la décision d'affichage sous une forme compacte et stable pour
     * faciliter les lectures comparatives dans les journaux.
     */
    private static string ConstruireResumeDiagnosticAffichageGroupe(
        DiagnosticDecisionAffichageGroupeSucces diagnostic
    )
    {
        return $"affiche={(diagnostic.DoitAfficher ? "oui" : "non")};raison={diagnostic.Raison};seuil={diagnostic.SeuilAffichage};ecart={diagnostic.EcartSelection};ambiguite={diagnostic.SeuilAmbiguite};second={diagnostic.SecondResume};recouvrement={diagnostic.TauxRecouvrement:0.00};union={diagnostic.TauxUnion:0.00}";
    }

    private async Task ChargerBadgesGroupeSuccesEnCoursAsync(
        int identifiantJeu,
        int versionAffichage
    )
    {
        GroupeSuccesPotentiel? groupe = _analyseSuccesEnCours?.GroupePrincipal;
        _vueModele.SuccesEnCours.BadgesGroupeDetecte.Clear();

        if (!GroupeSuccesDoitEtreAffiche(groupe))
        {
            return;
        }

        HashSet<int> identifiantsGroupe = [.. groupe!.IdentifiantsSucces];
        List<GameAchievementV2> succesDuGroupe =
        [
            .. _succesJeuCourant.Where(item => identifiantsGroupe.Contains(item.Id)),
        ];

        foreach (GameAchievementV2 succes in succesDuGroupe)
        {
            SuccesGrilleAffiche succesAffiche = ServicePresentationSucces.ConstruirePourGrille(
                succes
            );
            CurrentAchievementGroupBadgeViewModel badge = new()
            {
                IdentifiantSucces = succesAffiche.IdentifiantSucces,
                Titre = succesAffiche.Titre,
                ToolTip = string.Empty,
                TexteSecours = string.IsNullOrWhiteSpace(succesAffiche.Titre)
                    ? "?"
                    : succesAffiche.Titre[..1].ToUpperInvariant(),
                EstHardcore = succesAffiche.EstHardcore,
                ImageOpacity = succesAffiche.EstDebloque ? 1 : 0.58,
            };

            _vueModele.SuccesEnCours.BadgesGroupeDetecte.Add(badge);

            try
            {
                string descriptionTraduite =
                    await _serviceTraductionTexte.TraduireVersFrancaisAsync(succes.Description);

                if (
                    _versionAffichageSuccesEnCours != versionAffichage
                    || _identifiantJeuSuccesCourant != identifiantJeu
                )
                {
                    return;
                }

                badge.ToolTip = string.IsNullOrWhiteSpace(descriptionTraduite)
                    ? succes.Description.Trim()
                    : descriptionTraduite.Trim();

                ImageSource? imageBadge = await ChargerImageDistanteAsync(succesAffiche.UrlBadge);

                if (
                    _versionAffichageSuccesEnCours != versionAffichage
                    || _identifiantJeuSuccesCourant != identifiantJeu
                )
                {
                    return;
                }

                if (imageBadge is null)
                {
                    continue;
                }

                badge.Image = succesAffiche.EstDebloque
                    ? imageBadge
                    : ConvertirImageEnNoirEtBlanc(imageBadge);
                badge.ImageOpacity = succesAffiche.EstDebloque ? 1 : 0.58;
                badge.ImageVisible = true;
            }
            catch { }
        }
    }

    /*
     * Transporte la décision finale d'affichage du groupe afin de la réutiliser
     * à la fois pour l'UI et pour la journalisation diagnostique.
     */
    private readonly record struct DiagnosticDecisionAffichageGroupeSucces(
        bool DoitAfficher,
        string Raison,
        int SeuilAffichage,
        int EcartSelection,
        int SeuilAmbiguite,
        string SecondResume,
        double TauxRecouvrement,
        double TauxUnion
    );

    /*
     * Arrondit les coins d'un badge rendu dans le groupe du succès courant
     * pour conserver le même traitement que dans la grille principale.
     */
    private void ImageBadgeGroupeSucces_Charge(object sender, RoutedEventArgs e)
    {
        if (sender is Image image)
        {
            AppliquerCoinsArrondisImage(image);
        }
    }

    /*
     * Réapplique les coins arrondis lorsque la taille d'un badge du groupe
     * change après un chargement ou un redimensionnement.
     */
    private void ImageBadgeGroupeSucces_TailleChangee(object sender, SizeChangedEventArgs e)
    {
        if (sender is Image image)
        {
            AppliquerCoinsArrondisImage(image);
        }
    }

    /*
     * Traduit le type de groupe technique en libellé lisible en français.
     */
    private static string TraduireTypeGroupeSucces(TypeGroupeSuccesPotentiel type)
    {
        return type switch
        {
            TypeGroupeSuccesPotentiel.Niveau => "Niveau",
            TypeGroupeSuccesPotentiel.Monde => "Monde",
            TypeGroupeSuccesPotentiel.Boss => "Boss",
            TypeGroupeSuccesPotentiel.Collection => "Collection",
            TypeGroupeSuccesPotentiel.Mode => "Mode",
            TypeGroupeSuccesPotentiel.Objet => "Objet",
            TypeGroupeSuccesPotentiel.DefiTechnique => "Défi",
            TypeGroupeSuccesPotentiel.NonRelie => "Ensemble",
            TypeGroupeSuccesPotentiel.Lexical => "Famille",
            _ => "Groupe",
        };
    }

    /*
     * Enrichit le succès déjà affiché avec son image et sa traduction
     * dès que ces informations asynchrones sont disponibles.
     */
    private async Task EnrichirSuccesEnCoursAffichageAsync(
        int identifiantJeu,
        int identifiantSucces,
        SuccesAffiche succesAffiche,
        int versionAffichage,
        bool doitSauvegarder,
        bool estEpingleManuellement
    )
    {
        try
        {
            ImageSource? imageSucces = await ChargerImageDistanteAsync(succesAffiche.UrlBadge);

            if (
                _versionAffichageSuccesEnCours != versionAffichage
                || _identifiantJeuSuccesCourant != identifiantJeu
            )
            {
                return;
            }

            if (imageSucces is not null)
            {
                _vueModele.SuccesEnCours.Image = succesAffiche.EstDebloque
                    ? imageSucces
                    : ConvertirImageEnNoirEtBlanc(imageSucces);
                _vueModele.SuccesEnCours.ImageOpacity = succesAffiche.EstDebloque ? 1 : 0.58;
                _vueModele.SuccesEnCours.ImageVisible = true;
                _vueModele.SuccesEnCours.TexteVisuel = string.Empty;
                _vueModele.SuccesEnCours.TexteVisuelVisible = false;
                AppliquerCoinsArrondisImagePremierSuccesNonDebloque();
            }
            else
            {
                _vueModele.SuccesEnCours.Image = null;
                ImagePremierSuccesNonDebloque.Clip = null;
                _vueModele.SuccesEnCours.ImageVisible = false;
                _vueModele.SuccesEnCours.TexteVisuel = "Visuel indisponible";
                _vueModele.SuccesEnCours.TexteVisuelVisible = true;
            }

            string descriptionSucces = await _serviceTraductionTexte.TraduireVersFrancaisAsync(
                succesAffiche.Description
            );

            if (
                _versionAffichageSuccesEnCours != versionAffichage
                || _identifiantJeuSuccesCourant != identifiantJeu
            )
            {
                return;
            }

            _vueModele.SuccesEnCours.Description = string.IsNullOrWhiteSpace(descriptionSucces)
                ? succesAffiche.Description.Trim()
                : descriptionSucces.Trim();

            if (doitSauvegarder)
            {
                SauvegarderDernierSuccesAffiche(
                    new EtatSuccesAfficheLocal
                    {
                        IdentifiantJeu = identifiantJeu,
                        IdentifiantSucces = identifiantSucces,
                        Titre = _vueModele.SuccesEnCours.Titre,
                        Description = _vueModele.SuccesEnCours.Description,
                        DetailsPoints = _vueModele.SuccesEnCours.DetailsPoints,
                        DetailsFaisabilite = _vueModele.SuccesEnCours.DetailsFaisabilite,
                        ExplicationFaisabilite = _vueModele.SuccesEnCours.ToolTipDetailsFaisabilite,
                        EstEpingleManuellement = estEpingleManuellement,
                        CheminImageBadge = succesAffiche.UrlBadge,
                        TexteVisuel = _vueModele.SuccesEnCours.TexteVisuel,
                    }
                );
            }

            DemanderExportObs();
        }
        catch { }
    }

    /*
     * Affiche un succès détecté localement comme nouvellement débloqué
     * lorsqu'il correspond bien au jeu courant.
     */
    private async Task<bool> AfficherSuccesDebloqueDetecteAsync(SuccesDebloqueDetecte succesDetecte)
    {
        if (succesDetecte.IdentifiantJeu <= 0)
        {
            ServiceSurveillanceSuccesLocaux.JournaliserEvenement(
                "succes_ui_ignore",
                $"raison=jeu_invalide;jeu={succesDetecte.IdentifiantJeu};succes={succesDetecte.IdentifiantSucces}"
            );
            return false;
        }

        _succesDebloqueDetecteEnAttente = succesDetecte;

        if (
            _identifiantJeuSuccesCourant != succesDetecte.IdentifiantJeu
            || _succesJeuCourant.Count == 0
        )
        {
            ServiceSurveillanceSuccesLocaux.JournaliserEvenement(
                "succes_ui_en_attente",
                $"raison=contexte_indisponible;jeu={succesDetecte.IdentifiantJeu};succes={succesDetecte.IdentifiantSucces};jeuCourant={_identifiantJeuSuccesCourant};nbSucces={_succesJeuCourant.Count}"
            );
            return false;
        }

        GameAchievementV2? succes = _succesJeuCourant.FirstOrDefault(item =>
            item.Id == succesDetecte.IdentifiantSucces
        );

        if (succes is null)
        {
            ServiceSurveillanceSuccesLocaux.JournaliserEvenement(
                "succes_ui_ignore",
                $"raison=succes_introuvable;jeu={succesDetecte.IdentifiantJeu};succes={succesDetecte.IdentifiantSucces}"
            );
            return false;
        }

        MarquerSuccesCommeDebloqueLocalement(succes, succesDetecte);
        if (
            !_succesDebloquesLocauxTemporaires.TryGetValue(
                succesDetecte.IdentifiantJeu,
                out HashSet<int>? succesTemp
            )
        )
        {
            succesTemp = [];
            _succesDebloquesLocauxTemporaires[succesDetecte.IdentifiantJeu] = succesTemp;
        }
        succesTemp.Add(succesDetecte.IdentifiantSucces);
        _succesDebloqueDetecteEnAttente = null;
        _etatListeSuccesUi.IdentifiantSuccesTemporaire = succes.Id;
        _etatListeSuccesUi.RetourPremierSuccesApresSelectionTemporaire = true;
        _minuteurAffichageTemporaireSuccesGrille.Stop();
        _minuteurAffichageTemporaireSuccesGrille.Start();
        RafraichirSuccesEtProgressionApresDeblocageLocal();

        await AppliquerSuccesEnCoursAsync(_identifiantJeuSuccesCourant, succes, false, false);
        await ExporterEtatObsAsync(succesDetecte);
        ServiceSurveillanceSuccesLocaux.JournaliserEvenement(
            "succes_ui_affiche",
            $"jeu={succesDetecte.IdentifiantJeu};succes={succesDetecte.IdentifiantSucces};titre={succesDetecte.TitreSucces}"
        );
        return true;
    }

    /*
     * Injecte localement l'état débloqué d'un succès à partir d'un signal
     * de surveillance reçu pendant la session.
     */
    private static void MarquerSuccesCommeDebloqueLocalement(
        GameAchievementV2 succes,
        SuccesDebloqueDetecte succesDetecte
    )
    {
        string dateObtention = string.IsNullOrWhiteSpace(succesDetecte.DateObtention)
            ? DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
            : succesDetecte.DateObtention.Trim();

        succes.DateEarned = dateObtention;

        if (succesDetecte.Hardcore)
        {
            succes.DateEarnedHardcore = dateObtention;
        }
    }

    /*
     * Réapplique sur la collection courante les succès débloqués localement
     * avant que l'API n'ait eu le temps de refléter ce nouvel état.
     */
    private void FusionnerSuccesDebloquesLocauxTemporaires(
        int identifiantJeu,
        List<GameAchievementV2> succes
    )
    {
        if (
            identifiantJeu <= 0
            || !_succesDebloquesLocauxTemporaires.TryGetValue(identifiantJeu, out HashSet<int>? ids)
            || ids.Count == 0
        )
        {
            return;
        }

        string dateSession = DateTime.Now.ToString(
            "yyyy-MM-dd HH:mm:ss",
            CultureInfo.InvariantCulture
        );

        foreach (GameAchievementV2 succesJeu in succes.Where(item => ids.Contains(item.Id)))
        {
            if (string.IsNullOrWhiteSpace(succesJeu.DateEarned))
            {
                succesJeu.DateEarned = dateSession;
            }
        }
    }

    /*
     * Détermine si un succès doit être considéré comme débloqué pour l'affichage
     * en combinant les données API et les détections locales temporaires.
     */
    private bool SuccesEstDebloquePourAffichage(GameAchievementV2 succes)
    {
        if (
            _identifiantJeuSuccesCourant > 0
            && _succesDebloquesLocauxTemporaires.TryGetValue(
                _identifiantJeuSuccesCourant,
                out HashSet<int>? ids
            )
            && ids.Contains(succes.Id)
        )
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(succes.DateEarned)
            || !string.IsNullOrWhiteSpace(succes.DateEarnedHardcore);
    }

    /*
     * ?f???vite de retraiter plusieurs fois le m?f?me succ?f?s local lorsque plusieurs
     * signaux proches arrivent pendant une courte fenêtre de temps.
     */
    private bool SuccesDejaTraiteRecemment(SuccesDebloqueDetecte succes)
    {
        string signature = ConstruireSignatureSuccesTraite(succes);

        if (
            _succesDetectesRecemment.TryGetValue(signature, out DateTimeOffset horodatage)
            && DateTimeOffset.UtcNow - horodatage < TimeSpan.FromMinutes(5)
        )
        {
            return true;
        }

        return false;
    }

    /*
     * Mémorise qu'un succès local a déjà été pris en compte par l'interface.
     */
    private void MarquerSuccesCommeTraite(SuccesDebloqueDetecte succes)
    {
        string signature = ConstruireSignatureSuccesTraite(succes);
        _succesDetectesRecemment[signature] = DateTimeOffset.UtcNow;
    }

    /*
     * Construit la signature stable servant à dédoublonner un succès traité.
     */
    private static string ConstruireSignatureSuccesTraite(SuccesDebloqueDetecte succes)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{succes.IdentifiantJeu}|{succes.IdentifiantSucces}"
        );
    }

    /*
     * Rafraîchit toutes les zones dépendantes après un déblocage détecté
     * localement afin de garder une interface cohérente.
     */
    private void RafraichirSuccesEtProgressionApresDeblocageLocal()
    {
        RafraichirStyleBadgesGrilleSucces();
        MettreAJourProgressionJeuDepuisSuccesLocaux();
        RedessinerGrilleTousSuccesDepuisEtatLocal();
    }

    /*
     * Recalcule la progression visible du jeu à partir de l'état local
     * des succès, puis synchronise si besoin l'état persistant.
     */
    private void MettreAJourProgressionJeuDepuisSuccesLocaux()
    {
        if (_identifiantJeuSuccesCourant <= 0 || _succesJeuCourant.Count == 0)
        {
            return;
        }

        int nombreSucces = _succesJeuCourant.Count;
        int nombreDebloques = _succesJeuCourant.Count(SuccesEstDebloquePourAffichage);
        double pourcentage = nombreSucces <= 0 ? 0 : (double)nombreDebloques * 100d / nombreSucces;

        _vueModele.JeuCourant.Progression = string.Create(
            CultureInfo.CurrentCulture,
            $"{nombreDebloques} / {nombreSucces}"
        );
        _vueModele.JeuCourant.Details = ServicePresentationJeu.ConstruireResumePoints(
            _succesJeuCourant
        );
        _vueModele.JeuCourant.Pourcentage =
            $"{Math.Round(pourcentage, MidpointRounding.AwayFromZero).ToString(CultureInfo.CurrentCulture)} % complété";
        _vueModele.JeuCourant.ProgressionValeur = Math.Clamp(pourcentage, 0, 100);

        if (_configurationConnexion.DernierJeuAffiche?.Id == _identifiantJeuSuccesCourant)
        {
            _configurationConnexion.DernierJeuAffiche.ResumeProgression = _vueModele
                .JeuCourant
                .Progression;
            _configurationConnexion.DernierJeuAffiche.PourcentageProgression = _vueModele
                .JeuCourant
                .Pourcentage;
            _configurationConnexion.DernierJeuAffiche.ValeurProgression = _vueModele
                .JeuCourant
                .ProgressionValeur;
            _configurationConnexion.DernierJeuAffiche.Details = _vueModele.JeuCourant.Details;
            _configurationConnexion.DernierJeuAffiche.EtatJeu =
                nombreDebloques >= nombreSucces && nombreSucces > 0
                    ? "Jeu compl\u00E9t\u00E9"
                    : string.Empty;
            _dernierJeuAfficheModifie = true;
        }
    }

    /*
     * Redessine la grille des succès à partir de l'état local courant
     * sans attendre un rechargement complet de l'API.
     */
    private void RedessinerGrilleTousSuccesDepuisEtatLocal()
    {
        if (_identifiantJeuSuccesCourant <= 0 || _succesJeuCourant.Count == 0)
        {
            return;
        }

        int versionGrille = ++_etatListeSuccesUi.VersionChargementGrille;
        DemarrerMiseAJourGrilleTousSuccesEnArrierePlan(
            _identifiantJeuSuccesCourant,
            [.. _succesJeuCourant],
            versionGrille
        );
    }

    /*
     * Déplace la sélection du succès courant vers le précédent ou le suivant
     * dans l'ordre actuellement affiché.
     */
    private async Task ExecuterNavigationSuccesEnCoursAsync(int direction)
    {
        if (_identifiantJeuSuccesCourant <= 0 || _succesJeuCourant.Count <= 1)
        {
            return;
        }

        List<GameAchievementV2> succesOrdonnes =
        [
            .. OrdonnerSuccesPourGrilleSelonMode(_identifiantJeuSuccesCourant, _succesJeuCourant),
        ];

        GameAchievementV2? succesCourant = ObtenirSuccesEnCoursSelectionne(succesOrdonnes);

        if (succesCourant is null)
        {
            return;
        }

        int indexCourant = succesOrdonnes.FindIndex(item => item.Id == succesCourant.Id);

        if (indexCourant < 0)
        {
            return;
        }

        int indexCible = Math.Clamp(indexCourant + direction, 0, succesOrdonnes.Count - 1);

        if (indexCible == indexCourant)
        {
            return;
        }

        GameAchievementV2 succesCible = succesOrdonnes[indexCible];
        _etatListeSuccesUi.IdentifiantSuccesEpingle = succesCible.Id;
        _etatListeSuccesUi.IdentifiantSuccesTemporaire = null;
        _etatListeSuccesUi.RetourPremierSuccesApresSelectionTemporaire = false;
        _minuteurAffichageTemporaireSuccesGrille.Stop();
        RafraichirStyleBadgesGrilleSucces();

        await AppliquerSuccesEnCoursAsync(_identifiantJeuSuccesCourant, succesCible, true, true);
    }

    /*
     * Met de côté le succès courant pour faire apparaître le prochain succès
     * non débloqué dans l'ordre de la grille.
     */
    private async Task ExecuterPassageSuccesEnCoursAsync()
    {
        if (_identifiantJeuSuccesCourant <= 0 || _succesJeuCourant.Count == 0)
        {
            return;
        }

        List<GameAchievementV2> succesOrdonnes =
        [
            .. OrdonnerSuccesPourGrilleSelonMode(_identifiantJeuSuccesCourant, _succesJeuCourant),
        ];

        GameAchievementV2? succesCourant = ObtenirSuccesEnCoursSelectionne(succesOrdonnes);

        if (succesCourant is null || SuccesEstDebloquePourAffichage(succesCourant))
        {
            return;
        }

        if (succesOrdonnes.Count(item => !SuccesEstDebloquePourAffichage(item)) <= 1)
        {
            return;
        }

        _etatListeSuccesUi.SuccesPasses.RemoveAll(id => id == succesCourant.Id);
        _etatListeSuccesUi.SuccesPasses.Add(succesCourant.Id);
        SauvegarderOrdreSuccesPasses(_identifiantJeuSuccesCourant, _etatListeSuccesUi.SuccesPasses);

        _etatListeSuccesUi.IdentifiantSuccesEpingle = null;
        _etatListeSuccesUi.IdentifiantSuccesTemporaire = null;
        _etatListeSuccesUi.RetourPremierSuccesApresSelectionTemporaire = false;
        _minuteurAffichageTemporaireSuccesGrille.Stop();

        RedessinerGrilleTousSuccesDepuisEtatLocal();
        await MettreAJourPremierSuccesNonDebloqueAsync(
            _identifiantJeuSuccesCourant,
            _succesJeuCourant
        );
    }

    /*
     * Retourne le succès effectivement représenté dans la carte principale
     * en tenant compte des états temporaire et épinglé.
     */
    private GameAchievementV2? ObtenirSuccesEnCoursSelectionne(
        List<GameAchievementV2> succesOrdonnes
    )
    {
        if (_etatListeSuccesUi.IdentifiantSuccesTemporaire.HasValue)
        {
            GameAchievementV2? succesTemporaire = succesOrdonnes.FirstOrDefault(item =>
                item.Id == _etatListeSuccesUi.IdentifiantSuccesTemporaire.Value
            );

            if (succesTemporaire is not null)
            {
                return succesTemporaire;
            }
        }

        if (_etatListeSuccesUi.IdentifiantSuccesEpingle.HasValue)
        {
            GameAchievementV2? succesEpingle = succesOrdonnes.FirstOrDefault(item =>
                item.Id == _etatListeSuccesUi.IdentifiantSuccesEpingle.Value
            );

            if (succesEpingle is not null)
            {
                return succesEpingle;
            }
        }

        return SelectionnerSuccesEnCours(succesOrdonnes).Succes;
    }

    /*
     * Met à jour l'état des boutons de navigation autour du succès courant
     * selon sa position et son statut de déblocage.
     */
    private void MettreAJourNavigationSuccesEnCours(GameAchievementV2? succesSelectionne)
    {
        if (
            _identifiantJeuSuccesCourant <= 0
            || _succesJeuCourant.Count <= 1
            || succesSelectionne is null
        )
        {
            _vueModele.SuccesEnCours.NavigationVisible = false;
            _vueModele.SuccesEnCours.PrecedentActif = false;
            _vueModele.SuccesEnCours.SuivantActif = false;
            _vueModele.SuccesEnCours.PasserActif = false;
            _vueModele.SuccesEnCours.PrecedentOpacity = 1;
            _vueModele.SuccesEnCours.SuivantOpacity = 1;
            return;
        }

        List<GameAchievementV2> succesOrdonnes =
        [
            .. OrdonnerSuccesPourGrilleSelonMode(_identifiantJeuSuccesCourant, _succesJeuCourant),
        ];
        int indexCourant = succesOrdonnes.FindIndex(item => item.Id == succesSelectionne.Id);

        if (indexCourant < 0)
        {
            _vueModele.SuccesEnCours.NavigationVisible = false;
            _vueModele.SuccesEnCours.PrecedentActif = false;
            _vueModele.SuccesEnCours.SuivantActif = false;
            _vueModele.SuccesEnCours.PasserActif = false;
            _vueModele.SuccesEnCours.PrecedentOpacity = 1;
            _vueModele.SuccesEnCours.SuivantOpacity = 1;
            return;
        }

        _vueModele.SuccesEnCours.NavigationVisible = true;
        _vueModele.SuccesEnCours.PrecedentActif = indexCourant > 0;
        _vueModele.SuccesEnCours.SuivantActif = indexCourant < succesOrdonnes.Count - 1;
        _vueModele.SuccesEnCours.PasserActif =
            !SuccesEstDebloquePourAffichage(succesSelectionne)
            && succesOrdonnes.Count(item => !SuccesEstDebloquePourAffichage(item)) > 1;
        _vueModele.SuccesEnCours.PrecedentOpacity = _vueModele.SuccesEnCours.PrecedentActif
            ? 1
            : 0.38;
        _vueModele.SuccesEnCours.SuivantOpacity = _vueModele.SuccesEnCours.SuivantActif ? 1 : 0.38;
    }

    /*
     * Convertit une image en niveaux de gris pour représenter visuellement
     * un succès encore verrouillé.
     */

    private static ImageSource ConvertirImageEnNoirEtBlanc(ImageSource image)
    {
        if (image is not BitmapSource bitmapSource)
        {
            return image;
        }

        FormatConvertedBitmap bitmapConverti = new();
        bitmapConverti.BeginInit();
        bitmapConverti.Source = bitmapSource;
        bitmapConverti.DestinationFormat = PixelFormats.Gray32Float;
        bitmapConverti.EndInit();
        bitmapConverti.Freeze();
        return bitmapConverti;
    }
}
