using System.IO;
using System.Windows;
using System.Windows.Threading;
using RA.Compagnon.Modeles.Api;
using RA.Compagnon.Modeles.Api.V2.Achievement;
using RA.Compagnon.Modeles.Api.V2.Game;
using RA.Compagnon.Modeles.Api.V2.User;
using RA.Compagnon.Modeles.Local;
using RA.Compagnon.Services;

namespace RA.Compagnon;

public partial class MainWindow
{
    private async Task ChargerJeuEnCoursAsync(
        bool afficherEtatChargement = true,
        bool forcerChargementJeu = true
    )
    {
        App.JournaliserDemarrage("ChargerJeuEnCours d�but");
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

            UserProfileV2 profil = await ClientRetroAchievements.ObtenirProfilUtilisateurAsync(
                _configurationConnexion.Pseudo,
                _configurationConnexion.CleApiWeb
            );

            App.JournaliserDemarrage("ChargerJeuEnCours apres Profil");
            JournaliserDiagnosticChangementJeu("charger_jeu_profil_charge");
            _profilUtilisateurAccessible = true;
            _dernierProfilUtilisateurCharge = profil;
            _dernierResumeUtilisateurCharge = null;
            DefinirEtatConnexion("Connect�");
            await AppliquerProfilUtilisateurAsync(profil, forcerChargementJeu);
            App.JournaliserDemarrage("ChargerJeuEnCours apres AppliquerProfil");
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
            _minuteurActualisationApi.Stop();
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
                "Impossible de charger les succ�s r�cents pour ce compte.";
        }
        catch (Exception)
        {
            _dernierProfilUtilisateurCharge = null;
            _dernierResumeUtilisateurCharge = null;
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

            if (_actualisationApiCibleeEnAttente && ConfigurationConnexionEstComplete())
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
            ? "Aucune activit� d�tect�e."
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
            TextePourcentageJeuEnCours.Text = "Aucun jeu r�cent � afficher.";
            BarreProgressionJeuEnCours.Value = 0;
            return;
        }

        titreJeuProvisoire = DeterminerTitreJeuApiProvisoire(
            profil.LastGame,
            dernierJeuJoue?.Title
        );
        string titreAffichageInitial = string.IsNullOrWhiteSpace(_dernierTitreJeuApi)
            ? titreJeuProvisoire
            : _dernierTitreJeuApi;
        bool infosJeuDejaAfficheesPourCeJeu = PeutConserverInfosJeuAffichees(
            identifiantJeuEffectif
        );

