using System.IO;
using System.Windows.Threading;
using RA.Compagnon.Modeles.Api.V2.Game;
using RA.Compagnon.Modeles.Api.V2.User;
using RA.Compagnon.Modeles.Catalogue;
using RA.Compagnon.Modeles.Local;
using RA.Compagnon.Modeles.Presentation;
using RA.Compagnon.Services;

/*
 * Regroupe la surveillance locale des émulateurs, la résolution de jeu et
 * la détection des succès débloqués issus des sources locales.
 */
namespace RA.Compagnon;

/*
 * Porte la logique de sondes locales et de synchronisation fine entre
 * l'état détecté des émulateurs et l'interface utilisateur.
 */
public partial class MainWindow
{
    /*
     * Indique si un émulateur local doit faire apparaître la notice de
     * présence du compte dans l'en-tête.
     */
    private bool EtatLocalEmulateurEstActifPourNotice()
    {
        return _serviceOrchestrateurEtatJeu.EtatLocalEmulateurEstActifPourNotice(
            _presenceLocaleCompteActive
        );
    }

    /*
     * Indique si un jeu local est actuellement considéré comme actif.
     */
    private bool EtatLocalJeuEstActif()
    {
        return _serviceOrchestrateurEtatJeu.EtatLocalJeuEstActif(
            _identifiantJeuLocalActif,
            _dernierEtatSondeLocaleEmulateurs?.EmulateurDetecte == true
        );
    }

    /*
     * Redémarre le minuteur d'actualisation API lorsque le profil reste
     * accessible, afin d'éviter une dérive de synchronisation.
     */
    private void RedemarrerMinuteurActualisationApi()
    {
        if (!_profilUtilisateurAccessible)
        {
            return;
        }

        _minuteurActualisationApi.Stop();
        _minuteurActualisationApi.Start();
    }

    /*
     * Réinitialise l'ensemble du contexte de surveillance locale et distante.
     */
    private void ReinitialiserContexteSurveillance()
    {
        _actualisationApiCibleeEnAttente = false;
        _suiviEtatJeuVisibleInitialise = false;
        _dernierIdentifiantJeuApi = 0;
        _dernierIdentifiantJeuAvecInfos = 0;
        _dernierIdentifiantJeuAvecProgression = 0;
        _dernierTitreJeuApi = string.Empty;
        _dernierePresenceRiche = string.Empty;
        _signatureDerniereNoticeCompteJournalisee = string.Empty;
        _dernierPseudoCharge = string.Empty;
        _dernierProfilUtilisateurCharge = null;
        _dernierResumeUtilisateurCharge = null;
        _dernierEtatSondeLocaleEmulateurs = null;
        _presenceLocaleCompteActive = false;
        _signatureDernierSuccesLocalDirectAffiche = string.Empty;
        _signatureSuccesLocalDirectIgnoreeAuRejeu = string.Empty;
        _signatureDernierEtatJeuVisible = string.Empty;
        _identifiantJeuSuccesObserve = 0;
        _etatSuccesObserves = [];
        _identifiantJeuLocalResolutEnAttente = 0;
        _titreJeuLocalResolutEnAttente = string.Empty;
        _identifiantJeuLocalActif = 0;
        _titreJeuLocalActif = string.Empty;
        ReinitialiserContexteRejouerJeu();
        _serviceOrchestrateurEtatJeu.Reinitialiser();
        _consolesResolutionLocale = [];
        _serviceSurveillanceSuccesLocaux.ArreterSurveillance();
        _horodatageDerniereSynchronisationEtatJeuUtc = DateTimeOffset.MinValue;
    }

    /*
     * Surveille périodiquement le Rich Presence utilisateur pour ajuster
     * l'état visible du jeu si nécessaire.
     */
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

