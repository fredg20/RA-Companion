using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using RA.Compagnon.Modeles.Api.V2.Achievement;
using RA.Compagnon.Modeles.Api.V2.Game;
using RA.Compagnon.Modeles.Api.V2.User;
using RA.Compagnon.Modeles.Etat;
using RA.Compagnon.Modeles.Local;
using RA.Compagnon.Modeles.Presentation;
using RA.Compagnon.Services;

namespace RA.Compagnon;

public partial class MainWindow
{
    private async Task ChargerJeuEnCoursAsync(
        bool afficherEtatChargement = true,
        bool forcerChargementJeu = true
    )
    {
        App.JournaliserDemarrage("ChargerJeuEnCours début");
        JournaliserDiagnosticChangementJeu(
            "charger_jeu_debut",
            $"forcer={forcerChargementJeu};chargement={afficherEtatChargement}"
        );

        if (!ConfigurationConnexionEstComplete())
        {
            ReinitialiserJeuEnCours();
            return;
        }

        if (_chargementJeuEnCoursActif)
        {
            return;
        }

        _chargementJeuEnCoursActif = true;

        try
        {
            if (afficherEtatChargement)
            {
                ReinitialiserPremierSuccesNonDebloque();
                ReinitialiserGrilleTousSucces();
                DefinirTempsJeuSousImage(string.Empty);
                DefinirEtatJeuDansProgression(string.Empty);
                ReinitialiserSuccesRecents();
            }

            UserProfileV2 profil = await _serviceUtilisateurRetroAchievements.ObtenirProfilAsync(
                _configurationConnexion.Pseudo,
                _configurationConnexion.CleApiWeb
            );

            App.JournaliserDemarrage("ChargerJeuEnCours apres Profil");
            JournaliserDiagnosticChangementJeu("charger_jeu_profil_charge");
            _profilUtilisateurAccessible = true;
            _dernierProfilUtilisateurCharge = profil;
            _dernierResumeUtilisateurCharge = null;
            DefinirEtatConnexion("Connecté");
            await AppliquerProfilUtilisateurAsync(profil, forcerChargementJeu);
            App.JournaliserDemarrage("ChargerJeuEnCours apres uppliquerProfil");
            DemarrerChargementSuccesRecentsEnArrierePlan(
                profil,
                _versionChargementContenuJeu,
                profil.LastGameId
            );
            App.JournaliserDemarrage("ChargerJeuEnCours apres SuccesRecents");
            JournaliserDiagnosticChangementJeu("charger_jeu_fin");
        }
        catch (UtilisateurRetroAchievementsInaccessibleException)
        {
            _profilUtilisateurAccessible = false;
            _dernierProfilUtilisateurCharge = null;
            _dernierResumeUtilisateurCharge = null;
            _dernieresDonneesJeuAffichees = null;
            _minuteurActualisationApi.Stop();
            _minuteurActualisationRichPresence.Stop();
            _minuteurSondeLocaleEmulateurs.Stop();
            ReinitialiserContexteSurveillance();
            DefinirEtatConnexion("Profil inaccessible");

            ReinitialiserMetaConsoleJeuEnCours();
            ReinitialiserPremierSuccesNonDebloque();
            ReinitialiserGrilleTousSucces();
            DefinirTempsJeuSousImage(string.Empty);
            DefinirEtatJeuDansProgression(string.Empty);
            DefinirTitreJeuEnCours(string.Empty);
            DefinirDetailsJeuEnCours(string.Empty);
            TexteResumeProgressionJeuEnCours.Text = "-- / --";
            TextePourcentageJeuEnCours.Text = "Impossible de charger la progression du jeu.";
            BarreProgressionJeuEnCours.Value = 0;
            ReinitialiserSuccesRecents();
            TexteEtatSuccesRecents.Text =
                "Impossible de charger les succès récents pour ce compte.";
        }
        catch (Exception)
        {
            _dernierProfilUtilisateurCharge = null;
            _dernierResumeUtilisateurCharge = null;
            _dernieresDonneesJeuAffichees = null;
            DefinirEtatConnexion("Hors ligne ou erreur API");

            if (afficherEtatChargement)
            {
                ReinitialiserMetaConsoleJeuEnCours();
                ReinitialiserPremierSuccesNonDebloque();
                ReinitialiserGrilleTousSucces();
                DefinirTempsJeuSousImage(string.Empty);
                DefinirEtatJeuDansProgression(string.Empty);
                DefinirTitreJeuEnCours(string.Empty);
                DefinirDetailsJeuEnCours(string.Empty);
                TexteResumeProgressionJeuEnCours.Text = "-- / --";
                TextePourcentageJeuEnCours.Text = "Impossible de charger la progression du jeu.";
                BarreProgressionJeuEnCours.Value = 0;
                ReinitialiserSuccesRecents();
            }
        }
        finally
        {
            _chargementJeuEnCoursActif = false;
            App.JournaliserDemarrage("ChargerJeuEnCours fin");

            if (_identifiantJeuLocalResolutEnAttente > 0 && ConfigurationConnexionEstComplete())
            {
                int identifiantJeuLocalResolut = _identifiantJeuLocalResolutEnAttente;
                string titreJeuLocalResolut = _titreJeuLocalResolutEnAttente;
                _identifiantJeuLocalResolutEnAttente = 0;
                _titreJeuLocalResolutEnAttente = string.Empty;
                ChargerJeuResolutLocal(identifiantJeuLocalResolut, titreJeuLocalResolut);
                RedemarrerMinuteurActualisationApi();
            }
            else if (_actualisationApiCibleeEnAttente && ConfigurationConnexionEstComplete())
            {
                _actualisationApiCibleeEnAttente = false;
                _ = Dispatcher.InvokeAsync(async () =>
                {
                    await ChargerJeuEnCoursAsync(false, true);
                    RedemarrerMinuteurActualisationApi();
                });
            }
        }
    }

