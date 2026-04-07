using System.Globalization;
using System.IO;
using RA.Compagnon.Modeles.Api.V2.Game;
using RA.Compagnon.Modeles.Debug;
using RA.Compagnon.Modeles.Local;

namespace RA.Compagnon.Services;

public sealed class ServiceTestSuccesDebug
{
    private static readonly string CheminJournalTestSucces = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "RA-Compagnon",
        "journal-test-succes-debug.log"
    );

    public static void JournaliserEvenement(string evenement, string details)
    {
        _ = ServiceModeDiagnostic.JournaliserLigne(
            CheminJournalTestSucces,
            string.Create(
                CultureInfo.InvariantCulture,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] evenement={Nettoyer(evenement)};details={Nettoyer(details)}{Environment.NewLine}"
            )
        );
    }

    public ResultatScenarioTestSuccesDebug ConstruireScenarioDepuisContexte(
        string nomEmulateur,
        int identifiantJeu,
        string titreJeu,
        IReadOnlyList<GameAchievementV2> succesJeuCourant,
        Func<GameAchievementV2, bool> succesEstDebloque,
        ModeDeclenchementTestSuccesDebug modeDeclenchement
    )
    {
        if (identifiantJeu <= 0)
        {
            return Invalide("jeu_invalide");
        }

        if (succesJeuCourant.Count == 0)
        {
            return Invalide("aucun_succes");
        }

        GameAchievementV2? succesCible = succesJeuCourant
            .OrderBy(item => item.DisplayOrder)
            .ThenBy(item => item.Id)
            .FirstOrDefault(item => !succesEstDebloque(item));

        if (succesCible is null)
        {
            return Invalide("aucun_succes_disponible");
        }

        (string sourceSimulee, string typeSourceLocale, string cheminSourceLocale) =
            ConstruireConfigurationSource(nomEmulateur, modeDeclenchement);

        if (
            modeDeclenchement == ModeDeclenchementTestSuccesDebug.SourceLocale
            && string.IsNullOrWhiteSpace(cheminSourceLocale)
        )
        {
            return Invalide("source_locale_indisponible");
        }

        ScenarioTestSuccesDebug scenario = new()
        {
            NomEmulateur = nomEmulateur.Trim(),
            SourceSimulee = sourceSimulee,
            TypeSourceLocale = typeSourceLocale,
            CheminSourceLocale = cheminSourceLocale,
            IdentifiantJeu = identifiantJeu,
            TitreJeu = titreJeu?.Trim() ?? string.Empty,
            IdentifiantSucces = succesCible.Id,
            TitreSucces = succesCible.Title?.Trim() ?? string.Empty,
            Points = succesCible.Points,
            Hardcore = true,
            DateObtention = DateTime.Now.ToString(
                "yyyy-MM-dd HH:mm:ss",
                CultureInfo.InvariantCulture
            ),
            ModeDeclenchement = modeDeclenchement,
        };

        return new ResultatScenarioTestSuccesDebug
        {
            EstValide = true,
            Motif = string.Empty,
            Scenario = scenario,
        };
    }

    public static SuccesDebloqueDetecte ConstruireSuccesDebloque(ScenarioTestSuccesDebug scenario)
    {
        return new SuccesDebloqueDetecte
        {
            IdentifiantJeu = scenario.IdentifiantJeu,
            TitreJeu = scenario.TitreJeu,
            IdentifiantSucces = scenario.IdentifiantSucces,
            TitreSucces = scenario.TitreSucces,
            Points = scenario.Points,
            Hardcore = scenario.Hardcore,
            DateObtention = scenario.DateObtention,
        };
    }

    public static List<GameAchievementV2> ConstruireSuccesVirtuelsSession(
        IReadOnlyList<GameAchievementV2> succesJeuCourant,
        ScenarioTestSuccesDebug scenario
    )
    {
        List<GameAchievementV2> succesClones =
        [
            .. succesJeuCourant.Select(item => new GameAchievementV2
            {
                Id = item.Id,
                Title = item.Title,
                Description = item.Description,
                Points = item.Points,
                TrueRatio = item.TrueRatio,
                NumAwarded = item.NumAwarded,
                NumAwardedHardcore = item.NumAwardedHardcore,
                BadgeName = item.BadgeName,
                DisplayOrder = item.DisplayOrder,
                Type = item.Type,
                DateEarned = item.DateEarned,
                DateEarnedHardcore = item.DateEarnedHardcore,
                MemAddr = item.MemAddr,
            }),
        ];

        GameAchievementV2? succesCible = succesClones.FirstOrDefault(item =>
            item.Id == scenario.IdentifiantSucces
        );

        if (succesCible is not null)
        {
            succesCible.DateEarned = scenario.DateObtention;

            if (scenario.Hardcore)
            {
                succesCible.DateEarnedHardcore = scenario.DateObtention;
            }
        }

        return succesClones;
    }

    public ResultatExecutionTestSuccesDebug InjecterScenarioSourceLocale(
        ScenarioTestSuccesDebug scenario
    )
    {
        if (scenario.ModeDeclenchement != ModeDeclenchementTestSuccesDebug.SourceLocale)
        {
            return InvalideExecution("mode_invalide", scenario.CheminSourceLocale);
        }

        if (string.IsNullOrWhiteSpace(scenario.CheminSourceLocale))
        {
            return InvalideExecution("chemin_invalide", string.Empty);
        }

        try
        {
            string? repertoire = Path.GetDirectoryName(scenario.CheminSourceLocale);

            if (!string.IsNullOrWhiteSpace(repertoire))
            {
                Directory.CreateDirectory(repertoire);
            }

            List<string> lignes = ConstruireLignesInjection(scenario);

            using FileStream flux = new(
                scenario.CheminSourceLocale,
                FileMode.Append,
                FileAccess.Write,
                FileShare.ReadWrite
            );
            using StreamWriter ecrivain = new(flux);

            foreach (string ligne in lignes)
            {
                ecrivain.WriteLine(ligne);
            }

            ecrivain.Flush();
            flux.Flush(true);

            return new ResultatExecutionTestSuccesDebug
            {
                EstReussi = true,
                Motif = string.Empty,
                Chemin = scenario.CheminSourceLocale,
            };
        }
        catch (Exception exception)
        {
            return InvalideExecution(
                $"{exception.GetType().Name}:{exception.Message}",
                scenario.CheminSourceLocale
            );
        }
    }

    public static SignalSuccesLocal? ConstruireSignalSourceLocale(ScenarioTestSuccesDebug scenario)
    {
        if (
            scenario.ModeDeclenchement != ModeDeclenchementTestSuccesDebug.SourceLocale
            || string.IsNullOrWhiteSpace(scenario.NomEmulateur)
            || string.IsNullOrWhiteSpace(scenario.TypeSourceLocale)
            || string.IsNullOrWhiteSpace(scenario.CheminSourceLocale)
        )
        {
            return null;
        }

        return new SignalSuccesLocal
        {
            NomEmulateur = scenario.NomEmulateur,
            TypeSource = scenario.TypeSourceLocale,
            Chemin = scenario.CheminSourceLocale,
            HorodatageUtc = DateTimeOffset.UtcNow,
        };
    }

    private static ResultatScenarioTestSuccesDebug Invalide(string motif)
    {
        return new ResultatScenarioTestSuccesDebug { EstValide = false, Motif = motif };
    }

    private static ResultatExecutionTestSuccesDebug InvalideExecution(string motif, string chemin)
    {
        return new ResultatExecutionTestSuccesDebug
        {
            EstReussi = false,
            Motif = motif,
            Chemin = chemin,
        };
    }

    private static (
        string SourceSimulee,
        string TypeSourceLocale,
        string CheminSourceLocale
    ) ConstruireConfigurationSource(
        string nomEmulateur,
        ModeDeclenchementTestSuccesDebug modeDeclenchement
    )
    {
        string sourceSimulee = ConstruireSourceSimulee(nomEmulateur, modeDeclenchement);

        if (modeDeclenchement != ModeDeclenchementTestSuccesDebug.SourceLocale)
        {
            return (sourceSimulee, string.Empty, string.Empty);
        }

        string typeSourceLocale =
            ServiceCatalogueEmulateursLocaux.ObtenirTypeSourceJournalSuccesLocal(nomEmulateur);

        if (string.IsNullOrWhiteSpace(typeSourceLocale))
        {
            return (sourceSimulee, string.Empty, string.Empty);
        }

        return nomEmulateur.Trim() switch
        {
            "RetroArch" => (
                sourceSimulee,
                typeSourceLocale,
                TrouverCheminJournalRetroArchPourInjection()
            ),
            _ => (
                sourceSimulee,
                typeSourceLocale,
                ServiceSourcesLocalesEmulateurs.TrouverCheminJournalSuccesLocal(nomEmulateur)
            ),
        };
    }

    private static string ConstruireSourceSimulee(
        string nomEmulateur,
        ModeDeclenchementTestSuccesDebug modeDeclenchement
    )
    {
        if (string.IsNullOrWhiteSpace(nomEmulateur))
        {
            return modeDeclenchement switch
            {
                ModeDeclenchementTestSuccesDebug.SourceLocale => "debug_source_locale",
                ModeDeclenchementTestSuccesDebug.Session => "debug_session",
                _ => "debug_interne",
            };
        }

        string suffixe = modeDeclenchement switch
        {
            ModeDeclenchementTestSuccesDebug.SourceLocale => "_log_test",
            ModeDeclenchementTestSuccesDebug.Session => "_session_test",
            _ => "_interne_test",
        };

        return nomEmulateur.Trim() switch
        {
            "RetroArch" => $"retroarch{suffixe}",
            "RALibretro" => $"ralibretro{suffixe}",
            "LunaProject64" => $"lunaproject64{suffixe}",
            "RAP64" => $"rap64{suffixe}",
            "DuckStation" => $"duckstation{suffixe}",
            "PCSX2" => $"pcsx2{suffixe}",
            "PPSSPP" => $"ppsspp{suffixe}",
            _ => $"debug_{nomEmulateur.Trim().ToLowerInvariant()}{suffixe}",
        };
    }

    private static List<string> ConstruireLignesInjection(ScenarioTestSuccesDebug scenario)
    {
        return string.Equals(
            ServiceCatalogueEmulateursLocaux.ObtenirTypeSourceJournalSuccesLocal(
                scenario.NomEmulateur
            ),
            "logs",
            StringComparison.Ordinal
        )
            ?
            [
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"[INFO] [RCHEEVOS] Awarding achievement {scenario.IdentifiantSucces}: {scenario.TitreSucces}"
                ),
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"[INFO] [RCHEEVOS] Achievement {scenario.IdentifiantSucces} awarded, new score: 0"
                ),
            ]
            :
            [
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{DateTime.Now:HHmmss.fff}|INFO| Awarding achievement {scenario.IdentifiantSucces}: {scenario.TitreSucces}"
                ),
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{DateTime.Now.AddMilliseconds(50):HHmmss.fff}|INFO| Achievement {scenario.IdentifiantSucces} awarded"
                ),
            ];
    }

    private static string TrouverCheminJournalRetroArchPourInjection()
    {
        string repertoireLogs = ServiceSourcesLocalesEmulateurs.TrouverRepertoireLogsRetroArch();
        string cheminJournal = ServiceSourcesLocalesEmulateurs.TrouverCheminJournalSuccesLocal(
            "RetroArch"
        );

        if (!string.IsNullOrWhiteSpace(cheminJournal))
        {
            return cheminJournal;
        }

        if (string.IsNullOrWhiteSpace(repertoireLogs))
        {
            return string.Empty;
        }

        return Path.Combine(repertoireLogs, $"retroarch__{DateTime.Now:yyyy_MM_dd__HH_mm_ss}.log");
    }

    private static string Nettoyer(string? valeur)
    {
        return string.IsNullOrWhiteSpace(valeur)
            ? string.Empty
            : valeur.Replace("\r", " ").Replace("\n", " ").Trim();
    }
}
