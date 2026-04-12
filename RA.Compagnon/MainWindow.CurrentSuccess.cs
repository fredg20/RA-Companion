using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RA.Compagnon.Modeles.Api.V2.Game;
using RA.Compagnon.Modeles.Etat;
using RA.Compagnon.Modeles.Local;
using RA.Compagnon.Modeles.Presentation;
using RA.Compagnon.Services;

namespace RA.Compagnon;

public partial class MainWindow
{
    /// <summary>
    /// Réinitialise la zone du premier succès non débloqué.
    /// </summary>
    private void ReinitialiserPremierSuccesNonDebloque()
    {
        _vueModele.SuccesEnCours.Image = null;
        ImagePremierSuccesNonDebloque.Clip = null;
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
        MettreAJourNavigationSuccesEnCours(null);
    }

    /// <summary>
    /// Réinitialise la grille de tous les rétrosuccès.
    /// </summary>
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

    /// <summary>
    /// Efface les zones de rétrosuccès et leur état persisté pour éviter de garder ceux d'un ancien jeu.
    /// </summary>
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

    private void ReinitialiserEtatSuccesTemporairesSession()
    {
        _succesDebloquesLocauxTemporaires.Clear();
        _succesDetectesRecemment.Clear();
        _succesDebloqueDetecteEnAttente = null;
        _etatListeSuccesUi.IdentifiantSuccesTemporaire = null;
        _etatListeSuccesUi.RetourPremierSuccesApresSelectionTemporaire = false;
        _minuteurAffichageTemporaireSuccesGrille.Stop();
    }

    /// <summary>
    /// Met • jour l'affichage des succès du jeu courant.
    /// </summary>
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

    private void DemarrerMiseAJourSuccesJeuEnArrierePlan(GameInfoAndUserProgressV2 jeu)
    {
        _ = MettreAJourSuccesJeuEnArrierePlanAsync(jeu);
    }

    private async Task MettreAJourSuccesJeuEnArrierePlanAsync(GameInfoAndUserProgressV2 jeu)
    {
        try
        {
            await MettreAJourSuccesJeuAsync(jeu);
        }
        catch
        {
            // Les succès enrichissent l'affichage, mais ne doivent pas bloquer la carte principale.
        }
    }

    /// <summary>
    /// Charge la grille complète des succès sans bloquer l'affichage du succès principal.
    /// </summary>
    private void DemarrerMiseAJourGrilleTousSuccesEnArrierePlan(
        int identifiantJeu,
        List<GameAchievementV2> succes,
        int versionGrille
    )
    {
        _ = MettreAJourGrilleTousSuccesEnArrierePlanAsync(identifiantJeu, succes, versionGrille);
    }

    /// <summary>
    /// Exécute le remplissage complet de la grille en arrière-plan et ignore les erreurs non critiques.
    /// </summary>
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
        catch
        {
            // La grille complète enrichit l'interface, mais ne doit pas bloquer le rendu principal.
        }
    }

    /// <summary>
    /// Choisit le rétrosuccès en cours en suivant l'ordre réel de la grille quand aucun badge n'est sélectionné.
    /// </summary>
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

    /// <summary>
    /// Met • jour la carte du premier succès restant • débloquer.
    /// </summary>
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

    /// <summary>
    /// Applique le succès choisi à la carte principale, qu'il provienne du mode automatique ou d'un clic sur la grille.
    /// </summary>
    private Task AppliquerSuccesEnCoursAsync(
        int identifiantJeu,
        GameAchievementV2? succesSelectionne,
        bool doitSauvegarder,
        bool estEpingleManuellement
    )
    {
        if (succesSelectionne is null)
        {
            _versionAffichageSuccesEnCours++;
            _vueModele.SuccesEnCours.Image = null;
            _vueModele.SuccesEnCours.ImageVisible = false;
            _vueModele.SuccesEnCours.ImageOpacity = 0.58;
            _vueModele.SuccesEnCours.TexteVisuel = "Tous les succès sont débloqués";
            _vueModele.SuccesEnCours.TexteVisuelVisible = true;
            _vueModele.SuccesEnCours.Titre = "Tous les succès sont obtenus";
            _vueModele.SuccesEnCours.TitreVisible = true;
            _vueModele.SuccesEnCours.Description = "Ce jeu ne contient plus de succès • débloquer.";
            _vueModele.SuccesEnCours.DescriptionVisible = true;
            _vueModele.SuccesEnCours.DetailsPoints = string.Empty;
            _vueModele.SuccesEnCours.DetailsPointsVisible = false;
            _vueModele.SuccesEnCours.DetailsFaisabilite = string.Empty;
            _vueModele.SuccesEnCours.DetailsFaisabiliteVisible = false;
            _vueModele.SuccesEnCours.ToolTipDetailsFaisabilite = string.Empty;
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
        _vueModele.SuccesEnCours.ImageOpacity = succesAffiche.EstDebloque ? 1 : 0.58;
        _vueModele.SuccesEnCours.ImageVisible = false;
        _vueModele.SuccesEnCours.TexteVisuel = string.Empty;
        _vueModele.SuccesEnCours.TexteVisuelVisible = false;

        _vueModele.SuccesEnCours.Titre = succesAffiche.Titre;
        _vueModele.SuccesEnCours.TitreVisible = true;
        _vueModele.SuccesEnCours.Description = succesAffiche.Description.Trim();
        _vueModele.SuccesEnCours.DescriptionVisible = true;
        _vueModele.SuccesEnCours.DetailsPoints = succesAffiche.DetailsPoints;
        _vueModele.SuccesEnCours.DetailsPointsVisible = !string.IsNullOrWhiteSpace(
            succesAffiche.DetailsPoints
        );
        _vueModele.SuccesEnCours.DetailsFaisabilite = succesAffiche.DetailsFaisabilite;
        _vueModele.SuccesEnCours.DetailsFaisabiliteVisible = !string.IsNullOrWhiteSpace(
            succesAffiche.DetailsFaisabilite
        );
        _vueModele.SuccesEnCours.ToolTipDetailsFaisabilite = succesAffiche.ExplicationFaisabilite;
        MettreAJourNavigationSuccesEnCours(succesSelectionne);

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
        }
        catch
        {
            // Le contenu principal du succès doit rester instantané même si l'image ou la traduction tardent.
        }
    }

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
        ServiceSurveillanceSuccesLocaux.JournaliserEvenement(
            "succes_ui_affiche",
            $"jeu={succesDetecte.IdentifiantJeu};succes={succesDetecte.IdentifiantSucces};titre={succesDetecte.TitreSucces}"
        );
        return true;
    }

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

    private void MarquerSuccesCommeTraite(SuccesDebloqueDetecte succes)
    {
        string signature = ConstruireSignatureSuccesTraite(succes);
        _succesDetectesRecemment[signature] = DateTimeOffset.UtcNow;
    }

    private static string ConstruireSignatureSuccesTraite(SuccesDebloqueDetecte succes)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{succes.IdentifiantJeu}|{succes.IdentifiantSucces}"
        );
    }

    private void RafraichirSuccesEtProgressionApresDeblocageLocal()
    {
        RafraichirStyleBadgesGrilleSucces();
        MettreAJourProgressionJeuDepuisSuccesLocaux();
        RedessinerGrilleTousSuccesDepuisEtatLocal();
    }

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
                    ? "Jeu complété"
                    : "Progression en cours";
            _dernierJeuAfficheModifie = true;
        }
    }

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

    /// <summary>
    /// Convertit une image en niveaux de gris pour l'affichage des succès verrouillés.
    /// </summary>
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
