using RA.Compagnon.Modeles.Etat;

/*
 * Centralise les transitions d'état du jeu visible afin d'éviter les
 * régressions causées par des signaux locaux ou distants concurrents.
 */
namespace RA.Compagnon.Services;

/*
 * Orchestre l'état affiché du jeu courant à partir des sources locales,
 * de l'API et des événements de succès.
 */
public sealed class ServiceOrchestrateurEtatJeu
{
    private static readonly TimeSpan DelaiGraceDetectionFaible = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan DelaiGraceEtatValide = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan DelaiGraceSondeLocale = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan DelaiGraceJeuLocalResolut = TimeSpan.FromSeconds(25);
    private static readonly TimeSpan DelaiGraceNoticeCompteLocale = TimeSpan.FromMilliseconds(600);
    private static readonly TimeSpan DelaiDebounceSuccesLocal = TimeSpan.FromSeconds(2);

    private DateTimeOffset _horodatageDernierePresenceLocaleCompteValide;
    private DateTimeOffset _horodatageDerniereDetectionLocaleValide;
    private DateTimeOffset _horodatageDerniereResolutionJeuLocalValide;
    private DateTimeOffset _horodatageDernierSignalSuccesLocalUtc;
    private string _signatureDernierEtatRichPresence = string.Empty;
    private string _signatureDerniereSondeLocale = string.Empty;
    private string _signatureDerniereResolutionLocale = string.Empty;

    public EtatOrchestrateurJeu EtatCourant { get; private set; } = EtatOrchestrateurJeu.Initial;

    /*
     * Réinitialise l'état courant et tout le contexte de grâce associé.
     */
    public void Reinitialiser()
    {
        EtatCourant = EtatOrchestrateurJeu.Initial;
        ReinitialiserContexteLocal();
    }

    /*
     * Efface tous les horodatages et signatures utilisés pour arbitrer les
     * transitions d'état.
     */
    public void ReinitialiserContexteLocal()
    {
        _horodatageDernierePresenceLocaleCompteValide = DateTimeOffset.MinValue;
        _horodatageDerniereDetectionLocaleValide = DateTimeOffset.MinValue;
        _horodatageDerniereResolutionJeuLocalValide = DateTimeOffset.MinValue;
        _horodatageDernierSignalSuccesLocalUtc = DateTimeOffset.MinValue;
        _signatureDernierEtatRichPresence = string.Empty;
        _signatureDerniereSondeLocale = string.Empty;
        _signatureDerniereResolutionLocale = string.Empty;
    }

    /*
     * Mémorise qu'une présence locale de compte a été observée récemment.
     */
    public void EnregistrerPresenceLocaleCompte(bool presenceActive)
    {
        if (presenceActive)
        {
            _horodatageDernierePresenceLocaleCompteValide = DateTimeOffset.UtcNow;
        }
    }

    /*
     * Détermine si la notice de présence locale doit rester visible grâce au
     * dernier horodatage valide connu.
     */
    public bool EtatLocalEmulateurEstActifPourNotice(bool presenceLocaleCompteActive)
    {
        if (presenceLocaleCompteActive)
        {
            return true;
        }

        if (_horodatageDernierePresenceLocaleCompteValide == DateTimeOffset.MinValue)
        {
            return false;
        }

        return DateTimeOffset.UtcNow - _horodatageDernierePresenceLocaleCompteValide
            <= DelaiGraceNoticeCompteLocale;
    }

    /*
     * Mémorise une détection locale valide d'émulateur.
     */
    public void EnregistrerDetectionLocaleValide()
    {
        _horodatageDerniereDetectionLocaleValide = DateTimeOffset.UtcNow;
    }

    /*
     * Indique si la dernière sonde locale doit encore être considérée comme
     * exploitable pendant sa fenêtre de grâce.
     */
    public bool SondeLocaleEstEncoreValide()
    {
        if (_horodatageDerniereDetectionLocaleValide == DateTimeOffset.MinValue)
        {
            return false;
        }

        return DateTimeOffset.UtcNow - _horodatageDerniereDetectionLocaleValide
            <= DelaiGraceSondeLocale;
    }

    /*
     * Mémorise qu'une résolution locale de jeu a été confirmée.
     */
    public void EnregistrerResolutionJeuLocalValide()
    {
        _horodatageDerniereResolutionJeuLocalValide = DateTimeOffset.UtcNow;
    }

