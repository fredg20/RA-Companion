using System.Diagnostics;
using System.Globalization;
using RA.Compagnon.Modeles.Api.V2.Game;
using RA.Compagnon.Modeles.Api.V2.User;
using RA.Compagnon.Modeles.Local;
using RA.Compagnon.Modeles.Obs;
using RA.Compagnon.Modeles.Presentation;
using RA.Compagnon.Services;

/*
 * Regroupe la préparation des données publiques exportées vers OBS Studio.
 */
namespace RA.Compagnon;

/*
 * Transforme l'état visible courant de Compagnon en fichiers locaux stables
 * pour les futures sources OBS.
 */
public partial class MainWindow
{
    /*
     * Déclenche un export OBS en arrière-plan dès qu'un changement visible
     * mérite de réécrire state.json sans bloquer l'interface.
     */
    private void DemanderExportObs()
    {
        if (!_configurationConnexion.ExportObsActif)
        {
            return;
        }

        LancerTacheNonBloquante(ExporterEtatObsAsync(), "export_obs");
    }

    /*
     * Exporte l'état courant vers OBS sans bloquer l'interface si l'écriture
     * échoue temporairement.
     */
    private async Task ExporterEtatObsAsync(SuccesDebloqueDetecte? dernierSuccesObtenu = null)
    {
        if (!_configurationConnexion.ExportObsActif)
        {
            return;
        }

        try
        {
            await _serviceExportObs.ExporterAsync(ConstruireEtatObs(dernierSuccesObtenu));
        }
        catch (Exception exception)
        {
            JournaliserExceptionNonBloquante("export_obs", exception);
        }
    }

    /*
     * Active ou désactive l'export automatique OBS puis persiste ce choix dans
     * la configuration locale.
     */
    private async Task DefinirExportObsActifAsync(bool actif)
    {
        if (_configurationConnexion.ExportObsActif == actif)
        {
            return;
        }

        _configurationConnexion.ExportObsActif = actif;
        await _serviceConfigurationLocale.SauvegarderEtatApplicationAsync(_configurationConnexion);

        if (actif)
        {
            await ExporterEtatObsAsync();
            return;
        }

        await ExporterEtatObsDesactiveAsync();
    }

    /*
     * Publie un état neutre quand l'export OBS est désactivé afin d'éviter que
     * l'overlay conserve une ancienne information.
     */
    private async Task ExporterEtatObsDesactiveAsync()
    {
        try
        {
            await _serviceExportObs.ExporterAsync(
                new EtatExportObs
                {
                    MisAJourUtc = DateTimeOffset.UtcNow.ToString("O"),
                    Jeu = new JeuExportObs { Statut = "OBS désactivé" },
                    EtatSynchronisation = "Export OBS désactivé",
                    ModeAffichageSucces = _configurationConnexion.ModeAffichageSucces,
                }
            );
        }
        catch { }
    }

    /*
     * Génère un état fictif pour valider rapidement les sources OBS sans
     * devoir lancer un jeu ou débloquer un vrai succès.
     */
    private async Task ExporterEtatObsTestAsync()
    {
        EtatExportObs etat = new()
        {
            MisAJourUtc = DateTimeOffset.UtcNow.ToString("O"),
            Jeu = new JeuExportObs
            {
                IdentifiantJeu = 1,
                Titre = "RA-Compagnon - Test OBS",
                Statut = "Dernier jeu",
                Details = "État fictif généré par Compagnon.",
                Genre = "Test",
                Developpeur = "Compagnon",
            },
            Progression = new ProgressionExportObs
            {
                Resume = "12 / 30 succès",
                Pourcentage = "40 %",
                Valeur = 40,
            },
            UserInfo = new UserInfoExportObs
            {
                LastGameId = 236,
                TotalPoints = 24682,
                TotalTruePoints = 80704,
                Rank = 1864,
                Awards = 16,
                UserPic = "/UserPic/RetroS3xual.png",
                RetroRatio = "3.27%",
            },
            SuccesCourant = new SuccesExportObs
            {
                IdentifiantJeu = 1,
                IdentifiantSucces = 1001,
                Titre = "Succès OBS softcore",
                Description = "Cet état simule un rétrosuccès réussi en softcore.",
                DetailsPoints = "10 pts softcore / hardcore",
                DetailsFaisabilite = "Faisabilité : test",
                Badge = "https://i.retroachievements.org/Badge/00000.png",
                EstHardcore = false,
            },
            GrilleSuccesJeu =
            [
                new SuccesBadgeExportObs
                {
                    IdentifiantSucces = 1001,
                    Titre = "Succès OBS softcore",
                    Description = "Cet état simule un rétrosuccès réussi en softcore.",
                    Badge = "https://i.retroachievements.org/Badge/00000.png",
                    EstDebloque = true,
                    EstHardcore = false,
                    EstSelectionne = true,
                },
                new SuccesBadgeExportObs
                {
                    IdentifiantSucces = 1002,
                    Titre = "Succès du groupe 2",
                    Description = "Exemple de badge contextuel encore verrouillé.",
                    Badge = "https://i.retroachievements.org/Badge/00000_lock.png",
                    EstDebloque = false,
                    EstHardcore = false,
                },
                new SuccesBadgeExportObs
                {
                    IdentifiantSucces = 1003,
                    Titre = "Succès du groupe 3",
                    Description = "Exemple de badge débloqué en softcore.",
                    Badge = "https://i.retroachievements.org/Badge/00000.png",
                    EstDebloque = true,
                    EstHardcore = false,
                },
            ],
            DernierSuccesObtenu = new SuccesDebloqueExportObs
            {
                IdentifiantJeu = 1,
                IdentifiantSucces = 1001,
                Titre = "Succès OBS softcore",
                Points = 10,
                Mode = "Softcore",
                DateObtention = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            },
            EtatSynchronisation = "Test OBS",
            ModeAffichageSucces = _configurationConnexion.ModeAffichageSucces,
        };

        await _serviceExportObs.ExporterAsync(etat);
    }

