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

            UserProfileV2 profil = await ServiceUtilisateurRetroAchievements.ObtenirProfilAsync(
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

            bool afficherErreur = EnregistrerPhaseErreurChargementOrchestrateur(
                0,
                string.Empty,
                "profil_inaccessible"
            );

            if (!afficherErreur)
            {
                ReinitialiserSuccesRecents();
                TexteEtatSuccesRecents.Text =
                    "Impossible de charger les succès récents pour ce compte.";
                return;
            }

            ReinitialiserMetaConsoleJeuEnCours();
            ReinitialiserPremierSuccesNonDebloque();
            ReinitialiserGrilleTousSucces();
            DefinirTempsJeuSousImage(string.Empty);
            DefinirEtatJeuDansProgression(string.Empty);
            DefinirTitreJeuEnCours(string.Empty);
            DefinirDetailsJeuEnCours(string.Empty);
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
                if (!EnregistrerPhaseErreurChargementOrchestrateur(0, string.Empty, "api"))
                {
                    ReinitialiserSuccesRecents();
                    return;
                }

                ReinitialiserMetaConsoleJeuEnCours();
                ReinitialiserPremierSuccesNonDebloque();
                ReinitialiserGrilleTousSucces();
                DefinirTempsJeuSousImage(string.Empty);
                DefinirEtatJeuDansProgression(string.Empty);
                DefinirTitreJeuEnCours(string.Empty);
                DefinirDetailsJeuEnCours(string.Empty);
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
            if (!EnregistrerPhaseAucunJeuOrchestrateur("aucun_jeu_recent"))
            {
                return;
            }

            ReinitialiserMetaConsoleJeuEnCours();
            ReinitialiserCarrouselVisuelsJeuEnCours();
            ReinitialiserPremierSuccesNonDebloque();
            ReinitialiserGrilleTousSucces();
            DefinirTempsJeuSousImage(string.Empty);
            DefinirEtatJeuDansProgression(string.Empty);
            DefinirTitreJeuEnCours(string.Empty);
            DefinirDetailsJeuEnCours(string.Empty);
            return;
        }

        if (
            _serviceOrchestrateurEtatJeu.DoitIgnorerChargementApi(
                identifiantJeuEffectif,
                forcerChargementJeu
            )
        )
        {
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
            EnregistrerPhaseChargementApiOrchestrateur(
                identifiantJeuEffectif,
                titreAffichageInitial,
                EtatLocalJeuEstActif() ? "api_local" : "api"
            );
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
        EnregistrerPhaseChargementApiOrchestrateur(
            identifiantJeuEffectif,
            titreAffichageInitial,
            EtatLocalJeuEstActif() ? "api_local" : "api",
            false
        );
        int versionChargement = ++_versionChargementContenuJeu;
        DemarrerPipelineChargementJeu(
            identifiantJeuEffectif,
            titreAffichageInitial,
            versionChargement
        );
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
                bool afficherErreur = EnregistrerPhaseErreurChargementOrchestrateur(
                    identifiantJeuEffectif,
                    titreJeuProvisoire,
                    "api_background"
                );

                if (!afficherErreur)
                {
                    return;
                }
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
            int nombreSuccesAvant = _succesJeuCourant.Count;
            donneesEnrichies.Communaute = donneesCourantes.Communaute;
            donneesEnrichies.CommunauteAffichee = donneesCourantes.CommunauteAffichee;
            donneesEnrichies = FusionnerDonneesJeuSansRegression(
                donneesEnrichies,
                donneesCourantes
            );

            if (DonneesJeuSontIdempotentes(donneesEnrichies, donneesCourantes))
            {
                _dernieresDonneesJeuAffichees = donneesEnrichies;
                JournaliserDiagnosticChangementJeu(
                    "pipeline_idempotent_enrichissement",
                    $"jeu={identifiantJeuEffectif};succes={donneesEnrichies.Jeu.Succes.Count}"
                );
                MarquerEtapePipelineChargementJeu(
                    EtapePipelineChargementJeu.MetadonneesEnrichies,
                    identifiantJeuEffectif,
                    versionChargement
                );
                return;
            }

            _dernieresDonneesJeuAffichees = donneesEnrichies;

            GameInfoAndUserProgressV2 jeu = donneesEnrichies.Jeu;
            JeuAffiche jeuAffiche = ServicePresentationJeu.Construire(donneesEnrichies);
            MarquerEtapePipelineChargementJeu(
                EtapePipelineChargementJeu.MetadonneesEnrichies,
                jeu.Id,
                versionChargement
            );

            _dernierTitreJeuApi = jeu.Title;
            AppliquerMetaConsoleJeuEnCoursInitiale(jeu);
            DemarrerEnrichissementMetaConsoleJeuEnCours(jeu);
            DefinirTempsJeuSousImage(jeuAffiche.TempsJeu);
            DefinirEtatJeuDansProgression(jeuAffiche.Statut);
            DefinirDetailsJeuEnCours(jeuAffiche.Details);
            MettreAJourActionVueDetailleeJeuEnCours(jeu);

            int nombreSuccesApres = jeu.Succes.Count;

            if (
                _identifiantJeuSuccesCourant != identifiantJeuEffectif
                || nombreSuccesApres > nombreSuccesAvant
                || GrilleTousSuccesJeuEnCours.Children.Count < nombreSuccesApres
            )
            {
                JournaliserDiagnosticChangementJeu(
                    "progression_succes_enrichis_recharge",
                    $"jeu={identifiantJeuEffectif};avant={nombreSuccesAvant};apres={nombreSuccesApres};badges={GrilleTousSuccesJeuEnCours.Children.Count}"
                );
                DemarrerMiseAJourSuccesJeuEnArrierePlan(jeu);
            }
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
                ServicePresentationCommunaute.Construire(donneesCommunaute);

            JeuAffiche jeuAffiche = ServicePresentationJeu.Construire(
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
            && _dernierIdentifiantJeuApi == identifiantJeuEffectif
            && PipelineChargementJeuEstActuel(identifiantJeuEffectif, versionChargement);
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
                await ServiceActiviteRetroAchievements.ObtenirActiviteRecenteAsync(
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
                ServicePresentationActivite.Construire(activiteRecente, profil.LastGameId)
            );
        }
        catch
        {
            if (!ChargementSuccesRecentsEstToujoursActuel(versionChargement, identifiantJeuProfil))
            {
                return;
            }

            AppliquerSuccesRecents(ServicePresentationActivite.ConstruireErreur());
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
                || PipelineChargementJeuEstActuel(identifiantJeuProfil, versionChargement)
            )
            && (
                identifiantJeuProfil <= 0
                || _dernierIdentifiantJeuApi <= 0
                || _dernierIdentifiantJeuApi == identifiantJeuProfil
            );
    }

    private async Task AppliquerProgressionJeuAsync(DonneesJeuAffiche donneesJeu)
    {
        donneesJeu = FusionnerDonneesJeuSansRegression(donneesJeu, _dernieresDonneesJeuAffichees);

        if (DonneesJeuSontIdempotentes(donneesJeu, _dernieresDonneesJeuAffichees))
        {
            _dernieresDonneesJeuAffichees = donneesJeu;
            JournaliserDiagnosticChangementJeu(
                "pipeline_idempotent_progression",
                $"jeu={donneesJeu.Jeu.Id};succes={donneesJeu.Jeu.Succes.Count}"
            );
            MarquerEtapePipelineChargementJeu(
                EtapePipelineChargementJeu.DonneesMinimales
                    | EtapePipelineChargementJeu.SuccesCharges,
                donneesJeu.Jeu.Id,
                _versionChargementContenuJeu
            );
            return;
        }

        GameInfoAndUserProgressV2 jeu = donneesJeu.Jeu;
        JeuAffiche jeuAffiche = ServicePresentationJeu.Construire(donneesJeu);
        JournaliserDiagnosticChangementJeu("progression_debut", $"jeu={jeu.Id}");
        EnregistrerPhaseJeuAfficheOrchestrateur(jeu.Id, jeu.Title, "progression");
        _dernierTitreJeuApi = jeu.Title;
        _dernierIdentifiantJeuAvecInfos = jeu.Id;
        _dernierIdentifiantJeuAvecProgression = jeu.Id;
        _dernieresDonneesJeuAffichees = donneesJeu;
        MarquerEtapePipelineChargementJeu(
            EtapePipelineChargementJeu.DonneesMinimales,
            jeu.Id,
            _versionChargementContenuJeu
        );
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
        MettreAJourActionVueDetailleeJeuEnCours(jeu);
        _ = InitialiserContexteSuccesJeu(jeu, out _);

        await SauvegarderDernierJeuAfficheAsync(jeu, jeuAffiche.TempsJeu, jeuAffiche.Statut);
        await DetecterNouveauxSuccesJeuAsync(jeu);
        DemarrerMiseAJourCachesJeuLocaux(jeu);
        DemarrerMiseAJourSuccesJeuEnArrierePlan(jeu);
        MarquerEtapePipelineChargementJeu(
            EtapePipelineChargementJeu.SuccesCharges,
            jeu.Id,
            _versionChargementContenuJeu
        );
        JournaliserDiagnosticChangementJeu("progression_succes_ok");
        JournaliserDiagnosticChangementJeu("progression_fin");
    }

    private DonneesJeuAffiche FusionnerDonneesJeuSansRegression(
        DonneesJeuAffiche donneesNouvelles,
        DonneesJeuAffiche? donneesExistantes
    )
    {
        if (
            donneesExistantes is null
            || donneesExistantes.Jeu.Id <= 0
            || donneesExistantes.Jeu.Id != donneesNouvelles.Jeu.Id
        )
        {
            return donneesNouvelles;
        }

        GameInfoAndUserProgressV2 jeuNouveau = donneesNouvelles.Jeu;
        GameInfoAndUserProgressV2 jeuExistant = donneesExistantes.Jeu;
        int nbSuccesNouveaux = jeuNouveau.Succes.Count;
        int nbSuccesExistants = jeuExistant.Succes.Count;

        if (nbSuccesExistants > nbSuccesNouveaux)
        {
            jeuNouveau.Succes = jeuExistant.Succes.ToDictionary(
                item => item.Key,
                item => item.Value
            );
            jeuNouveau.NombreSucces = Math.Max(
                Math.Max(jeuNouveau.NombreSucces, nbSuccesExistants),
                jeuExistant.NombreSucces
            );
            JournaliserDiagnosticChangementJeu(
                "pipeline_non_regression_succes",
                $"jeu={jeuNouveau.Id};avant={nbSuccesNouveaux};conserve={nbSuccesExistants}"
            );
        }

        return new DonneesJeuAffiche
        {
            Jeu = jeuNouveau,
            DetailsEtendus = donneesNouvelles.DetailsEtendus ?? donneesExistantes.DetailsEtendus,
            Progression = donneesNouvelles.Progression ?? donneesExistantes.Progression,
            RangsEtScores =
                donneesNouvelles.RangsEtScores.Count > 0
                    ? donneesNouvelles.RangsEtScores
                    : donneesExistantes.RangsEtScores,
            Communaute = donneesNouvelles.Communaute ?? donneesExistantes.Communaute,
            CommunauteAffichee =
                donneesNouvelles.CommunauteAffichee ?? donneesExistantes.CommunauteAffichee,
        };
    }

    private static bool DonneesJeuSontIdempotentes(
        DonneesJeuAffiche donneesNouvelles,
        DonneesJeuAffiche? donneesExistantes
    )
    {
        if (
            donneesExistantes is null
            || donneesExistantes.Jeu.Id <= 0
            || donneesExistantes.Jeu.Id != donneesNouvelles.Jeu.Id
        )
        {
            return false;
        }

        if (donneesNouvelles.DetailsEtendus is not null && donneesExistantes.DetailsEtendus is null)
        {
            return false;
        }

        if (donneesNouvelles.Progression is not null && donneesExistantes.Progression is null)
        {
            return false;
        }

        if (donneesNouvelles.RangsEtScores.Count > donneesExistantes.RangsEtScores.Count)
        {
            return false;
        }

        Dictionary<string, GameAchievementV2> succesNouveaux = donneesNouvelles.Jeu.Succes;
        Dictionary<string, GameAchievementV2> succesExistants = donneesExistantes.Jeu.Succes;

        if (succesNouveaux.Count != succesExistants.Count)
        {
            return false;
        }

        foreach ((string cleSucces, GameAchievementV2 succesNouveau) in succesNouveaux)
        {
            if (
                !succesExistants.TryGetValue(cleSucces, out GameAchievementV2? succesExistant)
                && !succesExistants.TryGetValue(
                    succesNouveau.Id.ToString(CultureInfo.InvariantCulture),
                    out succesExistant
                )
            )
            {
                return false;
            }

            if (
                !string.Equals(
                    succesNouveau.DateEarned,
                    succesExistant.DateEarned,
                    StringComparison.Ordinal
                )
                || !string.Equals(
                    succesNouveau.DateEarnedHardcore,
                    succesExistant.DateEarnedHardcore,
                    StringComparison.Ordinal
                )
            )
            {
                return false;
            }
        }

        return true;
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
            _etatSuccesObserves = ServiceDetectionSuccesJeu.CapturerEtat(succesCourants);
            ServiceDetectionSuccesJeu.JournaliserInitialisation(
                jeu.Id,
                jeu.Title,
                succesCourants.Count
            );
            return;
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

        foreach (SuccesDebloqueDetecte succes in nouveauxSuccesFiltres)
        {
            ServiceDetectionSuccesJeu.JournaliserDetection(succes, "session");
            MarquerSuccesCommeTraite(succes);
        }

        SuccesDebloqueDetecte? succesLePlusRecent = SelectionnerSuccesDebloqueLePlusRecent(
            nouveauxSuccesFiltres
        );

        if (succesLePlusRecent is not null)
        {
            _ = await AfficherSuccesDebloqueDetecteAsync(succesLePlusRecent);
        }

        _etatSuccesObserves = ServiceDetectionSuccesJeu.CapturerEtat(succesCourants);
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
            ServiceDetectionSuccesUtilisateurLocal.DetecterNouveauxSucces(precedent, courant);

        List<SuccesDebloqueDetecte> succesFiltresAAfficher = [];

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

            SuccesDebloqueDetecte succesDetecte = new()
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
            };

            if (SuccesDejaTraiteRecemment(succesDetecte))
            {
                continue;
            }

            ServiceDetectionSuccesJeu.JournaliserDetection(succesDetecte, "cache_local");
            MarquerSuccesCommeTraite(succesDetecte);
            succesFiltresAAfficher.Add(succesDetecte);
        }

        SuccesDebloqueDetecte? succesAAfficher = SelectionnerSuccesDebloqueLePlusRecent(
            succesFiltresAAfficher
        );

        if (succesAAfficher is not null)
        {
            _ = await AfficherSuccesDebloqueDetecteAsync(succesAAfficher);
        }
    }

    private static SuccesDebloqueDetecte? SelectionnerSuccesDebloqueLePlusRecent(
        IReadOnlyList<SuccesDebloqueDetecte> succesDetectes
    )
    {
        if (succesDetectes.Count == 0)
        {
            return null;
        }

        return succesDetectes
            .OrderByDescending(succes => ConvertirDateDeblocageSucces(succes.DateObtention))
            .ThenByDescending(succes => succes.Hardcore)
            .ThenByDescending(succes => succes.IdentifiantSucces)
            .FirstOrDefault();
    }

    private static DateTimeOffset ConvertirDateDeblocageSucces(string? valeur)
    {
        if (
            DateTimeOffset.TryParse(
                valeur,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AllowWhiteSpaces,
                out DateTimeOffset date
            )
        )
        {
            return date;
        }

        return DateTimeOffset.MinValue;
    }
}
