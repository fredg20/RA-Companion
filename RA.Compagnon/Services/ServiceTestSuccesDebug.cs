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
        try
        {
            string? repertoire = Path.GetDirectoryName(CheminJournalTestSucces);

            if (!string.IsNullOrWhiteSpace(repertoire))
            {
                Directory.CreateDirectory(repertoire);
            }

            File.AppendAllText(
                CheminJournalTestSucces,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] evenement={Nettoyer(evenement)};details={Nettoyer(details)}{Environment.NewLine}"
                )
            );
        }
        catch
        {
            // Ce journal reste auxiliaire.
        }
    }

    public ResultatScenarioTestSuccesDebug ConstruireScenarioDepuisContexte(
        string nomEmulateur,
        int identifiantJeu,
        string titreJeu,
        IReadOnlyList<GameAchievementV2> succesJeuCourant,
        Func<GameAchievementV2, bool> succesEstDebloque
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

        ScenarioTestSuccesDebug scenario =
            new()
            {
                NomEmulateur = nomEmulateur.Trim(),
                SourceSimulee = ConstruireSourceSimulee(nomEmulateur),
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
                ModeDeclenchement = ModeDeclenchementTestSuccesDebug.InterneUi,
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

    private static ResultatScenarioTestSuccesDebug Invalide(string motif)
    {
        return new ResultatScenarioTestSuccesDebug
        {
            EstValide = false,
            Motif = motif,
        };
    }

    private static string ConstruireSourceSimulee(string nomEmulateur)
    {
        if (string.IsNullOrWhiteSpace(nomEmulateur))
        {
            return "debug_interne";
        }

        return nomEmulateur.Trim() switch
        {
            "RetroArch" => "retroarch_log_test",
            "RALibretro" => "ralibretro_log_test",
            "LunaProject64" => "lunaproject64_log_test",
            _ => "debug_interne",
        };
    }

    private static string Nettoyer(string? valeur)
    {
        return string.IsNullOrWhiteSpace(valeur)
            ? string.Empty
            : valeur.Replace("\r", " ").Replace("\n", " ").Trim();
    }
}