    /*
     * Détermine si un jeu local doit rester considéré comme actif malgré les
     * fluctuations momentanées de détection.
     */
    public bool EtatLocalJeuEstActif(int identifiantJeuLocalActif, bool emulateurDetecte)
    {
        if (identifiantJeuLocalActif <= 0)
        {
            return false;
        }

        if (emulateurDetecte)
        {
            return true;
        }

        if (_horodatageDerniereDetectionLocaleValide == DateTimeOffset.MinValue)
        {
            return DateTimeOffset.UtcNow - _horodatageDerniereResolutionJeuLocalValide
                <= DelaiGraceJeuLocalResolut;
        }

        if (SondeLocaleEstEncoreValide())
        {
            return true;
        }

        return DateTimeOffset.UtcNow - _horodatageDerniereResolutionJeuLocalValide
            <= DelaiGraceJeuLocalResolut;
    }

    /*
     * Indique si un signal local de succès doit être ignoré à cause du délai
     * d'antirebond encore actif.
     */
    public bool DoitIgnorerSignalSuccesLocal()
    {
        if (_horodatageDernierSignalSuccesLocalUtc == DateTimeOffset.MinValue)
        {
            return false;
        }

        return DateTimeOffset.UtcNow - _horodatageDernierSignalSuccesLocalUtc
            < DelaiDebounceSuccesLocal;
    }

    /*
     * Enregistre le dernier instant où un signal local de succès a été traité.
     */
    public void EnregistrerSignalSuccesLocal()
    {
        _horodatageDernierSignalSuccesLocalUtc = DateTimeOffset.UtcNow;
    }

    /*
     * Vérifie si un nouvel état Rich Presence mérite réellement un traitement.
     */
    public bool DoitTraiterEtatRichPresence(string signatureEtat)
    {
        if (
            string.Equals(
                _signatureDernierEtatRichPresence,
                signatureEtat,
                StringComparison.Ordinal
            )
        )
        {
            return false;
        }

        _signatureDernierEtatRichPresence = signatureEtat;
        return true;
    }

    /*
     * Détermine si une nouvelle sonde locale diffère assez de la précédente
     * pour justifier un traitement.
     */
    public bool DoitTraiterSondeLocale(string signatureSonde)
    {
        if (string.Equals(_signatureDerniereSondeLocale, signatureSonde, StringComparison.Ordinal))
        {
            return false;
        }

        _signatureDerniereSondeLocale = signatureSonde;
        return true;
    }

    /*
     * Détermine si une résolution locale de jeu doit être traitée ou si elle
     * duplique un état déjà connu.
     */
    public bool DoitTraiterResolutionLocale(
        string signatureResolution,
        int identifiantJeu,
        int identifiantJeuApiCourant,
        int identifiantJeuLocalCourant
    )
    {
        if (
            !string.Equals(
                _signatureDerniereResolutionLocale,
                signatureResolution,
                StringComparison.Ordinal
            )
        )
        {
            _signatureDerniereResolutionLocale = signatureResolution;
            return true;
        }

        return identifiantJeuApiCourant != identifiantJeu
            || identifiantJeuLocalCourant != identifiantJeu;
    }

    /*
     * Efface la dernière signature de résolution locale mémorisée.
     */
    public void OublierResolutionLocale()
    {
        _signatureDerniereResolutionLocale = string.Empty;
    }

    /*
     * Enregistre une transition vers l'absence de jeu affichable.
     */
    public bool EnregistrerAucunJeu(string source)
    {
        return AppliquerTransition(PhaseOrchestrateurJeu.AucunJeu, 0, string.Empty, source);
    }

    /*
     * Enregistre une phase de détection locale, faible ou confirmée.
     */
    public bool EnregistrerDetectionLocale(int identifiantJeu, string titreJeu, string source)
    {
        PhaseOrchestrateurJeu phaseCible =
            identifiantJeu > 0
                ? PhaseOrchestrateurJeu.JeuLocalConfirme
                : PhaseOrchestrateurJeu.DetectionLocale;

        return AppliquerTransition(phaseCible, identifiantJeu, titreJeu, source);
    }

    /*
     * Enregistre qu'un chargement API pour un jeu est en cours.
     */
    public bool EnregistrerChargement(int identifiantJeu, string titreJeu, string source)
    {
        return AppliquerTransition(
            PhaseOrchestrateurJeu.ChargementApi,
            identifiantJeu,
            titreJeu,
            source
        );
    }

    /*
     * Enregistre qu'un jeu complet est désormais affiché dans l'interface.
     */
    public bool EnregistrerJeuAffiche(int identifiantJeu, string titreJeu, string source)
    {
        return AppliquerTransition(
            PhaseOrchestrateurJeu.JeuAffiche,
            identifiantJeu,
            titreJeu,
            source
        );
    }

