using RA.Compagnon.Modeles.Local;

/*
 * Déclare le catalogue des émulateurs locaux pris en charge et les règles
 * associées à leur détection ou à leur surveillance de succès.
 */
namespace RA.Compagnon.Services;

/*
 * Fournit les métadonnées descriptives de chaque émulateur connu de Compagnon.
 */
public static class ServiceCatalogueEmulateursLocaux
{
    /*
     * Expose la liste complète des définitions d'émulateurs connues.
     */
    public static IReadOnlyList<DefinitionEmulateurLocal> Definitions { get; } =
    [
        new(
            "RetroArch",
            ["retroarch"],
            [],
            StrategieExtractionTitreEmulateurLocal.SeparateursRetroArch,
            StrategieRenseignementJeuEmulateurLocal.RetroArchLog,
            StrategieSurveillanceSuccesLocale.RetroArchLogs,
            false,
            true,
            ["logs"]
        ),
        new(
            "RALibretro",
            ["ralibretro"],
            [],
            StrategieExtractionTitreEmulateurLocal.RALibretro,
            StrategieRenseignementJeuEmulateurLocal.RALibretroRACache,
            StrategieSurveillanceSuccesLocale.RALibretroRACache,
            true,
            true,
            ["racache"]
        ),
        new(
            "BizHawk",
            ["emuhawk", "bizhawk"],
            [],
            StrategieExtractionTitreEmulateurLocal.BizHawk,
            StrategieRenseignementJeuEmulateurLocal.BizHawkConfig,
            StrategieSurveillanceSuccesLocale.JournalLogsSimple,
            false,
            true,
            ["logs"]
        ),
        new(
            "DuckStation",
            [
                "duckstation",
                "duckstation-qt",
                "duckstation-nogui",
                "duckstation-sdl",
                "duckstation-x64",
                "duckstation-avx2",
                "duckstation-qt-x64",
                "duckstation-qt-x64-releaseltcg",
                "duckstation-qt-x64-release",
                "duckstation-qt-releaseltcg",
            ],
            ["playstation", "sony playstation", "ps1", "psx", "ps one"],
            StrategieExtractionTitreEmulateurLocal.DuckStation,
            StrategieRenseignementJeuEmulateurLocal.DuckStationLog,
            StrategieSurveillanceSuccesLocale.JournalLogsSimple,
            false,
            true,
            ["logs"]
        ),
        new(
            "PCSX2",
            ["pcsx2", "pcsx2-qt"],
            ["playstation 2", "ps2"],
            StrategieExtractionTitreEmulateurLocal.PCSX2,
            StrategieRenseignementJeuEmulateurLocal.PCSX2Log,
            StrategieSurveillanceSuccesLocale.JournalLogsSimple,
            false,
            true,
            ["logs"]
        ),
        new(
            "PPSSPP",
            ["ppsspp", "ppssppwindows", "ppssppwindows64"],
            ["playstation portable", "psp"],
            StrategieExtractionTitreEmulateurLocal.PPSSPP,
            StrategieRenseignementJeuEmulateurLocal.PPSSPPLog,
            StrategieSurveillanceSuccesLocale.JournalLogsSimple,
            true,
            true,
            ["logs"]
        ),
        new(
            "SkyEmu",
            ["skyemu"],
            [
                "game boy",
                "gb",
                "game boy color",
                "gbc",
                "game boy advance",
                "gba",
                "nintendo ds",
                "nds",
                "ds",
            ],
            StrategieExtractionTitreEmulateurLocal.SkyEmu,
            StrategieRenseignementJeuEmulateurLocal.SkyEmuRecentGames,
            StrategieSurveillanceSuccesLocale.Aucune,
            false,
            false,
            []
        ),
        new(
            "RANes",
            ["ranes"],
            ["nintendo entertainment system", "nes", "famicom"],
            StrategieExtractionTitreEmulateurLocal.RANes,
            StrategieRenseignementJeuEmulateurLocal.RANesRACache,
            StrategieSurveillanceSuccesLocale.RANesRACache,
            true,
            true,
            ["racache"]
        ),
        new(
            "RAVBA",
            ["ravba", "ravba-m", "ravisualboyadvance", "ravisualboyadvance-m"],
            ["game boy", "gb", "game boy color", "gbc", "game boy advance", "gba"],
            StrategieExtractionTitreEmulateurLocal.RAVBA,
            StrategieRenseignementJeuEmulateurLocal.RAVBARACache,
            StrategieSurveillanceSuccesLocale.RAVBARACache,
            true,
            true,
            ["racache"]
        ),
        new(
            "RASnes9x",
            ["rasnes9x"],
            ["super nintendo", "super nintendo entertainment system", "snes"],
            StrategieExtractionTitreEmulateurLocal.RASnes9x,
            StrategieRenseignementJeuEmulateurLocal.RASnes9xRACache,
            StrategieSurveillanceSuccesLocale.RASnes9xRACache,
            true,
            true,
            ["racache"]
        ),
        new(
            "Dolphin",
            [
                "dolphin",
                "dolphin-qt2",
                "dolphin emulator",
                "slippi dolphin",
                "slippi dolphin launcher",
            ],
            ["gamecube", "nintendo gamecube", "wii", "nintendo wii", "wiiware"],
            StrategieExtractionTitreEmulateurLocal.Dolphin,
            StrategieRenseignementJeuEmulateurLocal.DolphinConfig,
            StrategieSurveillanceSuccesLocale.JournalLogsSimple,
            true,
            true,
            ["logs"]
        ),
        new(
            "RAP64",
            [
                "rap64",
                "raproject64",
                "raproject64d",
                "raproject64-debug",
                "raproject64-release",
                "raproject64-final",
                "raproject64-dev",
                "raproject64-test",
                "raproject64-nightly",
                "raproject64-canary",
                "raproject64-preview",
                "raproject64-beta",
                "raproject64-alpha",
                "raproject64-qt",
                "raproject64x64",
                "raproject64-x64",
                "raproject64-win64",
                "raproject64-win32",
                "raproject64.exe",
                "raproject64_d",
                "raproject64_d.exe",
                "raproject64d.exe",
            ],
            ["nintendo 64", "n64"],
            StrategieExtractionTitreEmulateurLocal.Project64,
            StrategieRenseignementJeuEmulateurLocal.Project64RACache,
            StrategieSurveillanceSuccesLocale.Project64RACache,
            true,
            true,
            ["racache"]
        ),
        new(
            "Flycast",
            ["flycast"],
            ["dreamcast", "naomi", "atomiswave"],
            StrategieExtractionTitreEmulateurLocal.Flycast,
            StrategieRenseignementJeuEmulateurLocal.FlycastConfig,
            StrategieSurveillanceSuccesLocale.JournalLogsSimple,
            false,
            true,
            ["logs"]
        ),
    ];

