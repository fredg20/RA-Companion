/*
 * Regroupe le chargement du jeu courant depuis l'API, les enrichissements
 * différés qui suivent et les gardes nécessaires pour éviter que des retours
 * asynchrones tardifs ne dégradent l'état visible de la fenêtre.
 */
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

/*
 * Porte la partie de la fenêtre principale qui orchestre le chargement du jeu
 * courant, de sa progression et des données enrichies associées.
 */
public partial class MainWindow
{
    /*
     * Met à jour l'indicateur discret de synchronisation du jeu courant.
     */
    private void DefinirEtatSynchronisationJeu(string texte)
    {
        _vueModele.EtatSynchronisationJeu = texte;
        _vueModele.VisibiliteSynchronisationJeu = string.IsNullOrWhiteSpace(texte)
            ? Visibility.Hidden
            : Visibility.Visible;
    }

    /*
     * Réinitialise l'indicateur visuel de synchronisation du jeu.
     */
    private void ReinitialiserEtatSynchronisationJeu()
    {
        DefinirEtatSynchronisationJeu(string.Empty);
    }

    /*
     * Lance le chargement du jeu courant depuis le profil utilisateur et
     * recompose l'interface principale à partir du résultat obtenu.
     */
    private async Task ChargerJeuEnCoursAsync(
        bool afficherEtatChargement = true,
        bool forcerChargementJeu = true,
        bool forcerChargementSansCache = false
    )
    {
        App.JournaliserDemarrage("ChargerJeuEnCours début");
        JournaliserDiagnosticChangementJeu(
            "charger_jeu_debut",
            $"forcer={forcerChargementJeu};chargement={afficherEtatChargement};sans_cache={forcerChargementSansCache}"
        );

        if (!ConfigurationConnexionEstComplete())
        {
            ReinitialiserEtatSynchronisationJeu();
            ReinitialiserJeuEnCours();
            return;
        }

        if (_chargementJeuEnCoursActif)
        {
            return;
        }

        _chargementJeuEnCoursActif = true;
        MettreAJourActionRechargerJeuEnCours();

        if (
            !afficherEtatChargement
            && (
                _configurationConnexion.DernierJeuAffiche is not null
                || _dernieresDonneesJeuAffichees is not null
            )
        )
        {
            DefinirEtatSynchronisationJeu("Synchronisation...");
        }

        try
        {
            if (afficherEtatChargement)
            {
                ReinitialiserEtatSynchronisationJeu();
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

            App.JournaliserDemarrage("ChargerJeuEnCours après Profil");
            JournaliserDiagnosticChangementJeu("charger_jeu_profil_charge");
            _profilUtilisateurAccessible = true;
            _dernierProfilUtilisateurCharge = profil;
            _dernierResumeUtilisateurCharge = null;
            DefinirEtatConnexion("Connecté");
            await AppliquerProfilUtilisateurAsync(
                profil,
                forcerChargementJeu,
                forcerChargementSansCache
            );
            App.JournaliserDemarrage("ChargerJeuEnCours après AppliquerProfil");
            DemarrerChargementSuccesRecentsEnArrierePlan(
                profil,
                _versionChargementContenuJeu,
                profil.LastGameId
            );
            App.JournaliserDemarrage("ChargerJeuEnCours après SuccèsRécents");
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
                ReinitialiserEtatSynchronisationJeu();
                ReinitialiserSuccesRecents();
                TexteEtatSuccesRecents.Text =
                    "Impossible de charger les succès récents pour ce compte.";
                return;
            }

            ReinitialiserEtatSynchronisationJeu();
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
            ReinitialiserEtatSynchronisationJeu();

            if (afficherEtatChargement)
            {
                if (!EnregistrerPhaseErreurChargementOrchestrateur(0, string.Empty, "api"))
                {
                    ReinitialiserEtatSynchronisationJeu();
                    ReinitialiserSuccesRecents();
                    return;
                }

                ReinitialiserEtatSynchronisationJeu();
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
            MettreAJourActionRechargerJeuEnCours();
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

    /*
     * Applique un profil utilisateur chargé en décidant s'il faut recharger
     * le jeu courant, réutiliser un état existant ou afficher un état vide.
     */
    private async Task AppliquerProfilUtilisateurAsync(
        UserProfileV2 profil,
        bool forcerChargementJeu,
        bool forcerChargementSansCache
    )
    {
        DefinirTitreZoneJeu();
        EtatRichPresence etatRichPresence = ServiceSondeRichPresence.Sonder(
            new DonneesCompteUtilisateur
            {
                Profil = profil,
                Resume = _dernierResumeUtilisateurCharge,
            },
            journaliser: false
        );

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
                ReinitialiserEtatSynchronisationJeu();
                return;
            }

            ReinitialiserEtatSynchronisationJeu();
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
            ReinitialiserEtatSynchronisationJeu();
            return;
        }

        titreJeuProvisoire = DeterminerTitreJeuApiProvisoire(
            profil.LastGame,
            dernierJeuJoue?.Title
        );

        if (
            !EtatLocalJeuEstActif()
            && string.Equals(
                etatRichPresence.StatutAffiche,
                "Dernier jeu",
                StringComparison.OrdinalIgnoreCase
            )
            && _configurationConnexion.DernierJeuAffiche?.Id > 0
        )
        {
            identifiantJeuEffectif = _configurationConnexion.DernierJeuAffiche.Id;

            if (!string.IsNullOrWhiteSpace(_configurationConnexion.DernierJeuAffiche.Title))
            {
                titreJeuProvisoire = _configurationConnexion.DernierJeuAffiche.Title;
            }
        }

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
            ReinitialiserEtatSynchronisationJeu();
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
            versionChargement,
            forcerChargementSansCache
        );
    }