        if (!infosJeuDejaAfficheesPourCeJeu)
        {
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

        _dernierIdentifiantJeuApi = identifiantJeuEffectif;
        _dernierePresenceRiche = messagePresence;
        _dernierPseudoCharge = profil.User;

        if (contexteApiInchange)
        {
            DefinirTitreJeuEnCours(_dernierTitreJeuApi);
            return;
        }

        int versionChargement = ++_versionChargementContenuJeu;
        DemarrerChargementJeuUtilisateurEnArrierePlan(
            identifiantJeuEffectif,
            titreJeuProvisoire,
            infosJeuDejaAfficheesPourCeJeu,
            progressionDejaAfficheePourCeJeu,
            versionChargement
        );
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
            GameInfoAndUserProgressV2 jeu =
                await ClientRetroAchievements.ObtenirJeuEtProgressionUtilisateurAsync(
                    _configurationConnexion.Pseudo,
                    _configurationConnexion.CleApiWeb,
                    identifiantJeuEffectif
                );

            if (!ChargementContenuJeuEstToujoursActuel(versionChargement, identifiantJeuEffectif))
            {
                return;
            }

            await AppliquerProgressionJeuAsync(jeu);
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
            IReadOnlyList<AchievementUnlockV2> succesRecents =
                await ClientRetroAchievements.ObtenirSuccesDebloquesEntreAsync(
                    _configurationConnexion.Pseudo,
                    _configurationConnexion.CleApiWeb,
                    maintenant.AddDays(-7),
                    maintenant
                );

            IEnumerable<AchievementUnlockV2> succesTries = succesRecents.OrderByDescending(succes =>
                ConvertirDateSucces(succes.Date)
            );

            if (profil.LastGameId > 0)
            {
                List<AchievementUnlockV2> succesJeuEnCours =
                [
                    .. succesTries.Where(succes => succes.GameId == profil.LastGameId),
                ];

                if (succesJeuEnCours.Count > 0)
                {
                    if (
                        !ChargementSuccesRecentsEstToujoursActuel(
                            versionChargement,
                            identifiantJeuProfil
                        )
                    )
                    {
                        return;
                    }

                    AppliquerSuccesRecents(
                        [.. succesJeuEnCours.Take(3)],
                        $"Affichage des {Math.Min(3, succesJeuEnCours.Count)} derniers succ�s du jeu en cours."
                    );
                    return;
                }
            }

            List<AchievementUnlockV2> succesAffiches = [.. succesTries.Take(3)];

            if (succesAffiches.Count == 0)
            {
                if (
                    !ChargementSuccesRecentsEstToujoursActuel(
                        versionChargement,
                        identifiantJeuProfil
                    )
                )
                {
                    return;
                }

                ReinitialiserSuccesRecents();
                TexteEtatSuccesRecents.Text = "Aucun succ�s r�cent sur les 7 derniers jours.";
                return;
            }

            if (!ChargementSuccesRecentsEstToujoursActuel(versionChargement, identifiantJeuProfil))
            {
                return;
            }

            AppliquerSuccesRecents(
                succesAffiches,
                $"Affichage des {succesAffiches.Count} derniers succ�s connus."
            );
        }
        catch
        {
            if (!ChargementSuccesRecentsEstToujoursActuel(versionChargement, identifiantJeuProfil))
            {
                return;
            }

            ReinitialiserSuccesRecents();
            TexteEtatSuccesRecents.Text = "Impossible de charger les succ�s r�cents.";
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

    private async Task AppliquerProgressionJeuAsync(GameInfoAndUserProgressV2 jeu)
    {
        JournaliserDiagnosticChangementJeu("progression_debut", $"jeu={jeu.Id}");
        _dernierTitreJeuApi = jeu.Title;
        _dernierIdentifiantJeuAvecInfos = jeu.Id;
        _dernierIdentifiantJeuAvecProgression = jeu.Id;
        AppliquerVisuelsJeuEnCoursInitiaux(jeu);
        DemarrerEnrichissementVisuelsJeuEnCours(jeu);
        JournaliserDiagnosticChangementJeu("progression_visuels_ok");
        AppliquerMetaConsoleJeuEnCoursInitiale(jeu);
        DemarrerEnrichissementMetaConsoleJeuEnCours(jeu);

        string detailsTempsJeu = string.Empty;
        string detailsRecompense = DeterminerStatutJeu(jeu);

        DefinirTempsJeuSousImage(detailsTempsJeu);
        DefinirEtatJeuDansProgression(detailsRecompense);
        DefinirDetailsJeuEnCours(string.Empty);
        await MettreAJourSuccesJeuAsync(jeu);
        JournaliserDiagnosticChangementJeu("progression_succes_ok");

        TexteResumeProgressionJeuEnCours.Text = $"{jeu.NumAwardedToUser} / {jeu.NumAchievements}";
        TextePourcentageJeuEnCours.Text = NormaliserPourcentage(jeu.UserCompletion);
        BarreProgressionJeuEnCours.Value = ExtrairePourcentage(jeu.UserCompletion);

        await SauvegarderDernierJeuAfficheAsync(jeu, detailsTempsJeu, detailsRecompense);
        JournaliserDiagnosticChangementJeu("progression_fin");
    }
}

