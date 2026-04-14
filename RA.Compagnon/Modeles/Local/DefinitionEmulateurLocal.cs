/*
 * Déclare les énumérations et la définition d'un émulateur local connu par
 * Compagnon.
 */
namespace RA.Compagnon.Modeles.Local;

/*
 * Décrit la stratégie utilisée pour extraire le titre du jeu courant d'un
 * émulateur local.
 */
public enum StrategieExtractionTitreEmulateurLocal
{
    SeparateursRetroArch,
    RALibretro,
    BizHawk,
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

/*
 * Décrit la stratégie utilisée pour déterminer le jeu courant dans un
 * émulateur local.
 */
public enum StrategieRenseignementJeuEmulateurLocal
{
    Aucune,
    BizHawkConfig,
    DolphinConfig,
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

/*
 * Décrit la stratégie utilisée pour surveiller localement les succès d'un
 * émulateur.
 */
public enum StrategieSurveillanceSuccesLocale
{
    Aucune,
    RetroArchLogs,
    JournalLogsSimple,
    Project64RACache,
    RALibretroRACache,
    RANesRACache,
    RAVBARACache,
    RASnes9xRACache,
}

/*
 * Décrit un émulateur local, ses stratégies de détection et ses capacités
 * de surveillance ou de détection directe des succès.
 */
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