    /*
     * Ouvre l'overlay dans une fenêtre de navigateur minimale afin de pouvoir
     * valider rapidement le rendu sans passer immédiatement par OBS Studio.
     */
    private void OuvrirOverlayObsMinimal()
    {
        string urlOverlay =
            $"{_serviceServeurObsLocal.UrlOverlay}?preview={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";

        if (EssayerOuvrirOverlayObsMinimalAvecNavigateur("msedge.exe", urlOverlay))
        {
            return;
        }

        if (EssayerOuvrirOverlayObsMinimalAvecNavigateur("chrome.exe", urlOverlay))
        {
            return;
        }

        Process.Start(new ProcessStartInfo { FileName = urlOverlay, UseShellExecute = true });
    }

    /*
     * Lance un navigateur compatible en mode application, ce qui retire les
     * onglets et barres inutiles pour offrir une fenêtre de prévisualisation.
     */
    private static bool EssayerOuvrirOverlayObsMinimalAvecNavigateur(
        string executable,
        string urlOverlay
    )
    {
        try
        {
            Process.Start(
                new ProcessStartInfo
                {
                    FileName = executable,
                    Arguments = $"--app=\"{urlOverlay}\" --window-size=760,260",
                    UseShellExecute = true,
                }
            );
            return true;
        }
        catch
        {
            return false;
        }
    }

    /*
     * Construit le modèle sérialisable à partir de la configuration persistée
     * et des champs visibles du ViewModel.
     */
    private EtatExportObs ConstruireEtatObs(SuccesDebloqueDetecte? dernierSuccesObtenu)
    {
        EtatJeuAfficheLocal? jeu = _configurationConnexion.DernierJeuAffiche;
        EtatSuccesAfficheLocal? succes = _configurationConnexion.DernierSuccesAffiche;

        return new EtatExportObs
        {
            MisAJourUtc = DateTimeOffset.UtcNow.ToString("O"),
            Jeu = ConstruireJeuObs(jeu),
            Progression = ConstruireProgressionObs(jeu),
            UserInfo = ConstruireUserInfoObs(),
            SuccesCourant = ConstruireSuccesObs(succes),
            GrilleSuccesJeu = ConstruireGrilleSuccesJeuObs(succes),
            DernierSuccesObtenu = ConstruireDernierSuccesObs(dernierSuccesObtenu),
            EtatSynchronisation = _vueModele.EtatSynchronisationJeu,
            ModeAffichageSucces = _configurationConnexion.ModeAffichageSucces,
        };
    }

    /*
     * Convertit le jeu courant ou sauvegardé vers la forme publique OBS.
     */
    private JeuExportObs ConstruireJeuObs(EtatJeuAfficheLocal? jeu)
    {
        if (jeu is null)
        {
            return new JeuExportObs();
        }

        string imageIcone = ObtenirCheminImageIconePourObs(jeu);
        string imageFond = string.IsNullOrWhiteSpace(jeu.ImageBoxArt)
            ? imageIcone
            : jeu.ImageBoxArt;
        string imageConsole = ObtenirCheminImageConsolePourObs(jeu);

        return new JeuExportObs
        {
            IdentifiantJeu = jeu.Id,
            Titre = jeu.Title,
            Statut = jeu.EtatJeu,
            Details = jeu.Details,
            IdentifiantConsole = jeu.ConsoleId,
            Genre = jeu.Genre,
            Developpeur = jeu.Developer,
            Image = imageFond,
            ImageIcone = imageIcone,
            ImageConsole = imageConsole,
        };
    }

