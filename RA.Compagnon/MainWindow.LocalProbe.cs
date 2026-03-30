using RA.Compagnon.Modeles.Api.V2.Game;
using RA.Compagnon.Modeles.Api.V2.User;
using RA.Compagnon.Modeles.Catalogue;
using RA.Compagnon.Modeles.Local;
using RA.Compagnon.Modeles.Presentation;
using RA.Compagnon.Services;
using System.Windows.Threading;

namespace RA.Compagnon;

public partial class MainWindow
{
    private static readonly TimeSpan DureeGraceSondeLocale = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan DureeGraceJeuLocalResolut = TimeSpan.FromSeconds(25);
    private static readonly TimeSpan DureeGraceNoticeCompteLocale = TimeSpan.FromMilliseconds(600);

    private bool EtatLocalEmulateurEstActifPourNotice()
    {
        if (_presenceLocaleCompteActive)
        {
            return true;
        }

        if (_horodatageDernierePresenceLocaleCompteValide == DateTimeOffset.MinValue)
        {
            return false;
        }

        return DateTimeOffset.UtcNow - _horodatageDernierePresenceLocaleCompteValide
            <= DureeGraceNoticeCompteLocale;
    }

    private bool EtatLocalJeuEstActif()
    {
        if (_identifiantJeuLocalActif <= 0)
        {
            return false;
        }

        if (_dernierEtatSondeLocaleEmulateurs?.EmulateurDetecte == true)
        {
            return true;
        }

        if (_horodatageDerniereDetectionLocaleValide == DateTimeOffset.MinValue)
        {
            return DateTimeOffset.UtcNow - _horodatageDerniereResolutionJeuLocalValide
                <= DureeGraceJeuLocalResolut;
        }

        if (
            DateTimeOffset.UtcNow - _horodatageDerniereDetectionLocaleValide
            <= DureeGraceSondeLocale
        )
        {
            return true;
        }

        return DateTimeOffset.UtcNow - _horodatageDerniereResolutionJeuLocalValide
            <= DureeGraceJeuLocalResolut;
    }

    /// <summary>
    /// Active les rafraîchissements API généraux ainsi que la surveillance légère du Rich Presence.
    /// </summary>
    private void DemarrerActualisationAutomatique()
    {
        if (!ConfigurationConnexionEstComplete())
        {
            return;
        }

        if (_profilUtilisateurAccessible && !_minuteurActualisationApi.IsEnabled)
        {
            _minuteurActualisationApi.Start();
        }

        if (_profilUtilisateurAccessible && !_minuteurActualisationRichPresence.IsEnabled)
        {
            _minuteurActualisationRichPresence.Start();
        }

        if (!_minuteurPresenceLocaleCompte.IsEnabled)
        {
            _minuteurPresenceLocaleCompte.Start();
        }

        if (!_minuteurSondeLocaleEmulateurs.IsEnabled)
        {
            _minuteurSondeLocaleEmulateurs.Start();
        }
    }

    /// <summary>
    /// Redémarre le minuteur API pour repousser le prochain tick après un rafraîchissement ciblé.
    /// </summary>
    private void RedemarrerMinuteurActualisationApi()
    {
        if (!_profilUtilisateurAccessible)
        {
            return;
        }

        _minuteurActualisationApi.Stop();
        _minuteurActualisationApi.Start();
    }

    /// <summary>
    /// Arrête les rafraîchissements périodiques.
    /// </summary>
    private void ArreterActualisationAutomatique()
    {
        _minuteurActualisationApi.Stop();
        _minuteurActualisationRichPresence.Stop();
        _minuteurPresenceLocaleCompte.Stop();
        _minuteurSondeLocaleEmulateurs.Stop();
        _minuteurRotationVisuelsJeuEnCours.Stop();
    }