    /*
     * Synchronise le pseudo local avec le profil reçu afin de garder une
     * configuration persistante cohérente.
     */
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

        try
        {
            await _serviceConfigurationLocale.SauvegarderAsync(_configurationConnexion);
        }
        catch { }
    }

    /*
     * Lance en arrière-plan le chargement détaillé du jeu utilisateur sans
     * bloquer l'affichage initial déjà disponible.
     */
    private void DemarrerChargementJeuUtilisateurEnArrierePlan(
        int identifiantJeuEffectif,
        string titreJeuProvisoire,
        bool infosJeuDejaAfficheesPourCeJeu,
        bool progressionDejaAfficheePourCeJeu,
        int versionChargement,
        bool forcerChargementSansCache
    )
    {
        _ = ChargerJeuUtilisateurEnArrierePlanAsync(
            identifiantJeuEffectif,
            titreJeuProvisoire,
            infosJeuDejaAfficheesPourCeJeu,
            progressionDejaAfficheePourCeJeu,
            versionChargement,
            forcerChargementSansCache
        );
    }

    /*
     * Charge le jeu utilisateur en arrière-plan et ignore les erreurs qui
     * ne doivent pas casser le rendu principal déjà affiché.
     */
    private async Task ChargerJeuUtilisateurEnArrierePlanAsync(
        int identifiantJeuEffectif,
        string titreJeuProvisoire,
        bool infosJeuDejaAfficheesPourCeJeu,
        bool progressionDejaAfficheePourCeJeu,
        int versionChargement,
        bool forcerChargementSansCache
    )
    {
        try
        {
            DonneesJeuAffiche donneesJeu = forcerChargementSansCache
                ? await _serviceJeuRetroAchievements.ObtenirDonneesJeuRapidesSansCacheAsync(
                    _configurationConnexion.Pseudo,
                    _configurationConnexion.CleApiWeb,
                    identifiantJeuEffectif
                )
                : await _serviceJeuRetroAchievements.ObtenirDonneesJeuRapidesAsync(
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

    /*
     * Enrichit le jeu courant avec des informations complémentaires une fois
     * le chargement principal terminé.
     */
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

                if (
                    SuccesEnCoursDoitEtreRafraichi(
                        identifiantJeuEffectif,
                        donneesEnrichies.Jeu.Succes.Count
                    )
                )
                {
                    JournaliserDiagnosticChangementJeu(
                        "pipeline_idempotent_enrichissement_repare_succes",
                        $"jeu={identifiantJeuEffectif};succes={donneesEnrichies.Jeu.Succes.Count}"
                    );
                    await MettreAJourSuccesJeuEnArrierePlanAsync(donneesEnrichies.Jeu);
                }

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
                await MettreAJourSuccesJeuEnArrierePlanAsync(jeu);
            }
        }
        catch { }
    }

    /*
     * Enrichit la zone communauté du jeu courant sans bloquer le reste de
     * l'interface si cette étape échoue.
     */
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
        catch { }
    }

    /*
     * Vérifie qu'un retour asynchrone concerne encore le jeu et la version
     * de chargement actuellement visibles.
     */
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

    /*
     * Charge les succès récents de l'utilisateur pour le jeu courant.
     */
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

    /*
     * Lance en arrière-plan le chargement des succès récents affichés dans
     * la zone dédiée de l'interface.
     */
    private void DemarrerChargementSuccesRecentsEnArrierePlan(
        UserProfileV2 profil,
        int versionChargement,
        int identifiantJeuProfil
    )
    {
        _ = ChargerSuccesRecentsEnArrierePlanAsync(profil, versionChargement, identifiantJeuProfil);
    }

    /*
     * Exécute le chargement des succès récents en arrière-plan en tolérant
     * les échecs non critiques.
     */
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

    /*
     * Vérifie qu'un retour de succès récents correspond encore au contexte
     * de jeu actuellement affiché.
     */
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

    /*
     * Applique à l'interface la progression et les données principales
     * du jeu courant.
     */
    private async Task AppliquerProgressionJeuAsync(DonneesJeuAffiche donneesJeu)
    {
        donneesJeu = FusionnerDonneesJeuSansRegression(donneesJeu, _dernieresDonneesJeuAffichees);

        if (DonneesJeuSontIdempotentes(donneesJeu, _dernieresDonneesJeuAffichees))
        {
            _dernieresDonneesJeuAffichees = donneesJeu;
            ReinitialiserEtatSynchronisationJeu();
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

            if (SuccesEnCoursDoitEtreRafraichi(donneesJeu.Jeu.Id, donneesJeu.Jeu.Succes.Count))
            {
                JournaliserDiagnosticChangementJeu(
                    "pipeline_idempotent_progression_repare_succes",
                    $"jeu={donneesJeu.Jeu.Id};succes={donneesJeu.Jeu.Succes.Count}"
                );
                await MettreAJourSuccesJeuEnArrierePlanAsync(donneesJeu.Jeu);
            }

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
        _vueModele.JeuCourant.Progression = jeuAffiche.ResumeProgression;
        _vueModele.JeuCourant.Pourcentage = jeuAffiche.PourcentageTexte;
        _vueModele.JeuCourant.ProgressionValeur = jeuAffiche.PourcentageValeur;
        MettreAJourActionVueDetailleeJeuEnCours(jeu);
        _ = InitialiserContexteSuccesJeu(jeu, out _);

        await SauvegarderDernierJeuAfficheAsync(jeu, jeuAffiche.TempsJeu, jeuAffiche.Statut);
        await DetecterNouveauxSuccesJeuAsync(jeu);
        DemarrerMiseAJourCachesJeuLocaux(jeu);
        await MettreAJourSuccesJeuEnArrierePlanAsync(jeu);
        ReinitialiserEtatSynchronisationJeu();
        JournaliserDiagnosticChangementJeu("progression_succes_ok");
        JournaliserDiagnosticChangementJeu("progression_fin");
    }

    /*
     * Fusionne un nouvel état de jeu avec l'état déjà affiché pour éviter
     * les régressions visuelles lors des enrichissements successifs.
     */
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

    /*
     * Indique si deux états de jeu peuvent être considérés équivalents
     * pour les besoins d'un rechargement idempotent.
     */
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

    /*
     * Lance la mise à jour des caches locaux dérivés du jeu courant.
     */
    private void DemarrerMiseAJourCachesJeuLocaux(GameInfoAndUserProgressV2 jeu)
    {
        _ = MettreAJourCachesJeuLocauxAsync(jeu);
    }

    /*
     * Met à jour les caches locaux associés au jeu courant.
     */
    private async Task MettreAJourCachesJeuLocauxAsync(GameInfoAndUserProgressV2 jeu)
    {
        try
        {
            string? titreObserveLocal =
                _identifiantJeuLocalActif == jeu.Id ? _titreJeuLocalActif : null;

            await _serviceCatalogueJeuxLocal.EnregistrerJeuAsync(jeu, titreObserveLocal);
            _ = await _serviceEtatUtilisateurJeuxLocal.EnregistrerEtatJeuAsync(jeu);
        }
        catch { }
    }

    /*
     * Détecte les nouveaux succès du jeu courant à partir des données API
     * les plus récentes.
     */
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

        string sourceDetection = DeterminerSourceDetectionDepuisContexteLocalRecent(jeu.Id);

        foreach (SuccesDebloqueDetecte succes in nouveauxSuccesFiltres)
        {
            ServiceDetectionSuccesJeu.JournaliserDetection(succes, sourceDetection);
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

    /*
     * Complète la détection des nouveaux succès en la confrontant au cache
     * local persisté de l'utilisateur.
     */
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

    /*
     * Sélectionne le succès débloqué le plus récent parmi les candidats
     * détectés pendant le chargement.
     */
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

    /*
     * Convertit un texte de date de déblocage en horodatage comparable.
     */
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