    /*
     * Enregistre un échec de chargement pour un jeu donné.
     */
    public bool EnregistrerErreurChargement(int identifiantJeu, string titreJeu, string source)
    {
        return AppliquerTransition(
            PhaseOrchestrateurJeu.ErreurChargement,
            identifiantJeu,
            titreJeu,
            source
        );
    }

    /*
     * Détermine si un état local provisoire peut être affiché sans écraser un
     * état plus fiable ou plus récent.
     */
    public bool PeutAfficherEtatLocalProvisoire(int identifiantJeuProbable)
    {
        if (identifiantJeuProbable <= 0)
        {
            return !DoitIgnorerDetectionLocaleFaible();
        }

        if (!EtatCourant.ConcerneJeu(identifiantJeuProbable))
        {
            return true;
        }

        return EtatCourant.Phase < PhaseOrchestrateurJeu.JeuLocalConfirme;
    }

    /*
     * Indique si le même jeu est déjà en cours d'affichage ou de chargement.
     */
    public bool MemeJeuAfficheOuEnChargement(int identifiantJeu)
    {
        if (!EtatCourant.ConcerneJeu(identifiantJeu))
        {
            return false;
        }

        return EtatCourant.Phase
            is PhaseOrchestrateurJeu.ChargementApi
                or PhaseOrchestrateurJeu.JeuAffiche;
    }

    /*
     * Détermine si une résolution locale doit être ignorée parce qu'un état
     * déjà plus avancé existe pour ce même jeu.
     */
    public bool DoitIgnorerResolutionLocale(int identifiantJeu)
    {
        if (!EtatCourant.ConcerneJeu(identifiantJeu))
        {
            return false;
        }

        return EtatCourant.Phase >= PhaseOrchestrateurJeu.JeuLocalConfirme;
    }

    /*
     * Indique si un chargement API serait redondant pour le jeu visé.
     */
    public bool DoitIgnorerChargementApi(int identifiantJeu, bool forcerChargementJeu)
    {
        if (forcerChargementJeu || !EtatCourant.ConcerneJeu(identifiantJeu))
        {
            return false;
        }

        return EtatCourant.Phase >= PhaseOrchestrateurJeu.ChargementApi;
    }

    /*
     * Indique si l'état courant fait partie des états transitoires encore
     * acceptables à afficher à l'utilisateur.
     */
    public bool EtatTransitoireEstAffichable()
    {
        return EtatCourant.Phase
            is PhaseOrchestrateurJeu.DetectionLocale
                or PhaseOrchestrateurJeu.JeuLocalConfirme
                or PhaseOrchestrateurJeu.ChargementApi
                or PhaseOrchestrateurJeu.AucunJeu
                or PhaseOrchestrateurJeu.ErreurChargement;
    }

    /*
     * Retourne le texte lisible associé à l'état courant lorsqu'il représente
     * une phase transitoire ou une absence de jeu.
     */
    public string ObtenirTexteEtatAffichable()
    {
        return EtatCourant.Phase switch
        {
            PhaseOrchestrateurJeu.AucunJeu => ObtenirTexteAucunJeu(),
            PhaseOrchestrateurJeu.DetectionLocale => "Detection locale en cours...",
            PhaseOrchestrateurJeu.JeuLocalConfirme => "Confirmation du jeu local...",
            PhaseOrchestrateurJeu.ChargementApi => "Chargement de la progression...",
            PhaseOrchestrateurJeu.ErreurChargement =>
                "Impossible de charger la progression du jeu.",
            _ => string.Empty,
        };
    }

    /*
     * Détermine si une détection locale faible doit être ignorée au profit
     * d'un état valide encore récent.
     */
    private bool DoitIgnorerDetectionLocaleFaible()
    {
        if (!EtatCourantRepresenteUnJeuValide())
        {
            return false;
        }

        return EtatCourantEstRecent(DelaiGraceDetectionFaible);
    }

    /*
     * Détermine si une transition doit être refusée pour conserver un état
     * déjà valide et encore récent.
     */
    private bool DoitConserverEtatValideFaceATransition(
        PhaseOrchestrateurJeu phase,
        int identifiantJeu,
        string source
    )
    {
        if (string.Equals(source, "non_configure", StringComparison.Ordinal))
        {
            return false;
        }

        if (!EtatCourantRepresenteUnJeuValide() || !EtatCourantEstRecent(DelaiGraceEtatValide))
        {
            return false;
        }

        if (identifiantJeu > 0 && identifiantJeu != EtatCourant.IdentifiantJeu)
        {
            return false;
        }

        if (phase == PhaseOrchestrateurJeu.DetectionLocale && identifiantJeu <= 0)
        {
            return true;
        }

        if (phase is PhaseOrchestrateurJeu.AucunJeu or PhaseOrchestrateurJeu.ErreurChargement)
        {
            return identifiantJeu <= 0 || identifiantJeu == EtatCourant.IdentifiantJeu;
        }

        if (
            identifiantJeu > 0
            && identifiantJeu == EtatCourant.IdentifiantJeu
            && phase <= EtatCourant.Phase
            && ObtenirPrioriteSource(source) < ObtenirPrioriteSource(EtatCourant.Source)
        )
        {
            return true;
        }

        return false;
    }