    /// <summary>
    /// Aucun amorçage local : l'application reste entièrement autonome.
    /// </summary>
    private static Task AmorcerEtatJeuLocalAuDemarrageAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Réinitialise les derniers marqueurs utilisés pour éviter les rechargements API inutiles.
    /// </summary>
    private void ReinitialiserContexteSurveillance()
    {
        _actualisationApiCibleeEnAttente = false;
        _dernierIdentifiantJeuApi = 0;
        _dernierIdentifiantJeuAvecInfos = 0;
        _dernierIdentifiantJeuAvecProgression = 0;
        _dernierTitreJeuApi = string.Empty;
        _dernierePresenceRiche = string.Empty;
        _signatureDernierEtatRichPresence = string.Empty;
        _signatureDerniereSondeLocaleEmulateurs = string.Empty;
        _signatureDernierJeuLocalResolut = string.Empty;
        _signatureDerniereNoticeCompteJournalisee = string.Empty;
        _dernierPseudoCharge = string.Empty;
        _dernierProfilUtilisateurCharge = null;
        _dernierResumeUtilisateurCharge = null;
        _dernierEtatSondeLocaleEmulateurs = null;
        _presenceLocaleCompteActive = false;
        _horodatageDernierePresenceLocaleCompteValide = DateTimeOffset.MinValue;
        _horodatageDerniereDetectionLocaleValide = DateTimeOffset.MinValue;
        _horodatageDerniereResolutionJeuLocalValide = DateTimeOffset.MinValue;
        _horodatageDernierSignalSuccesLocalUtc = DateTimeOffset.MinValue;
        _signatureDernierSuccesLocalDirectAffiche = string.Empty;
        _identifiantJeuSuccesObserve = 0;
        _etatSuccesObserves = [];
        _identifiantJeuLocalResolutEnAttente = 0;
        _titreJeuLocalResolutEnAttente = string.Empty;
        _identifiantJeuLocalActif = 0;
        _titreJeuLocalActif = string.Empty;
        _consolesResolutionLocale = [];
        _serviceSurveillanceSuccesLocaux.ArreterSurveillance();
    }

    /// <summary>
    /// Surveille en continu l'état Rich Presence sans recharger tout le contenu principal.
    /// </summary>
    private async void ActualisationRichPresence_Tick(object? sender, EventArgs e)
    {
        if (
            !ConfigurationConnexionEstComplete()
            || !_profilUtilisateurAccessible
            || _surveillanceRichPresenceEnCours
        )
        {
            return;
        }

        _surveillanceRichPresenceEnCours = true;

        try
        {
            UserSummaryV2? resume = await ServiceUtilisateurRetroAchievements.ObtenirResumeAsync(
                _configurationConnexion.Pseudo,
                _configurationConnexion.CleApiWeb
            );

            if (resume is null)
            {
                return;
            }

            _dernierResumeUtilisateurCharge = resume;

            EtatRichPresence etat = ServiceSondeRichPresence.Sonder(
                new DonneesCompteUtilisateur
                {
                    Profil = _dernierProfilUtilisateurCharge,
                    Resume = resume,
                },
                journaliser: false
            );

            string signatureEtat =
                $"{etat.StatutAffiche}|{etat.SousStatutAffiche}|{etat.IdentifiantDernierJeu}|{etat.DatePresenceBrute}|{etat.SourceRichPresence}";

            if (
                string.Equals(
                    _signatureDernierEtatRichPresence,
                    signatureEtat,
                    StringComparison.Ordinal
                )
            )
            {
                return;
            }

            _signatureDernierEtatRichPresence = signatureEtat;
            _dernierePresenceRiche = etat.MessageRichPresence;
            MettreAJourNoticeCompteEntete();

            if (EtatLocalJeuEstActif() && etat.IdentifiantDernierJeu != _identifiantJeuLocalActif)
            {
                return;
            }

            if (
                etat.IdentifiantDernierJeu > 0
                && etat.IdentifiantDernierJeu != _dernierIdentifiantJeuApi
                && !_chargementJeuEnCoursActif
            )
            {
                await ChargerJeuEnCoursAsync(false, true);
            }
        }
        catch
        {
            // Une erreur ponctuelle de sonde ne doit pas interrompre la surveillance continue.
        }
        finally
        {
            _surveillanceRichPresenceEnCours = false;
        }
    }

