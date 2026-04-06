using System.IO;
using System.Windows.Threading;
using RA.Compagnon.Modeles.Api.V2.Game;
using RA.Compagnon.Modeles.Api.V2.User;
using RA.Compagnon.Modeles.Catalogue;
using RA.Compagnon.Modeles.Local;
using RA.Compagnon.Modeles.Presentation;
using RA.Compagnon.Services;

namespace RA.Compagnon;

public partial class MainWindow
{
    private bool EtatLocalEmulateurEstActifPourNotice()
    {
        return _serviceOrchestrateurEtatJeu.EtatLocalEmulateurEstActifPourNotice(
            _presenceLocaleCompteActive
        );
    }

    private bool EtatLocalJeuEstActif()
    {
        return _serviceOrchestrateurEtatJeu.EtatLocalJeuEstActif(
            _identifiantJeuLocalActif,
            _dernierEtatSondeLocaleEmulateurs?.EmulateurDetecte == true
        );
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
        _signatureDerniereNoticeCompteJournalisee = string.Empty;
        _dernierPseudoCharge = string.Empty;
        _dernierProfilUtilisateurCharge = null;
        _dernierResumeUtilisateurCharge = null;
        _dernierEtatSondeLocaleEmulateurs = null;
        _presenceLocaleCompteActive = false;
        _signatureDernierSuccesLocalDirectAffiche = string.Empty;
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

            _serviceOrchestrateurEtatJeu.EnregistrerPresenceLocaleCompte(presenceActive);

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
                // Pour RALibretro, le titre fenetre/JSON recent peut rester sur l'ancien jeu
                // pendant une transition. On attend donc le prochain Game ID RA au lieu
                // de lancer un matching par titre potentiellement faux.
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

    private void ReinitialiserContexteRejouerJeu()
    {
        _identifiantJeuRejouableCourant = 0;
        _nomEmulateurRejouableCourant = string.Empty;
        _cheminEmulateurRejouableCourant = string.Empty;
        _cheminJeuRejouableCourant = string.Empty;
    }

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

        if (_configurationConnexion.DernierJeuAffiche?.Id == identifiantJeu)
        {
            bool modifie =
                _configurationConnexion.DernierJeuAffiche.NomEmulateurRelance
                    != _nomEmulateurRejouableCourant
                || _configurationConnexion.DernierJeuAffiche.CheminExecutableEmulateur
                    != _cheminEmulateurRejouableCourant
                || _configurationConnexion.DernierJeuAffiche.CheminJeuLocal
                    != _cheminJeuRejouableCourant;

            _configurationConnexion.DernierJeuAffiche.NomEmulateurRelance =
                _nomEmulateurRejouableCourant;
            _configurationConnexion.DernierJeuAffiche.CheminExecutableEmulateur =
                _cheminEmulateurRejouableCourant;
            _configurationConnexion.DernierJeuAffiche.CheminJeuLocal = _cheminJeuRejouableCourant;

            if (modifie)
            {
                _dernierJeuAfficheModifie = true;
                _ = PersisterDernierJeuAfficheSiNecessaireAsync();
            }

            MettreAJourActionRejouerJeuEnCours(_configurationConnexion.DernierJeuAffiche);
        }
    }

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
            // La détection locale ne doit pas casser l'application si la persistance échoue ponctuellement.
        }
    }

    private static string[] ObtenirAliasConsolesDepuisEmulateur(string nomEmulateur)
    {
        return ServiceCatalogueEmulateursLocaux.ObtenirAliasConsoles(nomEmulateur);
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

    private static bool EstEmulateurSuccesLocalDirectPrisEnCharge(string nomEmulateur)
    {
        return ServiceCatalogueEmulateursLocaux.EstSuccesLocalDirectPrisEnCharge(nomEmulateur);
    }

    private static bool TypeSourcePeutPorterSuccesDirect(SignalSuccesLocal signal)
    {
        return ServiceCatalogueEmulateursLocaux.TypeSourcePeutPorterSuccesDirect(
            signal.NomEmulateur,
            signal.TypeSource
        );
    }
}
