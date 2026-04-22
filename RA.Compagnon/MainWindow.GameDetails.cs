/*
 * Regroupe la logique détaillée de la carte du jeu courant, en particulier
 * les actions Détails, Recharger et Rejouer ainsi que la modale de vue
 * détaillée accessible depuis l'interface principale.
 */
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
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
    private static readonly string CheminJournalRejouer =
        ServiceModeDiagnostic.ConstruireCheminJournal("journal-rejouer.log");
    private const int SwRestore = 9;
    private const uint InputKeyboard = 1;
    private const uint KeyEventFKeyUp = 0x0002;
    private const uint MessageToucheBas = 0x0100;
    private const uint MessageToucheHaut = 0x0101;
    private const ushort ToucheControle = 0x11;
    private const ushort ToucheO = 0x4F;
    private const int ScanCodeControle = 0x1D;
    private const int ScanCodeO = 0x18;

    private sealed record ContexteJouerBizHawk(
        int IdentifiantJeu,
        string TitreJeu,
        string CheminExecutable,
        string CheminJeu
    );

    [StructLayout(LayoutKind.Sequential)]
    private struct EntreeClavierNative
    {
        public uint Type;
        public DonneesEntreeClavierNative Donnees;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct DonneesEntreeClavierNative
    {
        [FieldOffset(0)]
        public DetailEntreeClavierNative Clavier;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DetailEntreeClavierNative
    {
        public ushort ToucheVirtuelle;
        public ushort ScanCode;
        public uint Indicateurs;
        public uint Temps;
        public nuint InformationSupplementaire;
    }

    [return: MarshalAs(UnmanagedType.Bool)]
    [LibraryImport("user32.dll")]
    private static partial bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);

    [return: MarshalAs(UnmanagedType.Bool)]
    [LibraryImport("user32.dll")]
    private static partial bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, EntreeClavierNative[] pInputs, int cbSize);

    [return: MarshalAs(UnmanagedType.Bool)]
    [LibraryImport("user32.dll")]
    private static partial bool PostMessageW(IntPtr hWnd, uint msg, nuint wParam, nint lParam);

    [LibraryImport("user32.dll")]
    private static partial IntPtr SetFocus(IntPtr hWnd);

    [LibraryImport("user32.dll")]
    private static partial uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [LibraryImport("kernel32.dll")]
    private static partial uint GetCurrentThreadId();

    [return: MarshalAs(UnmanagedType.Bool)]
    [LibraryImport("user32.dll")]
    private static partial bool AttachThreadInput(
        uint idAttach,
        uint idAttachTo,
        [MarshalAs(UnmanagedType.Bool)] bool fAttach
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
            if (MettreAJourActionJouerBizHawk(jeuSauvegarde))
            {
                return;
            }

            _vueModele.JeuCourant.ToolTipActionRejouer = string.Empty;
            _vueModele.JeuCourant.ActionRejouerActivee = false;
            _vueModele.JeuCourant.ActionRejouerVisible = false;
            return;
        }

        string cheminExecutable = DeterminerCheminExecutableRelance(jeuSauvegarde);
        _vueModele.JeuCourant.LibelleActionRejouer = "Rejouer";
        bool actionDisponible = !string.IsNullOrWhiteSpace(cheminExecutable);

        if (!actionDisponible && MettreAJourActionJouerBizHawk(jeuSauvegarde))
        {
            return;
        }

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
     * Propose un lancement BizHawk dès qu'un jeu affiché est connu; le clic
     * valide ensuite que le fichier local du jeu est disponible.
     */
    private bool MettreAJourActionJouerBizHawk(EtatJeuAfficheLocal? jeuSauvegarde)
    {
        ContexteJouerBizHawk? contexte = ObtenirContexteJouerBizHawk(
            jeuSauvegarde,
            exigerCheminJeu: false,
            exigerExecutable: false
        );

        if (contexte is null)
        {
            return false;
        }

        bool actionActivee = !DoitMasquerActionRejouerPendantJeu();
        _vueModele.JeuCourant.LibelleActionRejouer = "Jouer";
        _vueModele.JeuCourant.ActionRejouerActivee = actionActivee;
        _vueModele.JeuCourant.ActionRejouerVisible = true;
        _vueModele.JeuCourant.ToolTipActionRejouer = actionActivee
            ? "Jouer à ce jeu avec BizHawk"
            : "Disponible quand Dernier jeu réapparaît";
        return true;
    }

    /*
     * Retrouve l'exécutable BizHawk et le chemin du jeu affiché afin de
     * préparer un lancement fiable depuis le bouton Jouer.
     */
    private ContexteJouerBizHawk? ObtenirContexteJouerBizHawk(
        EtatJeuAfficheLocal? jeuSauvegarde,
        bool exigerCheminJeu = true,
        bool exigerExecutable = true
    )
    {
        EtatJeuAfficheLocal? jeuCible = jeuSauvegarde ?? _configurationConnexion.DernierJeuAffiche;
        int identifiantJeu =
            jeuCible?.Id ?? _dernieresDonneesJeuAffichees?.Jeu.Id ?? _dernierIdentifiantJeuApi;
        string titreJeu =
            jeuCible?.Title ?? _dernieresDonneesJeuAffichees?.Jeu.Title ?? string.Empty;

        if (identifiantJeu <= 0 || string.IsNullOrWhiteSpace(titreJeu))
        {
            return null;
        }

        string cheminExecutable = ServiceSourcesLocalesEmulateurs.TrouverCheminExecutableBizHawk();

        if (
            exigerExecutable
            && (string.IsNullOrWhiteSpace(cheminExecutable) || !File.Exists(cheminExecutable))
        )
        {
            return null;
        }

        string cheminJeu = ObtenirCheminJeuBizHawkPourJeuAffiche(
            jeuCible,
            identifiantJeu,
            titreJeu,
            ref cheminExecutable
        );

        if (exigerCheminJeu && (string.IsNullOrWhiteSpace(cheminJeu) || !File.Exists(cheminJeu)))
        {
            return null;
        }

        return new ContexteJouerBizHawk(identifiantJeu, titreJeu, cheminExecutable, cheminJeu);
    }

    /*
     * Priorise les sources les plus sûres pour rattacher le jeu affiché à une
     * ROM jouable par BizHawk.
     */
    private static string ObtenirCheminJeuBizHawkPourJeuAffiche(
        EtatJeuAfficheLocal? jeuCible,
        int identifiantJeu,
        string titreJeu,
        ref string cheminExecutable
    )
    {
        if (
            jeuCible is not null
            && !string.IsNullOrWhiteSpace(jeuCible.CheminJeuLocal)
            && File.Exists(jeuCible.CheminJeuLocal)
        )
        {
            return jeuCible.CheminJeuLocal;
        }

        if (
            ServiceSondeLocaleEmulateurs.EssayerObtenirContexteRejouerDepuisSources(
                "BizHawk",
                out int identifiantJeuDetecte,
                out _,
                out string cheminExecutableDetecte,
                out string cheminJeuDetecte
            )
            && identifiantJeuDetecte == identifiantJeu
            && File.Exists(cheminJeuDetecte)
        )
        {
            if (File.Exists(cheminExecutableDetecte))
            {
                cheminExecutable = cheminExecutableDetecte;
            }

            return cheminJeuDetecte;
        }

        if (
            ServiceSondeLocaleEmulateurs.EssayerObtenirContexteRejouerDepuisTitre(
                "BizHawk",
                titreJeu,
                out string cheminExecutableTitre,
                out string cheminJeuTitre
            ) && File.Exists(cheminJeuTitre)
        )
        {
            if (File.Exists(cheminExecutableTitre))
            {
                cheminExecutable = cheminExecutableTitre;
            }

            return cheminJeuTitre;
        }

        return string.Empty;
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

        if (
            jeuSauvegarde is null
            || string.IsNullOrWhiteSpace(jeuSauvegarde.CheminJeuLocal)
            || !File.Exists(jeuSauvegarde.CheminJeuLocal)
        )
        {
            if (ExecuterActionJouerBizHawk(jeuSauvegarde))
            {
                return;
            }

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
            if (ExecuterActionJouerBizHawk(jeuSauvegarde))
            {
                return;
            }

            JournaliserRejouer(
                "ignore",
                $"raison=emulateur_introuvable;emulateur={jeuSauvegarde.NomEmulateurRelance};cheminExecutable={cheminExecutable}"
            );
            MessageBox.Show(
                "L'émulateur n'a pas été retrouvé. Ouvre-le une fois ou corrige son emplacement dans l'aide.",
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
                "Le fichier du jeu n'a pas été retrouvé à son dernier emplacement connu.",
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
                    "Le lancement a été refusé ou n'a pas pu être démarré.",
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
     * Lance BizHawk avec la ROM associée au jeu affiché lorsque le contexte
     * de relance classique n'est pas disponible.
     */
    private bool ExecuterActionJouerBizHawk(EtatJeuAfficheLocal? jeuSauvegarde)
    {
        ContexteJouerBizHawk? contexte = ObtenirContexteJouerBizHawk(
            jeuSauvegarde,
            exigerCheminJeu: false,
            exigerExecutable: false
        );

        if (contexte is null)
        {
            return false;
        }

        if (
            string.IsNullOrWhiteSpace(contexte.CheminExecutable)
            || !File.Exists(contexte.CheminExecutable)
        )
        {
            JournaliserRejouer(
                "jouer_bizhawk_ignore",
                $"raison=bizhawk_introuvable;jeu={contexte.IdentifiantJeu.ToString(CultureInfo.InvariantCulture)};titre={contexte.TitreJeu}"
            );
            MessageBox.Show(
                "BizHawk n'a pas été retrouvé. Ouvre-le une fois ou indique son emplacement dans l'aide.",
                "Jouer",
                MessageBoxButton.OK,
                MessageBoxImage.Warning
            );
            return true;
        }

        if (string.IsNullOrWhiteSpace(contexte.CheminJeu) || !File.Exists(contexte.CheminJeu))
        {
            JournaliserRejouer(
                "jouer_bizhawk_selection_tentative",
                $"raison=jeu_introuvable;jeu={contexte.IdentifiantJeu.ToString(CultureInfo.InvariantCulture)};titre={contexte.TitreJeu};executable={contexte.CheminExecutable}"
            );
            return OuvrirBizHawkAvecSelectionJeu(contexte);
        }

        try
        {
            string arguments = $"\"{contexte.CheminJeu}\"";
            SuccesDebloqueDetecte? succesDirectExistant =
                ServiceSondeLocaleEmulateurs.LireDernierSuccesDebloqueDepuisSourceLocale(
                    "BizHawk",
                    contexte.IdentifiantJeu,
                    contexte.TitreJeu,
                    _succesJeuCourant
                );

            _signatureSuccesLocalDirectIgnoreeAuRejeu = succesDirectExistant is null
                ? string.Empty
                : ConstruireSignatureSuccesLocalDirect(succesDirectExistant);
            JournaliserRejouer(
                "jouer_bizhawk_tentative",
                $"jeu={contexte.IdentifiantJeu.ToString(CultureInfo.InvariantCulture)};titre={contexte.TitreJeu};executable={contexte.CheminExecutable};arguments={arguments};shell=false;baselineSucces={_signatureSuccesLocalDirectIgnoreeAuRejeu}"
            );

            Process? processus = Process.Start(
                new ProcessStartInfo
                {
                    FileName = contexte.CheminExecutable,
                    Arguments = arguments,
                    WorkingDirectory =
                        Path.GetDirectoryName(contexte.CheminExecutable) ?? string.Empty,
                    UseShellExecute = false,
                }
            );

            if (processus is null)
            {
                JournaliserRejouer("jouer_bizhawk_echec", "raison=processus_null");
                MessageBox.Show(
                    "BizHawk n'a pas pu être démarré.",
                    "Jouer",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
                return true;
            }

            JournaliserRejouer(
                "jouer_bizhawk_demarre",
                $"pid={processus.Id.ToString(CultureInfo.InvariantCulture)};nom={processus.ProcessName}"
            );
            _rejeuDemarreEnAttenteChargement = true;
            ChargerJeuResolutLocal(contexte.IdentifiantJeu, contexte.TitreJeu);
            return true;
        }
        catch (Exception exception)
        {
            JournaliserRejouer(
                "jouer_bizhawk_exception",
                $"type={exception.GetType().Name};message={exception.Message}"
            );
            MessageBox.Show(
                "Impossible de démarrer BizHawk pour le moment.",
                "Jouer",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
            return true;
        }
    }

    /*
     * Ouvre BizHawk sans ROM connue puis déclenche la fenêtre de sélection de
     * jeu afin que l'utilisateur choisisse lui-même le fichier à lancer.
     */
    private bool OuvrirBizHawkAvecSelectionJeu(ContexteJouerBizHawk contexte)
    {
        try
        {
            Process? processus = Process.Start(
                new ProcessStartInfo
                {
                    FileName = contexte.CheminExecutable,
                    WorkingDirectory =
                        Path.GetDirectoryName(contexte.CheminExecutable) ?? string.Empty,
                    UseShellExecute = false,
                }
            );

            if (processus is null)
            {
                JournaliserRejouer("jouer_bizhawk_selection_echec", "raison=processus_null");
                MessageBox.Show(
                    "BizHawk n'a pas pu être démarré.",
                    "Jouer",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
                return true;
            }

            JournaliserRejouer(
                "jouer_bizhawk_selection_demarre",
                $"pid={processus.Id.ToString(CultureInfo.InvariantCulture)};nom={processus.ProcessName};jeu={contexte.IdentifiantJeu.ToString(CultureInfo.InvariantCulture)}"
            );
            _ = OuvrirFenetreSelectionJeuBizHawkAsync(processus, contexte);
            return true;
        }
        catch (Exception exception)
        {
            JournaliserRejouer(
                "jouer_bizhawk_selection_exception",
                $"type={exception.GetType().Name};message={exception.Message}"
            );
            MessageBox.Show(
                "Impossible de démarrer BizHawk pour ouvrir la sélection de jeu.",
                "Jouer",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
            return true;
        }
    }

    /*
     * Attend que la fenêtre BizHawk soit prête, puis envoie Ctrl+O pour ouvrir
     * la boîte de recherche de ROM.
     */
    private static async Task OuvrirFenetreSelectionJeuBizHawkAsync(
        Process processus,
        ContexteJouerBizHawk contexte
    )
    {
        try
        {
            for (int tentative = 1; tentative <= 24; tentative++)
            {
                if (processus.HasExited)
                {
                    JournaliserRejouer(
                        "jouer_bizhawk_selection_abandon",
                        $"raison=processus_termine;jeu={contexte.IdentifiantJeu.ToString(CultureInfo.InvariantCulture)}"
                    );
                    return;
                }

                IntPtr poigneeFenetre = TrouverPoigneeFenetreBizHawk(
                    processus,
                    contexte.CheminExecutable
                );

                if (poigneeFenetre != IntPtr.Zero)
                {
                    bool focusApplique = ActiverFenetreBizHawk(poigneeFenetre);
                    await Task.Delay(500);
                    bool raccourciEnvoye = EnvoyerRaccourciOuvrirRomBizHawk(poigneeFenetre);
                    JournaliserRejouer(
                        "jouer_bizhawk_selection_ouverte",
                        $"tentative={tentative.ToString(CultureInfo.InvariantCulture)};jeu={contexte.IdentifiantJeu.ToString(CultureInfo.InvariantCulture)};titre={contexte.TitreJeu};focus={focusApplique.ToString(CultureInfo.InvariantCulture)};raccourci={raccourciEnvoye.ToString(CultureInfo.InvariantCulture)}"
                    );
                    return;
                }

                await Task.Delay(250);
            }

            JournaliserRejouer(
                "jouer_bizhawk_selection_abandon",
                $"raison=fenetre_introuvable;jeu={contexte.IdentifiantJeu.ToString(CultureInfo.InvariantCulture)}"
            );
        }
        catch (Exception exception)
        {
            JournaliserRejouer(
                "jouer_bizhawk_selection_exception_async",
                $"type={exception.GetType().Name};message={exception.Message}"
            );
        }
    }

    /*
     * Retrouve la poignée de la fenêtre BizHawk, même quand le processus lancé
     * délègue à une instance déjà active.
     */
    private static IntPtr TrouverPoigneeFenetreBizHawk(
        Process processusLance,
        string cheminExecutable
    )
    {
        try
        {
            if (!processusLance.HasExited)
            {
                processusLance.Refresh();

                if (processusLance.MainWindowHandle != IntPtr.Zero)
                {
                    return processusLance.MainWindowHandle;
                }
            }
        }
        catch { }

        string nomProcessusAttendu = Path.GetFileNameWithoutExtension(cheminExecutable);
        IEnumerable<string> nomsProcessus = new[] { nomProcessusAttendu, "EmuHawk", "BizHawk" }
            .Where(nom => !string.IsNullOrWhiteSpace(nom))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (string nomProcessus in nomsProcessus)
        {
            foreach (Process processus in Process.GetProcessesByName(nomProcessus))
            {
                using (processus)
                {
                    try
                    {
                        if (processus.HasExited)
                        {
                            continue;
                        }

                        processus.Refresh();

                        if (processus.MainWindowHandle == IntPtr.Zero)
                        {
                            continue;
                        }

                        string cheminProcessus = LireCheminExecutableProcessus(processus);

                        if (
                            string.IsNullOrWhiteSpace(cheminProcessus)
                            || string.Equals(
                                cheminProcessus,
                                cheminExecutable,
                                StringComparison.OrdinalIgnoreCase
                            )
                            || ServiceSourcesLocalesEmulateurs.CheminExecutableCorrespondEmulateur(
                                "BizHawk",
                                cheminProcessus
                            )
                        )
                        {
                            return processus.MainWindowHandle;
                        }
                    }
                    catch { }
                }
            }
        }

        return IntPtr.Zero;
    }

    /*
     * Lit prudemment le chemin d'un processus, car Windows peut refuser l'accès
     * selon le niveau d'intégrité de l'application cible.
     */
    private static string LireCheminExecutableProcessus(Process processus)
    {
        try
        {
            return processus.MainModule?.FileName?.Trim() ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    /*
     * Ramène BizHawk au premier plan en attachant temporairement les files
     * d'entrée clavier lorsque Windows refuse un simple SetForegroundWindow.
     */
    private static bool ActiverFenetreBizHawk(IntPtr poigneeFenetre)
    {
        ShowWindowAsync(poigneeFenetre, SwRestore);

        uint threadFenetre = GetWindowThreadProcessId(poigneeFenetre, out _);
        uint threadCourant = GetCurrentThreadId();
        bool attache =
            threadFenetre != 0
            && threadFenetre != threadCourant
            && AttachThreadInput(threadCourant, threadFenetre, true);

        try
        {
            SetFocus(poigneeFenetre);
            return SetForegroundWindow(poigneeFenetre);
        }
        finally
        {
            if (attache)
            {
                AttachThreadInput(threadCourant, threadFenetre, false);
            }
        }
    }

    /*
     * Simule Ctrl+O, le raccourci standard de BizHawk pour File > Open ROM.
     */
    private static bool EnvoyerRaccourciOuvrirRomBizHawk(IntPtr poigneeFenetre)
    {
        EntreeClavierNative[] entrees =
        [
            CreerEntreeClavier(ToucheControle, 0),
            CreerEntreeClavier(ToucheO, 0),
            CreerEntreeClavier(ToucheO, KeyEventFKeyUp),
            CreerEntreeClavier(ToucheControle, KeyEventFKeyUp),
        ];

        uint touchesEnvoyees = SendInput(
            (uint)entrees.Length,
            entrees,
            Marshal.SizeOf<EntreeClavierNative>()
        );

        bool clavierGlobalEnvoye = touchesEnvoyees == entrees.Length;
        bool messageCibleEnvoye =
            PostMessageW(
                poigneeFenetre,
                MessageToucheBas,
                ToucheControle,
                ConstruireParametreTouche(ScanCodeControle, toucheRelachee: false)
            )
            && PostMessageW(
                poigneeFenetre,
                MessageToucheBas,
                ToucheO,
                ConstruireParametreTouche(ScanCodeO, toucheRelachee: false)
            )
            && PostMessageW(
                poigneeFenetre,
                MessageToucheHaut,
                ToucheO,
                ConstruireParametreTouche(ScanCodeO, toucheRelachee: true)
            )
            && PostMessageW(
                poigneeFenetre,
                MessageToucheHaut,
                ToucheControle,
                ConstruireParametreTouche(ScanCodeControle, toucheRelachee: true)
            );

        return clavierGlobalEnvoye || messageCibleEnvoye;
    }

    /*
     * Construit une entrée clavier native pour SendInput.
     */
    private static EntreeClavierNative CreerEntreeClavier(ushort toucheVirtuelle, uint indicateurs)
    {
        return new EntreeClavierNative
        {
            Type = InputKeyboard,
            Donnees = new DonneesEntreeClavierNative
            {
                Clavier = new DetailEntreeClavierNative
                {
                    ToucheVirtuelle = toucheVirtuelle,
                    Indicateurs = indicateurs,
                },
            },
        };
    }

    /*
     * Construit le paramètre lParam minimal attendu par les messages clavier
     * Win32 envoyés directement à la fenêtre BizHawk.
     */
    private static nint ConstruireParametreTouche(int scanCode, bool toucheRelachee)
    {
        int valeur = 1 | (scanCode << 16);

        if (toucheRelachee)
        {
            valeur |= 1 << 30;
            valeur |= 1 << 31;
        }

        return valeur;
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
            Padding = ConstantesDesign.PaddingBoutonAction,
        };
        boutonPageJeu.Click += (_, _) => OuvrirPageJeuRetroAchievements(jeu.Id);
        contenu.Children.Add(boutonPageJeu);

        SystemControls.Border conteneurContenu = new()
        {
            Padding = ConstantesDesign.PaddingCarteSecondaire,
            HorizontalAlignment = HorizontalAlignment.Center,
            CornerRadius = ObtenirRayonCoins("RayonCoinsStandard", ConstantesDesign.EspaceStandard),
            Child = contenu,
        };

        UiControls.ContentDialog dialogue = new(RacineModales)
        {
            Title = "Détails du jeu",
            Content = conteneurContenu,
            MinWidth =
                ConstantesDesign.LargeurCarteSecondaire + (MargeInterieureModaleConnexion * 2),
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