    private async void ActualisationPresenceLocaleCompte_Tick(object? sender, EventArgs e)
    {
        if (_surveillancePresenceLocaleCompteEnCours)
        {
            return;
        }

        _surveillancePresenceLocaleCompteEnCours = true;

        try
        {
            bool presenceActive = await Task.Run(
                ServiceSondeLocaleEmulateurs.SonderPresenceEmulateur
            );

            if (presenceActive)
            {
                _horodatageDernierePresenceLocaleCompteValide = DateTimeOffset.UtcNow;
            }

            if (_presenceLocaleCompteActive != presenceActive)
            {
                _presenceLocaleCompteActive = presenceActive;
                MettreAJourNoticeCompteEntete();
            }
        }
        catch
        {
            // La notice doit rester robuste même si cette sonde légère échoue ponctuellement.
        }
        finally
        {
            _surveillancePresenceLocaleCompteEnCours = false;
        }
    }

    /// <summary>
    /// Surveille localement les émulateurs connus pour déclencher un rafraîchissement ciblé plus tôt.
    /// </summary>
    private async void ActualisationSondeLocaleEmulateurs_Tick(object? sender, EventArgs e)
    {
        if (_surveillanceLocaleEmulateursEnCours)
        {
            return;
        }

        _surveillanceLocaleEmulateursEnCours = true;

        try
        {
            EtatSondeLocaleEmulateur etatBrut = await Task.Run(() =>
                _serviceSondeLocaleEmulateurs.Sonder()
            );
            EtatSondeLocaleEmulateur etat = etatBrut;

            if (etatBrut.EmulateurDetecte)
            {
                _horodatageDerniereDetectionLocaleValide = DateTimeOffset.UtcNow;
                _dernierEtatSondeLocaleEmulateurs = etatBrut;
            }
            else if (
                _dernierEtatSondeLocaleEmulateurs?.EmulateurDetecte == true
                && DateTimeOffset.UtcNow - _horodatageDerniereDetectionLocaleValide
                    <= DureeGraceSondeLocale
            )
            {
                etat = _dernierEtatSondeLocaleEmulateurs;
            }
            else
            {
                _dernierEtatSondeLocaleEmulateurs = etatBrut;
            }

            _serviceSurveillanceSuccesLocaux.MettreAJourCible(
                etat.EmulateurDetecte ? etat : null
            );
            MettreAJourNoticeCompteEntete();

            if (
                string.Equals(
                    _signatureDerniereSondeLocaleEmulateurs,
                    etat.Signature,
                    StringComparison.Ordinal
                )
            )
            {
                return;
            }

            _signatureDerniereSondeLocaleEmulateurs = etat.Signature;

            if (
                !ConfigurationConnexionEstComplete()
                || !_profilUtilisateurAccessible
                || !etat.EmulateurDetecte
            )
            {
                _signatureDernierJeuLocalResolut = string.Empty;
                _identifiantJeuLocalActif = 0;
                _titreJeuLocalActif = string.Empty;
                _identifiantJeuLocalResolutEnAttente = 0;
                _titreJeuLocalResolutEnAttente = string.Empty;
                MettreAJourNoticeCompteEntete();
                return;
            }

            AppliquerTitreJeuLocalProvisoire(etat);

            if (etat.IdentifiantJeuProbable > 0)
            {
                string titreJeuRacache = string.IsNullOrWhiteSpace(etat.TitreJeuProbable)
                    ? $"Game ID {etat.IdentifiantJeuProbable}"
                    : etat.TitreJeuProbable;
                string signatureResolutionDirecte =
                    $"{etat.NomEmulateur}|{etat.IdentifiantJeuProbable}|racache_direct";

                if (
                    !string.Equals(
                        _signatureDernierJeuLocalResolut,
                        signatureResolutionDirecte,
                        StringComparison.Ordinal
                    )
                    || _identifiantJeuLocalActif != etat.IdentifiantJeuProbable
                )
                {
                    _signatureDernierJeuLocalResolut = signatureResolutionDirecte;
                    _identifiantJeuLocalActif = etat.IdentifiantJeuProbable;
                    _titreJeuLocalActif = titreJeuRacache;
                    _horodatageDerniereResolutionJeuLocalValide = DateTimeOffset.UtcNow;

                    if (_chargementJeuEnCoursActif)
                    {
                        _identifiantJeuLocalResolutEnAttente = etat.IdentifiantJeuProbable;
                        _titreJeuLocalResolutEnAttente = titreJeuRacache;
                        return;
                    }

                    ChargerJeuResolutLocal(etat.IdentifiantJeuProbable, titreJeuRacache);
                }

                return;
            }

            if (string.Equals(etat.NomEmulateur, "RALibretro", StringComparison.Ordinal))
            {
                // Pour RALibretro, le titre fenetre/JSON recent peut rester sur l'ancien jeu
                // pendant une transition. On attend donc le prochain Game ID RA au lieu
                // de lancer un matching par titre potentiellement faux.
                return;
            }

            JeuLocalResolut? jeuResolutImmediate =
                ServiceResolutionJeuLocal.ResoudreDepuisJeuxRecents(
                    etat.TitreJeuProbable,
                    _dernierResumeUtilisateurCharge?.RecentlyPlayed ?? []
                );

            if (jeuResolutImmediate is not null)
            {
                string signatureResolutionImmediate =
                    $"{etat.Signature}|{jeuResolutImmediate.IdentifiantJeu}|{jeuResolutImmediate.Source}";

                if (
                    !string.Equals(
                        _signatureDernierJeuLocalResolut,
                        signatureResolutionImmediate,
                        StringComparison.Ordinal
                    )
                    || _dernierIdentifiantJeuApi != jeuResolutImmediate.IdentifiantJeu
                )
                {
                    _signatureDernierJeuLocalResolut = signatureResolutionImmediate;
                    _identifiantJeuLocalActif = jeuResolutImmediate.IdentifiantJeu;
                    _titreJeuLocalActif = jeuResolutImmediate.TitreRetroAchievements;
                    _horodatageDerniereResolutionJeuLocalValide = DateTimeOffset.UtcNow;

                    if (_chargementJeuEnCoursActif)
                    {
                        _identifiantJeuLocalResolutEnAttente = jeuResolutImmediate.IdentifiantJeu;
                        _titreJeuLocalResolutEnAttente = jeuResolutImmediate.TitreRetroAchievements;
                        return;
                    }

                    ChargerJeuResolutLocal(
                        jeuResolutImmediate.IdentifiantJeu,
                        jeuResolutImmediate.TitreRetroAchievements
                    );
                }

                return;
            }

            JeuLocalResolut? jeuResolut = await ResoudreJeuLocalDepuisSondeAsync(etat);

            if (jeuResolut is not null)
            {
                string signatureResolution =
                    $"{etat.Signature}|{jeuResolut.IdentifiantJeu}|{jeuResolut.Source}";

                if (
                    string.Equals(
                        _signatureDernierJeuLocalResolut,
                        signatureResolution,
                        StringComparison.Ordinal
                    )
                    && _dernierIdentifiantJeuApi == jeuResolut.IdentifiantJeu
                )
                {
                    return;
                }

                _signatureDernierJeuLocalResolut = signatureResolution;
                _identifiantJeuLocalActif = jeuResolut.IdentifiantJeu;
                _titreJeuLocalActif = jeuResolut.TitreRetroAchievements;
                _horodatageDerniereResolutionJeuLocalValide = DateTimeOffset.UtcNow;

                if (_chargementJeuEnCoursActif)
                {
                    _identifiantJeuLocalResolutEnAttente = jeuResolut.IdentifiantJeu;
                    _titreJeuLocalResolutEnAttente = jeuResolut.TitreRetroAchievements;
                    return;
                }

                ChargerJeuResolutLocal(
                    jeuResolut.IdentifiantJeu,
                    jeuResolut.TitreRetroAchievements
                );
                return;
            }

            _signatureDernierJeuLocalResolut = string.Empty;
        }
        catch
        {
            // Une erreur locale ponctuelle ne doit pas casser la surveillance continue.
        }
        finally
        {
            _surveillanceLocaleEmulateursEnCours = false;
        }
    }