    /*
     * Recherche une définition d'émulateur par son nom logique.
     */
    public static DefinitionEmulateurLocal? TrouverParNom(string nomEmulateur)
    {
        return Definitions.FirstOrDefault(definition =>
            string.Equals(definition.NomEmulateur, nomEmulateur, StringComparison.Ordinal)
        );
    }

    /*
     * Indique si l'émulateur nommé possède au moins une stratégie exploitable
     * par Compagnon.
     */
    public static bool EstEmulateurValide(string nomEmulateur)
    {
        DefinitionEmulateurLocal? definition = TrouverParNom(nomEmulateur);
        return definition is not null && EstEmulateurValide(definition);
    }

    /*
     * Indique si une définition d'émulateur est réellement exploitable.
     */
    public static bool EstEmulateurValide(DefinitionEmulateurLocal definition)
    {
        return definition.StrategieRenseignementJeu
                != StrategieRenseignementJeuEmulateurLocal.Aucune
            || definition.StrategieSurveillanceSucces != StrategieSurveillanceSuccesLocale.Aucune;
    }

    /*
     * Retourne les alias de consoles associés à un émulateur donné.
     */
    public static string[] ObtenirAliasConsoles(string nomEmulateur)
    {
        return TrouverParNom(nomEmulateur)?.AliasConsoles ?? [];
    }

    /*
     * Indique si l'émulateur peut fournir une détection directe d'un succès
     * local sans repasser par l'API.
     */
    public static bool EstSuccesLocalDirectPrisEnCharge(string nomEmulateur)
    {
        return TrouverParNom(nomEmulateur)?.SupporteSuccesLocalDirect == true;
    }

