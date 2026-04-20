using System.IO;
using RA.Compagnon.Modeles.Api.V2.User;
using RA.Compagnon.Modeles.Catalogue;
using RA.Compagnon.Modeles.Etat;
using RA.Compagnon.Modeles.Local;
using RA.Compagnon.Modeles.Presentation;
using RA.Compagnon.Services;

/*
 * Regroupe les aides liées au jeu local résolu, au dernier jeu rejouable et
 * au préchargement local de l'affichage avant la réponse complète de l'API.
 */
namespace RA.Compagnon;

/*
 * Porte la logique qui relie la détection locale d'un jeu aux données
 * affichées et aux actions de rejeu de la fenêtre principale.
 */
public partial class MainWindow
{
    /*
     * Compare le dernier jeu fourni par l'API au dernier jeu sauvegardé
     * localement pour savoir si les deux sources décrivent bien le même jeu.
     */
    private bool DernierJeuApiCorrespondAuJeuSauvegarde(int identifiantJeuApi, string? titreJeuApi)
    {
        EtatJeuAfficheLocal? jeuSauvegarde = _configurationConnexion.DernierJeuAffiche;

        if (jeuSauvegarde is null)
        {
            return false;
        }

        if (identifiantJeuApi > 0 && jeuSauvegarde.Id > 0 && identifiantJeuApi != jeuSauvegarde.Id)
        {
            return false;
        }

        string titreApiNormalise = NormaliserTitreComparaisonJeu(titreJeuApi);
        string titreSauvegardeNormalise = NormaliserTitreComparaisonJeu(jeuSauvegarde.Title);

        if (
            !string.IsNullOrWhiteSpace(titreApiNormalise)
            && !string.IsNullOrWhiteSpace(titreSauvegardeNormalise)
        )
        {
            return string.Equals(
                titreApiNormalise,
                titreSauvegardeNormalise,
                StringComparison.OrdinalIgnoreCase
            );
        }

        return identifiantJeuApi > 0
            && jeuSauvegarde.Id > 0
            && identifiantJeuApi == jeuSauvegarde.Id;
    }

    /*
     * Nettoie un titre de jeu avant comparaison afin d'éviter les écarts
     * purement visuels liés aux espaces.
     */
    private static string NormaliserTitreComparaisonJeu(string? titre)
    {
        return string.IsNullOrWhiteSpace(titre) ? string.Empty : titre.Trim();
    }

    /*
     * Récupère le dernier jeu joué depuis le profil RetroAchievements.
     */
    private async Task<RecentlyPlayedGameV2?> ObtenirDernierJeuJoueAsync()
    {
        try
        {
            IReadOnlyList<RecentlyPlayedGameV2> jeuxRecents =
                await ServiceUtilisateurRetroAchievements.ObtenirJeuxRecemmentJouesAsync(
                    _configurationConnexion.Pseudo,
                    _configurationConnexion.CleApiWeb
                );

            return jeuxRecents.Count > 0 ? jeuxRecents[0] : null;
        }
        catch
        {
            return null;
        }
    }

    /*
     * Détermine un titre provisoire côté API à partir des différentes sources
     * disponibles dans le profil utilisateur.
     */
    private static string DeterminerTitreJeuApiProvisoire(
        string nomDernierJeuProfil,
        string? titreDernierJeuRecent
    )
    {
        if (!string.IsNullOrWhiteSpace(nomDernierJeuProfil))
        {
            return nomDernierJeuProfil;
        }

        if (!string.IsNullOrWhiteSpace(titreDernierJeuRecent))
        {
            return titreDernierJeuRecent;
        }

        return string.Empty;
    }

