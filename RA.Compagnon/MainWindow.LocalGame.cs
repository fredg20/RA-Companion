using RA.Compagnon.Modeles.Api.V2.User;
using RA.Compagnon.Modeles.Local;
using RA.Compagnon.Services;

namespace RA.Compagnon;

public partial class MainWindow
{
    private async Task<RecentlyPlayedGameV2?> ObtenirDernierJeuJoueAsync()
    {
        try
        {
            IReadOnlyList<RecentlyPlayedGameV2> jeuxRecents =
                await _serviceUtilisateurRetroAchievements.ObtenirJeuxRecemmentJouesAsync(
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

        DefinirTitreZoneJeu();

        if (!infosJeuDejaAfficheesPourCeJeu)
        {
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
        DemarrerChargementJeuUtilisateurEnArrierePlan(
            identifiantJeu,
            titreAffichage,
            infosJeuDejaAfficheesPourCeJeu,
            progressionDejaAfficheePourCeJeu,
            versionChargement
        );
    }
}