    private async Task AppliquerProfilUtilisateurAsync(
        UserProfileV2 profil,
        bool forcerChargementJeu
    )
    {
        DefinirTitreZoneJeu();

        string messagePresence = string.IsNullOrWhiteSpace(profil.RichPresenceMsg)
            ? "Aucune activité détectée."
            : profil.RichPresenceMsg;
        int identifiantJeuEffectif = profil.LastGameId;
        RecentlyPlayedGameV2? dernierJeuJoue = null;
        string titreJeuProvisoire;

        if (identifiantJeuEffectif <= 0)
        {
            dernierJeuJoue = await ObtenirDernierJeuJoueAsync();
            identifiantJeuEffectif = dernierJeuJoue?.Id ?? 0;
        }

        if (identifiantJeuEffectif <= 0)
        {
            ReinitialiserMetaConsoleJeuEnCours();
            ReinitialiserCarrouselVisuelsJeuEnCours();
            ReinitialiserPremierSuccesNonDebloque();
            ReinitialiserGrilleTousSucces();
            DefinirTempsJeuSousImage(string.Empty);
            DefinirEtatJeuDansProgression(string.Empty);
            DefinirTitreJeuEnCours(string.Empty);
            DefinirDetailsJeuEnCours(string.Empty);
            TexteResumeProgressionJeuEnCours.Text = "-- / --";
            TextePourcentageJeuEnCours.Text = "uucun jeu récent à afficher.";
            BarreProgressionJeuEnCours.Value = 0;
            return;
        }

        titreJeuProvisoire = DeterminerTitreJeuApiProvisoire(
            profil.LastGame,
            dernierJeuJoue?.Title
        );

        if (EtatLocalJeuEstActif())
        {
            identifiantJeuEffectif = _identifiantJeuLocalActif;

            if (!string.IsNullOrWhiteSpace(_titreJeuLocalActif))
            {
                titreJeuProvisoire = _titreJeuLocalActif;
            }
        }

        string titreAffichageInitial = string.IsNullOrWhiteSpace(_dernierTitreJeuApi)
            ? titreJeuProvisoire
            : _dernierTitreJeuApi;
        bool infosJeuDejaAfficheesPourCeJeu = PeutConserverInfosJeuAffichees(
            identifiantJeuEffectif
        );

        if (!infosJeuDejaAfficheesPourCeJeu)
        {
            ReinitialiserCarrouselVisuelsJeuEnCours();
            ReinitialiserImageJeuEnCours();
            DefinirDetailsJeuEnCours(string.Empty);
            DefinirTempsJeuSousImage(string.Empty);
            DefinirEtatJeuDansProgression(string.Empty);
        }

        bool progressionDejaAfficheePourCeJeu = PeutConserverProgressionAffichee(
            identifiantJeuEffectif
        );

        if (!infosJeuDejaAfficheesPourCeJeu)
        {
            DefinirTitreJeuEnCours(titreAffichageInitial);
        }

        if (!progressionDejaAfficheePourCeJeu)
        {
            TexteResumeProgressionJeuEnCours.Text = "-- / --";
            TextePourcentageJeuEnCours.Text = "Chargement de la progression...";
            BarreProgressionJeuEnCours.Value = 0;
        }

        bool contexteApiInchange =
            !forcerChargementJeu
            && _dernierIdentifiantJeuApi == identifiantJeuEffectif
            && string.Equals(_dernierePresenceRiche, messagePresence, StringComparison.Ordinal)
            && string.Equals(_dernierPseudoCharge, profil.User, StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(_dernierTitreJeuApi);

        await SynchroniserPseudoUtilisateurDepuisProfilAsync(profil);
        _dernierIdentifiantJeuApi = identifiantJeuEffectif;
        _dernierePresenceRiche = messagePresence;
        _dernierPseudoCharge = profil.User;

        if (contexteApiInchange)
        {
            DefinirTitreJeuEnCours(_dernierTitreJeuApi);
            return;
        }

        DemarrerDiagnosticChangementJeu(
            $"api:{identifiantJeuEffectif}",
            $"source=api;jeu={identifiantJeuEffectif};titre={titreJeuProvisoire}"
        );
        int versionChargement = ++_versionChargementContenuJeu;
        DemarrerChargementJeuUtilisateurEnArrierePlan(
            identifiantJeuEffectif,
            titreJeuProvisoire,
            infosJeuDejaAfficheesPourCeJeu,
            progressionDejaAfficheePourCeJeu,
            versionChargement
        );
    }

    private async Task SynchroniserPseudoUtilisateurDepuisProfilAsync(UserProfileV2 profil)
    {
        string pseudoApi = profil.User?.Trim() ?? string.Empty;

        if (
            string.IsNullOrWhiteSpace(pseudoApi)
            || string.Equals(_configurationConnexion.Pseudo, pseudoApi, StringComparison.Ordinal)
        )
        {
            return;
        }

        _configurationConnexion.Pseudo = pseudoApi;
        MettreAJourResumeConnexion();
        await _serviceConfigurationLocale.SauvegarderUtilisateurAsync(_configurationConnexion);
        await _serviceConfigurationLocale.SauvegarderEtatApplicationAsync(_configurationConnexion);
    }

    private void DemarrerChargementJeuUtilisateurEnArrierePlan(
        int identifiantJeuEffectif,
        string titreJeuProvisoire,
        bool infosJeuDejaAfficheesPourCeJeu,
        bool progressionDejaAfficheePourCeJeu,
        int versionChargement
    )
    {
        _ = ChargerJeuUtilisateurEnArrierePlanAsync(
            identifiantJeuEffectif,
            titreJeuProvisoire,
            infosJeuDejaAfficheesPourCeJeu,
            progressionDejaAfficheePourCeJeu,
            versionChargement
        );
    }

    private async Task ChargerJeuUtilisateurEnArrierePlanAsync(
        int identifiantJeuEffectif,
        string titreJeuProvisoire,
        bool infosJeuDejaAfficheesPourCeJeu,
        bool progressionDejaAfficheePourCeJeu,
        int versionChargement
    )
    {
        try
        {
            DonneesJeuAffiche donneesJeu =
                await _serviceJeuRetroAchievements.ObtenirDonneesJeuRapidesAsync(
                    _configurationConnexion.Pseudo,
                    _configurationConnexion.CleApiWeb,
                    identifiantJeuEffectif
                );

            if (!ChargementContenuJeuEstToujoursActuel(versionChargement, identifiantJeuEffectif))
            {
                return;
            }

            await AppliquerProgressionJeuAsync(donneesJeu);

            _ = EnrichirJeuEnArrierePlanAsync(
                identifiantJeuEffectif,
                versionChargement,
                donneesJeu
            );
            _ = EnrichirCommunauteJeuEnArrierePlanAsync(identifiantJeuEffectif, versionChargement);
        }
        catch
        {
            if (!ChargementContenuJeuEstToujoursActuel(versionChargement, identifiantJeuEffectif))
            {
                return;
            }

            if (!infosJeuDejaAfficheesPourCeJeu)
            {
                DefinirTitreJeuEnCours(titreJeuProvisoire);
                DefinirDetailsJeuEnCours(string.Empty);
                DefinirTempsJeuSousImage(string.Empty);
                DefinirEtatJeuDansProgression(string.Empty);
            }

            if (!progressionDejaAfficheePourCeJeu)
            {
                TexteResumeProgressionJeuEnCours.Text = "-- / --";
                TextePourcentageJeuEnCours.Text = "Impossible de charger la progression du jeu.";
                BarreProgressionJeuEnCours.Value = 0;
            }
        }
    }

    private async Task EnrichirJeuEnArrierePlanAsync(
        int identifiantJeuEffectif,
        int versionChargement,
        DonneesJeuAffiche donneesJeu
    )
    {
        try
        {
            DonneesJeuAffiche donneesEnrichies =
                await _serviceJeuRetroAchievements.EnrichirDonneesJeuAsync(
                    _configurationConnexion.Pseudo,
                    _configurationConnexion.CleApiWeb,
                    donneesJeu
                );

            if (!ChargementContenuJeuEstToujoursActuel(versionChargement, identifiantJeuEffectif))
            {
                return;
            }

            if (_dernieresDonneesJeuAffichees?.Jeu.Id != identifiantJeuEffectif)
            {
                return;
            }

            DonneesJeuAffiche donneesCourantes = _dernieresDonneesJeuAffichees;
            donneesEnrichies.Communaute = donneesCourantes.Communaute;
            donneesEnrichies.CommunauteAffichee = donneesCourantes.CommunauteAffichee;
            _dernieresDonneesJeuAffichees = donneesEnrichies;

            GameInfoAndUserProgressV2 jeu = donneesEnrichies.Jeu;
            JeuAffiche jeuAffiche = _servicePresentationJeu.Construire(donneesEnrichies);

            _dernierTitreJeuApi = jeu.Title;
            AppliquerMetaConsoleJeuEnCoursInitiale(jeu);
            DemarrerEnrichissementMetaConsoleJeuEnCours(jeu);
            DefinirTempsJeuSousImage(jeuAffiche.TempsJeu);
            DefinirEtatJeuDansProgression(jeuAffiche.Statut);
            DefinirDetailsJeuEnCours(jeuAffiche.Details);
        }
        catch
        {
            // Les enrichissements secondaires ne doivent pas ralentir ni casser le rendu principal.
        }
    }

    private async Task EnrichirCommunauteJeuEnArrierePlanAsync(
        int identifiantJeuEffectif,
        int versionChargement
    )
    {
        try
        {
            DonneesCommunauteJeu donneesCommunaute =
                await _serviceCommunauteRetroAchievements.ObtenirDonneesJeuAsync(
                    _configurationConnexion.Pseudo,
                    _configurationConnexion.CleApiWeb,
                    identifiantJeuEffectif
                );

            if (!ChargementContenuJeuEstToujoursActuel(versionChargement, identifiantJeuEffectif))
            {
                return;
            }

            if (_dernieresDonneesJeuAffichees?.Jeu.Id != identifiantJeuEffectif)
            {
                return;
            }

            _dernieresDonneesJeuAffichees.Communaute = donneesCommunaute;
            _dernieresDonneesJeuAffichees.CommunauteAffichee =
                _servicePresentationCommunaute.Construire(donneesCommunaute);

            JeuAffiche jeuAffiche = _servicePresentationJeu.Construire(
                _dernieresDonneesJeuAffichees
            );
            DefinirDetailsJeuEnCours(jeuAffiche.Details);
        }
        catch
        {
            // Les données communautaires restent secondaires et ne doivent pas casser l'affichage principal.
        }
    }

    private bool ChargementContenuJeuEstToujoursActuel(
        int versionChargement,
        int identifiantJeuEffectif
    )
    {
        return versionChargement == _versionChargementContenuJeu
            && identifiantJeuEffectif > 0
            && _dernierIdentifiantJeuApi == identifiantJeuEffectif;
    }

    private async Task ChargerSuccesRecentsAsync(
        UserProfileV2 profil,
        int versionChargement,
        int identifiantJeuProfil
    )
    {
        try
        {
            DateTimeOffset maintenant = DateTimeOffset.UtcNow;
            DonneesActiviteRecente activiteRecente =
                await _serviceActiviteRetroAchievements.ObtenirActiviteRecenteAsync(
                    _configurationConnexion.Pseudo,
                    _configurationConnexion.CleApiWeb,
                    maintenant.AddDays(-7),
                    maintenant
                );

            if (!ChargementSuccesRecentsEstToujoursActuel(versionChargement, identifiantJeuProfil))
            {
                return;
            }

            AppliquerSuccesRecents(
                _servicePresentationActivite.Construire(activiteRecente, profil.LastGameId)
            );
        }
        catch
        {
            if (!ChargementSuccesRecentsEstToujoursActuel(versionChargement, identifiantJeuProfil))
            {
                return;
            }

            AppliquerSuccesRecents(_servicePresentationActivite.ConstruireErreur());
        }
    }

    private void DemarrerChargementSuccesRecentsEnArrierePlan(
        UserProfileV2 profil,
        int versionChargement,
        int identifiantJeuProfil
    )
    {
        _ = ChargerSuccesRecentsEnArrierePlanAsync(profil, versionChargement, identifiantJeuProfil);
    }

    private async Task ChargerSuccesRecentsEnArrierePlanAsync(
        UserProfileV2 profil,
        int versionChargement,
        int identifiantJeuProfil
    )
    {
        try
        {
            await ChargerSuccesRecentsAsync(profil, versionChargement, identifiantJeuProfil);
        }
        catch { }
    }

    private bool ChargementSuccesRecentsEstToujoursActuel(
        int versionChargement,
        int identifiantJeuProfil
    )
    {
        return versionChargement == _versionChargementContenuJeu
            && (
                identifiantJeuProfil <= 0
                || _dernierIdentifiantJeuApi <= 0
                || _dernierIdentifiantJeuApi == identifiantJeuProfil
            );
    }

    private async Task AppliquerProgressionJeuAsync(DonneesJeuAffiche donneesJeu)
    {
        GameInfoAndUserProgressV2 jeu = donneesJeu.Jeu;
        JeuAffiche jeuAffiche = _servicePresentationJeu.Construire(donneesJeu);
        JournaliserDiagnosticChangementJeu("progression_debut", $"jeu={jeu.Id}");
        _dernierTitreJeuApi = jeu.Title;
        _dernierIdentifiantJeuAvecInfos = jeu.Id;
        _dernierIdentifiantJeuAvecProgression = jeu.Id;
        _dernieresDonneesJeuAffichees = donneesJeu;
        AppliquerVisuelsJeuEnCoursInitiaux(jeu);
        DemarrerEnrichissementVisuelsJeuEnCours(jeu);
        JournaliserDiagnosticChangementJeu("progression_visuels_ok");
        AppliquerMetaConsoleJeuEnCoursInitiale(jeu);
        DemarrerEnrichissementMetaConsoleJeuEnCours(jeu);

        DefinirTempsJeuSousImage(jeuAffiche.TempsJeu);
        DefinirEtatJeuDansProgression(jeuAffiche.Statut);
        DefinirDetailsJeuEnCours(jeuAffiche.Details);
        TexteResumeProgressionJeuEnCours.Text = jeuAffiche.ResumeProgression;
        TextePourcentageJeuEnCours.Text = jeuAffiche.PourcentageTexte;
        BarreProgressionJeuEnCours.Value = jeuAffiche.PourcentageValeur;

        await SauvegarderDernierJeuAfficheAsync(jeu, jeuAffiche.TempsJeu, jeuAffiche.Statut);
        await DetecterNouveauxSuccesJeuAsync(jeu);
        DemarrerMiseAJourCachesJeuLocaux(jeu);
        DemarrerMiseAJourSuccesJeuEnArrierePlan(jeu);
        JournaliserDiagnosticChangementJeu("progression_succes_ok");
        JournaliserDiagnosticChangementJeu("progression_fin");
    }

    private void DemarrerMiseAJourCachesJeuLocaux(GameInfoAndUserProgressV2 jeu)
    {
        _ = MettreAJourCachesJeuLocauxAsync(jeu);
    }

    private async Task MettreAJourCachesJeuLocauxAsync(GameInfoAndUserProgressV2 jeu)
    {
        try
        {
            string? titreObserveLocal =
                _identifiantJeuLocalActif == jeu.Id ? _titreJeuLocalActif : null;

            await _serviceCatalogueJeuxLocal.EnregistrerJeuAsync(jeu, titreObserveLocal);
            _ = await _serviceEtatUtilisateurJeuxLocal.EnregistrerEtatJeuAsync(jeu);
        }
        catch
        {
            // Le cache local reste purement opportuniste.
        }
    }

    private async Task DetecterNouveauxSuccesJeuAsync(GameInfoAndUserProgressV2 jeu)
    {
        List<GameAchievementV2> succesCourants = [.. jeu.Succes.Values];

        if (_identifiantJeuSuccesObserve != jeu.Id)
        {
            await DetecterNouveauxSuccesJeuDepuisCacheLocalAsync(jeu);
            _identifiantJeuSuccesObserve = jeu.Id;
            _etatSuccesObserves = _serviceDetectionSuccesJeu.CapturerEtat(succesCourants);
            ServiceDetectionSuccesJeu.JournaliserInitialisation(
                jeu.Id,
                jeu.Title,
                succesCourants.Count
            );
            return;
        }

        IReadOnlyList<SuccesDebloqueDetecte> nouveauxSucces =
            _serviceDetectionSuccesJeu.DetecterNouveauxSucces(
                jeu.Id,
                jeu.Title,
                _etatSuccesObserves,
                succesCourants
            );

        foreach (SuccesDebloqueDetecte succes in nouveauxSucces)
        {
            ServiceDetectionSuccesJeu.JournaliserDetection(succes, "session");
        }

        _etatSuccesObserves = _serviceDetectionSuccesJeu.CapturerEtat(succesCourants);
    }

    private async Task DetecterNouveauxSuccesJeuDepuisCacheLocalAsync(GameInfoAndUserProgressV2 jeu)
    {
        EtatJeuUtilisateurLocal? precedent = await _serviceEtatUtilisateurJeuxLocal.ObtenirJeuAsync(
            jeu.Id
        );

        if (precedent is null)
        {
            return;
        }

        EtatJeuUtilisateurLocal courant = ServiceEtatUtilisateurJeuxLocal.ConstruireEtatJeu(jeu);
        IReadOnlyList<EtatSuccesUtilisateurLocal> nouveauxSucces =
            _serviceDetectionSuccesUtilisateurLocal.DetecterNouveauxSucces(precedent, courant);

        foreach (EtatSuccesUtilisateurLocal succesLocal in nouveauxSucces)
        {
            if (
                !jeu.Succes.TryGetValue(
                    succesLocal.AchievementId.ToString(CultureInfo.InvariantCulture),
                    out GameAchievementV2? succesJeu
                ) || succesJeu is null
            )
            {
                continue;
            }

            ServiceDetectionSuccesJeu.JournaliserDetection(
                new SuccesDebloqueDetecte
                {
                    IdentifiantJeu = jeu.Id,
                    TitreJeu = jeu.Title?.Trim() ?? string.Empty,
                    IdentifiantSucces = succesJeu.Id,
                    TitreSucces = succesJeu.Title?.Trim() ?? string.Empty,
                    Points = succesJeu.Points,
                    Hardcore = succesLocal.EstHardcore,
                    DateObtention = succesLocal.EstHardcore
                        ? succesLocal.DateDeblocageHardcoreUtc
                        : succesLocal.DateDeblocageUtc,
                },
                "cache_local"
            );
        }
    }
}
