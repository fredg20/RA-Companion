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
using System.Windows.Automation;
using System.Windows.Media;
using Microsoft.Win32;
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
    private int _versionChargementNomsFichiersCompatiblesJouer;
    private int _identifiantJeuNomsFichiersCompatiblesJouer;
    private const int SwRestore = 9;
    private const uint InputKeyboard = 1;
    private const uint KeyEventFKeyUp = 0x0002;
    private const uint MessageToucheBas = 0x0100;
    private const uint MessageToucheHaut = 0x0101;
    private const ushort ToucheEchappement = 0x1B;
    private const ushort ToucheControle = 0x11;
    private const ushort ToucheAlt = 0x12;
    private const ushort ToucheF = 0x46;
    private const ushort ToucheO = 0x4F;
    private const int ScanCodeEchappement = 0x01;
    private const int ScanCodeControle = 0x1D;
    private const int ScanCodeAlt = 0x38;
    private const int ScanCodeF = 0x21;
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
        public DetailEntreeSourisNative Souris;

        [FieldOffset(0)]
        public DetailEntreeClavierNative Clavier;

        [FieldOffset(0)]
        public DetailEntreeMaterielNative Materiel;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DetailEntreeSourisNative
    {
        public int DeltaX;
        public int DeltaY;
        public uint DonneeSouris;
        public uint Indicateurs;
        public uint Temps;
        public nuint InformationSupplementaire;
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

    [StructLayout(LayoutKind.Sequential)]
    private struct DetailEntreeMaterielNative
    {
        public uint Message;
        public ushort ParametreBas;
        public ushort ParametreHaut;
    }

    [return: MarshalAs(UnmanagedType.Bool)]
    [LibraryImport("user32.dll")]
    private static partial bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);

    [return: MarshalAs(UnmanagedType.Bool)]
    [LibraryImport("user32.dll")]
    private static partial bool SetForegroundWindow(IntPtr hWnd);

    [LibraryImport("user32.dll", SetLastError = true)]
    private static partial uint SendInput(
        uint nInputs,
        [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] EntreeClavierNative[] pInputs,
        int cbSize
    );

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
        MasquerNomsFichiersCompatiblesJouer();
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
            MasquerNomsFichiersCompatiblesJouer();
            return;
        }

        string cheminExecutable = DeterminerCheminExecutableRelance(jeuSauvegarde);
        _vueModele.JeuCourant.LibelleActionRejouer = "Rejouer";
        bool actionDisponible = !string.IsNullOrWhiteSpace(cheminExecutable);

        if (!actionDisponible && MettreAJourActionJouerBizHawk(jeuSauvegarde))
        {
            return;
        }

        MasquerNomsFichiersCompatiblesJouer();

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
        ActualiserNomsFichiersCompatiblesJouer(contexte.IdentifiantJeu);
        return true;
    }

    /*
     * Masque la zone des noms de fichiers compatibles quand l'action
     * courante est Rejouer ou qu'aucun jeu cible n'est disponible.
     */
    private void MasquerNomsFichiersCompatiblesJouer()
    {
        _versionChargementNomsFichiersCompatiblesJouer++;
        _identifiantJeuNomsFichiersCompatiblesJouer = 0;
        _vueModele.JeuCourant.NomsFichiersCompatiblesJouer = string.Empty;
        _vueModele.JeuCourant.ToolTipNomsFichiersCompatiblesJouer = string.Empty;
        _vueModele.JeuCourant.NomsFichiersCompatiblesJouerVisibles = false;
    }

    /*
     * Lance le chargement des noms de fichiers compatibles à afficher entre
     * Jouer et Détails lorsque Compagnon ne connaît pas encore la ROM locale.
     */
    private void ActualiserNomsFichiersCompatiblesJouer(int identifiantJeu)
    {
        if (identifiantJeu <= 0)
        {
            MasquerNomsFichiersCompatiblesJouer();
            return;
        }

        if (
            _identifiantJeuNomsFichiersCompatiblesJouer == identifiantJeu
            && _vueModele.JeuCourant.NomsFichiersCompatiblesJouerVisibles
            && !_vueModele.JeuCourant.NomsFichiersCompatiblesJouer.Contains(
                "chargement",
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            return;
        }

        _identifiantJeuNomsFichiersCompatiblesJouer = identifiantJeu;
        int versionChargement = ++_versionChargementNomsFichiersCompatiblesJouer;

        DefinirNomsFichiersCompatiblesJouer(
            "Fichiers compatibles : chargement...",
            "Chargement des noms de fichiers compatibles depuis RetroAchievements.",
            visible: true
        );

        if (string.IsNullOrWhiteSpace(_configurationConnexion.CleApiWeb))
        {
            DefinirNomsFichiersCompatiblesJouer(
                "Fichiers compatibles indisponibles",
                "La clé API RetroAchievements est requise pour afficher les noms de fichiers compatibles.",
                visible: true
            );
            return;
        }

        LancerTacheNonBloquante(
            ChargerNomsFichiersCompatiblesJouerAsync(identifiantJeu, versionChargement),
            "charger_noms_fichiers_compatibles"
        );
    }

    /*
     * Charge les noms de fichiers compatibles sans bloquer l'interface, puis
     * ignore le résultat si l'utilisateur a changé de jeu entre-temps.
     */
    private async Task ChargerNomsFichiersCompatiblesJouerAsync(
        int identifiantJeu,
        int versionChargement
    )
    {
        try
        {
            IReadOnlyList<GameHashV2> hashes = await ClientRetroAchievements.ObtenirHashesJeuAsync(
                _configurationConnexion.CleApiWeb,
                identifiantJeu
            );

            if (!NomsFichiersCompatiblesJouerToujoursCourants(identifiantJeu, versionChargement))
            {
                return;
            }

            List<string> noms =
            [
                .. hashes
                    .Select(hash => hash.NomFichier)
                    .Where(nom => !string.IsNullOrWhiteSpace(nom))
                    .Select(nom => nom.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Order(StringComparer.CurrentCultureIgnoreCase),
            ];

            if (noms.Count == 0)
            {
                DefinirNomsFichiersCompatiblesJouer(
                    "Aucun nom compatible listé",
                    "RetroAchievements ne liste aucun nom de fichier compatible pour ce jeu.",
                    visible: true
                );
                return;
            }

            DefinirNomsFichiersCompatiblesJouer(
                ConstruireResumeNomsFichiersCompatiblesJouer(noms),
                ConstruireInfobulleNomsFichiersCompatiblesJouer(noms),
                visible: true
            );

            JournaliserRejouer(
                "noms_fichiers_compatibles_jouer_charge",
                $"jeu={identifiantJeu.ToString(CultureInfo.InvariantCulture)};nombre={noms.Count.ToString(CultureInfo.InvariantCulture)};premier={NettoyerDetailHashJournal(noms.FirstOrDefault())}"
            );
        }
        catch (Exception exception)
        {
            if (!NomsFichiersCompatiblesJouerToujoursCourants(identifiantJeu, versionChargement))
            {
                return;
            }

            JournaliserRejouer(
                "noms_fichiers_compatibles_jouer_exception",
                $"jeu={identifiantJeu.ToString(CultureInfo.InvariantCulture)};type={exception.GetType().Name};message={exception.Message}"
            );
            DefinirNomsFichiersCompatiblesJouer(
                "Fichiers compatibles indisponibles",
                "Impossible de charger les noms de fichiers compatibles depuis RetroAchievements.",
                visible: true
            );
        }
    }

    /*
     * Indique si le résultat asynchrone appartient encore au jeu affiché.
     */
    private bool NomsFichiersCompatiblesJouerToujoursCourants(
        int identifiantJeu,
        int versionChargement
    )
    {
        return _identifiantJeuNomsFichiersCompatiblesJouer == identifiantJeu
            && _versionChargementNomsFichiersCompatiblesJouer == versionChargement;
    }

    /*
     * Applique le texte et l'infobulle de la zone des fichiers compatibles.
     */
    private void DefinirNomsFichiersCompatiblesJouer(string texte, string infobulle, bool visible)
    {
        _vueModele.JeuCourant.NomsFichiersCompatiblesJouer = texte;
        _vueModele.JeuCourant.ToolTipNomsFichiersCompatiblesJouer = infobulle;
        _vueModele.JeuCourant.NomsFichiersCompatiblesJouerVisibles = visible;
    }

    /*
     * Construit un résumé court pour la ligne d'action sans faire déborder
     * la carte lorsque plusieurs fichiers sont compatibles.
     */
    private static string ConstruireResumeNomsFichiersCompatiblesJouer(List<string> noms)
    {
        const int NombreMaximumNomsAffiches = 3;

        string resume = string.Join(Environment.NewLine, noms.Take(NombreMaximumNomsAffiches));

        if (noms.Count > NombreMaximumNomsAffiches)
        {
            int nombreRestant = noms.Count - NombreMaximumNomsAffiches;
            resume =
                $"{resume}{Environment.NewLine}(+{nombreRestant.ToString(CultureInfo.CurrentCulture)})";
        }

        return $"Fichiers compatibles :{Environment.NewLine}{resume}";
    }

    /*
     * Construit l'infobulle complète contenant tous les noms de fichiers
     * compatibles retournés par RetroAchievements.
     */
    private static string ConstruireInfobulleNomsFichiersCompatiblesJouer(List<string> noms)
    {
        return string.Join(
            Environment.NewLine,
            ["Fichiers compatibles RetroAchievements :", .. noms]
        );
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
            OpenFileDialog dialogue = new()
            {
                Title = $"Choisir le fichier du jeu à ouvrir avec BizHawk - {contexte.TitreJeu}",
                CheckFileExists = true,
                Multiselect = false,
                Filter =
                    "Jeux compatibles|*.nes;*.fds;*.sms;*.gg;*.sg;*.pce;*.cue;*.iso;*.bin;*.gb;*.gbc;*.gba;*.sfc;*.smc;*.n64;*.z64;*.v64;*.zip;*.7z|Tous les fichiers|*.*",
            };

            bool? resultat = dialogue.ShowDialog(this);

            if (resultat != true || string.IsNullOrWhiteSpace(dialogue.FileName))
            {
                JournaliserRejouer(
                    "jouer_bizhawk_selection_annulee",
                    $"jeu={contexte.IdentifiantJeu.ToString(CultureInfo.InvariantCulture)};titre={contexte.TitreJeu}"
                );
                return true;
            }

            string cheminJeu = dialogue.FileName;
            Process? processus = Process.Start(
                new ProcessStartInfo
                {
                    FileName = contexte.CheminExecutable,
                    Arguments = $"\"{cheminJeu}\"",
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
                $"pid={processus.Id.ToString(CultureInfo.InvariantCulture)};nom={processus.ProcessName};jeu={contexte.IdentifiantJeu.ToString(CultureInfo.InvariantCulture)};rom={cheminJeu}"
            );
            MemoriserCheminJeuBizHawk(contexte, cheminJeu);
            _rejeuDemarreEnAttenteChargement = true;
            ChargerJeuResolutLocal(contexte.IdentifiantJeu, contexte.TitreJeu);
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
     * Mémorise le chemin choisi manuellement afin que les prochains lancements
     * puissent utiliser BizHawk directement sans redemander le fichier.
     */
    private void MemoriserCheminJeuBizHawk(ContexteJouerBizHawk contexte, string cheminJeu)
    {
        EtatJeuAfficheLocal? jeuAffiche = _configurationConnexion.DernierJeuAffiche;

        if (jeuAffiche is null || jeuAffiche.Id != contexte.IdentifiantJeu)
        {
            return;
        }

        jeuAffiche.NomEmulateurRelance = "BizHawk";
        jeuAffiche.CheminExecutableEmulateur = contexte.CheminExecutable;
        jeuAffiche.CheminJeuLocal = cheminJeu;
        _dernierJeuAfficheModifie = true;
        LancerTacheNonBloquante(
            PersisterDernierJeuAfficheSiNecessaireAsync(),
            "persistance_dernier_jeu_apres_jouer_bizhak"
        );
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
                    bool automatisationMenu = await TenterOuvrirSelectionRomBizHawkParMenuAsync(
                        poigneeFenetre
                    );
                    bool dialogueApresMenu =
                        automatisationMenu && await AttendreDialogueSelectionRomBizHawkAsync();
                    (bool raccourciEnvoye, bool dialogueApresRaccourci) = dialogueApresMenu
                        ? (false, false)
                        : await TenterOuvrirSelectionRomBizHawkParRaccourcisAsync(poigneeFenetre);

                    bool dialogueDetecte = dialogueApresMenu || dialogueApresRaccourci;
                    JournaliserRejouer(
                        dialogueDetecte
                            ? "jouer_bizhawk_selection_ouverte"
                            : "jouer_bizhawk_selection_non_confirmee",
                        $"tentative={tentative.ToString(CultureInfo.InvariantCulture)};jeu={contexte.IdentifiantJeu.ToString(CultureInfo.InvariantCulture)};titre={contexte.TitreJeu};focus={focusApplique.ToString(CultureInfo.InvariantCulture)};menu={automatisationMenu.ToString(CultureInfo.InvariantCulture)};dialogueMenu={dialogueApresMenu.ToString(CultureInfo.InvariantCulture)};raccourci={raccourciEnvoye.ToString(CultureInfo.InvariantCulture)};dialogueRaccourci={dialogueApresRaccourci.ToString(CultureInfo.InvariantCulture)}"
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
     * Tente d'ouvrir la sélection de ROM par le menu natif BizHawk avant de
     * retomber sur une simulation clavier plus fragile.
     */
    private static async Task<bool> TenterOuvrirSelectionRomBizHawkParMenuAsync(
        IntPtr poigneeFenetre
    )
    {
        try
        {
            AutomationElement fenetre = AutomationElement.FromHandle(poigneeFenetre);

            if (TenterInvoquerElementMenu(fenetre, EstMenuOuvrirRomBizHawk))
            {
                return true;
            }

            if (TenterOuvrirMenuFichierBizHawk(fenetre))
            {
                await Task.Delay(250);

                if (TenterInvoquerElementMenu(fenetre, EstMenuOuvrirRomBizHawk))
                {
                    return true;
                }

                if (
                    TenterInvoquerElementMenu(
                        AutomationElement.RootElement,
                        EstMenuOuvrirRomBizHawk
                    )
                )
                {
                    return true;
                }
            }
        }
        catch (Exception exception)
        {
            JournaliserRejouer(
                "jouer_bizhawk_selection_menu_exception",
                $"type={exception.GetType().Name};message={exception.Message}"
            );
        }

        return false;
    }

    /*
     * Déplie le menu Fichier de BizHawk afin que ses sous-actions deviennent
     * accessibles à UI Automation.
     */
    private static bool TenterOuvrirMenuFichierBizHawk(AutomationElement racine)
    {
        return TenterInvoquerElementMenu(racine, EstMenuFichierBizHawk, autoriserExpansion: true);
    }

    /*
     * Cherche un élément de menu correspondant à un prédicat, puis tente de
     * l'invoquer ou de le déplier selon ses patrons UI Automation disponibles.
     */
    private static bool TenterInvoquerElementMenu(
        AutomationElement racine,
        Func<AutomationElement, bool> correspond,
        bool autoriserExpansion = false
    )
    {
        AutomationElementCollection elements = racine.FindAll(
            TreeScope.Descendants,
            System.Windows.Automation.Condition.TrueCondition
        );

        foreach (AutomationElement element in elements.Cast<AutomationElement>())
        {
            if (!correspond(element))
            {
                continue;
            }

            if (
                autoriserExpansion
                && element.TryGetCurrentPattern(
                    ExpandCollapsePattern.Pattern,
                    out object? motifExpansion
                )
                && motifExpansion is ExpandCollapsePattern expansion
            )
            {
                expansion.Expand();
                return true;
            }

            if (
                element.TryGetCurrentPattern(InvokePattern.Pattern, out object? motifInvocation)
                && motifInvocation is InvokePattern invocation
            )
            {
                invocation.Invoke();
                return true;
            }
        }

        return false;
    }

    /*
     * Reconnaît le menu Fichier de BizHawk, avec tolérance pour les libellés
     * qui contiennent un accélérateur clavier.
     */
    private static bool EstMenuFichierBizHawk(AutomationElement element)
    {
        if (element.Current.ControlType != ControlType.MenuItem)
        {
            return false;
        }

        string nom = NormaliserLibelleMenuBizHawk(element.Current.Name);
        return string.Equals(nom, "file", StringComparison.OrdinalIgnoreCase);
    }

    /*
     * Reconnaît l'action Open ROM de BizHawk, peu importe la présence de
     * points de suspension ou du raccourci dans le libellé UI Automation.
     */
    private static bool EstMenuOuvrirRomBizHawk(AutomationElement element)
    {
        if (element.Current.ControlType != ControlType.MenuItem)
        {
            return false;
        }

        string nom = NormaliserLibelleMenuBizHawk(element.Current.Name);
        return nom.Contains("open rom", StringComparison.OrdinalIgnoreCase)
            || nom.Contains("open a rom", StringComparison.OrdinalIgnoreCase)
            || nom.Contains("open game", StringComparison.OrdinalIgnoreCase);
    }

    /*
     * Attend brièvement qu'une boîte de sélection de fichier apparaisse après
     * une tentative d'ouverture de ROM.
     */
    private static async Task<bool> AttendreDialogueSelectionRomBizHawkAsync()
    {
        for (int tentative = 1; tentative <= 12; tentative++)
        {
            if (DialogueSelectionRomBizHawkEstVisible())
            {
                return true;
            }

            await Task.Delay(250);
        }

        return false;
    }

    /*
     * Inspecte les fenêtres de premier niveau pour confirmer qu'une boîte de
     * sélection de fichier est réellement visible.
     */
    private static bool DialogueSelectionRomBizHawkEstVisible()
    {
        try
        {
            AutomationElementCollection fenetres = AutomationElement.RootElement.FindAll(
                TreeScope.Children,
                System.Windows.Automation.Condition.TrueCondition
            );

            foreach (AutomationElement fenetre in fenetres.Cast<AutomationElement>())
            {
                if (fenetre.Current.ControlType != ControlType.Window)
                {
                    continue;
                }

                if (EstDialogueSelectionRomBizHawk(fenetre))
                {
                    return true;
                }
            }
        }
        catch { }

        return false;
    }

    /*
     * Reconnaît les titres usuels d'une boîte d'ouverture de fichier associée
     * à BizHawk ou à Windows.
     */
    private static bool EstDialogueSelectionRomBizHawk(AutomationElement fenetre)
    {
        string nom = NormaliserLibelleMenuBizHawk(fenetre.Current.Name);

        if (string.IsNullOrWhiteSpace(nom))
        {
            return false;
        }

        return nom.Contains("open rom", StringComparison.OrdinalIgnoreCase)
            || nom.Contains("open a rom", StringComparison.OrdinalIgnoreCase)
            || nom.Contains("open file", StringComparison.OrdinalIgnoreCase)
            || nom.Contains("select rom", StringComparison.OrdinalIgnoreCase)
            || string.Equals(nom, "open", StringComparison.OrdinalIgnoreCase)
            || string.Equals(nom, "ouvrir", StringComparison.OrdinalIgnoreCase);
    }

    /*
     * Nettoie un libellé de menu pour comparer les actions BizHawk sans être
     * gêné par les accélérateurs, les points de suspension ou les raccourcis.
     */
    private static string NormaliserLibelleMenuBizHawk(string? libelle)
    {
        if (string.IsNullOrWhiteSpace(libelle))
        {
            return string.Empty;
        }

        string valeur = libelle.Replace("&", string.Empty, StringComparison.Ordinal);
        int positionTabulation = valeur.IndexOf('\t', StringComparison.Ordinal);

        if (positionTabulation >= 0)
        {
            valeur = valeur[..positionTabulation];
        }

        return valeur.Replace("...", string.Empty, StringComparison.Ordinal).Trim();
    }

    /*
     * Tente plusieurs raccourcis clavier et confirme après chaque tentative
     * qu'une boîte de sélection de ROM est réellement apparue.
     */
    private static async Task<(
        bool Envoye,
        bool DialogueDetecte
    )> TenterOuvrirSelectionRomBizHawkParRaccourcisAsync(IntPtr poigneeFenetre)
    {
        bool raccourciEnvoye = false;

        for (int tentative = 1; tentative <= 3; tentative++)
        {
            ActiverFenetreBizHawk(poigneeFenetre);
            await Task.Delay(150);
            EnvoyerToucheSimple(ToucheEchappement);
            await Task.Delay(150);

            bool ctrlOEnvoye = EnvoyerRaccourciOuvrirRomBizHawk(poigneeFenetre);
            raccourciEnvoye |= ctrlOEnvoye;

            if (ctrlOEnvoye && await AttendreDialogueSelectionRomBizHawkAsync())
            {
                JournaliserRejouer(
                    "jouer_bizhawk_selection_raccourci",
                    $"methode=ctrl_o;tentative={tentative.ToString(CultureInfo.InvariantCulture)}"
                );
                return (true, true);
            }

            ActiverFenetreBizHawk(poigneeFenetre);
            await Task.Delay(150);
            bool menuClavierEnvoye = EnvoyerRaccourciMenuFichierOuvrirRomBizHawk();
            raccourciEnvoye |= menuClavierEnvoye;

            if (menuClavierEnvoye && await AttendreDialogueSelectionRomBizHawkAsync())
            {
                JournaliserRejouer(
                    "jouer_bizhawk_selection_raccourci",
                    $"methode=alt_f_o;tentative={tentative.ToString(CultureInfo.InvariantCulture)}"
                );
                return (true, true);
            }
        }

        return (raccourciEnvoye, false);
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

        bool clavierGlobalEnvoye = EnvoyerSequenceClavier(entrees);
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
     * Simule Alt+F puis O pour ouvrir le menu Fichier et déclencher Open ROM
     * lorsque le raccourci direct n'est pas capté.
     */
    private static bool EnvoyerRaccourciMenuFichierOuvrirRomBizHawk()
    {
        bool menuFichierEnvoye = EnvoyerSequenceClavier([
            CreerEntreeClavier(ToucheAlt, 0),
            CreerEntreeClavier(ToucheF, 0),
            CreerEntreeClavier(ToucheF, KeyEventFKeyUp),
            CreerEntreeClavier(ToucheAlt, KeyEventFKeyUp),
        ]);

        Thread.Sleep(200);
        bool ouvrirRomEnvoye = EnvoyerSequenceClavier([
            CreerEntreeClavier(ToucheO, 0),
            CreerEntreeClavier(ToucheO, KeyEventFKeyUp),
        ]);

        return menuFichierEnvoye && ouvrirRomEnvoye;
    }

    /*
     * Envoie une touche isolée pour fermer un menu ouvert ou nettoyer l'état
     * clavier avant une nouvelle tentative.
     */
    private static bool EnvoyerToucheSimple(ushort toucheVirtuelle)
    {
        return EnvoyerSequenceClavier([
            CreerEntreeClavier(toucheVirtuelle, 0),
            CreerEntreeClavier(toucheVirtuelle, KeyEventFKeyUp),
        ]);
    }

    /*
     * Envoie une séquence clavier native et confirme que Windows a accepté
     * chaque entrée.
     */
    private static bool EnvoyerSequenceClavier(EntreeClavierNative[] entrees)
    {
        uint touchesEnvoyees = SendInput(
            (uint)entrees.Length,
            entrees,
            Marshal.SizeOf<EntreeClavierNative>()
        );

        if (touchesEnvoyees != entrees.Length)
        {
            JournaliserRejouer(
                "jouer_bizhawk_selection_sendinput_echec",
                $"attendu={entrees.Length.ToString(CultureInfo.InvariantCulture)};envoye={touchesEnvoyees.ToString(CultureInfo.InvariantCulture)};erreur={Marshal.GetLastWin32Error().ToString(CultureInfo.InvariantCulture)};taille={Marshal.SizeOf<EntreeClavierNative>().ToString(CultureInfo.InvariantCulture)}"
            );
        }

        return touchesEnvoyees == entrees.Length;
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
     * Affiche les fichiers compatibles RetroAchievements connus pour le jeu
     * courant sans exposer la clé API de l'utilisateur.
     */
    /*
     * Remplace le bloc de hashes déjà affiché afin qu'un nouveau clic
     * recharge le contenu sans empiler plusieurs résultats.
     */
    /*
     * Construit le bloc résultat des fichiers compatibles avec une zone
     * copiable pour faciliter la validation d'un hash ou d'un nom de fichier.
     */
    /*
     * Construit un bloc de message simple pour l'état de chargement ou
     * d'erreur des fichiers compatibles.
     */
    /*
     * Construit le texte détaillé des fichiers compatibles afin de permettre
     * une copie facile depuis la modale.
     */
    /*
     * Nettoie un libellé de hash avant journalisation afin de garder chaque
     * entrée sur une seule ligne.
     */
    private static string NettoyerDetailHashJournal(string? valeur)
    {
        return string.IsNullOrWhiteSpace(valeur)
            ? string.Empty
            : valeur
                .Replace("\r", " ", StringComparison.Ordinal)
                .Replace("\n", " ", StringComparison.Ordinal)
                .Trim();
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
