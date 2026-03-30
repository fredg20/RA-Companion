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

    private void AppliquerTitreJeuLocalProvisoire(EtatSondeLocaleEmulateur etat)
    {
        string titreJeuProbable = etat.TitreJeuProbable?.Trim() ?? string.Empty;

        // Quand le bon jeu est déjà chargé avec sa progression/ses succès, on évite
        // de réécraser l'UI avec l'état transitoire "Détection locale en cours...".
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
        TexteResumeProgressionJeuEnCours.Text = "-- / --";
        TextePourcentageJeuEnCours.Text = "Détection locale en cours...";
        BarreProgressionJeuEnCours.Value = 0;
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

        if (!progressionDejaAfficheePourCeJeu)
        {
            TexteResumeProgressionJeuEnCours.Text = "-- / --";
            TextePourcentageJeuEnCours.Text = "Confirmation du jeu local...";
            BarreProgressionJeuEnCours.Value = 0;
        }

        DemarrerDiagnosticChangementJeu(
            $"local:{identifiantJeu}",
            $"source=local;jeu={identifiantJeu};titre={titreAffichage}"
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
            // Le préchargement local reste opportuniste.
        }
    }
}
