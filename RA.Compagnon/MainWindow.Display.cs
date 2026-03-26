using System.Globalization;
using System.Windows;
using System.Windows.Media;
using RA.Compagnon.Modeles.Api;
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
        ImageConsoleJeuEnCours.Source = null;
        ImageConsoleJeuEnCours.Visibility = Visibility.Collapsed;
        TexteAnneeJeuEnCours.Text = string.Empty;
        TexteAnneeJeuEnCours.Visibility = Visibility.Collapsed;
        TexteConsoleJeuEnCours.Text = string.Empty;
        TexteConsoleJeuEnCours.Visibility = Visibility.Collapsed;
        TexteTypeJeuEnCours.Text = string.Empty;
        TexteTypeJeuEnCours.Visibility = Visibility.Collapsed;
        TexteDeveloppeurJeuEnCours.Text = string.Empty;
        TexteDeveloppeurJeuEnCours.Visibility = Visibility.Collapsed;
        LigneMetaJeuEnCours.Visibility = Visibility.Collapsed;
    }

    /// <summary>
    /// Met à jour l'année du jeu, sa console, son type, son développeur et l'icône officielle.
    /// </summary>
    private async Task MettreAJourMetaConsoleJeuEnCoursAsync(JeuUtilisateurRetroAchievements jeu)
    {
        ReinitialiserMetaConsoleJeuEnCours();

        string anneeJeu = ExtraireAnneeJeu(jeu.DateSortie);
        DefinirTitreJeuEnCours(jeu.Titre);

        if (!string.IsNullOrWhiteSpace(anneeJeu))
        {
            TexteAnneeJeuEnCours.Text = anneeJeu;
            TexteAnneeJeuEnCours.Visibility = Visibility.Visible;
        }

        if (!string.IsNullOrWhiteSpace(jeu.NomConsole))
        {
            TexteConsoleJeuEnCours.Text = jeu.NomConsole.Trim();
            TexteConsoleJeuEnCours.Visibility = Visibility.Visible;
        }

        if (!string.IsNullOrWhiteSpace(jeu.Genre))
        {
            string genreTraduit = await _serviceTraductionTexte.TraduireVersFrancaisAsync(
                jeu.Genre
            );
            TexteTypeJeuEnCours.Text = genreTraduit.Trim();
            TexteTypeJeuEnCours.Visibility = string.IsNullOrWhiteSpace(TexteTypeJeuEnCours.Text)
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        if (!string.IsNullOrWhiteSpace(jeu.Developpeur))
        {
            TexteDeveloppeurJeuEnCours.Text = jeu.Developpeur.Trim();
            TexteDeveloppeurJeuEnCours.Visibility = Visibility.Visible;
        }

        try
        {
            IReadOnlyList<ConsoleRetroAchievements> consoles =
                await ClientRetroAchievements.ObtenirConsolesAsync(
                    _configurationConnexion.CleApiWeb
                );
            ConsoleRetroAchievements? console = consoles.FirstOrDefault(item =>
                item.IdentifiantConsole == jeu.IdentifiantConsole
            );

            if (console is not null && !string.IsNullOrWhiteSpace(console.UrlIcone))
            {
                ImageSource? imageConsole = await ChargerImageDistanteAsync(console.UrlIcone);

                if (imageConsole is not null)
                {
                    ImageConsoleJeuEnCours.Source = imageConsole;
                    ImageConsoleJeuEnCours.Visibility = Visibility.Visible;
                }
            }
        }
        catch
        {
            // L'icône de console reste facultative. En cas d'échec, on conserve au moins l'année.
        }

        LigneMetaJeuEnCours.Visibility =
            ImageConsoleJeuEnCours.Visibility == Visibility.Visible
            || TexteConsoleJeuEnCours.Visibility == Visibility.Visible
            || TexteAnneeJeuEnCours.Visibility == Visibility.Visible
                ? Visibility.Visible
                : Visibility.Collapsed;
    }

    /// <summary>
    /// Affiche immédiatement les métadonnées déjà connues du jeu sans attendre les enrichissements lents.
    /// </summary>
    private void AppliquerMetaConsoleJeuEnCoursInitiale(JeuUtilisateurRetroAchievements jeu)
    {
        ReinitialiserMetaConsoleJeuEnCours();

        string anneeJeu = ExtraireAnneeJeu(jeu.DateSortie);
        DefinirTitreJeuEnCours(jeu.Titre);

        if (!string.IsNullOrWhiteSpace(anneeJeu))
        {
            TexteAnneeJeuEnCours.Text = anneeJeu;
            TexteAnneeJeuEnCours.Visibility = Visibility.Visible;
        }

        if (!string.IsNullOrWhiteSpace(jeu.NomConsole))
        {
            TexteConsoleJeuEnCours.Text = jeu.NomConsole.Trim();
            TexteConsoleJeuEnCours.Visibility = Visibility.Visible;
        }

        if (!string.IsNullOrWhiteSpace(jeu.Genre))
        {
            TexteTypeJeuEnCours.Text = jeu.Genre.Trim();
            TexteTypeJeuEnCours.Visibility = Visibility.Visible;
        }

        if (!string.IsNullOrWhiteSpace(jeu.Developpeur))
        {
            TexteDeveloppeurJeuEnCours.Text = jeu.Developpeur.Trim();
            TexteDeveloppeurJeuEnCours.Visibility = Visibility.Visible;
        }

        LigneMetaJeuEnCours.Visibility =
            TexteConsoleJeuEnCours.Visibility == Visibility.Visible
            || TexteAnneeJeuEnCours.Visibility == Visibility.Visible
            || TexteTypeJeuEnCours.Visibility == Visibility.Visible
            || TexteDeveloppeurJeuEnCours.Visibility == Visibility.Visible
                ? Visibility.Visible
                : Visibility.Collapsed;
    }

    /// <summary>
    /// Lance les enrichissements secondaires des métadonnées sans bloquer le rendu initial.
    /// </summary>
    private void DemarrerEnrichissementMetaConsoleJeuEnCours(JeuUtilisateurRetroAchievements jeu)
    {
        _ = EnrichirMetaConsoleJeuEnCoursAsync(jeu);
    }

    /// <summary>
    /// Traduit le genre et charge l'icône de console après l'affichage initial.
    /// </summary>
    private async Task EnrichirMetaConsoleJeuEnCoursAsync(JeuUtilisateurRetroAchievements jeu)
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
                IReadOnlyList<ConsoleRetroAchievements> consoles =
                    await ClientRetroAchievements.ObtenirConsolesAsync(
                        _configurationConnexion.CleApiWeb
                    );
                ConsoleRetroAchievements? console = consoles.FirstOrDefault(item =>
                    item.IdentifiantConsole == jeu.IdentifiantConsole
                );

                if (console is not null && !string.IsNullOrWhiteSpace(console.UrlIcone))
                {
                    imageConsole = await ChargerImageDistanteAsync(console.UrlIcone);
                }
            }
            catch
            {
                // L'icône de console reste facultative.
            }

            if (_dernierIdentifiantJeuAvecInfos != jeu.IdentifiantJeu)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(genreAffiche))
            {
                TexteTypeJeuEnCours.Text = genreAffiche;
                TexteTypeJeuEnCours.Visibility = Visibility.Visible;
            }

            if (imageConsole is not null)
            {
                ImageConsoleJeuEnCours.Source = imageConsole;
                ImageConsoleJeuEnCours.Visibility = Visibility.Visible;
            }

            LigneMetaJeuEnCours.Visibility =
                ImageConsoleJeuEnCours.Visibility == Visibility.Visible
                || TexteConsoleJeuEnCours.Visibility == Visibility.Visible
                || TexteAnneeJeuEnCours.Visibility == Visibility.Visible
                || TexteTypeJeuEnCours.Visibility == Visibility.Visible
                || TexteDeveloppeurJeuEnCours.Visibility == Visibility.Visible
                    ? Visibility.Visible
                    : Visibility.Collapsed;
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
        TexteDetailsJeuEnCours.Text = details;
        TexteDetailsJeuEnCours.Visibility = string.IsNullOrWhiteSpace(details)
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    /// <summary>
    /// Met à jour le temps de jeu affiché sous l'image du jeu.
    /// </summary>
    private void DefinirTempsJeuSousImage(string tempsJeu)
    {
        TexteTempsJeuSousImage.Text = tempsJeu;
        TexteTempsJeuSousImage.Visibility = string.IsNullOrWhiteSpace(tempsJeu)
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    /// <summary>
    /// Met à jour l'état du jeu dans l'en-tête de la carte de progression.
    /// </summary>
    private void DefinirEtatJeuDansProgression(string etat)
    {
        TexteEtatJeuDansProgression.Text = string.IsNullOrWhiteSpace(etat) ? "Progression" : etat;
    }

    /// <summary>
    /// Met à jour le titre de la zone principale selon la détection locale d'un émulateur.
    /// </summary>
    private void DefinirTitreZoneJeu(bool emulateurLocalDetecte)
    {
        bool basculeVersDernierJeuJoue =
            string.Equals(TitreZoneJeuEnCours.Text, "Jeu en cours", StringComparison.Ordinal)
            && !emulateurLocalDetecte;

        TitreZoneJeuEnCours.Text = emulateurLocalDetecte ? "Jeu en cours" : "Dernier jeu joué";

        if (basculeVersDernierJeuJoue)
        {
            _ = PersisterDernierJeuAfficheSiNecessaireAsync();
        }
    }

    /// <summary>
    /// Recalcule la découpe arrondie de l'image du jeu quand sa taille change.
    /// </summary>
    private void ImageJeuEnCours_TailleChangee(object sender, SizeChangedEventArgs e)
    {
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
    private async void VisuelJeuPrecedent_Click(object sender, RoutedEventArgs e)
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
    private async void VisuelJeuSuivant_Click(object sender, RoutedEventArgs e)
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
        _signatureAnimationTitreJeu = string.Empty;
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
        TexteEtatSuccesRecents.Text = "Les succès récents apparaîtront ici.";
        TexteSuccesRecent1.Text = "Aucun succès chargé.";
        TexteSuccesRecent2.Text = "Aucun succès chargé.";
        TexteSuccesRecent3.Text = "Aucun succès chargé.";
    }

    /// <summary>
    /// Remplit les lignes de la section des succès récents.
    /// </summary>
    private void AppliquerSuccesRecents(
        List<SuccesRecentRetroAchievements> succesRecents,
        string texteEtat
    )
    {
        TexteEtatSuccesRecents.Text = texteEtat;

        string[] lignes =
        [
            "Aucun autre succès récent.",
            "Aucun autre succès récent.",
            "Aucun autre succès récent.",
        ];

        for (int index = 0; index < Math.Min(3, succesRecents.Count); index++)
        {
            lignes[index] = ConstruireLigneSucces(succesRecents[index]);
        }

        TexteSuccesRecent1.Text = lignes[0];
        TexteSuccesRecent2.Text = lignes[1];
        TexteSuccesRecent3.Text = lignes[2];
    }

    /// <summary>
    /// Construit une ligne d'affichage lisible pour un succès récent.
    /// </summary>
    private static string ConstruireLigneSucces(SuccesRecentRetroAchievements succes)
    {
        string mode = succes.ModeHardcore ? "Hardcore" : "Standard";
        DateTimeOffset dateDeblocage = ConvertirDateSucces(succes.DateDeblocage);
        string dateFormatee =
            dateDeblocage == DateTimeOffset.MinValue
                ? "Date inconnue"
                : dateDeblocage.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);
        string titreJeu = string.IsNullOrWhiteSpace(succes.TitreJeu)
            ? "Jeu inconnu"
            : succes.TitreJeu;
        string description = string.IsNullOrWhiteSpace(succes.Description)
            ? string.Empty
            : $"\n{succes.Description}";

        return $"{succes.Titre} - {succes.Points} pts - {mode}\n{titreJeu} - {dateFormatee}{description}";
    }

    /// <summary>
    /// Convertit la date d'un succès récent en horodatage exploitable pour le tri.
    /// </summary>
    private static DateTimeOffset ConvertirDateSucces(string dateApi)
    {
        if (string.IsNullOrWhiteSpace(dateApi))
        {
            return DateTimeOffset.MinValue;
        }

        if (
            DateTimeOffset.TryParse(
                dateApi,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AllowWhiteSpaces,
                out DateTimeOffset dateParsee
            )
        )
        {
            return dateParsee;
        }

        string[] formatsAcceptes =
        [
            "yyyy-MM-dd HH:mm:ss",
            "yyyy-MM-ddTHH:mm:ssK",
            "yyyy-MM-ddTHH:mm:ss.fffK",
        ];

        if (
            DateTimeOffset.TryParseExact(
                dateApi,
                formatsAcceptes,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AllowWhiteSpaces,
                out dateParsee
            )
        )
        {
            return dateParsee;
        }

        if (
            DateTimeOffset.TryParse(
                dateApi,
                CultureInfo.CurrentCulture,
                DateTimeStyles.AllowWhiteSpaces,
                out dateParsee
            )
        )
        {
            return dateParsee;
        }

        return DateTimeOffset.MinValue;
    }

    /// <summary>
    /// Réinitialise la section "Jeu en cours" sur un état neutre.
    /// </summary>
    private void ReinitialiserJeuEnCours()
    {
        DefinirTitreZoneJeu(true);
        _dernierIdentifiantJeuAvecInfos = 0;
        _dernierIdentifiantJeuAvecProgression = 0;
        ReinitialiserCarrouselVisuelsJeuEnCours();
        ReinitialiserImageJeuEnCours();
        ReinitialiserPremierSuccesNonDebloque();
        ReinitialiserGrilleTousSucces();
        ReinitialiserMetaConsoleJeuEnCours();
        DefinirTempsJeuSousImage(string.Empty);
        DefinirEtatJeuDansProgression(string.Empty);
        DefinirTitreJeuEnCours(string.Empty);
        DefinirDetailsJeuEnCours(string.Empty);
        TexteResumeProgressionJeuEnCours.Text = "-- / --";
        TextePourcentageJeuEnCours.Text = "Aucun jeu pour afficher une progression";
        BarreProgressionJeuEnCours.Value = 0;
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
    private static string ExtraireAnneeJeu(string dateSortie)
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
            return dateParsee.Year.ToString(CultureInfo.InvariantCulture);
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
            return dateSimple.Year.ToString(CultureInfo.InvariantCulture);
        }

        return dateSortie.Length >= 4 ? dateSortie[..4] : string.Empty;
    }

    /// <summary>
    /// Détermine le statut lisible du jeu selon HighestAwardKind, avec repli sur les compteurs pour completed/mastered.
    /// </summary>
    private static string DeterminerStatutJeu(JeuUtilisateurRetroAchievements jeu)
    {
        string etatApi = jeu.PlusHauteRecompense.Trim().ToLowerInvariant();

        string etatDirect = etatApi switch
        {
            "mastered" => "Jeu maîtrisé",
            "completed" => "Jeu complété",
            "beaten" => "Jeu battu",
            "beaten-hardcore" => "Jeu battu en hardcore",
            "beaten-softcore" => "Jeu battu en softcore",
            _ => string.Empty,
        };

        if (!string.IsNullOrWhiteSpace(etatDirect))
        {
            return etatDirect;
        }

        if (jeu.NombreSucces > 0 && jeu.NombreSuccesObtenusHardcore == jeu.NombreSucces)
        {
            return "Jeu maîtrisé";
        }

        if (
            jeu.NombreSucces > 0
            && jeu.NombreSuccesObtenus == jeu.NombreSucces
            && jeu.NombreSuccesObtenusHardcore < jeu.NombreSucces
        )
        {
            return "Jeu complété";
        }

        return "Progression en cours";
    }

    /// <summary>
    /// Formate une durée exprimée en minutes en texte français lisible.
    /// </summary>
    private static string FormaterTempsJeuTotal(int totalMinutes)
    {
        if (totalMinutes <= 0)
        {
            return "0 min";
        }

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

    /// <summary>
    /// Convertit une chaîne de pourcentage de l'API en valeur numérique exploitable.
    /// </summary>
    private static double ExtrairePourcentage(string pourcentageApi)
    {
        if (string.IsNullOrWhiteSpace(pourcentageApi))
        {
            return 0;
        }

        string valeurNormalisee = pourcentageApi.Replace("%", string.Empty).Trim();

        if (
            double.TryParse(
                valeurNormalisee,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out double pourcentage
            )
        )
        {
            return Math.Clamp(pourcentage, 0, 100);
        }

        if (
            double.TryParse(
                valeurNormalisee,
                NumberStyles.Float,
                CultureInfo.CurrentCulture,
                out pourcentage
            )
        )
        {
            return Math.Clamp(pourcentage, 0, 100);
        }

        return 0;
    }

    /// <summary>
    /// Normalise l'affichage texte du pourcentage de complétion.
    /// </summary>
    private static string NormaliserPourcentage(string pourcentageApi)
    {
        double valeur = ExtrairePourcentage(pourcentageApi);
        return $"{valeur:0.##} % complété";
    }
}
