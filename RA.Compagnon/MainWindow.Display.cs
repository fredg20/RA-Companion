using System.Globalization;
using System.Windows;
using System.Windows.Media;
using RA.Compagnon.Modeles.Api;
using RA.Compagnon.Modeles.Api.V2.Achievement;
using RA.Compagnon.Modeles.Api.V2.Game;
using RA.Compagnon.Services;
using SystemControls = System.Windows.Controls;

namespace RA.Compagnon;

public partial class MainWindow
{
    /// <summary>
    /// R�initialise l'affichage des m�tadonn�es sous le titre du jeu courant.
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
    /// Met � jour l'ann�e du jeu, sa console, son type, son d�veloppeur et l'ic�ne officielle.
    /// </summary>
    private async Task MettreAJourMetaConsoleJeuEnCoursAsync(GameInfoAndUserProgressV2 jeu)
    {
        ReinitialiserMetaConsoleJeuEnCours();

        string anneeJeu = ExtraireAnneeJeu(jeu.Released);
        DefinirTitreJeuEnCours(jeu.Title);

        if (!string.IsNullOrWhiteSpace(anneeJeu))
        {
            TexteAnneeJeuEnCours.Text = anneeJeu;
            TexteAnneeJeuEnCours.Visibility = Visibility.Visible;
        }

        if (!string.IsNullOrWhiteSpace(jeu.ConsoleName))
        {
            TexteConsoleJeuEnCours.Text = jeu.ConsoleName.Trim();
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

        if (!string.IsNullOrWhiteSpace(jeu.Developer))
        {
            TexteDeveloppeurJeuEnCours.Text = jeu.Developer.Trim();
            TexteDeveloppeurJeuEnCours.Visibility = Visibility.Visible;
        }

        try
        {
            IReadOnlyList<ConsoleV2> consoles = await ClientRetroAchievements.ObtenirConsolesAsync(
                _configurationConnexion.CleApiWeb
            );
            ConsoleV2? console = consoles.FirstOrDefault(item => item.ConsoleId == jeu.ConsoleId);

            if (console is not null && !string.IsNullOrWhiteSpace(console.IconUrl))
            {
                ImageSource? imageConsole = await ChargerImageDistanteAsync(console.IconUrl);

                if (imageConsole is not null)
                {
                    ImageConsoleJeuEnCours.Source = imageConsole;
                    ImageConsoleJeuEnCours.Visibility = Visibility.Visible;
                }
            }
        }
        catch
        {
            // L'ic�ne de console reste facultative. En cas d'�chec, on conserve au moins l'ann�e.
        }

        LigneMetaJeuEnCours.Visibility =
            ImageConsoleJeuEnCours.Visibility == Visibility.Visible
            || TexteConsoleJeuEnCours.Visibility == Visibility.Visible
            || TexteAnneeJeuEnCours.Visibility == Visibility.Visible
                ? Visibility.Visible
                : Visibility.Collapsed;
    }

    /// <summary>
    /// Affiche imm�diatement les m�tadonn�es d�j� connues du jeu sans attendre les enrichissements lents.
    /// </summary>
    private void AppliquerMetaConsoleJeuEnCoursInitiale(GameInfoAndUserProgressV2 jeu)
    {
        ReinitialiserMetaConsoleJeuEnCours();

        string anneeJeu = ExtraireAnneeJeu(jeu.Released);
        DefinirTitreJeuEnCours(jeu.Title);

        if (!string.IsNullOrWhiteSpace(anneeJeu))
        {
            TexteAnneeJeuEnCours.Text = anneeJeu;
            TexteAnneeJeuEnCours.Visibility = Visibility.Visible;
        }

        if (!string.IsNullOrWhiteSpace(jeu.ConsoleName))
        {
            TexteConsoleJeuEnCours.Text = jeu.ConsoleName.Trim();
            TexteConsoleJeuEnCours.Visibility = Visibility.Visible;
        }

        if (!string.IsNullOrWhiteSpace(jeu.Genre))
        {
            TexteTypeJeuEnCours.Text = jeu.Genre.Trim();
            TexteTypeJeuEnCours.Visibility = Visibility.Visible;
        }

        if (!string.IsNullOrWhiteSpace(jeu.Developer))
        {
            TexteDeveloppeurJeuEnCours.Text = jeu.Developer.Trim();
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
    /// Lance les enrichissements secondaires des m�tadonn�es sans bloquer le rendu initial.
    /// </summary>
    private void DemarrerEnrichissementMetaConsoleJeuEnCours(GameInfoAndUserProgressV2 jeu)
    {
        _ = EnrichirMetaConsoleJeuEnCoursAsync(jeu);
    }

    /// <summary>
    /// Traduit le genre et charge l'ic�ne de console apr�s l'affichage initial.
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
                    await ClientRetroAchievements.ObtenirConsolesAsync(
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
                // L'ic�ne de console reste facultative.
            }

            if (_dernierIdentifiantJeuAvecInfos != jeu.Id)
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
    /// Met � jour la ligne de d�tails sous le type et le d�veloppeur du jeu.
    /// </summary>
    private void DefinirDetailsJeuEnCours(string details)
    {
        TexteDetailsJeuEnCours.Text = details;
        TexteDetailsJeuEnCours.Visibility = string.IsNullOrWhiteSpace(details)
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    /// <summary>
    /// Met � jour le temps de jeu affich� sous l'image du jeu.
    /// </summary>
    private void DefinirTempsJeuSousImage(string tempsJeu)
    {
        TexteTempsJeuSousImage.Text = tempsJeu;
        TexteTempsJeuSousImage.Visibility = string.IsNullOrWhiteSpace(tempsJeu)
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    /// <summary>
    /// Met � jour l'�tat du jeu dans l'en-t�te de la carte de progression.
    /// </summary>
    private void DefinirEtatJeuDansProgression(string etat)
    {
        TexteEtatJeuDansProgression.Text = string.IsNullOrWhiteSpace(etat)
            ? "Progression du jeu"
            : etat;
    }

    private void DefinirTitreZoneJeu()
    {
        TitreZoneJeuEnCours.Text = "Dernier jeu jou�";
    }

    /// <summary>
    /// Recalcule la d�coupe arrondie de l'image du jeu quand sa taille change.
    /// </summary>
    private void ImageJeuEnCours_TailleChangee(object sender, SizeChangedEventArgs e)
    {
        AppliquerCoinsArrondisImageJeuEnCours();
    }

    /// <summary>
    /// Recalcule la d�coupe arrondie du badge du premier succ�s quand sa taille change.
    /// </summary>
    private void ImagePremierSuccesNonDebloque_TailleChangee(object sender, SizeChangedEventArgs e)
    {
        AppliquerCoinsArrondisImagePremierSuccesNonDebloque();
    }

    /// <summary>
    /// Affiche le visuel pr�c�dent du jeu courant.
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
    /// Recalcule le d�filement du titre quand sa taille ou celle de son conteneur change.
    /// </summary>
    private void TitreJeuEnCours_MiseEnPageChangee(object sender, SizeChangedEventArgs e)
    {
        _signatureAnimationTitreJeu = string.Empty;
        PlanifierMiseAJourAnimationTitreJeuEnCours();
    }

    /// <summary>
    /// Applique les coins arrondis � l'image du jeu courant selon sa taille r�elle.
    /// </summary>
    private void AppliquerCoinsArrondisImageJeuEnCours()
    {
        AppliquerCoinsArrondisImage(ImageJeuEnCours);
        AppliquerCoinsArrondisImage(ImageJeuEnCoursTransition);
    }

    /// <summary>
    /// Applique les coins arrondis au badge du premier succ�s selon sa taille r�elle.
    /// </summary>
    private void AppliquerCoinsArrondisImagePremierSuccesNonDebloque()
    {
        AppliquerCoinsArrondisImage(ImagePremierSuccesNonDebloque);
    }

    /// <summary>
    /// Applique une d�coupe arrondie � une image selon sa taille r�elle.
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
    /// D�clenche un cycle p�riodique d'actualisation API.
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
    /// R�initialise la section des succ�s r�cents sur un �tat neutre.
    /// </summary>
    private void ReinitialiserSuccesRecents()
    {
        TexteEtatSuccesRecents.Text = "Les derniers succ�s de ce compte appara�tront ici.";
        TexteSuccesRecent1.Text = "Aucun succ�s r�cent � afficher.";
        TexteSuccesRecent2.Text = "Aucun succ�s r�cent � afficher.";
        TexteSuccesRecent3.Text = "Aucun succ�s r�cent � afficher.";
    }

    /// <summary>
    /// Remplit les lignes de la section des succ�s r�cents.
    /// </summary>
    private void AppliquerSuccesRecents(List<AchievementUnlockV2> succesRecents, string texteEtat)
    {
        TexteEtatSuccesRecents.Text = texteEtat;

        string[] lignes =
        [
            "Aucun autre succ�s r�cent.",
            "Aucun autre succ�s r�cent.",
            "Aucun autre succ�s r�cent.",
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
    /// Construit une ligne d'affichage lisible pour un succ�s r�cent.
    /// </summary>
    private static string ConstruireLigneSucces(AchievementUnlockV2 succes)
    {
        string mode = succes.HardcoreMode ? "Hardcore" : "Standard";
        DateTimeOffset dateDeblocage = ConvertirDateSucces(succes.Date);
        string dateFormatee =
            dateDeblocage == DateTimeOffset.MinValue
                ? "Date inconnue"
                : dateDeblocage.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);
        string titreJeu = string.IsNullOrWhiteSpace(succes.TitleJeu)
            ? "Jeu inconnu"
            : succes.TitleJeu;
        string description = string.IsNullOrWhiteSpace(succes.Description)
            ? string.Empty
            : $"\n{succes.Description}";

        return $"{succes.Title} - {succes.Points} pts - {mode}\n{titreJeu} - {dateFormatee}{description}";
    }

    /// <summary>
    /// Convertit la date d'un succ�s r�cent en horodatage exploitable pour le tri.
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
    /// R�initialise la section "Jeu en cours" sur un �tat neutre.
    /// </summary>
    private void ReinitialiserJeuEnCours()
    {
        DefinirTitreZoneJeu();
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
        TextePourcentageJeuEnCours.Text = "Connecte ton compte pour afficher ton activit�.";
        BarreProgressionJeuEnCours.Value = 0;
        ReinitialiserSuccesRecents();
    }

    /// <summary>
    /// Indique si la progression affich�e peut �tre conserv�e pour le m�me jeu.
    /// </summary>
    private bool PeutConserverProgressionAffichee(int identifiantJeu)
    {
        return identifiantJeu > 0 && _dernierIdentifiantJeuAvecProgression == identifiantJeu;
    }

    /// <summary>
    /// Indique si les informations visibles du jeu peuvent �tre conserv�es pour le m�me jeu.
    /// </summary>
    private bool PeutConserverInfosJeuAffichees(int identifiantJeu)
    {
        return identifiantJeu > 0 && _dernierIdentifiantJeuAvecInfos == identifiantJeu;
    }

    /// <summary>
    /// Extrait l'ann�e de sortie d'un jeu � partir du champ API de date.
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
    /// D�termine le statut lisible du jeu selon HighestAwardKind, avec repli sur les compteurs pour completed/mastered.
    /// </summary>
    private static string DeterminerStatutJeu(GameInfoAndUserProgressV2 jeu)
    {
        string etatApi = jeu.HighestAwardKind.Trim().ToLowerInvariant();

        string etatDirect = etatApi switch
        {
            "mastered" => "Jeu ma�tris�",
            "completed" => "Jeu compl�t�",
            "beaten" => "Jeu battu",
            "beaten-hardcore" => "Jeu battu en hardcore",
            "beaten-softcore" => "Jeu battu en softcore",
            _ => string.Empty,
        };

        if (!string.IsNullOrWhiteSpace(etatDirect))
        {
            return etatDirect;
        }

        if (jeu.NumAchievements > 0 && jeu.NumAwardedToUserHardcore == jeu.NumAchievements)
        {
            return "Jeu ma�tris�";
        }

        if (
            jeu.NumAchievements > 0
            && jeu.NumAwardedToUser == jeu.NumAchievements
            && jeu.NumAwardedToUserHardcore < jeu.NumAchievements
        )
        {
            return "Jeu compl�t�";
        }

        return "Progression en cours";
    }

    /// <summary>
    /// Formate une dur�e exprim�e en minutes en texte fran�ais lisible.
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
    /// Convertit une cha�ne de pourcentage de l'API en valeur num�rique exploitable.
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
    /// Normalise l'affichage texte du pourcentage de compl�tion.
    /// </summary>
    private static string NormaliserPourcentage(string pourcentageApi)
    {
        double valeur = ExtrairePourcentage(pourcentageApi);
        return $"{valeur:0.##} % compl�t�";
    }
}