            if (!_serviceOrchestrateurEtatJeu.DoitTraiterEtatRichPresence(signatureEtat))
            {
                return;
            }
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
        catch (Exception exception)
        {
            JournaliserExceptionNonBloquante("richpresence_tick", exception);
        }
        finally
        {
            _surveillanceRichPresenceEnCours = false;
        }
    }

    /*
     * Détecte la présence locale d'un émulateur afin de mettre à jour
     * l'indication de compte actif et la synchronisation ciblée.
     */
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

            _serviceOrchestrateurEtatJeu.EnregistrerPresenceLocaleCompte(presenceActive);

            if (_presenceLocaleCompteActive != presenceActive)
            {
                _presenceLocaleCompteActive = presenceActive;
                MettreAJourNoticeCompteEntete();
                DemanderSynchronisationCibleeEtatJeu(
                    presenceActive ? "presence_locale_active" : "presence_locale_inactive"
                );
            }
        }
        catch (Exception exception)
        {
            JournaliserExceptionNonBloquante("presence_locale_compte_tick", exception);
        }
        finally
        {
            _surveillancePresenceLocaleCompteEnCours = false;
        }
    }

    /*
     * Exécute la sonde locale principale pour résoudre le jeu courant à partir
     * de l'émulateur détecté et de son contexte.
     */
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
                _serviceOrchestrateurEtatJeu.EnregistrerDetectionLocaleValide();
                _dernierEtatSondeLocaleEmulateurs = etatBrut;
            }
            else if (
                _dernierEtatSondeLocaleEmulateurs?.EmulateurDetecte == true
                && _serviceOrchestrateurEtatJeu.SondeLocaleEstEncoreValide()
            )
            {
                etat = _dernierEtatSondeLocaleEmulateurs;
            }
            else
            {
                _dernierEtatSondeLocaleEmulateurs = etatBrut;
            }

            bool emulateurValide =
                etat.EmulateurDetecte
                && ServiceCatalogueEmulateursLocaux.EstEmulateurValide(etat.NomEmulateur);
            bool emulateurValideBrut =
                etatBrut.EmulateurDetecte
                && ServiceCatalogueEmulateursLocaux.EstEmulateurValide(etatBrut.NomEmulateur);
            _emulateurValideDetecteEnDirect = emulateurValideBrut;

            if (emulateurValideBrut)
            {
                await MemoriserEmplacementEmulateurDetecteAsync(etatBrut);
            }

            _serviceSurveillanceSuccesLocaux.MettreAJourCible(emulateurValide ? etat : null);
            MettreAJourNoticeCompteEntete();

            if (!_serviceOrchestrateurEtatJeu.DoitTraiterSondeLocale(etat.Signature))
            {
                return;
            }

            if (
                !ConfigurationConnexionEstComplete()
                || !_profilUtilisateurAccessible
                || !etat.EmulateurDetecte
                || !emulateurValide
            )
            {
                if (etat.EmulateurDetecte && !emulateurValide)
                {
                    ServiceResolutionJeuLocal.JournaliserEvenementInterface(
                        "resolution_locale_ignoree",
                        $"raison=emulateur_non_valide;emulateur={etat.NomEmulateur};processus={etat.NomProcessus};titreFenetre={etat.TitreFenetre}"
                    );
                }

                _serviceOrchestrateurEtatJeu.OublierResolutionLocale();
                _identifiantJeuLocalActif = 0;
                _titreJeuLocalActif = string.Empty;
                _identifiantJeuLocalResolutEnAttente = 0;
                _titreJeuLocalResolutEnAttente = string.Empty;
                ReinitialiserContexteRejouerJeu();
                MettreAJourNoticeCompteEntete();
                return;
            }

            AppliquerTitreJeuLocalProvisoire(etat);

            if (etat.IdentifiantJeuProbable > 0)
            {
                if (
                    _serviceOrchestrateurEtatJeu.DoitIgnorerResolutionLocale(
                        etat.IdentifiantJeuProbable
                    )
                )
                {
                    return;
                }

                string titreJeuRacache = string.IsNullOrWhiteSpace(etat.TitreJeuProbable)
                    ? $"Game ID {etat.IdentifiantJeuProbable}"
                    : etat.TitreJeuProbable;
                string signatureResolutionDirecte =
                    $"{etat.NomEmulateur}|{etat.IdentifiantJeuProbable}|racache_direct";

                if (
                    _serviceOrchestrateurEtatJeu.DoitTraiterResolutionLocale(
                        signatureResolutionDirecte,
                        etat.IdentifiantJeuProbable,
                        _dernierIdentifiantJeuApi,
                        _identifiantJeuLocalActif
                    )
                )
                {
                    ActualiserContexteRejouerJeu(etat, etat.IdentifiantJeuProbable);
                    _identifiantJeuLocalActif = etat.IdentifiantJeuProbable;
                    _titreJeuLocalActif = titreJeuRacache;
                    _serviceOrchestrateurEtatJeu.EnregistrerResolutionJeuLocalValide();

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
                return;
            }

            if (DoitVerrouillerAffichageSurDernierJeuActifRecemment())
            {
                ReappliquerDernierJeuActifRecemment();
                _serviceOrchestrateurEtatJeu.OublierResolutionLocale();
                return;
            }

            JeuLocalResolut? jeuResolutImmediate =
                ServiceResolutionJeuLocal.ResoudreDepuisJeuxRecents(
                    etat.TitreJeuProbable,
                    _dernierResumeUtilisateurCharge?.RecentlyPlayed ?? []
                );

            if (jeuResolutImmediate is not null)
            {
                if (
                    _serviceOrchestrateurEtatJeu.DoitIgnorerResolutionLocale(
                        jeuResolutImmediate.IdentifiantJeu
                    )
                )
                {
                    return;
                }

                string signatureResolutionImmediate =
                    $"{etat.Signature}|{jeuResolutImmediate.IdentifiantJeu}|{jeuResolutImmediate.Source}";

                if (
                    _serviceOrchestrateurEtatJeu.DoitTraiterResolutionLocale(
                        signatureResolutionImmediate,
                        jeuResolutImmediate.IdentifiantJeu,
                        _dernierIdentifiantJeuApi,
                        _identifiantJeuLocalActif
                    )
                )
                {
                    ActualiserContexteRejouerJeu(etat, jeuResolutImmediate.IdentifiantJeu);
                    _identifiantJeuLocalActif = jeuResolutImmediate.IdentifiantJeu;
                    _titreJeuLocalActif = jeuResolutImmediate.TitreRetroAchievements;
                    _serviceOrchestrateurEtatJeu.EnregistrerResolutionJeuLocalValide();

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
                if (
                    _serviceOrchestrateurEtatJeu.DoitIgnorerResolutionLocale(
                        jeuResolut.IdentifiantJeu
                    )
                )
                {
                    return;
                }

                string signatureResolution =
                    $"{etat.Signature}|{jeuResolut.IdentifiantJeu}|{jeuResolut.Source}";

                if (
                    !_serviceOrchestrateurEtatJeu.DoitTraiterResolutionLocale(
                        signatureResolution,
                        jeuResolut.IdentifiantJeu,
                        _dernierIdentifiantJeuApi,
                        _identifiantJeuLocalActif
                    )
                )
                {
                    return;
                }

                ActualiserContexteRejouerJeu(etat, jeuResolut.IdentifiantJeu);
                _identifiantJeuLocalActif = jeuResolut.IdentifiantJeu;
                _titreJeuLocalActif = jeuResolut.TitreRetroAchievements;
                _serviceOrchestrateurEtatJeu.EnregistrerResolutionJeuLocalValide();

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

            _serviceOrchestrateurEtatJeu.OublierResolutionLocale();
        }
        catch (Exception exception)
        {
            JournaliserExceptionNonBloquante("sonde_locale_emulateurs_tick", exception);
        }
        finally
        {
            _surveillanceLocaleEmulateursEnCours = false;
        }
    }

    /*
     * Tente de résoudre un jeu local depuis le titre détecté, les jeux récents
     * et les catalogues mis en cache.
     */
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

    /*
     * Détermine les consoles candidates à partir du nom de l'émulateur
     * détecté localement.
     */
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

    /*
     * Efface le contexte local utilisé pour l'action de rejeu.
     */
    private void ReinitialiserContexteRejouerJeu()
    {
        _identifiantJeuRejouableCourant = 0;
        _nomEmulateurRejouableCourant = string.Empty;
        _cheminEmulateurRejouableCourant = string.Empty;
        _cheminJeuRejouableCourant = string.Empty;
    }

    /*
     * Met à jour le contexte de rejeu avec les chemins détectés pour le jeu
     * local actuellement résolu.
     */
    private void ActualiserContexteRejouerJeu(EtatSondeLocaleEmulateur etat, int identifiantJeu)
    {
        if (
            identifiantJeu <= 0
            || string.IsNullOrWhiteSpace(etat.NomEmulateur)
            || string.IsNullOrWhiteSpace(etat.CheminExecutable)
            || string.IsNullOrWhiteSpace(etat.CheminJeuProbable)
            || !File.Exists(etat.CheminExecutable)
            || !File.Exists(etat.CheminJeuProbable)
        )
        {
            if (_identifiantJeuRejouableCourant != identifiantJeu)
            {
                ReinitialiserContexteRejouerJeu();
            }

            return;
        }

        _identifiantJeuRejouableCourant = identifiantJeu;
        _nomEmulateurRejouableCourant = etat.NomEmulateur;
        _cheminEmulateurRejouableCourant = etat.CheminExecutable;
        _cheminJeuRejouableCourant = etat.CheminJeuProbable;

        EtatJeuAfficheLocal? jeuAffiche = ObtenirEtatJeuAffichePourContexteRejouer(identifiantJeu);

        if (jeuAffiche is not null)
        {
            bool modifie =
                jeuAffiche.NomEmulateurRelance != _nomEmulateurRejouableCourant
                || jeuAffiche.CheminExecutableEmulateur != _cheminEmulateurRejouableCourant
                || jeuAffiche.CheminJeuLocal != _cheminJeuRejouableCourant;

            jeuAffiche.NomEmulateurRelance = _nomEmulateurRejouableCourant;
            jeuAffiche.CheminExecutableEmulateur = _cheminEmulateurRejouableCourant;
            jeuAffiche.CheminJeuLocal = _cheminJeuRejouableCourant;

            if (modifie)
            {
                _dernierJeuAfficheModifie = true;
                _ = PersisterDernierJeuAfficheSiNecessaireAsync();
            }

            MettreAJourActionRejouerJeuEnCours(jeuAffiche);
        }
    }

    /*
     * Retourne l'état du jeu affiché à enrichir pour le rejeu, en le créant
     * au besoin à partir des données actuellement affichées.
     */
    private EtatJeuAfficheLocal? ObtenirEtatJeuAffichePourContexteRejouer(int identifiantJeu)
    {
        if (_configurationConnexion.DernierJeuAffiche?.Id == identifiantJeu)
        {
            return _configurationConnexion.DernierJeuAffiche;
        }

        if (_dernieresDonneesJeuAffichees?.Jeu.Id != identifiantJeu)
        {
            return null;
        }

        JeuAffiche jeuAffiche = ServicePresentationJeu.Construire(_dernieresDonneesJeuAffichees);
        GameInfoAndUserProgressV2 jeu = _dernieresDonneesJeuAffichees.Jeu;

        _configurationConnexion.DernierJeuAffiche = new EtatJeuAfficheLocal
        {
            IdentifiantJeu = jeu.Id,
            EstJeuEnCours = EtatLocalJeuEstActif(),
            Titre = jeu.Title,
            Details = jeuAffiche.Details,
            ResumeProgression = jeuAffiche.ResumeProgression,
            PourcentageProgression = jeuAffiche.PourcentageTexte,
            ValeurProgression = jeuAffiche.PourcentageValeur,
            TempsJeuSousImage = jeuAffiche.TempsJeu,
            EtatJeu = jeuAffiche.Statut,
            CheminImageBoite = jeu.ImageBoxArt,
            IdentifiantConsole = jeu.ConsoleId,
            DateSortie = jeu.Released,
            Genre = jeu.Genre,
            Developpeur = jeu.Developer,
        };

        _dernierJeuAfficheModifie = true;
        return _configurationConnexion.DernierJeuAffiche;
    }

    /*
     * Mémorise le chemin d'un émulateur détecté localement pour faciliter les
     * futures actions de rejeu.
     */
    private async Task MemoriserEmplacementEmulateurDetecteAsync(EtatSondeLocaleEmulateur etat)
    {
        if (
            !etat.EmulateurDetecte
            || string.IsNullOrWhiteSpace(etat.NomEmulateur)
            || string.IsNullOrWhiteSpace(etat.CheminExecutable)
        )
        {
            ServiceSondeLocaleEmulateurs.JournaliserEvenement(
                "memoire_emplacement_ignoree",
                $"raison=chemin_absent_ou_detection_incomplete;emulateur={etat.NomEmulateur};processus={etat.NomProcessus};chemin={etat.CheminExecutable}"
            );
            return;
        }

        _configurationConnexion.EmplacementsEmulateursDetectes ??= [];

        if (
            !ServiceSourcesLocalesEmulateurs.MemoriserEmplacementEmulateurDetecte(
                etat.NomEmulateur,
                etat.CheminExecutable
            )
        )
        {
            ServiceSondeLocaleEmulateurs.JournaliserEvenement(
                "memoire_emplacement_ignoree",
                $"raison=chemin_non_retenu;emulateur={etat.NomEmulateur};processus={etat.NomProcessus};chemin={etat.CheminExecutable}"
            );
            return;
        }

        _configurationConnexion.EmplacementsEmulateursDetectes[etat.NomEmulateur] =
            etat.CheminExecutable;
        ServiceSondeLocaleEmulateurs.JournaliserEvenement(
            "memoire_emplacement_enregistree",
            $"emulateur={etat.NomEmulateur};processus={etat.NomProcessus};chemin={etat.CheminExecutable}"
        );

        try
        {
            await _serviceConfigurationLocale.SauvegarderEtatApplicationAsync(
                _configurationConnexion
            );
            ServiceSondeLocaleEmulateurs.JournaliserEvenement(
                "memoire_emplacement_persistee",
                $"emulateur={etat.NomEmulateur};chemin={etat.CheminExecutable}"
            );
        }
        catch
        {
            ServiceSondeLocaleEmulateurs.JournaliserEvenement(
                "memoire_emplacement_echec_persistance",
                $"emulateur={etat.NomEmulateur};chemin={etat.CheminExecutable}"
            );
        }
    }

    /*
     * Retourne les alias de consoles associés à un émulateur connu.
     */
    private static string[] ObtenirAliasConsolesDepuisEmulateur(string nomEmulateur)
    {
        return ServiceCatalogueEmulateursLocaux.ObtenirAliasConsoles(nomEmulateur);
    }

    /*
     * Normalise un nom de console pour faciliter les rapprochements textuels.
     */
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

    /*
     * Redirige un signal de succès local vers le thread UI pour traitement.
     */
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

    /*
     * Traite un signal local de succès en essayant d'abord l'affichage direct,
     * puis un rafraîchissement ciblé si nécessaire.
     */
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

        bool jeuLocalActif = EtatLocalJeuEstActif();
        int identifiantJeuSignal =
            _identifiantJeuLocalActif > 0 ? _identifiantJeuLocalActif
            : etat.IdentifiantJeuProbable > 0 ? etat.IdentifiantJeuProbable
            : _identifiantJeuSuccesCourant;
        string titreJeuSignal =
            !string.IsNullOrWhiteSpace(_titreJeuLocalActif) ? _titreJeuLocalActif
            : !string.IsNullOrWhiteSpace(etat.TitreJeuProbable) ? etat.TitreJeuProbable
            : _dernieresDonneesJeuAffichees?.Jeu.Title ?? string.Empty;

        if (!jeuLocalActif && identifiantJeuSignal <= 0)
        {
            ServiceSurveillanceSuccesLocaux.JournaliserEvenement(
                "signal_ignore",
                $"raison=jeu_local_inactif;emulateur={signal.NomEmulateur}"
            );
            return;
        }

        EnregistrerContexteSignalSuccesLocal(signal, identifiantJeuSignal);

        if (
            await TenterAfficherSuccesLocalDirectAsync(signal, identifiantJeuSignal, titreJeuSignal)
        )
        {
            return;
        }

        if (_serviceOrchestrateurEtatJeu.DoitIgnorerSignalSuccesLocal())
        {
            ServiceSurveillanceSuccesLocaux.JournaliserEvenement(
                "signal_ignore",
                $"raison=debounce;emulateur={signal.NomEmulateur}"
            );
            return;
        }

        _serviceOrchestrateurEtatJeu.EnregistrerSignalSuccesLocal();

        if (await TenterAfficherSuccesApiSansCacheAsync(signal, identifiantJeuSignal))
        {
            return;
        }

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

    /*
     * Tente d'afficher immédiatement un succès détecté via une source locale
     * compatible sans attendre un rafraîchissement API complet.
     */
    private async Task<bool> TenterAfficherSuccesLocalDirectAsync(
        SignalSuccesLocal signal,
        int identifiantJeu,
        string titreJeu
    )
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
                identifiantJeu,
                titreJeu,
                _succesJeuCourant
            );

        if (succesDirect is null)
        {
            return false;
        }

        string signatureSucces = ConstruireSignatureSuccesLocalDirect(succesDirect);

        if (
            !string.IsNullOrWhiteSpace(_signatureSuccesLocalDirectIgnoreeAuRejeu)
            && string.Equals(
                _signatureSuccesLocalDirectIgnoreeAuRejeu,
                signatureSucces,
                StringComparison.Ordinal
            )
        )
        {
            ServiceSurveillanceSuccesLocaux.JournaliserEvenement(
                "signal_succes_direct_ignore",
                $"raison=baseline_rejeu;emulateur={signal.NomEmulateur};succes={succesDirect.IdentifiantSucces}"
            );
            return false;
        }

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

        string sourceDetectionDirecte = string.Equals(
            signal.NomEmulateur,
            "BizHawk",
            StringComparison.Ordinal
        )
            ? "bizhawk_json"
            : $"{signal.NomEmulateur.ToLowerInvariant()}_log";
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
        _signatureSuccesLocalDirectIgnoreeAuRejeu = string.Empty;
        ServiceSurveillanceSuccesLocaux.JournaliserEvenement(
            "signal_succes_direct_affiche",
            $"emulateur={signal.NomEmulateur};source={signal.TypeSource};jeu={succesDirect.IdentifiantJeu};succes={succesDirect.IdentifiantSucces}"
        );
        return true;
    }

    /*
     * Construit une signature stable pour dédupliquer l'affichage d'un succès
     * détecté localement.
     */
    private static string ConstruireSignatureSuccesLocalDirect(SuccesDebloqueDetecte succes)
    {
        return $"{succes.IdentifiantJeu}|{succes.IdentifiantSucces}|{succes.TitreSucces}";
    }

    /*
     * Tente de détecter un nouveau succès en rechargeant rapidement les données
     * API du jeu sans utiliser le cache.
     */
    private async Task<bool> TenterAfficherSuccesApiSansCacheAsync(
        SignalSuccesLocal signal,
        int identifiantJeu
    )
    {
        if (
            !string.Equals(signal.NomEmulateur, "DuckStation", StringComparison.Ordinal)
            || identifiantJeu <= 0
            || _identifiantJeuSuccesCourant != identifiantJeu
            || _succesJeuCourant.Count == 0
        )
        {
            return false;
        }

        try
        {
            ServiceSurveillanceSuccesLocaux.JournaliserEvenement(
                "signal_succes_api_sans_cache_debut",
                $"emulateur={signal.NomEmulateur};source={signal.TypeSource};jeu={identifiantJeu}"
            );

            DonneesJeuAffiche donneesJeu =
                await _serviceJeuRetroAchievements.ObtenirDonneesJeuRapidesSansCacheAsync(
                    _configurationConnexion.Pseudo,
                    _configurationConnexion.CleApiWeb,
                    identifiantJeu
                );
            GameInfoAndUserProgressV2 jeu = donneesJeu.Jeu;

            if (jeu.Id != identifiantJeu)
            {
                ServiceSurveillanceSuccesLocaux.JournaliserEvenement(
                    "signal_succes_api_sans_cache_ignore",
                    $"raison=jeu_mismatch;emulateur={signal.NomEmulateur};attendu={identifiantJeu};recu={jeu.Id}"
                );
                return false;
            }

            List<GameAchievementV2> succesCourants = [.. jeu.Succes.Values];

            if (_identifiantJeuSuccesObserve != jeu.Id)
            {
                _identifiantJeuSuccesObserve = jeu.Id;
                _etatSuccesObserves = ServiceDetectionSuccesJeu.CapturerEtat(succesCourants);
                ServiceDetectionSuccesJeu.JournaliserInitialisation(
                    jeu.Id,
                    jeu.Title,
                    succesCourants.Count
                );
                ServiceSurveillanceSuccesLocaux.JournaliserEvenement(
                    "signal_succes_api_sans_cache_initialise",
                    $"emulateur={signal.NomEmulateur};jeu={jeu.Id};succes={succesCourants.Count}"
                );
                return false;
            }

            IReadOnlyList<SuccesDebloqueDetecte> nouveauxSucces =
                ServiceDetectionSuccesJeu.DetecterNouveauxSucces(
                    jeu.Id,
                    jeu.Title,
                    _etatSuccesObserves,
                    succesCourants
                );
            List<SuccesDebloqueDetecte> nouveauxSuccesFiltres =
            [
                .. nouveauxSucces.Where(succes => !SuccesDejaTraiteRecemment(succes)),
            ];

            _etatSuccesObserves = ServiceDetectionSuccesJeu.CapturerEtat(succesCourants);

            foreach (SuccesDebloqueDetecte succes in nouveauxSuccesFiltres)
            {
                ServiceDetectionSuccesJeu.JournaliserDetection(succes, "duckstation_api");
                MarquerSuccesCommeTraite(succes);
            }

            SuccesDebloqueDetecte? succesLePlusRecent = SelectionnerSuccesDebloqueLePlusRecent(
                nouveauxSuccesFiltres
            );

            if (succesLePlusRecent is null)
            {
                ServiceSurveillanceSuccesLocaux.JournaliserEvenement(
                    "signal_succes_api_sans_cache_aucun_deblocage",
                    $"emulateur={signal.NomEmulateur};source={signal.TypeSource};jeu={jeu.Id}"
                );
                return false;
            }

            bool affiche = await AfficherSuccesDebloqueDetecteAsync(succesLePlusRecent);
            ServiceSurveillanceSuccesLocaux.JournaliserEvenement(
                affiche
                    ? "signal_succes_api_sans_cache_affiche"
                    : "signal_succes_api_sans_cache_echec_ui",
                $"emulateur={signal.NomEmulateur};source={signal.TypeSource};jeu={succesLePlusRecent.IdentifiantJeu};succes={succesLePlusRecent.IdentifiantSucces}"
            );
            return affiche;
        }
        catch
        {
            ServiceSurveillanceSuccesLocaux.JournaliserEvenement(
                "signal_succes_api_sans_cache_echec",
                $"emulateur={signal.NomEmulateur};source={signal.TypeSource};jeu={identifiantJeu}"
            );
            return false;
        }
    }

    /*
     * Indique si l'émulateur donné prend en charge la lecture directe d'un
     * succès local débloqué.
     */
    private static bool EstEmulateurSuccesLocalDirectPrisEnCharge(string nomEmulateur)
    {
        return ServiceCatalogueEmulateursLocaux.EstSuccesLocalDirectPrisEnCharge(nomEmulateur);
    }

    /*
     * Indique si le type de source reçu peut transporter une information de
     * succès local directement exploitable.
     */
    private static bool TypeSourcePeutPorterSuccesDirect(SignalSuccesLocal signal)
    {
        return ServiceCatalogueEmulateursLocaux.TypeSourcePeutPorterSuccesDirect(
            signal.NomEmulateur,
            signal.TypeSource
        );
    }

    /*
     * Mémorise le contexte du dernier signal local afin de faciliter les
     * diagnostics et la détection de source.
     */
    private void EnregistrerContexteSignalSuccesLocal(
        SignalSuccesLocal signal,
        int identifiantJeuSignal
    )
    {
        _identifiantJeuDernierSignalSuccesLocal = identifiantJeuSignal;
        _nomEmulateurDernierSignalSuccesLocal = signal.NomEmulateur?.Trim() ?? string.Empty;
        _typeSourceDernierSignalSuccesLocal = signal.TypeSource?.Trim() ?? string.Empty;
        _horodatageDernierSignalSuccesLocalUtc = signal.HorodatageUtc;
    }

    /*
     * Déduit une source de détection à partir du dernier contexte local récent.
     */
    private string DeterminerSourceDetectionDepuisContexteLocalRecent(int identifiantJeu)
    {
        if (
            identifiantJeu <= 0
            || _horodatageDernierSignalSuccesLocalUtc == DateTimeOffset.MinValue
            || _identifiantJeuDernierSignalSuccesLocal != identifiantJeu
        )
        {
            return "session";
        }

        if (
            DateTimeOffset.UtcNow - _horodatageDernierSignalSuccesLocalUtc
            > TimeSpan.FromSeconds(10)
        )
        {
            return "session";
        }

        if (
            string.Equals(
                _nomEmulateurDernierSignalSuccesLocal,
                "BizHawk",
                StringComparison.Ordinal
            ) && _typeSourceDernierSignalSuccesLocal.StartsWith("logs", StringComparison.Ordinal)
        )
        {
            return "bizhawk_json";
        }

        return "session";
    }
}
