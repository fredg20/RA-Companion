using System.Globalization;
using System.Windows;
using System.Windows.Media;
using RA.Compagnon.Modeles.Api.V2.Achievement;
using RA.Compagnon.Modeles.Api.V2.Game;
using RA.Compagnon.Modeles.Presentation;
using RA.Compagnon.Services;
using SystemControls = System.Windows.Controls;

namespace RA.Compagnon;

public partial class MainWindow
{
    /// <summary>
    /// Réinitialise l'affichage des métadonnées sous le titre du jeu courant.
    /// </summary>
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

    /// <summary>
    /// Met à jour l'année du jeu, sa console, son type, ses crédits et l'icône officielle.
    /// </summary>
    /// <summary>
    /// Affiche immédiatement les métadonnées déjà connues du jeu sans attendre les enrichissements lents.
    /// </summary>
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

    /// <summary>
    /// Lance les enrichissements secondaires des métadonnées sans bloquer le rendu initial.
    /// </summary>
    private void DemarrerEnrichissementMetaConsoleJeuEnCours(GameInfoAndUserProgressV2 jeu)
    {
        _ = EnrichirMetaConsoleJeuEnCoursAsync(jeu);
    }

    /// <summary>
    /// Traduit le genre et charge l'icône de console après l'affichage initial.
    /// </summary>
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
                    imageConsole = await ChargerImageDistanteAsync(console.IconUrl);
                }
            }
            catch
            {
                // L'icône de console reste facultative.
            }

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
        catch
        {
            // Les enrichissements restent facultatifs.
        }
    }

    /// <summary>
    /// Met à jour la ligne de détails sous le type et le développeur du jeu.
    /// </summary>
    private void DefinirDetailsJeuEnCours(string details)
    {
        _vueModele.JeuCourant.Details = details;
        _vueModele.JeuCourant.DetailsVisible = !string.IsNullOrWhiteSpace(details);
        JournaliserDiagnosticAffichageJeu(
            "details_jeu",
            $"visible={_vueModele.JeuCourant.DetailsVisible};details={details}"
        );
    }

    /// <summary>
    /// Met à jour le temps de jeu affiché sous l'image du jeu.
    /// </summary>
    private void DefinirTempsJeuSousImage(string tempsJeu)
    {
        _vueModele.JeuCourant.TempsDeJeu = tempsJeu;
        _vueModele.JeuCourant.TempsDeJeuVisible = !string.IsNullOrWhiteSpace(tempsJeu);
        MettreAJourVisibiliteInformationsJeuEnCours();
    }

    /// <summary>
    /// Met à jour l'état du jeu dans l'en-tête de la carte de progression.
    /// </summary>
    private void DefinirEtatJeuDansProgression(string etat)
    {
        _vueModele.JeuCourant.Etat = string.IsNullOrWhiteSpace(etat) ? string.Empty : etat;
    }

    private void DefinirTitreZoneJeu()
    {
        _vueModele.TitreCarteJeuEnCours = "Dernier jeu joué";
    }

    /// <summary>
    /// Recalcule la découpe arrondie de l'image du jeu quand sa taille change.
    /// </summary>
    private void ImageJeuEnCours_TailleChangee(object sender, SizeChangedEventArgs e)
    {
        MettreAJourDimensionsZoneJeuSelonVisuel();
        AppliquerCoinsArrondisImageJeuEnCours();
    }

    /// <summary>
    /// Recalcule la découpe arrondie du badge du premier succès quand sa taille change.
    /// </summary>
    private void ImagePremierSuccesNonDebloque_TailleChangee(object sender, SizeChangedEventArgs e)
    {
        AppliquerCoinsArrondisImagePremierSuccesNonDebloque();
    }

    /// <summary>
    /// Affiche le visuel précédent du jeu courant.
    /// </summary>
    private async Task ExecuterActionVisuelJeuPrecedentAsync()
    {
        if (_visuelsJeuEnCours.Count <= 1)
        {
            return;
        }

        _indexVisuelJeuEnCours--;
        await MettreAJourAffichageVisuelJeuEnCoursAsync();
    }

    /// <summary>
    /// Affiche le visuel suivant du jeu courant.
    /// </summary>
    private async Task ExecuterActionVisuelJeuSuivantAsync()
    {
        if (_visuelsJeuEnCours.Count <= 1)
        {
            return;
        }

        _indexVisuelJeuEnCours++;
        await MettreAJourAffichageVisuelJeuEnCoursAsync();
    }

    /// <summary>
    /// Recalcule le défilement du titre quand sa taille ou celle de son conteneur change.
    /// </summary>
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

    /// <summary>
    /// Applique les coins arrondis à l'image du jeu courant selon sa taille réelle.
    /// </summary>
    private void AppliquerCoinsArrondisImageJeuEnCours()
    {
        AppliquerCoinsArrondisImage(ImageJeuEnCours);
        AppliquerCoinsArrondisImage(ImageJeuEnCoursTransition);
    }

    /// <summary>
    /// Conserve la plus grande taille de visuel rencontrée pour stabiliser la zone du jeu.
    /// </summary>
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

    /// <summary>
    /// Applique les coins arrondis au badge du premier succès selon sa taille réelle.
    /// </summary>
    private void AppliquerCoinsArrondisImagePremierSuccesNonDebloque()
    {
        AppliquerCoinsArrondisImage(ImagePremierSuccesNonDebloque);
    }

    /// <summary>
    /// Applique une découpe arrondie à une image selon sa taille réelle.
    /// </summary>
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

    /// <summary>
    /// Déclenche un cycle périodique d'actualisation API.
    /// </summary>
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

        await ChargerJeuEnCoursAsync(false, false);
    }

    /// <summary>
    /// Réinitialise la section des succès récents sur un état neutre.
    /// </summary>
    private void ReinitialiserSuccesRecents()
    {
        AppliquerSuccesRecents(ServicePresentationActivite.ConstruireEtatNeutre());
    }

    /// <summary>
    /// Remplit les lignes de la section des succès récents.
    /// </summary>
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

    /// <summary>
    /// Réinitialise la section "Jeu en cours" sur un état neutre.
    /// </summary>
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

    /// <summary>
    /// Indique si la progression affichée peut être conservée pour le même jeu.
    /// </summary>
    private bool PeutConserverProgressionAffichee(int identifiantJeu)
    {
        return identifiantJeu > 0 && _dernierIdentifiantJeuAvecProgression == identifiantJeu;
    }

    /// <summary>
    /// Indique si les informations visibles du jeu peuvent être conservées pour le même jeu.
    /// </summary>
    private bool PeutConserverInfosJeuAffichees(int identifiantJeu)
    {
        return identifiantJeu > 0 && _dernierIdentifiantJeuAvecInfos == identifiantJeu;
    }

    /// <summary>
    /// Extrait l'année de sortie d'un jeu à partir du champ API de date.
    /// </summary>
    /// <summary>
    /// Formate la date de sortie la plus complète possible pour l'affichage.
    /// </summary>
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

    /// <summary>
    /// Construit une ligne courte pour le développeur du jeu.
    /// </summary>
    private static string ConstruireCreditsJeu(GameInfoAndUserProgressV2 jeu)
    {
        if (!string.IsNullOrWhiteSpace(jeu.Developer))
        {
            return jeu.Developer.Trim();
        }

        return string.Empty;
    }

    /// <summary>
    /// Affiche la grille d'informations du jeu uniquement si au moins une ligne est utile.
    /// </summary>
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

    /// <summary>
    /// Formate une durée exprimée en secondes en texte français lisible.
    /// </summary>
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
