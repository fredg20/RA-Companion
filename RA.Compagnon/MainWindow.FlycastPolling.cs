using System.Globalization;
using System.IO;
using RA.Compagnon.Modeles.Api.V2.Game;
using RA.Compagnon.Modeles.Local;
using RA.Compagnon.Modeles.Presentation;
using RA.Compagnon.Services;

namespace RA.Compagnon;

public partial class MainWindow
{
    private static readonly string CheminJournalDetectionFlycastApi = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "RA-Compagnon",
        "journal-detection-flycast-api.log"
    );

    private async void ActualisationSuccesFlycastApi_Tick(object? sender, EventArgs e)
    {
        if (_verificationSuccesFlycastApiEnCours)
        {
            return;
        }

        if (
            !PeutVerifierSuccesFlycastParApi(
                out int identifiantJeu,
                out GameAchievementV2? succesCible,
                out string raisonBlocage
            )
        )
        {
            if (!string.IsNullOrWhiteSpace(raisonBlocage))
            {
                JournaliserDetectionFlycastApi("ignore", raisonBlocage);
                ServiceSurveillanceSuccesLocaux.JournaliserEvenement(
                    "flycast_api_poll_ignore",
                    raisonBlocage
                );
            }

            return;
        }

        GameAchievementV2 succesCourant = succesCible!;

        _verificationSuccesFlycastApiEnCours = true;

        try
        {
            JournaliserDetectionFlycastApi(
                "debut",
                $"jeu={identifiantJeu};succes={succesCourant.Id}"
            );
            ServiceSurveillanceSuccesLocaux.JournaliserEvenement(
                "flycast_api_poll_debut",
                $"jeu={identifiantJeu};succes={succesCourant.Id}"
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
                JournaliserDetectionFlycastApi(
                    "ignore",
                    $"raison=jeu_mismatch;attendu={identifiantJeu};recu={jeu.Id}"
                );
                ServiceSurveillanceSuccesLocaux.JournaliserEvenement(
                    "flycast_api_poll_ignore",
                    $"raison=jeu_mismatch;attendu={identifiantJeu};recu={jeu.Id}"
                );
                return;
            }

            SynchroniserEtatSuccesDepuisApi(jeu);
            _etatSuccesObserves = ServiceDetectionSuccesJeu.CapturerEtat([.. jeu.Succes.Values]);

            GameAchievementV2? succesApi = jeu.Succes.Values.FirstOrDefault(item =>
                item.Id == succesCourant.Id
            );

            if (succesApi is null)
            {
                JournaliserDetectionFlycastApi(
                    "ignore",
                    $"raison=succes_absent;jeu={identifiantJeu};succes={succesCourant.Id}"
                );
                ServiceSurveillanceSuccesLocaux.JournaliserEvenement(
                    "flycast_api_poll_ignore",
                    $"raison=succes_absent;jeu={identifiantJeu};succes={succesCourant.Id}"
                );
                return;
            }

            if (!SuccesPossedeDateObtention(succesApi))
            {
                JournaliserDetectionFlycastApi(
                    "aucun_deblocage",
                    $"jeu={identifiantJeu};succes={succesApi.Id};dateSoft={NettoyerValeurJournal(succesApi.DateEarned)};dateHard={NettoyerValeurJournal(succesApi.DateEarnedHardcore)}"
                );
                ServiceSurveillanceSuccesLocaux.JournaliserEvenement(
                    "flycast_api_poll_aucun_deblocage",
                    $"jeu={identifiantJeu};succes={succesApi.Id}"
                );
                return;
            }

            SuccesDebloqueDetecte succesDetecte = new()
            {
                IdentifiantJeu = identifiantJeu,
                TitreJeu = jeu.Title?.Trim() ?? string.Empty,
                IdentifiantSucces = succesApi.Id,
                TitreSucces = succesApi.Title?.Trim() ?? string.Empty,
                Points = succesApi.Points,
                Hardcore = !string.IsNullOrWhiteSpace(succesApi.DateEarnedHardcore),
                DateObtention = !string.IsNullOrWhiteSpace(succesApi.DateEarnedHardcore)
                    ? succesApi.DateEarnedHardcore
                    : succesApi.DateEarned,
            };

            if (SuccesDejaTraiteRecemment(succesDetecte))
            {
                await RafraichirSuccesEnCoursDepuisApiAsync(succesApi.Id);
                JournaliserDetectionFlycastApi(
                    "ignore",
                    $"raison=succes_deja_traite;jeu={identifiantJeu};succes={succesApi.Id};date={NettoyerValeurJournal(succesDetecte.DateObtention)}"
                );
                ServiceSurveillanceSuccesLocaux.JournaliserEvenement(
                    "flycast_api_poll_ignore",
                    $"raison=succes_deja_traite;jeu={identifiantJeu};succes={succesApi.Id}"
                );
                return;
            }

            ServiceDetectionSuccesJeu.JournaliserDetection(succesDetecte, "flycast_api");
            MarquerSuccesCommeTraite(succesDetecte);

            bool affiche = await AfficherSuccesDebloqueDetecteAsync(succesDetecte);
            JournaliserDetectionFlycastApi(
                affiche ? "affiche" : "echec_ui",
                $"jeu={identifiantJeu};succes={succesApi.Id};titreSucces={NettoyerValeurJournal(succesApi.Title)};date={NettoyerValeurJournal(succesDetecte.DateObtention)}"
            );

            ServiceSurveillanceSuccesLocaux.JournaliserEvenement(
                affiche ? "flycast_api_poll_affiche" : "flycast_api_poll_echec_ui",
                $"jeu={identifiantJeu};succes={succesApi.Id}"
            );
        }
        catch
        {
            JournaliserDetectionFlycastApi(
                "echec",
                $"jeu={identifiantJeu};succes={succesCourant.Id}"
            );
            ServiceSurveillanceSuccesLocaux.JournaliserEvenement(
                "flycast_api_poll_echec",
                $"jeu={identifiantJeu};succes={succesCourant.Id}"
            );
        }
        finally
        {
            _verificationSuccesFlycastApiEnCours = false;
        }
    }

    private bool PeutVerifierSuccesFlycastParApi(
        out int identifiantJeu,
        out GameAchievementV2? succesCible,
        out string raisonBlocage
    )
    {
        identifiantJeu = 0;
        succesCible = null;
        raisonBlocage = string.Empty;

        if (!ConfigurationConnexionEstComplete())
        {
            raisonBlocage = "raison=configuration_incomplete";
            return false;
        }

        if (!_profilUtilisateurAccessible)
        {
            raisonBlocage = "raison=profil_inaccessible";
            return false;
        }

        if (_chargementJeuEnCoursActif)
        {
            raisonBlocage = "raison=chargement_jeu_en_cours";
            return false;
        }

        if (_dernierEtatSondeLocaleEmulateurs is not { EmulateurDetecte: true } etat)
        {
            raisonBlocage = "raison=sonde_absente";
            return false;
        }

        if (!string.Equals(etat.NomEmulateur, "Flycast", StringComparison.Ordinal))
        {
            raisonBlocage = $"raison=emulateur_non_flycast;emulateur={etat.NomEmulateur}";
            return false;
        }

        identifiantJeu =
            etat.IdentifiantJeuProbable > 0
                ? etat.IdentifiantJeuProbable
                : _identifiantJeuLocalActif > 0
                    ? _identifiantJeuLocalActif
                    : _identifiantJeuSuccesCourant;

        if (identifiantJeu <= 0)
        {
            raisonBlocage = "raison=jeu_inconnu";
            return false;
        }

        if (_identifiantJeuSuccesCourant != identifiantJeu)
        {
            raisonBlocage =
                $"raison=jeu_courant_non_aligne;attendu={identifiantJeu};courant={_identifiantJeuSuccesCourant}";
            return false;
        }

        if (_succesJeuCourant.Count == 0)
        {
            raisonBlocage = $"raison=liste_succes_vide;jeu={identifiantJeu}";
            return false;
        }

        succesCible = ObtenirSuccesEnCoursAfficheActuellement();

        if (succesCible is null)
        {
            raisonBlocage = $"raison=succes_courant_absent;jeu={identifiantJeu}";
            return false;
        }

        if (SuccesEstDebloquePourAffichage(succesCible))
        {
            raisonBlocage =
                $"raison=succes_deja_debloque;jeu={identifiantJeu};succes={succesCible.Id}";
            return false;
        }

        return true;
    }

    private GameAchievementV2? ObtenirSuccesEnCoursAfficheActuellement()
    {
        if (_identifiantJeuSuccesCourant <= 0 || _succesJeuCourant.Count == 0)
        {
            return null;
        }

        List<GameAchievementV2> succesOrdonnes =
        [
            .. OrdonnerSuccesPourGrilleSelonMode(_identifiantJeuSuccesCourant, _succesJeuCourant),
        ];

        return ObtenirSuccesEnCoursSelectionne(succesOrdonnes);
    }

    private void SynchroniserEtatSuccesDepuisApi(GameInfoAndUserProgressV2 jeu)
    {
        if (_identifiantJeuSuccesCourant != jeu.Id || _succesJeuCourant.Count == 0)
        {
            return;
        }

        Dictionary<int, GameAchievementV2> succesApiParId = jeu.Succes.Values.ToDictionary(item =>
            item.Id
        );

        foreach (GameAchievementV2 succesLocal in _succesJeuCourant)
        {
            if (!succesApiParId.TryGetValue(succesLocal.Id, out GameAchievementV2? succesApi))
            {
                continue;
            }

            succesLocal.DateEarned = succesApi.DateEarned;
            succesLocal.DateEarnedHardcore = succesApi.DateEarnedHardcore;
        }

        if (_dernieresDonneesJeuAffichees?.Jeu.Id != jeu.Id)
        {
            return;
        }

        foreach (GameAchievementV2 succesLocal in _dernieresDonneesJeuAffichees.Jeu.Succes.Values)
        {
            if (!succesApiParId.TryGetValue(succesLocal.Id, out GameAchievementV2? succesApi))
            {
                continue;
            }

            succesLocal.DateEarned = succesApi.DateEarned;
            succesLocal.DateEarnedHardcore = succesApi.DateEarnedHardcore;
        }
    }

    private async Task RafraichirSuccesEnCoursDepuisApiAsync(int identifiantSucces)
    {
        GameAchievementV2? succesCourant = ObtenirSuccesEnCoursAfficheActuellement();

        if (succesCourant is null || succesCourant.Id != identifiantSucces)
        {
            return;
        }

        GameAchievementV2? succesLocal = _succesJeuCourant.FirstOrDefault(item =>
            item.Id == identifiantSucces
        );

        if (succesLocal is null)
        {
            return;
        }

        bool estEpingleManuellement =
            _etatListeSuccesUi.IdentifiantSuccesEpingle == identifiantSucces;
        await AppliquerSuccesEnCoursAsync(
            _identifiantJeuSuccesCourant,
            succesLocal,
            false,
            estEpingleManuellement
        );
    }

    private static bool SuccesPossedeDateObtention(GameAchievementV2 succes)
    {
        return !string.IsNullOrWhiteSpace(succes.DateEarned)
            || !string.IsNullOrWhiteSpace(succes.DateEarnedHardcore);
    }

    private static void ReinitialiserJournalDetectionFlycastApiSession()
    {
        _ = ServiceModeDiagnostic.ReinitialiserJournalSession(CheminJournalDetectionFlycastApi);
    }

    private static void JournaliserDetectionFlycastApi(string etat, string details)
    {
        _ = ServiceModeDiagnostic.JournaliserLigne(
            CheminJournalDetectionFlycastApi,
            $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] etat={NettoyerValeurJournal(etat)};details={NettoyerValeurJournal(details)}{Environment.NewLine}"
        );
    }

    private static string NettoyerValeurJournal(string? valeur)
    {
        return string.IsNullOrWhiteSpace(valeur)
            ? string.Empty
            : valeur.Replace("\r", " ").Replace("\n", " ").Trim();
    }
}