    /*
     * Indique si l'état courant représente déjà un jeu valable dans une phase
     * suffisamment solide.
     */
    private bool EtatCourantRepresenteUnJeuValide()
    {
        if (EtatCourant.IdentifiantJeu <= 0)
        {
            return false;
        }

        return EtatCourant.Phase
            is PhaseOrchestrateurJeu.JeuLocalConfirme
                or PhaseOrchestrateurJeu.ChargementApi
                or PhaseOrchestrateurJeu.JeuAffiche;
    }

    /*
     * Vérifie si l'état courant se situe encore dans une fenêtre de récence.
     */
    private bool EtatCourantEstRecent(TimeSpan delai)
    {
        if (EtatCourant.HorodatageUtc == DateTimeOffset.MinValue)
        {
            return false;
        }

        return DateTimeOffset.UtcNow - EtatCourant.HorodatageUtc <= delai;
    }

    /*
     * Retourne une priorité relative de source pour arbitrer les transitions
     * concurrentes d'un même jeu.
     */
    private static int ObtenirPrioriteSource(string source)
    {
        return source switch
        {
            "progression" => 50,
            "api_local" => 45,
            "api" => 40,
            "local" => 30,
            "RetroArch" => 30,
            "RALibretro" => 30,
            "RAP64" => 30,
            "DuckStation" => 30,
            "profil_inaccessible" => 10,
            "api_background" => 10,
            "aucun_jeu_recent" => 10,
            "non_configure" => 100,
            _ => 20,
        };
    }

    /*
     * Retourne le texte à afficher lorsqu'aucun jeu n'est disponible.
     */
    private string ObtenirTexteAucunJeu()
    {
        return EtatCourant.Source switch
        {
            "non_configure" => "Connecte ton compte pour afficher ton activite.",
            "aucun_jeu_recent" => "Aucun jeu recent a afficher.",
            _ => string.Empty,
        };
    }

    /*
     * Applique la transition d'état demandée si elle n'est ni redondante ni
     * moins fiable que l'état courant.
     */
    private bool AppliquerTransition(
        PhaseOrchestrateurJeu phase,
        int identifiantJeu,
        string titreJeu,
        string source
    )
    {
        string titreNettoye = titreJeu?.Trim() ?? string.Empty;
        string sourceNettoyee = source?.Trim() ?? string.Empty;

        if (phase == PhaseOrchestrateurJeu.DetectionLocale && identifiantJeu <= 0)
        {
            if (DoitIgnorerDetectionLocaleFaible())
            {
                return false;
            }
        }

        if (DoitConserverEtatValideFaceATransition(phase, identifiantJeu, sourceNettoyee))
        {
            return false;
        }

        if (phase == PhaseOrchestrateurJeu.AucunJeu)
        {
            EtatCourant = new EtatOrchestrateurJeu(
                PhaseOrchestrateurJeu.AucunJeu,
                0,
                string.Empty,
                sourceNettoyee,
                DateTimeOffset.UtcNow
            );
            return true;
        }

        if (identifiantJeu > 0 && EtatCourant.ConcerneJeu(identifiantJeu))
        {
            if (phase < EtatCourant.Phase)
            {
                return false;
            }

            if (
                phase == EtatCourant.Phase
                && ObtenirPrioriteSource(sourceNettoyee) < ObtenirPrioriteSource(EtatCourant.Source)
            )
            {
                return false;
            }

            EtatCourant = new EtatOrchestrateurJeu(
                phase,
                identifiantJeu,
                string.IsNullOrWhiteSpace(titreNettoye) ? EtatCourant.TitreJeu : titreNettoye,
                string.IsNullOrWhiteSpace(sourceNettoyee) ? EtatCourant.Source : sourceNettoyee,
                DateTimeOffset.UtcNow
            );
            return true;
        }

        EtatCourant = new EtatOrchestrateurJeu(
            phase,
            identifiantJeu,
            titreNettoye,
            sourceNettoyee,
            DateTimeOffset.UtcNow
        );
        return true;
    }
}
