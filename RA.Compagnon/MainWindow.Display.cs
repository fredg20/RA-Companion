using System.Globalization;
using System.Windows;
using System.Windows.Media;
using RA.Compagnon.Modeles.Api.V2.Achievement;
using RA.Compagnon.Modeles.Api.V2.Game;
using RA.Compagnon.Modeles.Presentation;
using RA.Compagnon.Services;
using SystemControls = System.Windows.Controls;

/*
 * Centralise la préparation et la mise à jour des informations visibles du
 * jeu courant, de ses métadonnées et des succès récents.
 */
namespace RA.Compagnon;

/*
 * Porte la logique d'affichage des métadonnées textuelles et visuelles liées
 * au jeu courant dans la fenêtre principale.
 */
public partial class MainWindow
{
    /*
     * Réinitialise entièrement les métadonnées de console et les informations
     * secondaires du jeu courant dans le ViewModel.
     */
    private void ReinitialiserMetaConsoleJeuEnCours()
    {
        if (DoitVerrouillerAffichageSurDernierJeuActifRecemment())
        {
            JournaliserDiagnosticAffichageJeu("meta_reinitialisation_ignoree_actif_recemment");
            return;
        }

        _identifiantJeuMetaConsoleAffichee = 0;
        _vueModele.JeuCourant.ImageConsole = null;
        _vueModele.JeuCourant.ImageConsoleVisible = false;
        _vueModele.JeuCourant.Console = string.Empty;
        _vueModele.JeuCourant.ConsoleVisible = false;
        _vueModele.JeuCourant.Genre = string.Empty;
        _vueModele.JeuCourant.GenreVisible = false;
        _vueModele.JeuCourant.Credits = string.Empty;
        _vueModele.JeuCourant.CreditsVisible = false;
        _vueModele.JeuCourant.DateSortie = string.Empty;
        _vueModele.JeuCourant.DateSortieVisible = false;
        _vueModele.JeuCourant.TempsDeJeu = string.Empty;
        _vueModele.JeuCourant.TempsDeJeuVisible = false;
        _vueModele.JeuCourant.Details = string.Empty;
        _vueModele.JeuCourant.DetailsVisible = false;
        _vueModele.JeuCourant.InformationsVisibles = false;
        JournaliserDiagnosticAffichageJeu("meta_reinitialisee");
    }

    /*
     * Prépare le changement de métadonnées pour un nouveau jeu et indique si
     * une remise à zéro préalable était nécessaire.
     */
    private bool PreparerAffichageMetaConsoleJeuEnCours(GameInfoAndUserProgressV2 jeu)
    {
        bool reinitialisationNecessaire = _identifiantJeuMetaConsoleAffichee != jeu.Id;

        if (reinitialisationNecessaire)
        {
            ReinitialiserMetaConsoleJeuEnCours();
        }

        _identifiantJeuMetaConsoleAffichee = jeu.Id;
        DefinirTitreJeuEnCours(jeu.Title);
        JournaliserDiagnosticAffichageJeu(
            "meta_preparee",
            $"jeu={jeu.Id};reinit={(reinitialisationNecessaire ? "oui" : "non")}"
        );

        return reinitialisationNecessaire;
    }