    private async Task<JeuLocalResolut?> ResoudreJeuLocalDepuisSondeAsync(
        EtatSondeLocaleEmulateur etat
    )
    {
        string titreJeuLocal = etat.TitreJeuProbable?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(titreJeuLocal))
        {
            return null;
        }

        IReadOnlyList<RecentlyPlayedGameV2> jeuxRecents =
            _dernierResumeUtilisateurCharge?.RecentlyPlayed ?? [];
        IReadOnlyList<int> identifiantsConsoleEmulateur =
            await ObtenirIdentifiantsConsoleCandidatsParEmulateurAsync(etat.NomEmulateur);
        HashSet<int> identifiantsConsoleCandidats =
        [
            .. jeuxRecents.Select(jeu => jeu.IdentifiantConsole),
            .. identifiantsConsoleEmulateur,
        ];

        int identifiantConsoleJeuAffiche =
            _dernieresDonneesJeuAffichees?.Jeu.IdentifiantConsole ?? 0;

        if (
            identifiantConsoleJeuAffiche > 0
            && (
                identifiantsConsoleEmulateur.Count == 0
                || identifiantsConsoleEmulateur.Contains(identifiantConsoleJeuAffiche)
            )
        )
        {
            identifiantsConsoleCandidats.Add(identifiantConsoleJeuAffiche);
        }

