using RA.Compagnon.Modeles.Etat;

namespace RA.Compagnon.Services;

/// <summary>
/// Centralise les transitions principales du flux de changement de jeu afin
/// d'eviter les regressions visuelles entre detection locale, chargement et affichage.
/// </summary>
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

    public void Reinitialiser()
    {
        EtatCourant = EtatOrchestrateurJeu.Initial;
        ReinitialiserContexteLocal();
    }

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

    public void EnregistrerPresenceLocaleCompte(bool presenceActive)
    {
        if (presenceActive)
        {
            _horodatageDernierePresenceLocaleCompteValide = DateTimeOffset.UtcNow;
        }
    }

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

    public void EnregistrerDetectionLocaleValide()
    {
        _horodatageDerniereDetectionLocaleValide = DateTimeOffset.UtcNow;
    }

    public bool SondeLocaleEstEncoreValide()
    {
        if (_horodatageDerniereDetectionLocaleValide == DateTimeOffset.MinValue)
        {
            return false;
        }

        return DateTimeOffset.UtcNow - _horodatageDerniereDetectionLocaleValide
            <= DelaiGraceSondeLocale;
    }

    public void EnregistrerResolutionJeuLocalValide()
    {
        _horodatageDerniereResolutionJeuLocalValide = DateTimeOffset.UtcNow;
    }

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

    public bool DoitIgnorerSignalSuccesLocal()
    {
        if (_horodatageDernierSignalSuccesLocalUtc == DateTimeOffset.MinValue)
        {
            return false;
        }

        return DateTimeOffset.UtcNow - _horodatageDernierSignalSuccesLocalUtc
            < DelaiDebounceSuccesLocal;
    }

    public void EnregistrerSignalSuccesLocal()
    {
        _horodatageDernierSignalSuccesLocalUtc = DateTimeOffset.UtcNow;
    }

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

    public bool DoitTraiterSondeLocale(string signatureSonde)
    {
        if (string.Equals(_signatureDerniereSondeLocale, signatureSonde, StringComparison.Ordinal))
        {
            return false;
        }

        _signatureDerniereSondeLocale = signatureSonde;
        return true;
    }

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

    public void OublierResolutionLocale()
    {
        _signatureDerniereResolutionLocale = string.Empty;
    }

    public bool EnregistrerAucunJeu(string source)
    {
        return AppliquerTransition(PhaseOrchestrateurJeu.AucunJeu, 0, string.Empty, source);
    }

    public bool EnregistrerDetectionLocale(int identifiantJeu, string titreJeu, string source)
    {
        PhaseOrchestrateurJeu phaseCible =
            identifiantJeu > 0
                ? PhaseOrchestrateurJeu.JeuLocalConfirme
                : PhaseOrchestrateurJeu.DetectionLocale;

        return AppliquerTransition(phaseCible, identifiantJeu, titreJeu, source);
    }

    public bool EnregistrerChargement(int identifiantJeu, string titreJeu, string source)
    {
        return AppliquerTransition(
            PhaseOrchestrateurJeu.ChargementApi,
            identifiantJeu,
            titreJeu,
            source
        );
    }

    public bool EnregistrerJeuAffiche(int identifiantJeu, string titreJeu, string source)
    {
        return AppliquerTransition(
            PhaseOrchestrateurJeu.JeuAffiche,
            identifiantJeu,
            titreJeu,
            source
        );
    }

    public bool EnregistrerErreurChargement(int identifiantJeu, string titreJeu, string source)
    {
        return AppliquerTransition(
            PhaseOrchestrateurJeu.ErreurChargement,
            identifiantJeu,
            titreJeu,
            source
        );
    }

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

    public bool DoitIgnorerResolutionLocale(int identifiantJeu)
    {
        if (!EtatCourant.ConcerneJeu(identifiantJeu))
        {
            return false;
        }

        return EtatCourant.Phase >= PhaseOrchestrateurJeu.JeuLocalConfirme;
    }

    public bool DoitIgnorerChargementApi(int identifiantJeu, bool forcerChargementJeu)
    {
        if (forcerChargementJeu || !EtatCourant.ConcerneJeu(identifiantJeu))
        {
            return false;
        }

        return EtatCourant.Phase >= PhaseOrchestrateurJeu.ChargementApi;
    }

    public bool EtatTransitoireEstAffichable()
    {
        return EtatCourant.Phase
            is PhaseOrchestrateurJeu.DetectionLocale
                or PhaseOrchestrateurJeu.JeuLocalConfirme
                or PhaseOrchestrateurJeu.ChargementApi
                or PhaseOrchestrateurJeu.AucunJeu
                or PhaseOrchestrateurJeu.ErreurChargement;
    }

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

    private bool DoitIgnorerDetectionLocaleFaible()
    {
        if (!EtatCourantRepresenteUnJeuValide())
        {
            return false;
        }

        return EtatCourantEstRecent(DelaiGraceDetectionFaible);
    }

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

    private bool EtatCourantEstRecent(TimeSpan delai)
    {
        if (EtatCourant.HorodatageUtc == DateTimeOffset.MinValue)
        {
            return false;
        }

        return DateTimeOffset.UtcNow - EtatCourant.HorodatageUtc <= delai;
    }

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

    private string ObtenirTexteAucunJeu()
    {
        return EtatCourant.Source switch
        {
            "non_configure" => "Connecte ton compte pour afficher ton activite.",
            "aucun_jeu_recent" => "Aucun jeu recent a afficher.",
            _ => string.Empty,
        };
    }

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