    /*
     * Indique si le Rich Presence confirme bien que l'affichage doit se
     * positionner sur un dernier jeu précis plutôt que sur un autre état.
     */
    private static bool RichPresenceConfirmeDernierJeu(
        EtatRichPresence etatRichPresence,
        int identifiantJeuAttendu
    )
    {
        if (
            !string.Equals(
                etatRichPresence.StatutAffiche,
                "Dernier jeu",
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            return false;
        }

        if (identifiantJeuAttendu <= 0)
        {
            return etatRichPresence.IdentifiantDernierJeu > 0;
        }

        return etatRichPresence.IdentifiantDernierJeu > 0
            && etatRichPresence.IdentifiantDernierJeu == identifiantJeuAttendu;
    }

    /*
     * Indique si l'affichage doit rester verrouillé sur le dernier jeu actif
     * récemment plutôt que d'être réinitialisé.
     */
    private bool DoitVerrouillerAffichageSurDernierJeuActifRecemment()
    {
        EtatJeuAfficheLocal? dernierJeuSauvegarde = _configurationConnexion.DernierJeuAffiche;

        if (EtatLocalJeuEstActif() || dernierJeuSauvegarde is null || dernierJeuSauvegarde.Id <= 0)
        {
            return false;
        }

        EtatRichPresence etatRichPresence = ServiceSondeRichPresence.Sonder(
            new DonneesCompteUtilisateur
            {
                Profil = _dernierProfilUtilisateurCharge,
                Resume = _dernierResumeUtilisateurCharge,
            },
            journaliser: false
        );

        return RichPresenceConfirmeDernierJeu(etatRichPresence, dernierJeuSauvegarde.Id);
    }

    /*
     * Réapplique l'état du dernier jeu sauvegardé lorsqu'il doit rester
     * visible malgré l'absence d'un jeu local actif.
     */
    private void ReappliquerDernierJeuActifRecemment()
    {
        if (!DoitVerrouillerAffichageSurDernierJeuActifRecemment())
        {
            return;
        }

        EtatJeuAfficheLocal? jeuSauvegarde = _configurationConnexion.DernierJeuAffiche;

        if (jeuSauvegarde is null || string.IsNullOrWhiteSpace(jeuSauvegarde.Title))
        {
            return;
        }

        HydraterActionRejouerDepuisSourcesLocalesActifRecemment(jeuSauvegarde);
        AppliquerEtatJeuSauvegarde(jeuSauvegarde);
    }

    /*
     * Tente d'enrichir le contexte de rejeu du dernier jeu affiché à partir
     * des sources locales actuellement détectables.
     */
    private void HydraterActionRejouerDepuisSourcesLocalesActifRecemment(
        EtatJeuAfficheLocal jeuSauvegarde
    )
    {
        if (jeuSauvegarde.Id <= 0)
        {
            return;
        }

        bool contexteRelanceActuelValide =
            !string.IsNullOrWhiteSpace(jeuSauvegarde.NomEmulateurRelance)
            && !string.IsNullOrWhiteSpace(jeuSauvegarde.CheminExecutableEmulateur)
            && !string.IsNullOrWhiteSpace(jeuSauvegarde.CheminJeuLocal)
            && File.Exists(jeuSauvegarde.CheminExecutableEmulateur)
            && File.Exists(jeuSauvegarde.CheminJeuLocal);

        foreach (
            DefinitionEmulateurLocal definition in ServiceCatalogueEmulateursLocaux.Definitions.Where(
                ServiceCatalogueEmulateursLocaux.EstEmulateurValide
            )
        )
        {
            if (
                !ServiceSondeLocaleEmulateurs.EssayerObtenirContexteRejouerDepuisSources(
                    definition.NomEmulateur,
                    out int identifiantJeu,
                    out _,
                    out string cheminExecutable,
                    out string cheminJeu
                )
                || identifiantJeu != jeuSauvegarde.Id
            )
            {
                continue;
            }

            bool modifie =
                !contexteRelanceActuelValide
                || !string.Equals(
                    jeuSauvegarde.NomEmulateurRelance,
                    definition.NomEmulateur,
                    StringComparison.Ordinal
                )
                || !string.Equals(
                    jeuSauvegarde.CheminExecutableEmulateur,
                    cheminExecutable,
                    StringComparison.OrdinalIgnoreCase
                )
                || !string.Equals(
                    jeuSauvegarde.CheminJeuLocal,
                    cheminJeu,
                    StringComparison.OrdinalIgnoreCase
                );

            if (modifie)
            {
                jeuSauvegarde.NomEmulateurRelance = definition.NomEmulateur;
                jeuSauvegarde.CheminExecutableEmulateur = cheminExecutable;
                jeuSauvegarde.CheminJeuLocal = cheminJeu;
                _dernierJeuAfficheModifie = true;
                _ = PersisterDernierJeuAfficheSiNecessaireAsync();
            }

            break;
        }

        if (contexteRelanceActuelValide || string.IsNullOrWhiteSpace(jeuSauvegarde.Title))
        {
            return;
        }

        foreach (
            DefinitionEmulateurLocal definition in ServiceCatalogueEmulateursLocaux.Definitions.Where(
                ServiceCatalogueEmulateursLocaux.EstEmulateurValide
            )
        )
        {
            if (
                !ServiceSondeLocaleEmulateurs.EssayerObtenirContexteRejouerDepuisTitre(
                    definition.NomEmulateur,
                    jeuSauvegarde.Title,
                    out string cheminExecutable,
                    out string cheminJeu
                )
            )
            {
                continue;
            }

            jeuSauvegarde.NomEmulateurRelance = definition.NomEmulateur;
            jeuSauvegarde.CheminExecutableEmulateur = cheminExecutable;
            jeuSauvegarde.CheminJeuLocal = cheminJeu;
            _dernierJeuAfficheModifie = true;
            _ = PersisterDernierJeuAfficheSiNecessaireAsync();
            break;
        }
    }

    /*
     * Affiche un titre provisoire issu de la sonde locale lorsqu'aucune
     * donnée API plus fiable n'est encore disponible.
     */
    private void AppliquerTitreJeuLocalProvisoire(EtatSondeLocaleEmulateur etat)
    {
        if (DoitVerrouillerAffichageSurDernierJeuActifRecemment())
        {
            ReappliquerDernierJeuActifRecemment();
            return;
        }

        string titreJeuProbable = etat.TitreJeuProbable?.Trim() ?? string.Empty;
        if (
            !_serviceOrchestrateurEtatJeu.PeutAfficherEtatLocalProvisoire(
                etat.IdentifiantJeuProbable
            )
        )
        {
            return;
        }

        if (
            etat.IdentifiantJeuProbable > 0
            && (
                _dernierIdentifiantJeuAvecProgression == etat.IdentifiantJeuProbable
                || _dernieresDonneesJeuAffichees?.Jeu.Id == etat.IdentifiantJeuProbable
                || (
                    _identifiantJeuSuccesCourant == etat.IdentifiantJeuProbable
                    && _succesJeuCourant.Count > 0
                )
            )
        )
        {
            return;
        }

        if (
            string.IsNullOrWhiteSpace(titreJeuProbable)
            || string.Equals(
                _dernierTitreJeuApi,
                titreJeuProbable,
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            return;
        }

        DefinirTitreZoneJeu();
        DefinirTitreJeuEnCours(titreJeuProbable);
        DefinirDetailsJeuEnCours(string.Empty);
        DefinirEtatJeuDansProgression(string.Empty);
        DefinirTempsJeuSousImage(string.Empty);
        EnregistrerPhaseDetectionLocaleOrchestrateur(
            0,
            titreJeuProbable,
            string.IsNullOrWhiteSpace(etat.NomEmulateur) ? "local" : etat.NomEmulateur
        );
    }

    /*
     * Déclenche le chargement complet d'un jeu local résolu en tenant compte
     * des informations déjà affichées ou encore en cours de chargement.
     */
    private void ChargerJeuResolutLocal(int identifiantJeu, string titreJeuProvisoire)
    {
        if (identifiantJeu <= 0)
        {
            return;
        }

        bool infosJeuDejaAfficheesPourCeJeu = PeutConserverInfosJeuAffichees(identifiantJeu);
        bool progressionDejaAfficheePourCeJeu = PeutConserverProgressionAffichee(identifiantJeu);
        string titreAffichage = string.IsNullOrWhiteSpace(titreJeuProvisoire)
            ? _dernierTitreJeuApi
            : titreJeuProvisoire.Trim();
        bool memeJeuLocalDejaApplique =
            identifiantJeu == _identifiantJeuLocalActif
            && identifiantJeu == _dernierIdentifiantJeuApi
            && string.Equals(
                _titreJeuLocalActif,
                titreAffichage,
                StringComparison.OrdinalIgnoreCase
            )
            && !_rejeuDemarreEnAttenteChargement
            && (_chargementJeuEnCoursActif || infosJeuDejaAfficheesPourCeJeu);

        memeJeuLocalDejaApplique |= _serviceOrchestrateurEtatJeu.MemeJeuAfficheOuEnChargement(
            identifiantJeu
        );

        if (memeJeuLocalDejaApplique)
        {
            MettreAJourNoticeCompteEntete();
            return;
        }

        DefinirTitreZoneJeu();

        if (!infosJeuDejaAfficheesPourCeJeu)
        {
            ReinitialiserCarrouselVisuelsJeuEnCours();
            ReinitialiserImageJeuEnCours();
            DefinirTitreJeuEnCours(titreAffichage);
            DefinirDetailsJeuEnCours(string.Empty);
            DefinirEtatJeuDansProgression(string.Empty);
            DefinirTempsJeuSousImage(string.Empty);
        }

        DemarrerDiagnosticChangementJeu(
            $"local:{identifiantJeu}",
            $"source=local;jeu={identifiantJeu};titre={titreAffichage}"
        );
        EnregistrerPhaseDetectionLocaleOrchestrateur(
            identifiantJeu,
            titreAffichage,
            "local",
            !progressionDejaAfficheePourCeJeu
        );
        JournaliserDiagnosticChangementJeu("jeu_local_resolu", $"jeu={identifiantJeu}");
        ServiceResolutionJeuLocal.JournaliserEvenementInterface(
            "jeu_local_applique",
            $"gameId={identifiantJeu};titre={titreAffichage}"
        );
        _identifiantJeuLocalActif = identifiantJeu;
        _titreJeuLocalActif = titreAffichage;
        _dernierIdentifiantJeuApi = identifiantJeu;
        _rejeuDemarreEnAttenteChargement = false;
        MettreAJourNoticeCompteEntete();
        int versionChargement = ++_versionChargementContenuJeu;
        DemarrerPipelineChargementJeu(identifiantJeu, titreAffichage, versionChargement);
        DemarrerPrechargementJeuDepuisCacheLocal(identifiantJeu, versionChargement);
        DemarrerChargementJeuUtilisateurEnArrierePlan(
            identifiantJeu,
            titreAffichage,
            infosJeuDejaAfficheesPourCeJeu,
            progressionDejaAfficheePourCeJeu,
            versionChargement,
            false
        );
    }

    /*
     * Démarre le préchargement local du jeu depuis les caches disponibles.
     */
    private void DemarrerPrechargementJeuDepuisCacheLocal(int identifiantJeu, int versionChargement)
    {
        _ = PrechargerJeuDepuisCacheLocalAsync(identifiantJeu, versionChargement);
    }

    /*
     * Applique rapidement une progression de jeu construite depuis les caches
     * locaux si le chargement demandé est toujours d'actualité.
     */
    private async Task PrechargerJeuDepuisCacheLocalAsync(int identifiantJeu, int versionChargement)
    {
        try
        {
            JeuCatalogueLocal? jeuCatalogue = await _serviceCatalogueJeuxLocal.ObtenirJeuAsync(
                identifiantJeu
            );
            EtatJeuUtilisateurLocal? etatUtilisateur =
                await _serviceEtatUtilisateurJeuxLocal.ObtenirJeuAsync(identifiantJeu);
            DonneesJeuAffiche? donneesJeu =
                ServiceJeuRetroAchievements.ConstruireDonneesJeuDepuisCacheLocal(
                    jeuCatalogue,
                    etatUtilisateur
                );

            if (
                donneesJeu is null
                || !ChargementContenuJeuEstToujoursActuel(versionChargement, identifiantJeu)
            )
            {
                return;
            }

            await AppliquerProgressionJeuAsync(donneesJeu);
        }
        catch { }
    }
}