        IReadOnlyList<JeuCatalogueLocal> jeuxCatalogueLocal =
            await _serviceCatalogueJeuxLocal.ObtenirJeuxAsync(identifiantsConsoleCandidats);
        JeuLocalResolut? resolutionCatalogueLocal =
            ServiceResolutionJeuLocal.ResoudreDepuisCatalogueLocal(
                titreJeuLocal,
                jeuxCatalogueLocal,
                identifiantsConsoleCandidats
            );

        if (resolutionCatalogueLocal is not null)
        {
            return resolutionCatalogueLocal;
        }

        if (jeuxRecents.Count == 0)
        {
            try
            {
                jeuxRecents =
                    await ServiceUtilisateurRetroAchievements.ObtenirJeuxRecemmentJouesAsync(
                        _configurationConnexion.Pseudo,
                        _configurationConnexion.CleApiWeb
                    );
            }
            catch
            {
                jeuxRecents = [];
            }
        }

        foreach (int identifiantConsole in jeuxRecents.Select(jeu => jeu.IdentifiantConsole))
        {
            identifiantsConsoleCandidats.Add(identifiantConsole);
        }

        ServiceResolutionJeuLocal.JournaliserEvenementInterface(
            "resolution_locale_contexte",
            $"emulateur={etat.NomEmulateur};titreLocal={titreJeuLocal};jeuxRecents={jeuxRecents.Count};consoles={string.Join(",", identifiantsConsoleCandidats.OrderBy(id => id))}"
        );

