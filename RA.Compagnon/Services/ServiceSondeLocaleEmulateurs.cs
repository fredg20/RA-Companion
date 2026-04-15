using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows.Automation;
using RA.Compagnon.Modeles.Api.V2.Game;
using RA.Compagnon.Modeles.Local;

/*
 * Sonde les sources locales des émulateurs compatibles afin d'identifier
 * le jeu en cours, son contexte de relance et certains déblocages locaux.
 */
namespace RA.Compagnon.Services;

/*
 * Centralise les heuristiques de détection locale utilisées pour lire les
 * journaux, configurations, fenêtres et caches propres aux émulateurs.
 */
public sealed partial class ServiceSondeLocaleEmulateurs
{
    /*
     * Représente un renseignement jeu minimal issu d'une source locale RA.
     */
    private sealed record RenseignementJeuRA(int IdentifiantJeu, string TitreJeu);

    /*
     * Représente l'état minimal remonté par le serveur HTTP de SkyEmu.
     */
    private sealed record EtatHttpSkyEmu(int Port, string CheminRom);

    /*
     * Représente l'état local d'un succès BizHawk selon ses modes de déblocage.
     */
    private sealed record EtatSuccesBizHawk(bool DebloqueSoftcore, bool DebloqueHardcore);

    /*
     * Représente un renseignement de succès minimal issu d'un journal local RA.
     */
    public sealed record RenseignementSuccesRA(int IdentifiantSucces, string TitreSucces);

    /*
     * Représente le délégué natif utilisé pour énumérer les fenêtres visibles.
     */
    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    private const int ProcessCommandLineInformation = 60;
    private static readonly Lock VerrouCacheSuccesBizHawk = new();
    private static readonly Lock VerrouCacheDuckStation = new();
    private static readonly Lock VerrouCacheProject64 = new();
    private static readonly Lock VerrouCacheRALibretro = new();
    private static readonly Lock VerrouCachePPSSPP = new();
    private static readonly Lock VerrouCacheRetroArch = new();
    private static readonly Lock VerrouCacheFlycast = new();
    private static string _dernierRepertoireDuckStation = string.Empty;
    private static DateTime _dernierHorodatageCacheGamelistUtc = DateTime.MinValue;
    private static Dictionary<string, string> _cacheSerialVersCheminDuckStation = [];
    private static readonly Dictionary<string, RenseignementJeuRA> _cacheRenseignementProject64 =
    [];
    private static readonly Dictionary<
        string,
        DateTime
    > _cacheHorodatageRenseignementProject64Utc = [];
    private static RenseignementJeuRA? _dernierRenseignementRALibretro;
    private static DateTime _dernierHorodatageRenseignementRALibretroUtc = DateTime.MinValue;
    private static RenseignementJeuRA? _dernierRenseignementPPSSPP;
    private static DateTime _dernierHorodatageRenseignementPPSSPPUtc = DateTime.MinValue;
    private static RenseignementJeuRA? _dernierRenseignementRetroArch;
    private static DateTime _dernierHorodatageRenseignementRetroArchUtc = DateTime.MinValue;
    private static string _dernierRepertoireContenuFlycast = string.Empty;
    private static DateTime _dernierHorodatageCacheContenuFlycastUtc = DateTime.MinValue;
    private static List<string> _cacheFichiersJeuFlycast = [];
    private static int _dernierIdentifiantJeuSuccesBizHawk;
    private static Dictionary<int, EtatSuccesBizHawk> _cacheSuccesBizHawk = [];

    private static readonly IReadOnlyList<DefinitionEmulateurLocal> Definitions =
        ServiceCatalogueEmulateursLocaux.Definitions;

