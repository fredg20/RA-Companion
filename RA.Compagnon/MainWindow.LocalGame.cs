using RA.Compagnon.Modeles.Api.V2.User;
using RA.Compagnon.Modeles.Catalogue;
using RA.Compagnon.Modeles.Etat;
using RA.Compagnon.Modeles.Local;
using RA.Compagnon.Modeles.Presentation;
using RA.Compagnon.Services;

namespace RA.Compagnon;

public partial class MainWindow
{
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

    private bool DoitVerrouillerAffichageSurDernierJeuActifRecemment()
    {
        if (EtatLocalJeuEstActif() || _configurationConnexion.DernierJeuAffiche?.Id <= 0)
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

        return string.Equals(
            etatRichPresence.StatutAffiche,
            "Actif récemment",
            StringComparison.OrdinalIgnoreCase
        );
    }

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

        AppliquerEtatJeuSauvegarde(jeuSauvegarde);
    }

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

        // Quand le bon jeu est deja charge avec sa progression et ses succes,
        // on evite de reecraser l'UI avec un etat transitoire.
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
        MettreAJourNoticeCompteEntete();
        int versionChargement = ++_versionChargementContenuJeu;
        DemarrerPipelineChargementJeu(identifiantJeu, titreAffichage, versionChargement);
        DemarrerPrechargementJeuDepuisCacheLocal(identifiantJeu, versionChargement);
        DemarrerChargementJeuUtilisateurEnArrierePlan(
            identifiantJeu,
            titreAffichage,
            infosJeuDejaAfficheesPourCeJeu,
            progressionDejaAfficheePourCeJeu,
            versionChargement
        );
    }

    private void DemarrerPrechargementJeuDepuisCacheLocal(int identifiantJeu, int versionChargement)
    {
        _ = PrechargerJeuDepuisCacheLocalAsync(identifiantJeu, versionChargement);
    }

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
        catch
        {
            // Le prechargement local reste opportuniste.
        }
    }
}