        return await ServiceResolutionJeuLocal.ResoudreAsync(
            titreJeuLocal,
            jeuxRecents,
            identifiantsConsoleCandidats,
            jeuxCatalogueLocal,
            (identifiantConsole, jetonAnnulation) =>
                _serviceCatalogueRetroAchievements.ObtenirJeuxSystemeAvecHashesAsync(
                    _configurationConnexion.CleApiWeb,
                    identifiantConsole,
                    jetonAnnulation
                )
        );
    }

    private async Task<IReadOnlyList<int>> ObtenirIdentifiantsConsoleCandidatsParEmulateurAsync(
        string nomEmulateur
    )
    {
        string[] aliasConsoles = ObtenirAliasConsolesDepuisEmulateur(nomEmulateur);

        if (aliasConsoles.Length == 0)
        {
            return [];
        }

        if (_consolesResolutionLocale.Count == 0)
        {
            try
            {
                _consolesResolutionLocale =
                    await _serviceCatalogueRetroAchievements.ObtenirConsolesAsync(
                        _configurationConnexion.CleApiWeb
                    );
            }
            catch
            {
                return [];
            }
        }

        List<int> identifiants = [];

        foreach (ConsoleV2 console in _consolesResolutionLocale)
        {
            string nomNormalise = NormaliserNomConsole(console.Name);

            if (string.IsNullOrWhiteSpace(nomNormalise))
            {
                continue;
            }

            if (
                aliasConsoles.Any(alias =>
                {
                    string aliasNormalise = NormaliserNomConsole(alias);
                    return !string.IsNullOrWhiteSpace(aliasNormalise)
                        && (
                            nomNormalise.Contains(aliasNormalise, StringComparison.Ordinal)
                            || aliasNormalise.Contains(nomNormalise, StringComparison.Ordinal)
                        );
                })
            )
            {
                identifiants.Add(console.Id);
            }
        }

        return [.. identifiants.Distinct()];
    }

    private static string[] ObtenirAliasConsolesDepuisEmulateur(string nomEmulateur)
    {
        if (string.IsNullOrWhiteSpace(nomEmulateur))
        {
            return [];
        }

        string nomEmulateurNormalise = nomEmulateur.Trim().ToLowerInvariant();

        return nomEmulateurNormalise switch
        {
            "flycast" => ["dreamcast", "naomi", "atomiswave"],
            "duckstation" => ["playstation", "sony playstation", "ps1", "psx", "ps one"],
            "pcsx2" => ["playstation 2", "ps2"],
            "ppsspp" => ["playstation portable", "psp"],
            "dolphin" => ["gamecube", "nintendo gamecube", "wii", "nintendo wii", "wiiware"],
            "lunaproject64" => ["nintendo 64", "n64"],
            _ => [],
        };
    }

    private static string NormaliserNomConsole(string valeur)
    {
        if (string.IsNullOrWhiteSpace(valeur))
        {
            return string.Empty;
        }

        return string.Concat(
            valeur.Trim().ToLowerInvariant().Where(caractere => char.IsLetterOrDigit(caractere))
        );
    }

    private void SurveillanceSuccesLocaux_SignalRecu(SignalSuccesLocal signal)
    {
        ServiceSurveillanceSuccesLocaux.JournaliserEvenement(
            "signal_dispatch_ui",
            $"emulateur={signal.NomEmulateur};source={signal.TypeSource};chemin={signal.Chemin}"
        );
        _ = Dispatcher.InvokeAsync(
            async () => await TraiterSignalSuccesLocalAsync(signal),
            DispatcherPriority.Background
        );
    }

    private async Task TraiterSignalSuccesLocalAsync(SignalSuccesLocal signal)
    {
        if (!ConfigurationConnexionEstComplete() || !_profilUtilisateurAccessible)
        {
            ServiceSurveillanceSuccesLocaux.JournaliserEvenement(
                "signal_ignore",
                "raison=configuration_incomplete_ou_profil_inaccessible"
            );
            return;
        }

        if (
            _dernierEtatSondeLocaleEmulateurs is not { EmulateurDetecte: true } etat
            || !string.Equals(etat.NomEmulateur, signal.NomEmulateur, StringComparison.Ordinal)
        )
        {
            ServiceSurveillanceSuccesLocaux.JournaliserEvenement(
                "signal_ignore",
                $"raison=emulateur_non_aligne;attendu={_dernierEtatSondeLocaleEmulateurs?.NomEmulateur ?? string.Empty};recu={signal.NomEmulateur}"
            );
            return;
        }

        if (!EtatLocalJeuEstActif())
        {
            ServiceSurveillanceSuccesLocaux.JournaliserEvenement(
                "signal_ignore",
                $"raison=jeu_local_inactif;emulateur={signal.NomEmulateur}"
            );
            return;
        }

        if (await TenterAfficherSuccesLocalDirectAsync(signal))
        {
            return;
        }

        if (
            DateTimeOffset.UtcNow - _horodatageDernierSignalSuccesLocalUtc
            < DureeMinimaleEntreSignauxSuccesLocaux
        )
        {
            ServiceSurveillanceSuccesLocaux.JournaliserEvenement(
                "signal_ignore",
                $"raison=debounce;emulateur={signal.NomEmulateur}"
            );
            return;
        }

        _horodatageDernierSignalSuccesLocalUtc = DateTimeOffset.UtcNow;

        if (_chargementJeuEnCoursActif)
        {
            _actualisationApiCibleeEnAttente = true;
            ServiceSurveillanceSuccesLocaux.JournaliserEvenement(
                "signal_refresh_differe",
                $"emulateur={signal.NomEmulateur};source={signal.TypeSource};chemin={signal.Chemin}"
            );
            return;
        }

        ServiceSurveillanceSuccesLocaux.JournaliserEvenement(
            "signal_refresh_immediat",
            $"emulateur={signal.NomEmulateur};source={signal.TypeSource};chemin={signal.Chemin}"
        );
        await ChargerJeuEnCoursAsync(false, true);
        RedemarrerMinuteurActualisationApi();
    }

    private async Task<bool> TenterAfficherSuccesLocalDirectAsync(SignalSuccesLocal signal)
    {
        if (
            !EstEmulateurSuccesLocalDirectPrisEnCharge(signal.NomEmulateur)
            || !TypeSourcePeutPorterSuccesDirect(signal)
        )
        {
            return false;
        }

        SuccesDebloqueDetecte? succesDirect =
            ServiceSondeLocaleEmulateurs.LireDernierSuccesDebloqueDepuisSourceLocale(
                signal.NomEmulateur,
                _identifiantJeuLocalActif > 0 ? _identifiantJeuLocalActif : _identifiantJeuSuccesCourant,
                !string.IsNullOrWhiteSpace(_titreJeuLocalActif)
                    ? _titreJeuLocalActif
                    : _dernieresDonneesJeuAffichees?.Jeu.Title ?? string.Empty,
                _succesJeuCourant
            );

        if (succesDirect is null)
        {
            return false;
        }

        string signatureSucces =
            $"{succesDirect.IdentifiantJeu}|{succesDirect.IdentifiantSucces}|{succesDirect.TitreSucces}";

        if (
            string.Equals(
                _signatureDernierSuccesLocalDirectAffiche,
                signatureSucces,
                StringComparison.Ordinal
            )
        )
        {
            ServiceSurveillanceSuccesLocaux.JournaliserEvenement(
                "signal_succes_direct_ignore",
                $"raison=deja_affiche;emulateur={signal.NomEmulateur};succes={succesDirect.IdentifiantSucces}"
            );
            return false;
        }

        string sourceDetectionDirecte = $"{signal.NomEmulateur.ToLowerInvariant()}_log";
        ServiceDetectionSuccesJeu.JournaliserDetection(succesDirect, sourceDetectionDirecte);
        MarquerSuccesCommeTraite(succesDirect);
        ServiceSurveillanceSuccesLocaux.JournaliserEvenement(
            "signal_succes_direct_detecte",
            $"emulateur={signal.NomEmulateur};source={signal.TypeSource};jeu={succesDirect.IdentifiantJeu};succes={succesDirect.IdentifiantSucces}"
        );
        bool affiche = await AfficherSuccesDebloqueDetecteAsync(succesDirect);

        if (!affiche)
        {
            ServiceSurveillanceSuccesLocaux.JournaliserEvenement(
                "signal_succes_direct_echec_ui",
                $"emulateur={signal.NomEmulateur};source={signal.TypeSource};jeu={succesDirect.IdentifiantJeu};succes={succesDirect.IdentifiantSucces}"
            );
            return false;
        }

        _signatureDernierSuccesLocalDirectAffiche = signatureSucces;
        ServiceSurveillanceSuccesLocaux.JournaliserEvenement(
            "signal_succes_direct_affiche",
            $"emulateur={signal.NomEmulateur};source={signal.TypeSource};jeu={succesDirect.IdentifiantJeu};succes={succesDirect.IdentifiantSucces}"
        );
        return true;
    }

    private static bool EstEmulateurSuccesLocalDirectPrisEnCharge(string nomEmulateur)
    {
        return string.Equals(nomEmulateur, "RALibretro", StringComparison.Ordinal)
            || string.Equals(nomEmulateur, "LunaProject64", StringComparison.Ordinal)
            || string.Equals(nomEmulateur, "RetroArch", StringComparison.Ordinal);
    }

    private static bool TypeSourcePeutPorterSuccesDirect(SignalSuccesLocal signal)
    {
        return signal.NomEmulateur switch
        {
            "RetroArch" => signal.TypeSource.Contains("logs", StringComparison.Ordinal),
            "LunaProject64" => signal.TypeSource.Contains("racache", StringComparison.Ordinal),
            "RALibretro" => signal.TypeSource.Contains("racache", StringComparison.Ordinal),
            _ => false,
        };
    }
}