    /*
     * Applique immédiatement les métadonnées déjà disponibles avant les
     * enrichissements asynchrones complémentaires.
     */
    private void AppliquerMetaConsoleJeuEnCoursInitiale(GameInfoAndUserProgressV2 jeu)
    {
        bool reinitialisationNecessaire = PreparerAffichageMetaConsoleJeuEnCours(jeu);

        string dateSortieComplete = FormaterDateSortieJeu(jeu.Released);

        if (!string.IsNullOrWhiteSpace(jeu.ConsoleName))
        {
            _vueModele.JeuCourant.Console = jeu.ConsoleName.Trim();
            _vueModele.JeuCourant.ConsoleVisible = true;
        }

        if (!string.IsNullOrWhiteSpace(jeu.Genre))
        {
            _vueModele.JeuCourant.Genre = jeu.Genre.Trim();
            _vueModele.JeuCourant.GenreVisible = true;
        }

        string creditsJeu = ConstruireCreditsJeu(jeu);

        if (!string.IsNullOrWhiteSpace(creditsJeu))
        {
            _vueModele.JeuCourant.Credits = creditsJeu;
            _vueModele.JeuCourant.CreditsVisible = true;
        }

        if (!string.IsNullOrWhiteSpace(dateSortieComplete))
        {
            _vueModele.JeuCourant.DateSortie = dateSortieComplete;
            _vueModele.JeuCourant.DateSortieVisible = true;
        }

        if (jeu.UserTotalPlaytime > 0)
        {
            _vueModele.JeuCourant.TempsDeJeu = FormaterTempsJeuTotal(jeu.UserTotalPlaytime);
            _vueModele.JeuCourant.TempsDeJeuVisible = true;
        }

        MettreAJourVisibiliteInformationsJeuEnCours();
        JournaliserDiagnosticAffichageJeu(
            "meta_initiale",
            $"jeu={jeu.Id};reinit={reinitialisationNecessaire};console={_vueModele.JeuCourant.Console};genre={_vueModele.JeuCourant.Genre};credits={_vueModele.JeuCourant.Credits};sortie={_vueModele.JeuCourant.DateSortie};temps={_vueModele.JeuCourant.TempsDeJeu}"
        );
    }

    /*
     * Lance en arrière-plan l'enrichissement distant des métadonnées du jeu.
     */
    private void DemarrerEnrichissementMetaConsoleJeuEnCours(GameInfoAndUserProgressV2 jeu)
    {
        LancerTacheNonBloquante(
            EnrichirMetaConsoleJeuEnCoursAsync(jeu),
            "enrichissement_meta_console"
        );
    }

    /*
     * Complète les métadonnées du jeu avec la traduction du genre et
     * l'icône de console lorsqu'elles sont disponibles.
     */
    private async Task EnrichirMetaConsoleJeuEnCoursAsync(GameInfoAndUserProgressV2 jeu)
    {
        try
        {
            string genreAffiche = jeu.Genre?.Trim() ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(jeu.Genre))
            {
                genreAffiche = (
                    await _serviceTraductionTexte.TraduireVersFrancaisAsync(jeu.Genre)
                ).Trim();
            }

            ImageSource? imageConsole = null;

            try
            {
                IReadOnlyList<ConsoleV2> consoles =
                    await _serviceCatalogueRetroAchievements.ObtenirConsolesAsync(
                        _configurationConnexion.CleApiWeb
                    );
                ConsoleV2? console = consoles.FirstOrDefault(item =>
                    item.ConsoleId == jeu.ConsoleId
                );

                if (console is not null && !string.IsNullOrWhiteSpace(console.IconUrl))
                {
                    if (_configurationConnexion.DernierJeuAffiche?.Id == jeu.Id)
                    {
                        _configurationConnexion.DernierJeuAffiche.ImageConsole =
                            console.IconUrl.Trim();
                    }

                    imageConsole = await ChargerImageDistanteAsync(console.IconUrl);
                }
            }
            catch { }

            if (_dernierIdentifiantJeuAvecInfos != jeu.Id)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(genreAffiche))
            {
                _vueModele.JeuCourant.Genre = genreAffiche;
                _vueModele.JeuCourant.GenreVisible = true;
            }

            if (imageConsole is not null)
            {
                _vueModele.JeuCourant.ImageConsole = imageConsole;
                _vueModele.JeuCourant.ImageConsoleVisible = true;
            }

