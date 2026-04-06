using RA.Compagnon.Modeles.Local;

namespace RA.Compagnon.Services;

public static class ServiceCatalogueEmulateursLocaux
{
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
            StrategieRenseignementJeuEmulateurLocal.Aucune,
            StrategieSurveillanceSuccesLocale.Aucune,
            true,
            false,
            []
        ),
        new(
            "LunaProject64",
            ["project64"],
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

    public static DefinitionEmulateurLocal? TrouverParNom(string nomEmulateur)
    {
        return Definitions.FirstOrDefault(definition =>
            string.Equals(definition.NomEmulateur, nomEmulateur, StringComparison.Ordinal)
        );
    }

    public static bool EstEmulateurValide(string nomEmulateur)
    {
        DefinitionEmulateurLocal? definition = TrouverParNom(nomEmulateur);
        return definition is not null && EstEmulateurValide(definition);
    }

    public static bool EstEmulateurValide(DefinitionEmulateurLocal definition)
    {
        return definition.StrategieRenseignementJeu
                != StrategieRenseignementJeuEmulateurLocal.Aucune
            || definition.StrategieSurveillanceSucces != StrategieSurveillanceSuccesLocale.Aucune;
    }

    public static string[] ObtenirAliasConsoles(string nomEmulateur)
    {
        return TrouverParNom(nomEmulateur)?.AliasConsoles ?? [];
    }

    public static bool EstSuccesLocalDirectPrisEnCharge(string nomEmulateur)
    {
        return TrouverParNom(nomEmulateur)?.SupporteSuccesLocalDirect == true;
    }

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

    public static string ObtenirTypeSourceJournalSuccesLocal(string nomEmulateur)
    {
        return TrouverParNom(nomEmulateur)?.StrategieRenseignementJeu switch
        {
            StrategieRenseignementJeuEmulateurLocal.RetroArchLog => "logs",
            StrategieRenseignementJeuEmulateurLocal.FlycastConfig => "logs",
            StrategieRenseignementJeuEmulateurLocal.DuckStationLog => "logs",
            StrategieRenseignementJeuEmulateurLocal.PCSX2Log => "logs",
            StrategieRenseignementJeuEmulateurLocal.PPSSPPLog => "logs",
            StrategieRenseignementJeuEmulateurLocal.Project64RACache => "racache_log",
            StrategieRenseignementJeuEmulateurLocal.RALibretroRACache => "racache_log",
            StrategieRenseignementJeuEmulateurLocal.RANesRACache => "racache_log",
            StrategieRenseignementJeuEmulateurLocal.RAVBARACache => "racache_log",
            StrategieRenseignementJeuEmulateurLocal.RASnes9xRACache => "racache_log",
            _ => string.Empty,
        };
    }

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

    public static bool NecessiteSignalInitialSurveillance(string nomEmulateur)
    {
        return TrouverParNom(nomEmulateur)?.StrategieSurveillanceSucces
            == StrategieSurveillanceSuccesLocale.RetroArchLogs;
    }

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
