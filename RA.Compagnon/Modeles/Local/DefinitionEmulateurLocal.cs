namespace RA.Compagnon.Modeles.Local;

public enum StrategieExtractionTitreEmulateurLocal
{
    SeparateursRetroArch,
    RALibretro,
    DuckStation,
    PCSX2,
    PPSSPP,
    Dolphin,
    Project64,
    Flycast,
    RANes,
    RAVBA,
    RASnes9x,
}

public enum StrategieRenseignementJeuEmulateurLocal
{
    Aucune,
    Project64RACache,
    RALibretroRACache,
    RANesRACache,
    RAVBARACache,
    RASnes9xRACache,
    FlycastConfig,
    RetroArchLog,
    DuckStationLog,
    PCSX2Log,
    PPSSPPLog,
}

public enum StrategieSurveillanceSuccesLocale
{
    Aucune,
    RetroArchLogs,
    Project64RACache,
    RALibretroRACache,
    RANesRACache,
    RAVBARACache,
    RASnes9xRACache,
}

public sealed record DefinitionEmulateurLocal(
    string NomEmulateur,
    string[] NomsProcessus,
    string[] AliasConsoles,
    StrategieExtractionTitreEmulateurLocal StrategieExtractionTitre,
    StrategieRenseignementJeuEmulateurLocal StrategieRenseignementJeu,
    StrategieSurveillanceSuccesLocale StrategieSurveillanceSucces,
    bool AutoriserDetectionParTitreFenetre,
    bool SupporteSuccesLocalDirect,
    string[] PrefixesSourcesSuccesDirectes
);