    /*
     * Retourne l'icône du jeu à privilégier pour OBS, en se basant d'abord
     * sur les données fraîches en mémoire puis sur l'état local sauvegardé.
     */
    private string ObtenirCheminImageIconePourObs(EtatJeuAfficheLocal jeu)
    {
        if (_dernieresDonneesJeuAffichees?.Jeu.Id == jeu.Id)
        {
            if (!string.IsNullOrWhiteSpace(_dernieresDonneesJeuAffichees.DetailsEtendus?.ImageIcon))
            {
                return _dernieresDonneesJeuAffichees.DetailsEtendus.ImageIcon.Trim();
            }

            if (!string.IsNullOrWhiteSpace(_dernieresDonneesJeuAffichees.Progression?.ImageIcon))
            {
                return _dernieresDonneesJeuAffichees.Progression.ImageIcon.Trim();
            }
        }

        return string.IsNullOrWhiteSpace(jeu.ImageIcon) ? string.Empty : jeu.ImageIcon.Trim();
    }

    /*
     * Retourne l'icône de console exportée vers OBS depuis l'état local ou le
     * cache catalogue quand celui-ci est déjà présent.
     */
    private string ObtenirCheminImageConsolePourObs(EtatJeuAfficheLocal jeu)
    {
        if (!string.IsNullOrWhiteSpace(jeu.ImageConsole))
        {
            return jeu.ImageConsole.Trim();
        }

        string imageConsole = _serviceCatalogueRetroAchievements.ObtenirUrlIconeConsoleDepuisCache(
            jeu.ConsoleId
        );

        return string.IsNullOrWhiteSpace(imageConsole) ? string.Empty : imageConsole.Trim();
    }

    /*
     * Convertit les informations de progression déjà formatées par Compagnon.
     */
    private static ProgressionExportObs ConstruireProgressionObs(EtatJeuAfficheLocal? jeu)
    {
        if (jeu is null)
        {
            return new ProgressionExportObs();
        }

        return new ProgressionExportObs
        {
            Resume = jeu.ResumeProgression,
            Pourcentage = jeu.PourcentageProgression,
            Valeur = Math.Clamp(jeu.ValeurProgression, 0, 100),
        };
    }

    /*
     * Convertit les données de compte déjà chargées en sources OBS séparées,
     * sans relancer d'appel API pendant l'export.
     */
    private UserInfoExportObs ConstruireUserInfoObs()
    {
        UserSummaryV2? resume = _dernierResumeUtilisateurCharge;
        UserProfileV2? profil = _dernierProfilUtilisateurCharge;
        int totalPoints = resume?.TotalPoints ?? profil?.TotalPoints ?? 0;
        int totalTruePoints = resume?.TotalTruePoints ?? profil?.TotalTruePoints ?? 0;

        return new UserInfoExportObs
        {
            LastGameId = resume?.LastGameId > 0 ? resume.LastGameId : profil?.LastGameId ?? 0,
            TotalPoints = totalPoints,
            TotalTruePoints = totalTruePoints,
            Rank = resume?.Rank ?? 0,
            Awards = resume?.Awarded.Count ?? 0,
            UserPic = DeterminerUserPicObs(resume, profil),
            RetroRatio = CalculerRetroRatioObs(totalPoints, totalTruePoints),
        };
    }

    /*
     * Privilégie l'avatar du résumé enrichi, puis celui du profil simple.
     */
    private static string DeterminerUserPicObs(UserSummaryV2? resume, UserProfileV2? profil)
    {
        if (!string.IsNullOrWhiteSpace(resume?.UserPic))
        {
            return resume.UserPic.Trim();
        }

        return string.IsNullOrWhiteSpace(profil?.UserPic) ? string.Empty : profil.UserPic.Trim();
    }

    /*
     * Reproduit le ratio RetroAchievements à partir des points pondérés et
     * standards afin que la source texte reste indépendante du format API.
     */
    private static string CalculerRetroRatioObs(int totalPoints, int totalTruePoints)
    {
        if (totalPoints <= 0 || totalTruePoints <= 0)
        {
            return string.Empty;
        }

        double ratio = (double)totalTruePoints / totalPoints;
        return string.Create(CultureInfo.InvariantCulture, $"{ratio:0.00}%");
    }