    /*
     * Vérifie si un type de source local est compatible avec la détection
     * directe des succès pour cet émulateur.
     */
    public static bool TypeSourcePeutPorterSuccesDirect(string nomEmulateur, string typeSource)
    {
        DefinitionEmulateurLocal? definition = TrouverParNom(nomEmulateur);

        if (definition is null || !definition.SupporteSuccesLocalDirect)
        {
            return false;
        }

        return definition.PrefixesSourcesSuccesDirectes.Any(prefixe =>
            typeSource.StartsWith(prefixe, StringComparison.Ordinal)
        );
    }

    /*
     * Retourne le type de source journal attendu pour la surveillance de
     * succès d'un émulateur donné.
     */
    public static string ObtenirTypeSourceJournalSuccesLocal(string nomEmulateur)
    {
        return TrouverParNom(nomEmulateur)?.StrategieRenseignementJeu switch
        {
            StrategieRenseignementJeuEmulateurLocal.RetroArchLog => "logs",
            StrategieRenseignementJeuEmulateurLocal.BizHawkConfig => "logs",
            StrategieRenseignementJeuEmulateurLocal.DolphinConfig => "config",
            StrategieRenseignementJeuEmulateurLocal.FlycastConfig => "logs",
            StrategieRenseignementJeuEmulateurLocal.DuckStationLog => "logs",
            StrategieRenseignementJeuEmulateurLocal.PCSX2Log => "logs",
            StrategieRenseignementJeuEmulateurLocal.PPSSPPLog => "logs",
            StrategieRenseignementJeuEmulateurLocal.SkyEmuRecentGames => "recent_games",
            StrategieRenseignementJeuEmulateurLocal.Project64RACache => "racache_log",
            StrategieRenseignementJeuEmulateurLocal.RALibretroRACache => "racache_log",
            StrategieRenseignementJeuEmulateurLocal.RANesRACache => "racache_log",
            StrategieRenseignementJeuEmulateurLocal.RAVBARACache => "racache_log",
            StrategieRenseignementJeuEmulateurLocal.RASnes9xRACache => "racache_log",
            _ => string.Empty,
        };
    }

    /*
     * Indique si la surveillance locale des succès est active pour cet
     * émulateur.
     */
    public static bool SurveillanceSuccesActive(string nomEmulateur)
    {
        return TrouverParNom(nomEmulateur)?.StrategieSurveillanceSucces switch
        {
            StrategieSurveillanceSuccesLocale.RetroArchLogs => true,
            StrategieSurveillanceSuccesLocale.JournalLogsSimple => true,
            StrategieSurveillanceSuccesLocale.Project64RACache => true,
            StrategieSurveillanceSuccesLocale.RALibretroRACache => true,
            StrategieSurveillanceSuccesLocale.RANesRACache => true,
            StrategieSurveillanceSuccesLocale.RAVBARACache => true,
            StrategieSurveillanceSuccesLocale.RASnes9xRACache => true,
            _ => false,
        };
    }

    /*
     * Indique si l'émulateur nécessite un signal initial pour amorcer la
     * surveillance de ses succès.
     */
    public static bool NecessiteSignalInitialSurveillance(string nomEmulateur)
    {
        return TrouverParNom(nomEmulateur)?.StrategieSurveillanceSucces
            == StrategieSurveillanceSuccesLocale.RetroArchLogs;
    }

    /*
     * Indique si le type de source reçu doit planifier un suivi actif des
     * succès pour l'émulateur ciblé.
     */
    public static bool TypeSourceDoitPlanifierSuivi(string nomEmulateur, string typeSource)
    {
        DefinitionEmulateurLocal? definition = TrouverParNom(nomEmulateur);

        if (definition is null)
        {
            return false;
        }

        return definition.StrategieSurveillanceSucces switch
        {
            StrategieSurveillanceSuccesLocale.RetroArchLogs => typeSource == "logs",
            StrategieSurveillanceSuccesLocale.JournalLogsSimple => typeSource == "logs",
            StrategieSurveillanceSuccesLocale.Project64RACache => typeSource == "racache_log",
            StrategieSurveillanceSuccesLocale.RALibretroRACache => typeSource
                is "racache_log"
                    or "racache_data",
            StrategieSurveillanceSuccesLocale.RANesRACache => typeSource
                is "racache_log"
                    or "racache_data",
            StrategieSurveillanceSuccesLocale.RAVBARACache => typeSource
                is "racache_log"
                    or "racache_data",
            StrategieSurveillanceSuccesLocale.RASnes9xRACache => typeSource
                is "racache_log"
                    or "racache_data",
            _ => false,
        };
    }
}