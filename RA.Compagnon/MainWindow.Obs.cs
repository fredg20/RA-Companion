using RA.Compagnon.Modeles.Local;
using RA.Compagnon.Modeles.Obs;

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
        catch
        {
            // L'export OBS ne doit jamais empêcher Compagnon de continuer à fonctionner.
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
            SuccesCourant = new SuccesExportObs
            {
                IdentifiantJeu = 1,
                IdentifiantSucces = 1001,
                Titre = "Succès OBS de démonstration",
                Description = "Cet état sert à tester une source OBS sans session active.",
                DetailsPoints = "10 pts softcore / hardcore",
                DetailsFaisabilite = "Faisabilité : test",
            },
            DernierSuccesObtenu = new SuccesDebloqueExportObs
            {
                IdentifiantJeu = 1,
                IdentifiantSucces = 1001,
                Titre = "Succès OBS de démonstration",
                Points = 10,
                Mode = "Hardcore",
                DateObtention = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            },
            EtatSynchronisation = "Test OBS",
            ModeAffichageSucces = _configurationConnexion.ModeAffichageSucces,
        };

        await _serviceExportObs.ExporterAsync(etat);
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
            SuccesCourant = ConstruireSuccesObs(succes),
            DernierSuccesObtenu = ConstruireDernierSuccesObs(dernierSuccesObtenu),
            EtatSynchronisation = _vueModele.EtatSynchronisationJeu,
            ModeAffichageSucces = _configurationConnexion.ModeAffichageSucces,
        };
    }

    /*
     * Convertit le jeu courant ou sauvegardé vers la forme publique OBS.
     */
    private static JeuExportObs ConstruireJeuObs(EtatJeuAfficheLocal? jeu)
    {
        if (jeu is null)
        {
            return new JeuExportObs();
        }

        return new JeuExportObs
        {
            IdentifiantJeu = jeu.Id,
            Titre = jeu.Title,
            Statut = jeu.EtatJeu,
            Details = jeu.Details,
            IdentifiantConsole = jeu.ConsoleId,
            Genre = jeu.Genre,
            Developpeur = jeu.Developer,
            Image = jeu.ImageBoxArt,
        };
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
     * Convertit le succès mis en avant vers la forme publique OBS.
     */
    private static SuccesExportObs ConstruireSuccesObs(EtatSuccesAfficheLocal? succes)
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
        };
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