    /*
     * Convertit le succès mis en avant vers la forme publique OBS.
     */
    private SuccesExportObs ConstruireSuccesObs(EtatSuccesAfficheLocal? succes)
    {
        if (succes is null)
        {
            return new SuccesExportObs();
        }

        return new SuccesExportObs
        {
            IdentifiantJeu = succes.IdentifiantJeu,
            IdentifiantSucces = succes.IdentifiantSucces,
            Titre = succes.Titre,
            Description = succes.Description,
            DetailsPoints = succes.DetailsPoints,
            DetailsFaisabilite = succes.DetailsFaisabilite,
            Badge = succes.CheminImageBadge,
            EstHardcore = SuccesCourantEstReussiEnHardcore(succes),
        };
    }

    /*
     * Détermine si le succès actuellement mis en avant a été obtenu en mode
     * hardcore, afin d'adapter le rendu OBS sans persister un état visuel.
     */
    private bool SuccesCourantEstReussiEnHardcore(EtatSuccesAfficheLocal succes)
    {
        return _succesJeuCourant.Any(item =>
            item.Id == succes.IdentifiantSucces
            && !string.IsNullOrWhiteSpace(item.DateEarnedHardcore)
        );
    }

    /*
     * Convertit le groupe de rétrosuccès actuellement retenu par Compagnon en
     * liste légère de badges directement réutilisable dans l'overlay OBS.
     */
    private List<SuccesBadgeExportObs> ConstruireGrilleSuccesJeuObs(EtatSuccesAfficheLocal? succes)
    {
        int identifiantSuccesSelectionne = succes?.IdentifiantSucces ?? 0;
        int identifiantJeuCourant =
            _configurationConnexion.DernierJeuAffiche?.Id > 0
                ? _configurationConnexion.DernierJeuAffiche.Id
                : _identifiantJeuSuccesCourant;

        Dictionary<int, GameAchievementV2> succesParIdentifiant = _succesJeuCourant.ToDictionary(
            item => item.Id,
            item => item
        );

        if (
            _configurationConnexion.DerniereListeSuccesAffichee is { } listeSauvegardee
            && listeSauvegardee.Id == identifiantJeuCourant
            && listeSauvegardee.Achievements.Count > 0
        )
        {
            return
            [
                .. listeSauvegardee.Achievements.Select(item =>
                {
                    bool estDebloque = !item.CheminImageBadge.Contains(
                        "_lock",
                        StringComparison.OrdinalIgnoreCase
                    );
                    bool estHardcore =
                        succesParIdentifiant.TryGetValue(
                            item.IdentifiantSucces,
                            out GameAchievementV2? succesBrut
                        ) && !string.IsNullOrWhiteSpace(succesBrut.DateEarnedHardcore);
                    string description = succesBrut?.Description?.Trim() ?? string.Empty;

                    return new SuccesBadgeExportObs
                    {
                        IdentifiantSucces = item.IdentifiantSucces,
                        Titre = item.Titre,
                        Description = description,
                        Badge = item.CheminImageBadge,
                        EstDebloque = estDebloque,
                        EstHardcore = estHardcore,
                        EstSelectionne = item.IdentifiantSucces == identifiantSuccesSelectionne,
                    };
                }),
            ];
        }

        if (_identifiantJeuSuccesCourant <= 0 || _succesJeuCourant.Count == 0)
        {
            return [];
        }

        List<GameAchievementV2> succesOrdonnes = OrdonnerSuccesPourGrilleSelonMode(
            _identifiantJeuSuccesCourant,
            _succesJeuCourant
        );

        return
        [
            .. succesOrdonnes.Select(succesJeu =>
            {
                SuccesGrilleAffiche succesAffiche = ServicePresentationSucces.ConstruirePourGrille(
                    succesJeu
                );

                return new SuccesBadgeExportObs
                {
                    IdentifiantSucces = succesAffiche.IdentifiantSucces,
                    Titre = succesAffiche.Titre,
                    Description = succesJeu.Description?.Trim() ?? string.Empty,
                    Badge = succesAffiche.UrlBadge,
                    EstDebloque = succesAffiche.EstDebloque,
                    EstHardcore = succesAffiche.EstHardcore,
                    EstSelectionne =
                        succesAffiche.IdentifiantSucces == identifiantSuccesSelectionne,
                };
            }),
        ];
    }

    /*
     * Convertit le dernier succès obtenu de la session pour les futures alertes
     * OBS, en distinguant déjà Softcore et Hardcore.
     */
    private static SuccesDebloqueExportObs ConstruireDernierSuccesObs(SuccesDebloqueDetecte? succes)
    {
        if (succes is null)
        {
            return new SuccesDebloqueExportObs();
        }

        return new SuccesDebloqueExportObs
        {
            IdentifiantJeu = succes.IdentifiantJeu,
            IdentifiantSucces = succes.IdentifiantSucces,
            Titre = succes.TitreSucces,
            Points = succes.Points,
            Mode = succes.Hardcore ? "Hardcore" : "Softcore",
            DateObtention = succes.DateObtention,
        };
    }
}
