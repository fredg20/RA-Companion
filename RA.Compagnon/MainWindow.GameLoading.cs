using System.IO;
using System.Windows;
using System.Windows.Threading;
using RA.Compagnon.Modeles.Api;
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

            ProfilUtilisateurRetroAchievements profil =
                await ClientRetroAchievements.ObtenirProfilUtilisateurAsync(
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
            App.JournaliserDemarrage("ChargerJeuEnCours apres AppliquerProfil");
            DemarrerChargementSuccesRecentsEnArrierePlan(
                profil,
                _versionChargementContenuJeu,
                profil.IdentifiantDernierJeu
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
            TextePourcentageJeuEnCours.Text = "Progression du jeu indisponible";
            BarreProgressionJeuEnCours.Value = 0;
            ReinitialiserSuccesRecents();
            TexteEtatSuccesRecents.Text =
                "Les succès récents ne peuvent pas être chargés pour ce compte.";
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
                TextePourcentageJeuEnCours.Text = "Progression du jeu indisponible";
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
        ProfilUtilisateurRetroAchievements profil,
        bool forcerChargementJeu
    )
    {
        DefinirTitreZoneJeu(false);

        string messagePresence = string.IsNullOrWhiteSpace(profil.MessagePresenceRiche)
            ? "Aucune activité en cours."
            : profil.MessagePresenceRiche;
        int identifiantJeuEffectif = profil.IdentifiantDernierJeu;
        JeuRecemmentJoueRetroAchievements? dernierJeuJoue = null;
        string titreJeuProvisoire;

        if (identifiantJeuEffectif <= 0)
        {
            dernierJeuJoue = await ObtenirDernierJeuJoueAsync();
            identifiantJeuEffectif = dernierJeuJoue?.IdentifiantJeu ?? 0;
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
            TextePourcentageJeuEnCours.Text = "Aucun jeu pour afficher une progression";
            BarreProgressionJeuEnCours.Value = 0;
            return;
        }

        titreJeuProvisoire = DeterminerTitreJeuApiProvisoire(
            profil.NomDernierJeu,
            dernierJeuJoue?.Titre
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
            TextePourcentageJeuEnCours.Text = "Progression du jeu en cours de chargement";
            BarreProgressionJeuEnCours.Value = 0;
        }

        bool contexteApiInchange =
            !forcerChargementJeu
            && _dernierIdentifiantJeuApi == identifiantJeuEffectif
            && string.Equals(_dernierePresenceRiche, messagePresence, StringComparison.Ordinal)
            && string.Equals(_dernierPseudoCharge, profil.NomUtilisateur, StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(_dernierTitreJeuApi);

        _dernierIdentifiantJeuApi = identifiantJeuEffectif;
        _dernierePresenceRiche = messagePresence;
        _dernierPseudoCharge = profil.NomUtilisateur;

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
            JeuUtilisateurRetroAchievements jeu =
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
                TextePourcentageJeuEnCours.Text = "Progression du jeu indisponible";
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
        ProfilUtilisateurRetroAchievements profil,
        int versionChargement,
        int identifiantJeuProfil
    )
    {
        try
        {
            DateTimeOffset maintenant = DateTimeOffset.UtcNow;
            IReadOnlyList<SuccesRecentRetroAchievements> succesRecents =
                await ClientRetroAchievements.ObtenirSuccesDebloquesEntreAsync(
                    _configurationConnexion.Pseudo,
                    _configurationConnexion.CleApiWeb,
                    maintenant.AddDays(-7),
                    maintenant
                );

            IEnumerable<SuccesRecentRetroAchievements> succesTries =
                succesRecents.OrderByDescending(succes =>
                    ConvertirDateSucces(succes.DateDeblocage)
                );

            if (profil.IdentifiantDernierJeu > 0)
            {
                List<SuccesRecentRetroAchievements> succesJeuEnCours =
                [
                    .. succesTries.Where(succes =>
                        succes.IdentifiantJeu == profil.IdentifiantDernierJeu
                    ),
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
                        $"Affichage des {Math.Min(3, succesJeuEnCours.Count)} derniers succès du jeu en cours."
                    );
                    return;
                }
            }

            List<SuccesRecentRetroAchievements> succesAffiches = [.. succesTries.Take(3)];

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
                TexteEtatSuccesRecents.Text =
                    "Aucun succès récent n'a été détecté sur les 7 derniers jours.";
                return;
            }

            if (!ChargementSuccesRecentsEstToujoursActuel(versionChargement, identifiantJeuProfil))
            {
                return;
            }

            AppliquerSuccesRecents(
                succesAffiches,
                $"Affichage des {succesAffiches.Count} derniers succès connus."
            );
        }
        catch
        {
            if (!ChargementSuccesRecentsEstToujoursActuel(versionChargement, identifiantJeuProfil))
            {
                return;
            }

            ReinitialiserSuccesRecents();
            TexteEtatSuccesRecents.Text = "Impossible de charger les succès récents.";
        }
    }

    private void DemarrerChargementSuccesRecentsEnArrierePlan(
        ProfilUtilisateurRetroAchievements profil,
        int versionChargement,
        int identifiantJeuProfil
    )
    {
        _ = ChargerSuccesRecentsEnArrierePlanAsync(profil, versionChargement, identifiantJeuProfil);
    }

    private async Task ChargerSuccesRecentsEnArrierePlanAsync(
        ProfilUtilisateurRetroAchievements profil,
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

    private async Task AppliquerProgressionJeuAsync(JeuUtilisateurRetroAchievements jeu)
    {
        JournaliserDiagnosticChangementJeu("progression_debut", $"jeu={jeu.IdentifiantJeu}");
        _dernierTitreJeuApi = jeu.Titre;
        _dernierIdentifiantJeuAvecInfos = jeu.IdentifiantJeu;
        _dernierIdentifiantJeuAvecProgression = jeu.IdentifiantJeu;
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

        TexteResumeProgressionJeuEnCours.Text = $"{jeu.NombreSuccesObtenus} / {jeu.NombreSucces}";
        TextePourcentageJeuEnCours.Text = NormaliserPourcentage(jeu.CompletionUtilisateur);
        BarreProgressionJeuEnCours.Value = ExtrairePourcentage(jeu.CompletionUtilisateur);

        await SauvegarderDernierJeuAfficheAsync(jeu, detailsTempsJeu, detailsRecompense);
        JournaliserDiagnosticChangementJeu("progression_fin");
    }
}