            _vueModele.JeuCourant.ConsoleVisible =
                _vueModele.JeuCourant.ImageConsoleVisible
                || !string.IsNullOrWhiteSpace(_vueModele.JeuCourant.Console);
            MettreAJourVisibiliteInformationsJeuEnCours();
        }
        catch { }
    }

    /*
     * Met à jour le texte de détails du jeu et sa visibilité associée.
     */
    private void DefinirDetailsJeuEnCours(string details)
    {
        _vueModele.JeuCourant.Details = details;
        _vueModele.JeuCourant.DetailsVisible = !string.IsNullOrWhiteSpace(details);
        JournaliserDiagnosticAffichageJeu(
            "details_jeu",
            $"visible={_vueModele.JeuCourant.DetailsVisible};details={details}"
        );
    }

    /*
     * Met à jour le temps de jeu affiché sous l'image principale.
     */
    private void DefinirTempsJeuSousImage(string tempsJeu)
    {
        _vueModele.JeuCourant.TempsDeJeu = tempsJeu;
        _vueModele.JeuCourant.TempsDeJeuVisible = !string.IsNullOrWhiteSpace(tempsJeu);
        MettreAJourVisibiliteInformationsJeuEnCours();
    }

    /*
     * Synchronise le libellé d'état du jeu affiché dans la zone de progression.
     */
    private void DefinirEtatJeuDansProgression(string etat)
    {
        _vueModele.JeuCourant.Etat = string.IsNullOrWhiteSpace(etat) ? string.Empty : etat;
    }

    /*
     * Définit le titre générique de la carte de jeu lorsqu'aucun jeu actif
     * spécifique n'est encore chargé.
     */
    private void DefinirTitreZoneJeu()
    {
        _vueModele.TitreCarteJeuEnCours = "Dernier jeu joué";
    }

    /*
     * Réagit aux changements de taille de l'image principale pour remettre à
     * jour son conteneur et son écrêtage.
     */
    private void ImageJeuEnCours_TailleChangee(object sender, SizeChangedEventArgs e)
    {
        MettreAJourDimensionsZoneJeuSelonVisuel();
        AppliquerCoinsArrondisImageJeuEnCours();
    }

    /*
     * Réapplique l'écrêtage arrondi du succès mis en avant lorsqu'il change
     * de taille à l'écran.
     */
    private void ImagePremierSuccesNonDebloque_TailleChangee(object sender, SizeChangedEventArgs e)
    {
        AppliquerCoinsArrondisImagePremierSuccesNonDebloque();
    }

    /*
     * Fait reculer manuellement le carrousel de visuels du jeu courant.
     */
    private async Task ExecuterActionVisuelJeuPrecedentAsync()
    {
        if (_visuelsJeuEnCours.Count <= 1)
        {
            return;
        }

        _indexVisuelJeuEnCours--;
        await MettreAJourAffichageVisuelJeuEnCoursAsync();
    }

    /*
     * Fait avancer manuellement le carrousel de visuels du jeu courant.
     */
    private async Task ExecuterActionVisuelJeuSuivantAsync()
    {
        if (_visuelsJeuEnCours.Count <= 1)
        {
            return;
        }

        _indexVisuelJeuEnCours++;
        await MettreAJourAffichageVisuelJeuEnCoursAsync();
    }

    /*
     * Reprogramme l'animation du titre lorsque la largeur de sa zone change
     * réellement après une mise en page.
     */
    private void TitreJeuEnCours_MiseEnPageChangee(object sender, SizeChangedEventArgs e)
    {
        if (!ReferenceEquals(sender, ConteneurTitreJeuEnCours))
        {
            return;
        }

        if (Math.Abs(e.NewSize.Width - e.PreviousSize.Width) < 0.5)
        {
            return;
        }

        PlanifierMiseAJourAnimationTitreJeuEnCours();
    }

    /*
     * Réapplique les coins arrondis sur l'image active et sur l'image de
     * transition du carrousel.
     */
    private void AppliquerCoinsArrondisImageJeuEnCours()
    {
        AppliquerCoinsArrondisImage(ImageJeuEnCours);
        AppliquerCoinsArrondisImage(ImageJeuEnCoursTransition);
    }

    /*
     * Ajuste les dimensions minimales de la zone d'image pour éviter les
     * sauts de layout lorsque les visuels changent.
     */
    private void MettreAJourDimensionsZoneJeuSelonVisuel()
    {
        if (ConteneurImageJeuEnCours is null || ColonneImageJeuEnCours is null)
        {
            return;
        }

        double largeurVisible = Math.Max(
            ImageJeuEnCours.ActualWidth,
            ImageJeuEnCoursTransition.ActualWidth
        );
        double hauteurVisible = Math.Max(
            ImageJeuEnCours.ActualHeight,
            ImageJeuEnCoursTransition.ActualHeight
        );

        if (largeurVisible <= 0 || hauteurVisible <= 0)
        {
            return;
        }

        _largeurMaxVisuelJeuEnCours = Math.Max(_largeurMaxVisuelJeuEnCours, largeurVisible);
        _hauteurMaxVisuelJeuEnCours = Math.Max(_hauteurMaxVisuelJeuEnCours, hauteurVisible);

        ConteneurImageJeuEnCours.MinWidth = _largeurMaxVisuelJeuEnCours;
        ConteneurImageJeuEnCours.MinHeight = _hauteurMaxVisuelJeuEnCours;
        ColonneImageJeuEnCours.MinWidth = _largeurMaxVisuelJeuEnCours;
    }

    /*
     * Applique les coins arrondis au badge du premier succès non débloqué.
     */
    private void AppliquerCoinsArrondisImagePremierSuccesNonDebloque()
    {
        AppliquerCoinsArrondisImage(ImagePremierSuccesNonDebloque);
    }

    /*
     * Écrête une image dans un rectangle à coins arrondis cohérent avec le
     * style général de l'application.
     */
    private void AppliquerCoinsArrondisImage(SystemControls.Image image)
    {
        if (image.ActualWidth <= 0 || image.ActualHeight <= 0)
        {
            return;
        }

        double rayon = ObtenirRayonCoins("RayonCoinsPetit", 8).TopLeft;
        image.Clip = new RectangleGeometry(
            new Rect(0, 0, image.ActualWidth, image.ActualHeight),
            rayon,
            rayon
        );
    }

    /*
     * Exécute l'actualisation périodique de l'API tant que la configuration
     * est complète et qu'aucun chargement n'est déjà en cours.
     */
    private async void ActualisationApi_Tick(object? sender, EventArgs e)
    {
        if (!ConfigurationConnexionEstComplete())
        {
            ArreterActualisationAutomatique();
            return;
        }

        if (_chargementJeuEnCoursActif)
        {
            return;
        }

        _minuteurActualisationApi.Stop();

        try
        {
            await ChargerJeuEnCoursAsync(false, false);
        }
        catch (Exception exception)
        {
            JournaliserExceptionNonBloquante("actualisation_api_periodique", exception);
        }
        finally
        {
            if (ConfigurationConnexionEstComplete() && _profilUtilisateurAccessible)
            {
                _minuteurActualisationApi.Start();
            }
        }
    }

    /*
     * Réinitialise l'affichage textuel des succès récents vers un état neutre.
     */
    private void ReinitialiserSuccesRecents()
    {
        AppliquerSuccesRecents(ServicePresentationActivite.ConstruireEtatNeutre());
    }

    /*
     * Applique dans l'interface les lignes de texte représentant les succès
     * récents ou l'état neutre associé.
     */
    private void AppliquerSuccesRecents(ActiviteRecenteAffichee activiteRecente)
    {
        string[] lignes =
        [
            "Aucun autre succès récent.",
            "Aucun autre succès récent.",
            "Aucun autre succès récent.",
        ];

        for (int index = 0; index < Math.Min(3, activiteRecente.Lignes.Count); index++)
        {
            lignes[index] = activiteRecente.Lignes[index];
        }

        TexteEtatSuccesRecents.Text = activiteRecente.TexteEtat;
        TexteSuccesRecent1.Text = lignes[0];
        TexteSuccesRecent2.Text = lignes[1];
        TexteSuccesRecent3.Text = lignes[2];
    }

    /*
     * Réinitialise l'ensemble de l'affichage du jeu courant et de ses
     * dépendances visuelles lorsqu'aucun jeu n'est disponible.
     */
    private void ReinitialiserJeuEnCours()
    {
        _serviceOrchestrateurEtatJeu.Reinitialiser();
        ReinitialiserPipelineChargementJeu();
        DefinirTitreZoneJeu();
        _dernierIdentifiantJeuAvecInfos = 0;
        _dernierIdentifiantJeuAvecProgression = 0;
        ReinitialiserCarrouselVisuelsJeuEnCours();
        ReinitialiserImageJeuEnCours();
        ReinitialiserPremierSuccesNonDebloque();
        ReinitialiserGrilleTousSucces();
        _identifiantJeuSuccesObserve = 0;
        _etatSuccesObserves = [];
        ReinitialiserMetaConsoleJeuEnCours();
        DefinirTempsJeuSousImage(string.Empty);
        DefinirEtatJeuDansProgression(string.Empty);
        DefinirTitreJeuEnCours(string.Empty);
        DefinirDetailsJeuEnCours(string.Empty);
        ReinitialiserVueDetailleeJeuEnCours();
        EnregistrerPhaseAucunJeuOrchestrateur("non_configure");
        ReinitialiserSuccesRecents();
    }

    /*
     * Indique si la progression déjà affichée peut être conservée pour le jeu
     * courant afin d'éviter un clignotement inutile.
     */
    private bool PeutConserverProgressionAffichee(int identifiantJeu)
    {
        return identifiantJeu > 0 && _dernierIdentifiantJeuAvecProgression == identifiantJeu;
    }

    /*
     * Indique si les informations détaillées déjà affichées restent valides
     * pour le jeu demandé.
     */
    private bool PeutConserverInfosJeuAffichees(int identifiantJeu)
    {
        return identifiantJeu > 0 && _dernierIdentifiantJeuAvecInfos == identifiantJeu;
    }

    /*
     * Convertit la date de sortie du jeu en libellé français lisible.
     */
    private static string FormaterDateSortieJeu(string dateSortie)
    {
        if (string.IsNullOrWhiteSpace(dateSortie))
        {
            return string.Empty;
        }

        if (
            DateTimeOffset.TryParse(
                dateSortie,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal,
                out DateTimeOffset dateParsee
            )
        )
        {
            return dateParsee.ToString("d MMMM yyyy", CultureInfo.GetCultureInfo("fr-CA"));
        }

        if (
            DateTime.TryParse(
                dateSortie,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces,
                out DateTime dateSimple
            )
        )
        {
            return dateSimple.ToString("d MMMM yyyy", CultureInfo.GetCultureInfo("fr-CA"));
        }

        return dateSortie.Trim();
    }

    /*
     * Construit le texte de crédits affiché à partir du développeur fourni
     * par l'API du jeu.
     */
    private static string ConstruireCreditsJeu(GameInfoAndUserProgressV2 jeu)
    {
        if (!string.IsNullOrWhiteSpace(jeu.Developer))
        {
            return jeu.Developer.Trim();
        }

        return string.Empty;
    }

    /*
     * Calcule si la grille d'informations secondaires du jeu doit être visible
     * en fonction des lignes réellement renseignées.
     */
    private void MettreAJourVisibiliteInformationsJeuEnCours()
    {
        bool auMoinsUneLigneVisible =
            _vueModele.JeuCourant.ConsoleVisible
            || _vueModele.JeuCourant.GenreVisible
            || _vueModele.JeuCourant.CreditsVisible
            || _vueModele.JeuCourant.DateSortieVisible
            || _vueModele.JeuCourant.TempsDeJeuVisible
            || _vueModele.JeuCourant.DetailsVisible;

        _vueModele.JeuCourant.InformationsVisibles = auMoinsUneLigneVisible;
        JournaliserDiagnosticAffichageJeu(
            "visibilite_meta",
            $"grille={_vueModele.JeuCourant.InformationsVisibles};console={_vueModele.JeuCourant.ConsoleVisible};genre={_vueModele.JeuCourant.GenreVisible};credits={_vueModele.JeuCourant.CreditsVisible};sortie={_vueModele.JeuCourant.DateSortieVisible};temps={_vueModele.JeuCourant.TempsDeJeuVisible};details={_vueModele.JeuCourant.DetailsVisible}"
        );
    }

    /*
     * Formate une durée totale en secondes sous une forme courte en jours,
     * heures et minutes.
     */
    private static string FormaterTempsJeuTotal(int totalSecondes)
    {
        if (totalSecondes <= 0)
        {
            return "0 min";
        }

        int totalMinutes = totalSecondes / 60;
        int jours = totalMinutes / (24 * 60);
        int heures = (totalMinutes % (24 * 60)) / 60;
        int minutes = totalMinutes % 60;
        List<string> segments = [];

        if (jours > 0)
        {
            segments.Add(jours == 1 ? "1 j" : $"{jours} j");
        }

        if (heures > 0)
        {
            segments.Add(heures == 1 ? "1 h" : $"{heures} h");
        }

        if (minutes > 0 || segments.Count == 0)
        {
            segments.Add(minutes == 1 ? "1 min" : $"{minutes} min");
        }

        return string.Join(" ", segments);
    }
}
