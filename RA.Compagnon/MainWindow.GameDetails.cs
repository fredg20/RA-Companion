/*
 * Regroupe la logique détaillée de la carte du jeu courant, en particulier
 * les actions Détails, Recharger et Rejouer ainsi que la modale de vue
 * détaillée accessible depuis l'interface principale.
 */
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using RA.Compagnon.Modeles.Api.V2.Game;
using RA.Compagnon.Modeles.Local;
using RA.Compagnon.Modeles.Presentation;
using RA.Compagnon.Services;
using SystemControls = System.Windows.Controls;
using UiControls = Wpf.Ui.Controls;

namespace RA.Compagnon;

/*
 * Porte la partie de la fenêtre principale qui gère les actions détaillées
 * associées au jeu courant et à sa relance locale.
 */
public partial class MainWindow
{
    private static readonly string CheminJournalRejouer = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "RA-Compagnon",
        "journal-rejouer.log"
    );

    /*
     * Réinitialise les actions détaillées du jeu courant pour repartir d'un
     * état neutre quand aucun jeu exploitable n'est disponible.
     */
    private void ReinitialiserVueDetailleeJeuEnCours()
    {
        _vueModele.JeuCourant.LibelleActionDetails = "Détails";
        _vueModele.JeuCourant.ToolTipActionDetails = string.Empty;
        _vueModele.JeuCourant.ActionDetailsActivee = false;
        _vueModele.JeuCourant.ActionDetailsVisible = false;
        _vueModele.JeuCourant.LibelleActionRejouer = "Rejouer";
        _vueModele.JeuCourant.ToolTipActionRejouer = string.Empty;
        _vueModele.JeuCourant.ActionRejouerActivee = false;
        _vueModele.JeuCourant.ActionRejouerVisible = false;
        MettreAJourActionRechargerJeuEnCours();
    }

    /*
     * Met à jour l'action ouvrant la vue détaillée du jeu courant.
     */
    private void MettreAJourActionVueDetailleeJeuEnCours(GameInfoAndUserProgressV2 jeu)
    {
        bool actionDisponible = jeu.Id > 0;
        _vueModele.JeuCourant.LibelleActionDetails = "Détails";
        _vueModele.JeuCourant.ActionDetailsActivee = actionDisponible;
        _vueModele.JeuCourant.ActionDetailsVisible = actionDisponible;
        _vueModele.JeuCourant.ToolTipActionDetails = actionDisponible
            ? "Afficher les détails de ce jeu"
            : string.Empty;
        MettreAJourActionRechargerJeuEnCours(jeu.Id);
    }

    /*
     * Active ou désactive l'action Recharger selon le contexte courant
     * du jeu et de la connexion.
     */
    private void MettreAJourActionRechargerJeuEnCours(int identifiantJeu = 0)
    {
        if (identifiantJeu <= 0)
        {
            identifiantJeu = _dernieresDonneesJeuAffichees?.Jeu.Id ?? _dernierIdentifiantJeuApi;
        }

        bool actionDisponible =
            identifiantJeu > 0
            && ConfigurationConnexionEstComplete()
            && !_chargementJeuEnCoursActif;
        _vueModele.LibelleRechargerJeu = "Recharger";
        _vueModele.RechargerJeuActif = actionDisponible;
        _vueModele.ToolTipRechargerJeu = actionDisponible
            ? "Recharger ce jeu depuis l'API"
            : string.Empty;
    }

    /*
     * Met à jour l'état du bouton Rejouer à partir du dernier contexte
     * local connu pour ce jeu.
     */
    private void MettreAJourActionRejouerJeuEnCours(EtatJeuAfficheLocal? jeuSauvegarde)
    {
        if (jeuSauvegarde is not null)
        {
            HydraterActionRejouerDepuisSourcesLocalesActifRecemment(jeuSauvegarde);
        }

        jeuSauvegarde = ObtenirEtatRejouableCourant(jeuSauvegarde);

        if (
            jeuSauvegarde is null
            || jeuSauvegarde.Id <= 0
            || string.IsNullOrWhiteSpace(jeuSauvegarde.CheminJeuLocal)
            || !File.Exists(jeuSauvegarde.CheminJeuLocal)
        )
        {
            _vueModele.JeuCourant.ToolTipActionRejouer = string.Empty;
            _vueModele.JeuCourant.ActionRejouerActivee = false;
            _vueModele.JeuCourant.ActionRejouerVisible = false;
            return;
        }

        string cheminExecutable = DeterminerCheminExecutableRelance(jeuSauvegarde);
        _vueModele.JeuCourant.LibelleActionRejouer = "Rejouer";
        bool actionDisponible = !string.IsNullOrWhiteSpace(cheminExecutable);

        if (DoitMasquerActionRejouerPendantJeu())
        {
            _vueModele.JeuCourant.ActionRejouerActivee = false;
            _vueModele.JeuCourant.ActionRejouerVisible = actionDisponible;
            _vueModele.JeuCourant.ToolTipActionRejouer = actionDisponible
                ? "Disponible quand Dernier jeu reapparait"
                : string.Empty;
            return;
        }

        _vueModele.JeuCourant.ActionRejouerActivee = actionDisponible;
        _vueModele.JeuCourant.ActionRejouerVisible = actionDisponible;
        _vueModele.JeuCourant.ToolTipActionRejouer = actionDisponible
            ? "Relancer ce jeu"
            : string.Empty;
    }

    /*
     * Retourne l'état de jeu le plus pertinent pour une relance locale.
     */
    private EtatJeuAfficheLocal? ObtenirEtatRejouableCourant(EtatJeuAfficheLocal? jeuSauvegarde)
    {
        if (
            jeuSauvegarde is not null
            && jeuSauvegarde.Id > 0
            && !string.IsNullOrWhiteSpace(jeuSauvegarde.CheminJeuLocal)
        )
        {
            return jeuSauvegarde;
        }

        int identifiantJeuAffiche =
            _dernieresDonneesJeuAffichees?.Jeu.Id ?? _dernierIdentifiantJeuApi;

        if (
            identifiantJeuAffiche <= 0
            || _identifiantJeuRejouableCourant != identifiantJeuAffiche
            || string.IsNullOrWhiteSpace(_nomEmulateurRejouableCourant)
            || string.IsNullOrWhiteSpace(_cheminEmulateurRejouableCourant)
            || string.IsNullOrWhiteSpace(_cheminJeuRejouableCourant)
        )
        {
            return jeuSauvegarde;
        }

        EtatJeuAfficheLocal etat =
            jeuSauvegarde ?? new EtatJeuAfficheLocal { IdentifiantJeu = identifiantJeuAffiche };
        etat.Id = identifiantJeuAffiche;
        etat.NomEmulateurRelance = _nomEmulateurRejouableCourant;
        etat.CheminExecutableEmulateur = _cheminEmulateurRejouableCourant;
        etat.CheminJeuLocal = _cheminJeuRejouableCourant;
        return etat;
    }

    /*
     * Indique si l'action Rejouer doit rester visible mais inactive
     * pendant qu'un jeu est encore considéré comme actif.
     */
    private bool DoitMasquerActionRejouerPendantJeu()
    {
        if (_emulateurValideDetecteEnDirect)
        {
            return true;
        }

        EtatRichPresence etatRichPresence = ServiceSondeRichPresence.Sonder(
            new DonneesCompteUtilisateur
            {
                Profil = _dernierProfilUtilisateurCharge,
                Resume = _dernierResumeUtilisateurCharge,
            },
            journaliser: false
        );

        return string.Equals(
            etatRichPresence.StatutAffiche,
            "En jeu",
            StringComparison.OrdinalIgnoreCase
        );
    }

    /*
     * Détermine l'exécutable à relancer pour le jeu courant en fonction
     * de l'état local mémorisé.
     */
    private static string DeterminerCheminExecutableRelance(EtatJeuAfficheLocal jeuSauvegarde)
    {
        if (
            !string.IsNullOrWhiteSpace(jeuSauvegarde.CheminExecutableEmulateur)
            && File.Exists(jeuSauvegarde.CheminExecutableEmulateur)
            && ServiceSourcesLocalesEmulateurs.CheminExecutableCorrespondEmulateur(
                jeuSauvegarde.NomEmulateurRelance,
                jeuSauvegarde.CheminExecutableEmulateur
            )
        )
        {
            return jeuSauvegarde.CheminExecutableEmulateur;
        }

        if (string.IsNullOrWhiteSpace(jeuSauvegarde.NomEmulateurRelance))
        {
            return string.Empty;
        }

        string cheminDetecte = ServiceSourcesLocalesEmulateurs.TrouverEmplacementEmulateur(
            jeuSauvegarde.NomEmulateurRelance
        );

        return !string.IsNullOrWhiteSpace(cheminDetecte) && File.Exists(cheminDetecte)
            ? cheminDetecte
            : string.Empty;
    }

    /*
     * Construit les arguments de ligne de commande nécessaires pour relancer
     * proprement un jeu selon l'émulateur retenu.
     */
    private static string ConstruireArgumentsRelance(
        EtatJeuAfficheLocal jeuSauvegarde,
        string cheminExecutable
    )
    {
        if (
            string.Equals(
                jeuSauvegarde.NomEmulateurRelance,
                "RetroArch",
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            string cheminCore = TrouverCheminCoreRetroArch(
                cheminExecutable,
                jeuSauvegarde.CheminJeuLocal
            );

            if (!string.IsNullOrWhiteSpace(cheminCore))
            {
                return $"-L \"{cheminCore}\" \"{jeuSauvegarde.CheminJeuLocal}\"";
            }
        }

        if (
            string.Equals(
                jeuSauvegarde.NomEmulateurRelance,
                "RALibretro",
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            (string nomCore, int systeme) = TrouverContexteRelanceRALibretro(
                cheminExecutable,
                jeuSauvegarde.CheminJeuLocal
            );

            if (!string.IsNullOrWhiteSpace(nomCore) && systeme > 0)
            {
                return $"--core \"{nomCore}\" --system {systeme.ToString(CultureInfo.InvariantCulture)} --game \"{jeuSauvegarde.CheminJeuLocal}\"";
            }
        }

        return $"\"{jeuSauvegarde.CheminJeuLocal}\"";
    }

    /*
     * Tente de retrouver le core RetroArch à utiliser pour relancer le jeu
     * à partir du contexte local disponible.
     */
    private static string TrouverCheminCoreRetroArch(string cheminExecutable, string cheminJeu)
    {
        try
        {
            string? repertoireRetroArch = Path.GetDirectoryName(cheminExecutable);

            if (string.IsNullOrWhiteSpace(repertoireRetroArch))
            {
                return string.Empty;
            }

            string cheminHistorique = Path.Combine(
                repertoireRetroArch,
                "playlists",
                "builtin",
                "content_history.lpl"
            );

            if (!File.Exists(cheminHistorique))
            {
                return string.Empty;
            }

            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(cheminHistorique));

            if (
                !document.RootElement.TryGetProperty("items", out JsonElement items)
                || items.ValueKind != JsonValueKind.Array
            )
            {
                return string.Empty;
            }

            foreach (JsonElement item in items.EnumerateArray())
            {
                if (
                    !item.TryGetProperty("path", out JsonElement pathElement)
                    || pathElement.ValueKind != JsonValueKind.String
                )
                {
                    continue;
                }

                string cheminJeuHistorique = pathElement.GetString()?.Trim() ?? string.Empty;

                if (
                    !string.Equals(
                        cheminJeuHistorique,
                        cheminJeu,
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                {
                    continue;
                }

                if (
                    !item.TryGetProperty("core_path", out JsonElement corePathElement)
                    || corePathElement.ValueKind != JsonValueKind.String
                )
                {
                    return string.Empty;
                }

                string cheminCore = corePathElement.GetString()?.Trim() ?? string.Empty;
                return !string.IsNullOrWhiteSpace(cheminCore) && File.Exists(cheminCore)
                    ? cheminCore
                    : string.Empty;
            }
        }
        catch
        {
            return string.Empty;
        }

        return string.Empty;
    }

    /*
     * Déduit le core et le système à employer pour une relance via RALibretro.
     */
    private static (string NomCore, int Systeme) TrouverContexteRelanceRALibretro(
        string cheminExecutable,
        string cheminJeu
    )
    {
        try
        {
            string? repertoireRALibretro = Path.GetDirectoryName(cheminExecutable);

            if (string.IsNullOrWhiteSpace(repertoireRALibretro))
            {
                return (string.Empty, 0);
            }

            string cheminConfiguration = Path.Combine(repertoireRALibretro, "RALibretro.json");

            if (!File.Exists(cheminConfiguration))
            {
                return (string.Empty, 0);
            }

            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(cheminConfiguration));

            if (
                !document.RootElement.TryGetProperty("recent", out JsonElement recent)
                || recent.ValueKind != JsonValueKind.Array
            )
            {
                return (string.Empty, 0);
            }

            foreach (JsonElement item in recent.EnumerateArray())
            {
                if (
                    !item.TryGetProperty("path", out JsonElement pathElement)
                    || pathElement.ValueKind != JsonValueKind.String
                )
                {
                    continue;
                }

                string cheminJeuHistorique = pathElement.GetString()?.Trim() ?? string.Empty;

                if (
                    !string.Equals(
                        cheminJeuHistorique,
                        cheminJeu,
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                {
                    continue;
                }

                if (
                    !item.TryGetProperty("core", out JsonElement coreElement)
                    || coreElement.ValueKind != JsonValueKind.String
                )
                {
                    return (string.Empty, 0);
                }

                string nomCore = coreElement.GetString()?.Trim() ?? string.Empty;
                int systeme =
                    item.TryGetProperty("system", out JsonElement systemElement)
                    && systemElement.TryGetInt32(out int systemeExtrait)
                        ? systemeExtrait
                        : 0;

                if (string.IsNullOrWhiteSpace(nomCore) || systeme <= 0)
                {
                    return (string.Empty, 0);
                }

                return (nomCore, systeme);
            }
        }
        catch
        {
            return (string.Empty, 0);
        }

        return (string.Empty, 0);
    }

    /*
     * Lance la relance locale du jeu courant si toutes les informations
     * nécessaires sont disponibles.
     */
    private void ExecuterActionRejouerJeuEnCours()
    {
        EtatJeuAfficheLocal? jeuSauvegarde = ObtenirEtatRejouableCourant(
            _configurationConnexion.DernierJeuAffiche
        );

        if (jeuSauvegarde is null || string.IsNullOrWhiteSpace(jeuSauvegarde.CheminJeuLocal))
        {
            JournaliserRejouer("ignore", "raison=jeu_absent_ou_chemin_absent");
            MessageBox.Show(
                "Aucun dernier jeu local relancable n'est disponible pour le moment.",
                "Rejouer",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
            return;
        }

        string cheminExecutable = DeterminerCheminExecutableRelance(jeuSauvegarde);

        if (string.IsNullOrWhiteSpace(cheminExecutable) || !File.Exists(cheminExecutable))
        {
            JournaliserRejouer(
                "ignore",
                $"raison=emulateur_introuvable;emulateur={jeuSauvegarde.NomEmulateurRelance};cheminExecutable={cheminExecutable}"
            );
            MessageBox.Show(
                "L'emulateur n'a pas ete retrouve. Ouvre-le une fois ou corrige son emplacement dans l'aide.",
                "Rejouer",
                MessageBoxButton.OK,
                MessageBoxImage.Warning
            );
            return;
        }

        if (!File.Exists(jeuSauvegarde.CheminJeuLocal))
        {
            JournaliserRejouer(
                "ignore",
                $"raison=jeu_introuvable;cheminJeu={jeuSauvegarde.CheminJeuLocal}"
            );
            MessageBox.Show(
                "Le fichier du jeu n'a pas ete retrouve a son dernier emplacement connu.",
                "Rejouer",
                MessageBoxButton.OK,
                MessageBoxImage.Warning
            );
            return;
        }

        try
        {
            string arguments = ConstruireArgumentsRelance(jeuSauvegarde, cheminExecutable);
            SuccesDebloqueDetecte? succesDirectExistant =
                ServiceSondeLocaleEmulateurs.LireDernierSuccesDebloqueDepuisSourceLocale(
                    jeuSauvegarde.NomEmulateurRelance,
                    jeuSauvegarde.Id,
                    jeuSauvegarde.Title,
                    _succesJeuCourant
                );

            _signatureSuccesLocalDirectIgnoreeAuRejeu = succesDirectExistant is null
                ? string.Empty
                : ConstruireSignatureSuccesLocalDirect(succesDirectExistant);
            JournaliserRejouer(
                "tentative",
                $"emulateur={jeuSauvegarde.NomEmulateurRelance};executable={cheminExecutable};arguments={arguments};shell=false;baselineSucces={_signatureSuccesLocalDirectIgnoreeAuRejeu}"
            );

            Process? processus = Process.Start(
                new ProcessStartInfo
                {
                    FileName = cheminExecutable,
                    Arguments = arguments,
                    WorkingDirectory = Path.GetDirectoryName(cheminExecutable) ?? string.Empty,
                    UseShellExecute = false,
                }
            );

            if (processus is null)
            {
                JournaliserRejouer("echec", "raison=processus_null");
                MessageBox.Show(
                    "Le lancement a ete refuse ou n'a pas pu etre demarre.",
                    "Rejouer",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
                return;
            }

            JournaliserRejouer(
                "demarre",
                $"pid={processus.Id.ToString(CultureInfo.InvariantCulture)};nom={processus.ProcessName}"
            );
            _rejeuDemarreEnAttenteChargement = true;
            ChargerJeuResolutLocal(jeuSauvegarde.Id, jeuSauvegarde.Title);
        }
        catch (Exception exception)
        {
            JournaliserRejouer(
                "exception",
                $"type={exception.GetType().Name};message={exception.Message}"
            );
            MessageBox.Show(
                "Impossible de relancer ce jeu pour le moment.",
                "Rejouer",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
        }
    }

    /*
     * Gère le clic sur le bouton Rejouer de la carte du jeu courant.
     */
    private void BoutonRejouerJeuEnCours_Click(object sender, RoutedEventArgs e)
    {
        ExecuterActionRejouerJeuEnCours();
    }

    /*
     * Écrit une entrée de diagnostic dédiée aux scénarios de relance locale.
     */
    private static void JournaliserRejouer(string evenement, string details)
    {
        _ = ServiceModeDiagnostic.JournaliserLigne(
            CheminJournalRejouer,
            $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] evenement={evenement};details={details}{Environment.NewLine}"
        );
    }

    /*
     * Ouvre la vue détaillée du jeu courant lorsque les données disponibles
     * le permettent.
     */
    private async Task ExecuterActionVueDetailleeJeuEnCoursAsync()
    {
        if (_dernieresDonneesJeuAffichees?.Jeu is not GameInfoAndUserProgressV2 jeu || jeu.Id <= 0)
        {
            return;
        }

        JeuAffiche jeuAffiche = ServicePresentationJeu.Construire(_dernieresDonneesJeuAffichees);
        await AfficherModaleVueDetailleeJeuAsync(jeu, jeuAffiche);
    }

    /*
     * Force un rechargement du jeu courant depuis l'API et l'état local.
     */
    private async Task ExecuterActionRechargerJeuEnCoursAsync()
    {
        if (!ConfigurationConnexionEstComplete() || _chargementJeuEnCoursActif)
        {
            MettreAJourActionRechargerJeuEnCours();
            return;
        }

        int identifiantJeu = _dernieresDonneesJeuAffichees?.Jeu.Id ?? _dernierIdentifiantJeuApi;

        if (identifiantJeu <= 0)
        {
            MettreAJourActionRechargerJeuEnCours();
            return;
        }

        _vueModele.RechargerJeuActif = false;

        try
        {
            _dernieresDonneesJeuAffichees = null;
            _identifiantJeuSuccesCourant = 0;
            _succesJeuCourant = [];
            ReinitialiserEtatSuccesTemporairesSession();
            _identifiantJeuSuccesObserve = 0;
            _etatSuccesObserves = [];
            await ChargerJeuEnCoursAsync(false, true, true);
        }
        finally
        {
            MettreAJourActionRechargerJeuEnCours();
        }
    }

    /*
     * Gère le clic sur le bouton Détails de la carte du jeu courant.
     */
    private async void BoutonVueDetailleeJeuEnCours_Click(object sender, RoutedEventArgs e)
    {
        await ExecuterActionVueDetailleeJeuEnCoursAsync();
    }

    /*
     * Construit puis affiche la modale de vue détaillée du jeu courant.
     */
    private async Task AfficherModaleVueDetailleeJeuAsync(
        GameInfoAndUserProgressV2 jeu,
        JeuAffiche jeuAffiche
    )
    {
        SystemControls.StackPanel contenu = new()
        {
            Width = 460,
            Margin = new Thickness(0),
            Children =
            {
                new SystemControls.TextBlock
                {
                    FontSize = 20,
                    FontWeight = FontWeights.SemiBold,
                    Text = jeu.Title,
                    TextWrapping = TextWrapping.Wrap,
                },
                new SystemControls.TextBlock
                {
                    Margin = new Thickness(0, 6, 0, 0),
                    Opacity = 0.76,
                    Text = jeuAffiche.Statut,
                    TextWrapping = TextWrapping.Wrap,
                },
                ConstruireGrilleVueDetailleeJeu(jeu, jeuAffiche),
            },
        };

        string dernierSucces = ConstruireTexteDernierSuccesJeu(jeu);

        if (!string.IsNullOrWhiteSpace(dernierSucces))
        {
            contenu.Children.Add(
                new SystemControls.TextBlock
                {
                    Margin = new Thickness(0, 14, 0, 0),
                    Opacity = 0.72,
                    Text = "Dernier succès obtenu",
                }
            );
            contenu.Children.Add(
                new SystemControls.TextBox
                {
                    Margin = new Thickness(0, 4, 0, 0),
                    Style = (Style)FindResource("StyleTexteCopiable"),
                    Text = dernierSucces,
                    TextWrapping = TextWrapping.Wrap,
                }
            );
        }

        UiControls.Button boutonPageJeu = new()
        {
            Content = "Ouvrir la page RetroAchievements",
            Appearance = UiControls.ControlAppearance.Secondary,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 14, 0, 0),
            Padding = new Thickness(14, 6, 14, 6),
        };
        boutonPageJeu.Click += (_, _) => OuvrirPageJeuRetroAchievements(jeu.Id);
        contenu.Children.Add(boutonPageJeu);

        SystemControls.Border conteneurContenu = new()
        {
            Padding = new Thickness(MargeInterieureModaleConnexion),
            HorizontalAlignment = HorizontalAlignment.Center,
            CornerRadius = ObtenirRayonCoins("RayonCoinsStandard", 12),
            Child = contenu,
        };

        UiControls.ContentDialog dialogue = new(RacineModales)
        {
            Title = "Détails du jeu",
            Content = conteneurContenu,
            MinWidth = 460 + (MargeInterieureModaleConnexion * 2),
            CloseButtonText = "Fermer",
            DefaultButton = UiControls.ContentDialogButton.Close,
        };

        try
        {
            DefinirEtatModalesActif(true);
            await dialogue.ShowAsync();
        }
        finally
        {
            DefinirEtatModalesActif(false);
        }
    }

    /*
     * Construit la grille de contenu affichée dans la modale de détails
     * du jeu courant.
     */
    private SystemControls.Grid ConstruireGrilleVueDetailleeJeu(
        GameInfoAndUserProgressV2 jeu,
        JeuAffiche jeuAffiche
    )
    {
        (string Libelle, string Valeur)[] lignes =
        [
            ("Game ID", jeu.Id.ToString(CultureInfo.CurrentCulture)),
            ("Progression", jeuAffiche.PourcentageTexte),
            (
                "Succès",
                $"{jeu.NumAwardedToUser.ToString(CultureInfo.CurrentCulture)} / {jeu.NumAchievements.ToString(CultureInfo.CurrentCulture)}"
            ),
            (
                "Hardcore",
                $"{jeu.NumAwardedToUserHardcore.ToString(CultureInfo.CurrentCulture)} / {jeu.NumAchievements.ToString(CultureInfo.CurrentCulture)}"
            ),
            ("Points", ConstruireTextePointsDetailJeu(jeu)),
            (
                "Joueurs",
                jeu.NumDistinctPlayers > 0
                    ? jeu.NumDistinctPlayers.ToString(CultureInfo.CurrentCulture)
                    : "Inconnu"
            ),
            ("Récompense", ConstruireTexteRecompenseJeu(jeu)),
            ("Résumé", jeuAffiche.ResumeProgression),
        ];

        SystemControls.Grid grille = new() { Margin = new Thickness(0, 14, 0, 0) };
        grille.ColumnDefinitions.Add(
            new SystemControls.ColumnDefinition { Width = new GridLength(150) }
        );
        grille.ColumnDefinitions.Add(
            new SystemControls.ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
        );

        for (int index = 0; index < lignes.Length; index++)
        {
            grille.RowDefinitions.Add(
                new SystemControls.RowDefinition { Height = GridLength.Auto }
            );

            SystemControls.TextBlock libelle = new()
            {
                Margin = new Thickness(0, 0, 12, 8),
                FontWeight = FontWeights.SemiBold,
                Opacity = 0.72,
                Text = lignes[index].Libelle,
                TextWrapping = TextWrapping.Wrap,
            };
            SystemControls.Grid.SetRow(libelle, index);
            SystemControls.Grid.SetColumn(libelle, 0);
            grille.Children.Add(libelle);

            SystemControls.TextBox valeur = new()
            {
                Margin = new Thickness(0, 0, 0, 8),
                Style = (Style)FindResource("StyleTexteCopiable"),
                Text = lignes[index].Valeur,
                TextWrapping = TextWrapping.Wrap,
            };
            SystemControls.Grid.SetRow(valeur, index);
            SystemControls.Grid.SetColumn(valeur, 1);
            grille.Children.Add(valeur);
        }

        return grille;
    }

    /*
     * Construit le résumé de points affiché dans la vue détaillée du jeu.
     */
    private static string ConstruireTextePointsDetailJeu(GameInfoAndUserProgressV2 jeu)
    {
        int pointsTotaux = jeu.Succes.Values.Sum(item => Math.Max(0, item.Points));
        int pointsObtenus = jeu
            .Succes.Values.Where(SuccesEstDebloquePourDetail)
            .Sum(item => Math.Max(0, item.Points));

        if (pointsTotaux <= 0)
        {
            return "Inconnu";
        }

        return $"{pointsObtenus.ToString(CultureInfo.CurrentCulture)} / {pointsTotaux.ToString(CultureInfo.CurrentCulture)}";
    }

    /*
     * Indique si un succès doit être considéré comme débloqué dans la vue détaillée.
     */
    private static bool SuccesEstDebloquePourDetail(GameAchievementV2 succes)
    {
        return !string.IsNullOrWhiteSpace(succes.DateEarned)
            || !string.IsNullOrWhiteSpace(succes.DateEarnedHardcore);
    }

    /*
     * Construit le texte de récompense globale visible dans les détails du jeu.
     */
    private static string ConstruireTexteRecompenseJeu(GameInfoAndUserProgressV2 jeu)
    {
        string recompense = jeu.HighestAwardKind.Trim().ToLowerInvariant();

        return recompense switch
        {
            "mastered" => "Jeu maîtrisé",
            "completed" => "Jeu complété",
            "beaten" => "Jeu battu",
            "beaten-hardcore" => "Jeu battu en hardcore",
            "beaten-softcore" => "Jeu battu en softcore",
            _ => "Aucune récompense spéciale",
        };
    }

    /*
     * Construit le texte du dernier succès obtenu pour la vue détaillée.
     */
    private static string ConstruireTexteDernierSuccesJeu(GameInfoAndUserProgressV2 jeu)
    {
        GameAchievementV2? dernierSucces = jeu
            .Succes.Values.Select(item => new
            {
                Succes = item,
                Date = ExtraireDateDernierDeblocage(item),
            })
            .Where(item => item.Date is not null)
            .OrderByDescending(item => item.Date)
            .Select(item => item.Succes)
            .FirstOrDefault();

        if (dernierSucces is null)
        {
            return string.Empty;
        }

        string mode = !string.IsNullOrWhiteSpace(dernierSucces.DateEarnedHardcore)
            ? "Hardcore"
            : "Softcore";
        DateTimeOffset? date = ExtraireDateDernierDeblocage(dernierSucces);
        string dateTexte = date is null
            ? string.Empty
            : date.Value.ToLocalTime().ToString("d MMM yyyy HH:mm", CultureInfo.CurrentCulture);

        return string.IsNullOrWhiteSpace(dateTexte)
            ? $"{dernierSucces.Title} - {mode}"
            : $"{dernierSucces.Title} - {mode} - {dateTexte}";
    }

    /*
     * Extrait la date de déblocage la plus utile pour comparer les succès entre eux.
     */
    private static DateTimeOffset? ExtraireDateDernierDeblocage(GameAchievementV2 succes)
    {
        string valeur = !string.IsNullOrWhiteSpace(succes.DateEarnedHardcore)
            ? succes.DateEarnedHardcore
            : succes.DateEarned;

        if (string.IsNullOrWhiteSpace(valeur))
        {
            return null;
        }

        if (
            DateTimeOffset.TryParse(
                valeur,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal,
                out DateTimeOffset dateUtc
            )
        )
        {
            return dateUtc;
        }

        if (
            DateTimeOffset.TryParse(
                valeur,
                CultureInfo.CurrentCulture,
                DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal,
                out dateUtc
            )
        )
        {
            return dateUtc;
        }

        return null;
    }

    /*
     * Construit l'URL publique de la page RetroAchievements du jeu.
     */
    private static string ConstruireUrlJeuRetroAchievements(int identifiantJeu)
    {
        return $"https://retroachievements.org/game/{identifiantJeu.ToString(CultureInfo.InvariantCulture)}";
    }

    /*
     * Ouvre la page RetroAchievements du jeu dans le navigateur par défaut.
     */
    private static void OuvrirPageJeuRetroAchievements(int identifiantJeu)
    {
        if (identifiantJeu <= 0)
        {
            return;
        }

        try
        {
            Process.Start(
                new ProcessStartInfo
                {
                    FileName = ConstruireUrlJeuRetroAchievements(identifiantJeu),
                    UseShellExecute = true,
                }
            );
        }
        catch { }
    }
}