    private static readonly string CheminJournalSondeLocale = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "RA-Compagnon",
        "journal-sonde-locale.log"
    );
    private static readonly HttpClient HttpClientSkyEmu = new()
    {
        Timeout = TimeSpan.FromMilliseconds(250),
    };
    private string _derniereSignatureJournalisee = "\u0000";

    /*
     * Réinitialise le journal de session de la sonde locale.
     */
    public static void ReinitialiserJournalSession()
    {
        _ = ServiceModeDiagnostic.ReinitialiserJournalSession(CheminJournalSondeLocale);
    }

    /*
     * Retourne le chemin du journal produit par la sonde locale.
     */
    public static string ObtenirCheminJournal() => CheminJournalSondeLocale;

    /*
     * Tente de reconstruire un contexte de rejouer complet à partir des
     * sources locales connues pour un émulateur donné.
     */
    public static bool EssayerObtenirContexteRejouerDepuisSources(
        string nomEmulateur,
        out int identifiantJeu,
        out string titreJeu,
        out string cheminExecutable,
        out string cheminJeu
    )
    {
        identifiantJeu = 0;
        titreJeu = string.Empty;
        cheminExecutable = string.Empty;
        cheminJeu = string.Empty;

        if (string.IsNullOrWhiteSpace(nomEmulateur))
        {
            return false;
        }

        DefinitionEmulateurLocal? definition = ServiceCatalogueEmulateursLocaux.TrouverParNom(
            nomEmulateur
        );

        if (definition is null || !ServiceCatalogueEmulateursLocaux.EstEmulateurValide(definition))
        {
            return false;
        }

        cheminExecutable = ServiceSourcesLocalesEmulateurs.TrouverEmplacementEmulateur(
            definition.NomEmulateur
        );

        if (string.IsNullOrWhiteSpace(cheminExecutable) || !File.Exists(cheminExecutable))
        {
            return false;
        }

        RenseignementJeuRA? renseignementJeu = definition.StrategieRenseignementJeu switch
        {
            StrategieRenseignementJeuEmulateurLocal.RetroArchLog =>
                LireRenseignementJeuRetroArchDepuisLog(),
            StrategieRenseignementJeuEmulateurLocal.BizHawkConfig =>
                LireRenseignementJeuBizHawkDepuisConfiguration(),
            StrategieRenseignementJeuEmulateurLocal.DolphinConfig =>
                LireRenseignementJeuDolphinDepuisLog(),
            StrategieRenseignementJeuEmulateurLocal.DuckStationLog =>
                LireRenseignementJeuDuckStationDepuisLog(),
            StrategieRenseignementJeuEmulateurLocal.PCSX2Log =>
                LireRenseignementJeuPCSX2DepuisLog(),
            StrategieRenseignementJeuEmulateurLocal.PPSSPPLog =>
                LireRenseignementJeuPPSSPPDepuisLog(string.Empty),
            StrategieRenseignementJeuEmulateurLocal.SkyEmuRecentGames =>
                LireRenseignementJeuSkyEmuDepuisSourcesRejouer(),
            StrategieRenseignementJeuEmulateurLocal.FlycastConfig =>
                LireRenseignementJeuFlycastDepuisSourcesRejouer(),
            StrategieRenseignementJeuEmulateurLocal.Project64RACache =>
                LireRenseignementJeuProject64DepuisRACache(definition.NomEmulateur),
            StrategieRenseignementJeuEmulateurLocal.RALibretroRACache =>
                LireRenseignementJeuRALibretroDepuisRACache(),
            StrategieRenseignementJeuEmulateurLocal.RANesRACache =>
                LireRenseignementJeuRANesDepuisRACache(),
            StrategieRenseignementJeuEmulateurLocal.RAVBARACache =>
                LireRenseignementJeuRAVBADepuisRACache(),
            StrategieRenseignementJeuEmulateurLocal.RASnes9xRACache =>
                LireRenseignementJeuRASnes9xDepuisRACache(),
            _ => null,
        };

        identifiantJeu = renseignementJeu?.IdentifiantJeu ?? 0;
        titreJeu = renseignementJeu?.TitreJeu ?? string.Empty;

        cheminJeu = definition.StrategieRenseignementJeu switch
        {
            StrategieRenseignementJeuEmulateurLocal.RetroArchLog =>
                LireCheminJeuRetroArchDepuisLog(),
            StrategieRenseignementJeuEmulateurLocal.BizHawkConfig =>
                LireCheminJeuBizHawkDepuisConfiguration(),
            StrategieRenseignementJeuEmulateurLocal.DolphinConfig => LireCheminJeuDolphin(titreJeu),
            StrategieRenseignementJeuEmulateurLocal.DuckStationLog =>
                LireCheminJeuDuckStationDepuisLog(),
            StrategieRenseignementJeuEmulateurLocal.PCSX2Log => LireCheminJeuPCSX2DepuisLog(),
            StrategieRenseignementJeuEmulateurLocal.PPSSPPLog => LireCheminJeuPPSSPPDepuisLog(),
            StrategieRenseignementJeuEmulateurLocal.SkyEmuRecentGames =>
                LireCheminJeuSkyEmuDepuisRecentGames(),
            StrategieRenseignementJeuEmulateurLocal.FlycastConfig =>
                LireCheminJeuFlycastDepuisLogOuConfiguration(titreJeu),
            StrategieRenseignementJeuEmulateurLocal.Project64RACache =>
                LireCheminJeuProject64DepuisConfiguration(definition.NomEmulateur),
            StrategieRenseignementJeuEmulateurLocal.RALibretroRACache =>
                LireCheminJeuRALibretroDepuisConfiguration(),
            StrategieRenseignementJeuEmulateurLocal.RANesRACache =>
                LireCheminJeuRANesDepuisConfiguration(titreJeu),
            StrategieRenseignementJeuEmulateurLocal.RAVBARACache =>
                LireCheminJeuRAVBADepuisConfiguration(),
            StrategieRenseignementJeuEmulateurLocal.RASnes9xRACache =>
                LireCheminJeuRASnes9xDepuisConfiguration(),
            _ => string.Empty,
        };

        return identifiantJeu > 0
            && !string.IsNullOrWhiteSpace(cheminJeu)
            && File.Exists(cheminJeu);
    }

    /*
     * Tente de reconstruire un contexte de rejeu à partir d'un titre connu
     * lorsque la source locale ne fournit pas directement d'identifiant RA.
     */
    public static bool EssayerObtenirContexteRejouerDepuisTitre(
        string nomEmulateur,
        string titreJeuAttendu,
        out string cheminExecutable,
        out string cheminJeu
    )
    {
        cheminExecutable = string.Empty;
        cheminJeu = string.Empty;

        if (string.IsNullOrWhiteSpace(nomEmulateur) || string.IsNullOrWhiteSpace(titreJeuAttendu))
        {
            return false;
        }

        DefinitionEmulateurLocal? definition = ServiceCatalogueEmulateursLocaux.TrouverParNom(
            nomEmulateur
        );

        if (definition is null || !ServiceCatalogueEmulateursLocaux.EstEmulateurValide(definition))
        {
            return false;
        }

        cheminExecutable = ServiceSourcesLocalesEmulateurs.TrouverEmplacementEmulateur(
            definition.NomEmulateur
        );

        if (string.IsNullOrWhiteSpace(cheminExecutable) || !File.Exists(cheminExecutable))
        {
            return false;
        }

        cheminJeu = definition.StrategieRenseignementJeu switch
        {
            StrategieRenseignementJeuEmulateurLocal.SkyEmuRecentGames =>
                LireCheminJeuSkyEmuDepuisRecentGames(titreJeuAttendu),
            _ => string.Empty,
        };

        return !string.IsNullOrWhiteSpace(cheminJeu) && File.Exists(cheminJeu);
    }

    /*
     * Journalise un événement lié à la sonde locale pour faciliter le diagnostic.
     */
    public static void JournaliserEvenement(string evenement, string details)
    {
        _ = ServiceModeDiagnostic.JournaliserLigne(
            CheminJournalSondeLocale,
            string.Create(
                CultureInfo.InvariantCulture,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] evenement={NettoyerPourJournal(evenement)};details={NettoyerPourJournal(details)}{Environment.NewLine}"
            )
        );
    }

    /*
     * Sonde les émulateurs connus et retourne le meilleur état local trouvé.
     */
    public EtatSondeLocaleEmulateur Sonder(bool journaliser = true)
    {
        try
        {
            Process[] processus = Process.GetProcesses();

            foreach (DefinitionEmulateurLocal definition in Definitions)
            {
                EtatSondeLocaleEmulateur? etat = SonderPourDefinition(definition, processus);

                if (etat is not null)
                {
                    if (journaliser)
                    {
                        JournaliserSiChangement(etat);
                    }

                    return etat;
                }
            }
        }
        catch { }

        EtatSondeLocaleEmulateur etatAucun = new()
        {
            EmulateurDetecte = false,
            Signature = string.Empty,
            HorodatageUtc = DateTimeOffset.UtcNow,
        };

        if (journaliser)
        {
            JournaliserSiChangement(etatAucun);
        }

        return etatAucun;
    }

    /*
     * Indique si au moins un émulateur compatible semble actuellement présent.
     */
    public static bool SonderPresenceEmulateur()
    {
        try
        {
            Process[] processus = Process.GetProcesses();
            return Definitions.Any(definition =>
                processus.Any(processusCourant =>
                    CorrespondNomProcessus(processusCourant, definition)
                )
            );
        }
        catch
        {
            return false;
        }
    }

    /*
     * Construit l'état local pour une définition d'émulateur précise.
     */
    private static EtatSondeLocaleEmulateur? SonderPourDefinition(
        DefinitionEmulateurLocal definition,
        IEnumerable<Process> processus
    )
    {
        Process? processusCible = processus
            .Where(processusCourant => Correspond(processusCourant, definition))
            .OrderByDescending(ProcessusPossedeUneFenetreVisible)
            .ThenByDescending(processusCourant => LireTitreFenetre(processusCourant).Length)
            .FirstOrDefault();

        if (processusCible is null)
        {
            return null;
        }

        bool correspondAuNomProcessus = CorrespondNomProcessus(processusCible, definition);
        bool correspondAuxMetadonneesExecutable = CorrespondMetadonneesExecutable(
            processusCible,
            definition
        );
        IReadOnlyList<string> titresFenetres = LireTitresFenetresVisibles(processusCible);
        string titreFenetre = ChoisirTitreFenetre(definition, processusCible, titresFenetres);
        string cheminExecutable =
            correspondAuNomProcessus || correspondAuxMetadonneesExecutable
                ? LireCheminExecutableProcessus(processusCible)
                : string.Empty;

        if (
            string.IsNullOrWhiteSpace(cheminExecutable)
            && (correspondAuNomProcessus || correspondAuxMetadonneesExecutable)
        )
        {
            cheminExecutable = ServiceSourcesLocalesEmulateurs.TrouverEmplacementEmulateur(
                definition.NomEmulateur
            );
        }

        string titreJeuProbable = ExtraireTitreJeuPourDefinition(
            definition,
            processusCible,
            titreFenetre
        );
        string cheminJeuProbable = ExtraireCheminJeuPourDefinition(
            definition,
            processusCible,
            titreJeuProbable
        );
        int identifiantJeuProbable = 0;
        string informationsDiagnostic = string.Empty;

        if (
            definition.StrategieRenseignementJeu
            == StrategieRenseignementJeuEmulateurLocal.BizHawkConfig
        )
        {
            RenseignementJeuRA? renseignementJeu = LireRenseignementJeuBizHawkDepuisConfiguration();

            if (renseignementJeu is not null)
            {
                identifiantJeuProbable = renseignementJeu.IdentifiantJeu;

                if (!string.IsNullOrWhiteSpace(renseignementJeu.TitreJeu))
                {
                    titreJeuProbable = renseignementJeu.TitreJeu;
                }

                informationsDiagnostic = "source=bizhawk_json";
            }
        }
        else if (
            definition.StrategieRenseignementJeu
            == StrategieRenseignementJeuEmulateurLocal.DolphinConfig
        )
        {
            RenseignementJeuRA? renseignementJeu = LireRenseignementJeuDolphinDepuisLog();

            if (renseignementJeu is not null)
            {
                identifiantJeuProbable = renseignementJeu.IdentifiantJeu;

                if (!string.IsNullOrWhiteSpace(renseignementJeu.TitreJeu))
                {
                    titreJeuProbable = renseignementJeu.TitreJeu;
                }

                informationsDiagnostic =
                    $"source=dolphin_log;gameId={identifiantJeuProbable.ToString(CultureInfo.InvariantCulture)}";
            }
            else if (!string.IsNullOrWhiteSpace(titreJeuProbable))
            {
                informationsDiagnostic = "source=dolphin_process;gameId=0";
            }
        }
        else if (
            definition.StrategieRenseignementJeu
            == StrategieRenseignementJeuEmulateurLocal.Project64RACache
        )
        {
            RenseignementJeuRA? renseignementJeu = LireRenseignementJeuProject64DepuisRACache(
                definition.NomEmulateur
            );

            if (renseignementJeu is not null)
            {
                identifiantJeuProbable = renseignementJeu.IdentifiantJeu;

                if (!string.IsNullOrWhiteSpace(renseignementJeu.TitreJeu))
                {
                    titreJeuProbable = renseignementJeu.TitreJeu;
                }

                informationsDiagnostic = ConstruireDiagnosticSourceJeu(
                    definition.StrategieRenseignementJeu,
                    identifiantJeuProbable
                );
            }
        }
        else if (
            definition.StrategieRenseignementJeu
            == StrategieRenseignementJeuEmulateurLocal.RALibretroRACache
        )
        {
            RenseignementJeuRA? renseignementJeu = LireRenseignementJeuRALibretroDepuisRACache();

            if (renseignementJeu is not null)
            {
                identifiantJeuProbable = renseignementJeu.IdentifiantJeu;

                if (!string.IsNullOrWhiteSpace(renseignementJeu.TitreJeu))
                {
                    titreJeuProbable = renseignementJeu.TitreJeu;
                }

                informationsDiagnostic = ConstruireDiagnosticSourceJeu(
                    definition.StrategieRenseignementJeu,
                    identifiantJeuProbable
                );
            }

            if (identifiantJeuProbable <= 0)
            {
                titreJeuProbable = string.Empty;
            }
        }
        else if (
            definition.StrategieRenseignementJeu
            == StrategieRenseignementJeuEmulateurLocal.RANesRACache
        )
        {
            RenseignementJeuRA? renseignementJeu = LireRenseignementJeuRANesDepuisRACache();

            if (renseignementJeu is not null)
            {
                identifiantJeuProbable = renseignementJeu.IdentifiantJeu;

                if (!string.IsNullOrWhiteSpace(renseignementJeu.TitreJeu))
                {
                    titreJeuProbable = renseignementJeu.TitreJeu;
                }

                informationsDiagnostic = ConstruireDiagnosticSourceJeu(
                    definition.StrategieRenseignementJeu,
                    identifiantJeuProbable
                );
            }
        }
        else if (
            definition.StrategieRenseignementJeu
            == StrategieRenseignementJeuEmulateurLocal.RAVBARACache
        )
        {
            RenseignementJeuRA? renseignementJeu = LireRenseignementJeuRAVBADepuisRACache();

            if (renseignementJeu is not null)
            {
                identifiantJeuProbable = renseignementJeu.IdentifiantJeu;

                if (!string.IsNullOrWhiteSpace(renseignementJeu.TitreJeu))
                {
                    titreJeuProbable = renseignementJeu.TitreJeu;
                }

                informationsDiagnostic = ConstruireDiagnosticSourceJeu(
                    definition.StrategieRenseignementJeu,
                    identifiantJeuProbable
                );
            }
        }
        else if (
            definition.StrategieRenseignementJeu
            == StrategieRenseignementJeuEmulateurLocal.RASnes9xRACache
        )
        {
            RenseignementJeuRA? renseignementJeu = LireRenseignementJeuRASnes9xDepuisRACache();

            if (renseignementJeu is not null)
            {
                identifiantJeuProbable = renseignementJeu.IdentifiantJeu;

                if (!string.IsNullOrWhiteSpace(renseignementJeu.TitreJeu))
                {
                    titreJeuProbable = renseignementJeu.TitreJeu;
                }

                informationsDiagnostic = ConstruireDiagnosticSourceJeu(
                    definition.StrategieRenseignementJeu,
                    identifiantJeuProbable
                );
            }
        }
        else if (
            definition.StrategieRenseignementJeu
            == StrategieRenseignementJeuEmulateurLocal.FlycastConfig
        )
        {
            RenseignementJeuRA? renseignementJeu = LireRenseignementJeuFlycastDepuisLog(
                processusCible,
                titreJeuProbable
            );

            if (renseignementJeu is not null)
            {
                identifiantJeuProbable = renseignementJeu.IdentifiantJeu;

                if (!string.IsNullOrWhiteSpace(renseignementJeu.TitreJeu))
                {
                    titreJeuProbable = renseignementJeu.TitreJeu;
                }

                informationsDiagnostic = ConstruireDiagnosticSourceJeu(
                    definition.StrategieRenseignementJeu,
                    identifiantJeuProbable
                );
            }
        }
        else if (
            definition.StrategieRenseignementJeu
            == StrategieRenseignementJeuEmulateurLocal.RetroArchLog
        )
        {
            RenseignementJeuRA? renseignementJeu = LireRenseignementJeuRetroArchDepuisLog();

            if (renseignementJeu is not null)
            {
                identifiantJeuProbable = renseignementJeu.IdentifiantJeu;

                if (!string.IsNullOrWhiteSpace(renseignementJeu.TitreJeu))
                {
                    titreJeuProbable = renseignementJeu.TitreJeu;
                }

                informationsDiagnostic = ConstruireDiagnosticSourceJeu(
                    definition.StrategieRenseignementJeu,
                    identifiantJeuProbable
                );
            }
        }
        else if (
            definition.StrategieRenseignementJeu
            == StrategieRenseignementJeuEmulateurLocal.DuckStationLog
        )
        {
            RenseignementJeuRA? renseignementJeu = LireRenseignementJeuDuckStationDepuisLog();

            if (renseignementJeu is not null)
            {
                identifiantJeuProbable = renseignementJeu.IdentifiantJeu;

                if (!string.IsNullOrWhiteSpace(renseignementJeu.TitreJeu))
                {
                    titreJeuProbable = renseignementJeu.TitreJeu;
                }

                informationsDiagnostic = ConstruireDiagnosticSourceJeu(
                    definition.StrategieRenseignementJeu,
                    identifiantJeuProbable
                );
            }
        }
        else if (
            definition.StrategieRenseignementJeu == StrategieRenseignementJeuEmulateurLocal.PCSX2Log
        )
        {
            RenseignementJeuRA? renseignementJeu = LireRenseignementJeuPCSX2DepuisLog();

            if (renseignementJeu is not null)
            {
                identifiantJeuProbable = renseignementJeu.IdentifiantJeu;

                if (!string.IsNullOrWhiteSpace(renseignementJeu.TitreJeu))
                {
                    titreJeuProbable = renseignementJeu.TitreJeu;
                }

                informationsDiagnostic = ConstruireDiagnosticSourceJeu(
                    definition.StrategieRenseignementJeu,
                    identifiantJeuProbable
                );
            }
        }
        else if (
            definition.StrategieRenseignementJeu
            == StrategieRenseignementJeuEmulateurLocal.PPSSPPLog
        )
        {
            RenseignementJeuRA? renseignementJeu = LireRenseignementJeuPPSSPPDepuisLog(
                titreJeuProbable
            );

            renseignementJeu ??= LireRenseignementJeuPPSSPPDepuisCache(titreJeuProbable);

            if (renseignementJeu is not null)
            {
                identifiantJeuProbable = renseignementJeu.IdentifiantJeu;

                if (!string.IsNullOrWhiteSpace(renseignementJeu.TitreJeu))
                {
                    titreJeuProbable = renseignementJeu.TitreJeu;
                }

                informationsDiagnostic = ConstruireDiagnosticSourceJeu(
                    definition.StrategieRenseignementJeu,
                    identifiantJeuProbable
                );
            }
        }
        else if (
            definition.StrategieRenseignementJeu
            == StrategieRenseignementJeuEmulateurLocal.SkyEmuRecentGames
        )
        {
            EtatHttpSkyEmu? etatHttpSkyEmu = LireEtatHttpSkyEmu(processusCible);

            if (etatHttpSkyEmu is not null && !string.IsNullOrWhiteSpace(etatHttpSkyEmu.CheminRom))
            {
                cheminJeuProbable = etatHttpSkyEmu.CheminRom;
                titreJeuProbable = NettoyerNomFichierJeu(
                    Path.GetFileNameWithoutExtension(etatHttpSkyEmu.CheminRom)
                );
                informationsDiagnostic =
                    $"source=skyemu_http_status;port={etatHttpSkyEmu.Port.ToString(CultureInfo.InvariantCulture)};gameId=0";
            }
            else if (
                !string.IsNullOrWhiteSpace(titreJeuProbable)
                || !string.IsNullOrWhiteSpace(cheminJeuProbable)
            )
            {
                informationsDiagnostic = ConstruireDiagnosticSourceJeu(
                    definition.StrategieRenseignementJeu,
                    identifiantJeuProbable
                );
            }
        }

        if (
            definition.StrategieRenseignementJeu
                == StrategieRenseignementJeuEmulateurLocal.DuckStationLog
            && string.IsNullOrWhiteSpace(titreJeuProbable)
        )
        {
            informationsDiagnostic = ConstruireDiagnosticDuckStation(
                processusCible,
                titresFenetres
            );
        }

        string cheminJeuRecalcule = ExtraireCheminJeuPourDefinition(
            definition,
            processusCible,
            titreJeuProbable
        );

        if (!string.IsNullOrWhiteSpace(cheminJeuRecalcule))
        {
            cheminJeuProbable = cheminJeuRecalcule;
        }

        string signature =
            $"{definition.NomEmulateur}|{processusCible.ProcessName}|{titreFenetre}|{titreJeuProbable}|{cheminJeuProbable}|{identifiantJeuProbable.ToString(CultureInfo.InvariantCulture)}|{informationsDiagnostic}";

        return new EtatSondeLocaleEmulateur
        {
            EmulateurDetecte = true,
            NomEmulateur = definition.NomEmulateur,
            NomProcessus = processusCible.ProcessName,
            CheminExecutable = cheminExecutable,
            TitreFenetre = titreFenetre,
            TitreJeuProbable = titreJeuProbable,
            CheminJeuProbable = cheminJeuProbable,
            IdentifiantJeuProbable = identifiantJeuProbable,
            InformationsDiagnostic = informationsDiagnostic,
            Signature = signature,
            HorodatageUtc = DateTimeOffset.UtcNow,
        };
    }

    /*
     * Extrait le chemin du jeu à partir de la stratégie déclarée pour
     * l'émulateur courant.
     */
    private static string ExtraireCheminJeuPourDefinition(
        DefinitionEmulateurLocal definition,
        Process processus,
        string titreJeuProbable = ""
    )
    {
        string cheminDepuisCommande = NormaliserCheminJeuProbable(
            ExtraireCheminJeuDepuisLigneCommande(LireLigneCommandeProcessus(processus))
        );

        if (!string.IsNullOrWhiteSpace(cheminDepuisCommande))
        {
            string cheminExecutable = LireCheminExecutableProcessus(processus);

            if (
                !string.IsNullOrWhiteSpace(cheminExecutable)
                && string.Equals(
                    cheminDepuisCommande,
                    cheminExecutable,
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                cheminDepuisCommande = string.Empty;
            }
        }

        if (!string.IsNullOrWhiteSpace(cheminDepuisCommande))
        {
            return cheminDepuisCommande;
        }

        return definition.StrategieRenseignementJeu switch
        {
            StrategieRenseignementJeuEmulateurLocal.RetroArchLog =>
                LireCheminJeuRetroArchDepuisLog(),
            StrategieRenseignementJeuEmulateurLocal.BizHawkConfig =>
                LireCheminJeuBizHawkDepuisConfiguration(),
            StrategieRenseignementJeuEmulateurLocal.DolphinConfig => LireCheminJeuDolphin(
                titreJeuProbable
            ),
            StrategieRenseignementJeuEmulateurLocal.DuckStationLog =>
                LireCheminJeuDuckStationDepuisLog(),
            StrategieRenseignementJeuEmulateurLocal.PCSX2Log => LireCheminJeuPCSX2DepuisLog(),
            StrategieRenseignementJeuEmulateurLocal.PPSSPPLog => LireCheminJeuPPSSPPDepuisLog(),
            StrategieRenseignementJeuEmulateurLocal.SkyEmuRecentGames =>
                LireCheminJeuSkyEmuDepuisHttp(processus) is string cheminJeuSkyEmuHttp
                && !string.IsNullOrWhiteSpace(cheminJeuSkyEmuHttp)
                    ? cheminJeuSkyEmuHttp
                    : LireCheminJeuSkyEmuDepuisRecentGames(titreJeuProbable),
            StrategieRenseignementJeuEmulateurLocal.FlycastConfig =>
                LireCheminJeuFlycastDepuisLogOuConfiguration(titreJeuProbable),
            StrategieRenseignementJeuEmulateurLocal.Project64RACache =>
                LireCheminJeuProject64DepuisConfiguration(definition.NomEmulateur),
            StrategieRenseignementJeuEmulateurLocal.RALibretroRACache =>
                LireCheminJeuRALibretroDepuisConfiguration(),
            StrategieRenseignementJeuEmulateurLocal.RANesRACache =>
                LireCheminJeuRANesDepuisConfiguration(titreJeuProbable),
            StrategieRenseignementJeuEmulateurLocal.RAVBARACache =>
                LireCheminJeuRAVBADepuisConfiguration(),
            StrategieRenseignementJeuEmulateurLocal.RASnes9xRACache =>
                LireCheminJeuRASnes9xDepuisConfiguration(),
            _ => string.Empty,
        };
    }

    /*
     * Extrait le titre du jeu à partir de la stratégie déclarée pour
     * l'émulateur courant.
     */
    private static string ExtraireTitreJeuPourDefinition(
        DefinitionEmulateurLocal definition,
        Process processus,
        string titreFenetre
    )
    {
        return definition.StrategieExtractionTitre switch
        {
            StrategieExtractionTitreEmulateurLocal.SeparateursRetroArch =>
                ExtraireTitreAvecSeparateurs(titreFenetre, "RetroArch", "RetroArch "),
            StrategieExtractionTitreEmulateurLocal.RALibretro => ExtraireTitreRALibretro(
                processus,
                titreFenetre
            ),
            StrategieExtractionTitreEmulateurLocal.BizHawk => ExtraireTitreAvecSeparateurs(
                titreFenetre,
                "EmuHawk",
                "BizHawk"
            ),
            StrategieExtractionTitreEmulateurLocal.DuckStation => ExtraireTitreDuckStation(
                processus,
                titreFenetre
            ),
            StrategieExtractionTitreEmulateurLocal.PCSX2 => ExtraireTitrePCSX2(
                processus,
                titreFenetre
            ),
            StrategieExtractionTitreEmulateurLocal.PPSSPP => ExtraireTitrePPSSPP(
                processus,
                titreFenetre
            ),
            StrategieExtractionTitreEmulateurLocal.SkyEmu => ExtraireTitreSkyEmu(
                processus,
                titreFenetre
            ),
            StrategieExtractionTitreEmulateurLocal.RANes => ExtraireTitreAvecSeparateurs(
                titreFenetre,
                "RANes"
            ),
            StrategieExtractionTitreEmulateurLocal.RAVBA => ExtraireTitreAvecSeparateurs(
                titreFenetre,
                "RAVBA",
                "RAVBA-M",
                "RAVisualBoyAdvance",
                "RAVisualBoyAdvance-M"
            ),
            StrategieExtractionTitreEmulateurLocal.RASnes9x => ExtraireTitreAvecSeparateurs(
                titreFenetre,
                "RASnes9x"
            ),
            StrategieExtractionTitreEmulateurLocal.Dolphin => ExtraireTitreDolphin(
                processus,
                titreFenetre
            ),
            StrategieExtractionTitreEmulateurLocal.Project64 => ExtraireTitreProject64(
                processus,
                titreFenetre
            ),
            StrategieExtractionTitreEmulateurLocal.Flycast => ExtraireTitreAvecSeparateurs(
                titreFenetre,
                "Flycast"
            ),
            _ => string.Empty,
        };
    }

    /*
     * Vérifie si un processus correspond globalement à une définition d'émulateur.
     */
    private static bool Correspond(Process processus, DefinitionEmulateurLocal definition)
    {
        bool correspondAuNomProcessus = CorrespondNomProcessus(processus, definition);

        if (correspondAuNomProcessus)
        {
            return true;
        }

        bool correspondAuxMetadonneesExecutable = CorrespondMetadonneesExecutable(
            processus,
            definition
        );

        if (correspondAuxMetadonneesExecutable)
        {
            return true;
        }

        if (!definition.AutoriserDetectionParTitreFenetre)
        {
            return false;
        }

        string nomProcessus = processus.ProcessName?.Trim() ?? string.Empty;

        if (
            string.Equals(nomProcessus, "explorer", StringComparison.OrdinalIgnoreCase)
            || string.Equals(nomProcessus, "openwith", StringComparison.OrdinalIgnoreCase)
            || string.Equals(nomProcessus, "dllhost", StringComparison.OrdinalIgnoreCase)
        )
        {
            return false;
        }

        string titreFenetre = LireTitreFenetre(processus);

        return !string.IsNullOrWhiteSpace(titreFenetre)
            && titreFenetre.StartsWith(definition.NomEmulateur, StringComparison.OrdinalIgnoreCase);
    }

    /*
     * Vérifie si le nom d'un processus correspond aux noms attendus.
     */
    private static bool CorrespondNomProcessus(
        Process processus,
        DefinitionEmulateurLocal definition
    )
    {
        string nomProcessus = processus.ProcessName?.Trim() ?? string.Empty;

        if (
            string.Equals(definition.NomEmulateur, "DuckStation", StringComparison.Ordinal)
            && nomProcessus.Contains("duckstation", StringComparison.OrdinalIgnoreCase)
        )
        {
            return true;
        }

        return definition.NomsProcessus.Any(nom =>
            string.Equals(nomProcessus, nom, StringComparison.OrdinalIgnoreCase)
            || nomProcessus.StartsWith(nom, StringComparison.OrdinalIgnoreCase)
        );
    }

    /*
     * Vérifie si les métadonnées ou le chemin binaire correspondent à
     * l'empreinte attendue d'un émulateur.
     */
    private static bool CorrespondMetadonneesExecutable(
        Process processus,
        DefinitionEmulateurLocal definition
    )
    {
        try
        {
            string cheminExecutable = processus.MainModule?.FileName?.Trim() ?? string.Empty;

            if (
                ServiceSourcesLocalesEmulateurs.CorrespondAuCheminEmulateurManuel(
                    definition.NomEmulateur,
                    cheminExecutable
                )
            )
            {
                return true;
            }

            FileVersionInfo? version = processus.MainModule?.FileVersionInfo;

            string[] valeurs =
            [
                cheminExecutable,
                version?.ProductName ?? string.Empty,
                version?.FileDescription ?? string.Empty,
                version?.OriginalFilename ?? string.Empty,
                version?.InternalName ?? string.Empty,
            ];

            string[] jetons = ObtenirJetonsCorrespondanceEmulateur(definition);

            return valeurs.Any(valeur => CorrespondValeurEmpreinteEmulateur(valeur, jetons));
        }
        catch
        {
            return false;
        }
    }

    /*
     * Vérifie qu'une valeur contient un jeton compatible avec l'émulateur.
     */
    private static bool CorrespondValeurEmpreinteEmulateur(
        string valeur,
        IReadOnlyList<string> jetons
    )
    {
        string valeurNormalisee = NormaliserEmpreinteExecutable(valeur);

        if (string.IsNullOrWhiteSpace(valeurNormalisee))
        {
            return false;
        }

        return jetons.Any(jeton =>
        {
            string jetonNormalise = NormaliserEmpreinteExecutable(jeton);
            return !string.IsNullOrWhiteSpace(jetonNormalise)
                && valeurNormalisee.Contains(jetonNormalise, StringComparison.Ordinal);
        });
    }

    /*
     * Lit le chemin exécutable d'un processus lorsqu'il est accessible.
     */
    private static string LireCheminExecutableProcessus(Process processus)
    {
        try
        {
            return processus.MainModule?.FileName?.Trim() ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    /*
     * Construit les jetons de correspondance utiles pour reconnaître
     * un émulateur dans les noms de fichiers et processus.
     */
    private static string[] ObtenirJetonsCorrespondanceEmulateur(
        DefinitionEmulateurLocal definition
    )
    {
        List<string> jetons = [definition.NomEmulateur, .. definition.NomsProcessus];

        if (string.Equals(definition.NomEmulateur, "BizHawk", StringComparison.Ordinal))
        {
            jetons.Add("EmuHawk");
            jetons.Add("BizHawk");
        }
        else if (string.Equals(definition.NomEmulateur, "RAVBA", StringComparison.Ordinal))
        {
            jetons.Add("VisualBoyAdvance");
            jetons.Add("VisualBoyAdvance-M");
        }
        else if (string.Equals(definition.NomEmulateur, "RASnes9x", StringComparison.Ordinal))
        {
            jetons.Add("Snes9x");
        }
        else if (string.Equals(definition.NomEmulateur, "RAP64", StringComparison.Ordinal))
        {
            jetons.Add("Project64");
            jetons.Add("RAProject64");
            jetons.Add("RA Project64");
            jetons.Add("RAP64");
        }

        return [.. jetons.Where(jeton => !string.IsNullOrWhiteSpace(jeton)).Distinct()];
    }

    /*
     * Normalise une valeur textuelle pour les comparaisons souples d'empreinte.
     */
    private static string NormaliserEmpreinteExecutable(string valeur)
    {
        if (string.IsNullOrWhiteSpace(valeur))
        {
            return string.Empty;
        }

        StringBuilder builder = new(valeur.Length);

        foreach (char caractere in valeur)
        {
            if (char.IsLetterOrDigit(caractere))
            {
                builder.Append(char.ToLowerInvariant(caractere));
            }
        }

        return builder.ToString();
    }

    /*
     * Indique si un processus possède au moins une fenêtre visible.
     */
    private static int ProcessusPossedeUneFenetreVisible(Process processus)
    {
        try
        {
            return LireTitresFenetresVisibles(processus).Count;
        }
        catch
        {
            return 0;
        }
    }

    /*
     * Lit le meilleur titre de fenêtre disponible pour un processus.
     */
    private static string LireTitreFenetre(Process processus)
    {
        try
        {
            return ChoisirTitreFenetre(
                new DefinitionEmulateurLocal(
                    string.Empty,
                    [],
                    [],
                    StrategieExtractionTitreEmulateurLocal.Flycast,
                    StrategieRenseignementJeuEmulateurLocal.Aucune,
                    StrategieSurveillanceSuccesLocale.Aucune,
                    true,
                    false,
                    []
                ),
                processus,
                LireTitresFenetresVisibles(processus)
            );
        }
        catch
        {
            return string.Empty;
        }
    }

    /*
     * Extrait un titre probable à partir d'un séparateur présent dans la fenêtre.
     */
    private static string ExtraireTitreAvecSeparateurs(
        string titreFenetre,
        params string[] jetonsEmulateur
    )
    {
        string titre = titreFenetre.Trim();

        if (string.IsNullOrWhiteSpace(titre))
        {
            return string.Empty;
        }

        string[] separateurs = [" - ", " | ", " \u2014 ", " \u2013 ", " :: ", " / "];

        foreach (string separateur in separateurs)
        {
            string[] morceaux = titre.Split(separateur, StringSplitOptions.RemoveEmptyEntries);

            if (morceaux.Length < 2)
            {
                continue;
            }

            string premier = morceaux[0].Trim();
            string dernier = morceaux[^1].Trim();

            if (ContientJetonEmulateur(dernier, jetonsEmulateur))
            {
                return NettoyerTitreJeu(premier, jetonsEmulateur);
            }

            if (ContientJetonEmulateur(premier, jetonsEmulateur))
            {
                return NettoyerTitreJeu(dernier, jetonsEmulateur);
            }
        }

        if (ContientJetonEmulateur(titre, jetonsEmulateur))
        {
            return string.Empty;
        }

        return NettoyerTitreJeu(titre, jetonsEmulateur);
    }

    /*
     * Indique si une valeur contient encore un jeton caractéristique
     * de l'émulateur plutôt qu'un titre de jeu.
     */
    private static bool ContientJetonEmulateur(string valeur, IEnumerable<string> jetonsEmulateur)
    {
        return jetonsEmulateur.Any(jeton =>
            valeur.Contains(jeton, StringComparison.OrdinalIgnoreCase)
        );
    }

    /*
     * Nettoie un titre de jeu en retirant les mentions parasites liées
     * à l'émulateur.
     */
    private static string NettoyerTitreJeu(string titre, IEnumerable<string> jetonsEmulateur)
    {
        string resultat = titre.Trim();

        foreach (string jeton in jetonsEmulateur)
        {
            resultat = Regex.Replace(
                resultat,
                $@"\b{Regex.Escape(jeton)}\b",
                string.Empty,
                RegexOptions.IgnoreCase
            );
        }

        resultat = EspacesMultiplesRegex().Replace(resultat, " ").Trim();
        return resultat;
    }

    /*
     * Extrait un titre probable de DuckStation à partir du processus et
     * de son titre de fenêtre.
     */
    private static string ExtraireTitreDuckStation(Process processus, string titreFenetre)
    {
        string titreFenetreExtrait = ExtraireTitreAvecSeparateurs(
            titreFenetre,
            "DuckStation",
            "DuckStation "
        );

        if (!string.IsNullOrWhiteSpace(titreFenetreExtrait))
        {
            return titreFenetreExtrait;
        }

        string titreAutomatisation = ExtraireTitreDuckStationDepuisAutomatisation(processus);

        if (!string.IsNullOrWhiteSpace(titreAutomatisation))
        {
            return titreAutomatisation;
        }

        string ligneCommande = LireLigneCommandeProcessus(processus);
        string cheminJeu = ExtraireCheminJeuDepuisLigneCommande(ligneCommande);

        if (!string.IsNullOrWhiteSpace(cheminJeu))
        {
            return NettoyerNomFichierJeu(Path.GetFileNameWithoutExtension(cheminJeu));
        }

        string titreDepuisMemcard = ExtraireTitreDuckStationDepuisMemcardRecente();

        if (!string.IsNullOrWhiteSpace(titreDepuisMemcard))
        {
            return titreDepuisMemcard;
        }

        return string.Empty;
    }

    /*
     * Extrait un titre probable de PCSX2 à partir de son titre de fenêtre.
     */
    private static string ExtraireTitrePCSX2(Process _, string titreFenetre)
    {
        string titre = ExtraireTitreAvecSeparateurs(titreFenetre, "PCSX2");

        if (string.IsNullOrWhiteSpace(titre))
        {
            return string.Empty;
        }

        string titreNettoye = titre.Trim();

        if (EstDialoguePCSX2(titreNettoye))
        {
            return string.Empty;
        }

        return titreNettoye;
    }

    /*
     * Extrait un titre probable de PPSSPP à partir de son titre de fenêtre.
     */
    private static string ExtraireTitrePPSSPP(Process _, string titreFenetre)
    {
        string titre = ExtraireTitreAvecSeparateurs(titreFenetre, "PPSSPP");

        if (string.IsNullOrWhiteSpace(titre))
        {
            return string.Empty;
        }

        string titreNettoye = titre.Trim();

        titreNettoye = PrefixeSerialPpssppRegex().Replace(titreNettoye, string.Empty).Trim();

        titreNettoye = titreNettoye.Replace("\u00AE", string.Empty, StringComparison.Ordinal);
        titreNettoye = EspacesMultiplesRegex().Replace(titreNettoye, " ").Trim();

        return titreNettoye;
    }

    /*
     * Extrait un titre probable de Dolphin depuis la fenêtre ou la ligne
     * de commande du processus.
     */
    private static string ExtraireTitreDolphin(Process processus, string titreFenetre)
    {
        string titre = ExtraireTitreAvecSeparateurs(
            titreFenetre,
            "Dolphin",
            "Dolphin Emulator",
            "Slippi Dolphin",
            "Slippi Dolphin Launcher"
        );

        if (string.IsNullOrWhiteSpace(titre))
        {
            return string.Empty;
        }

        string titreNettoye = titre.Trim();

        if (EstDialogueDolphin(titreNettoye))
        {
            return ExtraireTitreDolphinDepuisCommande(processus);
        }

        titreNettoye = SuffixeCodeJeuDolphinRegex().Replace(titreNettoye, string.Empty).Trim();

        return string.IsNullOrWhiteSpace(titreNettoye)
            ? ExtraireTitreDolphinDepuisCommande(processus)
            : titreNettoye;
    }

    /*
     * Extrait un titre probable de Dolphin à partir de la ligne de commande.
     */
    private static string ExtraireTitreDolphinDepuisCommande(Process processus)
    {
        string cheminJeu = NormaliserCheminJeuProbable(
            ExtraireCheminJeuDepuisLigneCommande(LireLigneCommandeProcessus(processus))
        );

        return string.IsNullOrWhiteSpace(cheminJeu)
            ? string.Empty
            : NettoyerNomFichierJeu(Path.GetFileNameWithoutExtension(cheminJeu));
    }

    /*
     * Extrait un titre probable de SkyEmu à partir de la ligne de commande
     * ou, à défaut, du titre de fenêtre.
     */
    private static string ExtraireTitreSkyEmu(Process processus, string titreFenetre)
    {
        string cheminJeu = NormaliserCheminJeuProbable(
            ExtraireCheminJeuDepuisLigneCommande(LireLigneCommandeProcessus(processus))
        );

        if (!string.IsNullOrWhiteSpace(cheminJeu))
        {
            return NettoyerNomFichierJeu(Path.GetFileNameWithoutExtension(cheminJeu));
        }

        return ExtraireTitreAvecSeparateurs(titreFenetre, "SkyEmu", "Sky Emu");
    }

    /*
     * Extrait un titre probable de Project64 à partir de son titre de fenêtre.
     */
    private static string ExtraireTitreProject64(Process _, string titreFenetre)
    {
        return ExtraireTitreProject64DepuisFenetre(titreFenetre);
    }

    /*
     * Extrait un titre probable de RALibretro à partir de son titre de fenêtre.
     */
    private static string ExtraireTitreRALibretro(Process _, string titreFenetre)
    {
        string titre = ExtraireTitreRALibretroDepuisFenetre(titreFenetre);

        if (!string.IsNullOrWhiteSpace(titre))
        {
            return titre;
        }

        RenseignementJeuRA? renseignementJeu = LireRenseignementJeuRALibretroDepuisRACache();

        if (renseignementJeu is not null && !string.IsNullOrWhiteSpace(renseignementJeu.TitreJeu))
        {
            return renseignementJeu.TitreJeu;
        }

        string cheminConfiguration =
            ServiceSourcesLocalesEmulateurs.TrouverCheminConfigurationRALibretro();

        if (string.IsNullOrWhiteSpace(cheminConfiguration) || !File.Exists(cheminConfiguration))
        {
            return string.Empty;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(
                File.ReadAllText(cheminConfiguration, Encoding.UTF8)
            );

            if (
                document.RootElement.TryGetProperty("recent", out JsonElement recent)
                && recent.ValueKind == JsonValueKind.Array
                && recent.GetArrayLength() > 0
                && recent[0].TryGetProperty("path", out JsonElement path)
                && path.ValueKind == JsonValueKind.String
            )
            {
                string cheminJeu = path.GetString()?.Trim() ?? string.Empty;

                if (!string.IsNullOrWhiteSpace(cheminJeu))
                {
                    return NettoyerNomFichierJeu(Path.GetFileNameWithoutExtension(cheminJeu));
                }
            }
        }
        catch { }

        return string.Empty;
    }

    /*
     * Lit le chemin du jeu courant de RALibretro depuis sa configuration.
     */
    private static string LireCheminJeuRALibretroDepuisConfiguration()
    {
        string cheminConfiguration =
            ServiceSourcesLocalesEmulateurs.TrouverCheminConfigurationRALibretro();

        if (string.IsNullOrWhiteSpace(cheminConfiguration) || !File.Exists(cheminConfiguration))
        {
            return string.Empty;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(
                File.ReadAllText(cheminConfiguration, Encoding.UTF8)
            );

            if (
                document.RootElement.TryGetProperty("recent", out JsonElement recent)
                && recent.ValueKind == JsonValueKind.Array
                && recent.GetArrayLength() > 0
                && recent[0].TryGetProperty("path", out JsonElement path)
                && path.ValueKind == JsonValueKind.String
            )
            {
                return NormaliserCheminJeuProbable(path.GetString()?.Trim() ?? string.Empty);
            }
        }
        catch { }

        return string.Empty;
    }

    /*
     * Lit le chemin du jeu courant de Project64 ou RAP64 depuis sa configuration.
     */
    private static string LireCheminJeuProject64DepuisConfiguration(string nomEmulateur)
    {
        string cheminConfiguration =
            ServiceSourcesLocalesEmulateurs.TrouverCheminConfigurationProject64(nomEmulateur);

        if (string.IsNullOrWhiteSpace(cheminConfiguration) || !File.Exists(cheminConfiguration))
        {
            return string.Empty;
        }

        try
        {
            foreach (
                string ligne in ServiceSourcesLocalesEmulateurs.LireToutesLesLignesAvecPartage(
                    cheminConfiguration
                )
            )
            {
                const string prefixe = "Recent Rom 0=";

                if (!ligne.StartsWith(prefixe, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string cheminNormalise = NormaliserCheminJeuProbable(
                    ligne[prefixe.Length..].Trim()
                );

                if (!string.IsNullOrWhiteSpace(cheminNormalise))
                {
                    return cheminNormalise;
                }
            }
        }
        catch { }

        return string.Empty;
    }

    /*
     * Lit le chemin du jeu courant de RANes depuis sa configuration.
     */
    private static string LireCheminJeuRANesDepuisConfiguration(string titreJeuProbable)
    {
        string cheminConfiguration =
            ServiceSourcesLocalesEmulateurs.TrouverCheminConfigurationRANes();

        if (string.IsNullOrWhiteSpace(cheminConfiguration) || !File.Exists(cheminConfiguration))
        {
            return string.Empty;
        }

        try
        {
            List<string> cheminsCandidats = [];

            foreach (
                string ligne in ServiceSourcesLocalesEmulateurs.LireToutesLesLignesAvecPartage(
                    cheminConfiguration
                )
            )
            {
                if (ligne.StartsWith("ResumeROM ", StringComparison.OrdinalIgnoreCase))
                {
                    string cheminResume = NormaliserCheminJeuProbable(
                        ligne["ResumeROM ".Length..].Trim()
                    );

                    if (!string.IsNullOrWhiteSpace(cheminResume))
                    {
                        cheminsCandidats.Add(cheminResume);
                    }
                }

                if (!ligne.StartsWith("recent_files[", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                int indexSeparateur = ligne.IndexOf(' ');

                if (indexSeparateur < 0 || indexSeparateur == ligne.Length - 1)
                {
                    continue;
                }

                string cheminNormalise = NormaliserCheminJeuProbable(
                    ligne[(indexSeparateur + 1)..].Trim()
                );

                if (!string.IsNullOrWhiteSpace(cheminNormalise))
                {
                    cheminsCandidats.Add(cheminNormalise);
                }
            }

            cheminsCandidats = [.. cheminsCandidats.Distinct(StringComparer.OrdinalIgnoreCase)];

            if (cheminsCandidats.Count == 0)
            {
                return string.Empty;
            }

            if (string.IsNullOrWhiteSpace(titreJeuProbable))
            {
                return cheminsCandidats[0];
            }

            string meilleurChemin = string.Empty;
            int meilleurScore = int.MinValue;

            foreach (string cheminCandidat in cheminsCandidats)
            {
                string nomFichier = NettoyerNomFichierJeu(
                    Path.GetFileNameWithoutExtension(cheminCandidat)
                );
                string nomDossier = NettoyerNomFichierJeu(
                    Path.GetFileName(Path.GetDirectoryName(cheminCandidat) ?? string.Empty)
                );

                int score = 0;

                if (TitresSemblables(nomFichier, titreJeuProbable))
                {
                    score += 120;
                }
                else
                {
                    score +=
                        CalculerScoreProximiteTitreSouple(
                            NormaliserTitreComparaisonSouple(titreJeuProbable),
                            nomFichier
                        ) * 10;
                }

                if (TitresSemblables(nomDossier, titreJeuProbable))
                {
                    score += 80;
                }
                else
                {
                    score +=
                        CalculerScoreProximiteTitreSouple(
                            NormaliserTitreComparaisonSouple(titreJeuProbable),
                            nomDossier
                        ) * 6;
                }

                if (score <= meilleurScore)
                {
                    continue;
                }

                meilleurScore = score;
                meilleurChemin = cheminCandidat;
            }

            return !string.IsNullOrWhiteSpace(meilleurChemin)
                ? meilleurChemin
                : cheminsCandidats[0];
        }
        catch { }

        return string.Empty;
    }

    /*
     * Lit le chemin du jeu courant de RASnes9x depuis sa configuration.
     */
    private static string LireCheminJeuRASnes9xDepuisConfiguration()
    {
        string cheminConfiguration =
            ServiceSourcesLocalesEmulateurs.TrouverCheminConfigurationRASnes9x();

        if (string.IsNullOrWhiteSpace(cheminConfiguration) || !File.Exists(cheminConfiguration))
        {
            return string.Empty;
        }

        try
        {
            foreach (
                string ligne in ServiceSourcesLocalesEmulateurs.LireToutesLesLignesAvecPartage(
                    cheminConfiguration
                )
            )
            {
                const string prefixe = "Rom:RecentGame1";

                if (!ligne.StartsWith(prefixe, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                int indexSeparateur = ligne.IndexOf('=');

                if (indexSeparateur < 0 || indexSeparateur == ligne.Length - 1)
                {
                    continue;
                }

                string cheminNormalise = NormaliserCheminJeuProbable(
                    ligne[(indexSeparateur + 1)..].Trim()
                );

                if (!string.IsNullOrWhiteSpace(cheminNormalise))
                {
                    return cheminNormalise;
                }
            }
        }
        catch { }

        return string.Empty;
    }

    /*
     * Lit le chemin du jeu courant de RAVBA depuis sa configuration.
     */
    private static string LireCheminJeuRAVBADepuisConfiguration()
    {
        string cheminConfiguration =
            ServiceSourcesLocalesEmulateurs.TrouverCheminConfigurationRAVBA();

        if (string.IsNullOrWhiteSpace(cheminConfiguration) || !File.Exists(cheminConfiguration))
        {
            return string.Empty;
        }

        try
        {
            foreach (
                string ligne in ServiceSourcesLocalesEmulateurs.LireToutesLesLignesAvecPartage(
                    cheminConfiguration
                )
            )
            {
                if (!ligne.StartsWith("file1=", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string cheminNormalise = NormaliserCheminJeuProbable(
                    ligne["file1=".Length..].Trim()
                );

                if (!string.IsNullOrWhiteSpace(cheminNormalise))
                {
                    return cheminNormalise;
                }
            }
        }
        catch { }

        return string.Empty;
    }

    /*
     * Lit le chemin du jeu courant de PCSX2 depuis son journal local.
     */
    private static string LireCheminJeuPCSX2DepuisLog()
    {
        try
        {
            string cheminJournal = ServiceSourcesLocalesEmulateurs.TrouverCheminJournalPCSX2();

            if (string.IsNullOrWhiteSpace(cheminJournal) || !File.Exists(cheminJournal))
            {
                return string.Empty;
            }

            foreach (
                string ligne in ServiceSourcesLocalesEmulateurs
                    .LireToutesLesLignesAvecPartage(cheminJournal)
                    .AsEnumerable()
                    .Reverse()
            )
            {
                Match correspondanceIso = PCSX2IsoOuverteRegex().Match(ligne);

                if (!correspondanceIso.Success)
                {
                    continue;
                }

                string cheminNormalise = NormaliserCheminJeuProbable(
                    correspondanceIso.Groups[1].Value.Trim()
                );

                if (!string.IsNullOrWhiteSpace(cheminNormalise))
                {
                    return cheminNormalise;
                }
            }
        }
        catch { }

        return string.Empty;
    }

    /*
     * Lit le chemin du jeu courant de Flycast depuis sa configuration.
     */
    private static string LireCheminJeuFlycastDepuisConfiguration(string titreJeuProbable)
    {
        try
        {
            string cheminConfiguration =
                ServiceSourcesLocalesEmulateurs.TrouverCheminConfigurationFlycast();

            if (string.IsNullOrWhiteSpace(cheminConfiguration) || !File.Exists(cheminConfiguration))
            {
                return string.Empty;
            }

            string repertoireContenu = string.Empty;

            foreach (
                string ligne in ServiceSourcesLocalesEmulateurs.LireToutesLesLignesAvecPartage(
                    cheminConfiguration
                )
            )
            {
                const string prefixe = "Dreamcast.ContentPath";

                if (!ligne.StartsWith(prefixe, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                int indexSeparateur = ligne.IndexOf('=');

                if (indexSeparateur < 0 || indexSeparateur == ligne.Length - 1)
                {
                    continue;
                }

                repertoireContenu = ligne[(indexSeparateur + 1)..].Trim();
                break;
            }

            if (
                string.IsNullOrWhiteSpace(repertoireContenu) || !Directory.Exists(repertoireContenu)
            )
            {
                return string.Empty;
            }

            return TrouverCheminJeuFlycastDepuisRepertoireContenu(
                repertoireContenu,
                titreJeuProbable
            );
        }
        catch
        {
            return string.Empty;
        }
    }

    /*
     * Tente d'obtenir le chemin du jeu Flycast depuis le log puis la configuration.
     */
    private static string LireCheminJeuFlycastDepuisLogOuConfiguration(string titreJeuProbable)
    {
        string cheminDepuisLog = LireCheminJeuFlycastDepuisLog();

        if (!string.IsNullOrWhiteSpace(cheminDepuisLog))
        {
            return cheminDepuisLog;
        }

        return LireCheminJeuFlycastDepuisConfiguration(titreJeuProbable);
    }

    /*
     * Tente d'associer un titre au meilleur fichier jeu trouvé dans le
     * répertoire de contenu Flycast.
     */
    private static string TrouverCheminJeuFlycastDepuisRepertoireContenu(
        string repertoireContenu,
        string titreJeuProbable
    )
    {
        if (
            string.IsNullOrWhiteSpace(repertoireContenu)
            || string.IsNullOrWhiteSpace(titreJeuProbable)
        )
        {
            return string.Empty;
        }

        string repertoireNormalise;

        try
        {
            repertoireNormalise = Path.GetFullPath(repertoireContenu.Trim());
        }
        catch
        {
            return string.Empty;
        }

        if (!Directory.Exists(repertoireNormalise))
        {
            return string.Empty;
        }

        string titreNormalise = NormaliserTitreComparaisonSouple(titreJeuProbable);

        if (string.IsNullOrWhiteSpace(titreNormalise))
        {
            return string.Empty;
        }

        List<string> fichiersJeu = ObtenirFichiersJeuFlycast(repertoireNormalise);
        string meilleurChemin = string.Empty;
        int meilleurScore = int.MinValue;

        foreach (string cheminFichier in fichiersJeu)
        {
            string nomFichier = NettoyerNomFichierJeu(
                Path.GetFileNameWithoutExtension(cheminFichier)
            );
            string nomDossier = NettoyerNomFichierJeu(
                Path.GetFileName(Path.GetDirectoryName(cheminFichier) ?? string.Empty)
            );

            int score = 0;

            if (TitresSemblables(nomFichier, titreJeuProbable))
            {
                score += 120;
            }
            else
            {
                score += CalculerScoreProximiteTitreSouple(titreNormalise, nomFichier) * 10;
            }

            if (TitresSemblables(nomDossier, titreJeuProbable))
            {
                score += 80;
            }
            else
            {
                score += CalculerScoreProximiteTitreSouple(titreNormalise, nomDossier) * 6;
            }

            if (score <= meilleurScore)
            {
                continue;
            }

            meilleurScore = score;
            meilleurChemin = cheminFichier;
        }

        return meilleurScore >= 30 ? meilleurChemin : string.Empty;
    }

    /*
     * Retourne la liste des fichiers jeu plausibles présents pour Flycast.
     */
    private static List<string> ObtenirFichiersJeuFlycast(string repertoireContenu)
    {
        lock (VerrouCacheFlycast)
        {
            if (
                string.Equals(
                    _dernierRepertoireContenuFlycast,
                    repertoireContenu,
                    StringComparison.OrdinalIgnoreCase
                )
                && DateTime.UtcNow - _dernierHorodatageCacheContenuFlycastUtc
                    < TimeSpan.FromMinutes(5)
                && _cacheFichiersJeuFlycast.Count > 0
            )
            {
                return [.. _cacheFichiersJeuFlycast];
            }

            string[] extensions = [".cue", ".cdi", ".gdi", ".chd"];

            _cacheFichiersJeuFlycast =
            [
                .. new DirectoryInfo(repertoireContenu)
                    .EnumerateFiles("*.*", SearchOption.AllDirectories)
                    .Where(fichier =>
                        extensions.Contains(fichier.Extension, StringComparer.OrdinalIgnoreCase)
                    )
                    .Select(fichier => fichier.FullName),
            ];
            _dernierRepertoireContenuFlycast = repertoireContenu;
            _dernierHorodatageCacheContenuFlycastUtc = DateTime.UtcNow;
            return [.. _cacheFichiersJeuFlycast];
        }
    }

    /*
     * Calcule un score souple de proximité entre deux titres potentiellement
     * équivalents.
     */
    private static int CalculerScoreProximiteTitreSouple(
        string titreReferenceNormalise,
        string titreCandidat
    )
    {
        string candidatNormalise = NormaliserTitreComparaisonSouple(titreCandidat);

        if (
            string.IsNullOrWhiteSpace(titreReferenceNormalise)
            || string.IsNullOrWhiteSpace(candidatNormalise)
        )
        {
            return 0;
        }

        HashSet<string> jetonsReference =
        [
            .. titreReferenceNormalise
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(jeton => jeton.Length >= 3),
        ];
        HashSet<string> jetonsCandidats =
        [
            .. candidatNormalise
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(jeton => jeton.Length >= 3),
        ];

        if (jetonsReference.Count == 0 || jetonsCandidats.Count == 0)
        {
            return 0;
        }

        int communs = jetonsReference.Intersect(jetonsCandidats, StringComparer.Ordinal).Count();

        if (communs <= 0)
        {
            return 0;
        }

        if (
            candidatNormalise.Contains(titreReferenceNormalise, StringComparison.Ordinal)
            || titreReferenceNormalise.Contains(candidatNormalise, StringComparison.Ordinal)
        )
        {
            communs += 3;
        }

        return communs;
    }

    /*
     * Extrait un titre probable de RALibretro depuis son titre de fenêtre.
     */
    private static string ExtraireTitreRALibretroDepuisFenetre(string titreFenetre)
    {
        string titre = titreFenetre.Trim();

        if (string.IsNullOrWhiteSpace(titre))
        {
            return string.Empty;
        }

        if (
            titre.StartsWith("RALibretro", StringComparison.OrdinalIgnoreCase)
            || titre.StartsWith("RALibRetro", StringComparison.OrdinalIgnoreCase)
        )
        {
            return string.Empty;
        }

        return NettoyerNomFichierJeu(titre);
    }

    /*
     * Indique si un titre de fenêtre correspond plutôt à un dialogue Dolphin.
     */
    private static bool EstDialogueDolphin(string titre)
    {
        if (string.IsNullOrWhiteSpace(titre))
        {
            return false;
        }

        string titreNormalise = titre.Trim().ToLowerInvariant();

        return titreNormalise
            is "selectionner un dossier"
                or "select a folder"
                or "select a directory"
                or "ouvrir"
                or "open"
                or "browse for folder";
    }

    /*
     * Indique si un titre de fenêtre correspond plutôt à un dialogue PCSX2.
     */
    private static bool EstDialoguePCSX2(string titre)
    {
        if (string.IsNullOrWhiteSpace(titre))
        {
            return false;
        }

        string titreNormalise = NormaliserTexteComparaison(titre);

        if (titreNormalise.StartsWith("pcsx2 v", StringComparison.Ordinal))
        {
            return true;
        }

        if (titreNormalise.StartsWith("pcsx2 update installer", StringComparison.Ordinal))
        {
            return true;
        }

        return titreNormalise
            is "mise a jour automatique"
                or "lancer un disque"
                or "telecharger des jaquettes"
                or "parametres pcsx2"
                or "scanner les sous-dossiers ?"
                or "confirmer l'extinction"
                or "attention : memory card occupee"
                or "ouvrir"
                or "open"
                or "browse for folder";
    }

    /*
     * Extrait un titre probable de Project64 directement depuis une fenêtre.
     */
    private static string ExtraireTitreProject64DepuisFenetre(string titreFenetre)
    {
        if (string.IsNullOrWhiteSpace(titreFenetre))
        {
            return string.Empty;
        }

        string[] morceaux = titreFenetre.Split(
            " - ",
            StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries
        );

        if (
            morceaux.Length >= 3
            && morceaux[0].Contains("Project64", StringComparison.OrdinalIgnoreCase)
        )
        {
            if (EstBlocVersionProject64(morceaux[1]))
            {
                if (morceaux.Length == 3)
                {
                    return string.Empty;
                }

                string candidat = morceaux.Length >= 4 ? morceaux[^2] : morceaux[^1];
                return NettoyerTitreJeu(candidat, ["Project64", "RAP64", "RAProject64"]);
            }

            return NettoyerTitreJeu(morceaux[^1], ["Project64", "RAP64", "RAProject64"]);
        }

        if (
            morceaux.Length == 2
            && morceaux[0].Contains("Project64", StringComparison.OrdinalIgnoreCase)
            && EstBlocVersionProject64(morceaux[1])
        )
        {
            return string.Empty;
        }

        if (
            titreFenetre.Contains("Project64", StringComparison.OrdinalIgnoreCase)
            && (
                TitreFenetreProject64VersionSeuleRegex().IsMatch(titreFenetre.Trim())
                || TitreFenetreProject64GeneriqueRegex().IsMatch(titreFenetre.Trim())
            )
        )
        {
            return string.Empty;
        }

        return ExtraireTitreAvecSeparateurs(titreFenetre, "Project64", "RAP64", "RAProject64");
    }

    /*
     * Indique si une valeur de fenêtre correspond uniquement à un bloc de version.
     */
    private static bool EstBlocVersionProject64(string valeur)
    {
        if (string.IsNullOrWhiteSpace(valeur))
        {
            return false;
        }

        return BlocVersionProject64Regex().IsMatch(valeur.Trim());
    }

    /*
     * Tente d'extraire le titre DuckStation via l'automatisation d'interface.
     */
    private static string ExtraireTitreDuckStationDepuisAutomatisation(Process processus)
    {
        try
        {
            processus.Refresh();

            if (processus.MainWindowHandle == IntPtr.Zero)
            {
                return string.Empty;
            }

            AutomationElement fenetre = AutomationElement.FromHandle(processus.MainWindowHandle);
            AutomationElementCollection elements = fenetre.FindAll(
                TreeScope.Descendants,
                Condition.TrueCondition
            );

            string meilleurNom = string.Empty;
            double meilleurScore = 0;

            foreach (AutomationElement element in elements.Cast<AutomationElement>())
            {
                string nom = element.Current.Name?.Trim() ?? string.Empty;

                if (string.IsNullOrWhiteSpace(nom))
                {
                    continue;
                }

                double score = EvaluerNomAutomatisationDuckStation(nom);

                if (score > meilleurScore)
                {
                    meilleurScore = score;
                    meilleurNom = nom;
                }
            }

            return meilleurScore >= 1.2 ? meilleurNom : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    /*
     * Évalue la pertinence d'un nom issu de l'automatisation DuckStation.
     */
    private static double EvaluerNomAutomatisationDuckStation(string nom)
    {
        string valeur = nom.Trim();

        if (string.IsNullOrWhiteSpace(valeur))
        {
            return 0;
        }

        string valeurMinuscule = valeur.ToLowerInvariant();

        string[] termesExclus =
        [
            "duckstation",
            "file",
            "emulation",
            "system",
            "settings",
            "tools",
            "view",
            "help",
            "debug",
            "fullscreen",
            "pause",
            "resume",
            "controller",
            "memory card",
            "game list",
            "toolbar",
            "status bar",
            "qt",
        ];

        if (termesExclus.Any(terme => valeurMinuscule.Contains(terme, StringComparison.Ordinal)))
        {
            return 0;
        }

        if (ValeurNumeriqueSeuleRegex().IsMatch(valeurMinuscule))
        {
            return 0;
        }

        double score = 0;

        if (valeur.Length >= 4)
        {
            score += 0.4;
        }

        if (valeur.Contains(' '))
        {
            score += 0.4;
        }

        if (ContientDeuxLettresRegex().IsMatch(valeur))
        {
            score += 0.4;
        }

        if (valeur.Length >= 8)
        {
            score += 0.2;
        }

        return score;
    }

    /*
     * Journalise l'état local seulement lorsqu'il diffère du précédent.
     */
    private void JournaliserSiChangement(EtatSondeLocaleEmulateur etat)
    {
        if (string.Equals(_derniereSignatureJournalisee, etat.Signature, StringComparison.Ordinal))
        {
            return;
        }

        _derniereSignatureJournalisee = etat.Signature;
        Journaliser(etat);
    }

    /*
     * Écrit un état local complet dans le journal de diagnostic.
     */
    private static void Journaliser(EtatSondeLocaleEmulateur etat)
    {
        _ = ServiceModeDiagnostic.JournaliserLigne(
            CheminJournalSondeLocale,
            string.Create(
                CultureInfo.InvariantCulture,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] detecte={etat.EmulateurDetecte};emulateur={NettoyerPourJournal(etat.NomEmulateur)};processus={NettoyerPourJournal(etat.NomProcessus)};cheminExecutable={NettoyerPourJournal(etat.CheminExecutable)};titreFenetre={NettoyerPourJournal(etat.TitreFenetre)};titreJeu={NettoyerPourJournal(etat.TitreJeuProbable)};gameId={etat.IdentifiantJeuProbable.ToString(CultureInfo.InvariantCulture)};diagnostic={NettoyerPourJournal(etat.InformationsDiagnostic)};signature={NettoyerPourJournal(etat.Signature)}{Environment.NewLine}"
            )
        );
    }

    /*
     * Nettoie une valeur textuelle avant de l'injecter dans le journal.
     */
    private static string NettoyerPourJournal(string? valeur)
    {
        return string.IsNullOrWhiteSpace(valeur)
            ? string.Empty
            : valeur.Replace("\r", " ").Replace("\n", " ").Trim();
    }

    /*
     * Retourne la liste des titres visibles associés à un processus Windows.
     */
    private static IReadOnlyList<string> LireTitresFenetresVisibles(Process processus)
    {
        List<string> titres = [];

        try
        {
            EnumWindows(
                (handle, _) =>
                {
                    if (!IsWindowVisible(handle))
                    {
                        return true;
                    }

                    if (GetWindowThreadProcessId(handle, out uint identifiantProcessusFenetre) == 0)
                    {
                        return true;
                    }

                    if (identifiantProcessusFenetre != unchecked((uint)processus.Id))
                    {
                        return true;
                    }

                    int longueur = GetWindowTextLength(handle);

                    if (longueur <= 0)
                    {
                        return true;
                    }

                    StringBuilder constructeur = new(longueur + 1);
                    _ = GetWindowText(handle, constructeur, constructeur.Capacity);
                    string titre = constructeur.ToString().Trim();

                    if (!string.IsNullOrWhiteSpace(titre))
                    {
                        titres.Add(titre);
                    }

                    return true;
                },
                IntPtr.Zero
            );
        }
        catch
        {
            return [];
        }

        return [.. titres.Distinct(StringComparer.Ordinal)];
    }

    /*
     * Choisit le meilleur titre de fenêtre à retenir pour un émulateur donné.
     */
    private static string ChoisirTitreFenetre(
        DefinitionEmulateurLocal definition,
        Process processus,
        IReadOnlyList<string> titres
    )
    {
        string titrePrincipal = processus.MainWindowTitle?.Trim() ?? string.Empty;

        if (PeutRetenirTitreFenetrePourEmulateur(definition, processus, titrePrincipal))
        {
            return titrePrincipal;
        }

        if (titres.Count > 0)
        {
            return titres
                    .Where(titre =>
                        PeutRetenirTitreFenetrePourEmulateur(definition, processus, titre)
                    )
                    .OrderByDescending(titre =>
                        CalculerPrioriteTitreFenetre(definition, processus, titre, titrePrincipal)
                    )
                    .ThenByDescending(titre => titre.Length)
                    .ThenByDescending(titre => titre.Contains(" - ", StringComparison.Ordinal))
                    .FirstOrDefault()
                ?? string.Empty;
        }

        processus.Refresh();
        titrePrincipal = processus.MainWindowTitle?.Trim() ?? string.Empty;
        return PeutRetenirTitreFenetrePourEmulateur(definition, processus, titrePrincipal)
            ? titrePrincipal
            : string.Empty;
    }

    /*
     * Indique si un titre de fenêtre peut être retenu pour un émulateur précis.
     */
    private static bool PeutRetenirTitreFenetrePourEmulateur(
        DefinitionEmulateurLocal definition,
        Process processus,
        string titreFenetre
    )
    {
        string titreNettoye = titreFenetre?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(titreNettoye))
        {
            return false;
        }

        if (TitreFenetreSembleParasite(titreNettoye))
        {
            return false;
        }

        if (TitreFenetreReferenceEmulateur(definition, titreNettoye))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(
            ExtraireTitreJeuPourDefinition(definition, processus, titreNettoye)
        );
    }

    /*
     * Calcule une priorité relative pour départager plusieurs titres de fenêtre.
     */
    private static int CalculerPrioriteTitreFenetre(
        DefinitionEmulateurLocal definition,
        Process processus,
        string titre,
        string titrePrincipal
    )
    {
        int score = 0;
        string titreNettoye = titre?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(titreNettoye))
        {
            return int.MinValue;
        }

        string titreJeu = ExtraireTitreJeuPourDefinition(definition, processus, titreNettoye);

        if (!string.IsNullOrWhiteSpace(titreJeu))
        {
            score += 1000;
            score += Math.Min(titreJeu.Length, 120);
        }

        if (
            !string.IsNullOrWhiteSpace(titrePrincipal)
            && string.Equals(titreNettoye, titrePrincipal, StringComparison.Ordinal)
        )
        {
            score += 180;
        }

        if (TitreFenetreReferenceEmulateur(definition, titreNettoye))
        {
            score += 80;
        }

        if (TitreFenetreSembleParasite(titreNettoye))
        {
            score -= 220;
        }
        else
        {
            score += 25;
        }

        if (titreNettoye.Contains(" - ", StringComparison.Ordinal))
        {
            score += 10;
        }

        return score;
    }

    /*
     * Vérifie si un titre de fenêtre référence explicitement l'émulateur.
     */
    private static bool TitreFenetreReferenceEmulateur(
        DefinitionEmulateurLocal definition,
        string titreFenetre
    )
    {
        if (titreFenetre.Contains(definition.NomEmulateur, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return definition.NomsProcessus.Any(nom =>
            !string.IsNullOrWhiteSpace(nom)
            && titreFenetre.Contains(nom, StringComparison.OrdinalIgnoreCase)
        );
    }

    /*
     * Détermine si un titre de fenêtre semble parasite pour la détection du jeu.
     */
    private static bool TitreFenetreSembleParasite(string titreFenetre)
    {
        string titreNormalise = NormaliserTexteComparaison(titreFenetre);

        if (string.IsNullOrWhiteSpace(titreNormalise))
        {
            return true;
        }

        string[] fragmentsParasites =
        [
            "explorateur de fichiers",
            "file explorer",
            "ouvrir",
            "open",
            "enregistrer",
            "save as",
            "proprietes",
            "properties",
            "parametres",
            "settings",
            "options",
            "configuration",
            "a propos",
            "about",
            "outils",
            "tools",
            "debug",
            "debugger",
            "console",
            "plugins",
            "plugin",
            "controleur",
            "controller",
            "input config",
            "input settings",
            "cheats",
            "retroachievements",
        ];

        return fragmentsParasites.Any(fragment =>
            titreNormalise.Contains(fragment, StringComparison.Ordinal)
        );
    }

    /*
     * Normalise un texte pour les comparaisons strictes de diagnostics.
     */
    private static string NormaliserTexteComparaison(string valeur)
    {
        if (string.IsNullOrWhiteSpace(valeur))
        {
            return string.Empty;
        }

        string normalise = valeur.Normalize(NormalizationForm.FormD);
        StringBuilder constructeur = new(normalise.Length);

        foreach (char caractere in normalise)
        {
            UnicodeCategory categorie = CharUnicodeInfo.GetUnicodeCategory(caractere);

            if (categorie != UnicodeCategory.NonSpacingMark)
            {
                constructeur.Append(caractere);
            }
        }

        return constructeur.ToString().Normalize(NormalizationForm.FormC).Trim().ToLowerInvariant();
    }

    /*
     * Normalise un titre pour les comparaisons souples entre variantes.
     */
    private static string NormaliserTitreComparaisonSouple(string valeur)
    {
        string normalise = NormaliserTexteComparaison(valeur);

        if (string.IsNullOrWhiteSpace(normalise))
        {
            return string.Empty;
        }

        normalise = TexteEntreParenthesesRegex().Replace(normalise, " ");
        normalise = CaracteresNonAlphaNumeriquesRegex().Replace(normalise, " ");
        return EspacesMultiplesRegex().Replace(normalise, " ").Trim();
    }

    /*
     * Indique si deux titres semblent représenter le même jeu.
     */
    private static bool TitresSemblables(string titreA, string titreB)
    {
        string normaliseA = NormaliserTitreComparaisonSouple(titreA);
        string normaliseB = NormaliserTitreComparaisonSouple(titreB);

        if (string.IsNullOrWhiteSpace(normaliseA) || string.IsNullOrWhiteSpace(normaliseB))
        {
            return false;
        }

        return string.Equals(normaliseA, normaliseB, StringComparison.Ordinal)
            || normaliseA.Contains(normaliseB, StringComparison.Ordinal)
            || normaliseB.Contains(normaliseA, StringComparison.Ordinal);
    }

    /*
     * Construit un résumé de diagnostic sur la source utilisée pour identifier
     * le jeu courant.
     */
    private static string ConstruireDiagnosticSourceJeu(
        StrategieRenseignementJeuEmulateurLocal strategie,
        int identifiantJeu
    )
    {
        string source = strategie switch
        {
            StrategieRenseignementJeuEmulateurLocal.DolphinConfig => "dolphin_process",
            StrategieRenseignementJeuEmulateurLocal.Project64RACache => "project64_racache",
            StrategieRenseignementJeuEmulateurLocal.RALibretroRACache => "ralibretro_racache",
            StrategieRenseignementJeuEmulateurLocal.RANesRACache => "ranes_racache",
            StrategieRenseignementJeuEmulateurLocal.RAVBARACache => "ravba_racache",
            StrategieRenseignementJeuEmulateurLocal.RASnes9xRACache => "rasnes9x_racache",
            StrategieRenseignementJeuEmulateurLocal.FlycastConfig => "flycast_config",
            StrategieRenseignementJeuEmulateurLocal.RetroArchLog => "retroarch_log",
            StrategieRenseignementJeuEmulateurLocal.DuckStationLog => "duckstation_log",
            StrategieRenseignementJeuEmulateurLocal.PCSX2Log => "pcsx2_log",
            StrategieRenseignementJeuEmulateurLocal.PPSSPPLog => "ppsspp_log",
            StrategieRenseignementJeuEmulateurLocal.SkyEmuRecentGames => "skyemu_recent_games",
            _ => "inconnue",
        };

        return $"source={source};gameId={identifiantJeu.ToString(CultureInfo.InvariantCulture)}";
    }

    /*
     * Construit un diagnostic détaillé spécifique à DuckStation.
     */
    private static string ConstruireDiagnosticDuckStation(
        Process processus,
        IReadOnlyList<string> titresFenetres
    )
    {
        try
        {
            List<string> morceaux = [];

            if (titresFenetres.Count > 0)
            {
                morceaux.Add($"titres=[{string.Join(" | ", titresFenetres)}]");
            }

            if (processus.MainWindowHandle != IntPtr.Zero)
            {
                AutomationElement fenetre = AutomationElement.FromHandle(
                    processus.MainWindowHandle
                );
                AutomationElementCollection elements = fenetre.FindAll(
                    TreeScope.Descendants,
                    Condition.TrueCondition
                );

                string[] noms =
                [
                    .. elements
                        .Cast<AutomationElement>()
                        .Select(element => element.Current.Name?.Trim() ?? string.Empty)
                        .Where(nom => !string.IsNullOrWhiteSpace(nom))
                        .Distinct(StringComparer.Ordinal)
                        .Take(12),
                ];

                if (noms.Length > 0)
                {
                    morceaux.Add($"ui=[{string.Join(" | ", noms)}]");
                }
            }

            string ligneCommande = LireLigneCommandeProcessus(processus);

            if (!string.IsNullOrWhiteSpace(ligneCommande))
            {
                string cheminJeu = ExtraireCheminJeuDepuisLigneCommande(ligneCommande);

                morceaux.Add(
                    string.IsNullOrWhiteSpace(cheminJeu)
                        ? $"cmd=[{ligneCommande}]"
                        : $"cmdJeu=[{cheminJeu}]"
                );
            }

            string titreMemcard = ExtraireTitreDuckStationDepuisMemcardRecente();

            if (!string.IsNullOrWhiteSpace(titreMemcard))
            {
                morceaux.Add($"memcardJeu=[{titreMemcard}]");
            }

            string details = string.Join("; ", morceaux);

            return string.IsNullOrWhiteSpace(details)
                ? "source=duckstation_fallback"
                : $"source=duckstation_fallback; {details}";
        }
        catch
        {
            return string.Empty;
        }
    }

    /*
     * Lit la ligne de commande complète d'un processus lorsque c'est possible.
     */
    private static string LireLigneCommandeProcessus(Process processus)
    {
        try
        {
            int statut = NtQueryInformationProcess(
                processus.Handle,
                ProcessCommandLineInformation,
                IntPtr.Zero,
                0,
                out int tailleRetour
            );

            if (tailleRetour <= 0 && statut != unchecked((int)0xC0000004))
            {
                return string.Empty;
            }

            IntPtr buffer = Marshal.AllocHGlobal(tailleRetour);

            try
            {
                statut = NtQueryInformationProcess(
                    processus.Handle,
                    ProcessCommandLineInformation,
                    buffer,
                    tailleRetour,
                    out tailleRetour
                );

                if (statut < 0)
                {
                    return string.Empty;
                }

                UnicodeString commande = Marshal.PtrToStructure<UnicodeString>(buffer);

                if (commande.Length <= 0 || commande.Buffer == IntPtr.Zero)
                {
                    return string.Empty;
                }

                return Marshal.PtrToStringUni(commande.Buffer, commande.Length / 2)?.Trim()
                    ?? string.Empty;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        catch
        {
            return string.Empty;
        }
    }

    /*
     * Tente d'extraire un chemin de jeu depuis une ligne de commande brute.
     */
    private static string ExtraireCheminJeuDepuisLigneCommande(string ligneCommande)
    {
        if (string.IsNullOrWhiteSpace(ligneCommande))
        {
            return string.Empty;
        }

        MatchCollection correspondances = JetonsLigneCommandeRegex().Matches(ligneCommande);

        string[] extensionsJeuPossibles =
        [
            ".cue",
            ".cdi",
            ".gdi",
            ".chd",
            ".iso",
            ".bin",
            ".img",
            ".pbp",
            ".m3u",
            ".ecm",
            ".zip",
            ".exe",
            ".nes",
            ".fds",
            ".unf",
            ".unif",
            ".smc",
            ".sfc",
            ".fig",
            ".gb",
            ".gbc",
            ".gba",
            ".nds",
            ".n64",
            ".z64",
            ".v64",
            ".gen",
            ".md",
            ".smd",
            ".sms",
            ".gg",
            ".sg",
            ".32x",
            ".a26",
            ".lnx",
            ".pce",
            ".sgx",
            ".ws",
            ".wsc",
            ".col",
            ".int",
            ".rom",
            ".prg",
            ".d64",
            ".g64",
            ".crt",
            ".tap",
        ];

        foreach (Match correspondance in correspondances.Cast<Match>().Reverse())
        {
            string valeur = correspondance.Groups[1].Success
                ? correspondance.Groups[1].Value
                : correspondance.Groups[2].Value;

            if (string.IsNullOrWhiteSpace(valeur))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(valeur) || valeur.StartsWith('-'))
            {
                continue;
            }

            string extension = Path.GetExtension(valeur);

            if (
                !string.IsNullOrWhiteSpace(extension)
                && extensionsJeuPossibles.Contains(extension, StringComparer.OrdinalIgnoreCase)
            )
            {
                return valeur.Trim();
            }
        }

        return string.Empty;
    }

    /*
     * Tente de retrouver le chemin du jeu Dolphin par plusieurs sources locales.
     */
    private static string LireCheminJeuDolphin(string titreJeuProbable)
    {
        string cheminDepuisProcessus = LireCheminJeuDolphinDepuisProcessusActif();

        if (!string.IsNullOrWhiteSpace(cheminDepuisProcessus))
        {
            return cheminDepuisProcessus;
        }

        return LireCheminJeuDolphinDepuisConfiguration(titreJeuProbable);
    }

    /*
     * Tente de retrouver le chemin du jeu Dolphin depuis le processus actif.
     */
    private static string LireCheminJeuDolphinDepuisProcessusActif()
    {
        DefinitionEmulateurLocal? definition = ServiceCatalogueEmulateursLocaux.TrouverParNom(
            "Dolphin"
        );

        if (definition is null)
        {
            return string.Empty;
        }

        try
        {
            foreach (Process processus in Process.GetProcesses())
            {
                if (!Correspond(processus, definition))
                {
                    continue;
                }

                string cheminExecutable = LireCheminExecutableProcessus(processus);
                string cheminJeu = NormaliserCheminJeuProbable(
                    ExtraireCheminJeuDepuisLigneCommande(LireLigneCommandeProcessus(processus))
                );

                if (
                    !string.IsNullOrWhiteSpace(cheminJeu)
                    && !string.Equals(
                        cheminJeu,
                        cheminExecutable,
                        StringComparison.OrdinalIgnoreCase
                    )
                    && !string.Equals(
                        Path.GetExtension(cheminJeu),
                        ".exe",
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                {
                    return cheminJeu;
                }
            }
        }
        catch { }

        return string.Empty;
    }

    /*
     * Tente de retrouver le chemin du jeu Dolphin depuis les fichiers de configuration.
     */
    private static string LireCheminJeuDolphinDepuisConfiguration(string titreJeuProbable)
    {
        try
        {
            List<string> repertoiresCandidats = [];
            string cheminQt = ServiceSourcesLocalesEmulateurs.TrouverCheminConfigurationQtDolphin();

            if (File.Exists(cheminQt))
            {
                foreach (string ligne in File.ReadLines(cheminQt))
                {
                    if (!ligne.StartsWith("lastdir=", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string valeur = ligne[(ligne.IndexOf('=') + 1)..].Trim().Trim('"');

                    if (!string.IsNullOrWhiteSpace(valeur))
                    {
                        repertoiresCandidats.Add(valeur);
                    }
                }
            }

            string cheminDolphinIni =
                ServiceSourcesLocalesEmulateurs.TrouverCheminConfigurationDolphin();

            if (File.Exists(cheminDolphinIni))
            {
                foreach (string ligne in File.ReadLines(cheminDolphinIni))
                {
                    if (!ligne.StartsWith("ISOPath", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    int indexSeparateur = ligne.IndexOf('=');

                    if (indexSeparateur < 0 || indexSeparateur == ligne.Length - 1)
                    {
                        continue;
                    }

                    string valeur = ligne[(indexSeparateur + 1)..].Trim().Trim('"');

                    if (!string.IsNullOrWhiteSpace(valeur))
                    {
                        repertoiresCandidats.Add(valeur);
                    }
                }
            }

            repertoiresCandidats =
            [
                .. repertoiresCandidats
                    .Select(repertoire => repertoire.Replace('/', Path.DirectorySeparatorChar))
                    .Distinct(StringComparer.OrdinalIgnoreCase),
            ];

            if (repertoiresCandidats.Count == 0)
            {
                return string.Empty;
            }

            return TrouverCheminJeuDolphinDepuisRepertoires(repertoiresCandidats, titreJeuProbable);
        }
        catch
        {
            return string.Empty;
        }
    }

    /*
     * Recherche le meilleur chemin de jeu Dolphin depuis plusieurs répertoires candidats.
     */
    private static string TrouverCheminJeuDolphinDepuisRepertoires(
        IReadOnlyCollection<string> repertoires,
        string titreJeuProbable
    )
    {
        string[] extensions =
        [
            ".iso",
            ".gcm",
            ".wbfs",
            ".ciso",
            ".gcz",
            ".rvz",
            ".wia",
            ".wad",
            ".elf",
            ".dol",
        ];
        List<string> cheminsCandidats = [];

        foreach (string repertoire in repertoires)
        {
            if (string.IsNullOrWhiteSpace(repertoire) || !Directory.Exists(repertoire))
            {
                continue;
            }

            IEnumerable<string> cheminsTrouves = new DirectoryInfo(repertoire)
                .EnumerateFiles("*.*", SearchOption.AllDirectories)
                .Where(fichier =>
                    extensions.Contains(fichier.Extension, StringComparer.OrdinalIgnoreCase)
                )
                .Select(fichier => fichier.FullName);

            cheminsCandidats.AddRange(cheminsTrouves);
        }

        cheminsCandidats = [.. cheminsCandidats.Distinct(StringComparer.OrdinalIgnoreCase)];

        if (cheminsCandidats.Count == 0)
        {
            return string.Empty;
        }

        if (string.IsNullOrWhiteSpace(titreJeuProbable))
        {
            return cheminsCandidats[0];
        }

        string titreNormalise = NormaliserTitreComparaisonSouple(titreJeuProbable);
        string meilleurChemin = string.Empty;
        int meilleurScore = int.MinValue;

        foreach (string cheminCandidat in cheminsCandidats)
        {
            string nomFichier = NettoyerNomFichierJeu(
                Path.GetFileNameWithoutExtension(cheminCandidat)
            );
            string nomDossier = NettoyerNomFichierJeu(
                Path.GetFileName(Path.GetDirectoryName(cheminCandidat) ?? string.Empty)
            );

            int score = 0;

            if (TitresSemblables(nomFichier, titreJeuProbable))
            {
                score += 120;
            }
            else
            {
                score += CalculerScoreProximiteTitreSouple(titreNormalise, nomFichier) * 10;
            }

            if (TitresSemblables(nomDossier, titreJeuProbable))
            {
                score += 80;
            }
            else
            {
                score += CalculerScoreProximiteTitreSouple(titreNormalise, nomDossier) * 6;
            }

            if (score <= meilleurScore)
            {
                continue;
            }

            meilleurScore = score;
            meilleurChemin = cheminCandidat;
        }

        return meilleurScore >= 30 ? meilleurChemin : string.Empty;
    }

    /*
     * Lit le renseignement jeu courant de Dolphin depuis son journal local.
     */
    private static RenseignementJeuRA? LireRenseignementJeuDolphinDepuisLog()
    {
        try
        {
            string cheminJournal = ServiceSourcesLocalesEmulateurs.TrouverCheminJournalDolphin();

            if (string.IsNullOrWhiteSpace(cheminJournal) || !File.Exists(cheminJournal))
            {
                return null;
            }

            RenseignementJeuRA? renseignementSecours = null;

            foreach (
                string ligne in ServiceSourcesLocalesEmulateurs
                    .LireToutesLesLignesAvecPartage(cheminJournal)
                    .AsEnumerable()
                    .Reverse()
            )
            {
                Match correspondanceIdentifiee = DolphinGameIdentifieRegex().Match(ligne);

                if (
                    correspondanceIdentifiee.Success
                    && int.TryParse(
                        correspondanceIdentifiee.Groups[1].Value,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out int identifiantJeuIdentifie
                    )
                    && identifiantJeuIdentifie > 0
                )
                {
                    return new RenseignementJeuRA(
                        identifiantJeuIdentifie,
                        correspondanceIdentifiee.Groups[2].Value.Trim()
                    );
                }

                Match correspondanceChargee = DolphinGameChargeRegex().Match(ligne);

                if (
                    correspondanceChargee.Success
                    && int.TryParse(
                        correspondanceChargee.Groups[1].Value,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out int identifiantJeuCharge
                    )
                    && identifiantJeuCharge > 0
                )
                {
                    renseignementSecours ??= new RenseignementJeuRA(
                        identifiantJeuCharge,
                        string.Empty
                    );
                    continue;
                }

                Match correspondanceGenerique = JournalGameIdRegex().Match(ligne);

                if (
                    correspondanceGenerique.Success
                    && int.TryParse(
                        correspondanceGenerique.Groups[1].Value,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out int identifiantJeuGenerique
                    )
                    && identifiantJeuGenerique > 0
                )
                {
                    renseignementSecours ??= new RenseignementJeuRA(
                        identifiantJeuGenerique,
                        string.Empty
                    );
                }
            }

            return renseignementSecours;
        }
        catch
        {
            return null;
        }
    }

    /*
     * Normalise un chemin de jeu probable pour faciliter les comparaisons.
     */
    private static string NormaliserCheminJeuProbable(string cheminJeu)
    {
        if (string.IsNullOrWhiteSpace(cheminJeu))
        {
            return string.Empty;
        }

        try
        {
            string cheminNormalise = Path.GetFullPath(cheminJeu.Trim());
            return File.Exists(cheminNormalise) ? cheminNormalise : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    /*
     * Lit le renseignement jeu courant de Flycast depuis son journal local.
     */
    private static RenseignementJeuRA? LireRenseignementJeuFlycastDepuisLog(
        Process processus,
        string titreJeuFenetre
    )
    {
        try
        {
            string cheminJournal = ServiceSourcesLocalesEmulateurs.TrouverCheminJournalFlycast();

            if (string.IsNullOrWhiteSpace(cheminJournal) || !File.Exists(cheminJournal))
            {
                return ConstruireRenseignementJeuFlycastDepuisCommande(processus, titreJeuFenetre);
            }

            FileInfo fichierJournal = new(cheminJournal);

            if (
                fichierJournal.Length <= 0
                || DateTime.UtcNow - fichierJournal.LastWriteTimeUtc > TimeSpan.FromMinutes(15)
            )
            {
                return ConstruireRenseignementJeuFlycastDepuisCommande(processus, titreJeuFenetre);
            }

            foreach (
                string ligne in ServiceSourcesLocalesEmulateurs
                    .LireToutesLesLignesAvecPartage(cheminJournal)
                    .AsEnumerable()
                    .Reverse()
            )
            {
                Match correspondanceCharge = FlycastGameLoadedRegex().Match(ligne);

                if (
                    correspondanceCharge.Success
                    && int.TryParse(
                        correspondanceCharge.Groups[1].Value,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out int identifiantJeuCharge
                    )
                    && identifiantJeuCharge > 0
                )
                {
                    string titreJeu = correspondanceCharge.Groups[2].Value.Trim();
                    return new RenseignementJeuRA(identifiantJeuCharge, titreJeu);
                }

                Match correspondanceId = JournalGameIdRegex().Match(ligne);

                if (
                    correspondanceId.Success
                    && int.TryParse(
                        correspondanceId.Groups[1].Value,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out int identifiantJeu
                    )
                    && identifiantJeu > 0
                )
                {
                    return ConstruireRenseignementJeuFlycastDepuisCommande(
                        processus,
                        titreJeuFenetre,
                        identifiantJeu
                    );
                }
            }
        }
        catch { }

        return ConstruireRenseignementJeuFlycastDepuisCommande(processus, titreJeuFenetre);
    }

    /*
     * Construit le renseignement jeu Flycast pour le mode Rejouer à partir
     * des sources locales disponibles.
     */
    private static RenseignementJeuRA? LireRenseignementJeuFlycastDepuisSourcesRejouer()
    {
        try
        {
            string cheminJournal = ServiceSourcesLocalesEmulateurs.TrouverCheminJournalFlycast();

            if (string.IsNullOrWhiteSpace(cheminJournal) || !File.Exists(cheminJournal))
            {
                return null;
            }

            int identifiantJeu = 0;
            string titreJeu = string.Empty;

            foreach (
                string ligne in ServiceSourcesLocalesEmulateurs
                    .LireToutesLesLignesAvecPartage(cheminJournal)
                    .AsEnumerable()
                    .Reverse()
            )
            {
                if (string.IsNullOrWhiteSpace(titreJeu))
                {
                    Match correspondanceCharge = FlycastGameLoadedRegex().Match(ligne);

                    if (correspondanceCharge.Success)
                    {
                        if (
                            int.TryParse(
                                correspondanceCharge.Groups[1].Value,
                                NumberStyles.Integer,
                                CultureInfo.InvariantCulture,
                                out int identifiantCharge
                            )
                        )
                        {
                            identifiantJeu = identifiantCharge;
                        }

                        titreJeu = correspondanceCharge.Groups[2].Value.Trim();
                    }
                }

                if (identifiantJeu <= 0)
                {
                    Match correspondanceId = JournalGameIdRegex().Match(ligne);

                    if (
                        correspondanceId.Success
                        && int.TryParse(
                            correspondanceId.Groups[1].Value,
                            NumberStyles.Integer,
                            CultureInfo.InvariantCulture,
                            out int identifiantTrouve
                        )
                    )
                    {
                        identifiantJeu = identifiantTrouve;
                    }
                }

                if (identifiantJeu > 0 && !string.IsNullOrWhiteSpace(titreJeu))
                {
                    return new RenseignementJeuRA(identifiantJeu, titreJeu);
                }
            }

            return identifiantJeu > 0 || !string.IsNullOrWhiteSpace(titreJeu)
                ? new RenseignementJeuRA(identifiantJeu, titreJeu)
                : null;
        }
        catch
        {
            return null;
        }
    }

    /*
     * Lit le chemin du jeu RetroArch depuis son journal local.
     */
    private static string LireCheminJeuRetroArchDepuisLog()
    {
        try
        {
            string cheminJournal =
                ServiceSourcesLocalesEmulateurs.TrouverDernierCheminJournalRetroArch();

            if (string.IsNullOrWhiteSpace(cheminJournal) || !File.Exists(cheminJournal))
            {
                return string.Empty;
            }

            foreach (
                string ligne in ServiceSourcesLocalesEmulateurs
                    .LireToutesLesLignesAvecPartage(cheminJournal)
                    .AsEnumerable()
                    .Reverse()
            )
            {
                Match correspondanceContenu = RetroArchContenuChargeRegex().Match(ligne);

                if (!correspondanceContenu.Success)
                {
                    continue;
                }

                string cheminNormalise = NormaliserCheminJeuProbable(
                    correspondanceContenu.Groups[1].Value.Trim()
                );

                if (!string.IsNullOrWhiteSpace(cheminNormalise))
                {
                    return cheminNormalise;
                }
            }
        }
        catch { }

        return string.Empty;
    }

    /*
     * Lit le chemin du jeu Flycast depuis son journal local.
     */
    private static string LireCheminJeuFlycastDepuisLog()
    {
        try
        {
            string cheminJournal = ServiceSourcesLocalesEmulateurs.TrouverCheminJournalFlycast();

            if (string.IsNullOrWhiteSpace(cheminJournal) || !File.Exists(cheminJournal))
            {
                return string.Empty;
            }

            foreach (
                string ligne in ServiceSourcesLocalesEmulateurs
                    .LireToutesLesLignesAvecPartage(cheminJournal)
                    .AsEnumerable()
                    .Reverse()
            )
            {
                Match correspondanceContenu = FlycastContenuChargeRegex().Match(ligne);

                if (!correspondanceContenu.Success)
                {
                    continue;
                }

                string cheminNormalise = NormaliserCheminJeuProbable(
                    correspondanceContenu.Groups[1].Value.Trim()
                );

                if (!string.IsNullOrWhiteSpace(cheminNormalise))
                {
                    return cheminNormalise;
                }
            }
        }
        catch { }

        return string.Empty;
    }

    /*
     * Construit le renseignement jeu SkyEmu pour le mode Rejouer à partir
     * du fichier recent_games.txt.
     */
    private static RenseignementJeuRA? LireRenseignementJeuSkyEmuDepuisSourcesRejouer()
    {
        string cheminJeu = LireCheminJeuSkyEmuDepuisRecentGames();

        if (string.IsNullOrWhiteSpace(cheminJeu))
        {
            return null;
        }

        string titreJeu = NettoyerNomFichierJeu(Path.GetFileNameWithoutExtension(cheminJeu));
        return string.IsNullOrWhiteSpace(titreJeu) ? null : new RenseignementJeuRA(0, titreJeu);
    }

    /*
     * Lit le chemin du jeu SkyEmu depuis l'état HTTP exposé localement.
     */
    private static string LireCheminJeuSkyEmuDepuisHttp(Process processus)
    {
        return LireEtatHttpSkyEmu(processus)?.CheminRom ?? string.Empty;
    }

    /*
     * Lit l'état HTTP de SkyEmu quand son serveur de contrôle est accessible.
     */
    private static EtatHttpSkyEmu? LireEtatHttpSkyEmu(Process processus)
    {
        try
        {
            int port = LirePortServeurHttpSkyEmu(processus);

            if (port <= 0 || port > 65535)
            {
                return null;
            }

            string contenu = HttpClientSkyEmu
                .GetStringAsync(
                    $"http://127.0.0.1:{port.ToString(CultureInfo.InvariantCulture)}/status"
                )
                .GetAwaiter()
                .GetResult();

            if (string.IsNullOrWhiteSpace(contenu))
            {
                return null;
            }

            using JsonDocument document = JsonDocument.Parse(contenu);

            if (
                !document.RootElement.TryGetProperty(
                    "rom-loaded",
                    out JsonElement romChargeeElement
                )
                || romChargeeElement.ValueKind != JsonValueKind.True
            )
            {
                return null;
            }

            if (
                !document.RootElement.TryGetProperty("rom-path", out JsonElement cheminRomElement)
                || cheminRomElement.ValueKind != JsonValueKind.String
            )
            {
                return null;
            }

            string cheminRom = NormaliserCheminJeuProbable(
                cheminRomElement.GetString() ?? string.Empty
            );

            return string.IsNullOrWhiteSpace(cheminRom)
                ? null
                : new EtatHttpSkyEmu(port, cheminRom);
        }
        catch
        {
            return null;
        }
    }

    /*
     * Détermine le port HTTP de SkyEmu à partir de la ligne de commande ou
     * de son fichier de configuration binaire.
     */
    private static int LirePortServeurHttpSkyEmu(Process processus)
    {
        int port = LirePortServeurHttpSkyEmuDepuisLigneCommande(processus);

        return port > 0 ? port : LirePortServeurHttpSkyEmuDepuisConfiguration();
    }

    /*
     * Lit le port HTTP de SkyEmu lorsqu'il a été lancé en mode http_server.
     */
    private static int LirePortServeurHttpSkyEmuDepuisLigneCommande(Process processus)
    {
        try
        {
            IReadOnlyList<string> jetons = DecouperLigneCommande(
                LireLigneCommandeProcessus(processus)
            );

            for (int index = 0; index < jetons.Count - 1; index++)
            {
                if (
                    !string.Equals(jetons[index], "http_server", StringComparison.OrdinalIgnoreCase)
                )
                {
                    continue;
                }

                if (
                    int.TryParse(
                        jetons[index + 1],
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out int port
                    )
                )
                {
                    return port;
                }
            }
        }
        catch { }

        return 0;
    }

    /*
     * Lit le port HTTP de SkyEmu depuis user_settings.bin si le serveur est
     * activé dans les préférences.
     */
    private static int LirePortServeurHttpSkyEmuDepuisConfiguration()
    {
        return ServiceSourcesLocalesEmulateurs.EssayerLireConfigurationHttpSkyEmu(
            out bool serveurActive,
            out int port
        ) && serveurActive
            ? port
            : 0;
    }

    /*
     * Découpe une ligne de commande brute en jetons ordonnés.
     */
    private static IReadOnlyList<string> DecouperLigneCommande(string ligneCommande)
    {
        if (string.IsNullOrWhiteSpace(ligneCommande))
        {
            return [];
        }

        return
        [
            .. JetonsLigneCommandeRegex()
                .Matches(ligneCommande)
                .Cast<Match>()
                .Select(correspondance =>
                    correspondance.Groups[1].Success
                        ? correspondance.Groups[1].Value
                        : correspondance.Groups[2].Value
                )
                .Where(valeur => !string.IsNullOrWhiteSpace(valeur)),
        ];
    }

    /*
     * Lit le chemin du jeu SkyEmu depuis recent_games.txt, avec un filtrage
     * optionnel sur le titre attendu.
     */
    private static string LireCheminJeuSkyEmuDepuisRecentGames(string titreJeuAttendu = "")
    {
        try
        {
            string cheminRecentGames =
                ServiceSourcesLocalesEmulateurs.TrouverCheminRecentGamesSkyEmu();

            if (string.IsNullOrWhiteSpace(cheminRecentGames) || !File.Exists(cheminRecentGames))
            {
                return string.Empty;
            }

            foreach (
                string ligne in ServiceSourcesLocalesEmulateurs.LireToutesLesLignesAvecPartage(
                    cheminRecentGames
                )
            )
            {
                string cheminNormalise = NormaliserCheminJeuProbable(ligne.Trim());

                if (string.IsNullOrWhiteSpace(cheminNormalise))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(titreJeuAttendu))
                {
                    return cheminNormalise;
                }

                string titreObserve = NettoyerNomFichierJeu(
                    Path.GetFileNameWithoutExtension(cheminNormalise)
                );

                if (TitresSemblables(titreJeuAttendu, titreObserve))
                {
                    return cheminNormalise;
                }
            }
        }
        catch { }

        return string.Empty;
    }

    /*
     * Construit un renseignement jeu Flycast à partir d'un chemin issu
     * d'une commande ou d'un journal.
     */
    private static RenseignementJeuRA? ConstruireRenseignementJeuFlycastDepuisCommande(
        Process processus,
        string titreJeuFenetre,
        int identifiantJeu = 0
    )
    {
        try
        {
            string ligneCommande = LireLigneCommandeProcessus(processus);
            string cheminJeu = ExtraireCheminJeuDepuisLigneCommande(ligneCommande);

            string titreDepuisCommande = string.IsNullOrWhiteSpace(cheminJeu)
                ? string.Empty
                : NettoyerNomFichierJeu(Path.GetFileNameWithoutExtension(cheminJeu));
            string titreRetenu = !string.IsNullOrWhiteSpace(titreDepuisCommande)
                ? titreDepuisCommande
                : titreJeuFenetre;

            if (identifiantJeu <= 0 && string.IsNullOrWhiteSpace(titreRetenu))
            {
                return null;
            }

            return new RenseignementJeuRA(identifiantJeu, titreRetenu);
        }
        catch
        {
            return null;
        }
    }

    /*
     * Lit le renseignement jeu courant de RetroArch depuis son journal local.
     */
    private static RenseignementJeuRA? LireRenseignementJeuRetroArchDepuisLog()
    {
        try
        {
            string cheminJournal =
                ServiceSourcesLocalesEmulateurs.TrouverDernierCheminJournalRetroArch();

            if (string.IsNullOrWhiteSpace(cheminJournal))
            {
                JournaliserEvenement("retroarch_log_absent", "chemin=");
                return null;
            }

            if (
                DateTime.UtcNow - File.GetLastWriteTimeUtc(cheminJournal)
                > TimeSpan.FromMinutes(15)
            )
            {
                JournaliserEvenement("retroarch_log_trop_ancien", $"chemin={cheminJournal}");
                return LireRenseignementJeuRetroArchDepuisCache();
            }

            int? identifiantJeuSessionObserve = null;
            string titreJeuObserve = string.Empty;

            foreach (
                string ligne in ServiceSourcesLocalesEmulateurs
                    .LireToutesLesLignesAvecPartage(cheminJournal)
                    .AsEnumerable()
                    .Reverse()
            )
            {
                Match correspondanceIdentifie = RetroArchGameIdentifieRegex().Match(ligne);

                if (
                    correspondanceIdentifie.Success
                    && int.TryParse(
                        correspondanceIdentifie.Groups[1].Value,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out int identifiantJeuIdentifie
                    )
                )
                {
                    string titreJeu = correspondanceIdentifie.Groups[2].Value.Trim();
                    JournaliserEvenement(
                        "retroarch_log_identified",
                        $"chemin={cheminJournal};gameId={identifiantJeuIdentifie.ToString(CultureInfo.InvariantCulture)};titre={titreJeu}"
                    );
                    return MemoriserRenseignementRetroArch(
                        new RenseignementJeuRA(identifiantJeuIdentifie, titreJeu)
                    );
                }

                Match correspondanceCharge = RetroArchGameChargeRegex().Match(ligne);

                if (
                    correspondanceCharge.Success
                    && int.TryParse(
                        correspondanceCharge.Groups[1].Value,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out int identifiantJeuCharge
                    )
                )
                {
                    identifiantJeuSessionObserve ??= identifiantJeuCharge;
                }

                Match correspondanceSession = RetroArchSessionJeuRegex().Match(ligne);

                if (
                    correspondanceSession.Success
                    && int.TryParse(
                        correspondanceSession.Groups[1].Value,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out int identifiantJeuSession
                    )
                )
                {
                    identifiantJeuSessionObserve ??= identifiantJeuSession;
                }

                if (string.IsNullOrWhiteSpace(titreJeuObserve))
                {
                    Match correspondanceContenu = RetroArchContenuChargeRegex().Match(ligne);

                    if (correspondanceContenu.Success)
                    {
                        string cheminJeu = correspondanceContenu.Groups[1].Value.Trim();

                        if (!string.IsNullOrWhiteSpace(cheminJeu))
                        {
                            titreJeuObserve = NettoyerNomFichierJeu(
                                Path.GetFileNameWithoutExtension(cheminJeu)
                            );
                        }
                    }
                }
            }

            if (identifiantJeuSessionObserve.HasValue)
            {
                JournaliserEvenement(
                    "retroarch_log_fallback",
                    $"chemin={cheminJournal};gameId={identifiantJeuSessionObserve.Value.ToString(CultureInfo.InvariantCulture)};titre={titreJeuObserve}"
                );
                return MemoriserRenseignementRetroArch(
                    new RenseignementJeuRA(identifiantJeuSessionObserve.Value, titreJeuObserve)
                );
            }

            JournaliserEvenement("retroarch_log_aucune_correspondance", $"chemin={cheminJournal}");
        }
        catch { }

        return LireRenseignementJeuRetroArchDepuisCache();
    }

    /*
     * Lit le renseignement jeu courant de DuckStation depuis son journal local.
     */
    private static RenseignementJeuRA? LireRenseignementJeuDuckStationDepuisLog()
    {
        try
        {
            string cheminJournal =
                ServiceSourcesLocalesEmulateurs.TrouverCheminJournalDuckStation();

            if (string.IsNullOrWhiteSpace(cheminJournal) || !File.Exists(cheminJournal))
            {
                return null;
            }

            FileInfo fichierJournal = new(cheminJournal);

            if (
                fichierJournal.Length <= 0
                || DateTime.UtcNow - fichierJournal.LastWriteTimeUtc > TimeSpan.FromMinutes(15)
            )
            {
                return null;
            }

            foreach (
                string ligne in ServiceSourcesLocalesEmulateurs
                    .LireToutesLesLignesAvecPartage(cheminJournal)
                    .AsEnumerable()
                    .Reverse()
            )
            {
                Match correspondanceChargement = DuckStationGameLoadedRegex().Match(ligne);

                if (
                    correspondanceChargement.Success
                    && int.TryParse(
                        correspondanceChargement.Groups[2].Value,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out int identifiantJeuCharge
                    )
                    && identifiantJeuCharge > 0
                )
                {
                    string titreJeu = correspondanceChargement.Groups[1].Value.Trim();
                    return new RenseignementJeuRA(identifiantJeuCharge, titreJeu);
                }

                Match correspondance = JournalGameIdRegex().Match(ligne);

                if (
                    correspondance.Success
                    && int.TryParse(
                        correspondance.Groups[1].Value,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out int identifiantJeu
                    )
                    && identifiantJeu > 0
                )
                {
                    return new RenseignementJeuRA(identifiantJeu, string.Empty);
                }
            }
        }
        catch { }

        return null;
    }

    /*
     * Lit le renseignement jeu courant de PCSX2 depuis son journal local.
     */
    private static RenseignementJeuRA? LireRenseignementJeuPCSX2DepuisLog()
    {
        try
        {
            string cheminJournal = ServiceSourcesLocalesEmulateurs.TrouverCheminJournalPCSX2();

            if (string.IsNullOrWhiteSpace(cheminJournal) || !File.Exists(cheminJournal))
            {
                return null;
            }

            FileInfo fichierJournal = new(cheminJournal);

            if (
                fichierJournal.Length <= 0
                || DateTime.UtcNow - fichierJournal.LastWriteTimeUtc > TimeSpan.FromMinutes(15)
            )
            {
                return null;
            }

            foreach (
                string ligne in ServiceSourcesLocalesEmulateurs
                    .LireToutesLesLignesAvecPartage(cheminJournal)
                    .AsEnumerable()
                    .Reverse()
            )
            {
                Match correspondanceIdentifie = PCSX2GameIdentifieRegex().Match(ligne);

                if (
                    correspondanceIdentifie.Success
                    && int.TryParse(
                        correspondanceIdentifie.Groups[1].Value,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out int identifiantJeuIdentifie
                    )
                    && identifiantJeuIdentifie > 0
                )
                {
                    string titreJeu = correspondanceIdentifie.Groups[2].Value.Trim();
                    return new RenseignementJeuRA(identifiantJeuIdentifie, titreJeu);
                }

                Match correspondanceCharge = PCSX2GameChargeRegex().Match(ligne);

                if (
                    correspondanceCharge.Success
                    && int.TryParse(
                        correspondanceCharge.Groups[1].Value,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out int identifiantJeuCharge
                    )
                    && identifiantJeuCharge > 0
                )
                {
                    return new RenseignementJeuRA(identifiantJeuCharge, string.Empty);
                }
            }
        }
        catch { }

        return null;
    }

    /*
     * Lit le renseignement jeu courant de PPSSPP depuis son journal local.
     */
    private static RenseignementJeuRA? LireRenseignementJeuPPSSPPDepuisLog(string titreJeuAttendu)
    {
        try
        {
            string cheminJournal = ServiceSourcesLocalesEmulateurs.TrouverCheminJournalPPSSPP();

            if (string.IsNullOrWhiteSpace(cheminJournal) || !File.Exists(cheminJournal))
            {
                return null;
            }

            FileInfo fichierJournal = new(cheminJournal);

            if (
                fichierJournal.Length <= 0
                || DateTime.UtcNow - fichierJournal.LastWriteTimeUtc > TimeSpan.FromMinutes(15)
            )
            {
                return null;
            }

            bool exigerCorrespondanceTitre = !string.IsNullOrWhiteSpace(titreJeuAttendu);
            List<string> lignes =
            [
                .. ServiceSourcesLocalesEmulateurs.LireToutesLesLignesAvecPartage(cheminJournal),
            ];

            if (exigerCorrespondanceTitre)
            {
                for (int index = lignes.Count - 1; index >= 0; index--)
                {
                    Match correspondanceDemarrage = PPSSPPJeuDemarreRegex().Match(lignes[index]);

                    if (!correspondanceDemarrage.Success)
                    {
                        continue;
                    }

                    string titreDemarre = ExtraireTitrePPSSPPDepuisCheminJeu(
                        correspondanceDemarrage.Groups[1].Value
                    );

                    if (!TitresSemblables(titreJeuAttendu, titreDemarre))
                    {
                        continue;
                    }

                    for (
                        int indexRecherche = index;
                        indexRecherche < lignes.Count && indexRecherche <= index + 40;
                        indexRecherche++
                    )
                    {
                        string ligneRecherche = lignes[indexRecherche];
                        Match correspondanceJeuIdentifie = PPSSPPGameIdentifieRegex()
                            .Match(ligneRecherche);

                        if (
                            correspondanceJeuIdentifie.Success
                            && int.TryParse(
                                correspondanceJeuIdentifie.Groups[1].Value,
                                NumberStyles.Integer,
                                CultureInfo.InvariantCulture,
                                out int identifiantJeuIdentifie
                            )
                            && identifiantJeuIdentifie > 0
                        )
                        {
                            return MemoriserRenseignementPPSSPP(
                                new RenseignementJeuRA(
                                    identifiantJeuIdentifie,
                                    correspondanceJeuIdentifie.Groups[2].Value.Trim()
                                )
                            );
                        }

                        Match correspondanceJeuCharge = PPSSPPGameChargeRegex()
                            .Match(ligneRecherche);

                        if (
                            correspondanceJeuCharge.Success
                            && int.TryParse(
                                correspondanceJeuCharge.Groups[1].Value,
                                NumberStyles.Integer,
                                CultureInfo.InvariantCulture,
                                out int identifiantJeuCharge
                            )
                            && identifiantJeuCharge > 0
                        )
                        {
                            return MemoriserRenseignementPPSSPP(
                                new RenseignementJeuRA(identifiantJeuCharge, titreJeuAttendu)
                            );
                        }
                    }
                }
            }

            RenseignementJeuRA? renseignementSecours = null;

            foreach (string ligne in lignes.AsEnumerable().Reverse())
            {
                Match correspondanceJeuIdentifie = PPSSPPGameIdentifieRegex().Match(ligne);

                if (
                    correspondanceJeuIdentifie.Success
                    && int.TryParse(
                        correspondanceJeuIdentifie.Groups[1].Value,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out int identifiantJeuIdentifie
                    )
                    && identifiantJeuIdentifie > 0
                )
                {
                    RenseignementJeuRA renseignementIdentifie = new(
                        identifiantJeuIdentifie,
                        correspondanceJeuIdentifie.Groups[2].Value.Trim()
                    );

                    if (
                        !exigerCorrespondanceTitre
                        || TitresSemblables(titreJeuAttendu, renseignementIdentifie.TitreJeu)
                    )
                    {
                        return MemoriserRenseignementPPSSPP(renseignementIdentifie);
                    }

                    continue;
                }

                Match correspondanceJeuCharge = PPSSPPGameChargeRegex().Match(ligne);

                if (
                    correspondanceJeuCharge.Success
                    && int.TryParse(
                        correspondanceJeuCharge.Groups[1].Value,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out int identifiantJeuCharge
                    )
                    && identifiantJeuCharge > 0
                )
                {
                    if (exigerCorrespondanceTitre)
                    {
                        continue;
                    }

                    renseignementSecours ??= new RenseignementJeuRA(
                        identifiantJeuCharge,
                        string.Empty
                    );
                    continue;
                }

                Match correspondanceLoadCallback = PPSSPPLoadCallbackRegex().Match(ligne);

                if (
                    correspondanceLoadCallback.Success
                    && int.TryParse(
                        correspondanceLoadCallback.Groups[1].Value,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out int identifiantJeuCallback
                    )
                    && identifiantJeuCallback > 0
                )
                {
                    if (exigerCorrespondanceTitre)
                    {
                        continue;
                    }

                    string titreJeu = correspondanceLoadCallback.Groups[2].Value.Trim();

                    if (string.Equals(titreJeu, "(null)", StringComparison.OrdinalIgnoreCase))
                    {
                        titreJeu = string.Empty;
                    }

                    renseignementSecours ??= new RenseignementJeuRA(
                        identifiantJeuCallback,
                        titreJeu
                    );
                    continue;
                }

                Match correspondance = JournalGameIdRegex().Match(ligne);

                if (
                    correspondance.Success
                    && int.TryParse(
                        correspondance.Groups[1].Value,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out int identifiantJeu
                    )
                    && identifiantJeu > 0
                )
                {
                    if (exigerCorrespondanceTitre)
                    {
                        continue;
                    }

                    renseignementSecours ??= new RenseignementJeuRA(identifiantJeu, string.Empty);
                }
            }

            return renseignementSecours is null
                ? null
                : MemoriserRenseignementPPSSPP(renseignementSecours);
        }
        catch { }

        return null;
    }

    /*
     * Déduit un titre probable de PPSSPP à partir du chemin du jeu.
     */
    private static string ExtraireTitrePPSSPPDepuisCheminJeu(string cheminJeu)
    {
        if (string.IsNullOrWhiteSpace(cheminJeu))
        {
            return string.Empty;
        }

        string titre = Path.GetFileNameWithoutExtension(cheminJeu).Trim();

        if (string.IsNullOrWhiteSpace(titre))
        {
            return string.Empty;
        }

        titre = TexteEntreParenthesesRegex().Replace(titre, " ");
        titre = titre.Replace("\u00AE", string.Empty, StringComparison.Ordinal);
        return EspacesMultiplesRegex().Replace(titre, " ").Trim();
    }

    /*
     * Lit le chemin du jeu PPSSPP depuis son journal local.
     */
    private static string LireCheminJeuPPSSPPDepuisLog()
    {
        try
        {
            string cheminJournal = ServiceSourcesLocalesEmulateurs.TrouverCheminJournalPPSSPP();

            if (string.IsNullOrWhiteSpace(cheminJournal) || !File.Exists(cheminJournal))
            {
                return string.Empty;
            }

            foreach (
                string ligne in ServiceSourcesLocalesEmulateurs
                    .LireToutesLesLignesAvecPartage(cheminJournal)
                    .AsEnumerable()
                    .Reverse()
            )
            {
                Match correspondanceDemarrage = PPSSPPJeuDemarreRegex().Match(ligne);

                if (!correspondanceDemarrage.Success)
                {
                    continue;
                }

                string cheminNormalise = NormaliserCheminJeuProbable(
                    correspondanceDemarrage.Groups[1].Value.Trim()
                );

                if (!string.IsNullOrWhiteSpace(cheminNormalise))
                {
                    return cheminNormalise;
                }
            }
        }
        catch { }

        return string.Empty;
    }

    /*
     * Mémorise en cache le dernier renseignement jeu PPSSPP.
     */
    private static RenseignementJeuRA? MemoriserRenseignementPPSSPP(
        RenseignementJeuRA renseignement
    )
    {
        lock (VerrouCachePPSSPP)
        {
            _dernierRenseignementPPSSPP = renseignement;
            _dernierHorodatageRenseignementPPSSPPUtc = DateTime.UtcNow;
            return _dernierRenseignementPPSSPP;
        }
    }

    /*
     * Retourne le dernier renseignement jeu PPSSPP encore valide depuis le cache.
     */
    private static RenseignementJeuRA? LireRenseignementJeuPPSSPPDepuisCache(string titreJeuAttendu)
    {
        lock (VerrouCachePPSSPP)
        {
            if (
                _dernierRenseignementPPSSPP is null
                || DateTime.UtcNow - _dernierHorodatageRenseignementPPSSPPUtc
                    > TimeSpan.FromSeconds(10)
            )
            {
                return null;
            }

            if (
                string.IsNullOrWhiteSpace(titreJeuAttendu)
                || string.IsNullOrWhiteSpace(_dernierRenseignementPPSSPP.TitreJeu)
                || !TitresSemblables(titreJeuAttendu, _dernierRenseignementPPSSPP.TitreJeu)
            )
            {
                return null;
            }

            return _dernierRenseignementPPSSPP;
        }
    }

    /*
     * Mémorise en cache le dernier renseignement jeu RetroArch.
     */
    private static RenseignementJeuRA? MemoriserRenseignementRetroArch(
        RenseignementJeuRA renseignement
    )
    {
        lock (VerrouCacheRetroArch)
        {
            _dernierRenseignementRetroArch = renseignement;
            _dernierHorodatageRenseignementRetroArchUtc = DateTime.UtcNow;
            return _dernierRenseignementRetroArch;
        }
    }

    /*
     * Retourne le dernier renseignement jeu RetroArch encore valide depuis le cache.
     */
    private static RenseignementJeuRA? LireRenseignementJeuRetroArchDepuisCache()
    {
        lock (VerrouCacheRetroArch)
        {
            if (
                _dernierRenseignementRetroArch is null
                || DateTime.UtcNow - _dernierHorodatageRenseignementRetroArchUtc
                    > TimeSpan.FromSeconds(10)
            )
            {
                return null;
            }

            return _dernierRenseignementRetroArch;
        }
    }

    /*
     * Lit le renseignement jeu courant de Project64 depuis RACache.
     */
    private static RenseignementJeuRA? LireRenseignementJeuProject64DepuisRACache(
        string nomEmulateur
    )
    {
        try
        {
            string repertoireRACache =
                ServiceSourcesLocalesEmulateurs.TrouverRepertoireRACacheProject64(nomEmulateur);

            if (string.IsNullOrWhiteSpace(repertoireRACache))
            {
                return null;
            }

            string cheminJournal = Path.Combine(repertoireRACache, "RALog.txt");
            if (File.Exists(cheminJournal))
            {
                DateTime horodatageJournal = File.GetLastWriteTimeUtc(cheminJournal);

                if (DateTime.UtcNow - horodatageJournal <= TimeSpan.FromMinutes(15))
                {
                    int identifiantJeu = LireDernierIdentifiantJeuDepuisJournalRA(cheminJournal);

                    if (identifiantJeu > 0)
                    {
                        RenseignementJeuRA renseignement =
                            LireRenseignementJeuProject64DepuisFichierData(
                                repertoireRACache,
                                identifiantJeu
                            ) ?? new RenseignementJeuRA(identifiantJeu, string.Empty);
                        return MemoriserRenseignementProject64(nomEmulateur, renseignement);
                    }
                }
            }

            RenseignementJeuRA? dernierRenseignement =
                LireDernierRenseignementJeuProject64DepuisData(repertoireRACache);

            return dernierRenseignement is null
                ? LireRenseignementJeuProject64DepuisCache(nomEmulateur)
                : MemoriserRenseignementProject64(nomEmulateur, dernierRenseignement);
        }
        catch
        {
            return LireRenseignementJeuProject64DepuisCache(nomEmulateur);
        }
    }

    /*
     * Mémorise en cache le dernier renseignement jeu Project64.
     */
    private static RenseignementJeuRA? MemoriserRenseignementProject64(
        string nomEmulateur,
        RenseignementJeuRA renseignement
    )
    {
        if (string.IsNullOrWhiteSpace(nomEmulateur))
        {
            return renseignement;
        }

        lock (VerrouCacheProject64)
        {
            _cacheRenseignementProject64[nomEmulateur] = renseignement;
            _cacheHorodatageRenseignementProject64Utc[nomEmulateur] = DateTime.UtcNow;
            return renseignement;
        }
    }

    /*
     * Retourne le dernier renseignement jeu Project64 encore valide depuis le cache.
     */
    private static RenseignementJeuRA? LireRenseignementJeuProject64DepuisCache(string nomEmulateur)
    {
        if (string.IsNullOrWhiteSpace(nomEmulateur))
        {
            return null;
        }

        lock (VerrouCacheProject64)
        {
            if (
                !_cacheRenseignementProject64.TryGetValue(
                    nomEmulateur,
                    out RenseignementJeuRA? renseignement
                )
                || !_cacheHorodatageRenseignementProject64Utc.TryGetValue(
                    nomEmulateur,
                    out DateTime horodatage
                )
                || DateTime.UtcNow - horodatage > TimeSpan.FromSeconds(20)
            )
            {
                return null;
            }

            return renseignement;
        }
    }

    /*
     * Lit le renseignement jeu courant de RANes depuis RACache.
     */
    private static RenseignementJeuRA? LireRenseignementJeuRANesDepuisRACache()
    {
        return LireRenseignementJeuDepuisRACache(
            ServiceSourcesLocalesEmulateurs.TrouverRepertoireRACacheRANes()
        );
    }

    /*
     * Lit le renseignement jeu courant de RAVBA depuis RACache.
     */
    private static RenseignementJeuRA? LireRenseignementJeuRAVBADepuisRACache()
    {
        return LireRenseignementJeuDepuisRACache(
            ServiceSourcesLocalesEmulateurs.TrouverRepertoireRACacheRAVBA()
        );
    }

    /*
     * Lit le renseignement jeu courant de RASnes9x depuis RACache.
     */
    private static RenseignementJeuRA? LireRenseignementJeuRASnes9xDepuisRACache()
    {
        return LireRenseignementJeuDepuisRACache(
            ServiceSourcesLocalesEmulateurs.TrouverRepertoireRACacheRASnes9x()
        );
    }

    /*
     * Lit un renseignement jeu générique depuis un répertoire RACache.
     */
    private static RenseignementJeuRA? LireRenseignementJeuDepuisRACache(string repertoireRACache)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(repertoireRACache))
            {
                return null;
            }

            string cheminJournal = Path.Combine(repertoireRACache, "RALog.txt");

            if (File.Exists(cheminJournal))
            {
                DateTime horodatageJournal = File.GetLastWriteTimeUtc(cheminJournal);

                if (DateTime.UtcNow - horodatageJournal <= TimeSpan.FromMinutes(15))
                {
                    int identifiantJeu = LireDernierIdentifiantJeuDepuisJournalRA(cheminJournal);

                    if (identifiantJeu > 0)
                    {
                        return LireRenseignementJeuProject64DepuisFichierData(
                                repertoireRACache,
                                identifiantJeu
                            ) ?? new RenseignementJeuRA(identifiantJeu, string.Empty);
                    }
                }
            }

            return LireDernierRenseignementJeuProject64DepuisData(repertoireRACache);
        }
        catch
        {
            return null;
        }
    }

    /*
     * Lit le renseignement jeu courant de RALibretro depuis RACache.
     */
    private static RenseignementJeuRA? LireRenseignementJeuRALibretroDepuisRACache()
    {
        try
        {
            string repertoireRACache =
                ServiceSourcesLocalesEmulateurs.TrouverRepertoireRACacheRALibretro();
            RenseignementJeuRA? renseignementConfiguration =
                LireRenseignementJeuRALibretroDepuisConfiguration();

            if (string.IsNullOrWhiteSpace(repertoireRACache))
            {
                return renseignementConfiguration ?? LireRenseignementJeuRALibretroDepuisCache();
            }

            RenseignementJeuRA? dernierRenseignement =
                LireDernierRenseignementJeuProject64DepuisData(repertoireRACache);

            if (dernierRenseignement is not null)
            {
                if (
                    string.IsNullOrWhiteSpace(dernierRenseignement.TitreJeu)
                    && renseignementConfiguration is not null
                    && !string.IsNullOrWhiteSpace(renseignementConfiguration.TitreJeu)
                )
                {
                    dernierRenseignement = dernierRenseignement with
                    {
                        TitreJeu = renseignementConfiguration.TitreJeu,
                    };
                }

                return MemoriserRenseignementRALibretro(dernierRenseignement);
            }

            string cheminJournal = Path.Combine(repertoireRACache, "RALog.txt");

            if (File.Exists(cheminJournal))
            {
                DateTime horodatageJournal = File.GetLastWriteTimeUtc(cheminJournal);

                if (DateTime.UtcNow - horodatageJournal <= TimeSpan.FromMinutes(15))
                {
                    int identifiantJeu = LireDernierIdentifiantJeuDepuisJournalRA(cheminJournal);

                    if (identifiantJeu > 0)
                    {
                        RenseignementJeuRA renseignement =
                            LireRenseignementJeuProject64DepuisFichierData(
                                repertoireRACache,
                                identifiantJeu
                            ) ?? new RenseignementJeuRA(identifiantJeu, string.Empty);

                        if (
                            renseignementConfiguration is not null
                            && !string.IsNullOrWhiteSpace(renseignementConfiguration.TitreJeu)
                            && !string.IsNullOrWhiteSpace(renseignement.TitreJeu)
                            && !TitresSemblables(
                                renseignementConfiguration.TitreJeu,
                                renseignement.TitreJeu
                            )
                        )
                        {
                            return renseignementConfiguration;
                        }

                        if (
                            string.IsNullOrWhiteSpace(renseignement.TitreJeu)
                            && renseignementConfiguration is not null
                            && !string.IsNullOrWhiteSpace(renseignementConfiguration.TitreJeu)
                        )
                        {
                            renseignement = renseignement with
                            {
                                TitreJeu = renseignementConfiguration.TitreJeu,
                            };
                        }

                        return MemoriserRenseignementRALibretro(renseignement);
                    }
                }
            }

            return renseignementConfiguration ?? LireRenseignementJeuRALibretroDepuisCache();
        }
        catch
        {
            return LireRenseignementJeuRALibretroDepuisCache();
        }
    }

    /*
     * Mémorise en cache le dernier renseignement jeu RALibretro.
     */
    private static RenseignementJeuRA? MemoriserRenseignementRALibretro(
        RenseignementJeuRA renseignement
    )
    {
        lock (VerrouCacheRALibretro)
        {
            _dernierRenseignementRALibretro = renseignement;
            _dernierHorodatageRenseignementRALibretroUtc = DateTime.UtcNow;
            return _dernierRenseignementRALibretro;
        }
    }

    /*
     * Retourne le dernier renseignement jeu RALibretro encore valide depuis le cache.
     */
    private static RenseignementJeuRA? LireRenseignementJeuRALibretroDepuisCache()
    {
        lock (VerrouCacheRALibretro)
        {
            if (
                _dernierRenseignementRALibretro is null
                || DateTime.UtcNow - _dernierHorodatageRenseignementRALibretroUtc
                    > TimeSpan.FromSeconds(5)
            )
            {
                return null;
            }

            return _dernierRenseignementRALibretro;
        }
    }

    /*
     * Lit le renseignement jeu RALibretro directement depuis la configuration.
     */
    private static RenseignementJeuRA? LireRenseignementJeuRALibretroDepuisConfiguration()
    {
        string cheminConfiguration =
            ServiceSourcesLocalesEmulateurs.TrouverCheminConfigurationRALibretro();

        if (string.IsNullOrWhiteSpace(cheminConfiguration) || !File.Exists(cheminConfiguration))
        {
            return null;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(
                File.ReadAllText(cheminConfiguration, Encoding.UTF8)
            );

            if (
                !document.RootElement.TryGetProperty("recent", out JsonElement recent)
                || recent.ValueKind != JsonValueKind.Array
                || recent.GetArrayLength() == 0
            )
            {
                return null;
            }

            JsonElement premierRecent = recent[0];

            if (
                !premierRecent.TryGetProperty("path", out JsonElement path)
                || path.ValueKind != JsonValueKind.String
            )
            {
                return null;
            }

            string cheminJeu = path.GetString()?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(cheminJeu))
            {
                return null;
            }

            string titreJeu = NettoyerNomFichierJeu(Path.GetFileNameWithoutExtension(cheminJeu));
            return string.IsNullOrWhiteSpace(titreJeu) ? null : new RenseignementJeuRA(0, titreJeu);
        }
        catch
        {
            return null;
        }
    }

    /*
     * Lit le renseignement jeu BizHawk directement depuis la configuration locale.
     */
    private static RenseignementJeuRA? LireRenseignementJeuBizHawkDepuisConfiguration()
    {
        string cheminJournal = ServiceSourcesLocalesEmulateurs.TrouverCheminJournalJeuBizHawk();

        if (string.IsNullOrWhiteSpace(cheminJournal) || !File.Exists(cheminJournal))
        {
            return null;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(
                File.ReadAllText(cheminJournal, Encoding.UTF8)
            );

            if (
                !document.RootElement.TryGetProperty("rom_loaded", out JsonElement romLoaded)
                || romLoaded.ValueKind != JsonValueKind.True
            )
            {
                return null;
            }

            if (
                !document.RootElement.TryGetProperty("game", out JsonElement game)
                || game.ValueKind != JsonValueKind.Object
            )
            {
                return null;
            }

            int identifiantJeu =
                game.TryGetProperty("game_id", out JsonElement gameId)
                && gameId.ValueKind == JsonValueKind.Number
                && gameId.TryGetInt32(out int valeurGameId)
                    ? valeurGameId
                    : 0;

            string titreJeu =
                game.TryGetProperty("title", out JsonElement title)
                && title.ValueKind == JsonValueKind.String
                    ? title.GetString()?.Trim() ?? string.Empty
                    : string.Empty;

            if (identifiantJeu <= 0 && string.IsNullOrWhiteSpace(titreJeu))
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(titreJeu))
            {
                string cheminJeu = LireCheminJeuBizHawkDepuisConfiguration();
                titreJeu = NettoyerNomFichierJeu(Path.GetFileNameWithoutExtension(cheminJeu));
            }

            return new RenseignementJeuRA(identifiantJeu, titreJeu);
        }
        catch
        {
            return null;
        }
    }

    /*
     * Lit le chemin du jeu BizHawk depuis sa configuration locale.
     */
    private static string LireCheminJeuBizHawkDepuisConfiguration()
    {
        string cheminConfiguration =
            ServiceSourcesLocalesEmulateurs.TrouverCheminConfigurationBizHawk();

        if (string.IsNullOrWhiteSpace(cheminConfiguration) || !File.Exists(cheminConfiguration))
        {
            return string.Empty;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(
                File.ReadAllText(cheminConfiguration, Encoding.UTF8)
            );

            if (
                document.RootElement.TryGetProperty("RecentRoms", out JsonElement recentRoms)
                && recentRoms.ValueKind == JsonValueKind.Object
                && recentRoms.TryGetProperty("recentlist", out JsonElement recentList)
                && recentList.ValueKind == JsonValueKind.Array
            )
            {
                foreach (JsonElement entree in recentList.EnumerateArray())
                {
                    if (entree.ValueKind != JsonValueKind.String)
                    {
                        continue;
                    }

                    string valeur = entree.GetString()?.Trim() ?? string.Empty;

                    if (string.IsNullOrWhiteSpace(valeur))
                    {
                        continue;
                    }

                    string cheminNormalise = valeur.StartsWith(
                        "*OpenRom*",
                        StringComparison.Ordinal
                    )
                        ? valeur["*OpenRom*".Length..].Trim()
                        : valeur;

                    cheminNormalise = NormaliserCheminJeuProbable(cheminNormalise);

                    if (!string.IsNullOrWhiteSpace(cheminNormalise) && File.Exists(cheminNormalise))
                    {
                        return cheminNormalise;
                    }
                }
            }
        }
        catch { }

        return string.Empty;
    }

    /*
     * Lit le dernier succès débloqué BizHawk depuis son journal de jeu local.
     */
    private static SuccesDebloqueDetecte? LireDernierSuccesDebloqueBizHawkDepuisJournalJeu(
        int identifiantJeu,
        string titreJeu,
        IReadOnlyCollection<GameAchievementV2> succesConnus
    )
    {
        string cheminJournal = ServiceSourcesLocalesEmulateurs.TrouverCheminJournalJeuBizHawk();

        if (string.IsNullOrWhiteSpace(cheminJournal) || !File.Exists(cheminJournal))
        {
            return null;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(
                File.ReadAllText(cheminJournal, Encoding.UTF8)
            );

            if (
                !document.RootElement.TryGetProperty("rom_loaded", out JsonElement romLoaded)
                || romLoaded.ValueKind != JsonValueKind.True
            )
            {
                return null;
            }

            if (
                !document.RootElement.TryGetProperty("game", out JsonElement jeu)
                || jeu.ValueKind != JsonValueKind.Object
            )
            {
                return null;
            }

            int identifiantJeuJournal =
                jeu.TryGetProperty("game_id", out JsonElement gameId)
                && gameId.ValueKind == JsonValueKind.Number
                && gameId.TryGetInt32(out int valeurGameId)
                    ? valeurGameId
                    : 0;

            if (identifiantJeuJournal <= 0)
            {
                return null;
            }

            if (identifiantJeu > 0 && identifiantJeuJournal != identifiantJeu)
            {
                ReinitialiserCacheSuccesBizHawk(identifiantJeuJournal);
                return null;
            }

            if (
                !document.RootElement.TryGetProperty("achievements", out JsonElement succes)
                || succes.ValueKind != JsonValueKind.Array
            )
            {
                return null;
            }

            List<(int Id, string Titre, bool Softcore, bool Hardcore)> etatsActuels = [];

            foreach (JsonElement succesElement in succes.EnumerateArray())
            {
                if (succesElement.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                int identifiantSucces =
                    succesElement.TryGetProperty("id", out JsonElement id)
                    && id.ValueKind == JsonValueKind.Number
                    && id.TryGetInt32(out int valeurId)
                        ? valeurId
                        : 0;

                if (identifiantSucces <= 0)
                {
                    continue;
                }

                string titreSucces =
                    succesElement.TryGetProperty("title", out JsonElement titreElement)
                    && titreElement.ValueKind == JsonValueKind.String
                        ? titreElement.GetString()?.Trim() ?? string.Empty
                        : string.Empty;

                bool debloqueSoftcore = LireBooleenBizHawk(succesElement, "unlocked_softcore");
                bool debloqueHardcore = LireBooleenBizHawk(succesElement, "unlocked_hardcore");

                etatsActuels.Add(
                    (identifiantSucces, titreSucces, debloqueSoftcore, debloqueHardcore)
                );
            }

            if (etatsActuels.Count == 0)
            {
                return null;
            }

            lock (VerrouCacheSuccesBizHawk)
            {
                if (_dernierIdentifiantJeuSuccesBizHawk != identifiantJeuJournal)
                {
                    _dernierIdentifiantJeuSuccesBizHawk = identifiantJeuJournal;
                    _cacheSuccesBizHawk = ConstruireCacheSuccesBizHawk(etatsActuels);
                    return null;
                }

                (int Id, string Titre, bool Softcore, bool Hardcore)? changementDetecte = null;

                foreach (
                    (
                        int Id,
                        string Titre,
                        bool Softcore,
                        bool Hardcore
                    ) succesActuel in etatsActuels
                )
                {
                    if (
                        !_cacheSuccesBizHawk.TryGetValue(
                            succesActuel.Id,
                            out EtatSuccesBizHawk? etatPrecedent
                        )
                    )
                    {
                        continue;
                    }

                    bool softcoreVientDEtreDebloque =
                        !etatPrecedent.DebloqueSoftcore && succesActuel.Softcore;
                    bool hardcoreVientDEtreDebloque =
                        !etatPrecedent.DebloqueHardcore && succesActuel.Hardcore;

                    if (!softcoreVientDEtreDebloque && !hardcoreVientDEtreDebloque)
                    {
                        continue;
                    }

                    changementDetecte = succesActuel;

                    if (hardcoreVientDEtreDebloque)
                    {
                        break;
                    }
                }

                _cacheSuccesBizHawk = ConstruireCacheSuccesBizHawk(etatsActuels);

                if (changementDetecte is null)
                {
                    return null;
                }

                GameAchievementV2? succesConnu = succesConnus.FirstOrDefault(item =>
                    item.Id == changementDetecte.Value.Id
                );

                return new SuccesDebloqueDetecte
                {
                    IdentifiantJeu = identifiantJeuJournal,
                    TitreJeu = titreJeu?.Trim() ?? string.Empty,
                    IdentifiantSucces = changementDetecte.Value.Id,
                    TitreSucces =
                        succesConnu?.Title?.Trim() ?? changementDetecte.Value.Titre ?? string.Empty,
                    Points = succesConnu?.Points ?? 0,
                    Hardcore = changementDetecte.Value.Hardcore,
                    DateObtention = DateTimeOffset.Now.ToString(
                        "yyyy-MM-dd HH:mm:ss",
                        CultureInfo.InvariantCulture
                    ),
                };
            }
        }
        catch
        {
            return null;
        }
    }

    /*
     * Construit le cache d'état des succès BizHawk à partir du journal courant.
     */
    private static Dictionary<int, EtatSuccesBizHawk> ConstruireCacheSuccesBizHawk(
        IEnumerable<(int Id, string Titre, bool Softcore, bool Hardcore)> etats
    )
    {
        return etats.ToDictionary(
            succes => succes.Id,
            succes => new EtatSuccesBizHawk(succes.Softcore, succes.Hardcore)
        );
    }

    /*
     * Réinitialise le cache BizHawk lorsqu'un nouveau jeu est détecté.
     */
    private static void ReinitialiserCacheSuccesBizHawk(int identifiantJeu)
    {
        lock (VerrouCacheSuccesBizHawk)
        {
            _dernierIdentifiantJeuSuccesBizHawk = identifiantJeu;
            _cacheSuccesBizHawk = [];
        }
    }

    /*
     * Lit un booléen BizHawk dans un élément JSON avec tolérance aux formats.
     */
    private static bool LireBooleenBizHawk(JsonElement parent, string nomPropriete)
    {
        if (!parent.TryGetProperty(nomPropriete, out JsonElement valeur))
        {
            return false;
        }

        return valeur.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number => valeur.TryGetInt32(out int nombre) && nombre != 0,
            JsonValueKind.String => bool.TryParse(valeur.GetString(), out bool booleen)
                ? booleen
                : int.TryParse(
                    valeur.GetString(),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int nombreTexte
                )
                    && nombreTexte != 0,
            _ => false,
        };
    }

    /*
     * Lit le dernier succès débloqué à partir de la source locale adaptée
     * à l'émulateur courant.
     */
    public static SuccesDebloqueDetecte? LireDernierSuccesDebloqueDepuisSourceLocale(
        string nomEmulateur,
        int identifiantJeu,
        string titreJeu,
        IReadOnlyCollection<GameAchievementV2> succesConnus
    )
    {
        if (identifiantJeu <= 0)
        {
            return null;
        }

        try
        {
            if (string.Equals(nomEmulateur, "BizHawk", StringComparison.Ordinal))
            {
                return LireDernierSuccesDebloqueBizHawkDepuisJournalJeu(
                    identifiantJeu,
                    titreJeu,
                    succesConnus
                );
            }

            string cheminJournal = ServiceSourcesLocalesEmulateurs.TrouverCheminJournalSuccesLocal(
                nomEmulateur
            );

            if (string.IsNullOrWhiteSpace(cheminJournal) || !File.Exists(cheminJournal))
            {
                return null;
            }

            RenseignementSuccesRA? renseignement = LireDernierSuccesDepuisJournalRA(cheminJournal);

            if (renseignement is null)
            {
                return null;
            }

            GameAchievementV2? succes = succesConnus.FirstOrDefault(item =>
                item.Id == renseignement.IdentifiantSucces
            );

            return new SuccesDebloqueDetecte
            {
                IdentifiantJeu = identifiantJeu,
                TitreJeu = titreJeu?.Trim() ?? string.Empty,
                IdentifiantSucces = renseignement.IdentifiantSucces,
                TitreSucces = succes?.Title?.Trim() ?? renseignement.TitreSucces,
                Points = succes?.Points ?? 0,
                Hardcore = true,
                DateObtention = DateTimeOffset.Now.ToString(
                    "yyyy-MM-dd HH:mm:ss",
                    CultureInfo.InvariantCulture
                ),
            };
        }
        catch
        {
            return null;
        }
    }

    /*
     * Lit le dernier succès débloqué pour RALibretro à partir des journaux RA.
     */
    public static SuccesDebloqueDetecte? LireDernierSuccesDebloqueRALibretro(
        int identifiantJeu,
        string titreJeu,
        IReadOnlyCollection<GameAchievementV2> succesConnus
    )
    {
        return LireDernierSuccesDebloqueDepuisSourceLocale(
            "RALibretro",
            identifiantJeu,
            titreJeu,
            succesConnus
        );
    }

    /*
     * Lit le dernier identifiant de jeu mentionné dans un journal RA.
     */
    private static int LireDernierIdentifiantJeuDepuisJournalRA(string cheminJournal)
    {
        try
        {
            List<string> lignes = ServiceSourcesLocalesEmulateurs.LireToutesLesLignesAvecPartage(
                cheminJournal
            );

            foreach (string ligne in lignes.AsEnumerable().Reverse())
            {
                Match correspondance = JournalGameIdRegex().Match(ligne);

                if (
                    correspondance.Success
                    && int.TryParse(
                        correspondance.Groups[1].Value,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out int identifiantJeu
                    )
                )
                {
                    return identifiantJeu;
                }
            }
        }
        catch { }

        return 0;
    }

    /*
     * Lit le dernier succès mentionné dans un journal RA.
     */
    private static RenseignementSuccesRA? LireDernierSuccesDepuisJournalRA(string cheminJournal)
    {
        try
        {
            List<string> lignes = ServiceSourcesLocalesEmulateurs.LireToutesLesLignesAvecPartage(
                cheminJournal
            );

            foreach (string ligne in lignes.AsEnumerable().Reverse())
            {
                Match correspondanceAttribue = JournalSuccesAttribueRegex().Match(ligne);

                if (
                    correspondanceAttribue.Success
                    && int.TryParse(
                        correspondanceAttribue.Groups[1].Value,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out int identifiantSuccesAttribue
                    )
                )
                {
                    return new RenseignementSuccesRA(identifiantSuccesAttribue, string.Empty);
                }

                Match correspondanceAttribution = JournalSuccesAttributionRegex().Match(ligne);

                if (
                    correspondanceAttribution.Success
                    && int.TryParse(
                        correspondanceAttribution.Groups[1].Value,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out int identifiantSuccesAttribution
                    )
                )
                {
                    return new RenseignementSuccesRA(
                        identifiantSuccesAttribution,
                        correspondanceAttribution.Groups[2].Value.Trim()
                    );
                }

                Match correspondanceDeblocageDuckStation = JournalSuccesDebloqueDuckStationRegex()
                    .Match(ligne);

                if (
                    correspondanceDeblocageDuckStation.Success
                    && int.TryParse(
                        correspondanceDeblocageDuckStation.Groups[1].Value,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out int identifiantSuccesDebloqueDuckStation
                    )
                )
                {
                    return new RenseignementSuccesRA(
                        identifiantSuccesDebloqueDuckStation,
                        correspondanceDeblocageDuckStation.Groups[2].Value.Trim()
                    );
                }

                Match correspondanceDeblocageFlycast = JournalSuccesDebloqueFlycastRegex()
                    .Match(ligne);

                if (
                    correspondanceDeblocageFlycast.Success
                    && int.TryParse(
                        correspondanceDeblocageFlycast.Groups[2].Value,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out int identifiantSuccesDebloqueFlycast
                    )
                )
                {
                    return new RenseignementSuccesRA(
                        identifiantSuccesDebloqueFlycast,
                        correspondanceDeblocageFlycast.Groups[1].Value.Trim()
                    );
                }
            }
        }
        catch { }

        return null;
    }

    /*
     * Lit le dernier renseignement jeu Project64 depuis les fichiers Data.
     */
    private static RenseignementJeuRA? LireDernierRenseignementJeuProject64DepuisData(
        string repertoireRACache
    )
    {
        try
        {
            string repertoireData = Path.Combine(repertoireRACache, "Data");

            if (!Directory.Exists(repertoireData))
            {
                return null;
            }

            FileInfo? fichierJeuRecent = new DirectoryInfo(repertoireData)
                .EnumerateFiles("*.json", SearchOption.TopDirectoryOnly)
                .Where(fichier => FichierDonneesJeuRegex().IsMatch(fichier.Name))
                .OrderByDescending(fichier => fichier.LastWriteTimeUtc)
                .FirstOrDefault();

            if (fichierJeuRecent is null)
            {
                return null;
            }

            if (DateTime.UtcNow - fichierJeuRecent.LastWriteTimeUtc > TimeSpan.FromMinutes(15))
            {
                return null;
            }

            string nomBase = Path.GetFileNameWithoutExtension(fichierJeuRecent.Name);

            if (
                !int.TryParse(
                    nomBase,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int identifiantJeu
                )
            )
            {
                return null;
            }

            return LireRenseignementJeuProject64DepuisFichierData(
                repertoireRACache,
                identifiantJeu
            );
        }
        catch
        {
            return null;
        }
    }

    /*
     * Lit un renseignement jeu Project64 depuis un fichier Data précis.
     */
    private static RenseignementJeuRA? LireRenseignementJeuProject64DepuisFichierData(
        string repertoireRACache,
        int identifiantJeu
    )
    {
        try
        {
            string cheminDonneesJeu = Path.Combine(
                repertoireRACache,
                "Data",
                $"{identifiantJeu}.json"
            );

            if (!File.Exists(cheminDonneesJeu))
            {
                return null;
            }

            string contenu = string.Join(
                Environment.NewLine,
                ServiceSourcesLocalesEmulateurs.LireToutesLesLignesAvecPartage(cheminDonneesJeu)
            );

            if (string.IsNullOrWhiteSpace(contenu))
            {
                return null;
            }

            using JsonDocument document = JsonDocument.Parse(contenu);
            string titreJeu = string.Empty;

            if (
                document.RootElement.TryGetProperty("Title", out JsonElement titre)
                && titre.ValueKind == JsonValueKind.String
            )
            {
                titreJeu = titre.GetString()?.Trim() ?? string.Empty;
            }

            return new RenseignementJeuRA(identifiantJeu, titreJeu);
        }
        catch
        {
            return null;
        }
    }

    /*
     * Nettoie un nom de fichier de jeu pour l'utiliser comme titre probable.
     */
    private static string NettoyerNomFichierJeu(string nomFichier)
    {
        if (string.IsNullOrWhiteSpace(nomFichier))
        {
            return string.Empty;
        }

        string resultat = nomFichier.Trim();
        resultat = resultat.Replace('_', ' ');
        resultat = EspacesMultiplesRegex().Replace(resultat, " ").Trim();
        return resultat;
    }

    /*
     * Tente d'extraire un titre DuckStation depuis une carte mémoire récente.
     */
    private static string ExtraireTitreDuckStationDepuisMemcardRecente()
    {
        try
        {
            string repertoireDuckStation =
                ServiceSourcesLocalesEmulateurs.TrouverRepertoireDuckStation();

            if (string.IsNullOrWhiteSpace(repertoireDuckStation))
            {
                return string.Empty;
            }

            string repertoireMemcards = Path.Combine(repertoireDuckStation, "memcards");

            if (!Directory.Exists(repertoireMemcards))
            {
                return string.Empty;
            }

            FileInfo? memcardRecente = new DirectoryInfo(repertoireMemcards)
                .EnumerateFiles("*.mcd", SearchOption.TopDirectoryOnly)
                .OrderByDescending(fichier => fichier.LastWriteTimeUtc)
                .FirstOrDefault();

            if (memcardRecente is null)
            {
                return string.Empty;
            }

            if (DateTime.UtcNow - memcardRecente.LastWriteTimeUtc > TimeSpan.FromMinutes(15))
            {
                return string.Empty;
            }

            string nomBase = Path.GetFileNameWithoutExtension(memcardRecente.Name);
            nomBase = SuffixeSlotCarteMemoireRegex().Replace(nomBase, string.Empty);

            if (SerialJeuPlayStationRegex().IsMatch(nomBase))
            {
                string cheminJeu = ResoudreCheminJeuDuckStationDepuisSerial(
                    repertoireDuckStation,
                    nomBase
                );

                if (!string.IsNullOrWhiteSpace(cheminJeu))
                {
                    return NettoyerNomFichierJeu(Path.GetFileNameWithoutExtension(cheminJeu));
                }
            }

            return NettoyerNomFichierJeu(nomBase);
        }
        catch
        {
            return string.Empty;
        }
    }

    /*
     * Tente de retrouver le chemin du jeu DuckStation via une carte mémoire récente.
     */
    private static string TrouverCheminJeuDuckStationDepuisMemcardRecente()
    {
        try
        {
            string repertoireDuckStation =
                ServiceSourcesLocalesEmulateurs.TrouverRepertoireDuckStation();

            if (string.IsNullOrWhiteSpace(repertoireDuckStation))
            {
                return string.Empty;
            }

            string repertoireMemcards = Path.Combine(repertoireDuckStation, "memcards");

            if (!Directory.Exists(repertoireMemcards))
            {
                return string.Empty;
            }

            FileInfo? memcardRecente = new DirectoryInfo(repertoireMemcards)
                .EnumerateFiles("*.mcd", SearchOption.TopDirectoryOnly)
                .OrderByDescending(fichier => fichier.LastWriteTimeUtc)
                .FirstOrDefault();

            if (memcardRecente is null)
            {
                return string.Empty;
            }

            if (DateTime.UtcNow - memcardRecente.LastWriteTimeUtc > TimeSpan.FromMinutes(15))
            {
                return string.Empty;
            }

            string nomBase = Path.GetFileNameWithoutExtension(memcardRecente.Name);
            nomBase = SuffixeSlotCarteMemoireRegex().Replace(nomBase, string.Empty);

            if (!SerialJeuPlayStationRegex().IsMatch(nomBase))
            {
                return string.Empty;
            }

            return NormaliserCheminJeuProbable(
                ResoudreCheminJeuDuckStationDepuisSerial(repertoireDuckStation, nomBase)
            );
        }
        catch
        {
            return string.Empty;
        }
    }

    /*
     * Lit le chemin du jeu DuckStation depuis son journal local.
     */
    private static string LireCheminJeuDuckStationDepuisLog()
    {
        try
        {
            string cheminJournal =
                ServiceSourcesLocalesEmulateurs.TrouverCheminJournalDuckStation();

            if (string.IsNullOrWhiteSpace(cheminJournal) || !File.Exists(cheminJournal))
            {
                return TrouverCheminJeuDuckStationDepuisMemcardRecente();
            }

            FileInfo fichierJournal = new(cheminJournal);

            if (
                fichierJournal.Length <= 0
                || DateTime.UtcNow - fichierJournal.LastWriteTimeUtc > TimeSpan.FromMinutes(15)
            )
            {
                return TrouverCheminJeuDuckStationDepuisMemcardRecente();
            }

            foreach (
                string ligne in ServiceSourcesLocalesEmulateurs
                    .LireToutesLesLignesAvecPartage(cheminJournal)
                    .AsEnumerable()
                    .Reverse()
            )
            {
                Match correspondanceBootPath = DuckStationBootPathRegex().Match(ligne);

                if (!correspondanceBootPath.Success)
                {
                    continue;
                }

                string cheminJeu = correspondanceBootPath.Groups[1].Value.Trim();
                string cheminNormalise = NormaliserCheminJeuProbable(cheminJeu);

                if (!string.IsNullOrWhiteSpace(cheminNormalise))
                {
                    return cheminNormalise;
                }
            }
        }
        catch { }

        return TrouverCheminJeuDuckStationDepuisMemcardRecente();
    }

    /*
     * Résout un chemin de jeu DuckStation à partir d'un serial PlayStation.
     */
    private static string ResoudreCheminJeuDuckStationDepuisSerial(
        string repertoireDuckStation,
        string serial
    )
    {
        try
        {
            string cheminGamelist = Path.Combine(repertoireDuckStation, "cache", "gamelist.cache");

            if (!File.Exists(cheminGamelist))
            {
                return string.Empty;
            }

            DateTime horodatage = File.GetLastWriteTimeUtc(cheminGamelist);
            Dictionary<string, string> cache;

            lock (VerrouCacheDuckStation)
            {
                if (
                    !string.Equals(
                        _dernierRepertoireDuckStation,
                        repertoireDuckStation,
                        StringComparison.OrdinalIgnoreCase
                    )
                    || _dernierHorodatageCacheGamelistUtc != horodatage
                    || _cacheSerialVersCheminDuckStation.Count == 0
                )
                {
                    _cacheSerialVersCheminDuckStation = ConstruireCacheSerialDuckStation(
                        cheminGamelist
                    );
                    _dernierRepertoireDuckStation = repertoireDuckStation;
                    _dernierHorodatageCacheGamelistUtc = horodatage;
                }

                cache = _cacheSerialVersCheminDuckStation;
            }

            return cache.TryGetValue(serial, out string? cheminJeu) ? cheminJeu : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    /*
     * Construit le cache de correspondance entre serials et chemins DuckStation.
     */
    private static Dictionary<string, string> ConstruireCacheSerialDuckStation(
        string cheminGamelist
    )
    {
        string[] chaines = ExtraireChainesLisiblesDepuisBinaire(cheminGamelist, 4);
        Dictionary<string, string> correspondances = new(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < chaines.Length - 1; i++)
        {
            string chaine = chaines[i].Trim();
            string suivante = chaines[i + 1].Trim();

            if (!EstCheminJeuDuckStation(chaine) || !EstSerialJeuPlayStation(suivante))
            {
                continue;
            }

            correspondances[suivante] = chaine;
        }

        return correspondances;
    }

    /*
     * Extrait les chaînes lisibles d'un binaire pour y rechercher des indices.
     */
    private static string[] ExtraireChainesLisiblesDepuisBinaire(
        string cheminFichier,
        int longueurMin
    )
    {
        byte[] bytes = File.ReadAllBytes(cheminFichier);
        List<string> chaines = [];
        StringBuilder constructeur = new();

        foreach (byte valeur in bytes)
        {
            if ((valeur >= 32 && valeur <= 126) || valeur == 9)
            {
                constructeur.Append((char)valeur);
            }
            else
            {
                if (constructeur.Length >= longueurMin)
                {
                    chaines.Add(constructeur.ToString());
                }

                constructeur.Clear();
            }
        }

        if (constructeur.Length >= longueurMin)
        {
            chaines.Add(constructeur.ToString());
        }

        return [.. chaines];
    }

    /*
     * Indique si une valeur ressemble à un chemin de jeu DuckStation valide.
     */
    private static bool EstCheminJeuDuckStation(string valeur)
    {
        if (string.IsNullOrWhiteSpace(valeur))
        {
            return false;
        }

        string extension = Path.GetExtension(valeur);
        string[] extensionsJeuPossibles =
        [
            ".cue",
            ".chd",
            ".iso",
            ".bin",
            ".img",
            ".pbp",
            ".m3u",
            ".ecm",
            ".exe",
        ];

        return extensionsJeuPossibles.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }

    /*
     * Indique si une valeur ressemble à un serial de jeu PlayStation.
     */
    private static bool EstSerialJeuPlayStation(string valeur)
    {
        return !string.IsNullOrWhiteSpace(valeur) && SerialJeuPlayStationRegex().IsMatch(valeur);
    }

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    /*
     * Déclare l'expression régulière qui réduit les espaces multiples.
     */
    private static partial Regex EspacesMultiplesRegex();

    [GeneratedRegex(@"^v?\d+(\.\d+)+$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    /*
     * Déclare l'expression régulière qui reconnaît un bloc de version Project64.
     */
    private static partial Regex BlocVersionProject64Regex();

    [GeneratedRegex(@"^\d+(\.\d+)*$", RegexOptions.CultureInvariant)]
    /*
     * Déclare l'expression régulière qui reconnaît une valeur purement numérique.
     */
    private static partial Regex ValeurNumeriqueSeuleRegex();

    [GeneratedRegex(@"[A-Za-z].*[A-Za-z]", RegexOptions.CultureInvariant)]
    /*
     * Déclare l'expression régulière qui vérifie la présence d'au moins deux lettres.
     */
    private static partial Regex ContientDeuxLettresRegex();

    [GeneratedRegex(@"\([^)]*\)", RegexOptions.CultureInvariant)]
    /*
     * Déclare l'expression régulière qui repère le texte placé entre parenthèses.
     */
    private static partial Regex TexteEntreParenthesesRegex();

    [GeneratedRegex(@"[^a-z0-9]+", RegexOptions.CultureInvariant)]
    /*
     * Déclare l'expression régulière qui repère les caractères non alphanumériques.
     */
    private static partial Regex CaracteresNonAlphaNumeriquesRegex();

    [GeneratedRegex("\"([^\"]+)\"|([^\\s]+)", RegexOptions.CultureInvariant)]
    /*
     * Déclare l'expression régulière utilisée pour découper une ligne de commande.
     */
    private static partial Regex JetonsLigneCommandeRegex();

    [GeneratedRegex(
        @"RA:\s*game\s+(\d+)\s+loaded:\s*(.+?),\s*achievements",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase
    )]
    /*
     * Déclare l'expression régulière de détection de jeu chargé dans Flycast.
     */
    private static partial Regex FlycastGameLoadedRegex();

    [GeneratedRegex(
        @"RA:\s*cdreader_open_track\s+(.+?)\s+track\s+\d+",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase
    )]
    /*
     * Déclare l'expression régulière de détection de contenu chargé dans Flycast.
     */
    private static partial Regex FlycastContenuChargeRegex();

    [GeneratedRegex(
        @"(?:Identified game:\s*|Loading game\s+|Starting (?:new )?session for game\s+)(\d+)",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase
    )]
    /*
     * Déclare l'expression régulière qui extrait un Game ID depuis un journal.
     */
    private static partial Regex JournalGameIdRegex();

    [GeneratedRegex(
        @"Awarding achievement\s+(\d+)\s*:\s*(.+)$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase
    )]
    /*
     * Déclare l'expression régulière qui repère une attribution de succès.
     */
    private static partial Regex JournalSuccesAttributionRegex();

    [GeneratedRegex(
        @"Game loaded:\s*'(.+?)'\s*\(ID:\s*(\d+)\s*,",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase
    )]
    /*
     * Déclare l'expression régulière de détection de jeu chargé dans DuckStation.
     */
    private static partial Regex DuckStationGameLoadedRegex();

    [GeneratedRegex(@"Boot Path:\s*(.+)$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    /*
     * Déclare l'expression régulière qui extrait un chemin de boot DuckStation.
     */
    private static partial Regex DuckStationBootPathRegex();

    [GeneratedRegex(
        @"Achievements:\s+Identified game:\s*(\d+)\s+""([^""]+)""",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase
    )]
    /*
     * Déclare l'expression régulière qui repère un jeu identifié dans PCSX2.
     */
    private static partial Regex PCSX2GameIdentifieRegex();

    [GeneratedRegex(
        @"Achievements:\s+Game\s+(\d+)\s+loaded\b",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase
    )]
    /*
     * Déclare l'expression régulière qui repère un jeu chargé dans PCSX2.
     */
    private static partial Regex PCSX2GameChargeRegex();

    [GeneratedRegex(
        @"isoFile open ok:\s*(.+)$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase
    )]
    /*
     * Déclare l'expression régulière qui repère une ISO ouverte dans PCSX2.
     */
    private static partial Regex PCSX2IsoOuverteRegex();

    [GeneratedRegex(
        @"Load callback:\s*(\d+)\s*\(([^)]*)\)",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase
    )]
    /*
     * Déclare l'expression régulière qui repère un chargement PPSSPP.
     */
    private static partial Regex PPSSPPLoadCallbackRegex();

    [GeneratedRegex(
        @"RetroAchievements:\s+Identified game:\s*(\d+)\s+""([^""]+)""",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase
    )]
    /*
     * Déclare l'expression régulière qui repère un jeu identifié dans PPSSPP.
     */
    private static partial Regex PPSSPPGameIdentifieRegex();

    [GeneratedRegex(
        @"RetroAchievements:\s+Game\s+(\d+)\s+loaded\b",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase
    )]
    /*
     * Déclare l'expression régulière qui repère un jeu chargé dans PPSSPP.
     */
    private static partial Regex PPSSPPGameChargeRegex();

    [GeneratedRegex(
        @"Booted\s+(.+?)\.\.\.$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase
    )]
    /*
     * Déclare l'expression régulière qui repère le démarrage d'un jeu PPSSPP.
     */
    private static partial Regex PPSSPPJeuDemarreRegex();

    [GeneratedRegex(
        @"RetroAchievements\]:\s*Identified game:\s*(\d+)\s+""([^""]+)""",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase
    )]
    /*
     * Déclare l'expression régulière qui repère un jeu identifié dans Dolphin.
     */
    private static partial Regex DolphinGameIdentifieRegex();

    [GeneratedRegex(
        @"RetroAchievements\]:\s*Game\s+(\d+)\s+loaded\b",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase
    )]
    /*
     * Déclare l'expression régulière qui repère un jeu chargé dans Dolphin.
     */
    private static partial Regex DolphinGameChargeRegex();

    [GeneratedRegex(
        @"Achievement\s+(\d+)\s+awarded\b",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase
    )]
    /*
     * Déclare l'expression régulière qui repère un succès attribué.
     */
    private static partial Regex JournalSuccesAttribueRegex();

    [GeneratedRegex(
        @"Achievement\s+(\d+)\s+\((.+?)\)\s+for game\s+\d+\s+unlocked\b",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase
    )]
    /*
     * Déclare l'expression régulière qui repère un succès débloqué dans DuckStation.
     */
    private static partial Regex JournalSuccesDebloqueDuckStationRegex();

    [GeneratedRegex(
        @"Achievement\s+(.+?)\s+\((\d+)\)\s+for game\s+.+?\s+unlocked\b",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase
    )]
    /*
     * Déclare l'expression régulière qui repère un succès débloqué dans Flycast.
     */
    private static partial Regex JournalSuccesDebloqueFlycastRegex();

    [GeneratedRegex(@"^\d+\.json$", RegexOptions.CultureInvariant)]
    /*
     * Déclare l'expression régulière qui reconnaît un fichier de données jeu.
     */
    private static partial Regex FichierDonneesJeuRegex();

    [GeneratedRegex(
        @"(?:^|[_\-\s])(?:slot|card)\s*\d+$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase
    )]
    /*
     * Déclare l'expression régulière qui repère un suffixe de slot mémoire.
     */
    private static partial Regex SuffixeSlotCarteMemoireRegex();

    [GeneratedRegex(
        @"^(?:S[CLN][A-Z]{2}|PBPX)[-_ ]?\d{3,5}(?:[-_ ]?\d{2,3})?$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase
    )]
    /*
     * Déclare l'expression régulière qui valide un serial de jeu PlayStation.
     */
    private static partial Regex SerialJeuPlayStationRegex();

    [GeneratedRegex(
        @"^[A-Z]{4}\d{5}\s*:\s*",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase
    )]
    /*
     * Déclare l'expression régulière qui repère un préfixe de serial PPSSPP.
     */
    private static partial Regex PrefixeSerialPpssppRegex();

    [GeneratedRegex(
        @"\s*\(([A-Z0-9]{4,8})\)\s*$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase
    )]
    /*
     * Déclare l'expression régulière qui repère un suffixe de code jeu Dolphin.
     */
    private static partial Regex SuffixeCodeJeuDolphinRegex();

    [GeneratedRegex(
        @"(?:\[[^\]]+\]\s+)*\[RCHEEVOS\]\s+Identified game:\s*(\d+)\s+""([^""]+)""",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase
    )]
    /*
     * Déclare l'expression régulière qui repère un jeu identifié dans RetroArch.
     */
    private static partial Regex RetroArchGameIdentifieRegex();

    [GeneratedRegex(
        @"(?:\[[^\]]+\]\s+)*\[RCHEEVOS\]\s+Starting session for game\s+(\d+)",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase
    )]
    /*
     * Déclare l'expression régulière qui repère une session jeu RetroArch.
     */
    private static partial Regex RetroArchSessionJeuRegex();

    [GeneratedRegex(
        @"(?:\[[^\]]+\]\s+)*\[RCHEEVOS\]\s+Game\s+(\d+)\s+loaded",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase
    )]
    /*
     * Déclare l'expression régulière qui repère un jeu chargé dans RetroArch.
     */
    private static partial Regex RetroArchGameChargeRegex();

    [GeneratedRegex(
        @"(?:\[[^\]]+\]\s+)*\[(?:Core|Content)\]\s+(?:Using content|Chargement du fichier de contenu):\s+""([^""]+)""",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase
    )]
    /*
     * Déclare l'expression régulière qui repère un contenu chargé dans RetroArch.
     */
    private static partial Regex RetroArchContenuChargeRegex();

    [GeneratedRegex(
        @"^(?:Luna'?s?\s+)?Project64\s+v?\d+(?:\.\d+)+$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase
    )]
    /*
     * Déclare l'expression régulière qui reconnaît un titre Project64 réduit à sa version.
     */
    private static partial Regex TitreFenetreProject64VersionSeuleRegex();

    [GeneratedRegex(
        @"^(?:RAP64|RAProject64)\s*-\s*\d+(?:\.\d+)+(?:\s*-\s*[\w\s]+)?$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase
    )]
    /*
     * Déclare l'expression régulière qui reconnaît un titre générique Project64.
     */
    private static partial Regex TitreFenetreProject64GeneriqueRegex();

    [return: MarshalAs(UnmanagedType.Bool)]
    [LibraryImport("user32.dll")]
    /*
     * Déclare l'appel natif Windows utilisé pour énumérer les fenêtres.
     */
    private static partial bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [return: MarshalAs(UnmanagedType.Bool)]
    [LibraryImport("user32.dll")]
    /*
     * Déclare l'appel natif Windows qui indique si une fenêtre est visible.
     */
    private static partial bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    /*
     * Déclare l'appel natif Windows qui lit le texte d'une fenêtre.
     */
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [LibraryImport("user32.dll")]
    /*
     * Déclare l'appel natif Windows qui retourne la longueur du texte d'une fenêtre.
     */
    private static partial int GetWindowTextLength(IntPtr hWnd);

    [LibraryImport("user32.dll")]
    /*
     * Déclare l'appel natif Windows qui relie une fenêtre à son processus.
     */
    private static partial uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [LibraryImport("ntdll.dll")]
    /*
     * Déclare l'appel natif bas niveau utilisé pour lire certaines informations de processus.
     */
    private static partial int NtQueryInformationProcess(
        IntPtr processHandle,
        int processInformationClass,
        IntPtr processInformation,
        int processInformationLength,
        out int returnLength
    );

    [StructLayout(LayoutKind.Sequential)]
    /*
     * Représente une structure Unicode native utilisée par les appels bas niveau.
     */
    private readonly struct UnicodeString
    {
        public readonly ushort Length;
        public readonly ushort MaximumLength;
        public readonly IntPtr Buffer;
    }
}